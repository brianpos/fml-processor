# Generating FHIR Logical Models (StructureDefinitions) from C# Classes

> Status: design exploration / options paper
> Audience: maintainers of `fml-processor`
> Goal: evaluate approaches for a "code‑first" tool that turns C# classes into FHIR
> logical model `StructureDefinition` resources, so that the classes become the
> source of truth for documentation **and** the basis for authoring FML mappings
> to/from that content.

## 1. What we are actually trying to build

The request is a *code‑first* workflow:

1. A developer writes plain C# classes (POCOs) that describe some data shape
   (e.g. an internal DTO, a legacy record layout, an API contract).
2. A tool inspects those classes and emits a FHIR **logical model** —
   a `StructureDefinition` with `kind = logical`, one `ElementDefinition`
   per property, cardinalities, types, and descriptions pulled from the code.
3. Those generated `StructureDefinition`s can then be:
   - published as human‑readable documentation (via the IG publisher / Firely tooling), and
   - used as the `source`/`target` structures in FML `StructureMap`s, which this
     repository already parses, validates and serialises.

So the deliverable is *not* a new FHIR resource type — it is a **logical model**
(the FHIR mechanism for describing arbitrary, non‑resource data structures), and
the tool's job is to project C# type metadata into the `StructureDefinition`
element model.

The two framing questions in the problem statement are the right ones:

- **Is it a code generator?** (i.e. a Roslyn source generator / analyzer that runs at
  compile time)
- **Or a reflection tool?** (i.e. a program that loads the compiled assembly and
  walks the types with `System.Reflection`)

Both are viable. They differ mainly in *when* they run and *what metadata they can
see*. The sections below compare them and then recommend a path that fits this
repository.

## 2. Why this repo is well positioned

`fml-processor` already depends on the **Firely .NET SDK**
(`Hl7.Fhir.R5` + `Hl7.Fhir.Conformance`, see `fml-processor/fml-processor.csproj`).
That matters a lot, because the SDK gives us most of the building blocks for free:

- **`StructureDefinition`, `ElementDefinition`, `ElementDefinition.TypeRefComponent`**
  POCOs and a JSON serializer, so we never hand‑write JSON.
- **`ModelInspector`, `ClassMapping`, `PropertyMapping`** — the SDK's *own*
  reflection layer that already knows how to read FHIR attributes off .NET types.
- The existing FML validator in this repo consumes `StructureDefinition`s through
  `Hl7.Fhir.Specification.Navigation` / `Source`, so anything we generate can be
  fed straight back into the validator and mapping tooling that already exists
  (see `fml-processor/FmlAnnotations.cs`, which works with `ElementDefinition`).

In other words, generating the model is "just" building `StructureDefinition`
objects and calling the serializer — the hard parts (data model, serialization,
downstream consumption) already live in the codebase.

## 3. Option A — Reflection over the compiled assembly (recommended core)

Load the target assembly, enumerate the chosen types, and walk each type's
properties with `System.Reflection`, emitting one `ElementDefinition` per property.

**How it works**

- Resolve the assembly (project reference, or `Assembly.LoadFrom` on a DLL path).
- For each selected class, create a `StructureDefinition` with
  `kind = logical`, `derivation = specialization`, `type =` the canonical/type URL,
  `abstract = false`, and a root `ElementDefinition`.
- For each public property emit a child `ElementDefinition`:
  - **Path** = `RootName.propertyName`.
  - **Cardinality** from nullability + collection‑ness:
    - `List<T>`/`IEnumerable<T>` → `0..*` (or `1..*` if annotated required).
    - Nullable / reference type → `0..1`.
    - Non‑nullable value type or `[Required]` → `1..1`.
  - **Type**:
    - Primitives → FHIR primitives (`string`→`string`, `int`→`integer`,
      `bool`→`boolean`, `DateTime`→`dateTime`, `decimal`→`decimal`, `Guid`→`id`/`uuid`, etc.).
    - A nested class that is *also* in the selection set → a contentReference or a
      `type.code` pointing at that nested logical model's canonical (recurse).
    - `enum` → `code` with a generated `ValueSet`/`binding` (optional, phase 2).
  - **Documentation** = XML `<summary>` doc comments (see §6) or a `[Description]`
    attribute → `ElementDefinition.definition`/`short`.
- Serialize with the Firely JSON serializer to `*.StructureDefinition.json`.

**Pros**

- Smallest, most direct implementation; reuses the Firely POCO + serializer stack
  already referenced here.
- Runs against *any* assembly (including third‑party DLLs you don't have source for).
- Easy to unit test in the existing `fml-tester` MSTest project — build a couple of
  sample POCOs and assert on the emitted `StructureDefinition`.
- Can reuse `ModelInspector`/`ClassMapping` if we decide to honour Firely's own
  `[FhirElement]`/`[Cardinality]`/`[AllowedTypes]` attributes.

**Cons**

- Reflection does **not** see nullable reference‑type annotations trivially — you
  need `NullabilityInfoContext` (available in .NET 6+, and this repo targets net9.0,
  so that's fine) to distinguish `string?` from `string`.
- XML doc comments are **not** in the assembly. To get element descriptions you must
  ship the generated `MyAssembly.xml` doc file alongside the DLL and parse it (see §6),
  or fall back to `[Description]`/`[Display]` attributes.
- Requires loading/executing assembly load context; fine for a CLI tool.

This is the recommended **core engine** because it is the least code, is fully
testable, and leans on machinery already in the repo.

## 4. Option B — Roslyn source generator / analyzer (compile‑time)

Use a Roslyn `IIncrementalGenerator` (or a standalone analyzer using the Roslyn
APIs) that reads the C# *syntax + semantic model* at build time and emits the
`StructureDefinition` JSON as a build artifact.

**Pros**

- Sees **everything the compiler sees**: XML doc comments, nullable reference type
  annotations, `init`/`required` members, partial types, generics — no separate
  `.xml` doc file needed.
- Runs automatically on every build; the models can't drift from the code.
- No need to load/execute the target assembly.

**Cons**

- Considerably more machinery: a separate `netstandard2.0` analyzer project,
  Roslyn packages, and generators can't easily reference the Firely SDK to build
  `StructureDefinition` POCOs (analyzer dependency isolation makes that painful) —
  you'd likely hand‑build JSON via a template, losing the SDK guarantees.
- Harder to debug and unit test than a plain reflection walk.
- Can only see types **in the compilation** — can't target a prebuilt third‑party DLL.

Source generation is the "purest" code‑first story, but it is a much bigger build
for marginal benefit here, and it fights the Firely SDK dependency this repo relies on.

## 5. Option C — Hybrid (reflection core + optional doc/attribute enrichment)

Recommended overall shape: **Option A as the engine**, enriched with the metadata
that reflection alone misses:

- `NullabilityInfoContext` for accurate `0..1` vs `1..1`.
- Parse the sibling `*.xml` documentation file for `<summary>`/`<remarks>` →
  element `short`/`definition`.
- Honour a small set of opt‑in attributes for anything the type system can't express
  (fixed cardinality, bindings, element ordering, canonical URL, "ignore this member").

This keeps the implementation small and testable while getting ~90% of the fidelity
of the source‑generator approach. If compile‑time guarantees later become important,
Option B can be layered on without throwing away the mapping logic — factor the
"C# member → ElementDefinition" projection into a library both front‑ends can call.

## 6. Reusing the Firely SDK's existing attributes

Because this repo already references the Firely .NET SDK, we do **not** need to invent
a bespoke attribute vocabulary for the *structural* side of the model. The following
attributes are verified against `Hl7.Fhir.Base` 5.12.1 (the version referenced here)
by reflecting over the shipped assembly, and they map directly onto
`StructureDefinition`/`ElementDefinition`:

| Firely attribute (namespace) | Target | Key members | Maps to |
| --- | --- | --- | --- |
| `[FhirType(name, canonical)]` (`Introspection`) | Class | `Name`, `Canonical`, `IsResource`, `IsNestedType` | `StructureDefinition.name` / `.url` / `.type`; root element — this **is** the "driver + canonical" attribute |
| `[FhirElement(name)]` (`Introspection`) | Property | `Name`, `Order`, `Choice`, `XmlSerialization`, `InSummary`, `IsModifier`, `IsPrimitiveValue`, `FiveWs` | element `path`, `.order`, `representation` (xmlAttr), `.isModifier`, `.isSummary` |
| `[Cardinality(Min=…, Max=…)]` (`Validation`) | Property | `Min`, `Max` (`-1` = `*`) | `ElementDefinition.min` / `.max` — **authoritative cardinality**, better than guessing |
| `[AllowedTypes(types)]` (`Validation`) | Property | `Type[] Types` | `type[]` for choice (`value[x]`) elements |
| `[Binding(name)]` + `[Bindable(true)]` (`Introspection`) | Property/Class | `Name`, `IsBindable` | `ElementDefinition.binding` |
| `[References(resources)]` (`Introspection`) | Property | `string[] Resources` | `type.targetProfile` |
| `[DeclaredType(Type=…)]` (`Introspection`) | Property | `Type` | overrides the CLR→FHIR type mapping |
| `[BackboneType(definitionPath)]` (`Introspection`) | Class | `DefinitionPath` | nested `BackboneElement` typing |
| `[NotMapped]` (`Introspection`) | Any | — | **the "ignore this member" marker** — reuse instead of a custom `[LogicalIgnore]` |
| `[FhirModelAssembly(since)]` (`Introspection`) | Assembly | `FhirRelease Since` | marks an assembly as a FHIR model provider (discovery) |
| `[Versioned]` / `[VersionedValidation]` (`Introspection`/`Validation`) | Any | `FhirRelease Since` | version‑gating elements |
| `[UriPattern]` (`Validation`) | Property | — | uri‑format validation hint |

Two consequences:

- The doc's earlier hypothetical `[LogicalModel(Canonical=…)]` is essentially
  **`[FhirType(name, canonical)]`**, and `[LogicalIgnore]` is **`[NotMapped]`** — so we
  reuse these rather than defining new ones.
- The SDK's own **`ModelInspector` / `ClassMapping` / `PropertyMapping`** already *read
  all of these attributes for you*, producing a pre‑parsed, FHIR‑shaped view of a type.
  The generator can consume that instead of hand‑rolling `GetCustomAttribute` calls.

**The one thing these attributes deliberately do *not* carry is human documentation
prose** — there is no Firely attribute for `short`/`definition`/`comment` text. That
gap is exactly what XML doc comments fill (§7). So the division of labour is:

- **Firely attributes → the machine‑facing schema** (cardinality, type, order, binding,
  canonical, isModifier).
- **XML `///` doc comments → the human‑facing documentation** (`short`, `definition`,
  `comment`).

No overlap, no duplicated text.

## 7. Getting descriptions (documentation) out of the code — XML doc comments

Because the whole point is *documentation from code*, and the Firely attributes carry
no prose, **XML doc comments are the chosen documentation channel.**

**Plumbing.** Doc comments are **not** in the compiled DLL. Enable
`<GenerateDocumentationFile>true</GenerateDocumentationFile>` on the target project; the
compiler emits a sibling `MyAssembly.xml`. The reflection engine loads the DLL for
structure and reads the `.xml` for prose, keyed by *documentation IDs*:

- Type → `T:Namespace.MyClass`
- Property → `P:Namespace.MyClass.MyProperty` (generics use backtick arity, e.g. `` `1 ``)

**Tag → field mapping:**

| XML doc tag | Target |
| --- | --- |
| `<summary>` on a property | `ElementDefinition.short` (and/or `.definition`) |
| `<remarks>` on a property | `ElementDefinition.definition` or `.comment` (longer prose) |
| `<summary>` on the class | `StructureDefinition.description` + root element `.definition` |
| `<value>` | property `short` (alternative to `<summary>`) |
| `<see cref="…"/>` | resolve the cref doc‑ID → element path / link |
| `<example>` | `ElementDefinition.example` or an extension (phase 2) |

**Precedence chain** (best authoring experience + graceful degradation):

1. Explicit attribute (`[Description]` / a project override) — highest priority, for
   when published text must differ from the dev‑facing comment.
2. XML doc `<summary>` → `short`, `<remarks>` → `comment`/`definition` — the default,
   zero‑extra‑effort path.
3. Nothing → leave the description empty (do **not** invent text or emit raw tags).

**Gotchas to handle:** the `.xml` must be shipped/located (degrade gracefully if
missing); doc text is indentation‑polluted and must be whitespace‑normalized; inline
`<c>`/`<code>`/`<para>`/`<list>` tags should be stripped or converted to Markdown
(FHIR `definition`/`comment` are `markdown`); entities must be decoded; and
`<inheritdoc/>` / `<see cref>` are **not** expanded by the compiler — see §9.

## 8. The "driver" artifact — deciding which classes are output

The problem statement specifically asks for *"some artifact to drive which classes
should be output."* Options, roughly increasing in explicitness:

- **A) Marker attribute on the class** — reuse **`[FhirType(name, canonical)]`**.
  - Pros: co‑located with the code, refactor‑safe, self‑documenting, carries the
    canonical URL per model. Discovery = "all types with `[FhirType]`" (and/or the
    `[FhirModelAssembly]` marker at assembly level).
  - Cons: requires the target project to reference the Firely SDK (already a dependency
    here).
- **B) A configuration file** (JSON/YAML) listing fully‑qualified type names, output
  paths, canonical base URL, FHIR version, etc.
  - Pros: no source changes needed; can target third‑party assemblies; keeps
    publishing concerns (URL scheme, version) out of the code. Fits a CLI nicely.
  - Cons: names can drift from the code (rename breaks it silently).
- **C) Namespace / assembly convention** — "every public type under
  `MyCompany.LogicalModels`".
  - Pros: zero per‑type ceremony.
  - Cons: least explicit; easy to over‑ or under‑include.

**Recommendation:** support **(A) `[FhirType]` as the primary driver** (best fit for a
code‑first philosophy and lets each model carry its own canonical URL), with **(B) a
config file** as an override/supplement for cases where the source can't be touched or
where publishing metadata (base canonical URL, IG version, status, output directory)
should live outside the code. (C) can be an optional convenience switch.

A minimal config file would carry the cross‑cutting settings the attributes
shouldn't hard‑code: canonical URL base, FHIR release (R4/R5), publisher/status
defaults, output folder, and an include/exclude list.

## 9. Derivation chains — each layer is its own model (specialization, no flattening)

**Decision:** when `Derived : Base` and each class gets its own logical model, we use
**specialization, not flattening** (Option 2):

- `Derived`'s `StructureDefinition` sets `baseDefinition` = `Base`'s canonical URL,
  `derivation = specialization`.
- The differential contains **only the members declared on `Derived` itself** — the
  inherited members are *not* re‑emitted; they are inherited from `Base`'s model.
- In reflection terms: enumerate members with **`BindingFlags.DeclaredOnly`** so only
  the type's own properties are projected. (`Type.GetProperties()` without it would
  return inherited members too — that would be the flattening approach we are *not*
  taking.)

This mirrors how FHIR itself expresses derived StructureDefinitions, avoids
duplicating both elements *and* documentation across every layer, and keeps each
member documented exactly once — at the point where it is declared.

**Impact on `<inheritdoc/>`.** The C# compiler does **not** expand `<inheritdoc/>`; the
emitted `.xml` contains the literal tag. Choosing specialization largely *sidesteps*
this problem: because each layer only emits its own declared members, and those members'
`<summary>` prose lives natively on the declaring type, there is normally no
`<inheritdoc/>` to resolve. `Base`'s members are documented in `Base`'s model; `Derived`
doesn't repeat them.

A **minimal `inheritdoc` resolver** is still worth implementing for the residual cases
that remain even under specialization:

- A `Derived` member that **`override`s or `new`‑hides** a base member and uses
  `<inheritdoc/>` — walk the base‑type chain to find the nearest real `<summary>`.
- `<inheritdoc cref="ISomeInterface.Member"/>` — resolve the cref target (interface docs
  are not in the base *class* chain).
- An `override` member with no comment at all — climb to the base declaration for text.

If an `<inheritdoc>` cannot be resolved, leave the description empty rather than emitting
the literal tag.

## 10. Mapping cheat‑sheet: C# → StructureDefinition

| C# construct | StructureDefinition / ElementDefinition |
| --- | --- |
| Class selected for output (`[FhirType]`) | `StructureDefinition` `kind=logical`, `derivation=specialization`, root element |
| Class name / `[FhirType(Name=…)]` | `name`, `id`, root element `path` |
| `[FhirType(canonical)]` or config base + name | `url` (canonical), `type` |
| Base class (`Derived : Base`) | `baseDefinition` = `Base` canonical; only own members in differential (`DeclaredOnly`) |
| Declared public property | child `ElementDefinition` at `Root.prop` |
| `[FhirElement(Order=…)]` | element `.order` / ordering |
| Property CLR type (primitive) | `type.code` mapped to FHIR primitive |
| Property type = another selected class | nested logical model ref / `contentReference` (recurse) |
| `[Cardinality(Min,Max)]` (authoritative) | `min` / `max` (`-1`→`*`) |
| `List<T>` / `IEnumerable<T>` (fallback) | `max = *` |
| Nullable (`T?` / ref type, via `NullabilityInfoContext`) | `min = 0` |
| Non‑nullable value type / `required` | `min = 1` |
| `[AllowedTypes(...)]` | choice `type[]` (`value[x]`) |
| `[Binding]` / `enum` | `binding` (+ generated `ValueSet`, phase 2) |
| `[References(...)]` | `type.targetProfile` |
| XML `<summary>` | `short` / `definition` |
| XML `<remarks>` | `comment` / `definition` |
| `[Obsolete]` | element marked deprecated (optional) |
| `[NotMapped]` | skipped |

## 11. Suggested shape in this repository

- A new command/mode (the repo already has a CLI entry point in
  `fml-processor/Program.cs`) such as `fml-processor gen-logical --assembly … --config …`.
- A `LogicalModelGenerator` class encapsulating the "type → StructureDefinition"
  projection, so it can be unit tested directly.
- Emit `*.StructureDefinition.json` via the Firely serializer.
- Round‑trip test in `fml-tester`: define sample POCOs, generate, then feed the
  result into the **existing** FML validator to prove the generated models are usable
  as FML `source`/`target` structures.

## 12. Recommendation summary

- **Build it as a reflection‑based CLI tool (Option A/C)**, not a source generator —
  it is dramatically less code, reuses the Firely SDK POCOs + serializer already
  referenced here, works on any assembly, and is easy to test in `fml-tester`.
- **Reuse the Firely SDK's existing attributes** (`[FhirType]`, `[FhirElement]`,
  `[Cardinality]`, `[AllowedTypes]`, `[Binding]`, `[References]`, `[NotMapped]`, …) for
  the structural schema — ideally via `ModelInspector`/`ClassMapping` — instead of
  inventing new ones.
- **Use XML doc comments for the documentation prose** (`<summary>`→`short`,
  `<remarks>`→`comment`/`definition`), since no Firely attribute carries description
  text. Enrich reflection with `NullabilityInfoContext` for cardinality where
  `[Cardinality]` is absent.
- **Drive selection with `[FhirType]`** (primary) plus an optional config file
  (for canonical URL base, FHIR version, output paths, and third‑party types).
- **Model derivation chains as specialization, not flattening** — each layer emits only
  its own declared members (`DeclaredOnly`) with `baseDefinition` set to its parent,
  which also makes `<inheritdoc/>` a rare edge case rather than a core requirement.
- **Keep the projection logic in a standalone class** so a Roslyn source generator
  (Option B) could be added later for compile‑time guarantees without rewriting the
  core mapping.

### Open questions to confirm before implementation

- Target FHIR release for the generated logical models — R5 (matching the current
  SDK reference) or also R4?
- How should nested/complex types be represented — inline `BackboneElement`‑style
  paths, or separate logical models linked by canonical/`contentReference`?
- Are terminology bindings (enums → `ValueSet`) in scope for v1, or a later phase?
- Canonical URL scheme (per‑type `[FhirType(canonical)]` vs. base‑URL‑plus‑name from config)?

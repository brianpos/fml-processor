using fml_processor.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Navigation;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Utility;
using Hl7.FhirPath.Sprache;
using Microsoft.Health.Fhir.MappingLanguage;
using System.Text;

namespace fml_processor;

/// <summary>
/// Custom FML creator based on input StructureDefinitions only
/// </summary>
public class MapGenerator
{
    // Canonical indexed
    public Dictionary<string, StructureDefinition> Source = new Dictionary<string, StructureDefinition>();
    public Dictionary<string, StructureDefinition> Target = new Dictionary<string, StructureDefinition>();

    /// <summary>
    /// Resolver for the source FHIR version, used to walk the source StructureDefinitions via the
    /// <see cref="FmlStructureDefinitionWalker"/> (mirroring the approach used in the FmlValidator).
    /// </summary>
    public IAsyncResourceResolver? SourceResolver { get; set; }

    /// <summary>
    /// Resolver for the target FHIR version, used to walk the target StructureDefinitions via the
    /// <see cref="FmlStructureDefinitionWalker"/> (mirroring the approach used in the FmlValidator).
    /// </summary>
    public IAsyncResourceResolver? TargetResolver { get; set; }

    /// <summary>
    /// Appends known rules to the provided dictionary.
    /// </summary>
    /// <param name="rules">The dictionary to which the rules will be added.</param>
    /// <param name="name">The name of the rule set.</param>
    /// <param name="ruleText">The FML text representing the rules.</param>
    /// <param name="comment">An optional comment to annotate the first rule.</param>
    private static void AppendKnownRules(Dictionary<string, IEnumerable<Rule>> rules, string name, string ruleText, string? comment = null)
    {
        var rule = FmlParser.ParseRules(ruleText);
        if (!string.IsNullOrEmpty(comment))
            rule.First().SetAnnotation(new MapCommentAnnotation(comment));
        rules.Add(name, rule);
    }

    //private static Dictionary<string, IEnumerable<Rule>> CreateCustomRules()
    //{
    //    var rules = new Dictionary<string, IEnumerable<Rule>>();
    //    AppendKnownRules(rules, "Account.relatedAccount", "Account.relatedAccount as ra then { ra.account -> tgt.parent; ra.account -> tgt.guarantor as g, g.account = ra.account; } \"ra\";",
    //        "maps to both Account.parent and Account.guarantor.account depending on the value of relatedAccount.relationship");

    //    AppendKnownRules(rules, "Consent.source[x]", "  // Source splits into separate properties based on type\n  src.source : Attachment -> tgt.sourceAttachment;  src.source : Reference -> tgt.sourceReference;");
    //    AppendKnownRules(rules, "ActivityDefinition.dosage", "  src where (dosage.exists()) -> tgt.dosageInstruction as di then {\r\n    // each dosage goes into a new step, and the dosage is mapped to the component of that step\r\n    src.dosage as sd -> di.step as s, s.component = sd;\r\n  } \"mapDosageInstructions\";\r\n");
    //    AppendKnownRules(rules, "RelatedArtifact.url", "src.url as u where (src.document.empt()) -> tgt.document as d, d.url = u \"setUrl\"; // need to check if this impacts existing document");
    //    return rules;
    //}

    //public Dictionary<string, IEnumerable<Rule>> KnownCustomRules = CreateCustomRules();

    // Content now read from the configuration file not hard coded
    public HashSet<string> KnownMappings = new HashSet<string>(); 

    public List<FmlStructureMap> GenerateMaps(IEnumerable<string> filterTypes)
    {
        var result = new List<FmlStructureMap>();

        // iterate over all the source resources
        foreach (var sourceR in Source)
        {
            var targetResourceName = sourceR.Key;
            if (targetResourceName == "http://hl7.org/fhir/StructureDefinition/RequestGroup")
                targetResourceName = "http://hl7.org/fhir/StructureDefinition/RequestOrchestration";
            if (filterTypes != null && filterTypes.Any() && !filterTypes.Contains(sourceR.Value.Type)
                && "http://hl7.org/fhir/StructureDefinition/Base" != sourceR.Value.BaseDefinition
                && "http://hl7.org/fhir/StructureDefinition/Element" != sourceR.Value.BaseDefinition
                && "http://hl7.org/fhir/StructureDefinition/Quantity" != sourceR.Value.BaseDefinition
                && "http://hl7.org/fhir/StructureDefinition/BackboneElement" != sourceR.Value.BaseDefinition
                && "http://hl7.org/fhir/StructureDefinition/uri" != sourceR.Value.BaseDefinition
                && "http://hl7.org/fhir/StructureDefinition/string" != sourceR.Value.BaseDefinition
                )
                continue;
            if (Target.ContainsKey(targetResourceName))
            {
                FmlStructureMap fml = CreateMap(sourceR.Value, Target[targetResourceName]);
                if (fml != null)
                    result.Add(fml);
            }
            else
            {
                Console.WriteLine($"No target mapping for source type {sourceR.Key}");
            }
        }

        return result;
    }

    /// <summary>
    /// Enumerates the elements of a StructureDefinition by walking its snapshot with the
    /// <see cref="FmlStructureDefinitionWalker"/> (as done in the FmlValidator), rather than
    /// reading the bare ElementDefinitions from the differential.
    /// </summary>
    /// <remarks>
    /// The elements are returned in depth-first pre-order (parent before children), descending only
    /// into elements that carry inline children (e.g. BackboneElement/Element). Elements that are
    /// inherited from a base type (id, extension, modifierExtension, meta, ... and inherited datatype
    /// content such as Quantity's value/unit) are skipped, because the generated group already
    /// <c>extends</c> that base type which covers them. This mirrors the set of elements previously
    /// obtained from <c>Differential.Element.Skip(1)</c>.
    /// </remarks>
    private static IEnumerable<ElementDefinition> WalkElements(StructureDefinition sd, IAsyncResourceResolver resolver)
    {
        var walker = new FmlStructureDefinitionWalker(sd, resolver);

        // Type-less abstract roots (e.g. Base) have no walkable children; previously these produced
        // an empty differential loop, so return an empty set rather than letting the walker throw.
        if (!walker.Current.HasChildren)
            return Enumerable.Empty<ElementDefinition>();

        return WalkChildren(walker, resolver);
    }

    private static IEnumerable<ElementDefinition> WalkChildren(FmlStructureDefinitionWalker walker, IAsyncResourceResolver resolver)
    {
        foreach (var childNav in walker.Children())
        {
            // Skip elements that are inherited from a base type; the generated group extends that
            // base so the inherited elements (id/extension/modifierExtension/meta/... and inherited
            // datatype content) are already covered and must not be re-emitted.
            if (!IsIntroducedHere(childNav))
                continue;

            yield return childNav.Current;

            // Only descend into inline children (BackboneElement/Element); referenced datatypes are
            // not expanded here (that is handled explicitly for differing types during mapping).
            if (childNav.HasChildren)
            {
                var childWalker = new FmlStructureDefinitionWalker(childNav, resolver);
                foreach (var descendant in WalkChildren(childWalker, resolver))
                    yield return descendant;
            }
        }
    }

    /// <summary>
    /// Determines whether the element the navigator is positioned on was introduced by its own
    /// StructureDefinition (as opposed to being inherited from a base type). Inherited elements are
    /// covered by the group's <c>extends</c> base and are therefore not emitted.
    /// </summary>
    private static bool IsIntroducedHere(ElementDefinitionNavigator nav)
    {
        var basePath = nav.Current?.Base?.Path;

        // Without base information we cannot tell it is inherited, so keep it (defensive).
        if (string.IsNullOrEmpty(basePath))
            return true;

        var baseRootType = basePath.Contains('.') ? basePath.Substring(0, basePath.IndexOf('.')) : basePath;
        return baseRootType == nav.StructureDefinition?.Name;
    }

    /// <summary>
    /// Returns true when the element's primary type is a complex type: a resource/datatype (declared
    /// in PascalCase) or an inline BackboneElement/Element. FHIR primitive types (e.g. string, code)
    /// are declared in lowerCamelCase and return false.
    /// </summary>
    private static bool IsComplexType(ElementDefinition ed)
    {
        var code = ed.Type?.FirstOrDefault()?.Code;
        if (string.IsNullOrEmpty(code))
            return false;
        if (code == "BackboneElement" || code == "Element")
            return true;
        return char.IsUpper(code[0]);
    }

    /// <summary>
    /// Recursively expands the introduced (non-inherited) child elements of the named datatype
    /// referenced by the given element, rewriting their paths so they are rooted at
    /// <paramref name="parentPath"/>. This is used when a property's type differs between the source
    /// and target versions (for example a BackboneElement that became the Availability datatype), so
    /// that the datatype's properties - at any depth - can participate in the generated nested group
    /// and be located by (possibly cross-level) renames.
    /// </summary>
    private static IEnumerable<ElementDefinition> ExpandType(ElementDefinition ed, IAsyncResourceResolver resolver, string parentPath)
    {
        var canonical = ed.Type?.FirstOrDefault()?.GetTypeProfile();
        if (string.IsNullOrEmpty(canonical))
            yield break;

        var sd = resolver.FindStructureDefinitionAsync(canonical).GetAwaiter().GetResult();
        if (sd == null)
            yield break;

        foreach (var child in WalkElements(sd, resolver))
        {
            // Child paths are rooted at the datatype name (e.g. "Availability.availableTime.
            // availableStartTime"); re-root them under parentPath (e.g. the property path
            // "Location.hoursOfOperation") preserving the nested remainder.
            var relative = child.Path.Contains('.') ? child.Path.Substring(child.Path.IndexOf('.') + 1) : child.Path;
            var clone = (ElementDefinition)child.DeepCopy();
            clone.Path = parentPath + "." + relative;
            yield return clone;
        }
    }

    /// <summary>
    /// Produces a detached copy of a custom-rule <see cref="Rule"/> by round-tripping it through the
    /// serializer/parser. The custom-rule <see cref="GroupDeclaration"/> objects are shared across all
    /// generated maps (they live in <c>_customRules</c>), so the actual rule instances must never be
    /// added directly to a generated group - each generated map needs its own independent copy.
    /// The copy's hidden tokens are normalized (see <see cref="SanitizeClonedRule"/>) so the rule
    /// adopts the generated map's formatting rather than dragging in whitespace/comment noise from its
    /// original position in the custom-rules file.
    /// </summary>
    private static Rule CloneRule(Rule rule)
    {
        var sb = new StringBuilder();
        FmlSerializer.SerializeRule(sb, rule, 1);
        var clone = FmlParser.ParseRule(sb.ToString());
        SanitizeClonedRule(clone);
        return clone;
    }

    /// <summary>
    /// Normalizes the hidden (whitespace/comment) tokens on a cloned custom rule and its whole
    /// sub-tree so it renders cleanly inside a generated group. The custom-rules file is authored as
    /// free-form text, and ANTLR attaches each line's trailing comment as a leading hidden token of
    /// the following node; when such rules are injected/appended verbatim this produces stray
    /// end-of-line marker comments (e.g. "// custom rule below"), blank lines, and extra spaces before
    /// "->". This routine drops hoisted end-of-line comments and blank lines and strips whitespace-only
    /// trailing tokens, while preserving genuine standalone comments (those that sit on their own line).
    /// </summary>
    private static void SanitizeClonedRule(Rule rule)
    {
        rule.LeadingHiddenTokens = CleanLeadingTokens(rule.LeadingHiddenTokens);
        rule.TrailingHiddenTokens = StripWhitespaceOnlyTokens(rule.TrailingHiddenTokens);

        foreach (var s in rule.Sources)
        {
            s.LeadingHiddenTokens = CleanLeadingTokens(s.LeadingHiddenTokens);
            s.TrailingHiddenTokens = StripWhitespaceOnlyTokens(s.TrailingHiddenTokens);
        }
        foreach (var t in rule.Targets)
        {
            t.LeadingHiddenTokens = CleanLeadingTokens(t.LeadingHiddenTokens);
            t.TrailingHiddenTokens = StripWhitespaceOnlyTokens(t.TrailingHiddenTokens);
        }

        if (rule.Dependent != null)
        {
            rule.Dependent.LeadingHiddenTokens = CleanLeadingTokens(rule.Dependent.LeadingHiddenTokens);
            foreach (var inv in rule.Dependent.Invocations)
            {
                inv.LeadingHiddenTokens = CleanLeadingTokens(inv.LeadingHiddenTokens);
                inv.TrailingHiddenTokens = StripWhitespaceOnlyTokens(inv.TrailingHiddenTokens);
            }
            foreach (var nested in rule.Dependent.Rules)
                SanitizeClonedRule(nested);
        }
    }

    /// <summary>
    /// Cleans a list of leading hidden tokens: drops "hoisted" end-of-line comments (a comment
    /// preceded by whitespace that contains no newline - i.e. it was a trailing comment on the
    /// previous line that ANTLR re-attached here), reduces the initial whitespace to just its indent
    /// (removing leading newlines/blank lines so the node lines up under the serializer's per-node
    /// newline), and collapses blank lines between any preserved standalone comments. Returns null when
    /// nothing remains so the serializer falls back to its default indent.
    /// </summary>
    private static List<HiddenToken>? CleanLeadingTokens(List<HiddenToken>? tokens)
    {
        if (tokens == null || tokens.Count == 0)
            return null;

        var result = new List<HiddenToken>();
        for (int i = 0; i < tokens.Count; i++)
        {
            var tok = tokens[i];
            if (tok.IsComment)
            {
                // A comment whose immediately preceding token is whitespace without a newline was a
                // trailing (end-of-line) comment on the previous line - drop it and that space.
                var prev = result.Count > 0 ? result[result.Count - 1] : null;
                if (prev != null && prev.IsWhitespace && !prev.Text.Contains('\n'))
                {
                    result.RemoveAt(result.Count - 1);
                    continue;
                }
            }
            result.Add(tok);
        }

        if (result.Count == 0)
            return null;

        // Reduce the first whitespace token to just its indent (text after the last newline), so the
        // node aligns under the newline the serializer emits after the previous node instead of
        // introducing a blank line.
        if (result[0].IsWhitespace)
        {
            var text = result[0].Text;
            int nl = text.LastIndexOf('\n');
            var indent = nl >= 0 ? text.Substring(nl + 1) : text;
            if (indent.Length == 0)
                result.RemoveAt(0);
            else
                result[0] = new HiddenToken { TokenType = result[0].TokenType, Text = indent };
        }

        // Collapse blank lines within any remaining whitespace tokens (e.g. after a standalone
        // comment) down to a single newline.
        for (int i = 0; i < result.Count; i++)
        {
            if (result[i].IsWhitespace && result[i].Text.Contains('\n'))
            {
                var collapsed = System.Text.RegularExpressions.Regex.Replace(
                    result[i].Text, "(\\r?\\n)([ \\t]*\\r?\\n)+", "$1");
                if (collapsed != result[i].Text)
                    result[i] = new HiddenToken { TokenType = result[i].TokenType, Text = collapsed };
            }
        }

        return result.Count == 0 ? null : result;
    }

    /// <summary>
    /// Removes whitespace-only hidden tokens (keeping comment tokens), used for trailing token lists
    /// so stray spaces captured before "->" or after a source/target don't survive into the output.
    /// </summary>
    private static List<HiddenToken>? StripWhitespaceOnlyTokens(List<HiddenToken>? tokens)
    {
        if (tokens == null || tokens.Count == 0)
            return null;
        var kept = tokens.Where(t => !t.IsWhitespace).ToList();
        return kept.Count == 0 ? null : kept;
    }

    /// <summary>
    /// Collects the (relative) target element names written by a custom rule, including any nested
    /// then-rules, so the corresponding target elements can be "marked off" and not reported as
    /// unpopulated. Only <c>tgt</c>-context targets are collected (the group's target root).
    /// </summary>
    private static IEnumerable<string> CollectTargetElementNames(Rule rule)
    {
        foreach (var t in rule.Targets)
        {
            if (!string.IsNullOrEmpty(t.Element) && t.Context == "tgt")
                yield return t.Element!.Replace("[x]", "");
        }
        if (rule.Dependent?.Rules != null)
        {
            foreach (var nested in rule.Dependent.Rules)
                foreach (var name in CollectTargetElementNames(nested))
                    yield return name;
        }
    }

    /// <summary>
    /// If the <paramref name="currentGroup"/> has an attached custom-rule group (from the custom
    /// rules .fml file) that defines one or more rules whose source matches the element currently
    /// being processed, inject those rules into the group in place of the rule that would normally be
    /// generated, and mark off the target elements they write so they are not later reported as
    /// unpopulated. A matched custom rule that has no targets is omitted entirely (the property is
    /// intentionally dropped). Returns <c>true</c> when a matching custom rule was found (whether it
    /// produced output or was omitted), signalling the caller to skip normal generation.
    /// </summary>
    private static bool TryInjectCustomGroupRules(
        GroupDeclaration currentGroup,
        string targetRootPath,
        string currentElementPath,
        HashSet<Rule> consumedCustomRules,
        List<ElementDefinition> missedTargetElements)
    {
        if (!currentGroup.HasAnnotation<GroupDeclaration>())
            return false;

        var customGroup = currentGroup.Annotation<GroupDeclaration>();
        if (customGroup?.Rules == null || customGroup.Rules.Count == 0)
            return false;

        var sourceLeaf = (currentElementPath.Contains(".")
            ? currentElementPath.Substring(currentElementPath.LastIndexOf(".") + 1)
            : currentElementPath).Replace("[x]", "");

        var matched = customGroup.Rules
            .Where(r => r.Sources.Any(s => (s.Element ?? string.Empty).Replace("[x]", "") == sourceLeaf))
            .ToList();
        if (matched.Count == 0)
            return false;

        foreach (var customRule in matched)
        {
            if (!consumedCustomRules.Add(customRule))
                continue; // already injected via another of its source properties

            // A rule with no targets intentionally drops the property - omit it entirely.
            if (customRule.Targets.Any())
                currentGroup.Rules.Add(CloneRule(customRule));

            // Mark off the target elements the custom rule writes so they aren't reported as
            // "not populated". Match on the target path relative to the group's target root and,
            // as a fallback, on the leaf name.
            foreach (var tgtName in CollectTargetElementNames(customRule))
            {
                var tePath = (targetRootPath + "." + tgtName).Replace("[x]", "");
                missedTargetElements.RemoveAll(te =>
                    te.Path.Replace("[x]", "") == tePath
                    || (te.Path.Contains(".")
                            ? te.Path.Substring(te.Path.LastIndexOf(".") + 1)
                            : te.Path).Replace("[x]", "") == tgtName);
            }
        }

        return true;
    }

    /// <summary>
    /// Appends any custom-rule-group rules that were not consumed during the element walk to the end
    /// of their corresponding generated group. These are the "leftover" rules described by the
    /// custom-rules file semantics - rules that add new mappings rather than replacing the rule for a
    /// specific source property (e.g. an aggregating <c>src where (...) -> ...</c> rule whose source
    /// is the whole context and therefore never matches a single iterated element). Pure
    /// source-marker rules (no targets and no dependent) that were never matched are skipped. The
    /// target elements written by appended rules are marked off so they aren't reported as
    /// unpopulated.
    /// </summary>
    private static void AppendLeftoverCustomRules(
        FmlStructureMap fml,
        Dictionary<GroupDeclaration, string> customGroupTargetRoots,
        HashSet<Rule> consumedCustomRules,
        List<ElementDefinition> missedTargetElements)
    {
        foreach (var genGroup in fml.Groups)
        {
            if (!genGroup.HasAnnotation<GroupDeclaration>())
                continue;

            var customGroup = genGroup.Annotation<GroupDeclaration>();
            if (customGroup?.Rules == null)
                continue;

            var targetRoot = customGroupTargetRoots.TryGetValue(genGroup, out var root)
                ? root
                : (genGroup.Parameters.FirstOrDefault(p => p.Mode == ParameterMode.Target)?.Name ?? "tgt");

            foreach (var customRule in customGroup.Rules)
            {
                if (consumedCustomRules.Contains(customRule))
                    continue;

                // A rule that neither writes a target nor invokes/nests a dependent is a bare
                // source marker; if it was never matched to a property, there's nothing to append.
                if (!customRule.Targets.Any() && customRule.Dependent == null)
                    continue;

                consumedCustomRules.Add(customRule);
                genGroup.Rules.Add(CloneRule(customRule));

                foreach (var tgtName in CollectTargetElementNames(customRule))
                {
                    var tePath = (targetRoot + "." + tgtName).Replace("[x]", "");
                    missedTargetElements.RemoveAll(te =>
                        te.Path.Replace("[x]", "") == tePath
                        || (te.Path.Contains(".")
                                ? te.Path.Substring(te.Path.LastIndexOf(".") + 1)
                                : te.Path).Replace("[x]", "") == tgtName);
                }
            }
        }
    }

    private FmlStructureMap? CreateMap(StructureDefinition sourceSd, StructureDefinition targetSd)
    {
        if (sourceSd.Derivation == StructureDefinition.TypeDerivationRule.Constraint)
        {
            // Console.WriteLine($"Not generating for constraint {sourceSd.Name} {sourceSd.Url}");
            return null;
        }
        var fml = new FmlStructureMap();
        var sourceAlias = UseScope(fml, sourceSd.Url + "|" + sourceSd.Version, StructureMode.Source);
        var targetAlies = UseScope(fml, targetSd.Url + "|" + targetSd.Version, StructureMode.Target);

        var sourceWorkgroup = sourceSd.GetStringExtension("http://hl7.org/fhir/StructureDefinition/structuredefinition-wg");
        if (!string.IsNullOrEmpty(sourceWorkgroup))
            fml.AddAnnotation(new WorkgroupAnnotation(sourceWorkgroup));
        var targetWorkgroup = targetSd.GetStringExtension("http://hl7.org/fhir/StructureDefinition/structuredefinition-wg");
        if (!string.IsNullOrEmpty(targetWorkgroup))
            fml.AddAnnotation(new WorkgroupAnnotation(targetWorkgroup));

        string sourceVersion = getFhirVersion(sourceSd.Version).Replace("R", "").Replace("STU", "");
        string targetVersion = getFhirVersion(targetSd.Version).Replace("R", "").Replace("STU", "");

        SetMetadata(fml, "url", $"http://hl7.org/fhir/uv/xver/StructureMap/{sourceSd.Name}{sourceVersion}to{targetVersion}");
        SetMetadata(fml, "name", $"{sourceSd.Name}{sourceVersion}to{targetVersion}");
        SetMetadata(fml, "title", $"{sourceSd.Name} Transforms: {getFhirVersion(sourceSd.Version)} to {getFhirVersion(targetSd.Version)}");
        SetMetadata(fml, "status", "draft");

        fml.Imports ??= new List<ImportDeclaration>();
        fml.Imports.Add(new ImportDeclaration() { Url = $"http://hl7.org/fhir/uv/xver/StructureMap/*{sourceVersion}to{targetVersion}" });

        // walk all the properties in the sourceSd
        var group = new GroupDeclaration();
        fml.Groups.Add(group);
        group.Name = $"{ConceptMapConverter.PascalCase(sourceSd.Name)}";
        group.Parameters.Add(new GroupParameter
        {
            Mode = ParameterMode.Source,
            Type = sourceAlias,
            Name = "src"
        });
        group.Parameters.Add(new GroupParameter
        {
            Mode = ParameterMode.Target,
            Type = targetAlies,
            Name = "tgt"
        });
        group.Parameters[0].SetAnnotation(new StructureDefAnnotation(sourceSd));
        group.Parameters[1].SetAnnotation(new StructureDefAnnotation(targetSd));
        group.Extends = PascalCase(sourceSd.BaseDefinition?.Replace("http://hl7.org/fhir/StructureDefinition/", ""));
        if (group.Extends == "Base")
            group.Extends = null;
        if (group.Extends != null)
            group.TypeMode = GroupTypeMode.TypePlus;

        var customRuleKey = GetCustomRuleName(group);
        // Maps a generated group to the target path its rule targets are relative to, so that
        // leftover custom rules appended after the walk can have their populated targets marked off.
        var customGroupTargetRoots = new Dictionary<GroupDeclaration, string>();
        if (_customRules.ContainsKey(customRuleKey))
        {
            group.SetAnnotation(_customRules[customRuleKey]);
            customGroupTargetRoots[group] = targetSd.Name;
        }

        // Walk the source and target StructureDefinitions using the StructureDefinitionWalker
        // (as is done in the FmlValidator) rather than reading the bare differential elements.
        var sourceElements = WalkElements(sourceSd, SourceResolver!).ToList();
        var targetElements = WalkElements(targetSd, TargetResolver!).ToList();

        // for now just do a simple copy of all matching elements by name
        var missedSourceElements = new List<ElementDefinition>();
        var missedTargetElements = targetElements.ToList();
        // Custom rules injected from a group's attached custom-rule group are tracked here (by
        // reference) so a rule that reads several source properties is only injected once.
        var consumedCustomRules = new HashSet<Rule>();
        Stack<GroupDeclaration> groupStack = new Stack<GroupDeclaration>();
        Stack<string> groupPathStack = new Stack<string>(); // Track the full source path for each group
        Stack<string> groupTargetPathStack = new Stack<string>(); // Track the full target path for each group
        groupStack.Push(group);
        groupPathStack.Push(sourceSd.Name); // Root level is the resource name
        groupTargetPathStack.Push(targetSd.Name);

        for (int i = 0; i < sourceElements.Count; i++)
        {
            var se = sourceElements[i];
            // Calculate the parent path for this element
            var currentElementPath = se.Path;
            var parentPath = currentElementPath.Contains(".") ? currentElementPath.Substring(0, currentElementPath.LastIndexOf(".")) : sourceSd.Name;

            // Pop groups until we're at the right level
            while (groupPathStack.Count > 1 && parentPath != groupPathStack.Peek() && !parentPath.StartsWith(groupPathStack.Peek() + "."))
            {
                groupStack.Pop();
                groupPathStack.Pop();
                groupTargetPathStack.Pop();
            }

            if (TryInjectCustomGroupRules(groupStack.Peek(), groupTargetPathStack.Peek(), currentElementPath, consumedCustomRules, missedTargetElements))
            {
                // The current group's attached custom-rule group defines a rule whose source matches
                // this element; those rules were injected (or intentionally omitted when a matched
                // rule had no targets) in place of the rule we would normally generate. The source is
                // considered handled, so fall through without adding it to the missed-source list.
            }
            else
            {
                var matchingTe = targetElements.FirstOrDefault(te =>
                        te.Path.Replace("[x]", "") == se.Path.Replace("[x]", "")
                        || KnownMappings.Contains($"{se.Path.Replace("[x]", "")} -> {te.Path.Replace("[x]", "")}")
                        // || KnownMappings.Contains($"{te.Path.Replace("[x]", "")} -> {se.Path.Replace("[x]", "")}") // reverse mapping too (from the previous direction)
                        );
                if (matchingTe != null)
                {
                    var key = $"{se.Path.Replace("[x]", "")} -> {matchingTe.Path.Replace("[x]", "")}";
                    if (KnownMappings.Contains(key))
                    {
                        // we've used this mapping, so remove it
                        // (then we can check at the end if there are any that weren't detected/used)
                        KnownMappings.Remove(key);
                    }
                    missedTargetElements.Remove(matchingTe);
                    var rule = new Rule();
                    rule.Sources.Add(new RuleSource
                    {
                        Context = "src",
                        Element = se.Path.Contains(".") ? se.Path.Substring(se.Path.LastIndexOf(".") + 1).Replace("[x]", "") : se.Path.Replace("[x]", "")
                    });
                    // Render the target relative to the current group's target root so cross-level
                    // renames (e.g. Location.hoursOfOperation.openingTime ->
                    // Location.hoursOfOperation.availableTime.availableStartTime) become a dotted
                    // target path (availableTime.availableStartTime) within the group.
                    var targetRootPath = groupTargetPathStack.Peek();
                    string targetElementName;
                    if (matchingTe.Path.StartsWith(targetRootPath + "."))
                        targetElementName = matchingTe.Path.Substring(targetRootPath.Length + 1).Replace("[x]", "");
                    else
                        targetElementName = matchingTe.Path.Contains(".") ? matchingTe.Path.Substring(matchingTe.Path.LastIndexOf(".") + 1).Replace("[x]", "") : matchingTe.Path.Replace("[x]", "");
                    rule.Targets.Add(new RuleTarget
                    {
                        Context = "tgt",
                        Element = targetElementName
                    });

                    bool displayCardinality = false;
                    string? mappingWarningMessage = null;
                    if (se.Min == 0 && matchingTe.Min == 1) //  an optional field was made mandatory
                    {
                        displayCardinality = true;
                        mappingWarningMessage = " // Warning: source optional, target mandatory";
                    }
                    if (se.Max == "*" && matchingTe.Max == "1") // a repeating field was made single-valued
                    {
                        displayCardinality = true;
                        mappingWarningMessage = " // Warning: source repeating, target single-valued";
                    }

                    // these types need to go into their own group and invocation.
                    // A nested group is required when either side is an inline BackboneElement/Element.
                    // When exactly one side is an inline backbone and the other is a named complex
                    // datatype (e.g. Location.hoursOfOperation changed from a BackboneElement to the
                    // Availability datatype), the types differ so we walk into the datatype side and
                    // surface its properties (as opposed to same-typed elements where the type's own
                    // group/base already covers the internals).
                    bool sourceIsBackbone = se.Type.Any(t => t.Code == "BackboneElement" || t.Code == "Element");
                    bool targetIsBackbone = matchingTe.Type.Any(t => t.Code == "BackboneElement" || t.Code == "Element");
                    bool differingTypeDescent = (sourceIsBackbone ^ targetIsBackbone)
                        && IsComplexType(se) && IsComplexType(matchingTe);

                    if (sourceIsBackbone || (targetIsBackbone && differingTypeDescent))
                    {
                        rule.Sources[0].Variable = "s";
                        rule.Targets[0].Variable = "t";
                        rule.Dependent = new RuleDependent();
                        rule.Dependent.Invocations = new List<GroupInvocation>();

                        // Create a group name based on the source and target types
                        var groupName = ConceptMapConverter.PascalCase(se.Path.Replace("[x]", "")).Replace(".", "");

                        rule.Dependent.Invocations.Add(new GroupInvocation()
                        {
                            Name = groupName,
                            Parameters = [
                                new InvocationParameter() { Type = InvocationParameterType.Identifier, Value = "s" },
                            new InvocationParameter() { Type = InvocationParameterType.Identifier, Value = "t" }
                                ]
                        });

                        // Add a new group. It extends the base type of whichever side is the inline
                        // backbone (the datatype side does not provide a usable "extends" base here).
                        var groupBackbone = new GroupDeclaration()
                        {
                            Name = groupName,
                            Extends = sourceIsBackbone ? se.Type.First().Code : matchingTe.Type.First().Code
                        };
                        groupBackbone.Parameters.Add(new GroupParameter
                        {
                            Mode = ParameterMode.Source,
                            Name = "src"
                        });
                        groupBackbone.Parameters.Add(new GroupParameter
                        {
                            Mode = ParameterMode.Target,
                            Name = "tgt"
                        });

                        customRuleKey = GetCustomRuleName(groupBackbone);
                        if (_customRules.ContainsKey(customRuleKey))
                        {
                            groupBackbone.SetAnnotation(_customRules[customRuleKey]);
                            customGroupTargetRoots[groupBackbone] = matchingTe.Path;
                        }

                        // always display cardinality for groups
                        displayCardinality = true;

                        // Add the rule to the PARENT group before pushing the child
                        groupStack.Peek().Rules.Add(rule);

                        if (se.Path.Replace("[x]", "") != matchingTe.Path.Replace("[x]", ""))
                        {
                            if (rule.Sources.Count == rule.Targets.Count && rule.Sources.Count == 1 && rule.Sources[0].Element != rule.Targets[0].Element)
                            {
                                rule.TrailingHiddenTokens ??= new List<HiddenToken>();
                                rule.TrailingHiddenTokens.Add(new HiddenToken()
                                {
                                    TokenType = FmlMappingLexer.LINE_COMMENT,
                                    Text = " // renamed"
                                });
                            }
                            rule.SetAnnotation(new ElementRenamedAnnotation());
                            rule.Sources[0].SetAnnotation(new ElementDefAnnotation(se));
                            rule.Targets[0].SetAnnotation(new ElementDefAnnotation(matchingTe));

                            fml.SetAnnotation(new NeedsReviewAnnotation());
                            groupStack.Peek().SetAnnotation(new NeedsReviewAnnotation());
                            rule.SetAnnotation(new NeedsReviewAnnotation());
                        }

                        groupStack.Push(groupBackbone);
                        groupPathStack.Push(currentElementPath); // Push the full source path of this BackboneElement
                        groupTargetPathStack.Push(matchingTe.Path); // Push the matched target path for this group
                        fml.Groups.Add(groupBackbone);

                        // When the source and target types differ (e.g. a BackboneElement on one side
                        // and a named datatype such as Availability on the other), walk into the
                        // datatype side and surface its introduced properties so they participate in
                        // the nested group's mapping (and unmapped ones are reported for review).
                        if (differingTypeDescent)
                        {
                            groupBackbone.SetAnnotation(new NeedsReviewAnnotation());
                            fml.SetAnnotation(new NeedsReviewAnnotation());
                            rule.SetAnnotation(new NeedsReviewAnnotation());
                            rule.TrailingHiddenTokens ??= new List<HiddenToken>();
                            rule.TrailingHiddenTokens.Add(new HiddenToken()
                            {
                                TokenType = FmlMappingLexer.LINE_COMMENT,
                                Text = $" // Warning: type changed ({String.Join(",", se.Type?.Select(t => t.Code))} -> {String.Join(",", matchingTe.Type?.Select(t => t.Code))})"
                            });

                            if (!sourceIsBackbone)
                            {
                                // The source is the datatype: splice its (recursively expanded)
                                // children into the source stream so they are visited within the
                                // group just pushed.
                                var expandedSource = ExpandType(se, SourceResolver!, se.Path).ToList();
                                sourceElements.InsertRange(i + 1, expandedSource);
                            }
                            if (!targetIsBackbone)
                            {
                                // The target is the datatype: add its (recursively expanded) children
                                // to the pool so source children can match by name or via renames
                                // (including cross-level renames into nested datatype properties), and
                                // report any that go unmapped.
                                var expandedTarget = ExpandType(matchingTe, TargetResolver!, matchingTe.Path).ToList();
                                targetElements.AddRange(expandedTarget);
                                missedTargetElements.AddRange(expandedTarget);
                            }
                        }
                    }
                    else
                    {
                        // Check the types for miss-matching types to include messages there too
                        string sourceTypes = String.Join(",", se.Type?.Select(t => t.Code));
                        string sourceTargetProfiles = String.Join(",", se.Type?.SelectMany(t => t.TargetProfile.Select(t => t.Replace("http://hl7.org/fhir/StructureDefinition/", ""))) ?? Enumerable.Empty<string>());
                        string targetTypes = String.Join(",", matchingTe.Type?.Select(t => t.Code));
                        string targetTargetProfiles = String.Join(",", matchingTe.Type?.SelectMany(t => t.TargetProfile.Select(t => t.Replace("http://hl7.org/fhir/StructureDefinition/", "")))?.Where(t => !string.IsNullOrEmpty(t)) ?? Enumerable.Empty<string>());
                        if (!AreTypesCompatible(sourceTypes, targetTypes))
                        {
                            mappingWarningMessage = (mappingWarningMessage != null ? mappingWarningMessage + "    " : " // Warning: ") + $"Source Type unsupported: {InCompatibleTypes(sourceTypes, targetTypes)}  ({sourceTypes} -> {targetTypes})";
                            CloneRuleForTypes(groupStack.Peek().Rules, rule, sourceTypes, targetTypes, matchingTe);
                        }
                        if (!AreTypesCompatible(sourceTargetProfiles, targetTargetProfiles) && targetTargetProfiles.Any()) // no target profile means ANY
                        {
                            mappingWarningMessage = (mappingWarningMessage != null ? mappingWarningMessage + "    " : " // Warning: ") + $"Source TargetProfile unsupported: {InCompatibleTypes(sourceTargetProfiles, targetTargetProfiles)}  ({sourceTargetProfiles} -> {targetTargetProfiles})";
                        }

                        groupStack.Peek().Rules.Add(rule);

                        if (se.Path.Replace("[x]", "") != matchingTe.Path.Replace("[x]", ""))
                        {
                            rule.TrailingHiddenTokens ??= new List<HiddenToken>();
                            rule.TrailingHiddenTokens.Add(new HiddenToken()
                            {
                                TokenType = FmlMappingLexer.LINE_COMMENT,
                                Text = " // renamed"
                            });
                            rule.SetAnnotation(new ElementRenamedAnnotation());
                            rule.Sources[0].SetAnnotation(new ElementDefAnnotation(se));
                            rule.Targets[0].SetAnnotation(new ElementDefAnnotation(matchingTe));

                            fml.SetAnnotation(new NeedsReviewAnnotation());
                            groupStack.Peek().SetAnnotation(new NeedsReviewAnnotation());
                            rule.SetAnnotation(new NeedsReviewAnnotation());
                        }
                    }

                    if (mappingWarningMessage != null)
                    {
                        rule.TrailingHiddenTokens ??= new List<HiddenToken>();
                        rule.TrailingHiddenTokens.Add(new HiddenToken()
                        {
                            TokenType = FmlMappingLexer.LINE_COMMENT,
                            Text = mappingWarningMessage
                        });
                        fml.SetAnnotation(new NeedsReviewAnnotation());
                        groupStack.Peek().SetAnnotation(new NeedsReviewAnnotation());
                        rule.SetAnnotation(new NeedsReviewAnnotation());
                    }

                    if (displayCardinality)
                    {
                        rule.Sources[0].TrailingHiddenTokens ??= new List<HiddenToken>();
                        rule.Sources[0].TrailingHiddenTokens.Add(new HiddenToken()
                        {
                            TokenType = FmlMappingLexer.COMMENT,
                            Text = $" /* [{se.Min}..{se.Max}] */"
                        });

                        rule.Targets[0].TrailingHiddenTokens ??= new List<HiddenToken>();
                        rule.Targets[0].TrailingHiddenTokens.Add(new HiddenToken()
                        {
                            TokenType = FmlMappingLexer.COMMENT,
                            Text = $" /* [{matchingTe.Min}..{matchingTe.Max}] */"
                        });
                    }
                }
                else
                {
                    missedSourceElements.Add(se);
                }
            }
        }

        // Append any leftover custom rules. Per the custom-rules file semantics, rules whose source
        // matched a generated property replace that property's rule (handled during the walk); any
        // remaining custom rules (for example a rule whose source is the whole context, such as a
        // "src where (...) -> ..." aggregation) are appended to the end of the generated group.
        AppendLeftoverCustomRules(fml, customGroupTargetRoots, consumedCustomRules, missedTargetElements);

        if (missedSourceElements.Any())
        {
            string comment = "/* The following source properties were not read:\n";
            foreach (var element in missedSourceElements)
            {
                var targetTypes = String.Join(",", element.Type?.Select(t => t.Code));
                comment += $"    {element.Path} {targetTypes}[{element.Min}..{element.Max}]";
                IEnumerable<string> targetProfiles = element.Type?.SelectMany(t => t.TargetProfile.Select(tp => tp.Replace("http://hl7.org/fhir/StructureDefinition/", ""))) ?? Enumerable.Empty<string>();
                if (targetProfiles.Any())
                    comment += $" ({String.Join(",", targetProfiles)})";
                comment += "\n";
                fml.AddAnnotation(new NotReadElementAnnotation(element));
            }
            comment += "*/\n";
            if (group.Rules.Any())
            {
                group.LeadingHiddenTokens ??= new List<HiddenToken>();
                group.LeadingHiddenTokens.Add(new HiddenToken()
                {
                    TokenType = FmlMappingLexer.LINE_COMMENT,
                    Text = comment
                });
            }
            fml.SetAnnotation(new NeedsReviewAnnotation());
        }
        if (missedTargetElements.Any())
        {
            string comment = "/* The following target properties were not populated:\n";
            foreach (var element in missedTargetElements)
            {
                var targetTypes = String.Join(",", element.Type?.Select(t => t.Code));
                comment += $"    {element.Path} {targetTypes}[{element.Min}..{element.Max}]";
                IEnumerable<string> targetProfiles = element.Type?.SelectMany(t => t.TargetProfile.Select(tp => tp.Replace("http://hl7.org/fhir/StructureDefinition/", ""))) ?? Enumerable.Empty<string>();
                if (targetProfiles.Any())
                    comment += $" ({String.Join(",", targetProfiles)})";
                comment += "\n";
                fml.AddAnnotation(new NotPopulatedElementAnnotation(element));
            }
            comment += "*/\n";
            if (group.Rules.Any())
            {
                group.LeadingHiddenTokens ??= new List<HiddenToken>();
                group.LeadingHiddenTokens.Add(new HiddenToken()
                {
                    TokenType = FmlMappingLexer.LINE_COMMENT,
                    Text = comment
                });
            }
            // don't need to review props missed, if there are none to put somewhere (for now)
            // as these are likely legitimately new properties
            fml.SetAnnotation(new NeedsReviewAnnotation()); 
        }

        return fml;
    }

    private void CloneRuleForTypes(List<Rule> rules, Rule rule, string sourceTypes, string targetTypes, ElementDefinition matchingTe)
    {
        var sb = new StringBuilder();
        FmlSerializer.SerializeRule(sb, rule, 1);
        var ruleString = sb.ToString();

        rule.LeadingHiddenTokens ??= new List<HiddenToken>();
        rule.LeadingHiddenTokens.Add(new HiddenToken()
        {
            TokenType = FmlMappingLexer.LINE_COMMENT,
            Text = $"// rule changed to {sourceTypes} -> {targetTypes}\n// "
        });

        // clone the rule for each type
        var sourceTypeList = sourceTypes.Split(',').Select(t => t.Trim()).Where(t => !String.IsNullOrEmpty(t)).ToHashSet();
        var targetTypeList = targetTypes.Split(',').Select(t => t.Trim()).Where(t => !String.IsNullOrEmpty(t)).ToHashSet();

        foreach (var sourceType in sourceTypeList)
        {
            if (targetTypeList.Contains(sourceType))
            {
                // this is a direct mapping, can use it
                sourceTypeList.Remove(sourceType);
                targetTypeList.Remove(sourceType);

                var typedRule = FmlParser.ParseRule(ruleString);
                typedRule.Sources[0].Type = sourceType;
                rules.Add(typedRule);
            }
            else
            {
                // remove any compatible pairs from the source/target lists
                foreach (var pair in compatiblePairs.Where(p => p.Item1 == sourceType))
                {
                    if (targetTypeList.Contains(pair.Item2))
                    {
                        sourceTypeList.Remove(pair.Item1);
                        targetTypeList.Remove(pair.Item2);

                        var typedRule = FmlParser.ParseRule(ruleString);
                        typedRule.Sources[0].Type = pair.Item1;
                        rules.Add(typedRule);
                        break;
                    }
                }
            }
        }

        // Now grab these ones from the xversion extension
        var incompatibleTypes = sourceTypeList.Except(targetTypeList);
        foreach (var sourceType in incompatibleTypes)
        {
            var typedRule = FmlParser.ParseRule(ruleString);
            typedRule.Name = $"xver{PascalCase(typedRule.Sources[0].Element)}{PascalCase(sourceType)}";
            typedRule.Sources[0].Element = "extension";
            typedRule.Sources[0].Variable = "e";
            typedRule.Sources[0].Condition = $"url = 'http://hl7.org/fhir/6.0/StructureDefinition/extension-{matchingTe.Path}'";
            typedRule.Targets[0].Transform = new Transform() { Type = "evaluate", Parameters = [ new TransformParameter() { Value = "e.value", Type = TransformParameterType.Expression }] };
            rules.Add(typedRule);
        }
    }

    /// <summary>
    /// The left hand type is a compatible source to the right hand type.
    /// </summary>
    private readonly List<(string, string)> compatiblePairs = new List<(string, string)>
        {
            ("CodeableConcept", "CodeableReference"),
            ("Coding", "CodeableConcept"),
            ("date", "dateTime"),
            ("id", "string"),
            ("instant", "dateTime"),
            ("markdown", "string"),
            ("Reference", "CodeableReference"),
            ("Reference", "canonical"), // is this a legit mapping?
            ("string", "markdown"),
            ("string", "Annotation"),
            ("string", "CodeableConcept"),
            ("string", "CodeableReference"), // need to select where the string should go, display or text?
            ("string", "Reference"), // goes into the display property
            ("unsignedInt", "integer64"),
            ("uri", "canonical"),
            ("url", "uri"),
        };

    private bool AreTypesCompatible(string sourceTypes, string targetTypes)
    {
        var sourceTypeList = sourceTypes.Split(',').Select(t => t.Trim()).Where(t => !String.IsNullOrEmpty(t)).ToHashSet();
        var targetTypeList = targetTypes.Split(',').Select(t => t.Trim()).Where(t => !String.IsNullOrEmpty(t)).ToHashSet();

        // ensure that all source types are in target types
        // (the target having more types available is fine, just not the other way around)
        // the simple case
        if (sourceTypeList.IsSubsetOf(targetTypeList))
            return true;

        // remove any compatible pairs from the source/target lists
        foreach (var pair in compatiblePairs)
        {
            if (sourceTypeList.Contains(pair.Item1) && targetTypeList.Contains(pair.Item2))
            {
                sourceTypeList.Remove(pair.Item1);
            }
        }
        if (sourceTypeList.IsSubsetOf(targetTypeList))
            return true;

        return false;
    }

    private string InCompatibleTypes(string sourceTypes, string targetTypes)
    {
        var sourceTypeList = sourceTypes.Split(',').Select(t => t.Trim()).Where(t => !String.IsNullOrEmpty(t)).ToHashSet();
        var targetTypeList = targetTypes.Split(',').Select(t => t.Trim()).Where(t => !String.IsNullOrEmpty(t)).ToHashSet();

        // remove any compatible pairs from the source/target lists
        foreach (var pair in compatiblePairs)
        {
            if (sourceTypeList.Contains(pair.Item1) && targetTypeList.Contains(pair.Item2))
            {
                sourceTypeList.Remove(pair.Item1);
            }
        }

        // report any types in the source list that aren't in the target list
        var incompatibleTypes = sourceTypeList.Except(targetTypeList);
        return String.Join(",", incompatibleTypes);
    }

    string? getFhirVersion(string? version)
    {
        return version?.Substring(0, 3) switch
        {
            "3.0" => "STU3",
            "4.0" => "R4",
            "4.3" => "R4B",
            "5.0" => "R5",
            "6.0" => "R6",
            _ => null,
        };
    }
    private string? UseScope(FmlStructureMap map, string dt, StructureMode mode)
    {
        Canonical canonical = new Canonical(dt);

        string resourceType = canonical.Uri.Substring(canonical.Uri.LastIndexOf("/") + 1);
        string? fhirVersion = getFhirVersion(canonical.Version);
        map.Structures.Add(new StructureDeclaration()
        {
            Url = $"http://hl7.org/fhir/{canonical.Version?.Substring(0, 3)}/StructureDefinition/{resourceType}",
            Alias = resourceType + fhirVersion,
            Mode = mode
        });
        return resourceType + fhirVersion;
    }

	/// <summary>
	/// Converts a string to PascalCase, removing underscores, spaces, hyphens, and dots, and capitalizing the first letter of each word.
	/// </summary>
	/// <param name="input"></param>
	/// <returns></returns>
	public static string PascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var words = input.Split(new char[] { '_', ' ', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
        var pascalCased = string.Concat(words.Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1)));
        return pascalCased;
    }
    private GroupDeclaration? ConvertGroupToFml(ConceptMap.GroupComponent group)
    {
        if (string.IsNullOrEmpty(group.Source) || string.IsNullOrEmpty(group.Target))
        {
            return null;
        }

        // Extract resource/backbone type names from the StructureDefinition URLs
        string sourceName = ExtractTypeName(group.Source);
        string targetName = ExtractTypeName(group.Target);

        // Create a group name based on the source and target types
        string groupName = $"{PascalCase(sourceName)}_To_{PascalCase(targetName)}";
        if (PascalCase(sourceName) == PascalCase(targetName))
            groupName = PascalCase(sourceName);

        var fmlGroup = new GroupDeclaration
        {
            Name = groupName,
        };

        // Add parameters for source and target
        fmlGroup.Parameters.Add(new GroupParameter
        {
            Mode = ParameterMode.Source,
            Type = !sourceName.Contains(".") ? sourceName : null,
            Name = "src"
        });

        fmlGroup.Parameters.Add(new GroupParameter
        {
            Mode = ParameterMode.Target,
            Type = !targetName.Contains(".") ? targetName : null,
            Name = "tgt"
        });

        // Convert each element mapping into a rule
        foreach (var element in group.Element)
        {
            var rule = ConvertElementToRule(element, sourceName, targetName);
            if (rule != null)
            {
                fmlGroup.Rules.Add(rule);
            }
        }

        return fmlGroup;
    }

    private Rule? ConvertElementToRule(ConceptMap.SourceElementComponent element, string sourceName, string targetName)
    {
        if (string.IsNullOrEmpty(element.Code))
        {
            return null;
        }

        var rule = new Rule();

        // Create source from element code
        rule.Sources.Add(new RuleSource
        {
            Context = "src",
            Element = element.Code.Replace("[x]", ""),
            // Variable = SanitizeVariableName(element.Code)
        });

        // Create targets from element targets
        if (element.Target != null && element.Target.Count > 0)
        {
            foreach (var target in element.Target)
            {
                if (!string.IsNullOrEmpty(target.Code))
                {
                    var ruleTarget = new RuleTarget
                    {
                        Context = "tgt",
                        Element = target.Code.Replace("[x]", "")
                    };

                    if (target.Code != element.Code)
                    {
                        // this was a rename, so lets add a comment to mark that after the rule
                        var ht = new HiddenToken()
                        {
                            TokenType = FmlMappingLexer.LINE_COMMENT,
                            Text = $"  // Renamed"
                        };
                        rule.TrailingHiddenTokens ??= new List<HiddenToken>();
                        rule.TrailingHiddenTokens.Add(ht);
                    }

                    if (target.Relationship != ConceptMap.ConceptMapRelationship.Equivalent)
                    {
                        var ht = new HiddenToken()
                        {
                            TokenType = FmlMappingLexer.LINE_COMMENT,
                            Text = $"  // {target.Relationship.GetLiteral()}"
                        };
                        rule.TrailingHiddenTokens ??= new List<HiddenToken>();
                        rule.TrailingHiddenTokens.Add(ht);
                    }


                    // Add copy transform for the mapping
                    //ruleTarget.Transform = new Transform
                    //{
                    //	Type = TransformType.Copy,
                    //	Parameters = new List<TransformParameter>
                    //	{
                    //		new TransformParameter
                    //		{
                    //			Type = TransformParameterType.Identifier,
                    //			Value = SanitizeVariableName(element.Code)
                    //		}
                    //	}
                    //};

                    rule.Targets.Add(ruleTarget);
                }
            }
        }
        else
        {
            // the no map code!
            // inject some comment tokens before our node to indicate this
            var ht = new HiddenToken()
            {
                TokenType = FmlMappingLexer.LINE_COMMENT,
                Text = "  // No target mapping for "
            };
            rule.LeadingHiddenTokens ??= new List<HiddenToken>();
            rule.LeadingHiddenTokens.Add(ht);
        }

        return rule;
    }

    public static string ExtractTypeName(string structureDefinitionUrl)
    {
        if (structureDefinitionUrl.StartsWith("http://hl7.org/fhir/StructureDefinition/"))
        {
            return structureDefinitionUrl.Substring("http://hl7.org/fhir/StructureDefinition/".Length);
        }

        // Fallback: try to get the last segment after the last /
        var lastSlash = structureDefinitionUrl.LastIndexOf('/');
        if (lastSlash >= 0 && lastSlash < structureDefinitionUrl.Length - 1)
        {
            return structureDefinitionUrl.Substring(lastSlash + 1);
        }

        return structureDefinitionUrl;
    }

    private string SanitizeVariableName(string elementCode)
    {
        // Replace dots and other special characters with underscores for variable names
        // Also make first letter lowercase for variable convention
        var sanitized = elementCode.Replace('.', '_').Replace('[', '_').Replace(']', '_');

        if (sanitized.Length > 0 && char.IsUpper(sanitized[0]))
        {
            sanitized = char.ToLowerInvariant(sanitized[0]) + sanitized.Substring(1);
        }

        return sanitized;
    }

    public static void SetMetadata(FmlStructureMap map, string name, string value)
    {
        map.Metadata ??= new List<MetadataDeclaration>();
        var metadata = map.Metadata.FirstOrDefault(m => m.Path == name);
        if (metadata == null)
        {
            metadata = new MetadataDeclaration();
            metadata.Path = name;
            map.Metadata.Add(metadata);
        }
        metadata.Value = value;
    }


    // Implementation of the ConceptMapConverter class
    public ConceptMap Convert(ConceptMap source)
    {
        var result = source.DeepCopy() as ConceptMap;
        result.UseContext = null;
        result.Group.Clear();

        // scan all the groups
        foreach (var group in source.Group)
        {
            // is this a resource level mapping?
            if (!group.Source.StartsWith("http://hl7.org/fhir/StructureDefinition/"))
                break;

            string sourceResourceType = group.Source.Substring("http://hl7.org/fhir/StructureDefinition/".Length);
            string targetResourceType = group.Target.Substring("http://hl7.org/fhir/StructureDefinition/".Length);

            // scan all the elements in the group
            // and split elements into separate groups based on resource/backbone element types (split on .)
            // e.g. Observation.component.valueQuantity will go into group http://hl7.org/fhir/StructureDefinition/Observation.component
            Stack<ConceptMap.GroupComponent> currentGroup = new Stack<ConceptMap.GroupComponent>();
            Stack<string> currentSourcePath = new Stack<string>();
            Stack<string> currentTargetPath = new Stack<string>();

            foreach (var element in group.Element.Skip(1))
            {
                // Determine the backbone element path for this element
                // e.g., "Account.balance.amount" -> backbone is "Account.balance"
                string elementSourcePath = GetBackboneElementPath(element.Code);

                // Determine the corresponding target path from the first target
                string elementTargetPath = elementSourcePath;
                if (element.Target != null && element.Target.Count > 0 && !string.IsNullOrEmpty(element.Target[0].Code))
                {
                    elementTargetPath = GetBackboneElementPath(element.Target[0].Code);
                }

                // Pop groups until we're at the right level
                while (currentSourcePath.Count > 0 && !IsChildOf(elementSourcePath, currentSourcePath.Peek()))
                {
                    currentGroup.Pop();
                    currentSourcePath.Pop();
                    currentTargetPath.Pop();
                }

                // Push new groups if we're entering a new backbone element
                if (currentSourcePath.Count == 0 || elementSourcePath != currentSourcePath.Peek())
                {
                    var newGroup = new ConceptMap.GroupComponent()
                    {
                        Source = "http://hl7.org/fhir/StructureDefinition/" + elementSourcePath,
                        Target = "http://hl7.org/fhir/StructureDefinition/" + elementTargetPath,
                    };
                    currentGroup.Push(newGroup);
                    currentSourcePath.Push(elementSourcePath);
                    currentTargetPath.Push(elementTargetPath);
                    result.Group.Add(newGroup);
                }

                // Add this element to the current group if it's a direct child
                if (currentSourcePath.Count > 0 && currentGroup.Count > 0 &&
                    IsDirectChildOf(element.Code, currentSourcePath.Peek()))
                {
                    // Create a new element with relative path (just the last component)
                    var newElement = element.DeepCopy() as ConceptMap.SourceElementComponent;
                    newElement.Code = GetLastPathComponent(element.Code);

                    // Update target codes to be relative as well
                    if (newElement.Target != null)
                    {
                        foreach (var target in newElement.Target)
                        {
                            if (!string.IsNullOrEmpty(target.Code))
                            {
                                target.Code = GetLastPathComponent(target.Code);
                            }
                        }
                    }

                    currentGroup.Peek().Element.Add(newElement);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the backbone element path for a given element code.
    /// e.g., "Account.balance.amount" -> "Account.balance"
    ///       "Account.balance" -> "Account.balance"
    ///       "Account" -> "Account"
    /// </summary>
    private string GetBackboneElementPath(string elementCode)
    {
        var lastDotIndex = elementCode.LastIndexOf('.');
        if (lastDotIndex == -1)
        {
            // No dot, this is the resource level
            return elementCode;
        }

        // Check if the part after the last dot starts with a lowercase letter
        // If so, it's a property, and we want the parent path
        var lastComponent = elementCode.Substring(lastDotIndex + 1);
        if (lastComponent.Length > 0 && char.IsLower(lastComponent[0]))
        {
            // This is a property, return the backbone element path
            return elementCode.Substring(0, lastDotIndex);
        }

        // This is itself a backbone element
        return elementCode;
    }

    /// <summary>
    /// Checks if childPath is a child of parentPath.
    /// e.g., "Account.balance.amount" is a child of "Account.balance"
    ///       "Account.balance" is a child of "Account"
    /// </summary>
    private bool IsChildOf(string childPath, string parentPath)
    {
        if (childPath == parentPath)
            return true;

        return childPath.StartsWith(parentPath + ".");
    }

    /// <summary>
    /// Checks if elementPath is a direct child of parentPath.
    /// e.g., "Account.balance.amount" is a direct child of "Account.balance"
    ///       "Account.balance" is a direct child of "Account.balance" (the backbone element itself)
    ///       "Account.balance" is a direct child of "Account"
    ///       "Account.balance.amount" is NOT a direct child of "Account"
    /// </summary>
    private bool IsDirectChildOf(string elementPath, string parentPath)
    {
        // If they're equal, this is the backbone element itself within its own group
        if (elementPath == parentPath)
            return true;

        if (!elementPath.StartsWith(parentPath + "."))
            return false;

        // Get the relative path
        var relativePath = elementPath.Substring(parentPath.Length + 1);

        // Check if there's no further nesting (no dots in the relative path)
        return !relativePath.Contains('.');
    }

    /// <summary>
    /// Gets the last path component.
    /// e.g., "Account.balance.amount" -> "amount"
    ///       "Account" -> "Account"
    /// </summary>
    private string GetLastPathComponent(string path)
    {
        var lastDotIndex = path.LastIndexOf('.');
        if (lastDotIndex == -1)
        {
            return path;
        }

        return path.Substring(lastDotIndex + 1);
    }

    Dictionary<string, GroupDeclaration> _customRules = new Dictionary<string, GroupDeclaration>();

    string GetCustomRuleName(GroupDeclaration group)
    {
        // Build a normalized signature key from the parameter properties directly rather than
        // via the serializer, so that hidden whitespace tokens carried by parsed custom-rule
        // groups don't cause a mismatch with the generated groups (both must produce the same key).
        var sb = new StringBuilder();
        sb.Append(group.Name);
        sb.Append("(");
        for (int i = 0; i < group.Parameters.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            var p = group.Parameters[i];
            sb.Append(p.Mode == ParameterMode.Source ? "source" : "target");
            sb.Append(" ");
            sb.Append(p.Name);
            if (!string.IsNullOrEmpty(p.Type))
            {
                sb.Append(" : ");
                sb.Append(p.Type);
            }
        }
        sb.Append(")");
        return sb.ToString();
    }

    internal void SetCustomRules(List<GroupDeclaration> groups)
    {
        _customRules = new Dictionary<string, GroupDeclaration>();
        foreach (var group in groups)
        {
            _customRules[GetCustomRuleName(group)] = group;
        }
    }
}

# FSH Processor - Implementation Status

## Completed ?

### Core Infrastructure
- **Models**: All 17 model files created and compiling
  - FshDoc.cs - Root document model
  - FshNode.cs - Base node with position tracking and hidden tokens
  - ParseResult.cs - Success/Failure discriminated union
  - Entity models: Alias, Profile, Extension, Logical, Resource, Instance, Invariant, ValueSet, CodeSystem, RuleSet, Mapping
  - Rules.cs - 40+ rule types across all categories (SD, LR, Instance, Invariant, ValueSet, CodeSystem, Mapping)
  - Values.cs - 14 value types (String, Number, Code, Quantity, Ratio, Reference, Canonical, etc.)

### Parser Infrastructure
- **FshParser.cs**: Main parser with Parse() and ParseOrThrow() methods
- **FshModelVisitor.cs**: Working ANTLR visitor implementation with:
  - Document and entity parsing ?
  - All 7 core entities: Alias, Profile, Extension, Logical, Resource, Instance, Invariant ?
  - Metadata processing for all entity types ?
  - SD rules: CardRule, FlagRule, ValueSetRule, FixedValueRule, ContainsRule, OnlyRule, ObeysRule, CaretValueRule, InsertRule, PathRule ?
  - LR rules: Converts SD rules to LR equivalents ?
  - Instance/Invariant rules: Converts to type-specific rules ?
  - Value visitors: All 14 value types ?
  - Hidden token preservation (leading, trailing, EOF) ?
  - Position tracking for all nodes ?

## Remaining Work ??

### Entity Visitors (Not Yet Implemented)
These follow the same pattern as completed entities - add incrementally as needed:
- [ ] **ValueSet** - `VisitValueSet`, `VisitVsRule`, `VisitVsComponent`, filter components
- [ ] **CodeSystem** - `VisitCodeSystem`, `VisitCsRule`, `VisitConcept`
- [ ] **RuleSet** - `VisitRuleSet`, `VisitRuleSetRule`
- [ ] **ParamRuleSet** - `VisitParamRuleSet`, parameter extraction
- [ ] **Mapping** - `VisitMapping`, `VisitMappingEntityRule`, `VisitMappingRule`

### Rule Visitors (Not Yet Implemented)
- [ ] **AddElementRule** - `VisitAddElementRule` for LR rules
- [ ] **AddCRElementRule** - `VisitAddCRElementRule` for CodeableReference elements

### ValueSet-specific Rules
- [ ] **VsComponentRule** - Include/exclude concepts and filters
- [ ] **VsCaretValueRule** - Caret values for value sets
- [ ] **VsInsertRule** - Insert rule sets into value sets
- [ ] **CodeCaretValueRule** - Caret values for specific codes
- [ ] **CodeInsertRule** - Insert rule sets for specific codes

### CodeSystem-specific Rules
- [ ] **Concept** - Code concept definitions
- [ ] **CsCaretValueRule** - Caret values for code systems
- [ ] **CsInsertRule** - Insert rule sets into code systems

### Mapping-specific Rules
- [ ] **MappingMapRule** - Map rules with target, language, code
- [ ] **MappingInsertRule** - Insert rule sets into mappings
- [ ] **MappingPathRule** - Path rules for mappings

### Parameterized Rule Sets
- [ ] Extract parameters from `paramRuleSetRef`
- [ ] Handle parameter substitution in rules

### Serialization
- [ ] **FshSerializer.cs** - Round-trip serialization back to FSH text
  - Entity serialization for all types
  - Rule serialization for all types
  - Value serialization for all types
  - Hidden token output (whitespace/comment preservation)
  - Format preservation

### Testing
- [ ] Unit tests for parser (similar to FML ParserTests)
- [ ] Round-trip tests (parse ? serialize ? parse)
- [ ] Error handling tests
- [ ] Position tracking verification
- [ ] Hidden token preservation tests

## Implementation Pattern

All remaining visitors follow the same established pattern:

```csharp
public override object? VisitEntityName([NotNull] FSHParser.EntityNameContext context)
{
    // 1. Create entity object
    var entity = new EntityType
    {
        Position = GetPosition(context),
        LeadingHiddenTokens = GetLeadingHiddenTokens(context),
        TrailingHiddenTokens = GetTrailingHiddenTokens(context),
        Name = context.name().GetText()
    };

    // 2. Process metadata (if applicable)
    foreach (var metadata in context.metadata())
    {
        ProcessMetadata(entity, metadata);
    }

    // 3. Process child rules/elements
    foreach (var rule in context.rules())
    {
        var ruleObj = Visit(rule) as RuleType;
        if (ruleObj != null)
        {
            entity.Rules.Add(ruleObj);
        }
    }

    return entity;
}
```

## Usage Example

```csharp
using fsh_processor;

// Parse FSH text
var result = FshParser.Parse(fshText);

if (result is ParseResult.Success success)
{
    var doc = success.Document;
    
    // Access entities
    foreach (var entity in doc.Entities)
    {
        if (entity is Profile profile)
        {
            Console.WriteLine($"Profile: {profile.Name}");
            Console.WriteLine($"Parent: {profile.Parent}");
            Console.WriteLine($"Rules: {profile.Rules.Count}");
        }
        else if (entity is Alias alias)
        {
            Console.WriteLine($"Alias: {alias.Name} = {alias.Value}");
        }
    }
}
else if (result is ParseResult.Failure failure)
{
    foreach (var error in failure.Errors)
    {
        Console.WriteLine($"{error.Location}: {error.Message}");
    }
}
```

## Testing with Sample FSH

The visitor currently handles FSH files like the one in context (SDCTaskQuestionnaire.fsh):
- ? Profile declarations with Parent
- ? Metadata (Id, Title, Description)
- ? Card rules (* status 1..1 MS)
- ? Only rules (* status only code)
- ? ValueSet binding rules (* code from TaskCode (required))
- ? Reference type rules (* focus only Reference(SDCQuestionnaireServiceRequest))
- ? Flag rules (* focus MS)
- ? Caret rules (* ^short = "...")
- ? Contains rules with slicing
- ? Path rules
- ? Fixed value rules (* type = $temp#questionnaire)
- ? Invariant declarations

## Next Steps

1. **Add ValueSet/CodeSystem visitors** - Most FSH files use these
2. **Add RuleSet visitors** - Common for reusable patterns
3. **Implement FshSerializer** - Enable round-trip testing
4. **Add unit tests** - Verify each entity and rule type
5. **Add remaining rule types** - AddElement, AddCRElement as needed

The foundation is solid and working. Additional functionality can be added incrementally following the established patterns.

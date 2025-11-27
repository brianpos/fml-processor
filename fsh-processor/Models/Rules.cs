namespace fsh_processor.Models;

/// <summary>
/// Base class for all FSH rules
/// </summary>
public abstract class FshRule : FshNode
{
    /// <summary>
    /// Path (optional for some rules)
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Indent whitespace for all rules
    /// (before the * character)
    /// </summary>
    public required string Indent { get; set; }
}

// ============================================
// SD Rules (for Profile and Extension)
// ============================================

/// <summary>
/// Base class for structure definition rules
/// </summary>
public abstract class SdRule : FshRule
{
}

/// <summary>
/// Cardinality rule (* path Card flag*)
/// </summary>
public class CardRule : SdRule
{
    /// <summary>
    /// Cardinality (e.g., "0..1", "1..*")
    /// </summary>
    public string Cardinality { get; set; } = string.Empty;

    /// <summary>
    /// Flags
    /// </summary>
    public List<string> Flags { get; set; } = new();
}

/// <summary>
/// Flag rule (* path flag+)
/// </summary>
public class FlagRule : SdRule
{
    /// <summary>
    /// Additional paths (from AND clauses)
    /// </summary>
    public List<string> AdditionalPaths { get; set; } = new();

    /// <summary>
    /// Flags
    /// </summary>
    public List<string> Flags { get; set; } = new();
}

/// <summary>
/// ValueSet binding rule (* path from ValueSet strength?)
/// </summary>
public class ValueSetRule : SdRule
{
    /// <summary>
    /// ValueSet name
    /// </summary>
    public string ValueSetName { get; set; } = string.Empty;

    /// <summary>
    /// Binding strength (example, preferred, extensible, required)
    /// </summary>
    public string? Strength { get; set; }
}

/// <summary>
/// Fixed value rule (* path = value exactly?)
/// </summary>
public class FixedValueRule : FshRule
{
    /// <summary>
    /// The fixed value
    /// </summary>
    public FshValue? Value { get; set; }

    /// <summary>
    /// Whether "exactly" modifier is used
    /// </summary>
    public bool Exactly { get; set; }
}

/// <summary>
/// Contains rule (* path contains item+)
/// </summary>
public class ContainsRule : SdRule
{
    /// <summary>
    /// Items being defined
    /// </summary>
    public List<ContainsItem> Items { get; set; } = new();
}

/// <summary>
/// Item in a contains rule
/// </summary>
public class ContainsItem : FshNode
{
    /// <summary>
    /// Name of the item
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Named alias (from "named" keyword)
    /// </summary>
    public string? NamedAlias { get; set; }

    /// <summary>
    /// Cardinality
    /// </summary>
    public string Cardinality { get; set; } = string.Empty;

    /// <summary>
    /// Flags
    /// </summary>
    public List<string> Flags { get; set; } = new();
}

/// <summary>
/// Only rule (* path only type+)
/// </summary>
public class OnlyRule : SdRule
{
    /// <summary>
    /// Target types
    /// </summary>
    public List<string> TargetTypes { get; set; } = new();
}

/// <summary>
/// Obeys rule (* path? obeys invariant+)
/// </summary>
public class ObeysRule : SdRule
{
    /// <summary>
    /// Invariant names
    /// </summary>
    public List<string> InvariantNames { get; set; } = new();
}

/// <summary>
/// Caret value rule (* path? ^caretPath = value)
/// </summary>
public class CaretValueRule : FshRule
{
    /// <summary>
    /// Caret path (e.g., ^short, ^definition)
    /// </summary>
    public string CaretPath { get; set; } = string.Empty;

    /// <summary>
    /// The value
    /// </summary>
    public FshValue? Value { get; set; }
}

/// <summary>
/// Insert rule (* path? insert RuleSet)
/// </summary>
public class InsertRule : FshRule
{
    /// <summary>
    /// RuleSet reference
    /// </summary>
    public string RuleSetReference { get; set; } = string.Empty;

    /// <summary>
    /// Parameters for parameterized rule sets
    /// </summary>
    public List<string> Parameters { get; set; } = new();

    /// <summary>
    /// Whether this is a parameterized insert
    /// </summary>
    public bool IsParameterized { get; set; }
}

/// <summary>
/// Path rule (* path) - just establishes a path exists
/// </summary>
public class PathRule : SdRule
{
}

// ============================================
// LR Rules (for Logical and Resource)
// ============================================

/// <summary>
/// Base class for logical/resource rules (includes all SD rules plus add element rules)
/// </summary>
public abstract class LrRule : FshRule
{
}

/// <summary>
/// Add element rule (* path Card flag* type+ description?)
/// For Logical and Resource entities only
/// </summary>
public class AddElementRule : LrRule
{
    /// <summary>
    /// Cardinality
    /// </summary>
    public string Cardinality { get; set; } = string.Empty;

    /// <summary>
    /// Flags
    /// </summary>
    public List<string> Flags { get; set; } = new();

    /// <summary>
    /// Target types
    /// </summary>
    public List<string> TargetTypes { get; set; } = new();

    /// <summary>
    /// Short description
    /// </summary>
    public string? ShortDescription { get; set; }

    /// <summary>
    /// Definition (can be multiline)
    /// </summary>
    public string? Definition { get; set; }
}

/// <summary>
/// Add content reference element rule
/// For Logical and Resource entities only
/// </summary>
public class AddCRElementRule : LrRule
{
    /// <summary>
    /// Cardinality
    /// </summary>
    public string Cardinality { get; set; } = string.Empty;

    /// <summary>
    /// Flags
    /// </summary>
    public List<string> Flags { get; set; } = new();

    /// <summary>
    /// Content reference target
    /// </summary>
    public string ContentReference { get; set; } = string.Empty;

    /// <summary>
    /// Short description
    /// </summary>
    public string? ShortDescription { get; set; }

    /// <summary>
    /// Definition (can be multiline)
    /// </summary>
    public string? Definition { get; set; }
}

/// <summary>
/// LR-specific card rule
/// </summary>
public class LrCardRule : LrRule
{
    /// <summary>
    /// Cardinality (e.g., "0..1", "1..*")
    /// </summary>
    public string Cardinality { get; set; } = string.Empty;

    /// <summary>
    /// Flags
    /// </summary>
    public List<string> Flags { get; set; } = new();
}

/// <summary>
/// LR-specific flag rule
/// </summary>
public class LrFlagRule : LrRule
{
    /// <summary>
    /// Additional paths (from AND clauses)
    /// </summary>
    public List<string> AdditionalPaths { get; set; } = new();

    /// <summary>
    /// Flags
    /// </summary>
    public List<string> Flags { get; set; } = new();
}

// ============================================
// Instance Rules
// ============================================

/// <summary>
/// Base class for instance rules (limited subset of rules)
/// </summary>
public abstract class InstanceRule : FshRule
{
}

/// <summary>
/// Instance fixed value rule
/// </summary>
public class InstanceFixedValueRule : InstanceRule
{
    /// <summary>
    /// The fixed value
    /// </summary>
    public FshValue? Value { get; set; }

    /// <summary>
    /// Whether "exactly" modifier is used
    /// </summary>
    public bool Exactly { get; set; }
}

/// <summary>
/// Instance insert rule
/// </summary>
public class InstanceInsertRule : InstanceRule
{
    /// <summary>
    /// RuleSet reference
    /// </summary>
    public string RuleSetReference { get; set; } = string.Empty;

    /// <summary>
    /// Parameters for parameterized rule sets
    /// </summary>
    public List<string> Parameters { get; set; } = new();

    /// <summary>
    /// Whether this is a parameterized insert
    /// </summary>
    public bool IsParameterized { get; set; }
}

/// <summary>
/// Instance path rule
/// </summary>
public class InstancePathRule : InstanceRule
{
}

// ============================================
// Invariant Rules
// ============================================

/// <summary>
/// Base class for invariant rules (same as instance rules)
/// </summary>
public abstract class InvariantRule : FshRule
{
}

/// <summary>
/// Invariant fixed value rule
/// </summary>
public class InvariantFixedValueRule : InvariantRule
{
    /// <summary>
    /// The fixed value
    /// </summary>
    public FshValue? Value { get; set; }

    /// <summary>
    /// Whether "exactly" modifier is used
    /// </summary>
    public bool Exactly { get; set; }
}

/// <summary>
/// Invariant insert rule
/// </summary>
public class InvariantInsertRule : InvariantRule
{
    /// <summary>
    /// RuleSet reference
    /// </summary>
    public string RuleSetReference { get; set; } = string.Empty;

    /// <summary>
    /// Parameters for parameterized rule sets
    /// </summary>
    public List<string> Parameters { get; set; } = new();

    /// <summary>
    /// Whether this is a parameterized insert
    /// </summary>
    public bool IsParameterized { get; set; }
}

/// <summary>
/// Invariant path rule
/// </summary>
public class InvariantPathRule : InvariantRule
{
}

// ============================================
// ValueSet Rules
// ============================================

/// <summary>
/// Base class for value set rules
/// </summary>
public abstract class VsRule : FshRule
{
}

/// <summary>
/// ValueSet component rule (* include/exclude? component)
/// </summary>
public class VsComponentRule : VsRule
{
    /// <summary>
    /// Whether this is an include (true) or exclude (false)
    /// Null means no explicit include/exclude keyword
    /// </summary>
    public bool? IsInclude { get; set; }

    /// <summary>
    /// Whether this is a concept component (true) or filter component (false)
    /// </summary>
    public bool IsConceptComponent { get; set; }

    /// <summary>
    /// For concept components: the code
    /// </summary>
    public Code? ConceptCode { get; set; }

    /// <summary>
    /// From clause - system
    /// </summary>
    public string? FromSystem { get; set; }

    /// <summary>
    /// From clause - value sets
    /// </summary>
    public List<string> FromValueSets { get; set; } = new();

    /// <summary>
    /// Filter definitions (for filter components)
    /// </summary>
    public List<VsFilterDefinition> Filters { get; set; } = new();
}

/// <summary>
/// Filter definition in a value set
/// </summary>
public class VsFilterDefinition : FshNode
{
    /// <summary>
    /// Filter property name
    /// </summary>
    public string Property { get; set; } = string.Empty;

    /// <summary>
    /// Operator (= or sequence like "is-a")
    /// </summary>
    public string Operator { get; set; } = string.Empty;

    /// <summary>
    /// Filter value
    /// </summary>
    public FshValue? Value { get; set; }
}

/// <summary>
/// ValueSet caret value rule
/// </summary>
public class VsCaretValueRule : VsRule
{
    /// <summary>
    /// Caret path
    /// </summary>
    public string CaretPath { get; set; } = string.Empty;

    /// <summary>
    /// The value
    /// </summary>
    public FshValue? Value { get; set; }
}

/// <summary>
/// ValueSet insert rule
/// </summary>
public class VsInsertRule : VsRule
{
    /// <summary>
    /// RuleSet reference
    /// </summary>
    public string RuleSetReference { get; set; } = string.Empty;

    /// <summary>
    /// Parameters for parameterized rule sets
    /// </summary>
    public List<string> Parameters { get; set; } = new();

    /// <summary>
    /// Whether this is a parameterized insert
    /// </summary>
    public bool IsParameterized { get; set; }
}

/// <summary>
/// Code caret value rule (for value sets)
/// </summary>
public class CodeCaretValueRule : VsRule
{
    /// <summary>
    /// Codes this applies to
    /// </summary>
    public List<string> Codes { get; set; } = new();

    /// <summary>
    /// Caret path
    /// </summary>
    public string CaretPath { get; set; } = string.Empty;

    /// <summary>
    /// The value
    /// </summary>
    public FshValue? Value { get; set; }
}

/// <summary>
/// Code insert rule (for value sets)
/// </summary>
public class CodeInsertRule : VsRule
{
    /// <summary>
    /// Codes this applies to
    /// </summary>
    public List<string> Codes { get; set; } = new();

    /// <summary>
    /// RuleSet reference
    /// </summary>
    public string RuleSetReference { get; set; } = string.Empty;

    /// <summary>
    /// Parameters for parameterized rule sets
    /// </summary>
    public List<string> Parameters { get; set; } = new();

    /// <summary>
    /// Whether this is a parameterized insert
    /// </summary>
    public bool IsParameterized { get; set; }
}

// ============================================
// CodeSystem Rules
// ============================================

/// <summary>
/// Base class for code system rules
/// </summary>
public abstract class CsRule : FshRule
{
}

/// <summary>
/// Concept definition (* code+ description? definition?)
/// </summary>
public class Concept : CsRule
{
    /// <summary>
    /// Codes (hierarchical)
    /// </summary>
    public List<string> Codes { get; set; } = new();

    /// <summary>
    /// Display text
    /// </summary>
    public string? Display { get; set; }

    /// <summary>
    /// Definition (can be multiline)
    /// </summary>
    public string? Definition { get; set; }
}

/// <summary>
/// CodeSystem caret value rule
/// </summary>
public class CsCaretValueRule : CsRule
{
    /// <summary>
    /// Codes this applies to
    /// </summary>
    public List<string> Codes { get; set; } = new();

    /// <summary>
    /// Caret path
    /// </summary>
    public string CaretPath { get; set; } = string.Empty;

    /// <summary>
    /// The value
    /// </summary>
    public FshValue? Value { get; set; }
}

/// <summary>
/// CodeSystem insert rule
/// </summary>
public class CsInsertRule : CsRule
{
    /// <summary>
    /// Codes this applies to
    /// </summary>
    public List<string> Codes { get; set; } = new();

    /// <summary>
    /// RuleSet reference
    /// </summary>
    public string RuleSetReference { get; set; } = string.Empty;

    /// <summary>
    /// Parameters for parameterized rule sets
    /// </summary>
    public List<string> Parameters { get; set; } = new();

    /// <summary>
    /// Whether this is a parameterized insert
    /// </summary>
    public bool IsParameterized { get; set; }
}

// ============================================
// Mapping Rules
// ============================================

/// <summary>
/// Base class for mapping entity rules
/// </summary>
public abstract class MappingRule : FshRule
{
}

/// <summary>
/// Mapping map rule (* path? -> target language? code?)
/// </summary>
public class MappingMapRule : MappingRule
{
    /// <summary>
    /// Target mapping string
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Language (optional)
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Code (optional)
    /// </summary>
    public string? Code { get; set; }
}

/// <summary>
/// Mapping insert rule
/// </summary>
public class MappingInsertRule : MappingRule
{
    /// <summary>
    /// RuleSet reference
    /// </summary>
    public string RuleSetReference { get; set; } = string.Empty;

    /// <summary>
    /// Parameters for parameterized rule sets
    /// </summary>
    public List<string> Parameters { get; set; } = new();

    /// <summary>
    /// Whether this is a parameterized insert
    /// </summary>
    public bool IsParameterized { get; set; }
}

/// <summary>
/// Mapping path rule
/// </summary>
public class MappingPathRule : MappingRule
{
}

// ============================================
// RuleSet Rules
// ============================================

// Note: RuleSets can contain any FshRule type, so no special RuleSetRule class is needed.
// RuleSet.Rules is typed as List<FshRule> to allow any rule type.

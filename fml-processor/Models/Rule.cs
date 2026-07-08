namespace fml_processor.Models;

/// <summary>
/// Transformation rule
/// </summary>
public class Rule : FmlNode
{
    /// <summary>
    /// Optional name for the rule
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Source elements/expressions
    /// </summary>
    public List<RuleSource> Sources { get; set; } = new();

    /// <summary>
    /// Target transformations (optional - rules can have no targets)
    /// </summary>
    public List<RuleTarget> Targets { get; set; } = new();

    /// <summary>
    /// Dependent rules/group invocations
    /// </summary>
    public RuleDependent? Dependent { get; set; }

    /// <summary>
    /// Identity field list for a "simple batch identity" rule
    /// (the shorthand <c>source -&gt; target : field1, field2</c>).
    /// When non-null the rule is a batch identity rule and is serialized using the
    /// shorthand form; the single <see cref="Sources"/> / <see cref="Targets"/> entries
    /// carry the source and target contexts. Null for all other rule kinds.
    /// </summary>
    public List<string>? IdentityFields { get; set; }
}

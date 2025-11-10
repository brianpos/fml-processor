namespace fml_processor.Models;

/// <summary>
/// Transformation rule
/// </summary>
public class Rule
{
    /// <summary>
    /// Source position information
    /// </summary>
    public SourcePosition? Position { get; set; }

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
}

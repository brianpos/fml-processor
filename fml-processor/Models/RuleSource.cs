namespace fml_processor.Models;

/// <summary>
/// Source element specification in a rule
/// </summary>
public class RuleSource : FmlNode
{
    /// <summary>
    /// Context identifier
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// Element name (optional - if omitted, source is the context itself)
    /// </summary>
    public string? Element { get; set; }

    /// <summary>
    /// Type restriction (optional)
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Minimum cardinality (optional)
    /// </summary>
    public int? Min { get; set; }

    /// <summary>
    /// Maximum cardinality (optional, '*' for unbounded)
    /// Can be either an integer or the string "*"
    /// </summary>
    public object? Max { get; set; }  // int or string ("*")

    /// <summary>
    /// Default value as FHIRPath expression (optional)
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// List option: 'first', 'last', 'not_first', 'not_last', or 'only_one' (optional)
    /// </summary>
    public SourceListMode? ListMode { get; set; }

    /// <summary>
    /// Variable name to assign (optional)
    /// </summary>
    public string? Variable { get; set; }

    /// <summary>
    /// Where clause as FHIRPath expression (optional)
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    /// Check clause as FHIRPath expression (optional)
    /// </summary>
    public string? Check { get; set; }

    /// <summary>
    /// Log statement as FHIRPath expression (optional)
    /// </summary>
    public string? Log { get; set; }
}

/// <summary>
/// List modes for source elements
/// </summary>
public enum SourceListMode
{
    First,
    Last,
    NotFirst,
    NotLast,
    OnlyOne
}

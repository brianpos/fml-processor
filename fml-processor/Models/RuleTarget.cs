namespace fml_processor.Models;

/// <summary>
/// Target transformation specification in a rule
/// </summary>
public class RuleTarget
{
    /// <summary>
    /// Source position information
    /// </summary>
    public SourcePosition? Position { get; set; }

    /// <summary>
    /// Context identifier
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// Element name (may include sub-elements with dots)
    /// </summary>
    public string? Element { get; set; }

    /// <summary>
    /// Transform specification (optional)
    /// </summary>
    public Transform? Transform { get; set; }

    /// <summary>
    /// Variable name to assign (optional)
    /// </summary>
    public string? Variable { get; set; }

    /// <summary>
    /// List mode: 'first', 'share', 'last', or 'single' (optional)
    /// </summary>
    public TargetListMode? ListMode { get; set; }
}

/// <summary>
/// List modes for target elements
/// </summary>
public enum TargetListMode
{
    First,
    Share,
    Last,
    Single
}

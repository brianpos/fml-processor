namespace fml_processor.Models;

/// <summary>
/// Target transformation specification in a rule
/// </summary>
public class RuleTarget : FmlNode
{
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

    /// <summary>
    /// List-rule id used with <see cref="TargetListMode.Share"/>
    /// (the identifier or quoted string following <c>share</c>).
    /// Rules sharing the same id are coalesced into one target instance.
    /// Null when the list mode is not 'share'.
    /// </summary>
    public string? ListRuleId { get; set; }
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

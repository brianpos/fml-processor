namespace fsh_processor.Models;

/// <summary>
/// Alias declaration (Alias: name = value)
/// </summary>
public class Alias : FshEntity
{
    /// <summary>
    /// The value being aliased (URL, code, or sequence)
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

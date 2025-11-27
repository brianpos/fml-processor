namespace fsh_processor.Models;

/// <summary>
/// Extension definition (Extension: name)
/// </summary>
public class Extension : FshEntity
{
    /// <summary>
    /// Parent extension
    /// </summary>
    public string? Parent { get; set; }

    /// <summary>
    /// Id for the extension
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Context items
    /// </summary>
    public List<Context> Contexts { get; set; } = new();

    /// <summary>
    /// Rules defining the extension
    /// </summary>
    public List<FshRule> Rules { get; set; } = new();
}

/// <summary>
/// Context item for extensions
/// </summary>
public class Context : FshNode
{
    /// <summary>
    /// Context value (quoted or unquoted)
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Whether the context was quoted
    /// </summary>
    public bool IsQuoted { get; set; }
}

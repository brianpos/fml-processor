namespace fsh_processor.Models;

/// <summary>
/// Mapping definition (Mapping: name)
/// </summary>
public class Mapping : FshEntity
{
    /// <summary>
    /// Id for the mapping
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Source (what is being mapped)
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Target (what it maps to)
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Rules for the mapping
    /// </summary>
    public List<MappingRule> Rules { get; set; } = new();
}

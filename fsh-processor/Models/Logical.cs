namespace fsh_processor.Models;

/// <summary>
/// Logical model definition (Logical: name)
/// </summary>
public class Logical : FshEntity
{
    /// <summary>
    /// Parent logical model
    /// </summary>
    public string? Parent { get; set; }

    /// <summary>
    /// Id for the logical model
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
    /// Characteristics codes
    /// </summary>
    public List<string> Characteristics { get; set; } = new();

    /// <summary>
    /// Rules defining the logical model
    /// </summary>
    public List<LrRule> Rules { get; set; } = new();
}

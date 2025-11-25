namespace fsh_processor.Models;

/// <summary>
/// Root document representing a complete FSH file
/// Based on the FHIR Shorthand specification
/// </summary>
public class FshDoc : FshNode
{
    /// <summary>
    /// All entities defined in the document
    /// </summary>
    public List<FshEntity> Entities { get; set; } = new();
}

/// <summary>
/// Base class for all FSH entities (alias, profile, extension, etc.)
/// </summary>
public abstract class FshEntity : FshNode
{
    /// <summary>
    /// Name of the entity
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

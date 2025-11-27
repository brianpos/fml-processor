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

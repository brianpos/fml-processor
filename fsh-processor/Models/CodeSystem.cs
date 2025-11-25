namespace fsh_processor.Models;

/// <summary>
/// CodeSystem definition (CodeSystem: name)
/// </summary>
public class CodeSystem : FshEntity
{
    /// <summary>
    /// Id for the code system
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
    /// Rules for the code system (concepts and caret values)
    /// </summary>
    public List<CsRule> Rules { get; set; } = new();
}

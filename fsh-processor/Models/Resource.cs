namespace fsh_processor.Models;

/// <summary>
/// Resource definition (Resource: name)
/// </summary>
public class Resource : FshEntity
{
    /// <summary>
    /// Parent resource
    /// </summary>
    public string? Parent { get; set; }

    /// <summary>
    /// Id for the resource
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
    /// Rules defining the resource
    /// </summary>
    public List<LrRule> Rules { get; set; } = new();
}

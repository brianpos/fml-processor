namespace fsh_processor.Models;

/// <summary>
/// Instance definition (Instance: name)
/// </summary>
public class Instance : FshEntity
{
    /// <summary>
    /// InstanceOf (the type/profile this is an instance of)
    /// </summary>
    public string? InstanceOf { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Usage code
    /// </summary>
    public string? Usage { get; set; }

    /// <summary>
    /// Rules defining the instance values
    /// </summary>
    public List<InstanceRule> Rules { get; set; } = new();
}

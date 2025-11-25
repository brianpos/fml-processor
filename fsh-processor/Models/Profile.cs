namespace fsh_processor.Models;

/// <summary>
/// Profile definition (Profile: name)
/// </summary>
public class Profile : FshEntity
{
    /// <summary>
    /// Parent profile/resource
    /// </summary>
    public string? Parent { get; set; }

    /// <summary>
    /// Id for the profile
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
    /// Rules defining the profile
    /// </summary>
    public List<SdRule> Rules { get; set; } = new();
}

namespace fsh_processor.Models;

/// <summary>
/// Profile definition (Profile: name)
/// </summary>
public class Profile : FshEntity
{
    /// <summary>
    /// Parent profile/resource
    /// </summary>
    public Metadata? Parent { get; set; }

    /// <summary>
    /// Id for the profile
    /// </summary>
    public Metadata? Id { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public Metadata? Title { get; set; }

    /// <summary>
    /// Description
    /// </summary>
    public Metadata? Description { get; set; }

    /// <summary>
    /// Rules defining the profile
    /// </summary>
    public List<FshRule> Rules { get; set; } = new();
}

namespace fml_processor.Models;

/// <summary>
/// Metadata declaration (e.g., /// url = 'http://...', /// name = 'MyMap')
/// </summary>
public class MetadataDeclaration : FmlNode
{
    /// <summary>
    /// The qualified identifier (e.g., 'url', 'name', 'jurisdiction.coding.system')
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// The value of the metadata property
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// For markdown values enclosed in triple quotes
    /// </summary>
    public bool IsMarkdown { get; set; }
}

namespace fml_processor.Models;

/// <summary>
/// Map declaration (map url = identifier)
/// </summary>
public class MapDeclaration : FmlNode
{
    /// <summary>
    /// URL of the map
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Identifier/name of the map
    /// </summary>
    public string Identifier { get; set; } = string.Empty;
}

/// <summary>
/// Structure definition reference (uses statement)
/// </summary>
public class StructureDeclaration : FmlNode
{
    /// <summary>
    /// URL of the structure definition
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Optional alias for the structure
    /// </summary>
    public string? Alias { get; set; }

    /// <summary>
    /// How the structure is used: 'source', 'queried', 'target', or 'produced'
    /// </summary>
    public StructureMode Mode { get; set; }
}

/// <summary>
/// How a structure is used in the mapping
/// </summary>
public enum StructureMode
{
    Source,
    Queried,
    Target,
    Produced
}

/// <summary>
/// Import declaration (imports statement)
/// </summary>
public class ImportDeclaration : FmlNode
{
    /// <summary>
    /// URL of the imported map (may contain wildcards)
    /// </summary>
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Constant declaration (let statement)
/// </summary>
public class ConstantDeclaration : FmlNode
{
    /// <summary>
    /// Name of the constant
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// FHIRPath expression defining the constant value
    /// </summary>
    public string Expression { get; set; } = string.Empty;
}

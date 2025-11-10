namespace fml_processor.Models;

/// <summary>
/// Embedded ConceptMap declaration
/// </summary>
public class ConceptMapDeclaration
{
    /// <summary>
    /// Source position information
    /// </summary>
    public SourcePosition? Position { get; set; }

    /// <summary>
    /// URL/identifier of the concept map
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Prefix declarations for the concept map
    /// </summary>
    public List<ConceptMapPrefix> Prefixes { get; set; } = new();

    /// <summary>
    /// Code mappings
    /// </summary>
    public List<ConceptMapCodeMap> CodeMaps { get; set; } = new();
}

/// <summary>
/// Prefix declaration within a ConceptMap
/// </summary>
public class ConceptMapPrefix
{
    /// <summary>
    /// Source position information
    /// </summary>
    public SourcePosition? Position { get; set; }

    /// <summary>
    /// Prefix identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// URL that the prefix maps to
    /// </summary>
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Code mapping within a ConceptMap
/// </summary>
public class ConceptMapCodeMap
{
    /// <summary>
    /// Source position information
    /// </summary>
    public SourcePosition? Position { get; set; }

    /// <summary>
    /// Source code specification
    /// </summary>
    public ConceptMapCode Source { get; set; } = new();

    /// <summary>
    /// Target code specification
    /// </summary>
    public ConceptMapCode Target { get; set; } = new();
}

/// <summary>
/// Code reference in a ConceptMap
/// </summary>
public class ConceptMapCode
{
    /// <summary>
    /// Prefix identifier
    /// </summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Code value
    /// </summary>
    public string Code { get; set; } = string.Empty;
}

namespace fml_processor.Models;

/// <summary>
/// Root structure representing a complete FML StructureMap
/// Based on the FHIR Mapping Language specification: https://build.fhir.org/mapping-language.html
/// </summary>
public class FmlStructureMap : FmlNode
{

    /// <summary>
    /// Metadata declarations (e.g., /// url = '...', /// name = '...')
    /// </summary>
    public List<MetadataDeclaration> Metadata { get; set; } = new();

    /// <summary>
    /// Embedded ConceptMap declarations
    /// </summary>
    public List<ConceptMapDeclaration> ConceptMaps { get; set; } = new();

    /// <summary>
    /// Map declaration (map url = identifier)
    /// </summary>
    public MapDeclaration? MapDeclaration { get; set; }

    /// <summary>
    /// Structure definitions referenced by the map
    /// </summary>
    public List<StructureDeclaration> Structures { get; set; } = new();

    /// <summary>
    /// Imported maps
    /// </summary>
    public List<ImportDeclaration> Imports { get; set; } = new();

    /// <summary>
    /// Constant declarations
    /// </summary>
    public List<ConstantDeclaration> Constants { get; set; } = new();

    /// <summary>
    /// Groups containing transformation rules
    /// </summary>
    public List<GroupDeclaration> Groups { get; set; } = new();
}

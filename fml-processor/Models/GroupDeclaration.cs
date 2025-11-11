namespace fml_processor.Models;

/// <summary>
/// Group declaration containing transformation rules
/// </summary>
public class GroupDeclaration : FmlNode
{
    /// <summary>
    /// Name of the group
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Input/output parameters
    /// </summary>
    public List<GroupParameter> Parameters { get; set; } = new();

    /// <summary>
    /// Name of the group this extends (optional)
    /// </summary>
    public string? Extends { get; set; }

    /// <summary>
    /// Type mode: 'types' or 'type+' (optional)
    /// </summary>
    public GroupTypeMode? TypeMode { get; set; }

    /// <summary>
    /// Transformation rules within the group
    /// </summary>
    public List<Rule> Rules { get; set; } = new();
}

/// <summary>
/// Parameter for a group
/// </summary>
public class GroupParameter : FmlNode
{
    /// <summary>
    /// Parameter mode: 'source' or 'target'
    /// </summary>
    public ParameterMode Mode { get; set; }

    /// <summary>
    /// Parameter name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional type identifier
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// reference to the ElementDefinition (set once StructureDefinitions are resolved)
    /// (Not part of the parsed content, but injected later during validation)
    /// </summary>
    public Hl7.Fhir.Specification.Navigation.ElementDefinitionNavigator? ParameterElementDefinition { get; set; }
}

/// <summary>
/// Parameter mode for group parameters
/// </summary>
public enum ParameterMode
{
    Source,
    Target
}

/// <summary>
/// Type mode for groups
/// </summary>
public enum GroupTypeMode
{
    Types,
    TypePlus  // 'type+'
}

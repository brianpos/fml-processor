namespace fml_processor.Models;

/// <summary>
/// Transform specification
/// </summary>
public class Transform : FmlNode
{
    /// <summary>
    /// Type of transform
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Parameters for the transform
    /// </summary>
    public List<TransformParameter> Parameters { get; set; } = new();
}

/// <summary>
/// Well-known transform types available in FML
/// </summary>
public static class TransformType
{
    /// <summary>Create a new instance</summary>
    public const string Create = "create";
    
    /// <summary>Copy value as-is</summary>
    public const string Copy = "copy";
    
    /// <summary>Truncate string</summary>
    public const string Truncate = "truncate";
    
    /// <summary>Escape string</summary>
    public const string Escape = "escape";
    
    /// <summary>Cast to different type</summary>
    public const string Cast = "cast";
    
    /// <summary>Append strings</summary>
    public const string Append = "append";
    
    /// <summary>Translate using concept map</summary>
    public const string Translate = "translate";
    
    /// <summary>Create reference string</summary>
    public const string Reference = "reference";
    
    /// <summary>Date operation</summary>
    public const string DateOp = "dateOp";
    
    /// <summary>Generate UUID</summary>
    public const string Uuid = "uuid";
    
    /// <summary>Create pointer reference</summary>
    public const string Pointer = "pointer";
    
    /// <summary>Evaluate FHIRPath expression</summary>
    public const string Evaluate = "evaluate";
    
    /// <summary>Create CodeableConcept</summary>
    public const string Cc = "cc";
    
    /// <summary>Create Coding</summary>
    public const string C = "c";
    
    /// <summary>Create Quantity</summary>
    public const string Qty = "qty";
    
    /// <summary>Create Identifier</summary>
    public const string Id = "id";
    
    /// <summary>Create ContactPoint</summary>
    public const string Cp = "cp";
}

/// <summary>
/// Parameter for a transform
/// </summary>
public class TransformParameter : FmlNode
{
    /// <summary>
    /// Type of parameter
    /// </summary>
    public TransformParameterType Type { get; set; }

    /// <summary>
    /// Value of the parameter (string, number, or boolean)
    /// </summary>
    public object? Value { get; set; }
}

/// <summary>
/// Types of transform parameters
/// </summary>
public enum TransformParameterType
{
    Literal,
    Identifier,
    Expression
}

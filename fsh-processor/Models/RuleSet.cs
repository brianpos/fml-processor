namespace fsh_processor.Models;

/// <summary>
/// RuleSet definition (RuleSet: name)
/// </summary>
public class RuleSet : FshEntity
{
    /// <summary>
    /// Whether this is a parameterized rule set
    /// </summary>
    public bool IsParameterized { get; set; }

    /// <summary>
    /// Parameters for parameterized rule sets
    /// </summary>
    public List<RuleSetParameter> Parameters { get; set; } = new();

    /// <summary>
    /// Rules in the rule set
    /// </summary>
    public List<RuleSetRule> Rules { get; set; } = new();

    /// <summary>
    /// For parameterized rule sets, the unparsed content
    /// </summary>
    public string? UnparsedContent { get; set; }
}

/// <summary>
/// Parameter for a parameterized rule set
/// </summary>
public class RuleSetParameter : FshNode
{
    /// <summary>
    /// Parameter value
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Whether the parameter was bracketed
    /// </summary>
    public bool IsBracketed { get; set; }
}

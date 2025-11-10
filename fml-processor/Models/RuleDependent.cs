namespace fml_processor.Models;

/// <summary>
/// Dependent rules specification
/// </summary>
public class RuleDependent : FmlNode
{

    /// <summary>
    /// Group invocations
    /// </summary>
    public List<GroupInvocation> Invocations { get; set; } = new();

    /// <summary>
    /// Nested rules (optional)
    /// </summary>
    public List<Rule> Rules { get; set; } = new();
}

/// <summary>
/// Group invocation in a dependent rule
/// </summary>
public class GroupInvocation : FmlNode
{

    /// <summary>
    /// Name of the group to invoke
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Parameters to pass to the group
    /// </summary>
    public List<InvocationParameter> Parameters { get; set; } = new();
}

/// <summary>
/// Parameter for a group invocation
/// </summary>
public class InvocationParameter
{
    /// <summary>
    /// Type of parameter
    /// </summary>
    public InvocationParameterType Type { get; set; }

    /// <summary>
    /// Value of the parameter (string, number, or boolean)
    /// </summary>
    public object? Value { get; set; }
}

/// <summary>
/// Types of invocation parameters
/// </summary>
public enum InvocationParameterType
{
    Literal,
    Identifier
}

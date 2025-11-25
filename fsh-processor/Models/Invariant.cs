namespace fsh_processor.Models;

/// <summary>
/// Invariant definition (Invariant: name)
/// </summary>
public class Invariant : FshEntity
{
    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// FHIRPath expression
    /// </summary>
    public string? Expression { get; set; }

    /// <summary>
    /// XPath expression
    /// </summary>
    public string? XPath { get; set; }

    /// <summary>
    /// Severity code
    /// </summary>
    public string? Severity { get; set; }

    /// <summary>
    /// Rules for additional properties
    /// </summary>
    public List<InvariantRule> Rules { get; set; } = new();
}

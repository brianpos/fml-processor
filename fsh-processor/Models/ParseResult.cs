namespace fsh_processor.Models;

/// <summary>
/// Parse result - either a FshDoc or errors
/// </summary>
public abstract record ParseResult
{
    /// <summary>
    /// Successful parse result with a FshDoc
    /// </summary>
    public sealed record Success(FshDoc Document) : ParseResult;

    /// <summary>
    /// Failed parse result with errors
    /// </summary>
    public sealed record Failure(List<ParseError> Errors) : ParseResult;
}

/// <summary>
/// Parse error information
/// </summary>
public class ParseError
{
    /// <summary>
    /// Severity of the error
    /// </summary>
    public ErrorSeverity Severity { get; set; }

    /// <summary>
    /// Error code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Location in source (e.g., "@5:10")
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Line number (1-based)
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// Column number (0-based)
    /// </summary>
    public int Column { get; set; }
}

/// <summary>
/// Error severity levels
/// </summary>
public enum ErrorSeverity
{
    Error,
    Warning,
    Information
}

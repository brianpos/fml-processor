namespace fsh_processor.Models;

/// <summary>
/// Base class for all SimpleFSH syntax tree nodes.
/// Provides position tracking for error reporting and IDE features.
/// </summary>
public class SubToken : FshNode
{
    /// <summary>
    /// The raw text of the sub-token
    /// </summary>
    /// <remarks>
    /// This is used for tokens that are part of larger constructs, such as
    /// quotes, brackets, or other non whitespace delimiters.
    /// </remarks>
    public required string Text { get; set; }
}

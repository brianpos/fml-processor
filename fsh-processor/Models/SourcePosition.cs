namespace fsh_processor.Models;

/// <summary>
/// Source position information for tracking element location in original text
/// </summary>
public record SourcePosition
{
    /// <summary>
    /// Starting line number (1-based)
    /// </summary>
    public int StartLine { get; init; }

    /// <summary>
    /// Starting column number (0-based)
    /// </summary>
    public int StartColumn { get; init; }

    /// <summary>
    /// Ending line number (1-based)
    /// </summary>
    public int EndLine { get; init; }

    /// <summary>
    /// Ending column number (0-based)
    /// </summary>
    public int EndColumn { get; init; }

    /// <summary>
    /// Starting character index in the source (0-based)
    /// </summary>
    public int StartIndex { get; init; }

    /// <summary>
    /// Ending character index in the source (0-based)
    /// </summary>
    public int EndIndex { get; init; }
}

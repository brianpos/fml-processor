namespace fml_processor.Models;

/// <summary>
/// Represents a hidden token from the ANTLR parser (comments, whitespace, etc.)
/// These tokens are sent to the HIDDEN channel during lexing and can be preserved
/// for exact round-trip serialization.
/// </summary>
public class HiddenToken
{
    /// <summary>
    /// The type of hidden token (from ANTLR lexer).
    /// Common types: WS (whitespace), COMMENT (block comment), LINE_COMMENT (line comment)
    /// </summary>
    public int TokenType { get; set; }
    
    /// <summary>
    /// The actual text of the token, including all whitespace and formatting.
    /// For comments, this includes the comment delimiters (// or /* */)
    /// </summary>
    public string Text { get; set; } = string.Empty;
    
    /// <summary>
    /// Convenience property to check if this is a comment token
    /// </summary>
    public bool IsComment => TokenType == FmlMappingLexer.COMMENT || 
                             TokenType == FmlMappingLexer.LINE_COMMENT;
    
    /// <summary>
    /// Convenience property to check if this is whitespace
    /// </summary>
    public bool IsWhitespace => TokenType == FmlMappingLexer.WS;
}

/// <summary>
/// Extension methods for working with collections of hidden tokens
/// </summary>
public static class HiddenTokenExtensions
{
    /// <summary>
    /// Gets all comment tokens from the collection
    /// </summary>
    public static IEnumerable<HiddenToken> GetComments(this List<HiddenToken>? tokens)
    {
        return tokens?.Where(t => t.IsComment) ?? Enumerable.Empty<HiddenToken>();
    }
    
    /// <summary>
    /// Gets all whitespace tokens from the collection
    /// </summary>
    public static IEnumerable<HiddenToken> GetWhitespace(this List<HiddenToken>? tokens)
    {
        return tokens?.Where(t => t.IsWhitespace) ?? Enumerable.Empty<HiddenToken>();
    }
    
    /// <summary>
    /// Gets the combined text of all tokens in order
    /// </summary>
    public static string GetCombinedText(this List<HiddenToken>? tokens)
    {
        if (tokens == null || tokens.Count == 0) return string.Empty;
        return string.Concat(tokens.Select(t => t.Text));
    }
    
    /// <summary>
    /// Checks if the collection contains any comment tokens
    /// </summary>
    public static bool HasComments(this List<HiddenToken>? tokens)
    {
        return tokens?.Any(t => t.IsComment) == true;
    }
    
    /// <summary>
    /// Checks if the collection contains any whitespace tokens
    /// </summary>
    public static bool HasWhitespace(this List<HiddenToken>? tokens)
    {
        return tokens?.Any(t => t.IsWhitespace) == true;
    }
}

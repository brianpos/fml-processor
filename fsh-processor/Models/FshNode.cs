using Hl7.Fhir.Utility;

namespace fsh_processor.Models;

/// <summary>
/// Base class for all FML syntax tree nodes.
/// Provides common infrastructure for position tracking and hidden token preservation.
/// </summary>
/// <remarks>
/// All FML model classes inherit from this base to provide:
/// - Source position tracking (for error reporting and IDE features)
/// - Hidden token preservation (for exact round-trip serialization of comments and formatting)
/// </remarks>
public abstract class FshNode: IAnnotated, IAnnotatable
{
    /// <summary>
    /// Source position information for this node in the original FML text.
    /// Used for error reporting, debugging, and IDE features like go-to-definition.
    /// </summary>
    public SourcePosition? Position { get; set; }

    /// <summary>
    /// Hidden tokens (comments, whitespace) that appear before this element.
    /// Only populated when preserving exact formatting from parsed source.
    /// Null means the serializer should use default formatting rules.
    /// </summary>
    /// <remarks>
    /// These tokens are captured from the ANTLR HIDDEN channel and include:
    /// - Line comments (// ...)
    /// - Block comments (/* ... */)
    /// - Whitespace and blank lines
    /// </remarks>
    public List<HiddenToken>? LeadingHiddenTokens { get; set; }
    
    /// <summary>
    /// Hidden tokens that appear after this element on the same line.
    /// Typically includes inline comments and trailing whitespace before newline.
    /// Null means the serializer should use default formatting rules.
    /// </summary>
    /// <remarks>
    /// Trailing tokens only include same-line content (up to but not including newline).
    /// Newlines and subsequent content become leading tokens for the next element.
    /// </remarks>
    public List<HiddenToken>? TrailingHiddenTokens { get; set; }

    #region << Annotations >>
    [NonSerialized]
    private AnnotationList _annotations;

    private AnnotationList annotations => LazyInitializer.EnsureInitialized(ref _annotations, () => new AnnotationList());

    public IEnumerable<object> Annotations(Type type)
    {
        return annotations.OfType(type);
    }

    public void AddAnnotation(object annotation)
    {
        annotations.AddAnnotation(annotation);
    }

    public void RemoveAnnotations(Type type)
    {
        annotations.RemoveAnnotations(type);
    }
    #endregion
}

/// <summary>
/// Extension methods for working with FML syntax tree nodes
/// </summary>
public static class FmlNodeExtensions
{
    /// <summary>
    /// Gets all comment tokens from both leading and trailing positions
    /// </summary>
    public static IEnumerable<HiddenToken> GetAllComments(this FshNode node)
    {
        return node.LeadingHiddenTokens.GetComments()
            .Concat(node.TrailingHiddenTokens.GetComments());
    }
    
    /// <summary>
    /// Checks if the node has any associated comments
    /// </summary>
    public static bool HasComments(this FshNode node)
    {
        return node.LeadingHiddenTokens.HasComments() || 
               node.TrailingHiddenTokens.HasComments();
    }
    
    /// <summary>
    /// Removes all hidden tokens (comments and whitespace) from this node.
    /// After calling this, the serializer will use default formatting.
    /// </summary>
    public static void ClearHiddenTokens(this FshNode node)
    {
        node.LeadingHiddenTokens = null;
        node.TrailingHiddenTokens = null;
    }
    
    /// <summary>
    /// Copies hidden tokens from another node to this node
    /// </summary>
    public static void CopyHiddenTokensFrom(this FshNode target, FshNode source)
    {
        target.LeadingHiddenTokens = source.LeadingHiddenTokens;
        target.TrailingHiddenTokens = source.TrailingHiddenTokens;
    }
    
    /// <summary>
    /// Checks if the node has associated position information
    /// </summary>
    public static bool HasPosition(this FshNode node)
    {
        return node.Position != null;
    }
    
    /// <summary>
    /// Gets the combined text of all leading hidden tokens
    /// </summary>
    public static string GetLeadingText(this FshNode node)
    {
        return node.LeadingHiddenTokens.GetCombinedText();
    }
    
    /// <summary>
    /// Gets the combined text of all trailing hidden tokens
    /// </summary>
    public static string GetTrailingText(this FshNode node)
    {
        return node.TrailingHiddenTokens.GetCombinedText();
    }
}

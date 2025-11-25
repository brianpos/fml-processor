using Antlr4.Runtime;
using fsh_processor.antlr;
using fsh_processor.Models;
using fsh_processor.Visitors;

namespace fsh_processor;

/// <summary>
/// FSH Parser - Parses FHIR Shorthand (FSH) text into a structured object model.
/// </summary>
public static class FshParser
{
    /// <summary>
    /// Parse FSH text and build a structured object model with position tracking.
    /// </summary>
    /// <param name="fshText">The FSH text to parse</param>
    /// <returns>
    /// A <see cref="ParseResult"/> which is either:
    /// - <see cref="ParseResult.Success"/> with a <see cref="FshDoc"/> on successful parsing
    /// - <see cref="ParseResult.Failure"/> with a list of <see cref="ParseError"/> on failure
    /// </returns>
    public static ParseResult Parse(string fshText)
    {
        if (string.IsNullOrEmpty(fshText))
        {
            return new ParseResult.Failure(new List<ParseError>
            {
                new ParseError
                {
                    Severity = ErrorSeverity.Error,
                    Code = "empty-input",
                    Message = "Input FSH text is null or empty",
                    Location = "@0:0",
                    Line = 0,
                    Column = 0
                }
            });
        }

        try
        {
            // Create ANTLR input stream
            var inputStream = new AntlrInputStream(fshText);
            
            // Create lexer
            var lexer = new FSHLexer(inputStream);
            
            // Create token stream
            var tokenStream = new CommonTokenStream(lexer);
            
            // Create parser
            var parser = new FSHParser(tokenStream);
            
            // Add custom error listener
            var errorListener = new FshParserErrorListener();
            parser.RemoveErrorListeners(); // Remove default console error listener
            parser.AddErrorListener(errorListener);
            
            // Parse the document
            var tree = parser.doc();
            
            // Check for parsing errors
            var errors = errorListener.GetErrors();
            if (errors.Count > 0)
            {
                return new ParseResult.Failure(errors);
            }
            
            // Build the object model using the visitor
            var visitor = new FshModelVisitor(tokenStream);
            var document = visitor.Visit(tree) as FshDoc;
            
            if (document == null)
            {
                return new ParseResult.Failure(new List<ParseError>
                {
                    new ParseError
                    {
                        Severity = ErrorSeverity.Error,
                        Code = "visitor-error",
                        Message = "Failed to build FSH document from parse tree",
                        Location = "@0:0",
                        Line = 0,
                        Column = 0
                    }
                });
            }
            
            return new ParseResult.Success(document);
        }
        catch (Exception ex)
        {
            return new ParseResult.Failure(new List<ParseError>
            {
                new ParseError
                {
                    Severity = ErrorSeverity.Error,
                    Code = "exception",
                    Message = ex.Message,
                    Location = "@0:0",
                    Line = 0,
                    Column = 0
                }
            });
        }
    }
    
    /// <summary>
    /// Parse FSH text and return the FshDoc or throw an exception on error.
    /// </summary>
    /// <param name="fshText">The FSH text to parse</param>
    /// <returns>The parsed <see cref="FshDoc"/></returns>
    /// <exception cref="FshParseException">Thrown when parsing fails</exception>
    public static FshDoc ParseOrThrow(string fshText)
    {
        var result = Parse(fshText);
        
        return result switch
        {
            ParseResult.Success success => success.Document,
            ParseResult.Failure failure => throw new FshParseException(
                "Failed to parse FSH text", 
                failure.Errors),
            _ => throw new InvalidOperationException("Unexpected parse result type")
        };
    }
}

/// <summary>
/// Custom error listener for FSH parsing that collects errors into a structured format.
/// </summary>
internal class FshParserErrorListener : BaseErrorListener
{
    private readonly List<ParseError> _errors = new();

    /// <summary>
    /// Gets the list of parse errors encountered.
    /// </summary>
    public List<ParseError> GetErrors() => _errors;

    /// <summary>
    /// Called when a syntax error is encountered during parsing.
    /// </summary>
    public override void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        var location = $"@{line}:{charPositionInLine}";
        
        _errors.Add(new ParseError
        {
            Severity = ErrorSeverity.Error,
            Code = "syntax",
            Message = msg,
            Location = location,
            Line = line,
            Column = charPositionInLine
        });
    }
}

/// <summary>
/// Exception thrown when FSH parsing fails.
/// </summary>
public class FshParseException : Exception
{
    /// <summary>
    /// Gets the list of parse errors that caused the exception.
    /// </summary>
    public List<ParseError> Errors { get; }

    /// <summary>
    /// Creates a new FSH parse exception.
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="errors">List of parse errors</param>
    public FshParseException(string message, List<ParseError> errors) 
        : base(message)
    {
        Errors = errors;
    }

    /// <summary>
    /// Gets a detailed error message including all parse errors.
    /// </summary>
    public override string ToString()
    {
        var errors = string.Join("\n  ", Errors.Select(e => $"{e.Location}: {e.Message}"));
        return $"{Message}\n  {errors}";
    }
}

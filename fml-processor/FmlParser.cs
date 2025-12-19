using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using fml_processor.Models;
using fml_processor.Visitors;
using Hl7.Fhir.Support;
using static fml_processor.Models.ParseResult;

namespace fml_processor;

/// <summary>
/// FML Parser - Parses FHIR Mapping Language (FML) text into a structured object model.
/// </summary>
/// <remarks>
/// This parser converts FML text into a <see cref="FmlStructureMap"/> object model with
/// full source position tracking for all elements. This enables IDE features like
/// syntax highlighting, error reporting, go-to-definition, and refactoring.
/// </remarks>
public static class FmlParser
{
    /// <summary>
    /// Parse FML text and build a structured object model with position tracking.
    /// </summary>
    /// <param name="fmlText">The FML text to parse</param>
    /// <returns>
    /// A <see cref="ParseResult"/> which is either:
    /// - <see cref="ParseResult.Success"/> with a <see cref="FmlStructureMap"/> on successful parsing
    /// - <see cref="ParseResult.Failure"/> with a list of <see cref="ParseError"/> on failure
    /// </returns>
    /// <example>
    /// <code>
    /// var result = FmlParser.Parse(fmlText);
    /// 
    /// switch (result)
    /// {
    ///     case ParseResult.Success success:
    ///         var map = success.StructureMap;
    ///         Console.WriteLine($"Parsed {map.Groups.Count} groups");
    ///         break;
    ///         
    ///     case ParseResult.Failure failure:
    ///         foreach (var error in failure.Errors)
    ///         {
    ///             Console.WriteLine($"Error at {error.Location}: {error.Message}");
    ///         }
    ///         break;
    /// }
    /// </code>
    /// </example>
    public static ParseResult Parse(string fmlText)
    {
        if (string.IsNullOrEmpty(fmlText))
        {
            return new ParseResult.Failure(new List<ParseError>
            {
                new ParseError
                {
                    Severity = ErrorSeverity.Error,
                    Code = "empty-input",
                    Message = "Input FML text is null or empty",
                    Location = "@0:0",
                    Line = 0,
                    Column = 0
                }
            });
        }

        try
        {
            // Create ANTLR input stream
            var inputStream = new AntlrInputStream(fmlText);
            
            // Create lexer
            var lexer = new FmlMappingLexer(inputStream);
            
            // Create token stream
            var tokenStream = new CommonTokenStream(lexer);
            
            // Create parser
            var parser = new FmlMappingParser(tokenStream);
            
            // Add custom error listener
            var errorListener = new FmlParserErrorListener();
            parser.RemoveErrorListeners(); // Remove default console error listener
            parser.AddErrorListener(errorListener);
            
            // Parse the structure map
            var tree = parser.structureMap();
            
            // Check for parsing errors
            var errors = errorListener.GetErrors();
            if (errors.Count > 0)
            {
                return new ParseResult.Failure(errors);
            }
            
            // Build the object model using the visitor
            var visitor = new FmlMappingModelVisitor(tokenStream);
            var structureMap = visitor.Visit(tree) as FmlStructureMap;
            
            if (structureMap == null)
            {
                return new ParseResult.Failure(new List<ParseError>
                {
                    new ParseError
                    {
                        Severity = ErrorSeverity.Error,
                        Code = "visitor-error",
                        Message = "Failed to build structure map from parse tree",
                        Location = "@0:0",
                        Line = 0,
                        Column = 0
                    }
                });
            }
            
            return new ParseResult.Success(structureMap);
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
    /// Parse FML text and return the StructureMap or throw an exception on error.
    /// </summary>
    /// <param name="fmlText">The FML text to parse</param>
    /// <returns>The parsed <see cref="FmlStructureMap"/></returns>
    /// <exception cref="FmlParseException">Thrown when parsing fails</exception>
    /// <example>
    /// <code>
    /// try
    /// {
    ///     var map = FmlParser.ParseOrThrow(fmlText);
    ///     Console.WriteLine($"Parsed {map.Groups.Count} groups");
    /// }
    /// catch (FmlParseException ex)
    /// {
    ///     Console.WriteLine($"Parse failed: {ex.Message}");
    ///     foreach (var error in ex.Errors)
    ///     {
    ///         Console.WriteLine($"  {error.Location}: {error.Message}");
    ///     }
    /// }
    /// </code>
    /// </example>
    public static FmlStructureMap ParseOrThrow(string fmlText)
    {
        var result = Parse(fmlText);
        
        return result switch
        {
            ParseResult.Success success => success.StructureMap,
            ParseResult.Failure failure => throw new FmlParseException(
                "Failed to parse FML text", 
                failure.Errors),
            _ => throw new InvalidOperationException("Unexpected parse result type")
        };
    }

    public static Rule ParseRule(string ruleText)
    {
        return Parse<Rule>(ruleText, (parser) => parser.mapRule());
    }

    public static T Parse<T>(string fshText, Func<FmlMappingParser, IParseTree> parseNode)
        where T : FmlNode
    {
        if (string.IsNullOrEmpty(fshText))
        {
            var issues = new List<ParseError>
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
            };
            throw new FmlParseException(
                "Failed to parse FML text",
                issues);
        }

        try
        {
            // Create ANTLR input stream
            var inputStream = new AntlrInputStream(fshText);

            // Create lexer
            var lexer = new FmlMappingLexer(inputStream);

            // Create token stream
            var tokenStream = new CommonTokenStream(lexer);

            // Create parser
            var parser = new FmlMappingParser(tokenStream);

            // Add custom error listener
            var errorListener = new FmlParserErrorListener();
            parser.RemoveErrorListeners(); // Remove default console error listener
            parser.AddErrorListener(errorListener);

            // Parse the document
            var tree = parseNode(parser);

            // Check for parsing errors
            var errors = errorListener.GetErrors();
            if (errors.Count > 0)
            {
                throw new FmlParseException(
                    "Failed to parse FML text",
                    errors);
            }

            // Build the object model using the visitor
            var visitor = new FmlMappingModelVisitor(tokenStream);
            var document = visitor.Visit(tree) as T;

            if (document == null)
            {
                var issues = new List<ParseError>
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
                };
                throw new FmlParseException(
                    "Failed to parse FML text",
                    issues);
            }

            return document;
        }
        catch (Exception ex)
        {
            var issues = new List<ParseError>
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
            };
            throw new FmlParseException(
                "Failed to parse FML text",
                issues);
        }
    }
}

/// <summary>
/// Custom error listener for FML parsing that collects errors into a structured format.
/// </summary>
internal class FmlParserErrorListener : BaseErrorListener
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
        
        // Log to console for debugging
        Console.WriteLine($"Parse Error: {location} {msg}");
        
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
/// Exception thrown when FML parsing fails.
/// </summary>
public class FmlParseException : Exception
{
    /// <summary>
    /// Gets the list of parse errors that caused the exception.
    /// </summary>
    public List<ParseError> Errors { get; }

    /// <summary>
    /// Creates a new FML parse exception.
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="errors">List of parse errors</param>
    public FmlParseException(string message, List<ParseError> errors) 
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

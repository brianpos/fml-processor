# FML Object Model - C# Port

This is a direct port of the TypeScript FML (FHIR Mapping Language) object model to C#. The model provides a complete, structured representation of FML documents with full source position tracking.

## Overview

This object model is based on the [FHIR Mapping Language specification](https://build.fhir.org/mapping-language.html) and mirrors the TypeScript implementation for consistency across platforms.

### Key Features

? **Complete FML Coverage** - Represents all FML language constructs  
? **Source Position Tracking** - Every element tracks its position in the original source  
? **Type-Safe** - Strongly-typed with appropriate enums and classes  
? **No External Dependencies** - Pure C# model classes  
? **Parse Result Types** - Discriminated unions for success/failure results  
? **Comprehensive Transform Types** - All 17 standard transform types documented  

## Core Classes

### FmlStructureMap
The root class representing an entire FML document:

```csharp
public class FmlStructureMap
{
    public SourcePosition? Position { get; set; }
    public List<MetadataDeclaration> Metadata { get; set; }
    public List<ConceptMapDeclaration> ConceptMaps { get; set; }
    public MapDeclaration? MapDeclaration { get; set; }
    public List<StructureDeclaration> Structures { get; set; }
    public List<ImportDeclaration> Imports { get; set; }
    public List<ConstantDeclaration> Constants { get; set; }
    public List<GroupDeclaration> Groups { get; set; }
}
```

### SourcePosition
Tracks the location of an element in the source text:

```csharp
public record SourcePosition(
    int StartLine,      // 1-based
    int StartColumn,    // 0-based
    int EndLine,        // 1-based
    int EndColumn,      // 0-based
    int StartIndex,     // 0-based character index
    int EndIndex        // 0-based character index
);
```

### Rule
Represents a transformation rule:

```csharp
public class Rule
{
    public SourcePosition? Position { get; set; }
    public string? Name { get; set; }
    public List<RuleSource> Sources { get; set; }    // Can have multiple sources
    public List<RuleTarget> Targets { get; set; }    // Can be empty
    public RuleDependent? Dependent { get; set; }    // Then clause
}
```

### RuleSource
Source element with context.element pattern:

```csharp
public class RuleSource
{
    public string Context { get; set; }      // e.g., "patient"
    public string? Element { get; set; }     // e.g., "name"
    public string? Type { get; set; }        // Type restriction
    public int? Min { get; set; }            // Cardinality
    public object? Max { get; set; }         // int or "*"
    public string? DefaultValue { get; set; }
    public SourceListMode? ListMode { get; set; }
    public string? Variable { get; set; }    // as variable
    public string? Condition { get; set; }   // where clause
    public string? Check { get; set; }       // check clause
    public string? Log { get; set; }         // log clause
}
```

### RuleTarget
Target element with transform:

```csharp
public class RuleTarget
{
    public string Context { get; set; }
    public string? Element { get; set; }
    public Transform? Transform { get; set; }
    public string? Variable { get; set; }
    public TargetListMode? ListMode { get; set; }
}
```

### Transform
Transform specification with type and parameters:

```csharp
public class Transform
{
    public SourcePosition? Position { get; set; }
    public string Type { get; set; }  // Any transform type
    public List<TransformParameter> Parameters { get; set; }
}

public class TransformParameter
{
    public TransformParameterType Type { get; set; }  // Literal, Identifier, Expression
    public object? Value { get; set; }
}
```

### Standard Transform Types

The `TransformType` class provides constants for all standard transforms:

```csharp
public static class TransformType
{
    public const string Create = "create";        // Create a new instance
    public const string Copy = "copy";            // Copy value as-is
    public const string Truncate = "truncate";    // Truncate string
    public const string Escape = "escape";        // Escape string
    public const string Cast = "cast";            // Cast to different type
    public const string Append = "append";        // Append strings
    public const string Translate = "translate";  // Translate using concept map
    public const string Reference = "reference";  // Create reference string
    public const string DateOp = "dateOp";        // Date operation
    public const string Uuid = "uuid";            // Generate UUID
    public const string Pointer = "pointer";      // Create pointer reference
    public const string Evaluate = "evaluate";    // Evaluate FHIRPath expression
    public const string Cc = "cc";                // Create CodeableConcept
    public const string C = "c";                  // Create Coding
    public const string Qty = "qty";              // Create Quantity
    public const string Id = "id";                // Create Identifier
    public const string Cp = "cp";                // Create ContactPoint
}
```

## Enums

### StructureMode
How structures are used in the mapping:
- `Source` - Input structure
- `Queried` - Queried during mapping
- `Target` - Output structure
- `Produced` - Produced by the mapping

### ParameterMode
Group parameter direction:
- `Source` - Input parameter
- `Target` - Output parameter

### GroupTypeMode
Type processing mode:
- `Types` - Process types only
- `TypePlus` - Process types and subtypes (type+)

### SourceListMode
How to process source lists:
- `First` - Use first item
- `Last` - Use last item
- `NotFirst` - Use all but first
- `NotLast` - Use all but last
- `OnlyOne` - Expect exactly one item

### TargetListMode
How to create target lists:
- `First` - Create/replace first item
- `Share` - Share across iterations
- `Last` - Create/replace last item
- `Single` - Create single item

### TransformParameterType
Type of transform parameter:
- `Literal` - Literal value
- `Identifier` - Variable/identifier reference
- `Expression` - FHIRPath expression

### InvocationParameterType
Type of group invocation parameter:
- `Literal` - Literal value
- `Identifier` - Variable/identifier reference

## Parse Results

The model includes discriminated union types for parse results:

```csharp
public abstract record ParseResult
{
    public sealed record Success(FmlStructureMap StructureMap) : ParseResult;
    public sealed record Failure(List<ParseError> Errors) : ParseResult;
}

public class ParseError
{
    public ErrorSeverity Severity { get; set; }  // Error, Warning, Information
    public string Code { get; set; }
    public string Message { get; set; }
    public string Location { get; set; }         // e.g., "@5:10"
    public int Line { get; set; }                // 1-based
    public int Column { get; set; }              // 0-based
}
```

## Usage Example

```csharp
using Antlr4.Runtime;
using fml_processor.Models;
using fml_processor.Visitors;

// Parse FML text
var input = new AntlrInputStream(fmlText);
var lexer = new FmlMappingLexer(input);
var tokens = new CommonTokenStream(lexer);
var parser = new FmlMappingParser(tokens);
var tree = parser.structureMap();

// Build object model with position tracking
var visitor = new FmlMappingModelVisitor();
var structureMap = (FmlStructureMap?)visitor.Visit(tree);

if (structureMap != null)
{
    // Access metadata
    foreach (var metadata in structureMap.Metadata)
    {
        Console.WriteLine($"{metadata.Path} = {metadata.Value}");
        if (metadata.Position != null)
        {
            Console.WriteLine($"  at line {metadata.Position.StartLine}");
        }
    }
    
    // Access transformation rules
    foreach (var group in structureMap.Groups)
    {
        Console.WriteLine($"Group: {group.Name}");
        
        foreach (var rule in group.Rules)
        {
            // Access sources with context.element pattern
            foreach (var source in rule.Sources)
            {
                var path = source.Element != null 
                    ? $"{source.Context}.{source.Element}" 
                    : source.Context;
                Console.WriteLine($"  Source: {path}");
                
                if (source.Variable != null)
                {
                    Console.WriteLine($"    as {source.Variable}");
                }
            }
            
            // Access targets with transforms
            foreach (var target in rule.Targets)
            {
                var path = target.Element != null 
                    ? $"{target.Context}.{target.Element}" 
                    : target.Context;
                Console.WriteLine($"  Target: {path}");
                
                if (target.Transform != null)
                {
                    Console.WriteLine($"    Transform: {target.Transform.Type}");
                    Console.WriteLine($"    Parameters: {target.Transform.Parameters.Count}");
                }
            }
        }
    }
}
```

## Differences from Original C# Model

This new model differs from the initial implementation in several key ways:

1. **Source Position Tracking**: Every element has optional position information
2. **Unified Rule Model**: Single `Rule` class instead of inheritance hierarchy
3. **Context/Element Separation**: Sources and targets split path into context and element
4. **Comprehensive Transforms**: All 17 standard transform types documented
5. **Structured Transform Parameters**: Transform parameters have type information
6. **Parse Result Types**: Discriminated unions for success/failure
7. **Better Cardinality**: Max can be int or "*" string

## Compatibility with TypeScript Model

This C# model is a direct port of the TypeScript model, ensuring:
- ? Same property names (camelCase in TS ? PascalCase in C#)
- ? Same structure and relationships
- ? Same enum values
- ? Same semantic meaning

This makes it easy to share FML documents and transformations between TypeScript and C# implementations.

## Future Enhancements

Potential additions:
- Serialization back to FML text format
- Model validation utilities
- Deep equality comparisons
- Cloning/copying utilities
- LINQ query helpers
- JSON serialization support

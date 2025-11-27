using Microsoft.VisualStudio.TestTools.UnitTesting;
using fsh_processor;
using fsh_processor.Models;
using System;
using System.IO;
using System.Linq;

namespace fsh_tester;

[TestClass]
public class DebugQuoteTests
{
    [TestMethod]
    public void TestDefinitionExtractExtension()
    {
        var fshPath = @"C:\git\hl7\sdc\input\fsh\extensions\DefinitionExtractExtension.fsh";
        var originalFsh = File.ReadAllText(fshPath);
        
        Console.WriteLine("=== ORIGINAL FSH ===");
        Console.WriteLine(originalFsh);
        Console.WriteLine();
        
        // Parse
        var parseResult = FshParser.Parse(originalFsh);
        Assert.IsInstanceOfType<ParseResult.Success>(parseResult, "Parse should succeed");
        var doc = ((ParseResult.Success)parseResult).Document;
        
        // Serialize
        var serialized = FshSerializer.Serialize(doc);
        
        // Save to file for analysis
        File.WriteAllText(@"C:\temp\serialized_definition_extract.fsh", serialized);
        Console.WriteLine("Serialized output saved to C:\\temp\\serialized_definition_extract.fsh");
        
        Console.WriteLine("=== SERIALIZED FSH ===");
        Console.WriteLine(serialized);
        Console.WriteLine();
        
        // Try to re-parse
        var reParseResult = FshParser.Parse(serialized);
        if (reParseResult is ParseResult.Failure failure)
        {
            Console.WriteLine("=== RE-PARSE ERROR ===");
            Console.WriteLine($"Error count: {failure.Errors.Count}");
            foreach (var error in failure.Errors)
            {
                Console.WriteLine($"Line {error.Line}:{error.Column} - {error.Message}");
            }
            Console.WriteLine();
            
            // Show the problematic area
            var lines = serialized.Split('\n');
            Console.WriteLine("=== SERIALIZED CONTENT (first 50 lines) ===");
            for (int i = 0; i < Math.Min(50, lines.Length); i++)
            {
                Console.WriteLine($"{i+1:D3}: {lines[i]}");
            }
        }
        
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult, "Re-parse failed");
    }

    [TestMethod]
    public void TestEntryMode()
    {
        var fshPath = @"C:\git\hl7\sdc\input\fsh\extensions\EntryMode.fsh";
        var originalFsh = File.ReadAllText(fshPath);
        
        Console.WriteLine("=== ORIGINAL FSH ===");
        Console.WriteLine(originalFsh);
        Console.WriteLine();
        
        // Parse
        var parseResult = FshParser.Parse(originalFsh);
        Assert.IsInstanceOfType<ParseResult.Success>(parseResult, "Parse should succeed");
        var doc = ((ParseResult.Success)parseResult).Document;
        
        // Serialize
        var serialized = FshSerializer.Serialize(doc);
        
        Console.WriteLine("=== SERIALIZED FSH ===");
        Console.WriteLine(serialized);
        Console.WriteLine();
        
        // Try to re-parse
        var reParseResult = FshParser.Parse(serialized);
        if (reParseResult is ParseResult.Failure failure)
        {
            Console.WriteLine("=== RE-PARSE ERROR ===");
            Console.WriteLine($"Error count: {failure.Errors.Count}");
            foreach (var error in failure.Errors)
            {
                Console.WriteLine($"Line {error.Line}:{error.Column} - {error.Message}");
            }
            Console.WriteLine();
            
            // Show the problematic area
            var lines = serialized.Split('\n');
            Console.WriteLine("=== SERIALIZED CONTENT (first 50 lines) ===");
            for (int i = 0; i < Math.Min(50, lines.Length); i++)
            {
                Console.WriteLine($"{i+1:D3}: {lines[i]}");
            }
        }
        
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult, "Re-parse failed");
    }
}

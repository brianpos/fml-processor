using fml_processor;
using fml_processor.Models;

// Example 1: Simple FML parsing with result handling
var simpleFml = """
    map "http://example.org/fhir/StructureMap/tutorial" = tutorial
    
    uses "http://hl7.org/fhir/StructureDefinition/Patient" as source
    uses "http://hl7.org/fhir/StructureDefinition/Bundle" as target
    
    group tutorial(source src : Patient, target bundle : Bundle) {
      src.name as vName -> bundle.entry as vEntry then {
        vName.given as vGiven -> vEntry.name = vGiven;
      };
    }
    """;

Console.WriteLine("=== Example 1: Parse FML with Result Pattern ===\n");

var result = FmlParser.Parse(simpleFml);

switch (result)
{
    case ParseResult.Success success:
        var map = success.StructureMap;
        Console.WriteLine($"✅ Successfully parsed FML");
        Console.WriteLine($"   Map URL: {map.MapDeclaration?.Url}");
        Console.WriteLine($"   Map ID: {map.MapDeclaration?.Identifier}");
        Console.WriteLine($"   Structures: {map.Structures.Count}");
        Console.WriteLine($"   Groups: {map.Groups.Count}");
        
        if (map.Groups.Count > 0)
        {
            var group = map.Groups[0];
            Console.WriteLine($"\n   Group '{group.Name}':");
            Console.WriteLine($"     Parameters: {group.Parameters.Count}");
            Console.WriteLine($"     Rules: {group.Rules.Count}");
            
            if (group.Rules.Count > 0)
            {
                var rule = group.Rules[0];
                Console.WriteLine($"\n     First rule:");
                Console.WriteLine($"       Sources: {rule.Sources.Count}");
                Console.WriteLine($"       Targets: {rule.Targets.Count}");
                
                if (rule.Sources.Count > 0)
                {
                    var source = rule.Sources[0];
                    var sourcePath = source.Element != null 
                        ? $"{source.Context}.{source.Element}" 
                        : source.Context;
                    Console.WriteLine($"       Source path: {sourcePath}");
                    if (source.Position != null)
                    {
                        Console.WriteLine($"       Source position: Line {source.Position.StartLine}, Column {source.Position.StartColumn}");
                    }
                }
            }
        }
        break;
        
    case ParseResult.Failure failure:
        Console.WriteLine($"❌ Parse failed with {failure.Errors.Count} error(s):");
        foreach (var error in failure.Errors)
        {
            Console.WriteLine($"   {error.Severity} at {error.Location}: {error.Message}");
        }
        break;
}

// Example 2: Parse with exception handling
Console.WriteLine("\n=== Example 2: Parse FML with Exception Pattern ===\n");

try
{
    var map2 = FmlParser.ParseOrThrow(simpleFml);
    Console.WriteLine($"✅ Successfully parsed using ParseOrThrow");
    Console.WriteLine($"   Groups: {map2.Groups.Count}");
}
catch (FmlParseException ex)
{
    Console.WriteLine($"❌ Parse failed: {ex.Message}");
    foreach (var error in ex.Errors)
    {
        Console.WriteLine($"   {error.Location}: {error.Message}");
    }
}

// Example 3: Parse invalid FML to see error handling
Console.WriteLine("\n=== Example 3: Parse Invalid FML ===\n");

var invalidFml = """
    map "http://example.org" = test
    
    group test(source src Patient) {
      // Missing colon after 'src'
      src.name -> bundle.name
    }
    """;

var invalidResult = FmlParser.Parse(invalidFml);

switch (invalidResult)
{
    case ParseResult.Success:
        Console.WriteLine("✅ Unexpectedly parsed invalid FML");
        break;
        
    case ParseResult.Failure failure:
        Console.WriteLine($"❌ Parse failed as expected with {failure.Errors.Count} error(s):");
        foreach (var error in failure.Errors)
        {
            Console.WriteLine($"   [{error.Severity}] {error.Location}: {error.Message}");
        }
        break;
}

Console.WriteLine("\n=== Done ===");

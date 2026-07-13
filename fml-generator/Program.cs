using fml_processor;
using fml_processor.Models;

Console.WriteLine("FHIR Mapping Language Generator - Generate and test FHIR maps");

// Handle --help / -h before spinning up the host so it works nicely as a dotnet tool.
if (args.Any(a => a is "--help" or "-h" or "-?" or "/?"))
{
    Console.WriteLine("""

        Usage:
          fmlgen [options] path

        Options:
          -sv <version>        Source FHIR version (default: R4)
          -tv <version>        Target FHIR version (default: R6)
          -ft <type>           Filter to only generate maps for the specified resource type(s) (can be specified multiple times)
          -m <filename.txt>    property renames between the versions
          -c <filename.fml>    custom maps to consider when generating maps
          -ti <filename.ini>   FHIR.ini file to compare content against
          -o <directory>       Output directory (default: ./output)
          -h, --help           Show this help text.
        """);
    return;
}

// Parse the command line arguments into a Settings object
var settings = new GeneratorSettings();

for (int i = 0; i < args.Length; i++)
{
    var arg = args[i];

    // Options that require a following value.
    if (arg is "-sv" or "-tv" or "-m" or "-c" or "-o" or "-ti" or "-ft")
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine($"Missing value for option '{arg}'.");
            return;
        }

        var value = args[++i];
        switch (arg)
        {
            case "-sv": settings.SourceVersion = value; break;
            case "-tv": settings.TargetVersion = value; break;
            case "-ft": settings.FilterTypes.Add(value); break;
            case "-m": settings.PropertyRenamesFile = value; break;
            case "-c": settings.CustomMapsFile = value; break;
            case "-ti": settings.TraceFhirIniFile = value; break;
            case "-o": settings.OutputDirectory = value; break;
        }
    }
    else if (arg.StartsWith('-'))
    {
        Console.Error.WriteLine($"Unknown option '{arg}'.");
        return;
    }
    else
    {
        // The single positional argument is the input path.
        settings.InputPath = arg;
    }
}

// if target isn't provided, just use the source version as the target version
if (settings.TargetVersion == null)
    settings.TargetVersion = settings.SourceVersion;

// Run the generation routine
var engine = new GenerateFmlEngine(settings.SourceVersion, settings.TargetVersion);
var maps = engine.GenerateCrossVersionMaps(true, settings.FilterTypes);

// check the ini file if provided
if (settings.TraceFhirIniFile != null)
{
    string section = $"{settings.SourceVersion.ToLower()}-{settings.TargetVersion.ToLower()}-changes";
    Console.WriteLine($"[{section}]");
    var existingMaps = engine.ReadSection(settings.TraceFhirIniFile, section);
    engine.TraceIniContentForMaps(maps, existingMaps);
}


// Run the validation of the generated content
Console.WriteLine("\n=== Validating maps ===");
await engine.ValidateFml(maps, settings.FilterTypes);

Console.WriteLine("\n=== Done ===");

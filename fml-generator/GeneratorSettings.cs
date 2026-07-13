
public class GeneratorSettings
{
    public string SourceVersion { get; set; } = "R4";
    public string TargetVersion { get; set; } = "R6";
    public string? PropertyRenamesFile { get; set; }
    public string? CustomMapsFile { get; set; }
    public string? TraceFhirIniFile { get; set; }
    public List<string> FilterTypes { get; } = new();
    public string OutputDirectory { get; set; } = "./output";
    public string? InputPath { get; set; }
}

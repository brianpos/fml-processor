
public class ValidationSettings
{
    public bool ProcessIndividually { get; set; }
    public string? InputPath { get; set; }
    public IEnumerable<string> FilterTypes { get; set; } = Array.Empty<string>();
}

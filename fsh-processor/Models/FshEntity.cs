namespace fsh_processor.Models;

/// <summary>
/// Base class for all FSH entities (alias, profile, extension, etc.)
/// </summary>
public abstract class FshEntity : FshNode
{
    /// <summary>
    /// Name of the entity
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

public class FshString : FshNode
{
    public FshString(string QuotedValue, bool IsTrippleQuoted = false)
    {
        _quotedValue = QuotedValue;
        _isTrippleQuoted = IsTrippleQuoted;
    }

    public string RawValue()
    {
        string? rawValue;
        if (_quotedValue.StartsWith("\"\"\"") && _quotedValue.EndsWith("\"\"\"") && _quotedValue.Length >= 6)
        {
            rawValue = _quotedValue[3..^3];
            // now process all the escape characters
        }
        else
        {
            rawValue = _quotedValue[1..^1];
            // now process all the escape characters
        }
        return rawValue;
    }

    private string _quotedValue;
    private bool _isTrippleQuoted;

    public string QuotedValue()
    {
        return _quotedValue;
    }
}
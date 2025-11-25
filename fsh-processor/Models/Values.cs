namespace fsh_processor.Models;

/// <summary>
/// Base class for all FSH values
/// </summary>
public abstract class FshValue : FshNode
{
}

/// <summary>
/// String value
/// </summary>
public class StringValue : FshValue
{
    /// <summary>
    /// The string value
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a multiline string
    /// </summary>
    public bool IsMultiline { get; set; }
}

/// <summary>
/// Number value
/// </summary>
public class NumberValue : FshValue
{
    /// <summary>
    /// The numeric value
    /// </summary>
    public decimal Value { get; set; }
}

/// <summary>
/// DateTime value
/// </summary>
public class DateTimeValue : FshValue
{
    /// <summary>
    /// The datetime value as a string (preserving format)
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Time value
/// </summary>
public class TimeValue : FshValue
{
    /// <summary>
    /// The time value as a string (preserving format)
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Boolean value
/// </summary>
public class BooleanValue : FshValue
{
    /// <summary>
    /// The boolean value
    /// </summary>
    public bool Value { get; set; }
}

/// <summary>
/// Code value (CODE display?)
/// </summary>
public class Code : FshValue
{
    /// <summary>
    /// The code
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Optional display text
    /// </summary>
    public string? Display { get; set; }
}

/// <summary>
/// Quantity value (number? unit display?)
/// </summary>
public class Quantity : FshValue
{
    /// <summary>
    /// Numeric value (optional)
    /// </summary>
    public decimal? Value { get; set; }

    /// <summary>
    /// Unit (UNIT or CODE)
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Optional display text
    /// </summary>
    public string? Display { get; set; }
}

/// <summary>
/// Ratio value (ratioPart : ratioPart)
/// </summary>
public class Ratio : FshValue
{
    /// <summary>
    /// Numerator
    /// </summary>
    public RatioPart Numerator { get; set; } = new();

    /// <summary>
    /// Denominator
    /// </summary>
    public RatioPart Denominator { get; set; } = new();
}

/// <summary>
/// Part of a ratio (either a number or a quantity)
/// </summary>
public class RatioPart : FshNode
{
    /// <summary>
    /// Simple numeric value (if not a quantity)
    /// </summary>
    public decimal? Value { get; set; }

    /// <summary>
    /// Quantity (if not a simple number)
    /// </summary>
    public Quantity? QuantityValue { get; set; }
}

/// <summary>
/// Reference value (Reference(Type) display?)
/// </summary>
public class Reference : FshValue
{
    /// <summary>
    /// Reference type(s)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Optional display text
    /// </summary>
    public string? Display { get; set; }
}

/// <summary>
/// Canonical URL reference
/// </summary>
public class Canonical : FshValue
{
    /// <summary>
    /// The canonical URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Optional version
    /// </summary>
    public string? Version { get; set; }
}

/// <summary>
/// CodeableReference value (CodeableReference(Type))
/// </summary>
public class CodeableReference : FshValue
{
    /// <summary>
    /// Reference type(s)
    /// </summary>
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Name/Identifier value (for paths, names, etc.)
/// </summary>
public class NameValue : FshValue
{
    /// <summary>
    /// The name/identifier
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Regex value
/// </summary>
public class RegexValue : FshValue
{
    /// <summary>
    /// The regex pattern
    /// </summary>
    public string Pattern { get; set; } = string.Empty;
}

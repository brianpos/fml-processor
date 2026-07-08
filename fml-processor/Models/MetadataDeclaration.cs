namespace fml_processor.Models;

/// <summary>
/// The kind of literal held in a <see cref="MetadataDeclaration.Value"/>.
/// Controls how the value is rendered back to FML: <see cref="String"/> values are
/// quoted, while all other kinds are emitted as bare literals (e.g. <c>true</c>, <c>42</c>).
/// </summary>
public enum MetadataValueKind
{
    /// <summary>A quoted string literal (e.g. <c>'active'</c>).</summary>
    String,

    /// <summary>A boolean literal (<c>true</c> or <c>false</c>).</summary>
    Boolean,

    /// <summary>An integer, decimal, long or quantity number literal (e.g. <c>42</c>, <c>3.14</c>).</summary>
    Number,

    /// <summary>A date literal (e.g. <c>@2024-01-01</c>).</summary>
    Date,

    /// <summary>A dateTime literal (e.g. <c>@2024-01-01T12:00:00Z</c>).</summary>
    DateTime,

    /// <summary>A time literal (e.g. <c>@T12:00:00</c>).</summary>
    Time
}

/// <summary>
/// Metadata declaration (e.g., /// url = 'http://...', /// name = 'MyMap')
/// </summary>
public class MetadataDeclaration : FmlNode
{
    /// <summary>
    /// The qualified identifier (e.g., 'url', 'name', 'jurisdiction.coding.system')
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// The value of the metadata property
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// For markdown values enclosed in triple quotes
    /// </summary>
    public bool IsMarkdown { get; set; }

    /// <summary>
    /// The kind of literal stored in <see cref="Value"/>. Defaults to <see cref="MetadataValueKind.String"/>
    /// so string values are quoted when serialized; boolean/number/date literals are emitted bare.
    /// </summary>
    public MetadataValueKind ValueKind { get; set; } = MetadataValueKind.String;
}

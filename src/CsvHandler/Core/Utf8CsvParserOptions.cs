namespace CsvHandler.Core;

/// <summary>
/// Configuration options for the UTF-8 CSV parser.
/// </summary>
public readonly struct Utf8CsvParserOptions
{
    /// <summary>
    /// The field delimiter byte. Default is comma (44, ',').
    /// </summary>
    public byte Delimiter { get; init; }

    /// <summary>
    /// The quote character byte. Default is double-quote (34, '"').
    /// </summary>
    public byte Quote { get; init; }

    /// <summary>
    /// The escape character byte. Default is double-quote (34, '"').
    /// In RFC 4180, quotes are escaped by doubling them.
    /// </summary>
    public byte Escape { get; init; }

    /// <summary>
    /// Indicates whether the first record contains header names.
    /// </summary>
    public bool HasHeaders { get; init; }

    /// <summary>
    /// Indicates whether to trim leading and trailing whitespace from unquoted fields.
    /// </summary>
    public bool TrimFields { get; init; }

    /// <summary>
    /// Indicates whether to allow comment lines starting with CommentPrefix.
    /// </summary>
    public bool AllowComments { get; init; }

    /// <summary>
    /// The comment prefix byte. Default is hash/pound (35, '#').
    /// Only used when AllowComments is true.
    /// </summary>
    public byte CommentPrefix { get; init; }

    /// <summary>
    /// The parsing mode to use for handling CSV data.
    /// </summary>
    public CsvParseMode Mode { get; init; }

    /// <summary>
    /// Creates a new instance with RFC 4180 defaults.
    /// </summary>
    public static Utf8CsvParserOptions Default => new()
    {
        Delimiter = (byte)',',
        Quote = (byte)'"',
        Escape = (byte)'"',
        HasHeaders = true,
        TrimFields = false,
        AllowComments = false,
        CommentPrefix = (byte)'#',
        Mode = CsvParseMode.Rfc4180
    };

    /// <summary>
    /// Creates a new instance with lenient parsing mode.
    /// </summary>
    public static Utf8CsvParserOptions Lenient => new()
    {
        Delimiter = (byte)',',
        Quote = (byte)'"',
        Escape = (byte)'"',
        HasHeaders = true,
        TrimFields = true,
        AllowComments = true,
        CommentPrefix = (byte)'#',
        Mode = CsvParseMode.Lenient
    };

    /// <summary>
    /// Creates a new instance for tab-separated values (TSV).
    /// </summary>
    public static Utf8CsvParserOptions Tsv => new()
    {
        Delimiter = (byte)'\t',
        Quote = (byte)'"',
        Escape = (byte)'"',
        HasHeaders = true,
        TrimFields = false,
        AllowComments = false,
        CommentPrefix = (byte)'#',
        Mode = CsvParseMode.Rfc4180
    };
}

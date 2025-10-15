using System;
using System.Globalization;
using System.Text;

namespace CsvHandler.Core;

/// <summary>
/// Configuration options for CSV writing operations.
/// </summary>
public sealed class CsvWriterOptions
{
    /// <summary>
    /// Gets or sets the field delimiter character. Default is comma (',').
    /// </summary>
    public char Delimiter { get; set; } = ',';

    /// <summary>
    /// Gets or sets the quote character. Default is double-quote ('"').
    /// </summary>
    public char Quote { get; set; } = '"';

    /// <summary>
    /// Gets or sets the escape character. Default is double-quote ('"').
    /// In RFC 4180, quotes are escaped by doubling them.
    /// </summary>
    public char Escape { get; set; } = '"';

    /// <summary>
    /// Gets or sets whether to write a header row with field names.
    /// </summary>
    public bool WriteHeaders { get; set; } = true;

    /// <summary>
    /// Gets or sets the quote mode determining when fields should be quoted.
    /// </summary>
    public CsvQuoteMode QuoteMode { get; set; } = CsvQuoteMode.Minimal;

    /// <summary>
    /// Gets or sets the culture to use for formatting values.
    /// Default is InvariantCulture for consistent cross-platform behavior.
    /// </summary>
    public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// Gets or sets the newline sequence. Default is "\r\n" (CRLF) per RFC 4180.
    /// </summary>
    public string NewLine { get; set; } = "\r\n";

    /// <summary>
    /// Gets or sets the buffer size in bytes for writing operations.
    /// Default is 65536 (64 KB) for optimal performance.
    /// </summary>
    public int BufferSize { get; set; } = 65536;

    /// <summary>
    /// Gets or sets whether to flush automatically after each record.
    /// Enabling this can impact performance but ensures data is written immediately.
    /// </summary>
    public bool AutoFlush { get; set; }

    /// <summary>
    /// Gets or sets the encoding to use when writing text.
    /// Default is UTF-8 without BOM.
    /// </summary>
    public Encoding Encoding { get; set; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Creates a new instance with RFC 4180 defaults.
    /// </summary>
    public static CsvWriterOptions Default => new();

    /// <summary>
    /// Creates a new instance configured for tab-separated values (TSV).
    /// </summary>
    public static CsvWriterOptions Tsv => new()
    {
        Delimiter = '\t',
        Quote = '"',
        Escape = '"',
        WriteHeaders = true,
        QuoteMode = CsvQuoteMode.Minimal,
        NewLine = "\r\n"
    };

    /// <summary>
    /// Creates a new instance configured for semicolon-delimited values (common in European locales).
    /// </summary>
    public static CsvWriterOptions Semicolon => new()
    {
        Delimiter = ';',
        Quote = '"',
        Escape = '"',
        WriteHeaders = true,
        QuoteMode = CsvQuoteMode.Minimal,
        NewLine = "\r\n"
    };

    /// <summary>
    /// Validates the options and throws if any are invalid.
    /// </summary>
    internal void Validate()
    {
        if (Delimiter == Quote)
        {
            throw new ArgumentException("Delimiter and Quote characters must be different.");
        }

        if (BufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BufferSize), "BufferSize must be positive.");
        }

        if (string.IsNullOrEmpty(NewLine))
        {
            throw new ArgumentException("NewLine cannot be null or empty.", nameof(NewLine));
        }

        if (Culture == null)
        {
            throw new ArgumentNullException(nameof(Culture));
        }

        if (Encoding == null)
        {
            throw new ArgumentNullException(nameof(Encoding));
        }
    }
}

/// <summary>
/// Specifies when fields should be enclosed in quotes during CSV writing.
/// </summary>
public enum CsvQuoteMode
{
    /// <summary>
    /// Never quote fields. Use only when data is known to not contain special characters.
    /// </summary>
    None = 0,

    /// <summary>
    /// Quote only fields that contain delimiters, quotes, or newlines (RFC 4180 compliant).
    /// This is the recommended mode for most scenarios.
    /// </summary>
    Minimal = 1,

    /// <summary>
    /// Quote all fields regardless of content.
    /// </summary>
    All = 2,

    /// <summary>
    /// Quote all non-numeric fields. Numeric fields (int, double, etc.) are not quoted.
    /// </summary>
    NonNumeric = 3
}

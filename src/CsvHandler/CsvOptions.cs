using System;
using System.Globalization;

namespace CsvHandler;

/// <summary>
/// Configuration options for CSV reading.
/// </summary>
public sealed class CsvOptions
{
    /// <summary>
    /// Gets the default CSV options (comma-delimited, double-quote, has headers).
    /// </summary>
    public static CsvOptions Default { get; } = new CsvOptions();

    /// <summary>
    /// Gets or sets the field delimiter character. Default is comma (,).
    /// </summary>
    public char Delimiter { get; set; } = ',';

    /// <summary>
    /// Gets or sets the quote character used to enclose fields. Default is double-quote (").
    /// </summary>
    public char Quote { get; set; } = '"';

    /// <summary>
    /// Gets or sets the escape character used within quoted fields. Default is double-quote (").
    /// </summary>
    public char Escape { get; set; } = '"';

    /// <summary>
    /// Gets or sets whether the CSV file has a header row. Default is true.
    /// </summary>
    public bool HasHeaders { get; set; } = true;

    /// <summary>
    /// Gets or sets the trimming behavior for field values. Default is None.
    /// </summary>
    public CsvTrimOptions TrimOptions { get; set; } = CsvTrimOptions.None;

    /// <summary>
    /// Gets or sets the parsing mode for handling malformed CSV. Default is Strict.
    /// </summary>
    public CsvParseMode ParseMode { get; set; } = CsvParseMode.Strict;

    /// <summary>
    /// Gets or sets the culture used for parsing values. Default is InvariantCulture.
    /// </summary>
    public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// Gets or sets how errors are handled during reading. Default is Throw.
    /// </summary>
    public CsvErrorHandling ErrorHandling { get; set; } = CsvErrorHandling.Throw;

    /// <summary>
    /// Gets or sets an optional callback invoked when an error occurs.
    /// Return true to continue processing, false to stop.
    /// </summary>
    public Func<CsvError, bool>? OnError { get; set; }

    /// <summary>
    /// Gets or sets the buffer size for reading. Default is 4096 bytes.
    /// </summary>
    public int BufferSize { get; set; } = 4096;

    /// <summary>
    /// Gets or sets whether to allow jagged rows (rows with different field counts). Default is false.
    /// </summary>
    public bool AllowJaggedRows { get; set; }

    /// <summary>
    /// Gets or sets whether to skip empty lines. Default is true.
    /// </summary>
    public bool SkipEmptyLines { get; set; } = true;

    /// <summary>
    /// Gets or sets the comment character. Lines starting with this character are ignored. Default is null (no comments).
    /// </summary>
    public char? CommentCharacter { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of errors to collect when ErrorHandling is Collect. Default is 100.
    /// </summary>
    public int MaxErrorCount { get; set; } = 100;

    /// <summary>
    /// Validates the options and throws if invalid.
    /// </summary>
    public void Validate()
    {
        if (Delimiter == Quote)
            throw new ArgumentException("Delimiter and Quote characters cannot be the same.");

        if (Delimiter == Escape)
            throw new ArgumentException("Delimiter and Escape characters cannot be the same.");

        if (BufferSize <= 0)
            throw new ArgumentException("BufferSize must be greater than zero.", nameof(BufferSize));

        if (MaxErrorCount <= 0)
            throw new ArgumentException("MaxErrorCount must be greater than zero.", nameof(MaxErrorCount));

        if (Culture == null)
            throw new ArgumentNullException(nameof(Culture));
    }

    /// <summary>
    /// Creates a shallow copy of the current options.
    /// </summary>
    public CsvOptions Clone()
    {
        return (CsvOptions)MemberwiseClone();
    }
}

/// <summary>
/// Specifies how field values should be trimmed.
/// </summary>
[Flags]
public enum CsvTrimOptions
{
    /// <summary>
    /// Do not trim whitespace.
    /// </summary>
    None = 0,

    /// <summary>
    /// Trim leading whitespace from unquoted fields.
    /// </summary>
    UnquotedOnly = 1,

    /// <summary>
    /// Trim leading and trailing whitespace from all fields.
    /// </summary>
    All = 2,

    /// <summary>
    /// Trim whitespace inside quotes as well.
    /// </summary>
    InsideQuotes = 4
}

/// <summary>
/// Specifies the parsing mode for handling malformed CSV.
/// </summary>
public enum CsvParseMode
{
    /// <summary>
    /// Strict RFC 4180 compliance. Throw on any malformed data.
    /// </summary>
    Strict,

    /// <summary>
    /// Lenient mode. Attempt to recover from common formatting issues.
    /// </summary>
    Lenient,

    /// <summary>
    /// Tolerant mode. Accept most variations and attempt best-effort parsing.
    /// </summary>
    Tolerant
}

/// <summary>
/// Specifies how errors should be handled during CSV reading.
/// </summary>
public enum CsvErrorHandling
{
    /// <summary>
    /// Throw an exception immediately when an error occurs.
    /// </summary>
    Throw,

    /// <summary>
    /// Skip the problematic row and continue processing. Errors are logged.
    /// </summary>
    Skip,

    /// <summary>
    /// Collect errors in the Errors property and continue processing up to MaxErrorCount.
    /// </summary>
    Collect
}

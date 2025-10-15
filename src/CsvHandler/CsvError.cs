using System;

namespace CsvHandler;

/// <summary>
/// Represents an error encountered during CSV reading.
/// </summary>
public sealed class CsvError
{
    /// <summary>
    /// Gets the line number where the error occurred (1-based).
    /// </summary>
    public long LineNumber { get; }

    /// <summary>
    /// Gets the character position within the line where the error occurred (0-based).
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// Gets the error message describing what went wrong.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the type of error that occurred.
    /// </summary>
    public CsvErrorType ErrorType { get; }

    /// <summary>
    /// Gets the raw line content where the error occurred, if available.
    /// </summary>
    public string? RawLine { get; }

    /// <summary>
    /// Gets the field name associated with the error, if applicable.
    /// </summary>
    public string? FieldName { get; }

    /// <summary>
    /// Gets the inner exception that caused this error, if any.
    /// </summary>
    public Exception? InnerException { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvError"/> class.
    /// </summary>
    public CsvError(
        long lineNumber,
        int position,
        string message,
        CsvErrorType errorType,
        string? rawLine = null,
        string? fieldName = null,
        Exception? innerException = null)
    {
        LineNumber = lineNumber;
        Position = position;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        ErrorType = errorType;
        RawLine = rawLine;
        FieldName = fieldName;
        InnerException = innerException;
    }

    /// <summary>
    /// Returns a string representation of this error.
    /// </summary>
    public override string ToString()
    {
        var fieldInfo = FieldName != null ? $" (field: {FieldName})" : string.Empty;
        return $"CSV Error at line {LineNumber}, position {Position}{fieldInfo}: {Message}";
    }
}

/// <summary>
/// Specifies the type of CSV reading error.
/// </summary>
public enum CsvErrorType
{
    /// <summary>
    /// A field is malformed (e.g., unclosed quote, invalid escaping).
    /// </summary>
    MalformedField,

    /// <summary>
    /// A value could not be converted to the target type.
    /// </summary>
    TypeConversion,

    /// <summary>
    /// A required field is missing from the CSV data.
    /// </summary>
    MissingField,

    /// <summary>
    /// The header row is invalid or missing.
    /// </summary>
    InvalidHeader,

    /// <summary>
    /// The row has too many or too few fields.
    /// </summary>
    FieldCountMismatch,

    /// <summary>
    /// An unexpected end of file was encountered.
    /// </summary>
    UnexpectedEndOfFile,

    /// <summary>
    /// A general parsing error occurred.
    /// </summary>
    ParsingError
}

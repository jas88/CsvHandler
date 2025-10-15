using System;

namespace CsvHandler;

/// <summary>
/// Exception thrown when CSV parsing or deserialization fails.
/// </summary>
public class CsvException : Exception
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
    /// Gets the type of CSV error.
    /// </summary>
    public CsvErrorType ErrorType { get; }

    /// <summary>
    /// Gets the field name associated with the error, if applicable.
    /// </summary>
    public string? FieldName { get; }

    /// <summary>
    /// Gets the raw line content where the error occurred, if available.
    /// </summary>
    public string? RawLine { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvException"/> class.
    /// </summary>
    public CsvException(
        string message,
        long lineNumber,
        int position,
        CsvErrorType errorType,
        string? fieldName = null,
        string? rawLine = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        LineNumber = lineNumber;
        Position = position;
        ErrorType = errorType;
        FieldName = fieldName;
        RawLine = rawLine;
    }

    /// <summary>
    /// Converts this exception to a <see cref="CsvError"/> instance.
    /// </summary>
    public CsvError ToCsvError()
    {
        return new CsvError(
            LineNumber,
            Position,
            Message,
            ErrorType,
            RawLine,
            FieldName,
            InnerException);
    }

    /// <summary>
    /// Returns a string representation of this exception.
    /// </summary>
    public override string ToString()
    {
        var fieldInfo = FieldName != null ? $" (field: {FieldName})" : string.Empty;
        var baseMessage = $"CSV Error at line {LineNumber}, position {Position}{fieldInfo}: {Message}";

        if (InnerException != null)
            return $"{baseMessage}\n{InnerException}";

        return baseMessage;
    }
}

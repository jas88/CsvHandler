using System;

namespace CsvHandler;

/// <summary>
/// Represents an error that occurred while reading CSV data.
/// </summary>
public class CsvReadError
{
    /// <summary>
    /// Gets the error message describing what went wrong.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the line number where the error occurred (1-based).
    /// </summary>
    public int LineNumber { get; }

    /// <summary>
    /// Gets the column number where the error occurred (1-based).
    /// </summary>
    public int ColumnNumber { get; }

    /// <summary>
    /// Gets the raw CSV data that caused the error, if available.
    /// </summary>
    public string? RawData { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvReadError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="lineNumber">The line number where the error occurred.</param>
    /// <param name="columnNumber">The column number where the error occurred.</param>
    /// <param name="rawData">The raw CSV data that caused the error.</param>
    public CsvReadError(string message, int lineNumber, int columnNumber, string? rawData = null)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        LineNumber = lineNumber;
        ColumnNumber = columnNumber;
        RawData = rawData;
    }

    /// <summary>
    /// Returns a string representation of the error.
    /// </summary>
    public override string ToString()
    {
        return $"CSV Read Error at line {LineNumber}, column {ColumnNumber}: {Message}";
    }
}

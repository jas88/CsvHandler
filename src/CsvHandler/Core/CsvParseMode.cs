namespace CsvHandler.Core;

/// <summary>
/// Specifies the parsing mode for CSV field processing.
/// </summary>
public enum CsvParseMode
{
    /// <summary>
    /// Strict RFC 4180 compliance. Requires proper quoting and escaping.
    /// Fails on malformed input.
    /// </summary>
    Rfc4180,

    /// <summary>
    /// Lenient parsing mode. Makes best-effort attempt to parse malformed CSV.
    /// Tolerates unescaped quotes and other format violations.
    /// </summary>
    Lenient,

    /// <summary>
    /// Ignores quote characters entirely. Treats all characters as literals.
    /// Useful for simple delimited data without quoting.
    /// </summary>
    IgnoreQuotes
}

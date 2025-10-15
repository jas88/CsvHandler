using System;

namespace CsvHandler.Attributes;

/// <summary>
/// Marks a class or struct for CSV serialization and deserialization.
/// The type must be declared as partial to allow source generation.
/// </summary>
/// <remarks>
/// This attribute enables automatic generation of CSV serialization code for the target type.
/// The source generator will create methods to read from and write to CSV format.
/// </remarks>
/// <example>
/// <code>
/// [CsvRecord(HasHeaders = true, Delimiter = ',')]
/// public partial class Person
/// {
///     [CsvField(Order = 0)]
///     public string Name { get; set; }
///
///     [CsvField(Order = 1)]
///     public int Age { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
#if CSVHANDLER_GENERATOR
internal
#else
public
#endif
sealed class CsvRecordAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CsvRecordAttribute"/> class.
    /// </summary>
    public CsvRecordAttribute()
    {
        Delimiter = ',';
        Quote = '"';
        HasHeaders = true;
    }

    /// <summary>
    /// Gets or sets the delimiter character used to separate fields in the CSV.
    /// </summary>
    /// <remarks>
    /// Common delimiters include:
    /// <list type="bullet">
    /// <item><description>Comma (,) - Default and most common</description></item>
    /// <item><description>Tab (\t) - For TSV files</description></item>
    /// <item><description>Semicolon (;) - Common in European locales</description></item>
    /// <item><description>Pipe (|) - For data with many commas</description></item>
    /// </list>
    /// </remarks>
    /// <value>The delimiter character. Default is comma (,).</value>
    public char Delimiter { get; set; }

    /// <summary>
    /// Gets or sets the quote character used to enclose fields containing special characters.
    /// </summary>
    /// <remarks>
    /// Fields containing the delimiter, newlines, or the quote character itself
    /// will be enclosed in quotes. Quote characters within fields are escaped by doubling.
    /// </remarks>
    /// <value>The quote character. Default is double quote (").</value>
    public char Quote { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the CSV file has a header row.
    /// </summary>
    /// <remarks>
    /// When true, the first row is treated as column headers during deserialization
    /// and headers are written during serialization. Field names can be customized
    /// using the <see cref="CsvFieldAttribute.Name"/> property.
    /// </remarks>
    /// <value>
    /// <c>true</c> if the CSV has headers; otherwise, <c>false</c>. Default is <c>true</c>.
    /// </value>
    public bool HasHeaders { get; set; }

    /// <summary>
    /// Gets or sets the escape character used for special characters in fields.
    /// </summary>
    /// <remarks>
    /// If not set, quote doubling is used as the escape mechanism.
    /// Setting this enables backslash-style escaping (e.g., \" for quotes).
    /// </remarks>
    /// <value>The escape character, or null to use quote doubling.</value>
    public char? Escape { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to trim whitespace from field values.
    /// </summary>
    /// <remarks>
    /// When true, leading and trailing whitespace is removed from field values
    /// during deserialization. This does not affect values within quoted fields.
    /// </remarks>
    /// <value>
    /// <c>true</c> to trim whitespace; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    public bool TrimWhitespace { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to skip empty lines during deserialization.
    /// </summary>
    /// <remarks>
    /// When true, completely empty lines are ignored during CSV parsing.
    /// Lines containing only whitespace are considered empty if <see cref="TrimWhitespace"/> is true.
    /// </remarks>
    /// <value>
    /// <c>true</c> to skip empty lines; otherwise, <c>false</c>. Default is <c>true</c>.
    /// </value>
    public bool SkipEmptyLines { get; set; } = true;
}

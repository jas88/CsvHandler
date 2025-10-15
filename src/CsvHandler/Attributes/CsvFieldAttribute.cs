using System;

namespace CsvHandler.Attributes;

/// <summary>
/// Marks a property or field for CSV serialization and specifies its mapping configuration.
/// </summary>
/// <remarks>
/// This attribute controls how a property or field is mapped to and from CSV columns.
/// Properties and fields without this attribute are included by default unless marked with <see cref="CsvIgnoreAttribute"/>.
/// </remarks>
/// <example>
/// <code>
/// [CsvRecord]
/// public partial class Product
/// {
///     // Custom column name and order
///     [CsvField(Name = "Product ID", Order = 0)]
///     public int Id { get; set; }
///
///     // Specific column index (0-based)
///     [CsvField(Index = 1)]
///     public string Name { get; set; }
///
///     // Custom format for DateTime
///     [CsvField(Order = 2, Format = "yyyy-MM-dd")]
///     public DateTime ReleaseDate { get; set; }
///
///     // Custom format for decimal
///     [CsvField(Order = 3, Format = "F2")]
///     public decimal Price { get; set; }
///
///     // Custom converter
///     [CsvField(Order = 4, Converter = typeof(CustomStatusConverter))]
///     public Status CurrentStatus { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class CsvFieldAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CsvFieldAttribute"/> class.
    /// </summary>
    public CsvFieldAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvFieldAttribute"/> class with the specified column name.
    /// </summary>
    /// <param name="name">The name of the CSV column.</param>
    public CsvFieldAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Gets or sets the name of the CSV column.
    /// </summary>
    /// <remarks>
    /// If not specified, the property or field name is used as the column name.
    /// This is used for header matching when <see cref="CsvRecordAttribute.HasHeaders"/> is true.
    /// </remarks>
    /// <value>The CSV column name, or null to use the member name.</value>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the zero-based index of the CSV column.
    /// </summary>
    /// <remarks>
    /// This specifies the exact column position in the CSV file.
    /// If both <see cref="Index"/> and <see cref="Order"/> are specified, <see cref="Index"/> takes precedence.
    /// If neither is specified, fields are ordered by declaration order.
    /// </remarks>
    /// <value>The zero-based column index, or null to use <see cref="Order"/> or declaration order.</value>
    public int? Index { get; set; }

    /// <summary>
    /// Gets or sets the relative order of this field in the CSV output.
    /// </summary>
    /// <remarks>
    /// Fields are ordered by this value during serialization.
    /// Lower values appear first. Fields with the same order value are ordered by declaration order.
    /// This is ignored if <see cref="Index"/> is specified.
    /// </remarks>
    /// <value>The relative order value, or null to use declaration order.</value>
    public int? Order { get; set; }

    /// <summary>
    /// Gets or sets the format string for formatting the field value.
    /// </summary>
    /// <remarks>
    /// This is used when converting values to strings during serialization.
    /// The format string is passed to the type's ToString method or string.Format.
    /// Common examples:
    /// <list type="bullet">
    /// <item><description>DateTime: "yyyy-MM-dd", "MM/dd/yyyy HH:mm:ss"</description></item>
    /// <item><description>Decimal/Double: "F2" (2 decimal places), "N0" (no decimals with thousands separator)</description></item>
    /// <item><description>Integer: "D5" (5 digits with leading zeros), "X" (hexadecimal)</description></item>
    /// </list>
    /// </remarks>
    /// <value>The format string, or null to use default formatting.</value>
    public string? Format { get; set; }

    /// <summary>
    /// Gets or sets the type of a custom converter for this field.
    /// </summary>
    /// <remarks>
    /// The converter type must implement the appropriate converter interface and have a parameterless constructor.
    /// Custom converters override default serialization and deserialization logic.
    /// </remarks>
    /// <value>The converter type, or null to use default conversion.</value>
    /// <seealso cref="CsvConverterAttribute"/>
    public Type? Converter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this field should be ignored during CSV operations.
    /// </summary>
    /// <remarks>
    /// This is equivalent to applying the <see cref="CsvIgnoreAttribute"/> attribute.
    /// Ignored fields are not read from or written to CSV files.
    /// </remarks>
    /// <value>
    /// <c>true</c> to ignore this field; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    public bool Ignore { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this field is required during deserialization.
    /// </summary>
    /// <remarks>
    /// When true, deserialization fails if the field is missing or empty.
    /// This validation is performed before type conversion.
    /// </remarks>
    /// <value>
    /// <c>true</c> if the field is required; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the default value to use when the field is empty or missing during deserialization.
    /// </summary>
    /// <remarks>
    /// This value is used only if the field is not <see cref="Required"/>.
    /// The value must be compatible with the field's type or will cause a conversion error.
    /// </remarks>
    /// <value>The default value, or null if no default is specified.</value>
    public object? DefaultValue { get; set; }
}

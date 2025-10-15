using System;

namespace CsvHandler.Attributes;

/// <summary>
/// Specifies a custom converter type for CSV serialization and deserialization.
/// </summary>
/// <remarks>
/// <para>
/// This attribute can be applied to types or individual properties/fields to specify
/// a custom converter that handles serialization and deserialization logic.
/// </para>
/// <para>
/// The converter type must implement the appropriate converter interface (to be defined)
/// and must have a public parameterless constructor.
/// </para>
/// <para>
/// When applied to a type, all instances of that type use the specified converter
/// unless overridden at the property/field level.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Custom converter implementation
/// public class CustomDateTimeConverter : ICsvConverter&lt;DateTime&gt;
/// {
///     public string Serialize(DateTime value)
///     {
///         return value.ToString("O"); // ISO 8601 format
///     }
///
///     public DateTime Deserialize(string value)
///     {
///         return DateTime.Parse(value, null, DateTimeStyles.RoundtripKind);
///     }
/// }
///
/// // Apply to a type
/// [CsvRecord]
/// [CsvConverter(typeof(CustomDateTimeConverter))]
/// public partial class Event
/// {
///     [CsvField(Order = 0)]
///     public string Name { get; set; }
///
///     [CsvField(Order = 1)]
///     public DateTime Timestamp { get; set; } // Uses CustomDateTimeConverter
/// }
///
/// // Apply to a specific property
/// [CsvRecord]
/// public partial class Schedule
/// {
///     [CsvField(Order = 0)]
///     public string Title { get; set; }
///
///     [CsvField(Order = 1)]
///     [CsvConverter(typeof(CustomDateTimeConverter))]
///     public DateTime StartTime { get; set; }
///
///     [CsvField(Order = 2)]
///     public DateTime EndTime { get; set; } // Uses default DateTime converter
/// }
///
/// // Custom enum converter
/// public class StatusConverter : ICsvConverter&lt;Status&gt;
/// {
///     public string Serialize(Status value)
///     {
///         return value switch
///         {
///             Status.Active => "A",
///             Status.Inactive => "I",
///             Status.Pending => "P",
///             _ => throw new ArgumentException($"Unknown status: {value}")
///         };
///     }
///
///     public Status Deserialize(string value)
///     {
///         return value switch
///         {
///             "A" or "Active" => Status.Active,
///             "I" or "Inactive" => Status.Inactive,
///             "P" or "Pending" => Status.Pending,
///             _ => throw new ArgumentException($"Invalid status code: {value}")
///         };
///     }
/// }
///
/// [CsvRecord]
/// public partial class User
/// {
///     [CsvField(Order = 0)]
///     public string Name { get; set; }
///
///     [CsvField(Order = 1, Converter = typeof(StatusConverter))]
///     public Status CurrentStatus { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field,
    AllowMultiple = false, Inherited = true)]
#if CSVHANDLER_GENERATOR
internal
#else
public
#endif
sealed class CsvConverterAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CsvConverterAttribute"/> class.
    /// </summary>
    /// <param name="converterType">The type of the converter to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="converterType"/> is null.</exception>
    public CsvConverterAttribute(Type converterType)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(converterType);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(converterType);
#endif

        ConverterType = converterType;
    }

    /// <summary>
    /// Gets the type of the converter.
    /// </summary>
    /// <value>
    /// The converter type, which must implement the appropriate converter interface
    /// and have a public parameterless constructor.
    /// </value>
    public Type ConverterType { get; }
}

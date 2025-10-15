using System;

namespace CsvHandler.Attributes;

/// <summary>
/// Registers a CSV-serializable type with a CSV context class for ahead-of-time (AOT) compilation.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is applied to context classes that manage CSV serialization metadata.
/// It triggers source generation for the specified types, enabling fast, AOT-friendly serialization.
/// </para>
/// <para>
/// Context classes should be partial and can aggregate multiple CSV-serializable types.
/// The source generator creates optimized serialization code for all registered types.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Define CSV record types
/// [CsvRecord]
/// public partial class Person
/// {
///     [CsvField(Order = 0)]
///     public string Name { get; set; }
///
///     [CsvField(Order = 1)]
///     public int Age { get; set; }
/// }
///
/// [CsvRecord]
/// public partial class Product
/// {
///     [CsvField(Order = 0)]
///     public int Id { get; set; }
///
///     [CsvField(Order = 1)]
///     public string Name { get; set; }
/// }
///
/// // Register types in a context
/// [CsvSerializable(typeof(Person))]
/// [CsvSerializable(typeof(Product))]
/// public partial class MyCsvContext
/// {
///     // The source generator will add serialization methods here
/// }
///
/// // Usage
/// var context = new MyCsvContext();
/// var people = context.DeserializePerson(csvContent);
/// var csv = context.SerializePerson(people);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class CsvSerializableAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CsvSerializableAttribute"/> class.
    /// </summary>
    /// <param name="type">The type to register for CSV serialization.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="type"/> is not marked with <see cref="CsvRecordAttribute"/>.
    /// </exception>
    public CsvSerializableAttribute(Type type)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(type);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(type);
#endif

        Type = type;
    }

    /// <summary>
    /// Gets the type registered for CSV serialization.
    /// </summary>
    /// <value>The registered type that must be marked with <see cref="CsvRecordAttribute"/>.</value>
    public Type Type { get; }

    /// <summary>
    /// Gets or sets the name used to identify this serializable type in generated code.
    /// </summary>
    /// <remarks>
    /// If not specified, the type name is used. This is useful when the context manages
    /// multiple types with the same name from different namespaces.
    /// Generated methods will use this name (e.g., Serialize{TypeName}, Deserialize{TypeName}).
    /// </remarks>
    /// <value>The type name for generated code, or null to use the actual type name.</value>
    public string? TypeName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to generate async methods for this type.
    /// </summary>
    /// <remarks>
    /// When true, generates async methods like SerializeAsync and DeserializeAsync
    /// that work with Stream and StreamReader/StreamWriter.
    /// </remarks>
    /// <value>
    /// <c>true</c> to generate async methods; otherwise, <c>false</c>. Default is <c>true</c>.
    /// </value>
    public bool GenerateAsync { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to generate synchronous methods for this type.
    /// </summary>
    /// <remarks>
    /// When true, generates synchronous methods like Serialize and Deserialize
    /// that work with string and TextReader/TextWriter.
    /// </remarks>
    /// <value>
    /// <c>true</c> to generate synchronous methods; otherwise, <c>false</c>. Default is <c>true</c>.
    /// </value>
    public bool GenerateSync { get; set; } = true;
}

// Copyright (c) 2024 CsvHandler
// Licensed under the MIT License

using System;
using System.Buffers;

namespace CsvHandler;

/// <summary>
/// Interface for custom CSV field converters.
/// </summary>
/// <typeparam name="T">The type to convert to/from CSV format.</typeparam>
/// <remarks>
/// <para>
/// Implement this interface to provide custom serialization logic for specific types.
/// Converters can be specified via the [CsvConverter] attribute on properties/fields.
/// </para>
/// <para>
/// Example:
/// </para>
/// <code>
/// public class CustomDateConverter : ICsvConverter&lt;DateTime&gt;
/// {
///     public DateTime Read(ReadOnlySpan&lt;byte&gt; data)
///     {
///         var str = Encoding.UTF8.GetString(data);
///         return DateTime.ParseExact(str, "yyyyMMdd", CultureInfo.InvariantCulture);
///     }
///
///     public void Write(IBufferWriter&lt;byte&gt; writer, DateTime value)
///     {
///         var str = value.ToString("yyyyMMdd");
///         writer.Write(Encoding.UTF8.GetBytes(str));
///     }
/// }
///
/// public class MyClass
/// {
///     [CsvConverter(typeof(CustomDateConverter))]
///     public DateTime Date { get; set; }
/// }
/// </code>
/// </remarks>
public interface ICsvConverter<T>
{
    /// <summary>
    /// Reads and parses a value from CSV field data.
    /// </summary>
    /// <param name="data">The UTF-8 encoded CSV field data.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="FormatException">
    /// Thrown when the data cannot be parsed into the target type.
    /// </exception>
    T Read(ReadOnlySpan<byte> data);

    /// <summary>
    /// Writes a value to CSV field format.
    /// </summary>
    /// <param name="writer">The buffer writer to write to.</param>
    /// <param name="value">The value to serialize.</param>
    void Write(IBufferWriter<byte> writer, T value);
}

/// <summary>
/// Base class for CSV converters with common utility methods.
/// </summary>
/// <typeparam name="T">The type to convert.</typeparam>
public abstract class CsvConverter<T> : ICsvConverter<T>
{
    /// <inheritdoc/>
    public abstract T Read(ReadOnlySpan<byte> data);

    /// <inheritdoc/>
    public abstract void Write(IBufferWriter<byte> writer, T value);

    /// <summary>
    /// Helper method to get a string from UTF-8 data.
    /// </summary>
    protected static string GetString(ReadOnlySpan<byte> data)
    {
        return System.Text.Encoding.UTF8.GetString(data);
    }

    /// <summary>
    /// Helper method to write a string as UTF-8 data.
    /// </summary>
    protected static void WriteString(IBufferWriter<byte> writer, string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        writer.Write(bytes);
    }
}

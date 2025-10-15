// Copyright (c) 2024 CsvHandler
// Licensed under the MIT License
//
// This file demonstrates what a source-generated CsvContext looks like.
// It is NOT part of the runtime library - it's an example of what the
// source generator will emit.

#if FALSE // This is example code only

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using CsvHandler;
using CsvHandler.Attributes;

namespace ExampleApp;

// Example record types that would be defined by the user
[CsvRecord]
public partial class Person
{
    [CsvField(Order = 0, Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [CsvField(Order = 1, Name = "Age")]
    public int Age { get; set; }

    [CsvField(Order = 2, Name = "Email")]
    public string? Email { get; set; }

    [CsvField(Order = 3, Name = "Salary")]
    public decimal Salary { get; set; }
}

[CsvRecord]
public partial class Order
{
    [CsvField(Order = 0, Name = "OrderId")]
    public int OrderId { get; set; }

    [CsvField(Order = 1, Name = "CustomerId")]
    public string CustomerId { get; set; } = string.Empty;

    [CsvField(Order = 2, Name = "Amount")]
    public decimal Amount { get; set; }

    [CsvField(Order = 3, Name = "OrderDate")]
    public DateTime OrderDate { get; set; }
}

// User declares context with [CsvSerializable] attributes
[CsvSerializable(typeof(Person))]
[CsvSerializable(typeof(Order))]
public partial class AppCsvContext : CsvContext
{
}

// ============================================================================
// BELOW IS WHAT THE SOURCE GENERATOR WOULD EMIT
// ============================================================================

/// <summary>
/// Source-generated implementation of AppCsvContext.
/// </summary>
public partial class AppCsvContext : CsvContext
{
    private static AppCsvContext? s_default;

    /// <summary>
    /// Gets the default singleton instance of this context.
    /// </summary>
    public static new AppCsvContext Default => s_default ??= new AppCsvContext();

    // Type info instances (created lazily for efficiency)
    private CsvTypeInfo<Person>? _personTypeInfo;
    private CsvTypeInfo<Order>? _orderTypeInfo;

    /// <inheritdoc/>
    public override CsvTypeInfo<T>? GetTypeInfo<T>()
    {
        // Efficient type switching without reflection
        if (typeof(T) == typeof(Person))
            return (CsvTypeInfo<T>)(object)PersonTypeInfo;

        if (typeof(T) == typeof(Order))
            return (CsvTypeInfo<T>)(object)OrderTypeInfo;

        return null;
    }

    /// <inheritdoc/>
    public override CsvTypeInfo? GetTypeInfo(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        if (type == typeof(Person))
            return PersonTypeInfo;

        if (type == typeof(Order))
            return OrderTypeInfo;

        return null;
    }

    // ========================================================================
    // Person Type Info
    // ========================================================================

    private CsvTypeInfo<Person> PersonTypeInfo
    {
        get
        {
            if (_personTypeInfo != null)
                return _personTypeInfo;

            var typeInfo = new CsvTypeInfo<Person>
            {
                Options = Options,
                Reader = ReadPerson,
                Writer = WritePerson,
                AsyncReader = ReadPersonAsync,
                AsyncWriter = WritePersonAsync
            };

            // Add field metadata
            typeInfo.FieldsList.Add(new CsvFieldInfo
            {
                Name = nameof(Person.Name),
                CsvName = "Name",
                Order = 0,
                Index = 0,
                FieldType = typeof(string),
                IsRequired = true,
                Reader = data => Encoding.UTF8.GetString(data),
                Writer = (writer, value) =>
                {
                    var bytes = Encoding.UTF8.GetBytes((string)value!);
                    writer.Write(bytes);
                }
            });

            typeInfo.FieldsList.Add(new CsvFieldInfo
            {
                Name = nameof(Person.Age),
                CsvName = "Age",
                Order = 1,
                Index = 1,
                FieldType = typeof(int),
                IsRequired = true,
                Reader = data =>
                {
                    var str = Encoding.UTF8.GetString(data);
                    return int.Parse(str);
                },
                Writer = (writer, value) =>
                {
                    var bytes = Encoding.UTF8.GetBytes(value!.ToString()!);
                    writer.Write(bytes);
                }
            });

            typeInfo.FieldsList.Add(new CsvFieldInfo
            {
                Name = nameof(Person.Email),
                CsvName = "Email",
                Order = 2,
                Index = 2,
                FieldType = typeof(string),
                IsRequired = false,
                Reader = data => data.Length == 0 ? null : Encoding.UTF8.GetString(data),
                Writer = (writer, value) =>
                {
                    if (value != null)
                    {
                        var bytes = Encoding.UTF8.GetBytes((string)value);
                        writer.Write(bytes);
                    }
                }
            });

            typeInfo.FieldsList.Add(new CsvFieldInfo
            {
                Name = nameof(Person.Salary),
                CsvName = "Salary",
                Order = 3,
                Index = 3,
                FieldType = typeof(decimal),
                IsRequired = true,
                Reader = data =>
                {
                    var str = Encoding.UTF8.GetString(data);
                    return decimal.Parse(str);
                },
                Writer = (writer, value) =>
                {
                    var bytes = Encoding.UTF8.GetBytes(value!.ToString()!);
                    writer.Write(bytes);
                }
            });

            typeInfo.Validate();
            return _personTypeInfo = typeInfo;
        }
    }

    // Generated reader for Person
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Person ReadPerson(ReadOnlySpan<byte> data)
    {
        // This would use the Utf8CsvParser from the Modern parsers
        // For this example, simplified implementation
        var person = new Person();

        int fieldIndex = 0;
        int start = 0;

        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == (byte)',')
            {
                var fieldData = data.Slice(start, i - start);
                SetPersonField(person, fieldIndex, fieldData);
                fieldIndex++;
                start = i + 1;
            }
        }

        // Handle last field
        if (start < data.Length)
        {
            var fieldData = data.Slice(start);
            SetPersonField(person, fieldIndex, fieldData);
        }

        return person;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetPersonField(Person person, int fieldIndex, ReadOnlySpan<byte> data)
    {
        switch (fieldIndex)
        {
            case 0: // Name
                person.Name = Encoding.UTF8.GetString(data);
                break;
            case 1: // Age
                person.Age = int.Parse(Encoding.UTF8.GetString(data));
                break;
            case 2: // Email
                person.Email = data.Length == 0 ? null : Encoding.UTF8.GetString(data);
                break;
            case 3: // Salary
                person.Salary = decimal.Parse(Encoding.UTF8.GetString(data));
                break;
        }
    }

    // Generated writer for Person
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WritePerson(IBufferWriter<byte> writer, Person person)
    {
        // Write Name
        WriteField(writer, person.Name);
        writer.Write(new byte[] { (byte)',' });

        // Write Age
        WriteField(writer, person.Age.ToString());
        writer.Write(new byte[] { (byte)',' });

        // Write Email
        WriteField(writer, person.Email ?? string.Empty);
        writer.Write(new byte[] { (byte)',' });

        // Write Salary
        WriteField(writer, person.Salary.ToString());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteField(IBufferWriter<byte> writer, string value)
    {
        // Handle escaping if needed
        bool needsQuoting = value.Contains(',') || value.Contains('"') || value.Contains('\n');

        if (needsQuoting)
        {
            writer.Write(new byte[] { (byte)'"' });
            // Write escaped value
            var escaped = value.Replace("\"", "\"\"");
            writer.Write(Encoding.UTF8.GetBytes(escaped));
            writer.Write(new byte[] { (byte)'"' });
        }
        else
        {
            writer.Write(Encoding.UTF8.GetBytes(value));
        }
    }

    // Generated async reader for Person
    private static async IAsyncEnumerable<Person> ReadPersonAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        // Skip header if present
        var header = await reader.ReadLineAsync();

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var data = Encoding.UTF8.GetBytes(line);
            yield return ReadPerson(data);
        }
    }

    // Generated async writer for Person
    private static async ValueTask WritePersonAsync(Stream stream, Person person)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        WritePerson(buffer, person);
        buffer.Write(new byte[] { (byte)'\n' });

        await stream.WriteAsync(buffer.WrittenMemory);
    }

    // ========================================================================
    // Order Type Info (similar pattern as Person)
    // ========================================================================

    private CsvTypeInfo<Order> OrderTypeInfo
    {
        get
        {
            if (_orderTypeInfo != null)
                return _orderTypeInfo;

            var typeInfo = new CsvTypeInfo<Order>
            {
                Options = Options,
                Reader = ReadOrder,
                Writer = WriteOrder,
                AsyncReader = ReadOrderAsync,
                AsyncWriter = WriteOrderAsync
            };

            // Add field metadata for Order
            typeInfo.FieldsList.Add(new CsvFieldInfo
            {
                Name = nameof(Order.OrderId),
                CsvName = "OrderId",
                Order = 0,
                Index = 0,
                FieldType = typeof(int),
                IsRequired = true
            });

            typeInfo.FieldsList.Add(new CsvFieldInfo
            {
                Name = nameof(Order.CustomerId),
                CsvName = "CustomerId",
                Order = 1,
                Index = 1,
                FieldType = typeof(string),
                IsRequired = true
            });

            typeInfo.FieldsList.Add(new CsvFieldInfo
            {
                Name = nameof(Order.Amount),
                CsvName = "Amount",
                Order = 2,
                Index = 2,
                FieldType = typeof(decimal),
                IsRequired = true
            });

            typeInfo.FieldsList.Add(new CsvFieldInfo
            {
                Name = nameof(Order.OrderDate),
                CsvName = "OrderDate",
                Order = 3,
                Index = 3,
                FieldType = typeof(DateTime),
                IsRequired = true
            });

            typeInfo.Validate();
            return _orderTypeInfo = typeInfo;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Order ReadOrder(ReadOnlySpan<byte> data)
    {
        var order = new Order();
        // Similar pattern to ReadPerson
        return order;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteOrder(IBufferWriter<byte> writer, Order order)
    {
        // Similar pattern to WritePerson
    }

    private static async IAsyncEnumerable<Order> ReadOrderAsync(Stream stream)
    {
        // Similar pattern to ReadPersonAsync
        yield break;
    }

    private static async ValueTask WriteOrderAsync(Stream stream, Order order)
    {
        // Similar pattern to WritePersonAsync
    }
}

#endif

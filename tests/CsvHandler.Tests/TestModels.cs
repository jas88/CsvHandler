using System;
using CsvHandler.Attributes;

namespace CsvHandler.Tests;

/// <summary>
/// Test model classes for CSV serialization tests.
/// </summary>

[CsvRecord]
public partial class Person
{
    [CsvField(Order = 0)]
    public string Name { get; set; } = string.Empty;

    [CsvField(Order = 1)]
    public int Age { get; set; }

    [CsvField(Order = 2)]
    public string? City { get; set; }
}

[CsvRecord(HasHeaders = true, Delimiter = ',')]
public partial class Employee
{
    [CsvField(Name = "FullName", Order = 0)]
    public string Name { get; set; } = string.Empty;

    [CsvField(Order = 1)]
    public string Department { get; set; } = string.Empty;

    [CsvField(Order = 2)]
    public decimal Salary { get; set; }

    [CsvField(Order = 3, Format = "yyyy-MM-dd")]
    public DateTime HireDate { get; set; }

    [CsvField(Order = 4)]
    public bool IsActive { get; set; }
}

[CsvRecord]
public partial class Product
{
    [CsvField(Order = 0)]
    public int Id { get; set; }

    [CsvField(Order = 1)]
    public string Name { get; set; } = string.Empty;

    [CsvField(Order = 2)]
    public decimal Price { get; set; }

    [CsvField(Order = 3)]
    public int? Stock { get; set; }

    [CsvIgnore]
    public string InternalNotes { get; set; } = string.Empty;
}

[CsvRecord(Delimiter = '\t')]
public partial class TsvRecord
{
    [CsvField(Order = 0)]
    public string Field1 { get; set; } = string.Empty;

    [CsvField(Order = 1)]
    public string Field2 { get; set; } = string.Empty;

    [CsvField(Order = 2)]
    public string Field3 { get; set; } = string.Empty;
}

[CsvRecord]
public partial class NullableFieldsRecord
{
    [CsvField(Order = 0)]
    public string? Name { get; set; }

    [CsvField(Order = 1)]
    public int? Age { get; set; }

    [CsvField(Order = 2)]
    public DateTime? BirthDate { get; set; }

    [CsvField(Order = 3)]
    public decimal? Salary { get; set; }

    [CsvField(Order = 4)]
    public bool? IsActive { get; set; }
}

[CsvRecord]
public partial class DataTypesRecord
{
    [CsvField(Order = 0)]
    public byte ByteValue { get; set; }

    [CsvField(Order = 1)]
    public short ShortValue { get; set; }

    [CsvField(Order = 2)]
    public int IntValue { get; set; }

    [CsvField(Order = 3)]
    public long LongValue { get; set; }

    [CsvField(Order = 4)]
    public float FloatValue { get; set; }

    [CsvField(Order = 5)]
    public double DoubleValue { get; set; }

    [CsvField(Order = 6)]
    public decimal DecimalValue { get; set; }

    [CsvField(Order = 7)]
    public bool BoolValue { get; set; }

    [CsvField(Order = 8)]
    public char CharValue { get; set; }

    [CsvField(Order = 9)]
    public Guid GuidValue { get; set; }

    [CsvField(Order = 10, Format = "yyyy-MM-dd HH:mm:ss")]
    public DateTime DateTimeValue { get; set; }

    [CsvField(Order = 11)]
    public DateTimeOffset DateTimeOffsetValue { get; set; }
}

[CsvRecord]
public partial class RequiredFieldsRecord
{
    [CsvField(Order = 0, Required = true)]
    public string RequiredName { get; set; } = string.Empty;

    [CsvField(Order = 1)]
    public string? OptionalField { get; set; }

    [CsvField(Order = 2, DefaultValue = 0)]
    public int ValueWithDefault { get; set; }
}

/// <summary>
/// CSV context for testing source generation.
/// </summary>
[CsvSerializable(typeof(Person))]
[CsvSerializable(typeof(Employee))]
[CsvSerializable(typeof(Product))]
[CsvSerializable(typeof(TsvRecord))]
[CsvSerializable(typeof(NullableFieldsRecord))]
[CsvSerializable(typeof(DataTypesRecord))]
[CsvSerializable(typeof(RequiredFieldsRecord))]
public partial class TestCsvContext
{
    // Source generator will add serialization methods
}

using CsvHandler;
using CsvHandler.Attributes;

namespace CsvHandler.Tests.GeneratorTests;

/// <summary>
/// Test model for basic CSV generation.
/// </summary>
[CsvRecord]
public partial class TestPerson
{
    [CsvField(Order = 0)]
    public string Name { get; set; } = string.Empty;

    [CsvField(Order = 1)]
    public int Age { get; set; }

    [CsvField(Order = 2)]
    public string? Email { get; set; }
}

/// <summary>
/// Test model with various data types.
/// </summary>
[CsvRecord(Delimiter = '\t')]
public partial class ComplexRecord
{
    [CsvField(Order = 0)]
    public int Id { get; set; }

    [CsvField(Order = 1)]
    public string Name { get; set; } = string.Empty;

    [CsvField(Order = 2)]
    public decimal Price { get; set; }

    [CsvField(Order = 3)]
    public DateTime CreatedAt { get; set; }

    [CsvField(Order = 4)]
    public bool IsActive { get; set; }

    [CsvField(Order = 5)]
    public int? OptionalCount { get; set; }
}

/// <summary>
/// Test model with custom configuration.
/// </summary>
[CsvRecord(HasHeaders = false, TrimWhitespace = false)]
public partial class ConfiguredRecord
{
    [CsvField(Order = 0, Name = "first_name")]
    public string FirstName { get; set; } = string.Empty;

    [CsvField(Order = 1, Name = "last_name")]
    public string LastName { get; set; } = string.Empty;

    [CsvField(Order = 2, Name = "birth_date")]
    public DateTime BirthDate { get; set; }
}

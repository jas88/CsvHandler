using System;
using System.Buffers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CsvHandler.Tests.GeneratorTests;

/// <summary>
/// Tests for source generated CSV handlers.
/// </summary>
public class SourceGeneratorTests
{
    [Fact]
    public void TestPerson_ReadFromCsv_ParsesBasicRecord()
    {
        // Arrange
        var csvLine = "John Doe,30,john@example.com"u8;

        // Act
        var person = TestPerson.ReadFromCsv(csvLine);

        // Assert
        Assert.Equal("John Doe", person.Name);
        Assert.Equal(30, person.Age);
        Assert.Equal("john@example.com", person.Email);
    }

    [Fact]
    public void TestPerson_ReadFromCsv_HandlesNullableFields()
    {
        // Arrange
        var csvLine = "Jane Smith,25,"u8;

        // Act
        var person = TestPerson.ReadFromCsv(csvLine);

        // Assert
        Assert.Equal("Jane Smith", person.Name);
        Assert.Equal(25, person.Age);
        Assert.Null(person.Email);
    }

    [Fact]
    public void TestPerson_WriteToCsv_GeneratesCorrectOutput()
    {
        // Arrange
        var person = new TestPerson
        {
            Name = "Alice Johnson",
            Age = 35,
            Email = "alice@example.com"
        };
        var writer = new ArrayBufferWriter<byte>();

        // Act
        TestPerson.WriteToCsv(writer, person);

        // Assert
        var output = Encoding.UTF8.GetString(writer.WrittenSpan);
        Assert.Equal("Alice Johnson,35,alice@example.com", output);
    }

    [Fact]
    public void TestPerson_WriteToCsv_HandlesNullableFields()
    {
        // Arrange
        var person = new TestPerson
        {
            Name = "Bob Brown",
            Age = 40,
            Email = null
        };
        var writer = new ArrayBufferWriter<byte>();

        // Act
        TestPerson.WriteToCsv(writer, person);

        // Assert
        var output = Encoding.UTF8.GetString(writer.WrittenSpan);
        Assert.Equal("Bob Brown,40,", output);
    }

    [Fact]
    public async Task TestPerson_ReadAllFromCsvAsync_ParsesMultipleRecords()
    {
        // Arrange
        var csv = @"Name,Age,Email
John Doe,30,john@example.com
Jane Smith,25,jane@example.com";
        var stream = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var people = await TestPerson.ReadAllFromCsvAsync(stream).ToListAsync();

        // Assert
        Assert.Equal(2, people.Count);
        Assert.Equal("John Doe", people[0].Name);
        Assert.Equal(30, people[0].Age);
        Assert.Equal("Jane Smith", people[1].Name);
        Assert.Equal(25, people[1].Age);
    }

    [Fact]
    public async Task TestPerson_WriteToCsvAsync_WritesToStream()
    {
        // Arrange
        var person = new TestPerson
        {
            Name = "Charlie Davis",
            Age = 28,
            Email = "charlie@example.com"
        };
        var stream = new System.IO.MemoryStream();

        // Act
        await TestPerson.WriteToCsvAsync(stream, person);

        // Assert
        stream.Position = 0;
        var output = await new System.IO.StreamReader(stream).ReadToEndAsync();
        Assert.Equal("Charlie Davis,28,charlie@example.com\n", output);
    }

    [Fact]
    public void ComplexRecord_ReadFromCsv_ParsesMultipleTypes()
    {
        // Arrange
        var csvLine = "123\tProduct A\t99.99\t2025-01-15T10:30:00\ttrue\t42"u8;

        // Act
        var record = ComplexRecord.ReadFromCsv(csvLine);

        // Assert
        Assert.Equal(123, record.Id);
        Assert.Equal("Product A", record.Name);
        Assert.Equal(99.99m, record.Price);
        Assert.Equal(new DateTime(2025, 1, 15, 10, 30, 0), record.CreatedAt);
        Assert.True(record.IsActive);
        Assert.Equal(42, record.OptionalCount);
    }

    [Fact]
    public void ComplexRecord_WriteToCsv_FormatsCorrectly()
    {
        // Arrange
        var record = new ComplexRecord
        {
            Id = 456,
            Name = "Product B",
            Price = 149.50m,
            CreatedAt = new DateTime(2025, 10, 15, 14, 0, 0),
            IsActive = false,
            OptionalCount = null
        };
        var writer = new ArrayBufferWriter<byte>();

        // Act
        ComplexRecord.WriteToCsv(writer, record);

        // Assert
        var output = Encoding.UTF8.GetString(writer.WrittenSpan);
        Assert.Contains("456", output);
        Assert.Contains("Product B", output);
        Assert.Contains("149.5", output);
    }

    [Fact]
    public void ConfiguredRecord_UsesCustomFieldNames()
    {
        // Arrange
        var csvLine = "John,Doe,1990-05-15"u8;

        // Act
        var record = ConfiguredRecord.ReadFromCsv(csvLine);

        // Assert
        Assert.Equal("John", record.FirstName);
        Assert.Equal("Doe", record.LastName);
    }
}

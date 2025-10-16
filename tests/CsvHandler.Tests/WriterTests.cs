using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHandler.Core;
using FluentAssertions;
using Xunit;

#pragma warning disable CS1998 // Async method lacks 'await' operators

namespace CsvHandler.Tests;

/// <summary>
/// Comprehensive tests for CsvWriter functionality including quote handling,
/// escaping, custom delimiters, and async operations.
/// </summary>
public class WriterTests
{
    /// <summary>
    /// Helper extension to convert IEnumerable to IAsyncEnumerable for testing.
    /// </summary>
    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.Yield();
        }
    }

    #region Basic Writing Tests

    [Fact]
    public async Task WriteAllAsync_SimplePeople_WritesCorrectCsv()
    {
        // Arrange
        var people = new[]
        {
            new Person { Name = "Alice", Age = 30, City = "NYC" },
            new Person { Name = "Bob", Age = 25, City = "LA" }
        };
        var stream = new MemoryStream();

        // Act
        await using var writer = CsvWriter<Person>.Create(stream);
        await writer.WriteAllAsync(ToAsyncEnumerable(people));
        await writer.FlushAsync();

        // Assert
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().Contain("Name,Age,City");
        csv.Should().Contain("Alice,30,NYC");
        csv.Should().Contain("Bob,25,LA");
    }

    [Fact]
    public async Task WriteAllAsync_EmptyCollection_WritesOnlyHeaders()
    {
        // Arrange
        var people = Array.Empty<Person>();
        var stream = new MemoryStream();

        // Act
        await using var writer = CsvWriter<Person>.Create(stream);
        await writer.WriteAllAsync(ToAsyncEnumerable(people));
        await writer.FlushAsync();

        // Assert
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().Be("Name,Age,City\n");
    }

    [Fact]
    public async Task WriteAllAsync_NoHeaders_SkipsHeaderRow()
    {
        // Arrange
        var people = new[]
        {
            new Person { Name = "Alice", Age = 30, City = "NYC" }
        };
        var stream = new MemoryStream();
        var options = new CsvWriterOptions { WriteHeaders = false };

        // Act
        await using var writer = CsvWriter<Person>.Create(stream, options);
        await writer.WriteAllAsync(ToAsyncEnumerable(people));
        await writer.FlushAsync();

        // Assert
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().NotContain("Name,Age,City");
        csv.Should().StartWith("Alice,30,NYC");
    }

    #endregion

    #region Quote Escaping Tests

    [Fact]
    public async Task WriteAllAsync_FieldWithComma_QuotesField()
    {
        // Arrange
        var people = new[]
        {
            new Person { Name = "Smith, John", Age = 30, City = "NYC" }
        };
        var stream = new MemoryStream();

        // Act
        await using var writer = CsvWriter<Person>.Create(stream);
        await writer.WriteAllAsync(ToAsyncEnumerable(people));
        await writer.FlushAsync();

        // Assert
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().Contain("\"Smith, John\"");
    }

    [Fact]
    public async Task WriteAllAsync_FieldWithQuotes_EscapesQuotes()
    {
        // Arrange
        var people = new[]
        {
            new Person { Name = "He said \"Hello\"", Age = 30, City = "NYC" }
        };
        var stream = new MemoryStream();

        // Act
        await using var writer = CsvWriter<Person>.Create(stream);
        await writer.WriteAllAsync(ToAsyncEnumerable(people));
        await writer.FlushAsync();

        // Assert
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().Contain("\"He said \"\"Hello\"\"\"");
    }

    [Fact]
    public async Task WriteAllAsync_FieldWithNewline_QuotesField()
    {
        // Arrange
        var people = new[]
        {
            new Person { Name = "Line1\nLine2", Age = 30, City = "NYC" }
        };
        var stream = new MemoryStream();

        // Act
        await using var writer = CsvWriter<Person>.Create(stream);
        await writer.WriteAllAsync(ToAsyncEnumerable(people));
        await writer.FlushAsync();

        // Assert
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().Contain("\"Line1\nLine2\"");
    }

    #endregion

    #region QuoteMode Tests

    [Fact]
    public async Task WriteAllAsync_QuoteModeNone_NeverQuotes()
    {
        // Arrange
        var people = new[]
        {
            new Person { Name = "Alice", Age = 30, City = "NYC" }
        };
        var stream = new MemoryStream();
        var options = new CsvWriterOptions { QuoteMode = CsvQuoteMode.None };

        // Act
        await using var writer = CsvWriter<Person>.Create(stream, options);
        await writer.WriteAllAsync(ToAsyncEnumerable(people));
        await writer.FlushAsync();

        // Assert
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().NotContain("\"");
    }

    [Fact]
    public async Task WriteAllAsync_QuoteModeMinimal_QuotesOnlyWhenNeeded()
    {
        // Arrange
        var people = new[]
        {
            new Person { Name = "Alice", Age = 30, City = "NYC" },
            new Person { Name = "Smith, John", Age = 25, City = "LA" }
        };
        var stream = new MemoryStream();
        var options = new CsvWriterOptions { QuoteMode = CsvQuoteMode.Minimal };

        // Act
        await using var writer = CsvWriter<Person>.Create(stream, options);
        await writer.WriteAllAsync(ToAsyncEnumerable(people));
        await writer.FlushAsync();

        // Assert
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().Contain("Alice,30,NYC"); // No quotes
        csv.Should().Contain("\"Smith, John\""); // Quoted due to comma
    }

    [Fact]
    public async Task WriteAllAsync_QuoteModeAll_QuotesAllFields()
    {
        // Arrange
        var people = new[]
        {
            new Person { Name = "Alice", Age = 30, City = "NYC" }
        };
        var stream = new MemoryStream();
        var options = new CsvWriterOptions { QuoteMode = CsvQuoteMode.All };

        // Act
        await using var writer = CsvWriter<Person>.Create(stream, options);
        await writer.WriteAllAsync(ToAsyncEnumerable(people));
        await writer.FlushAsync();

        // Assert
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().Contain("\"Alice\",\"30\",\"NYC\"");
    }

    #endregion

    #region Custom Delimiter Tests

    [Fact]
    public async Task WriteAllAsync_TabDelimiter_WritesTsv()
    {
        // Arrange
        var records = new[]
        {
            new TsvRecord { Field1 = "A", Field2 = "B", Field3 = "C" }
        };
        var stream = new MemoryStream();

        // Act
        await using var writer = CsvWriter<TsvRecord>.Create(stream);
        await writer.WriteAllAsync(ToAsyncEnumerable(records));
        await writer.FlushAsync();

        // Assert
        var tsv = Encoding.UTF8.GetString(stream.ToArray());
        tsv.Should().Contain("A\tB\tC");
    }

    [Fact]
    public async Task WriteAllAsync_CustomDelimiter_UsesDelimiter()
    {
        // Arrange
        var people = new[]
        {
            new Person { Name = "Alice", Age = 30, City = "NYC" }
        };
        var stream = new MemoryStream();
        var options = new CsvWriterOptions { Delimiter = '|' };

        // Act
        await using var writer = CsvWriter<Person>.Create(stream, options);
        await writer.WriteAllAsync(ToAsyncEnumerable(people));
        await writer.FlushAsync();

        // Assert
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().Contain("Alice|30|NYC");
    }

    #endregion

    #region Data Type Formatting Tests

    [Fact]
    public async Task WriteAllAsync_DateTimeWithFormat_FormatsCorrectly()
    {
        // Arrange
        var employees = new[]
        {
            new Employee
            {
                Name = "Alice",
                Department = "Engineering",
                Salary = 75000,
                HireDate = new DateTime(2024, 1, 15),
                IsActive = true
            }
        };
        var stream = new MemoryStream();

        // Act
        await using var writer = CsvWriter<Employee>.Create(stream);
        await writer.WriteAllAsync(ToAsyncEnumerable(employees));
        await writer.FlushAsync();

        // Assert
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().Contain("2024-01-15");
    }

    [Fact]
    public async Task WriteAllAsync_DecimalValues_FormatsWithPrecision()
    {
        // Arrange
        var products = new[]
        {
            new Product { Id = 1, Name = "Widget", Price = 19.99m, Stock = 100 }
        };
        var stream = new MemoryStream();

        // Act
        await using var writer = CsvWriter<Product>.Create(stream);
        await writer.WriteAllAsync(ToAsyncEnumerable(products));
        await writer.FlushAsync();

        // Assert
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().Contain("19.99");
    }

    [Fact]
    public async Task WriteAllAsync_NullableFields_HandlesNulls()
    {
        // Arrange
        var records = new[]
        {
            new NullableFieldsRecord { Name = "Alice", Age = 30, BirthDate = null }
        };
        var stream = new MemoryStream();

        // Act
        await using var writer = CsvWriter<NullableFieldsRecord>.Create(stream);
        await writer.WriteAllAsync(ToAsyncEnumerable(records));
        await writer.FlushAsync();

        // Assert
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().Contain("Alice,30,");
    }

    #endregion

    #region Batch Writing Tests

    [Fact]
    public async Task WriteAsync_SingleRecord_AppendsToStream()
    {
        // Arrange
        var person = new Person { Name = "Alice", Age = 30, City = "NYC" };
        var stream = new MemoryStream();

        // Act
        await using var writer = CsvWriter<Person>.Create(stream);
        await writer.WriteAsync(person);
        await writer.FlushAsync();

        // Assert
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().Contain("Alice,30,NYC");
    }

    [Fact]
    public async Task WriteAsync_MultipleCalls_AppendsRecords()
    {
        // Arrange
        var stream = new MemoryStream();

        // Act
        await using var writer = CsvWriter<Person>.Create(stream);
        await writer.WriteAsync(new Person { Name = "Alice", Age = 30, City = "NYC" });
        await writer.WriteAsync(new Person { Name = "Bob", Age = 25, City = "LA" });
        await writer.FlushAsync();

        // Assert
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().Contain("Alice");
        csv.Should().Contain("Bob");
    }

    #endregion

    #region Large Dataset Tests

    [Fact]
    public async Task WriteAllAsync_LargeDataset_WritesEfficiently()
    {
        // Arrange
        var people = Enumerable.Range(1, 100000)
            .Select(i => new Person { Name = $"Person{i}", Age = 20 + (i % 50), City = $"City{i % 100}" })
            .ToArray();
        var stream = new MemoryStream();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await using var writer = CsvWriter<Person>.Create(stream);
        await writer.WriteAllAsync(ToAsyncEnumerable(people));
        await writer.FlushAsync();
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000); // Should write 100k rows in < 3 seconds
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Split('\n').Length.Should().Be(100001); // 100k data rows + 1 header
    }

    #endregion

    #region Round-trip Tests

    [Fact(Skip = "TODO: Implement CsvWriter<T> and CsvReader<T>")]
    public async Task WriteAndRead_ComplexData_MaintainsIntegrity()
    {
        // Arrange
        var original = new[]
        {
            new Employee
            {
                Name = "Alice, Smith",
                Department = "Engineering \"R&D\"",
                Salary = 75000.50m,
                HireDate = new DateTime(2024, 1, 15),
                IsActive = true
            }
        };
        var stream = new MemoryStream();

        // Act
        // await CsvWriter<Employee>
        //     .Create(stream, new TestCsvContext())
        //     .WriteAllAsync(original);

        stream.Position = 0;

        // var readBack = await CsvReader<Employee>
        //     .Create(stream, new TestCsvContext())
        //     .ReadAllAsync()
        //     .ToListAsync();

        // Assert
        // readBack.Should().HaveCount(1);
        // readBack[0].Name.Should().Be(original[0].Name);
        // readBack[0].Department.Should().Be(original[0].Department);
        // readBack[0].Salary.Should().Be(original[0].Salary);
    }

    #endregion

    #region Header Customization Tests

    [Fact]
    public async Task WriteAllAsync_CustomHeaderNames_UsesCustomNames()
    {
        // Arrange
        var employees = new[]
        {
            new Employee { Name = "Alice", Department = "Engineering", Salary = 75000, HireDate = DateTime.Now, IsActive = true }
        };
        var stream = new MemoryStream();

        // Act
        await using var writer = CsvWriter<Employee>.Create(stream);
        await writer.WriteAllAsync(ToAsyncEnumerable(employees));
        await writer.FlushAsync();

        // Assert
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().Contain("FullName"); // Custom header name from CsvField attribute
    }

    #endregion

    #region Unicode and Special Characters Tests

    [Fact]
    public async Task WriteAllAsync_UnicodeCharacters_WritesCorrectly()
    {
        // Arrange
        var people = new[]
        {
            new Person { Name = "田中太郎", Age = 30, City = "東京" },
            new Person { Name = "José García", Age = 25, City = "Madrid" }
        };
        var stream = new MemoryStream();

        // Act
        await using var writer = CsvWriter<Person>.Create(stream);
        await writer.WriteAllAsync(ToAsyncEnumerable(people));
        await writer.FlushAsync();

        // Assert
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().Contain("田中太郎");
        csv.Should().Contain("José García");
    }

    [Fact]
    public async Task WriteAllAsync_Emojis_WritesCorrectly()
    {
        // Arrange
        var people = new[]
        {
            new Person { Name = "User 😀", Age = 30, City = "NYC" }
        };
        var stream = new MemoryStream();

        // Act
        await using var writer = CsvWriter<Person>.Create(stream);
        await writer.WriteAllAsync(ToAsyncEnumerable(people));
        await writer.FlushAsync();

        // Assert
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.Should().Contain("😀");
    }

    #endregion
}

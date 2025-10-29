using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

#pragma warning disable CS1998 // Async method lacks 'await' operators
#pragma warning disable CS0219 // Variable is assigned but its value is never used

namespace CsvHandler.Tests;

/// <summary>
/// Comprehensive tests for CsvReader functionality including streaming,
/// error handling, type conversion, and async operations.
/// </summary>
public class ReaderTests
{
    // Note: These tests will be fully functional once CsvReader<T> is implemented
    // Currently they serve as specifications for the expected behavior

    #region Basic Reading Tests

    [Fact]
    public async Task ReadAllAsync_SimpleCsv_ReturnsExpectedData()
    {
        // Arrange
        var csv = "Name,Age,City\nAlice,30,NYC\nBob,25,LA"u8.ToArray();
        var stream = new MemoryStream(csv);

        // Act - Using reflection-based API since source generator context not yet implemented
        await using var reader = CsvReader<Person>.Create(stream);
        var people = await reader.ReadAllAsync().ToListAsync();

        // Assert
        Assert.Equal(2, people.Count);
        Assert.Equal("Alice", people[0].Name);
        Assert.Equal(30, people[0].Age);
        Assert.Equal("NYC", people[0].City);
    }

    [Fact]
    public async Task ReadAllAsync_EmptyFile_ReturnsEmptyList()
    {
        // Arrange
        var csv = Array.Empty<byte>();
        var stream = new MemoryStream(csv);

        // Act
        await using var reader = CsvReader<Person>.Create(stream);
        var people = await reader.ReadAllAsync().ToListAsync();

        // Assert
        Assert.Empty(people);
    }

    [Fact]
    public async Task ReadAllAsync_OnlyHeaders_ReturnsEmptyList()
    {
        // Arrange
        var csv = "Name,Age,City"u8.ToArray();
        var stream = new MemoryStream(csv);

        // Act
        await using var reader = CsvReader<Person>.Create(stream);
        var people = await reader.ReadAllAsync().ToListAsync();

        // Assert
        Assert.Empty(people);
    }

    #endregion

    #region Async Streaming Tests

    [Fact]
    public async Task ReadAllAsync_IAsyncEnumerable_StreamsData()
    {
        // Arrange
        var lines = Enumerable.Range(1, 1000)
            .Select(i => $"Person{i},{20 + i},{i % 100}");
        var csv = "Name,Age,City\n" + string.Join("\n", lines);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var count = 0;
        await using var reader = CsvReader<Person>.Create(stream);
        await foreach (var person in reader.ReadAllAsync())
        {
            count++;
            Assert.StartsWith("Person", person.Name);
        }

        // Assert
        Assert.Equal(1000, count);
    }

    [Fact]
    public async Task ReadAllAsync_CancellationToken_StopsReading()
    {
        // Arrange
        var csv = "Name,Age\n" + string.Join("\n", Enumerable.Range(1, 1000).Select(i => $"Person{i},{i}"));
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var cts = new System.Threading.CancellationTokenSource();

        // Act
        var count = 0;
        await using var reader = CsvReader<Person>.Create(stream);
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var person in reader.ReadAllAsync(cts.Token))
            {
                count++;
                if (count >= 100)
                {
                    cts.Cancel();
                }
            }
        });

        // Assert
        Assert.True(count < 1000);
    }

    #endregion

    #region Type Conversion Tests

    [Fact]
    public async Task ReadAllAsync_AllDataTypes_ConvertsCorrectly()
    {
        // Arrange
        var csv = @"ByteValue,ShortValue,IntValue,LongValue,FloatValue,DoubleValue,DecimalValue,BoolValue,CharValue,GuidValue,DateTimeValue,DateTimeOffsetValue
1,100,1000,100000,1.5,2.5,3.5,true,A,12345678-1234-1234-1234-123456789012,2024-01-15 10:30:00,2024-01-15T10:30:00+00:00";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        await using var reader = CsvReader<DataTypesRecord>.Create(stream);
        var records = await reader.ReadAllAsync().ToListAsync();

        // Assert
        var record = Assert.Single(records);
        Assert.Equal(1, record.ByteValue);
        Assert.Equal(1000, record.IntValue);
        Assert.Equal(3.5m, record.DecimalValue);
        Assert.True(record.BoolValue);
    }

    [Fact]
    public async Task ReadAllAsync_NullableTypes_HandlesNulls()
    {
        // Arrange
        var csv = "Name,Age,BirthDate,Salary,IsActive\nAlice,30,2024-01-01,50000.00,true\nBob,,,,"u8.ToArray();
        var stream = new MemoryStream(csv);

        // Act
        await using var reader = CsvReader<NullableFieldsRecord>.Create(stream);
        var records = await reader.ReadAllAsync().ToListAsync();

        // Assert
        Assert.Equal(2, records.Count);
        Assert.Equal(30, records[0].Age);
        Assert.Null(records[1].Age);
        Assert.Null(records[1].Salary);
    }

    [Fact]
    public async Task ReadAllAsync_DateTimeFormats_ParsesCorrectly()
    {
        // Arrange
        // Note: Using property names (not [CsvField] names) since reflection API doesn't support attributes
        var csv = "Name,Department,Salary,HireDate,IsActive\nAlice,Engineering,75000.00,2024-01-15,true"u8.ToArray();
        var stream = new MemoryStream(csv);

        // Act
        await using var reader = CsvReader<Employee>.Create(stream);
        var records = await reader.ReadAllAsync().ToListAsync();

        // Assert
        Assert.Equal(new DateTime(2024, 1, 15), records[0].HireDate);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ReadAllAsync_InvalidDataThrowMode_ThrowsException()
    {
        // Arrange
        var csv = "Name,Age,City\nAlice,NotANumber,NYC"u8.ToArray();
        var stream = new MemoryStream(csv);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FormatException>(async () =>
        {
            var options = new CsvOptions { ErrorHandling = CsvErrorHandling.Throw };
            await using var reader = CsvReader<Person>.Create(stream, new TestCsvContext(), options);
            await reader.ReadAllAsync().ToListAsync();
        });
    }

    [Fact]
    public async Task ReadAllAsync_InvalidDataSkipMode_SkipsInvalidRows()
    {
        // Arrange
        var csv = "Name,Age,City\nAlice,30,NYC\nBob,Invalid,LA\nCharlie,35,Chicago"u8.ToArray();
        var stream = new MemoryStream(csv);

        // Act
        var options = new CsvOptions { ErrorHandling = CsvErrorHandling.Skip };
        await using var reader = CsvReader<Person>.Create(stream, new TestCsvContext(), options);
        var people = await reader.ReadAllAsync().ToListAsync();

        // Assert
        Assert.Equal(2, people.Count);
        Assert.Equal("Alice", people[0].Name);
        Assert.Equal("Charlie", people[1].Name);
    }

    [Fact]
    public async Task ReadAllAsync_InvalidDataCollectMode_CollectsErrors()
    {
        // Arrange
        var csv = "Name,Age,City\nAlice,30,NYC\nBob,Invalid,LA\nCharlie,NotInt,Chicago"u8.ToArray();
        var stream = new MemoryStream(csv);

        // Act
        var options = new CsvOptions { ErrorHandling = CsvErrorHandling.Collect };
        await using var reader = CsvReader<Person>.Create(stream, new TestCsvContext(), options);
        var people = await reader.ReadAllAsync().ToListAsync();

        // Assert
        Assert.Single(people);
        Assert.Equal(2, reader.Errors.Count);
        Assert.Equal(3, reader.Errors[0].LineNumber); // Line 3: Bob,Invalid,LA (line 1 is header)
        Assert.Equal(4, reader.Errors[1].LineNumber); // Line 4: Charlie,NotInt,Chicago
    }

    #endregion

    #region Multi-line and Special Field Tests

    [Fact]
    public async Task ReadAllAsync_QuotedFieldsWithNewlines_ParsesCorrectly()
    {
        // Arrange
        var csv = "Name,Description\n\"Alice\",\"Line1\nLine2\nLine3\"\n\"Bob\",\"Single line\""u8.ToArray();
        var stream = new MemoryStream(csv);

        // Act
        await using var reader = CsvReader<SimpleRecord>.Create(stream, new TestCsvContext());
        var records = await reader.ReadAllAsync().ToListAsync();

        // Assert
        Assert.Equal(2, records.Count);
        Assert.Contains("\n", records[0].Description);
    }

    [Fact]
    public async Task ReadAllAsync_EmptyFields_HandlesCorrectly()
    {
        // Arrange
        var csv = File.ReadAllBytes("TestData/empty_fields.csv");
        var stream = new MemoryStream(csv);

        // Act
        await using var reader = CsvReader<ContactRecord>.Create(stream, new TestCsvContext());
        var records = await reader.ReadAllAsync().ToListAsync();

        // Assert
        Assert.Equal(4, records.Count);
        Assert.True(string.IsNullOrEmpty(records[1].Email));
        Assert.True(string.IsNullOrEmpty(records[2].Phone));
    }

    #endregion

    #region Header Handling Tests

    [Fact]
    public async Task ReadAllAsync_NoHeaders_ReadsDataCorrectly()
    {
        // Arrange
        var csv = "Alice,30,NYC\nBob,25,LA"u8.ToArray();
        var stream = new MemoryStream(csv);

        // Act
        var options = new CsvOptions { HasHeaders = false };
        await using var reader = CsvReader<Person>.Create(stream, new TestCsvContext(), options);
        var people = await reader.ReadAllAsync().ToListAsync();

        // Assert
        Assert.Equal(2, people.Count);
        Assert.Equal("Alice", people[0].Name);
    }

    [Fact]
    public async Task ReadAllAsync_CustomHeaderMapping_MapsCorrectly()
    {
        // Arrange
        var csv = "FullName,Department,Salary,HireDate,IsActive\nAlice,Engineering,75000,2024-01-15,true"u8.ToArray();
        var stream = new MemoryStream(csv);

        // Act
        await using var reader = CsvReader<Employee>.Create(stream, new TestCsvContext());
        var employees = await reader.ReadAllAsync().ToListAsync();

        // Assert
        Assert.Equal("Alice", employees[0].Name); // Maps from FullName
    }

    #endregion

    #region Integration with File System Tests

    [Fact]
    public async Task ReadAllAsync_FromFile_ReadsSuccessfully()
    {
        // Arrange
        var filePath = "TestData/simple.csv";

        // Act
        using var stream = File.OpenRead(filePath);
        await using var reader = CsvReader<Person>.Create(stream, new TestCsvContext());
        var people = await reader.ReadAllAsync().ToListAsync();

        // Assert
        Assert.True(people.Count > 0);
    }

    [Fact]
    public async Task ReadAllAsync_UnicodeFile_HandlesUtf8()
    {
        // Arrange
        var filePath = "TestData/unicode.csv";

        // Act
        using var stream = File.OpenRead(filePath);
        await using var reader = CsvReader<UnicodeRecord>.Create(stream, new TestCsvContext());
        var records = await reader.ReadAllAsync().ToListAsync();

        // Assert
        Assert.Contains(records, r => r.Language == "日本語");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task ReadAllAsync_LargeFile_ProcessesEfficiently()
    {
        // Arrange
        var lines = Enumerable.Range(1, 100000)
            .Select(i => $"Person{i},{20 + (i % 50)},City{i % 100}");
        var csv = "Name,Age,City\n" + string.Join("\n", lines);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await using var reader = CsvReader<Person>.Create(stream);
        var people = await reader.ReadAllAsync().ToListAsync();
        stopwatch.Stop();

        // Assert
        Assert.Equal(100000, people.Count);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000); // Should process 100k rows in < 5 seconds
    }

    #endregion

    #region Custom Converter Tests

    [Fact]
    public async Task ReadAllAsync_CustomConverter_UsesConverter()
    {
        // Arrange
        var csv = "Id,Status\n1,Active\n2,Inactive"u8.ToArray();
        var stream = new MemoryStream(csv);

        // Act
        await using var reader = CsvReader<StatusRecord>.Create(stream, new TestCsvContext());
        var records = await reader.ReadAllAsync().ToListAsync();

        // Assert
        Assert.Equal(Status.Active, records[0].Status);
        Assert.Equal(Status.Inactive, records[1].Status);
    }

    #endregion
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

#pragma warning disable CS1998 // Async method lacks 'await' operators

namespace CsvHandler.Tests;

/// <summary>
/// Integration tests for end-to-end CSV processing scenarios,
/// including real-world file handling, large datasets, and edge cases.
/// </summary>
public class IntegrationTests
{
    private const string TestDataPath = "TestData";

    #region Real CSV File Tests

    [Fact]
    public void ReadSimpleCsv_FromFile_ParsesAllRecords()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "simple.csv");
        if (!File.Exists(filePath))
        {
            // Skip if test data not available
            return;
        }

        var csv = File.ReadAllBytes(filePath);
        var parser = new CsvHandler.Core.Utf8CsvParser(
            csv,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        // Act
        var records = new List<List<string>>();

        while (true)
        {
            Range[] ranges = new Range[10];
            int count = parser.TryReadRecord(ranges);
            if (count <= 0) break;

            var record = new List<string>();
            for (int i = 0; i < count; i++)
            {
                record.Add(Encoding.UTF8.GetString(csv[ranges[i]]));
            }
            records.Add(record);
        }

        // Assert
        records.Should().NotBeEmpty();
        records[0].Should().Contain("Name");
    }

    [Fact]
    public void ReadQuotedCsv_WithMultilineFields_ParsesCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "quoted.csv");
        if (!File.Exists(filePath))
        {
            return;
        }

        var csv = File.ReadAllBytes(filePath);
        var parser = new CsvHandler.Core.Utf8CsvParser(
            csv,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        // Act
        var fieldCount = 0;
        while (parser.TryReadField(out var field))
        {
            fieldCount++;
        }

        // Assert
        fieldCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ReadMalformedCsv_InLenientMode_RecoversSafely()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "malformed.csv");
        if (!File.Exists(filePath))
        {
            return;
        }

        var csv = File.ReadAllBytes(filePath);
        var parser = new CsvHandler.Core.Utf8CsvParser(
            csv,
            CsvHandler.Core.Utf8CsvParserOptions.Lenient);

        // Act
        var fieldCount = 0;
        bool exceptionThrown = false;
        try
        {
            while (parser.TryReadField(out _))
            {
                fieldCount++;
            }
        }
        catch
        {
            exceptionThrown = true;
        }

        // Assert
        exceptionThrown.Should().BeFalse(); // Lenient mode should not throw
        fieldCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ReadUnicodeCsv_PreservesCharacters()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "unicode.csv");
        if (!File.Exists(filePath))
        {
            return;
        }

        var csv = File.ReadAllBytes(filePath);
        var parser = new CsvHandler.Core.Utf8CsvParser(
            csv,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        // Act
        var fields = new List<string>();
        while (parser.TryReadField(out var field))
        {
            fields.Add(Encoding.UTF8.GetString(field));
        }

        // Assert
        fields.Should().Contain(f => f.Contains("日本語") || f.Contains("中文") || f.Contains("한국어"));
    }

    [Fact]
    public void ReadEmptyFieldsCsv_HandlesNullsCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "empty_fields.csv");
        if (!File.Exists(filePath))
        {
            return;
        }

        var csv = File.ReadAllBytes(filePath);
        var parser = new CsvHandler.Core.Utf8CsvParser(
            csv,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        // Act
        var records = new List<List<string>>();

        while (true)
        {
            Range[] ranges = new Range[10];
            int count = parser.TryReadRecord(ranges);
            if (count <= 0) break;

            var record = new List<string>();
            for (int i = 0; i < count; i++)
            {
                record.Add(Encoding.UTF8.GetString(csv[ranges[i]]));
            }
            records.Add(record);
        }

        // Assert
        records.Should().HaveCountGreaterThan(1);
        records.Should().Contain(r => r.Any(f => string.IsNullOrEmpty(f)));
    }

    #endregion

    #region Large File Performance Tests

    [Fact]
    public void ParseLargeCsv_1MbFile_CompletesInReasonableTime()
    {
        // Arrange
        var lines = Enumerable.Range(1, 50000)
            .Select(i => $"Person{i},{20 + (i % 50)},City{i % 100},user{i}@example.com");
        var csv = "Name,Age,City,Email\n" + string.Join("\n", lines);
        var bytes = Encoding.UTF8.GetBytes(csv);

        bytes.Length.Should().BeGreaterThan(1_000_000); // > 1MB

        var parser = new CsvHandler.Core.Utf8CsvParser(
            bytes,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var fieldCount = 0;

        while (parser.TryReadField(out _))
        {
            fieldCount++;
        }

        stopwatch.Stop();

        // Assert
        fieldCount.Should().BeGreaterThan(200000); // 50k rows * 4 fields + headers
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500); // Should process 1MB in < 500ms
    }

    [Fact]
    public void ParseLargeCsv_10MbFile_StreamsEfficiently()
    {
        // Arrange - 10MB of CSV data
        var lines = Enumerable.Range(1, 500000)
            .Select(i => $"Person{i},{20 + (i % 50)},City{i % 100}");
        var csv = "Name,Age,City\n" + string.Join("\n", lines);
        var bytes = Encoding.UTF8.GetBytes(csv);

        bytes.Length.Should().BeGreaterThan(10_000_000); // > 10MB

        var parser = new CsvHandler.Core.Utf8CsvParser(
            bytes,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var recordCount = 0;

        while (true)
        {
            Range[] ranges = new Range[10];
            if (parser.TryReadRecord(ranges) <= 0) break;
            recordCount++;
        }

        stopwatch.Stop();

        // Assert
        recordCount.Should().BeGreaterThan(500000);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000); // Should process 10MB in < 3s
    }

    #endregion

    #region Memory Usage Tests

    [Fact]
    public void Parser_LargeFile_DoesNotExcessivelyAllocate()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(true);

        var lines = Enumerable.Range(1, 100000)
            .Select(i => $"Field1_{i},Field2_{i},Field3_{i}");
        var csv = string.Join("\n", lines);
        var bytes = Encoding.UTF8.GetBytes(csv);

        // Act
        var parser = new CsvHandler.Core.Utf8CsvParser(
            bytes,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        var fieldCount = 0;
        while (parser.TryReadField(out _))
        {
            fieldCount++;
        }

        var finalMemory = GC.GetTotalMemory(false);
        var memoryUsed = finalMemory - initialMemory;

        // Assert
        fieldCount.Should().BeGreaterThanOrEqualTo(300000);
        // Parser should be nearly zero-allocation (only the input buffer matters)
        memoryUsed.Should().BeLessThan(bytes.Length * 2); // Allow 2x for GC overhead
    }

    #endregion

    #region Complex CSV Scenarios

    [Fact]
    public void ParseComplexCsv_MixedQuoting_HandlesCorrectly()
    {
        // Arrange
        var csv = @"Name,Description,Value
Alice,Simple field,100
Bob,""Quoted, field with comma"",200
Charlie,""Field with ""embedded"" quotes"",300
Dave,""Multi-line
field"",400
Eve,Simple again,500";

        var bytes = Encoding.UTF8.GetBytes(csv);
        var parser = new CsvHandler.Core.Utf8CsvParser(
            bytes,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        // Act
        var records = new List<List<string>>();

        while (true)
        {
            Range[] ranges = new Range[10];
            int count = parser.TryReadRecord(ranges);
            if (count <= 0) break;

            var record = new List<string>();
            for (int i = 0; i < count; i++)
            {
                record.Add(Encoding.UTF8.GetString(bytes[ranges[i]]));
            }
            records.Add(record);
        }

        // Assert
        records.Should().HaveCount(6); // Header + 5 data rows
        records[2].Should().Contain(f => f.Contains("Quoted, field with comma"));
        records[3].Should().Contain(f => f.Contains("embedded"));
        records[4].Should().Contain(f => f.Contains('\n'));
    }

    [Fact]
    public void ParseCsv_AllDelimiters_WorksCorrectly()
    {
        // Test various delimiter types
        var testCases = new[]
        {
            (Data: "A,B,C", Delimiter: (byte)',', Name: "Comma"),
            (Data: "A\tB\tC", Delimiter: (byte)'\t', Name: "Tab"),
            (Data: "A|B|C", Delimiter: (byte)'|', Name: "Pipe"),
            (Data: "A;B;C", Delimiter: (byte)';', Name: "Semicolon")
        };

        foreach (var testCase in testCases)
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes(testCase.Data);
            var options = CsvHandler.Core.Utf8CsvParserOptions.Default with
            {
                Delimiter = testCase.Delimiter
            };
            var parser = new CsvHandler.Core.Utf8CsvParser(bytes, options);

            // Act
            var fields = new List<string>();
            while (parser.TryReadField(out var field))
            {
                fields.Add(Encoding.UTF8.GetString(field));
            }

            // Assert
            fields.Should().Equal(["A", "B", "C"], $"Failed for {testCase.Name}");
        }
    }

    #endregion

    #region Edge Case Integration Tests

    [Fact]
    public void ParseCsv_OnlyHeaders_ReturnsHeaders()
    {
        // Arrange
        var csv = "Name,Age,City"u8.ToArray();
        var parser = new CsvHandler.Core.Utf8CsvParser(
            csv,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        // Act
        var fields = new List<string>();
        while (parser.TryReadField(out var field))
        {
            fields.Add(Encoding.UTF8.GetString(field));
        }

        // Assert
        fields.Should().Equal(["Name", "Age", "City"]);
    }

    [Fact]
    public void ParseCsv_SingleColumn_HandlesCorrectly()
    {
        // Arrange
        var csv = "Column1\nValue1\nValue2\nValue3"u8.ToArray();
        var parser = new CsvHandler.Core.Utf8CsvParser(
            csv,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        // Act
        var values = new List<string>();
        while (parser.TryReadField(out var field))
        {
            values.Add(Encoding.UTF8.GetString(field));
        }

        // Assert
        values.Should().Equal(["Column1", "Value1", "Value2", "Value3"]);
    }

    [Fact]
    public void ParseCsv_InconsistentColumns_HandlesGracefully()
    {
        // Arrange
        var csv = "A,B,C\nD,E\nF,G,H,I"u8.ToArray();
        var parser = new CsvHandler.Core.Utf8CsvParser(
            csv,
            CsvHandler.Core.Utf8CsvParserOptions.Lenient);

        // Act
        var records = new List<List<string>>();

        while (true)
        {
            Range[] ranges = new Range[10];
            int count = parser.TryReadRecord(ranges);
            if (count <= 0) break;

            var record = new List<string>();
            for (int i = 0; i < count; i++)
            {
                record.Add(Encoding.UTF8.GetString(csv[ranges[i]]));
            }
            records.Add(record);
        }

        // Assert
        records.Should().HaveCount(3);
        records[0].Should().HaveCount(3);
        records[1].Should().HaveCount(2);
        records[2].Should().HaveCount(4);
    }

    #endregion

    #region Stress Tests

    [Fact]
    public void ParseCsv_VeryLongFields_HandlesCorrectly()
    {
        // Arrange - Fields with 100k characters each
        var longField1 = new string('A', 100000);
        var longField2 = new string('B', 100000);
        var csv = $"{longField1},{longField2}";
        var bytes = Encoding.UTF8.GetBytes(csv);

        var parser = new CsvHandler.Core.Utf8CsvParser(
            bytes,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        // Act
        parser.TryReadField(out var field1);
        parser.TryReadField(out var field2);

        // Assert
        field1.Length.Should().Be(100000);
        field2.Length.Should().Be(100000);
    }

    [Fact]
    public void ParseCsv_ManyColumns_HandlesCorrectly()
    {
        // Arrange - 1000 columns
        var fields = Enumerable.Range(1, 1000).Select(i => $"Field{i}");
        var csv = string.Join(",", fields);
        var bytes = Encoding.UTF8.GetBytes(csv);

        var parser = new CsvHandler.Core.Utf8CsvParser(
            bytes,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        // Act
        var count = 0;
        while (parser.TryReadField(out _))
        {
            count++;
        }

        // Assert
        count.Should().Be(1000);
    }

    [Fact]
    public void ParseCsv_RepeatedParsing_MaintainsPerformance()
    {
        // Arrange
        var csv = "A,B,C\nD,E,F\nG,H,I"u8.ToArray();
        var times = new List<long>();

        // Act - Parse same CSV 1000 times
        for (int i = 0; i < 1000; i++)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var parser = new CsvHandler.Core.Utf8CsvParser(
                csv,
                CsvHandler.Core.Utf8CsvParserOptions.Default);

            while (parser.TryReadField(out _)) { }

            stopwatch.Stop();
            times.Add(stopwatch.ElapsedTicks);
        }

        // Assert - Performance should be consistent
        var avgTime = times.Average();
        var deviationCheck = times.All(t => Math.Abs(t - avgTime) < avgTime * 2);
        deviationCheck.Should().BeTrue(); // All times within 2x of average
    }

    #endregion

    #region Round-trip Integration Tests

    [Fact(Skip = "TODO: Implement CsvWriter and CsvReader")]
    public async Task WriteAndRead_RealWorldData_MaintainsIntegrity()
    {
        // Arrange
        var testData = new[]
        {
            new Person { Name = "Alice, Smith", Age = 30, City = "New York" },
            new Person { Name = "Bob \"Builder\"", Age = 25, City = "Los Angeles" },
            new Person { Name = "Charlie\nBrown", Age = 35, City = "Chicago" }
        };

        var stream = new MemoryStream();

        // Act - Write
        // await CsvWriter<Person>
        //     .Create(stream, new TestCsvContext())
        //     .WriteAllAsync(testData);

        // Act - Read
        stream.Position = 0;
        // var readBack = await CsvReader<Person>
        //     .Create(stream, new TestCsvContext())
        //     .ReadAllAsync()
        //     .ToListAsync();

        // Assert
        // readBack.Should().HaveCount(3);
        // for (int i = 0; i < 3; i++)
        // {
        //     readBack[i].Name.Should().Be(testData[i].Name);
        //     readBack[i].Age.Should().Be(testData[i].Age);
        //     readBack[i].City.Should().Be(testData[i].City);
        // }
    }

    #endregion

    #region Excel/Google Sheets Compatibility Tests

    [Fact]
    public void ParseCsv_ExcelExport_HandlesCorrectly()
    {
        // Excel exports use CRLF and may include BOM
        var csv = "\ufeffName,Value\r\nAlice,100\r\nBob,200";
        var bytes = Encoding.UTF8.GetBytes(csv);

        var parser = new CsvHandler.Core.Utf8CsvParser(
            bytes,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        // Act
        var fields = new List<string>();
        while (parser.TryReadField(out var field))
        {
            fields.Add(Encoding.UTF8.GetString(field).Trim('\ufeff')); // Strip BOM if present
        }

        // Assert
        fields.Should().Contain("Name");
        fields.Should().Contain("Alice");
    }

    #endregion
}

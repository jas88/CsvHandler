using System;
using System.Text;
using CsvHandler.Core;
using FluentAssertions;
using Xunit;

namespace CsvHandler.Tests;

/// <summary>
/// Comprehensive tests for Utf8CsvParser covering RFC 4180 compliance,
/// SIMD/scalar code paths, and various edge cases.
/// </summary>
public class ParserTests
{
    #region RFC 4180 Compliance Tests

    [Fact]
    public void TryReadField_SimpleUnquotedField_ReturnsCorrectValue()
    {
        // Arrange
        var csv = "Alice,30"u8.ToArray();
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);

        // Act
        var success1 = parser.TryReadField(out var field1);
        var success2 = parser.TryReadField(out var field2);

        // Assert
        success1.Should().BeTrue();
        Encoding.UTF8.GetString(field1).Should().Be("Alice");
        success2.Should().BeTrue();
        Encoding.UTF8.GetString(field2).Should().Be("30");
    }

    [Fact]
    public void TryReadField_QuotedFieldWithComma_ReturnsCorrectValue()
    {
        // Arrange
        var csv = "\"Smith, John\",42"u8.ToArray();
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);

        // Act
        var success1 = parser.TryReadField(out var field1);
        var success2 = parser.TryReadField(out var field2);

        // Assert
        success1.Should().BeTrue();
        Encoding.UTF8.GetString(field1).Should().Be("Smith, John");
        success2.Should().BeTrue();
        Encoding.UTF8.GetString(field2).Should().Be("42");
    }

    [Fact]
    public void TryReadField_QuotedFieldWithEmbeddedNewline_ReturnsCorrectValue()
    {
        // Arrange
        var csv = "\"Line1\nLine2\",Value2"u8.ToArray();
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);

        // Act
        var success1 = parser.TryReadField(out var field1);
        var success2 = parser.TryReadField(out var field2);

        // Assert
        success1.Should().BeTrue();
        Encoding.UTF8.GetString(field1).Should().Be("Line1\nLine2");
        success2.Should().BeTrue();
        Encoding.UTF8.GetString(field2).Should().Be("Value2");
    }

    [Fact]
    public void TryReadField_EscapedQuotes_ReturnsCorrectValue()
    {
        // Arrange
        var csv = "\"He said \"\"Hello\"\"\",42"u8.ToArray();
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);

        // Act
        var success1 = parser.TryReadField(out var field1);
        var success2 = parser.TryReadField(out var field2);

        // Assert
        success1.Should().BeTrue();
        Encoding.UTF8.GetString(field1).Should().Be("He said \"\"Hello\"\"");
        success2.Should().BeTrue();
        Encoding.UTF8.GetString(field2).Should().Be("42");
    }

    #endregion

    #region Line Ending Tests

    [Theory]
    [InlineData("A,B\r\nC,D", "A", "B", "C", "D")] // CRLF (Windows)
    [InlineData("A,B\nC,D", "A", "B", "C", "D")]   // LF (Unix)
    [InlineData("A,B\rC,D", "A", "B", "C", "D")]   // CR (old Mac)
    public void TryReadField_AllLineEndings_HandlesCorrectly(string csvText, params string[] expected)
    {
        // Arrange
        var csv = Encoding.UTF8.GetBytes(csvText);
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);
        var results = new string[expected.Length];

        // Act
        for (int i = 0; i < expected.Length; i++)
        {
            var success = parser.TryReadField(out var field);
            success.Should().BeTrue();
            results[i] = Encoding.UTF8.GetString(field);
        }

        // Assert
        results.Should().Equal(expected);
    }

    [Fact]
    public void CurrentLine_AfterParsingMultipleLines_TracksCorrectly()
    {
        // Arrange
        var csv = "A,B\nC,D\nE,F"u8.ToArray();
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);

        // Act & Assert
        parser.CurrentLine.Should().Be(1);
        parser.TryReadField(out _);
        parser.TryReadField(out _);
        parser.CurrentLine.Should().Be(1);

        parser.TryReadField(out _);
        parser.CurrentLine.Should().Be(2);
        parser.TryReadField(out _);

        parser.TryReadField(out _);
        parser.CurrentLine.Should().Be(3);
    }

    #endregion

    #region Empty Fields and Edge Cases

    [Fact]
    public void TryReadField_EmptyField_ReturnsEmptySpan()
    {
        // Arrange
        var csv = "A,,C"u8.ToArray();
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);

        // Act
        parser.TryReadField(out var field1);
        parser.TryReadField(out var field2);
        parser.TryReadField(out var field3);

        // Assert
        Encoding.UTF8.GetString(field1).Should().Be("A");
        field2.Length.Should().Be(0);
        Encoding.UTF8.GetString(field3).Should().Be("C");
    }

    [Fact]
    public void TryReadField_TrailingComma_ReturnsEmptyLastField()
    {
        // Arrange
        var csv = "A,B,"u8.ToArray();
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);

        // Act
        parser.TryReadField(out var field1);
        parser.TryReadField(out var field2);
        parser.TryReadField(out var field3);
        var noMoreFields = parser.TryReadField(out _);

        // Assert
        Encoding.UTF8.GetString(field1).Should().Be("A");
        Encoding.UTF8.GetString(field2).Should().Be("B");
        field3.Length.Should().Be(0);
        noMoreFields.Should().BeFalse();
    }

    [Fact]
    public void TryReadField_EmptyInput_ReturnsFalse()
    {
        // Arrange
        var csv = Array.Empty<byte>();
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);

        // Act
        var success = parser.TryReadField(out var field);

        // Assert
        success.Should().BeFalse();
        parser.IsEndOfStream.Should().BeTrue();
    }

    [Fact]
    public void TryReadField_OnlyCommas_ReturnsEmptyFields()
    {
        // Arrange
        var csv = ",,,"u8.ToArray();
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);

        // Act & Assert
        for (int i = 0; i < 4; i++)
        {
            parser.TryReadField(out var field).Should().BeTrue();
            field.Length.Should().Be(0);
        }
    }

    #endregion

    #region Whitespace and Trimming Tests

    [Fact]
    public void TryReadField_TrimFieldsEnabled_TrimsWhitespace()
    {
        // Arrange
        var options = Utf8CsvParserOptions.Default with { TrimFields = true };
        var csv = "  Alice  ,  Bob  "u8.ToArray();
        var parser = new Utf8CsvParser(csv, options);

        // Act
        parser.TryReadField(out var field1);
        parser.TryReadField(out var field2);

        // Assert
        Encoding.UTF8.GetString(field1).Should().Be("Alice");
        Encoding.UTF8.GetString(field2).Should().Be("Bob");
    }

    [Fact]
    public void TryReadField_TrimFieldsDisabled_PreservesWhitespace()
    {
        // Arrange
        var options = Utf8CsvParserOptions.Default with { TrimFields = false };
        var csv = "  Alice  ,  Bob  "u8.ToArray();
        var parser = new Utf8CsvParser(csv, options);

        // Act
        parser.TryReadField(out var field1);
        parser.TryReadField(out var field2);

        // Assert
        Encoding.UTF8.GetString(field1).Should().Be("  Alice  ");
        Encoding.UTF8.GetString(field2).Should().Be("  Bob  ");
    }

    [Fact]
    public void TryReadField_QuotedFieldWithWhitespace_PreservesWhitespace()
    {
        // Arrange
        var options = Utf8CsvParserOptions.Default with { TrimFields = true };
        var csv = "\"  Alice  \",\"  Bob  \""u8.ToArray();
        var parser = new Utf8CsvParser(csv, options);

        // Act
        parser.TryReadField(out var field1);
        parser.TryReadField(out var field2);

        // Assert
        Encoding.UTF8.GetString(field1).Should().Be("  Alice  ");
        Encoding.UTF8.GetString(field2).Should().Be("  Bob  ");
    }

    #endregion

    #region Malformed CSV Tests

    [Fact]
    public void TryReadField_UnterminatedQuotedField_ThrowsInStrictMode()
    {
        // Arrange
        var csv = "\"Unterminated"u8.ToArray();
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);

        // Act & Assert
        // Cannot use Assert.Throws with ref struct, must test directly
        bool exceptionThrown = false;
        try
        {
            parser.TryReadField(out _);
        }
        catch (FormatException ex)
        {
            exceptionThrown = true;
            ex.Message.Should().Contain("Unterminated quoted field");
        }
        exceptionThrown.Should().BeTrue();
    }

    [Fact]
    public void TryReadField_UnterminatedQuotedField_RecoversInLenientMode()
    {
        // Arrange
        var options = Utf8CsvParserOptions.Lenient;
        var csv = "\"Unterminated"u8.ToArray();
        var parser = new Utf8CsvParser(csv, options);

        // Act
        var success = parser.TryReadField(out var field);

        // Assert
        success.Should().BeTrue();
        Encoding.UTF8.GetString(field).Should().Be("Unterminated");
    }

    [Fact]
    public void TryReadField_QuoteInMiddleOfUnquotedField_LenientMode_HandlesGracefully()
    {
        // Arrange
        var options = Utf8CsvParserOptions.Lenient;
        var csv = "Test\"Quote,Value"u8.ToArray();
        var parser = new Utf8CsvParser(csv, options);

        // Act
        parser.TryReadField(out var field1);
        parser.TryReadField(out var field2);

        // Assert
        Encoding.UTF8.GetString(field1).Should().Contain("Test");
        Encoding.UTF8.GetString(field2).Should().Be("Value");
    }

    #endregion

    #region Comment Handling Tests

    [Fact]
    public void TryReadField_CommentLine_SkipsLine()
    {
        // Arrange
        var options = Utf8CsvParserOptions.Lenient; // Has AllowComments = true
        var csv = "#Comment line\nA,B"u8.ToArray();
        var parser = new Utf8CsvParser(csv, options);

        // Act
        parser.TryReadField(out var field1);
        parser.TryReadField(out var field2);

        // Assert
        Encoding.UTF8.GetString(field1).Should().Be("A");
        Encoding.UTF8.GetString(field2).Should().Be("B");
    }

    [Fact]
    public void TryReadField_CommentsDisabled_TreatsHashAsData()
    {
        // Arrange
        var options = Utf8CsvParserOptions.Default with { AllowComments = false };
        var csv = "#NotComment,Value"u8.ToArray();
        var parser = new Utf8CsvParser(csv, options);

        // Act
        parser.TryReadField(out var field1);
        parser.TryReadField(out var field2);

        // Assert
        Encoding.UTF8.GetString(field1).Should().Be("#NotComment");
        Encoding.UTF8.GetString(field2).Should().Be("Value");
    }

    #endregion

    #region Custom Delimiter Tests

    [Fact]
    public void TryReadField_TabDelimiter_ParsesCorrectly()
    {
        // Arrange
        var options = Utf8CsvParserOptions.Tsv;
        var csv = "Name\tAge\tCity\nAlice\t30\tNYC"u8.ToArray();
        var parser = new Utf8CsvParser(csv, options);

        // Act
        parser.TryReadField(out var h1);
        parser.TryReadField(out var h2);
        parser.TryReadField(out var h3);
        parser.TryReadField(out var v1);
        parser.TryReadField(out var v2);
        parser.TryReadField(out var v3);

        // Assert
        Encoding.UTF8.GetString(v1).Should().Be("Alice");
        Encoding.UTF8.GetString(v2).Should().Be("30");
        Encoding.UTF8.GetString(v3).Should().Be("NYC");
    }

    [Fact]
    public void TryReadField_CustomDelimiter_ParsesCorrectly()
    {
        // Arrange
        var options = Utf8CsvParserOptions.Default with { Delimiter = (byte)'|' };
        var csv = "A|B|C"u8.ToArray();
        var parser = new Utf8CsvParser(csv, options);

        // Act
        parser.TryReadField(out var field1);
        parser.TryReadField(out var field2);
        parser.TryReadField(out var field3);

        // Assert
        Encoding.UTF8.GetString(field1).Should().Be("A");
        Encoding.UTF8.GetString(field2).Should().Be("B");
        Encoding.UTF8.GetString(field3).Should().Be("C");
    }

    #endregion

    #region TryReadRecord Tests

    [Fact]
    public void TryReadRecord_ValidRecord_ReturnsFieldCount()
    {
        // Arrange
        var csv = "A,B,C\nD,E,F"u8.ToArray();
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);
        Range[] ranges = new Range[10];

        // Act
        var count1 = parser.TryReadRecord(ranges);
        var count2 = parser.TryReadRecord(ranges);

        // Assert
        count1.Should().Be(3);
        count2.Should().Be(3);
    }

    [Fact]
    public void TryReadRecord_EndOfStream_ReturnsMinusOne()
    {
        // Arrange
        var csv = "A,B"u8.ToArray();
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);
        Range[] ranges = new Range[10];

        // Act
        parser.TryReadRecord(ranges);
        var result = parser.TryReadRecord(ranges);

        // Assert
        result.Should().Be(-1);
    }

    [Fact]
    public void TryReadRecord_MultipleRecords_ReturnsCorrectRanges()
    {
        // Arrange
        var csv = "A,B\nC,D"u8.ToArray();
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);
        Range[] ranges = new Range[10];

        // Act
        var count = parser.TryReadRecord(ranges);

        // Assert
        count.Should().Be(2);
        Encoding.UTF8.GetString(csv[ranges[0]]).Should().Be("A");
        Encoding.UTF8.GetString(csv[ranges[1]]).Should().Be("B");
    }

    #endregion

    #region Parser State Management Tests

    [Fact]
    public void Reset_AfterParsing_ResetsToBeginning()
    {
        // Arrange
        var csv = "A,B,C"u8.ToArray();
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);
        parser.TryReadField(out _);
        parser.TryReadField(out _);

        // Act
        parser.Reset();

        // Assert
        parser.Position.Should().Be(0);
        parser.CurrentLine.Should().Be(1);
        parser.TryReadField(out var field);
        Encoding.UTF8.GetString(field).Should().Be("A");
    }

    [Fact]
    public void SkipRecord_SkipsToNextRecord()
    {
        // Arrange
        var csv = "A,B,C\nD,E,F"u8.ToArray();
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);

        // Act
        parser.SkipRecord();
        parser.TryReadField(out var field);

        // Assert
        Encoding.UTF8.GetString(field).Should().Be("D");
        parser.CurrentLine.Should().Be(2);
    }

    [Fact]
    public void IsEndOfStream_TracksPosition()
    {
        // Arrange
        var csv = "A"u8.ToArray();
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);

        // Act & Assert
        parser.IsEndOfStream.Should().BeFalse();
        parser.TryReadField(out _);
        parser.IsEndOfStream.Should().BeTrue();
    }

    #endregion

    #region Performance and SIMD Tests

    [Fact]
    public void TryReadField_LargeUnquotedField_ParsesEfficiently()
    {
        // Arrange
        var largeField = new string('A', 10000);
        var csv = Encoding.UTF8.GetBytes($"{largeField},B");
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);

        // Act
        var success = parser.TryReadField(out var field);

        // Assert
        success.Should().BeTrue();
        field.Length.Should().Be(10000);
    }

    [Fact]
    public void TryReadField_ManyFields_ParsesAllCorrectly()
    {
        // Arrange
        var fields = new string[100];
        for (int i = 0; i < 100; i++)
        {
            fields[i] = $"Field{i}";
        }
        var csv = Encoding.UTF8.GetBytes(string.Join(",", fields));
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);

        // Act
        for (int i = 0; i < 100; i++)
        {
            parser.TryReadField(out var field);
            Encoding.UTF8.GetString(field).Should().Be($"Field{i}");
        }

        // Assert
        parser.IsEndOfStream.Should().BeTrue();
    }

    #endregion

    #region Quote Mode Tests

    [Fact]
    public void TryReadField_IgnoreQuotesMode_TreatsQuotesAsLiteral()
    {
        // Arrange
        var options = Utf8CsvParserOptions.Default with { Mode = Core.CsvParseMode.IgnoreQuotes };
        var csv = "\"A\",\"B\""u8.ToArray();
        var parser = new Utf8CsvParser(csv, options);

        // Act
        parser.TryReadField(out var field1);
        parser.TryReadField(out var field2);

        // Assert
        Encoding.UTF8.GetString(field1).Should().Be("\"A\"");
        Encoding.UTF8.GetString(field2).Should().Be("\"B\"");
    }

    #endregion

    #region UTF-8 and Special Characters Tests

    [Fact]
    public void TryReadField_Utf8Characters_ParsesCorrectly()
    {
        // Arrange
        var csv = "日本語,中文,한국어"u8.ToArray();
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);

        // Act
        parser.TryReadField(out var field1);
        parser.TryReadField(out var field2);
        parser.TryReadField(out var field3);

        // Assert
        Encoding.UTF8.GetString(field1).Should().Be("日本語");
        Encoding.UTF8.GetString(field2).Should().Be("中文");
        Encoding.UTF8.GetString(field3).Should().Be("한국어");
    }

    [Fact]
    public void TryReadField_EmojisAndUnicode_ParsesCorrectly()
    {
        // Arrange
        var csv = "😀,🎉,✨"u8.ToArray();
        var parser = new Utf8CsvParser(csv, Utf8CsvParserOptions.Default);

        // Act
        parser.TryReadField(out var field1);
        parser.TryReadField(out var field2);
        parser.TryReadField(out var field3);

        // Assert
        Encoding.UTF8.GetString(field1).Should().Be("😀");
        Encoding.UTF8.GetString(field2).Should().Be("🎉");
        Encoding.UTF8.GetString(field3).Should().Be("✨");
    }

    #endregion
}

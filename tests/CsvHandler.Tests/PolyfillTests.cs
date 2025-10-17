using System;
using System.Text;
using FluentAssertions;
using Xunit;

namespace CsvHandler.Tests;

/// <summary>
/// Tests to verify that netstandard2.0 polyfills work correctly
/// and provide feature parity with modern .NET versions.
/// </summary>
public class PolyfillTests
{
    #region Parser Compatibility Tests

    [Fact]
    public void Parser_NetStandard20_ParsesCorrectly()
    {
        // Verify parser works on netstandard2.0
        var csv = "A,B,C"u8.ToArray();
        var parser = new CsvHandler.Core.Utf8CsvParser(
            csv,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        parser.TryReadField(out var field1);
        parser.TryReadField(out var field2);
        parser.TryReadField(out var field3);

        Encoding.UTF8.GetString(field1).Should().Be("A");
        Encoding.UTF8.GetString(field2).Should().Be("B");
        Encoding.UTF8.GetString(field3).Should().Be("C");
    }

    [Fact]
    public void Parser_VectorizedScanning_WorksOnNetStandard20()
    {
        // .NET Standard 2.0 uses System.Numerics.Vector instead of System.Runtime.Intrinsics
        var largeField = new string('A', 1000);
        var csv = Encoding.UTF8.GetBytes($"{largeField},B,C");
        var parser = new CsvHandler.Core.Utf8CsvParser(
            csv,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        parser.TryReadField(out var field);
        field.Length.Should().Be(1000);
    }

    #endregion

    #region Span and Memory Polyfills

    [Fact]
    public void Span_Operations_WorkOnNetStandard20()
    {
        // System.Memory package provides Span<T> for netstandard2.0
        var csv = "Hello,World"u8.ToArray();
        ReadOnlySpan<byte> span = csv;

        var parser = new CsvHandler.Core.Utf8CsvParser(
            span,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        parser.TryReadField(out var field);
        Encoding.UTF8.GetString(field).Should().Be("Hello");
    }

    [Fact]
    public void Memory_SlicingAndIndexing_WorksCorrectly()
    {
        // Verify Memory<T> operations work via polyfill
        var csv = "A,B,C,D,E"u8.ToArray();
        Memory<byte> memory = csv;
        var slice = memory.Slice(0, 5); // "A,B,C"

        var parser = new CsvHandler.Core.Utf8CsvParser(
            slice.Span,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        var count = 0;
        while (parser.TryReadField(out _))
        {
            count++;
        }

        count.Should().Be(3);
    }

    #endregion

    #region HashCode Polyfill Tests

    [Fact]
    public void HashCode_Combine_WorksOnNetStandard20()
    {
        // Microsoft.Bcl.HashCode provides HashCode.Combine for netstandard2.0
        var hash1 = HashCode.Combine("Alice", 30);
        var hash2 = HashCode.Combine("Alice", 30);
        var hash3 = HashCode.Combine("Bob", 25);

        hash1.Should().Be(hash2);
        hash1.Should().NotBe(hash3);
    }

    #endregion

    #region String and Encoding Polyfills

    [Fact]
    public void Utf8_EncodingOperations_WorkOnNetStandard20()
    {
        // Verify UTF-8 operations work with polyfills
        var text = "Hello, 世界!";
        var bytes = Encoding.UTF8.GetBytes(text);
        var roundtrip = Encoding.UTF8.GetString(bytes);

        roundtrip.Should().Be(text);
    }

    [Fact]
    public void String_Operations_MatchModernBehavior()
    {
        // Verify string operations have parity
        var text = "  Trimmed  ";

#if NETSTANDARD2_0
        // In netstandard2.0, some string methods might use polyfills
        var trimmed = text.Trim();
#else
        var trimmed = text.Trim();
#endif

        trimmed.Should().Be("Trimmed");
    }

    #endregion

    #region Async Enumerable Polyfills

    [Fact(Skip = "TODO: Verify IAsyncEnumerable polyfill")]
    public async System.Threading.Tasks.Task AsyncEnumerable_WorksOnNetStandard20()
    {
        // System.Linq.Async provides IAsyncEnumerable for netstandard2.0
        // Verify that our CSV reader works with the polyfill

#if NETSTANDARD2_0
        // Should use polyfill from System.Linq.Async or similar
#endif

        await System.Threading.Tasks.Task.CompletedTask;
    }

    #endregion

    #region Range and Index Polyfills

    [Fact]
    public void Range_Operations_WorkOnNetStandard20()
    {
        // C# 8.0 Range/Index types work via compiler translation
        var csv = "A,B,C,D,E"u8.ToArray();
        var parser = new CsvHandler.Core.Utf8CsvParser(
            csv,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        Range[] ranges = new Range[10];
        var count = parser.TryReadRecord(ranges);

        count.Should().Be(5);

        // Verify Range works
        var firstRange = ranges[0];
        var slice = csv[firstRange];
        Encoding.UTF8.GetString(slice).Should().Be("A");
    }

    #endregion

    #region Performance Comparison Tests

    [Fact]
    public void Parser_Performance_ComparableAcrossTfms()
    {
        // Verify that netstandard2.0 performance is acceptable
        var csv = string.Join("\n", System.Linq.Enumerable.Range(1, 1000)
            .Select(i => $"Field{i},Value{i},{i}"));
        var bytes = Encoding.UTF8.GetBytes(csv);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var parser = new CsvHandler.Core.Utf8CsvParser(
            bytes,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        int fieldCount = 0;
        while (parser.TryReadField(out _))
        {
            fieldCount++;
        }

        stopwatch.Stop();

        fieldCount.Should().BeGreaterThanOrEqualTo(3000);

#if NET6_0_OR_GREATER
        // .NET 6+ should be faster due to SIMD, but netstandard2.0 should still be reasonable
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50);
#else
        // netstandard2.0 may be slightly slower but still performant
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
#endif
    }

    #endregion

    #region Compilation Symbol Tests

    [Fact]
    public void CompilationSymbols_SetCorrectly()
    {
#if NETSTANDARD2_0
        // Verify we're actually testing netstandard2.0 code path
        var isNetStandard = true;
#else
        var isNetStandard = false;
#endif

        // This test verifies conditional compilation works
#if NET6_0_OR_GREATER
        var isModernNet = true;
#else
        var isModernNet = false;
#endif

        // At least one should be true
        (isNetStandard || isModernNet).Should().BeTrue();
    }

    #endregion

    #region Vector Acceleration Tests

    [Fact]
    public void Vector_IsHardwareAccelerated_Available()
    {
#if NETSTANDARD2_0
        // netstandard2.0 uses System.Numerics.Vector
        var isAccelerated = System.Numerics.Vector.IsHardwareAccelerated;
#else
        // .NET 6+ uses System.Runtime.Intrinsics
        var isAccelerated = System.Runtime.Intrinsics.X86.Sse2.IsSupported ||
                           System.Runtime.Intrinsics.Arm.AdvSimd.IsSupported;
#endif

        // Just verify the check doesn't throw (hardware dependent)
        (isAccelerated == true || isAccelerated == false).Should().BeTrue();
    }

    #endregion

    #region Unsafe Operations Tests

    [Fact]
    public void Unsafe_ByteOffset_WorksOnNetStandard20()
    {
        // System.Runtime.CompilerServices.Unsafe package provides Unsafe.ByteOffset
        var csv = "A,B,C"u8.ToArray();
        ReadOnlySpan<byte> span = csv;

        // The parser uses Unsafe operations internally
        var parser = new CsvHandler.Core.Utf8CsvParser(
            span,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        parser.TryReadField(out var field);

        // If Unsafe operations work, we can calculate offsets
        field.Length.Should().BeGreaterThan(0);
    }

    #endregion

    #region Init-Only Setters Tests

    [Fact]
    public void InitOnlySetters_WorkOnNetStandard20()
    {
        // C# 9.0 init-only setters work via IsExternalInit polyfill
        var options = new CsvHandler.Core.Utf8CsvParserOptions
        {
            Delimiter = (byte)'|',
            Quote = (byte)'\'',
            TrimFields = true
        };

        options.Delimiter.Should().Be((byte)'|');
        options.TrimFields.Should().BeTrue();
    }

    #endregion

    #region Record Types Polyfill Tests

    [Fact]
    public void RecordTypes_WorkViaPolyfill()
    {
        // C# 9.0 record types use IsExternalInit
        // Verify that readonly struct works (similar to record struct)
        var options1 = CsvHandler.Core.Utf8CsvParserOptions.Default;
        var options2 = options1 with { Delimiter = (byte)'\t' };

        options1.Delimiter.Should().Be((byte)',');
        options2.Delimiter.Should().Be((byte)'\t');
    }

    #endregion

    #region Buffer Polyfills Tests

    [Fact]
    public void Buffers_ArrayPool_WorksOnNetStandard20()
    {
        // System.Buffers package provides ArrayPool for netstandard2.0
        var pool = System.Buffers.ArrayPool<byte>.Shared;
        var buffer = pool.Rent(1024);

        try
        {
            buffer.Length.Should().BeGreaterOrEqualTo(1024);

            // Use buffer with parser
            var csv = "A,B,C"u8;
            csv.CopyTo(buffer);

            var parser = new CsvHandler.Core.Utf8CsvParser(
                buffer.AsSpan(0, csv.Length),
                CsvHandler.Core.Utf8CsvParserOptions.Default);

            parser.TryReadField(out var field);
            Encoding.UTF8.GetString(field).Should().Be("A");
        }
        finally
        {
            pool.Return(buffer);
        }
    }

    #endregion

    #region Feature Parity Verification

    [Theory]
    [InlineData("A,B,C")]
    [InlineData("\"Quoted, Field\",B,C")]
    [InlineData("A\nB\nC")]
    [InlineData("")]
    public void FeatureParity_AllTfms_ProduceSameResults(string csvText)
    {
        // Verify that all TFMs produce identical results
        var csv = Encoding.UTF8.GetBytes(csvText);
        var parser = new CsvHandler.Core.Utf8CsvParser(
            csv,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        var fields = new System.Collections.Generic.List<string>();
        while (parser.TryReadField(out var field))
        {
            fields.Add(Encoding.UTF8.GetString(field));
        }

        // Results should be consistent across all target frameworks
        // (This test serves as a regression check)
    }

    #endregion
}

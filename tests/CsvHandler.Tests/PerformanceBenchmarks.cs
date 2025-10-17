using System;
using System.IO;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using CsvHandler.Core;

namespace CsvHandler.Tests;

/// <summary>
/// Performance benchmarks for CsvHandler using BenchmarkDotNet.
/// Run with: dotnet run -c Release --project tests/CsvHandler.Tests/CsvHandler.Tests.csproj --filter *PerformanceBenchmarks*
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class PerformanceBenchmarks
{
    private byte[] _smallCsv = null!;
    private byte[] _mediumCsv = null!;
    private byte[] _largeCsv = null!;
    private byte[] _quotedCsv = null!;
    private byte[] _mixedCsv = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Small CSV: 100 rows x 5 columns
        var smallLines = Enumerable.Range(1, 100)
            .Select(i => $"Person{i},{20 + (i % 50)},City{i % 10},user{i}@example.com,Active");
        _smallCsv = Encoding.UTF8.GetBytes("Name,Age,City,Email,Status\n" + string.Join("\n", smallLines));

        // Medium CSV: 10,000 rows x 5 columns (~500KB)
        var mediumLines = Enumerable.Range(1, 10000)
            .Select(i => $"Person{i},{20 + (i % 50)},City{i % 100},user{i}@example.com,Active");
        _mediumCsv = Encoding.UTF8.GetBytes("Name,Age,City,Email,Status\n" + string.Join("\n", mediumLines));

        // Large CSV: 100,000 rows x 5 columns (~5MB)
        var largeLines = Enumerable.Range(1, 100000)
            .Select(i => $"Person{i},{20 + (i % 50)},City{i % 100},user{i}@example.com,Active");
        _largeCsv = Encoding.UTF8.GetBytes("Name,Age,City,Email,Status\n" + string.Join("\n", largeLines));

        // Quoted CSV: 1,000 rows with quoted fields
        var quotedLines = Enumerable.Range(1, 1000)
            .Select(i => $"\"Smith, Person{i}\",\"{20 + (i % 50)}\",\"City {i % 100}\",\"user{i}@example.com\",\"Active\"");
        _quotedCsv = Encoding.UTF8.GetBytes("Name,Age,City,Email,Status\n" + string.Join("\n", quotedLines));

        // Mixed CSV: 1,000 rows with mix of quoted and unquoted
        var mixedLines = Enumerable.Range(1, 1000)
            .Select(i => i % 2 == 0
                ? $"Person{i},{20 + (i % 50)},City{i % 100},user{i}@example.com,Active"
                : $"\"Smith, Person{i}\",{20 + (i % 50)},\"City {i % 100}\",user{i}@example.com,Active");
        _mixedCsv = Encoding.UTF8.GetBytes("Name,Age,City,Email,Status\n" + string.Join("\n", mixedLines));
    }

    #region Parser Benchmarks

    [Benchmark(Description = "Parse Small CSV (100 rows)")]
    public int ParseSmallCsv()
    {
        var parser = new Utf8CsvParser(_smallCsv, Utf8CsvParserOptions.Default);
        var count = 0;
        while (parser.TryReadField(out _))
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "Parse Medium CSV (10k rows, ~500KB)")]
    public int ParseMediumCsv()
    {
        var parser = new Utf8CsvParser(_mediumCsv, Utf8CsvParserOptions.Default);
        var count = 0;
        while (parser.TryReadField(out _))
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "Parse Large CSV (100k rows, ~5MB)")]
    public int ParseLargeCsv()
    {
        var parser = new Utf8CsvParser(_largeCsv, Utf8CsvParserOptions.Default);
        var count = 0;
        while (parser.TryReadField(out _))
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "Parse Quoted CSV")]
    public int ParseQuotedCsv()
    {
        var parser = new Utf8CsvParser(_quotedCsv, Utf8CsvParserOptions.Default);
        var count = 0;
        while (parser.TryReadField(out _))
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "Parse Mixed CSV")]
    public int ParseMixedCsv()
    {
        var parser = new Utf8CsvParser(_mixedCsv, Utf8CsvParserOptions.Default);
        var count = 0;
        while (parser.TryReadField(out _))
        {
            count++;
        }
        return count;
    }

    #endregion

    #region Record-Level Parsing Benchmarks

    [Benchmark(Description = "Parse Records - Medium CSV")]
    public int ParseRecordsMediumCsv()
    {
        var parser = new Utf8CsvParser(_mediumCsv, Utf8CsvParserOptions.Default);
        Range[] ranges = new Range[10];
        var recordCount = 0;

        while (parser.TryReadRecord(ranges) > 0)
        {
            recordCount++;
        }

        return recordCount;
    }

    #endregion

    #region Mode Comparison Benchmarks

    [Benchmark(Description = "RFC 4180 Mode")]
    public int ParseRfc4180Mode()
    {
        var options = Utf8CsvParserOptions.Default;
        var parser = new Utf8CsvParser(_mediumCsv, options);
        var count = 0;
        while (parser.TryReadField(out _))
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "Lenient Mode")]
    public int ParseLenientMode()
    {
        var options = Utf8CsvParserOptions.Lenient;
        var parser = new Utf8CsvParser(_mediumCsv, options);
        var count = 0;
        while (parser.TryReadField(out _))
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "Ignore Quotes Mode")]
    public int ParseIgnoreQuotesMode()
    {
        var options = Utf8CsvParserOptions.Default with { Mode = Core.CsvParseMode.IgnoreQuotes };
        var parser = new Utf8CsvParser(_mediumCsv, options);
        var count = 0;
        while (parser.TryReadField(out _))
        {
            count++;
        }
        return count;
    }

    #endregion

    #region Trim Fields Benchmark

    [Benchmark(Description = "Parse with TrimFields")]
    public int ParseWithTrim()
    {
        var options = Utf8CsvParserOptions.Default with { TrimFields = true };
        var parser = new Utf8CsvParser(_mediumCsv, options);
        var count = 0;
        while (parser.TryReadField(out _))
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "Parse without TrimFields")]
    public int ParseWithoutTrim()
    {
        var options = Utf8CsvParserOptions.Default with { TrimFields = false };
        var parser = new Utf8CsvParser(_mediumCsv, options);
        var count = 0;
        while (parser.TryReadField(out _))
        {
            count++;
        }
        return count;
    }

    #endregion

    #region Delimiter Comparison

    [Benchmark(Description = "Parse Comma-delimited")]
    public int ParseCommaDelimited()
    {
        var parser = new Utf8CsvParser(_mediumCsv, Utf8CsvParserOptions.Default);
        var count = 0;
        while (parser.TryReadField(out _))
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "Parse Tab-delimited")]
    public int ParseTabDelimited()
    {
        // Create TSV data
        var tsvData = _mediumCsv.ToArray();
        for (int i = 0; i < tsvData.Length; i++)
        {
            if (tsvData[i] == (byte)',')
            {
                tsvData[i] = (byte)'\t';
            }
        }

        var parser = new Utf8CsvParser(tsvData, Utf8CsvParserOptions.Tsv);
        var count = 0;
        while (parser.TryReadField(out _))
        {
            count++;
        }
        return count;
    }

    #endregion

    #region Throughput Benchmarks

    [Benchmark(Description = "Throughput Test - MB/s")]
    public int ThroughputTest()
    {
        // Measure parsing throughput in MB/s
        var parser = new Utf8CsvParser(_largeCsv, Utf8CsvParserOptions.Default);
        var count = 0;
        while (parser.TryReadField(out _))
        {
            count++;
        }
        return count;
    }

    #endregion

    #region Memory Allocation Benchmarks

    [Benchmark(Description = "Parser Creation Overhead")]
    public Utf8CsvParser CreateParser()
    {
        return new Utf8CsvParser(_smallCsv, Utf8CsvParserOptions.Default);
    }

    [Benchmark(Description = "Options Creation Overhead")]
    public static Utf8CsvParserOptions CreateOptions()
    {
        return new Utf8CsvParserOptions
        {
            Delimiter = (byte)',',
            Quote = (byte)'"',
            HasHeaders = true,
            TrimFields = false
        };
    }

    #endregion

    #region SIMD vs Scalar Comparison (Platform-dependent)

#if NET6_0_OR_GREATER
    [Benchmark(Description = "Parse with SIMD acceleration (NET6+)")]
    public int ParseWithSimd()
    {
        // .NET 6+ uses SearchValues with SIMD
        var parser = new Utf8CsvParser(_largeCsv, Utf8CsvParserOptions.Default);
        var count = 0;
        while (parser.TryReadField(out _))
        {
            count++;
        }
        return count;
    }
#endif

    #endregion
}

/// <summary>
/// Helper class to run benchmarks from unit tests.
/// </summary>
public static class BenchmarkRunner
{
    public static void RunAll()
    {
        BenchmarkDotNet.Running.BenchmarkRunner.Run<PerformanceBenchmarks>();
    }
}

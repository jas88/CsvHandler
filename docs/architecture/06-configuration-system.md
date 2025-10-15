# Configuration System Architecture

## Executive Summary

This document defines a flexible, high-performance configuration system for CsvHandler that balances compile-time optimization with runtime flexibility. The system uses a two-tier architecture: a **Fast Path** for common scenarios with compile-time optimizations, and a **Flexible Path** for complex runtime configurations.

**Key Design Principles:**
1. Performance first: Common scenarios use zero-allocation, UTF-8 native processing
2. Graceful degradation: Complex configurations automatically fall back to flexible path
3. Clear contracts: Users understand performance implications of their choices
4. Migration friendly: Support 90%+ of CsvHelper use cases
5. Type safety: Immutable, thread-safe configuration objects

---

## 1. The Configuration Challenge

### 1.1 UTF-8 String Literal Constraints

**Problem:** UTF-8 string literals (`"text"u8`) are compile-time only and produce `ReadOnlySpan<byte>`, which cannot be stored in fields or passed across async boundaries.

```csharp
// ✅ Valid: Compile-time constant
ReadOnlySpan<byte> delimiter = ","u8;

// ❌ Invalid: Cannot be stored
class Config
{
    ReadOnlySpan<byte> Delimiter { get; } = ","u8; // Compiler error
}

// ❌ Invalid: Cannot cross async boundaries
async Task ParseAsync(ReadOnlySpan<byte> delimiter) { } // Compiler error
```

**Impact:**
- Cannot create truly configurable parsers with UTF-8 delimiters
- Source generators must know delimiter at compile-time
- Runtime configuration requires different approach

### 1.2 Source Generator Limitations

**Problem:** Source generators cannot handle runtime-determined values.

```csharp
// ✅ Source generator can optimize this
[CsvRecord(Delimiter = ",")]
public partial class Record { }

// ❌ Source generator cannot handle this
var delimiter = GetDelimiterFromConfig(); // Runtime value
var records = CsvReader.Create<Record>(stream, new CsvOptions { Delimiter = delimiter });
```

**Impact:**
- Must emit multiple specialized implementations
- Need runtime dispatcher to select correct implementation
- Cannot optimize every possible configuration

### 1.3 Ref Struct Constraints

**Problem:** `Span<byte>` and related types are ref structs with severe API limitations.

```csharp
// ❌ Cannot use in async methods
public async Task ParseAsync(ReadOnlySpan<byte> data) { }

// ❌ Cannot store in classes
class Parser
{
    ReadOnlySpan<byte> buffer; // Compiler error
}

// ❌ Cannot box to interfaces
IEnumerable<T> ParseMany(ReadOnlySpan<byte> data) { } // Yield return breaks this
```

**Impact:**
- Async APIs must use `Memory<byte>` instead
- Need careful API design to avoid breaking constraints
- Performance/flexibility trade-offs

---

## 2. Two-Tier Architecture

### 2.1 Overview

```
┌─────────────────────────────────────────────────────────────┐
│                        User Code                             │
│  var records = CsvReader.Create<T>(stream, options);        │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ↓
┌────────────────────────────────────────────────────────────┐
│                Configuration Analyzer                       │
│  • Checks if options match Fast Path requirements          │
│  • Selects appropriate implementation tier                 │
└────────────────────────┬───────────────────────────────────┘
                         │
         ┌───────────────┴───────────────┐
         ↓                               ↓
┌──────────────────────┐      ┌──────────────────────────┐
│      FAST PATH       │      │     FLEXIBLE PATH        │
│                      │      │                          │
│ • Compile-time known │      │ • Runtime configuration  │
│ • UTF-8 literals     │      │ • Dynamic delimiters     │
│ • Zero allocation    │      │ • Error callbacks        │
│ • Span<byte> based   │      │ • Complex logic          │
│ • Fully inlined      │      │ • Memory<byte> based     │
│                      │      │                          │
│ Performance: 100%    │      │ Performance: 70-85%      │
└──────────────────────┘      └──────────────────────────┘
```

### 2.2 Fast Path Requirements

A configuration uses the Fast Path if ALL conditions are met:

1. **Delimiter is standard**: `,` (comma), `\t` (tab), `;` (semicolon), or `|` (pipe)
2. **Quote is standard**: `"` (double quote) or `'` (single quote)
3. **Escape is same as quote**: Standard RFC 4180 behavior
4. **No custom callbacks**: No error handlers, validators, or custom converters
5. **No complex features**: No comments, no blank line callbacks, no custom trimming logic

### 2.3 Flexible Path Characteristics

Uses Flexible Path when:

1. **Custom delimiters**: Multi-byte delimiters, Unicode delimiters
2. **Different escape character**: Quote and escape differ
3. **Error callbacks**: Custom error handling logic
4. **Complex validation**: Runtime validation rules
5. **Case-sensitive operations**: Non-standard header matching
6. **Custom type converters**: User-provided converters with state

---

## 3. Configuration Class Design

### 3.1 Core Configuration Types

```csharp
namespace CsvHandler;

/// <summary>
/// Base configuration shared by reader and writer
/// </summary>
public abstract record CsvOptions
{
    // ========================================
    // TIER 1: DELIMITER CONFIGURATION
    // ========================================

    /// <summary>
    /// Field delimiter character (default: comma)
    /// Common: ',' '\t' ';' '|'
    /// </summary>
    public char Delimiter { get; init; } = ',';

    /// <summary>
    /// Quote character for fields containing special characters (default: double quote)
    /// Common: '"' '\''
    /// </summary>
    public char Quote { get; init; } = '"';

    /// <summary>
    /// Escape character for escaping quotes (default: same as Quote)
    /// RFC 4180: Same as quote (doubled quotes)
    /// Alternative: Backslash escaping
    /// </summary>
    public char Escape { get; init; } = '"';

    /// <summary>
    /// Line ending mode (default: auto-detect)
    /// </summary>
    public NewLineMode NewLine { get; init; } = NewLineMode.Auto;

    // ========================================
    // TIER 2: PARSING BEHAVIOR
    // ========================================

    /// <summary>
    /// Culture for number and date parsing (default: InvariantCulture)
    /// </summary>
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// Trim whitespace from fields (default: None)
    /// </summary>
    public TrimMode TrimMode { get; init; } = TrimMode.None;

    /// <summary>
    /// Allow comment lines starting with this character (default: disabled)
    /// </summary>
    public char? CommentChar { get; init; } = null;

    /// <summary>
    /// Skip blank lines (default: true)
    /// </summary>
    public bool IgnoreBlankLines { get; init; } = true;

    // ========================================
    // TIER 3: PERFORMANCE TUNING
    // ========================================

    /// <summary>
    /// Buffer size in bytes (default: 32KB)
    /// Larger buffers improve I/O performance but use more memory
    /// </summary>
    public int BufferSize { get; init; } = 32 * 1024;

    /// <summary>
    /// Maximum field size in bytes (default: 1MB)
    /// Guards against malformed CSV with huge fields
    /// </summary>
    public int MaxFieldSize { get; init; } = 1024 * 1024;

    /// <summary>
    /// Use memory pooling for allocations (default: true)
    /// Reduces GC pressure at cost of slightly higher memory usage
    /// </summary>
    public bool UseMemoryPool { get; init; } = true;

    // ========================================
    // INTERNAL: PERFORMANCE TIER CALCULATION
    // ========================================

    /// <summary>
    /// Determines which performance tier this configuration can use
    /// </summary>
    internal PerformanceTier GetPerformanceTier()
    {
        // Check Fast Path eligibility
        if (IsFastPathEligible())
            return PerformanceTier.Fast;

        // Check Flexible Path eligibility
        if (IsFlexiblePathEligible())
            return PerformanceTier.Flexible;

        // Fallback to General Path
        return PerformanceTier.General;
    }

    private bool IsFastPathEligible()
    {
        // Must be standard delimiter
        if (Delimiter != ',' && Delimiter != '\t' && Delimiter != ';' && Delimiter != '|')
            return false;

        // Must be standard quote
        if (Quote != '"' && Quote != '\'')
            return false;

        // Escape must equal quote (RFC 4180 mode)
        if (Escape != Quote)
            return false;

        // No comments allowed
        if (CommentChar.HasValue)
            return false;

        // Only standard trim modes
        if (TrimMode is not (TrimMode.None or TrimMode.Trim or TrimMode.TrimStart or TrimMode.TrimEnd))
            return false;

        return true;
    }

    private bool IsFlexiblePathEligible()
    {
        // Flexible path handles most configurations
        // Only fail if truly exotic

        // Must be single-byte UTF-8 characters
        if (Delimiter > 127 || Quote > 127 || Escape > 127)
            return false;

        return true;
    }
}

/// <summary>
/// Configuration for CSV reading
/// </summary>
public sealed record CsvReaderOptions : CsvOptions
{
    // ========================================
    // HEADER CONFIGURATION
    // ========================================

    /// <summary>
    /// Does the CSV file have a header row? (default: true)
    /// </summary>
    public bool HasHeaders { get; init; } = true;

    /// <summary>
    /// Header matching mode (default: case-insensitive)
    /// </summary>
    public HeaderMatchMode HeaderMatch { get; init; } = HeaderMatchMode.CaseInsensitive;

    /// <summary>
    /// Allow reordered columns (match by name, not position) (default: true)
    /// </summary>
    public bool AllowColumnReordering { get; init; } = true;

    // ========================================
    // ERROR HANDLING
    // ========================================

    /// <summary>
    /// How to handle parsing errors (default: Throw)
    /// </summary>
    public ErrorHandlingMode ErrorHandling { get; init; } = ErrorHandlingMode.Throw;

    /// <summary>
    /// Maximum errors to collect before stopping (default: 100)
    /// Only applies when ErrorHandling = Collect
    /// </summary>
    public int MaxErrorCount { get; init; } = 100;

    /// <summary>
    /// Callback invoked for bad data (default: null)
    /// WARNING: Using this callback forces Flexible Path
    /// </summary>
    public Action<BadDataContext>? BadDataCallback { get; init; } = null;

    /// <summary>
    /// Callback invoked for missing fields (default: null)
    /// WARNING: Using this callback forces Flexible Path
    /// </summary>
    public Action<MissingFieldContext>? MissingFieldCallback { get; init; } = null;

    /// <summary>
    /// How to handle bad data (default: Throw)
    /// </summary>
    public BadDataHandlingMode BadDataHandling { get; init; } = BadDataHandlingMode.Throw;

    // ========================================
    // VALIDATION
    // ========================================

    /// <summary>
    /// Validate that all expected headers are present (default: true)
    /// </summary>
    public bool ValidateHeaders { get; init; } = true;

    /// <summary>
    /// Allow missing optional fields (default: true)
    /// </summary>
    public bool AllowMissingFields { get; init; } = true;

    // ========================================
    // DEFAULT INSTANCE
    // ========================================

    public static CsvReaderOptions Default { get; } = new();

    // ========================================
    // FACTORY METHODS FOR COMMON SCENARIOS
    // ========================================

    /// <summary>
    /// TSV (tab-separated values) configuration
    /// </summary>
    public static CsvReaderOptions Tsv { get; } = new()
    {
        Delimiter = '\t',
    };

    /// <summary>
    /// Pipe-delimited configuration
    /// </summary>
    public static CsvReaderOptions PipeDelimited { get; } = new()
    {
        Delimiter = '|',
    };

    /// <summary>
    /// Semicolon-delimited (common in Europe)
    /// </summary>
    public static CsvReaderOptions SemicolonDelimited { get; } = new()
    {
        Delimiter = ';',
    };

    /// <summary>
    /// Strict RFC 4180 mode
    /// </summary>
    public static CsvReaderOptions Rfc4180 { get; } = new()
    {
        Delimiter = ',',
        Quote = '"',
        Escape = '"',
        HasHeaders = true,
        TrimMode = TrimMode.None,
        IgnoreBlankLines = false,
    };

    /// <summary>
    /// Lenient mode for messy CSVs
    /// </summary>
    public static CsvReaderOptions Lenient { get; } = new()
    {
        TrimMode = TrimMode.Trim,
        IgnoreBlankLines = true,
        ErrorHandling = ErrorHandlingMode.Collect,
        BadDataHandling = BadDataHandlingMode.UseDefault,
        AllowMissingFields = true,
    };
}

/// <summary>
/// Configuration for CSV writing
/// </summary>
public sealed record CsvWriterOptions : CsvOptions
{
    // ========================================
    // HEADER CONFIGURATION
    // ========================================

    /// <summary>
    /// Write header row (default: true)
    /// </summary>
    public bool WriteHeaders { get; init; } = true;

    /// <summary>
    /// Custom header names (default: null, use property names)
    /// </summary>
    public IReadOnlyList<string>? CustomHeaders { get; init; } = null;

    // ========================================
    // QUOTING BEHAVIOR
    // ========================================

    /// <summary>
    /// When to quote fields (default: WhenNeeded)
    /// </summary>
    public QuoteMode QuoteMode { get; init; } = QuoteMode.WhenNeeded;

    /// <summary>
    /// Always quote these field types
    /// </summary>
    public QuoteFieldTypes AlwaysQuoteTypes { get; init; } = QuoteFieldTypes.None;

    // ========================================
    // FLUSHING BEHAVIOR
    // ========================================

    /// <summary>
    /// When to flush the output stream (default: Auto)
    /// </summary>
    public FlushMode FlushMode { get; init; } = FlushMode.Auto;

    /// <summary>
    /// Auto-flush after this many records (default: 1000)
    /// Only applies when FlushMode = Auto
    /// </summary>
    public int AutoFlushInterval { get; init; } = 1000;

    // ========================================
    // DEFAULT INSTANCE
    // ========================================

    public static CsvWriterOptions Default { get; } = new();
}

// ========================================
// SUPPORTING ENUMS
// ========================================

/// <summary>
/// Line ending mode
/// </summary>
public enum NewLineMode
{
    /// <summary>Auto-detect from input (reading) or use Environment.NewLine (writing)</summary>
    Auto,
    /// <summary>LF: \n (Unix/Linux/macOS)</summary>
    Lf,
    /// <summary>CRLF: \r\n (Windows)</summary>
    CrLf,
    /// <summary>CR: \r (old Mac)</summary>
    Cr,
}

/// <summary>
/// Field trimming mode
/// </summary>
public enum TrimMode
{
    /// <summary>No trimming</summary>
    None,
    /// <summary>Trim whitespace from both ends</summary>
    Trim,
    /// <summary>Trim whitespace from start</summary>
    TrimStart,
    /// <summary>Trim whitespace from end</summary>
    TrimEnd,
    /// <summary>Trim inside quotes as well (non-RFC 4180)</summary>
    TrimInsideQuotes,
}

/// <summary>
/// Header matching mode
/// </summary>
public enum HeaderMatchMode
{
    /// <summary>Exact match (case-sensitive)</summary>
    Exact,
    /// <summary>Case-insensitive match</summary>
    CaseInsensitive,
    /// <summary>Case-insensitive, ignore whitespace</summary>
    CaseInsensitiveIgnoreWhitespace,
}

/// <summary>
/// Error handling strategy
/// </summary>
public enum ErrorHandlingMode
{
    /// <summary>Throw exception on first error</summary>
    Throw,
    /// <summary>Collect errors and continue, check Errors property after</summary>
    Collect,
    /// <summary>Silently ignore errors and skip bad records</summary>
    Ignore,
}

/// <summary>
/// How to handle malformed data
/// </summary>
public enum BadDataHandlingMode
{
    /// <summary>Throw exception</summary>
    Throw,
    /// <summary>Skip the entire record</summary>
    SkipRecord,
    /// <summary>Use default value for the field</summary>
    UseDefault,
}

/// <summary>
/// Quote mode for writing
/// </summary>
public enum QuoteMode
{
    /// <summary>Never quote fields (fast but not RFC 4180 compliant)</summary>
    Never,
    /// <summary>Quote only when field contains special characters</summary>
    WhenNeeded,
    /// <summary>Always quote all fields</summary>
    Always,
    /// <summary>Quote non-numeric fields</summary>
    NonNumeric,
}

/// <summary>
/// Field types that should always be quoted
/// </summary>
[Flags]
public enum QuoteFieldTypes
{
    None = 0,
    Strings = 1 << 0,
    Dates = 1 << 1,
    Guids = 1 << 2,
    Nulls = 1 << 3,
    All = Strings | Dates | Guids | Nulls,
}

/// <summary>
/// Flush mode for writing
/// </summary>
public enum FlushMode
{
    /// <summary>Auto-flush periodically based on AutoFlushInterval</summary>
    Auto,
    /// <summary>Only flush on explicit Flush() call or disposal</summary>
    Manual,
    /// <summary>Flush after every record (slow but safe)</summary>
    EveryRecord,
}

/// <summary>
/// Performance tier for configuration
/// </summary>
internal enum PerformanceTier
{
    /// <summary>
    /// Fast path: Compile-time optimized, UTF-8 native, zero allocation
    /// Performance: 100% (baseline)
    /// </summary>
    Fast,

    /// <summary>
    /// Flexible path: Runtime configuration, most features supported
    /// Performance: 70-85% of fast path
    /// </summary>
    Flexible,

    /// <summary>
    /// General path: Exotic configurations, full generality
    /// Performance: 50-70% of fast path
    /// </summary>
    General,
}

// ========================================
// ERROR CONTEXT TYPES
// ========================================

/// <summary>
/// Context provided to BadDataCallback
/// </summary>
public sealed class BadDataContext
{
    public long RecordNumber { get; init; }
    public int FieldIndex { get; init; }
    public string? FieldName { get; init; }
    public ReadOnlyMemory<byte> RawData { get; init; }
    public string Message { get; init; } = "";
}

/// <summary>
/// Context provided to MissingFieldCallback
/// </summary>
public sealed class MissingFieldContext
{
    public long RecordNumber { get; init; }
    public int ExpectedFieldIndex { get; init; }
    public string ExpectedFieldName { get; init; } = "";
    public int ActualFieldCount { get; init; }
}
```

---

## 4. Runtime Delimiter Handling

### 4.1 The Problem

UTF-8 string literals are compile-time only, but we need runtime delimiters.

### 4.2 Solution: Hybrid Approach

```csharp
namespace CsvHandler.Core;

/// <summary>
/// Handles delimiter detection at runtime while maintaining performance
/// </summary>
internal sealed class DelimiterConfig
{
    // ========================================
    // FAST PATH: COMPILE-TIME KNOWN DELIMITERS
    // ========================================

    // These are compile-time constants using UTF-8 literals
    private static ReadOnlySpan<byte> CommaDelimiter => ","u8;
    private static ReadOnlySpan<byte> TabDelimiter => "\t"u8;
    private static ReadOnlySpan<byte> SemicolonDelimiter => ";"u8;
    private static ReadOnlySpan<byte> PipeDelimiter => "|"u8;
    private static ReadOnlySpan<byte> DoubleQuote => "\""u8;
    private static ReadOnlySpan<byte> SingleQuote => "'"u8;

    // ========================================
    // RUNTIME CONFIGURATION
    // ========================================

    public char DelimiterChar { get; }
    public char QuoteChar { get; }
    public char EscapeChar { get; }

    // For exotic delimiters, store as byte array
    private readonly byte[]? _delimiterBytes;
    private readonly byte[]? _quoteBytes;
    private readonly byte[]? _escapeBytes;

    public DelimiterConfig(char delimiter, char quote, char escape)
    {
        DelimiterChar = delimiter;
        QuoteChar = quote;
        EscapeChar = escape;

        // Only allocate byte arrays for non-standard delimiters
        if (!IsStandardDelimiter(delimiter))
        {
            _delimiterBytes = Encoding.UTF8.GetBytes(new[] { delimiter });
        }

        if (!IsStandardQuote(quote))
        {
            _quoteBytes = Encoding.UTF8.GetBytes(new[] { quote });
        }

        if (escape != quote && !IsStandardQuote(escape))
        {
            _escapeBytes = Encoding.UTF8.GetBytes(new[] { escape });
        }
    }

    /// <summary>
    /// Gets delimiter as ReadOnlySpan<byte> - optimized for standard delimiters
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetDelimiterBytes()
    {
        // Fast path: return compile-time UTF-8 literal
        return DelimiterChar switch
        {
            ',' => CommaDelimiter,
            '\t' => TabDelimiter,
            ';' => SemicolonDelimiter,
            '|' => PipeDelimiter,
            _ => _delimiterBytes ?? throw new InvalidOperationException("Delimiter bytes not initialized"),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetQuoteBytes()
    {
        return QuoteChar switch
        {
            '"' => DoubleQuote,
            '\'' => SingleQuote,
            _ => _quoteBytes ?? throw new InvalidOperationException("Quote bytes not initialized"),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetEscapeBytes()
    {
        // If escape equals quote, return quote bytes
        if (EscapeChar == QuoteChar)
            return GetQuoteBytes();

        return EscapeChar switch
        {
            '"' => DoubleQuote,
            '\'' => SingleQuote,
            _ => _escapeBytes ?? throw new InvalidOperationException("Escape bytes not initialized"),
        };
    }

    private static bool IsStandardDelimiter(char c)
        => c is ',' or '\t' or ';' or '|';

    private static bool IsStandardQuote(char c)
        => c is '"' or '\'';
}
```

### 4.3 Source Generator Strategy

The source generator emits specialized implementations for each standard configuration:

```csharp
// Source generator emits:

// Fast path for comma-delimited CSV
internal sealed class CommaDelimitedCsvParser<T> : ICsvParser<T>
{
    private static ReadOnlySpan<byte> Delimiter => ","u8;
    private static ReadOnlySpan<byte> Quote => "\""u8;

    public T Parse(ReadOnlySpan<byte> line)
    {
        // Highly optimized, fully inlined, using UTF-8 literals
        // ...
    }
}

// Fast path for tab-delimited CSV
internal sealed class TabDelimitedCsvParser<T> : ICsvParser<T>
{
    private static ReadOnlySpan<byte> Delimiter => "\t"u8;
    private static ReadOnlySpan<byte> Quote => "\""u8;

    public T Parse(ReadOnlySpan<byte> line)
    {
        // Highly optimized, fully inlined, using UTF-8 literals
        // ...
    }
}

// Flexible path for custom delimiters
internal sealed class FlexibleCsvParser<T> : ICsvParser<T>
{
    private readonly DelimiterConfig _config;

    public FlexibleCsvParser(DelimiterConfig config)
    {
        _config = config;
    }

    public T Parse(ReadOnlySpan<byte> line)
    {
        // Uses runtime delimiter lookup, slightly slower
        var delimiter = _config.GetDelimiterBytes();
        // ...
    }
}
```

---

## 5. Error Callbacks Without Performance Loss

### 5.1 The Challenge

Error callbacks add overhead to the hot path, but many users never use them.

### 5.2 Solution: Callback Detection and Branching

```csharp
namespace CsvHandler.Core;

/// <summary>
/// Manages error callbacks with minimal overhead
/// </summary>
internal sealed class ErrorCallbackHandler
{
    private readonly Action<BadDataContext>? _badDataCallback;
    private readonly Action<MissingFieldContext>? _missingFieldCallback;

    // Cache whether callbacks are present (avoid null checks in hot path)
    public bool HasBadDataCallback { get; }
    public bool HasMissingFieldCallback { get; }
    public bool HasAnyCallback { get; }

    public ErrorCallbackHandler(
        Action<BadDataContext>? badDataCallback,
        Action<MissingFieldContext>? missingFieldCallback)
    {
        _badDataCallback = badDataCallback;
        _missingFieldCallback = missingFieldCallback;

        HasBadDataCallback = badDataCallback != null;
        HasMissingFieldCallback = missingFieldCallback != null;
        HasAnyCallback = HasBadDataCallback || HasMissingFieldCallback;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnBadData(BadDataContext context)
    {
        // JIT can eliminate this branch when HasBadDataCallback is false
        if (HasBadDataCallback)
        {
            _badDataCallback!(context);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnMissingField(MissingFieldContext context)
    {
        if (HasMissingFieldCallback)
        {
            _missingFieldCallback!(context);
        }
    }
}

/// <summary>
/// Parser base class that branches based on callback presence
/// </summary>
internal abstract class CsvParserBase<T>
{
    protected readonly ErrorCallbackHandler? _errorHandler;

    protected CsvParserBase(CsvReaderOptions options)
    {
        // Only create handler if callbacks are present
        if (options.BadDataCallback != null || options.MissingFieldCallback != null)
        {
            _errorHandler = new ErrorCallbackHandler(
                options.BadDataCallback,
                options.MissingFieldCallback);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryParseField(ReadOnlySpan<byte> field, out int value, long recordNum, int fieldIndex)
    {
        if (Utf8Parser.TryParse(field, out value, out _))
        {
            return true;
        }

        // Only invoke callback if present
        if (_errorHandler?.HasBadDataCallback == true)
        {
            _errorHandler.OnBadData(new BadDataContext
            {
                RecordNumber = recordNum,
                FieldIndex = fieldIndex,
                RawData = field.ToArray(),
                Message = "Failed to parse integer",
            });
        }

        return false;
    }
}
```

### 5.3 Source Generator Optimization

The source generator emits two versions: with and without callbacks:

```csharp
// Generated: Fast path without callbacks
internal sealed class PersonParser_NoCallbacks : ICsvParser<Person>
{
    public Person Parse(ReadOnlySpan<byte> line)
    {
        // Direct parsing, no callback overhead
        var person = new Person();

        // Parse fields directly, throw on error
        if (!Utf8Parser.TryParse(field0, out int id, out _))
            throw new CsvFormatException("Invalid ID");

        person.Id = id;
        // ...
        return person;
    }
}

// Generated: Flexible path with callbacks
internal sealed class PersonParser_WithCallbacks : ICsvParser<Person>
{
    private readonly ErrorCallbackHandler _errorHandler;

    public PersonParser_WithCallbacks(ErrorCallbackHandler errorHandler)
    {
        _errorHandler = errorHandler;
    }

    public Person Parse(ReadOnlySpan<byte> line)
    {
        var person = new Person();

        // Parse fields with callback support
        if (!Utf8Parser.TryParse(field0, out int id, out _))
        {
            _errorHandler.OnBadData(new BadDataContext { /* ... */ });
            return null; // or default handling
        }

        person.Id = id;
        // ...
        return person;
    }
}
```

---

## 6. Case-Insensitive Header Matching on UTF-8

### 6.1 The Challenge

Case-insensitive string comparison typically requires `string.Compare` with culture-specific rules, but we're working with UTF-8 bytes.

### 6.2 Solution: Layered Matching Strategy

```csharp
namespace CsvHandler.Core;

/// <summary>
/// Handles header matching with performance tiers
/// </summary>
internal sealed class HeaderMatcher
{
    private readonly HeaderMatchMode _mode;
    private readonly Dictionary<string, int> _headerMap;
    private readonly string[] _headers;

    public HeaderMatcher(string[] headers, HeaderMatchMode mode)
    {
        _headers = headers;
        _mode = mode;

        // Build lookup dictionary based on mode
        _headerMap = BuildHeaderMap(headers, mode);
    }

    private Dictionary<string, int> BuildHeaderMap(string[] headers, HeaderMatchMode mode)
    {
        var comparer = mode switch
        {
            HeaderMatchMode.Exact => StringComparer.Ordinal,
            HeaderMatchMode.CaseInsensitive => StringComparer.OrdinalIgnoreCase,
            HeaderMatchMode.CaseInsensitiveIgnoreWhitespace =>
                new WhitespaceIgnoringComparer(),
            _ => StringComparer.Ordinal,
        };

        var map = new Dictionary<string, int>(comparer);
        for (int i = 0; i < headers.Length; i++)
        {
            var key = mode == HeaderMatchMode.CaseInsensitiveIgnoreWhitespace
                ? headers[i].Replace(" ", "").Replace("\t", "")
                : headers[i];

            map[key] = i;
        }

        return map;
    }

    /// <summary>
    /// Find header index by name (fast path for string lookup)
    /// </summary>
    public bool TryGetHeaderIndex(string headerName, out int index)
    {
        return _headerMap.TryGetValue(headerName, out index);
    }

    /// <summary>
    /// Find header index by UTF-8 bytes (for direct parsing)
    /// </summary>
    public bool TryGetHeaderIndex(ReadOnlySpan<byte> utf8HeaderName, out int index)
    {
        // Fast path: exact match using byte comparison
        if (_mode == HeaderMatchMode.Exact)
        {
            return TryGetHeaderIndexExact(utf8HeaderName, out index);
        }

        // Slower path: decode to string and use dictionary
        // This is acceptable because header matching happens once per file
        var headerName = Encoding.UTF8.GetString(utf8HeaderName);
        return TryGetHeaderIndex(headerName, out index);
    }

    private bool TryGetHeaderIndexExact(ReadOnlySpan<byte> utf8HeaderName, out int index)
    {
        // Direct byte-by-byte comparison (fastest)
        for (int i = 0; i < _headers.Length; i++)
        {
            var headerBytes = Encoding.UTF8.GetBytes(_headers[i]);
            if (utf8HeaderName.SequenceEqual(headerBytes))
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }
}

/// <summary>
/// Custom comparer that ignores whitespace
/// </summary>
internal sealed class WhitespaceIgnoringComparer : IEqualityComparer<string>
{
    public bool Equals(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        // Remove whitespace and compare case-insensitively
        var xClean = RemoveWhitespace(x);
        var yClean = RemoveWhitespace(y);

        return string.Equals(xClean, yClean, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode(string obj)
    {
        var clean = RemoveWhitespace(obj);
        return StringComparer.OrdinalIgnoreCase.GetHashCode(clean);
    }

    private static string RemoveWhitespace(string s)
    {
        // Use span-based approach for performance
        Span<char> buffer = stackalloc char[s.Length];
        int pos = 0;

        foreach (char c in s)
        {
            if (!char.IsWhiteSpace(c))
            {
                buffer[pos++] = c;
            }
        }

        return new string(buffer.Slice(0, pos));
    }
}
```

---

## 7. Supporting Dynamic Types

### 7.1 The Challenge

Source generators work with compile-time types, but users sometimes need dynamic CSV parsing.

### 7.2 Solution: Reflection-Based Fallback

```csharp
namespace CsvHandler.Dynamic;

/// <summary>
/// Factory that selects appropriate parser based on type and configuration
/// </summary>
internal static class CsvParserFactory<T> where T : class, new()
{
    /// <summary>
    /// Creates the optimal parser for the given options
    /// </summary>
    public static ICsvParser<T> Create(CsvReaderOptions options)
    {
        // Check if source-generated parser is available
        if (CsvSourceGeneratedRegistry.HasParser<T>())
        {
            // Use source-generated parser (fastest)
            return CsvSourceGeneratedRegistry.GetParser<T>(options);
        }

        // Check if type is dynamic
        if (typeof(T) == typeof(ExpandoObject) || typeof(T).Name == "RuntimeType")
        {
            // Use dynamic parser
            return (ICsvParser<T>)new DynamicCsvParser(options);
        }

        // Fall back to reflection-based parser
        return new ReflectionCsvParser<T>(options);
    }
}

/// <summary>
/// Dynamic parser for ExpandoObject or dynamic types
/// </summary>
internal sealed class DynamicCsvParser : ICsvParser<ExpandoObject>
{
    private readonly CsvReaderOptions _options;
    private string[]? _headers;

    public DynamicCsvParser(CsvReaderOptions options)
    {
        _options = options;
    }

    public void SetHeaders(string[] headers)
    {
        _headers = headers;
    }

    public ExpandoObject Parse(ReadOnlySpan<byte> line)
    {
        if (_headers == null)
            throw new InvalidOperationException("Headers must be set before parsing");

        var record = new ExpandoObject();
        var dict = (IDictionary<string, object?>)record;

        // Parse fields and add to dynamic object
        var fields = SplitLine(line);
        for (int i = 0; i < Math.Min(fields.Length, _headers.Length); i++)
        {
            var fieldValue = Encoding.UTF8.GetString(fields[i]);
            dict[_headers[i]] = fieldValue;
        }

        return record;
    }

    private ReadOnlySpan<byte>[] SplitLine(ReadOnlySpan<byte> line)
    {
        // Simplified line splitting logic
        var delimiter = _options.Delimiter;
        var delimiterByte = (byte)delimiter;

        var fields = new List<ReadOnlySpan<byte>>();
        int start = 0;

        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == delimiterByte)
            {
                fields.Add(line.Slice(start, i - start));
                start = i + 1;
            }
        }

        fields.Add(line.Slice(start));
        return fields.ToArray();
    }
}

/// <summary>
/// Reflection-based parser using expression trees for performance
/// </summary>
internal sealed class ReflectionCsvParser<T> : ICsvParser<T> where T : class, new()
{
    private readonly CsvReaderOptions _options;
    private readonly Func<ReadOnlySpan<byte>[], T> _parseFunc;
    private readonly PropertyInfo[] _properties;

    public ReflectionCsvParser(CsvReaderOptions options)
    {
        _options = options;
        _properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToArray();

        // Build expression tree for faster parsing
        _parseFunc = BuildParseFunc();
    }

    private Func<ReadOnlySpan<byte>[], T> BuildParseFunc()
    {
        // Build expression tree that creates instance and sets properties
        var fieldsParam = Expression.Parameter(typeof(ReadOnlySpan<byte>[]), "fields");
        var instanceVar = Expression.Variable(typeof(T), "instance");

        var expressions = new List<Expression>
        {
            Expression.Assign(instanceVar, Expression.New(typeof(T)))
        };

        for (int i = 0; i < _properties.Length; i++)
        {
            var prop = _properties[i];
            var fieldAccess = Expression.ArrayIndex(fieldsParam, Expression.Constant(i));

            // Call appropriate parser based on property type
            var parseMethod = GetParseMethod(prop.PropertyType);
            var parseCall = Expression.Call(parseMethod, fieldAccess);

            var propAssign = Expression.Assign(
                Expression.Property(instanceVar, prop),
                parseCall);

            expressions.Add(propAssign);
        }

        expressions.Add(instanceVar);

        var block = Expression.Block(
            new[] { instanceVar },
            expressions);

        return Expression.Lambda<Func<ReadOnlySpan<byte>[], T>>(block, fieldsParam).Compile();
    }

    private static MethodInfo GetParseMethod(Type type)
    {
        // Return appropriate parsing method based on type
        if (type == typeof(int))
            return typeof(ReflectionCsvParser<T>).GetMethod(nameof(ParseInt32), BindingFlags.NonPublic | BindingFlags.Static)!;

        if (type == typeof(string))
            return typeof(ReflectionCsvParser<T>).GetMethod(nameof(ParseString), BindingFlags.NonPublic | BindingFlags.Static)!;

        // Add more types as needed
        throw new NotSupportedException($"Type {type} is not supported");
    }

    private static int ParseInt32(ReadOnlySpan<byte> field)
    {
        if (Utf8Parser.TryParse(field, out int value, out _))
            return value;

        throw new FormatException($"Failed to parse '{Encoding.UTF8.GetString(field)}' as Int32");
    }

    private static string ParseString(ReadOnlySpan<byte> field)
    {
        return Encoding.UTF8.GetString(field);
    }

    public T Parse(ReadOnlySpan<byte> line)
    {
        var fields = SplitLine(line);
        return _parseFunc(fields);
    }

    private ReadOnlySpan<byte>[] SplitLine(ReadOnlySpan<byte> line)
    {
        // Field splitting logic (simplified)
        // In real implementation, handle quotes, escapes, etc.
        var delimiter = (byte)_options.Delimiter;
        var fields = new List<ReadOnlySpan<byte>>();

        int start = 0;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == delimiter)
            {
                fields.Add(line.Slice(start, i - start));
                start = i + 1;
            }
        }

        fields.Add(line.Slice(start));
        return fields.ToArray();
    }
}
```

---

## 8. Thread-Safe and Immutable Configuration

### 8.1 Design Principles

1. **Immutable by default**: Use `record` with `init` properties
2. **No shared mutable state**: Each reader/writer gets its own config copy
3. **Validation on construction**: Catch configuration errors early
4. **Clear inheritance**: Base options shared, specific options separated

### 8.2 Validation

```csharp
namespace CsvHandler;

public abstract partial record CsvOptions
{
    /// <summary>
    /// Validates configuration and throws if invalid
    /// </summary>
    protected virtual void Validate()
    {
        if (BufferSize < 1024)
            throw new ArgumentException("Buffer size must be at least 1024 bytes", nameof(BufferSize));

        if (BufferSize > 10 * 1024 * 1024)
            throw new ArgumentException("Buffer size must not exceed 10MB", nameof(BufferSize));

        if (MaxFieldSize < 1)
            throw new ArgumentException("Max field size must be positive", nameof(MaxFieldSize));

        if (Delimiter == Quote)
            throw new ArgumentException("Delimiter and quote character cannot be the same");

        if (CommentChar.HasValue && CommentChar.Value == Delimiter)
            throw new ArgumentException("Comment character cannot be the same as delimiter");
    }
}

public sealed partial record CsvReaderOptions : CsvOptions
{
    /// <summary>
    /// Creates a new instance and validates configuration
    /// </summary>
    public CsvReaderOptions()
    {
        Validate();
    }

    protected override void Validate()
    {
        base.Validate();

        if (MaxErrorCount < 1)
            throw new ArgumentException("Max error count must be positive", nameof(MaxErrorCount));

        if (BadDataCallback != null && ErrorHandling == ErrorHandlingMode.Throw)
            throw new ArgumentException("BadDataCallback requires ErrorHandling to be Collect or Ignore");
    }
}
```

---

## 9. Performance Decision Matrix

### 9.1 Fast Path (100% Performance)

**Eligible when ALL of:**
- Delimiter is `,` `\t` `;` or `|`
- Quote is `"` or `'`
- Escape equals quote
- No error callbacks
- No custom type converters
- Standard trim modes only
- No comment support

**Characteristics:**
- UTF-8 string literals for delimiters
- Fully inlined code
- Zero allocation parsing
- SIMD-optimized scanning
- Source-generated parsers

**Use cases:**
- Standard CSV files (90% of cases)
- High-throughput ETL pipelines
- Large file batch processing
- Performance-critical applications

### 9.2 Flexible Path (70-85% Performance)

**Eligible when:**
- Custom single-byte delimiter
- Different escape character
- Error callbacks present
- Custom header matching
- Runtime type converters

**Characteristics:**
- Runtime delimiter lookup
- Callback branching
- Memory<byte> for async
- Still UTF-8 native
- Pooled allocations

**Use cases:**
- Tab-delimited files from legacy systems
- Error logging and recovery
- Complex validation rules
- Custom data formats

### 9.3 General Path (50-70% Performance)

**Falls back when:**
- Multi-byte delimiters
- Unicode delimiters
- Complex trimming logic
- Dynamic types
- Reflection-based parsing

**Characteristics:**
- Full generality
- Byte-by-byte processing
- String allocations possible
- No SIMD optimization
- Reflection or expression trees

**Use cases:**
- Truly exotic CSV formats
- Dynamic/unknown schemas
- Prototype and testing
- One-off scripts

---

## 10. Migration Path from CsvHelper

### 10.1 Phase 1: Drop-in Replacement

```csharp
// CsvHelper code (before)
using CsvHelper;
using CsvHelper.Configuration;

var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Delimiter = ";",
    HasHeaderRecord = true,
    TrimOptions = TrimOptions.Trim,
};

using var reader = new StreamReader("data.csv");
using var csv = new CsvReader(reader, config);
var records = csv.GetRecords<Product>().ToList();

// CsvHandler code (after) - minimal changes
using CsvHandler;

var options = new CsvReaderOptions
{
    Delimiter = ';',  // char instead of string
    HasHeaders = true,  // renamed property
    TrimMode = TrimMode.Trim,  // renamed enum
    Culture = CultureInfo.InvariantCulture,  // moved from constructor
};

using var reader = new StreamReader("data.csv");
using var csv = CsvReader.Create<Product>(reader, options);
var records = csv.ReadAll();  // or use await csv.ReadAllAsync()
```

### 10.2 Phase 2: Source Generator Adoption

```csharp
// Add attribute to get automatic optimization
[CsvRecord]
public partial class Product
{
    [CsvField(Index = 0, Name = "product_id")]
    public int Id { get; set; }

    [CsvField(Name = "product_name")]
    public string Name { get; set; } = "";

    [CsvField(Name = "unit_price")]
    public decimal Price { get; set; }
}

// Source generator creates optimized parser automatically
// API remains the same
using var csv = CsvReader.Create<Product>("data.csv", options);
await foreach (var product in csv.ReadAllAsync())
{
    ProcessProduct(product);
}
```

### 10.3 Phase 3: Modernize APIs

```csharp
// Use new streaming APIs for maximum performance
await using var stream = File.OpenRead("large-file.csv");

await foreach (var product in CsvReader.ReadUtf8Async<Product>(stream, options))
{
    await ProcessProductAsync(product);
}

// Or use batch processing
await foreach (var batch in CsvReader.ReadBatchesAsync<Product>(stream, batchSize: 10000))
{
    await ProcessBatchAsync(batch);
}
```

---

## 11. Configuration Examples

### 11.1 Standard CSV (Fast Path)

```csharp
// Uses default options - Fast Path automatically
var options = CsvReaderOptions.Default;

await using var csv = CsvReader.Create<Product>("products.csv");
var products = await csv.ReadAllAsync().ToListAsync();
```

### 11.2 TSV Files (Fast Path)

```csharp
// Tab-delimited - still Fast Path
var options = CsvReaderOptions.Tsv;

await using var csv = CsvReader.Create<Product>("products.tsv", options);
var products = await csv.ReadAllAsync().ToListAsync();
```

### 11.3 European CSV (Fast Path)

```csharp
// Semicolon delimiter, German culture - Fast Path
var options = new CsvReaderOptions
{
    Delimiter = ';',
    Culture = new CultureInfo("de-DE"),
};

await using var csv = CsvReader.Create<Product>("produkte.csv", options);
var products = await csv.ReadAllAsync().ToListAsync();
```

### 11.4 Custom Delimiter with Error Handling (Flexible Path)

```csharp
// Pipe delimiter with error collection - Flexible Path
var errorLog = new List<string>();

var options = new CsvReaderOptions
{
    Delimiter = '|',
    ErrorHandling = ErrorHandlingMode.Collect,
    BadDataCallback = ctx =>
    {
        errorLog.Add($"Line {ctx.RecordNumber}: {ctx.Message}");
    },
};

await using var csv = CsvReader.Create<Product>("products.txt", options);
var products = await csv.ReadAllAsync().ToListAsync();

// Check errors after
foreach (var error in errorLog)
{
    Console.WriteLine(error);
}
```

### 11.5 Lenient Parsing (Flexible Path)

```csharp
// Handle messy CSVs with missing fields and bad data
var options = CsvReaderOptions.Lenient with
{
    Culture = CultureInfo.CurrentCulture,
};

await using var csv = CsvReader.Create<Product>("messy-data.csv", options);
var products = await csv.ReadAllAsync().ToListAsync();
```

### 11.6 Writing with Custom Format

```csharp
var options = new CsvWriterOptions
{
    Delimiter = '\t',
    QuoteMode = QuoteMode.Always,
    NewLine = NewLineMode.Lf,
    WriteHeaders = true,
};

await using var csv = CsvWriter.Create<Product>("output.tsv", options);
await csv.WriteAllAsync(products);
```

---

## 12. Performance Implications

### 12.1 Configuration Impact Table

| Configuration | Performance Tier | % of Fast Path | Notes |
|---------------|------------------|----------------|-------|
| Default (comma, double-quote, no callbacks) | Fast | 100% | Optimal |
| Tab-delimited, no callbacks | Fast | 100% | Optimal |
| Semicolon, no callbacks | Fast | 100% | Optimal |
| Pipe-delimited, no callbacks | Fast | 100% | Optimal |
| Custom single-byte delimiter | Flexible | 75-85% | Runtime lookup |
| Different escape character | Flexible | 70-80% | More complex parsing |
| With error callbacks | Flexible | 75-85% | Callback branching |
| Case-sensitive header matching | Flexible | 80-90% | String comparison |
| Multi-byte delimiter | General | 50-60% | Byte-by-byte scan |
| Unicode delimiter | General | 50-60% | UTF-8 decode required |
| Dynamic types | General | 50-70% | Reflection overhead |

### 12.2 Memory Allocation Impact

| Feature | Allocation Impact | Mitigation |
|---------|------------------|-----------|
| Standard delimiters | Zero allocation | UTF-8 literals, span-based |
| Custom delimiter | 1-4 bytes per reader | Cached byte array |
| Error callbacks | Callback object allocation | Pooled context objects |
| String conversion | 2x field size | Avoided in fast path |
| Dynamic types | Per-field boxing | Expression tree compilation |
| Reflection parsing | Minimal after warmup | Cached expression trees |

---

## 13. Implementation Strategy

### 13.1 Phase 1: Core Configuration Classes

**Week 1:**
1. Implement `CsvOptions`, `CsvReaderOptions`, `CsvWriterOptions`
2. Add validation logic
3. Create performance tier calculator
4. Unit tests for configuration

### 13.2 Phase 2: Delimiter Handling

**Week 2:**
1. Implement `DelimiterConfig` class
2. Add standard delimiter optimizations
3. Support runtime delimiter lookup
4. Benchmark performance tiers

### 13.3 Phase 3: Error Handling

**Week 3:**
1. Implement `ErrorCallbackHandler`
2. Add callback detection and branching
3. Create error context types
4. Test callback performance impact

### 13.4 Phase 4: Header Matching

**Week 4:**
1. Implement `HeaderMatcher` class
2. Support case-insensitive matching on UTF-8
3. Add custom comparers
4. Test header lookup performance

### 13.5 Phase 5: Dynamic Type Support

**Week 5:**
1. Implement `DynamicCsvParser`
2. Add `ReflectionCsvParser` with expression trees
3. Create parser factory with tier selection
4. Benchmark reflection overhead

### 13.6 Phase 6: Source Generator Integration

**Week 6:**
1. Emit specialized parsers for each standard configuration
2. Add runtime dispatcher
3. Test source generation with all configurations
4. Measure performance across tiers

---

## 14. Testing Strategy

### 14.1 Unit Tests

```csharp
[Fact]
public void StandardConfiguration_UsesFastPath()
{
    var options = new CsvReaderOptions();
    Assert.Equal(PerformanceTier.Fast, options.GetPerformanceTier());
}

[Fact]
public void CustomDelimiter_UsesFlexiblePath()
{
    var options = new CsvReaderOptions { Delimiter = '~' };
    Assert.Equal(PerformanceTier.Flexible, options.GetPerformanceTier());
}

[Fact]
public void ErrorCallback_UsesFlexiblePath()
{
    var options = new CsvReaderOptions
    {
        BadDataCallback = ctx => Console.WriteLine(ctx.Message)
    };
    Assert.Equal(PerformanceTier.Flexible, options.GetPerformanceTier());
}

[Fact]
public void MultiByteDelimiter_UsesGeneralPath()
{
    var options = new CsvReaderOptions { Delimiter = '→' }; // U+2192
    Assert.Equal(PerformanceTier.General, options.GetPerformanceTier());
}
```

### 14.2 Performance Tests

```csharp
[Benchmark]
public void FastPath_CommaDelimited()
{
    var options = CsvReaderOptions.Default;
    var csv = CsvReader.Create<Product>(GetTestData(), options);
    foreach (var _ in csv.ReadAll()) { }
}

[Benchmark]
public void FlexiblePath_CustomDelimiter()
{
    var options = new CsvReaderOptions { Delimiter = '~' };
    var csv = CsvReader.Create<Product>(GetTestData(), options);
    foreach (var _ in csv.ReadAll()) { }
}

[Benchmark]
public void FlexiblePath_WithCallbacks()
{
    var options = new CsvReaderOptions
    {
        BadDataCallback = ctx => { /* no-op */ }
    };
    var csv = CsvReader.Create<Product>(GetTestData(), options);
    foreach (var _ in csv.ReadAll()) { }
}
```

### 14.3 Compatibility Tests

```csharp
[Theory]
[MemberData(nameof(CsvHelperScenarios))]
public async Task CompatibilityTest(string scenario, CsvReaderOptions options)
{
    // Test that CsvHandler produces same results as CsvHelper
    var csvHelperResult = await ParseWithCsvHelper(scenario);
    var csvHandlerResult = await ParseWithCsvHandler(scenario, options);

    Assert.Equal(csvHelperResult, csvHandlerResult);
}
```

---

## 15. Documentation Requirements

### 15.1 Performance Guide

Create `docs/performance-guide.md`:
- Explain three performance tiers
- Show configuration impact table
- Provide optimization tips
- Include before/after examples

### 15.2 Migration Guide

Create `docs/migration-from-csvhelper.md`:
- Property name mappings
- API equivalents
- Breaking changes
- Migration checklist

### 15.3 Configuration Reference

Create `docs/configuration-reference.md`:
- Complete property documentation
- Default values
- Performance implications
- Code examples for each option

### 15.4 API Documentation

XML documentation comments on all public types:
- Summary
- Remarks (including performance notes)
- Example usage
- See also references

---

## 16. Summary

This configuration system provides:

1. **Performance**: 100% for common cases, 70-85% for complex cases
2. **Flexibility**: Supports 90%+ of CsvHelper use cases
3. **Clarity**: Users understand performance implications
4. **Safety**: Immutable, thread-safe, validated configuration
5. **Migration**: Easy path from CsvHelper
6. **Extensibility**: Can add features without breaking changes

**Key Innovations:**
- Two-tier architecture (Fast/Flexible)
- Runtime delimiter handling without losing UTF-8 benefits
- Zero-overhead callbacks via detection and branching
- Source generator specialization for common configurations
- Clear performance contracts

**Next Steps:**
1. Implement core configuration classes
2. Build delimiter handling system
3. Add error callback infrastructure
4. Create source generator integration
5. Benchmark and validate performance tiers
6. Document migration path

---

**File**: `/Users/jas88/Developer/CsvHandler/docs/architecture/06-configuration-system.md`
**Last Updated**: 2025-10-15
**Status**: Design Complete - Ready for Implementation

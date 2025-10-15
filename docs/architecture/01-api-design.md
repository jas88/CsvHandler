# API Design Specification

## Core Reading API

### Basic Usage Pattern

```csharp
// Modern CsvHandler API (source generated)
using CsvHandler;

[CsvRecord]
public partial class Person
{
    [CsvField(Index = 0)]
    public int Id { get; set; }

    [CsvField(Name = "full_name")]
    public string Name { get; set; } = "";

    [CsvField(Index = 2)]
    public DateTime BirthDate { get; set; }

    [CsvField(Name = "email", Optional = true)]
    public string? Email { get; set; }
}

// Reading CSV
await using var reader = CsvReader.Create<Person>("data.csv");
await foreach (var person in reader.ReadAllAsync())
{
    Console.WriteLine($"{person.Name}: {person.Email}");
}

// High-performance variant with manual buffer management
await using var reader = CsvReader.CreateUtf8<Person>(
    File.OpenRead("data.csv"));

await foreach (var person in reader.ReadAllAsync())
{
    // person is pooled and reused - fastest option
    ProcessPerson(person);
}
```

### CsvReader API

```csharp
namespace CsvHandler;

/// <summary>
/// High-performance CSV reader with source generation support
/// </summary>
public sealed class CsvReader<T> : IAsyncDisposable, IDisposable
    where T : class, new()
{
    // Factory methods
    public static CsvReader<T> Create(
        string path,
        CsvReaderOptions? options = null);

    public static CsvReader<T> Create(
        Stream stream,
        CsvReaderOptions? options = null,
        bool leaveOpen = false);

    public static CsvReader<T> CreateUtf8(
        Stream stream,
        CsvReaderOptions? options = null,
        bool leaveOpen = false);

    // Async enumeration (primary API)
    public IAsyncEnumerable<T> ReadAllAsync(
        CancellationToken cancellationToken = default);

    // Single record
    public ValueTask<T?> ReadAsync(
        CancellationToken cancellationToken = default);

    // Batch reading for performance
    public IAsyncEnumerable<ReadOnlyMemory<T>> ReadBatchesAsync(
        int batchSize = 1000,
        CancellationToken cancellationToken = default);

    // Synchronous variants (for compatibility)
    public IEnumerable<T> ReadAll();
    public T? Read();

    // Header access
    public IReadOnlyList<string> Headers { get; }

    // Current position
    public long RecordNumber { get; }
    public long BytePosition { get; }

    // Validation
    public bool ValidateHeaders { get; init; }
    public IReadOnlyList<CsvError> Errors { get; }
}
```

### CsvReaderOptions

```csharp
namespace CsvHandler;

public sealed record CsvReaderOptions
{
    // Delimiters
    public byte Delimiter { get; init; } = (byte)',';
    public byte Quote { get; init; } = (byte)'"';
    public byte Escape { get; init; } = (byte)'"';
    public ReadOnlyMemory<byte> NewLine { get; init; } = "\n"u8.ToArray();

    // Encoding
    public Encoding? Encoding { get; init; } // null = detect/UTF-8
    public bool DetectEncoding { get; init; } = true;

    // Headers
    public bool HasHeaders { get; init; } = true;
    public HeaderMode HeaderMode { get; init; } = HeaderMode.Default;
    public StringComparer HeaderComparer { get; init; } =
        StringComparer.OrdinalIgnoreCase;

    // Parsing
    public bool TrimFields { get; init; } = false;
    public bool AllowComments { get; init; } = false;
    public byte CommentChar { get; init; } = (byte)'#';
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    // Error handling
    public ErrorHandling ErrorHandling { get; init; } =
        ErrorHandling.Throw;
    public int MaxErrors { get; init; } = 100;

    // Performance
    public int BufferSize { get; init; } = 32 * 1024; // 32KB
    public bool PoolRecords { get; init; } = false; // Reuse instances
    public int MaxFieldSize { get; init; } = 1024 * 1024; // 1MB

    // Compatibility
    public BadDataHandling BadDataHandling { get; init; } =
        BadDataHandling.Throw;
    public bool IgnoreBlankLines { get; init; } = true;
    public bool IgnoreQuotes { get; init; } = false;

    // Default instance
    public static CsvReaderOptions Default { get; } = new();
}

public enum HeaderMode
{
    Default,      // Use headers from file
    Skip,         // File has headers but ignore them
    None,         // No headers in file, use indices
    Custom        // Use provided header names
}

public enum ErrorHandling
{
    Throw,        // Throw exception on first error
    Collect,      // Collect errors, continue parsing
    Ignore        // Ignore all errors, skip bad records
}

public enum BadDataHandling
{
    Throw,        // Throw on malformed data
    Skip,         // Skip bad records
    UseDefault    // Use type defaults for bad fields
}
```

## Core Writing API

### Basic Usage Pattern

```csharp
// Writing CSV
var people = GetPeople();

await using var writer = CsvWriter.Create<Person>("output.csv");
await writer.WriteAllAsync(people);

// High-performance streaming write
await using var writer = CsvWriter.CreateUtf8<Person>(
    File.Create("output.csv"));

await foreach (var person in GetPeopleAsync())
{
    await writer.WriteAsync(person);
}

// Manual header control
var options = new CsvWriterOptions
{
    WriteHeaders = true,
    QuoteMode = QuoteMode.WhenNeeded
};

await using var writer = CsvWriter.Create<Person>("output.csv", options);
await writer.WriteHeaderAsync();
await writer.WriteAllAsync(people);
```

### CsvWriter API

```csharp
namespace CsvHandler;

/// <summary>
/// High-performance CSV writer with source generation support
/// </summary>
public sealed class CsvWriter<T> : IAsyncDisposable, IDisposable
    where T : class
{
    // Factory methods
    public static CsvWriter<T> Create(
        string path,
        CsvWriterOptions? options = null);

    public static CsvWriter<T> Create(
        Stream stream,
        CsvWriterOptions? options = null,
        bool leaveOpen = false);

    public static CsvWriter<T> CreateUtf8(
        IBufferWriter<byte> writer,
        CsvWriterOptions? options = null);

    // Single record
    public ValueTask WriteAsync(
        T record,
        CancellationToken cancellationToken = default);

    // Multiple records
    public ValueTask WriteAllAsync(
        IEnumerable<T> records,
        CancellationToken cancellationToken = default);

    public ValueTask WriteAllAsync(
        IAsyncEnumerable<T> records,
        CancellationToken cancellationToken = default);

    // Batch writing
    public ValueTask WriteBatchAsync(
        ReadOnlyMemory<T> batch,
        CancellationToken cancellationToken = default);

    // Header control
    public ValueTask WriteHeaderAsync(
        CancellationToken cancellationToken = default);

    // Synchronous variants
    public void Write(T record);
    public void WriteAll(IEnumerable<T> records);
    public void WriteHeader();

    // Flush control
    public ValueTask FlushAsync(
        CancellationToken cancellationToken = default);

    public void Flush();

    // Statistics
    public long RecordCount { get; }
    public long BytesWritten { get; }
}
```

### CsvWriterOptions

```csharp
namespace CsvHandler;

public sealed record CsvWriterOptions
{
    // Delimiters
    public byte Delimiter { get; init; } = (byte)',';
    public byte Quote { get; init; } = (byte)'"';
    public byte Escape { get; init; } = (byte)'"';
    public ReadOnlyMemory<byte> NewLine { get; init; } = "\n"u8.ToArray();

    // Headers
    public bool WriteHeaders { get; init; } = true;
    public IReadOnlyList<string>? CustomHeaders { get; init; }

    // Formatting
    public QuoteMode QuoteMode { get; init; } = QuoteMode.WhenNeeded;
    public bool TrimFields { get; init; } = false;
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    // Performance
    public int BufferSize { get; init; } = 32 * 1024; // 32KB
    public bool UseBufferPool { get; init; } = true;
    public FlushMode FlushMode { get; init; } = FlushMode.Auto;
    public int AutoFlushInterval { get; init; } = 1000; // records

    // Default instance
    public static CsvWriterOptions Default { get; } = new();
}

public enum QuoteMode
{
    Never,          // Never quote (fast but not RFC 4180)
    WhenNeeded,     // Quote only when necessary (default)
    Always,         // Always quote
    NonNumeric      // Quote non-numeric fields
}

public enum FlushMode
{
    Auto,           // Flush after AutoFlushInterval records
    Manual,         // Only flush on explicit Flush call
    EveryRecord     // Flush after each record (slow but safe)
}
```

## Attribute System

```csharp
namespace CsvHandler;

/// <summary>
/// Mark a class for CSV source generation
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CsvRecordAttribute : Attribute
{
    /// <summary>
    /// Generate only reader, writer, or both
    /// </summary>
    public CsvGenerationMode Mode { get; init; } = CsvGenerationMode.Both;

    /// <summary>
    /// Custom type converter
    /// </summary>
    public Type? ConverterType { get; init; }
}

public enum CsvGenerationMode
{
    Both,
    ReaderOnly,
    WriterOnly
}

/// <summary>
/// Map a property to a CSV field
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class CsvFieldAttribute : Attribute
{
    /// <summary>
    /// Field name in header (null = use property name)
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Field index (takes precedence over name)
    /// </summary>
    public int Index { get; init; } = -1;

    /// <summary>
    /// Whether field is optional
    /// </summary>
    public bool Optional { get; init; } = false;

    /// <summary>
    /// Default value if missing/null
    /// </summary>
    public object? Default { get; init; }

    /// <summary>
    /// Custom format string (dates, numbers, etc.)
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// Custom type converter
    /// </summary>
    public Type? ConverterType { get; init; }

    /// <summary>
    /// Alternative names for this field
    /// </summary>
    public string[]? Aliases { get; init; }
}

/// <summary>
/// Ignore this property
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CsvIgnoreAttribute : Attribute
{
    public CsvIgnoreMode Mode { get; init; } = CsvIgnoreMode.Both;
}

public enum CsvIgnoreMode
{
    Both,
    Read,
    Write
}

/// <summary>
/// Validate field values
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public abstract class CsvValidationAttribute : Attribute
{
    public abstract bool IsValid(object? value, out string? errorMessage);
}

// Common validations
public sealed class CsvRequiredAttribute : CsvValidationAttribute
{
    public override bool IsValid(object? value, out string? errorMessage)
    {
        if (value is null or "")
        {
            errorMessage = "Field is required";
            return false;
        }
        errorMessage = null;
        return true;
    }
}

public sealed class CsvRangeAttribute : CsvValidationAttribute
{
    public CsvRangeAttribute(double min, double max)
    {
        Min = min;
        Max = max;
    }

    public double Min { get; }
    public double Max { get; }

    public override bool IsValid(object? value, out string? errorMessage)
    {
        if (value is IComparable comparable)
        {
            var d = Convert.ToDouble(value);
            if (d < Min || d > Max)
            {
                errorMessage = $"Value must be between {Min} and {Max}";
                return false;
            }
        }
        errorMessage = null;
        return true;
    }
}
```

## Type Converters

```csharp
namespace CsvHandler.Converters;

/// <summary>
/// Base interface for type conversion
/// </summary>
public interface ICsvConverter<T>
{
    /// <summary>
    /// Parse UTF-8 bytes to value
    /// </summary>
    bool TryParse(
        ReadOnlySpan<byte> utf8Text,
        CultureInfo culture,
        out T? value);

    /// <summary>
    /// Format value to UTF-8 bytes
    /// </summary>
    bool TryFormat(
        T? value,
        Span<byte> destination,
        out int bytesWritten,
        CultureInfo culture);

    /// <summary>
    /// Estimate max bytes needed for formatting
    /// </summary>
    int GetMaxByteCount(T? value);
}

/// <summary>
/// Built-in converter registry
/// </summary>
public static class CsvConverters
{
    // Primitives
    public static ICsvConverter<int> Int32 { get; }
    public static ICsvConverter<long> Int64 { get; }
    public static ICsvConverter<double> Double { get; }
    public static ICsvConverter<decimal> Decimal { get; }
    public static ICsvConverter<bool> Boolean { get; }
    public static ICsvConverter<DateTime> DateTime { get; }
    public static ICsvConverter<DateTimeOffset> DateTimeOffset { get; }
    public static ICsvConverter<Guid> Guid { get; }

    // String (special case)
    public static ICsvConverter<string> String { get; }

    // Nullable variants
    public static ICsvConverter<int?> NullableInt32 { get; }
    // ... etc

    // Get converter for type
    public static ICsvConverter<T>? Get<T>();

    // Register custom converter
    public static void Register<T>(ICsvConverter<T> converter);
}

// Example custom converter
public sealed class CustomDateConverter : ICsvConverter<DateTime>
{
    private static readonly byte[] FormatBytes = "yyyy-MM-dd"u8.ToArray();

    public bool TryParse(
        ReadOnlySpan<byte> utf8Text,
        CultureInfo culture,
        out DateTime value)
    {
        return Utf8Parser.TryParse(
            utf8Text,
            out value,
            out _,
            'O');
    }

    public bool TryFormat(
        DateTime value,
        Span<byte> destination,
        out int bytesWritten,
        CultureInfo culture)
    {
        return Utf8Formatter.TryFormat(
            value,
            destination,
            out bytesWritten,
            'O');
    }

    public int GetMaxByteCount(DateTime value) => 64;
}
```

## Usage Examples

### Example 1: Basic Reading

```csharp
[CsvRecord]
public partial class Product
{
    [CsvField(Index = 0)]
    public int Id { get; set; }

    [CsvField(Name = "product_name")]
    public string Name { get; set; } = "";

    [CsvField(Index = 2)]
    public decimal Price { get; set; }

    [CsvField(Name = "in_stock")]
    public bool InStock { get; set; }
}

// Read all products
var products = new List<Product>();
await using var reader = CsvReader.Create<Product>("products.csv");

await foreach (var product in reader.ReadAllAsync())
{
    products.Add(product);
}

// Or use LINQ
var expensiveProducts = await CsvReader
    .Create<Product>("products.csv")
    .ReadAllAsync()
    .Where(p => p.Price > 100)
    .ToListAsync();
```

### Example 2: Custom Configuration

```csharp
var options = new CsvReaderOptions
{
    Delimiter = (byte)';',
    HasHeaders = true,
    TrimFields = true,
    Culture = new CultureInfo("de-DE"),
    ErrorHandling = ErrorHandling.Collect
};

await using var reader = CsvReader.Create<Product>(
    "products.csv",
    options);

await foreach (var product in reader.ReadAllAsync())
{
    ProcessProduct(product);
}

// Check for errors
if (reader.Errors.Any())
{
    foreach (var error in reader.Errors)
    {
        Console.WriteLine(
            $"Line {error.RecordNumber}: {error.Message}");
    }
}
```

### Example 3: High-Performance Streaming

```csharp
await using var stream = File.OpenRead("large_file.csv");
await using var reader = CsvReader.CreateUtf8<Product>(stream);

// Process in batches for better performance
await foreach (var batch in reader.ReadBatchesAsync(batchSize: 10000))
{
    await ProcessBatchAsync(batch);
}
```

### Example 4: Writing CSV

```csharp
var products = GetProducts();

await using var writer = CsvWriter.Create<Product>("output.csv");
await writer.WriteAllAsync(products);

// Or stream records
await using var writer = CsvWriter.Create<Product>("output.csv");

await foreach (var product in GetProductsAsync())
{
    await writer.WriteAsync(product);
}
```

### Example 5: Custom Converters

```csharp
[CsvRecord]
public partial class Order
{
    [CsvField(Index = 0)]
    public int OrderId { get; set; }

    [CsvField(Index = 1, ConverterType = typeof(CustomDateConverter))]
    public DateTime OrderDate { get; set; }

    [CsvField(Index = 2)]
    public decimal Total { get; set; }
}
```

### Example 6: Validation

```csharp
[CsvRecord]
public partial class User
{
    [CsvField(Index = 0)]
    [CsvRequired]
    public int UserId { get; set; }

    [CsvField(Index = 1)]
    [CsvRequired]
    public string Email { get; set; } = "";

    [CsvField(Index = 2)]
    [CsvRange(18, 120)]
    public int Age { get; set; }
}

// Validation happens automatically during reading
await using var reader = CsvReader.Create<User>("users.csv");
await foreach (var user in reader.ReadAllAsync())
{
    // user is already validated
    ProcessUser(user);
}
```

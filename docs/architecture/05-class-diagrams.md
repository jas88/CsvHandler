# Class Diagrams and System Structure

## Core Type Hierarchy

```
┌──────────────────────────────────────────────────────────┐
│                    CsvReader<T>                           │
├──────────────────────────────────────────────────────────┤
│ + Create(path, options): CsvReader<T>                   │
│ + Create(stream, options): CsvReader<T>                 │
│ + ReadAllAsync(): IAsyncEnumerable<T>                   │
│ + ReadAsync(): ValueTask<T?>                            │
│ + ReadBatchesAsync(size): IAsyncEnumerable<Memory<T>>   │
├──────────────────────────────────────────────────────────┤
│ - _stream: Stream                                        │
│ - _reader: BufferedStreamReader                         │
│ - _recordReader: ICsvRecordReader<T>                    │
│ - _options: CsvReaderOptions                            │
│ - _headers: List<string>                                │
│ - _errors: List<CsvError>                               │
└──────────────────────────────────────────────────────────┘
                         │
                         │ uses
                         ↓
┌──────────────────────────────────────────────────────────┐
│              ICsvRecordReader<T>                          │
├──────────────────────────────────────────────────────────┤
│ + Read(fieldReader, options): T                         │
│ + TryRead(fieldReader, options, out T, out error): bool │
│ + GetHeaders(): IReadOnlyList<string>                   │
└──────────────────────────────────────────────────────────┘
                         △
                         │ implements
            ┌────────────┼────────────┐
            │                         │
┌───────────────────────┐   ┌─────────────────────────┐
│  Generated Reader     │   │  ReflectionCsvReader<T> │
│  (Source Generated)   │   │  (Fallback)             │
├───────────────────────┤   ├─────────────────────────┤
│ + Read(): T           │   │ + Read(): T             │
│ + TryRead(): bool     │   │ + TryRead(): bool       │
└───────────────────────┘   └─────────────────────────┘
```

## Writer Hierarchy

```
┌──────────────────────────────────────────────────────────┐
│                    CsvWriter<T>                           │
├──────────────────────────────────────────────────────────┤
│ + Create(path, options): CsvWriter<T>                   │
│ + Create(stream, options): CsvWriter<T>                 │
│ + WriteAsync(record): ValueTask                         │
│ + WriteAllAsync(records): ValueTask                     │
│ + WriteHeaderAsync(): ValueTask                         │
│ + FlushAsync(): ValueTask                               │
├──────────────────────────────────────────────────────────┤
│ - _stream: Stream                                        │
│ - _writer: IBufferWriter<byte>                          │
│ - _recordWriter: ICsvRecordWriter<T>                    │
│ - _options: CsvWriterOptions                            │
│ - _recordCount: long                                     │
└──────────────────────────────────────────────────────────┘
                         │
                         │ uses
                         ↓
┌──────────────────────────────────────────────────────────┐
│              ICsvRecordWriter<T>                          │
├──────────────────────────────────────────────────────────┤
│ + Write(record, fieldWriter, options): void             │
│ + WriteAsync(record, fieldWriter, options): ValueTask   │
│ + GetHeaders(): IReadOnlyList<string>                   │
└──────────────────────────────────────────────────────────┘
                         △
                         │ implements
            ┌────────────┼────────────┐
            │                         │
┌───────────────────────┐   ┌─────────────────────────┐
│  Generated Writer     │   │  ReflectionCsvWriter<T> │
│  (Source Generated)   │   │  (Fallback)             │
├───────────────────────┤   ├─────────────────────────┤
│ + Write(record): void │   │ + Write(record): void   │
└───────────────────────┘   └─────────────────────────┘
```

## Core Processing Layer

```
┌──────────────────────────────────────────────────────────┐
│                  CsvFieldReader                           │
├──────────────────────────────────────────────────────────┤
│ + TryReadInt32(out int): bool                           │
│ + TryReadInt64(out long): bool                          │
│ + TryReadDouble(out double): bool                       │
│ + TryReadDecimal(out decimal): bool                     │
│ + TryReadBoolean(out bool): bool                        │
│ + TryReadDateTime(out DateTime): bool                   │
│ + TryReadDateTime(format, out DateTime): bool           │
│ + TryReadGuid(out Guid): bool                           │
│ + TryReadString(out string): bool                       │
│ + TryReadRaw(out ROS<byte>): bool                       │
├──────────────────────────────────────────────────────────┤
│ - _parser: Utf8CsvParser                                │
│ - _culture: CultureInfo                                 │
│ - _fieldIndex: int                                       │
└──────────────────────────────────────────────────────────┘
                         │
                         │ uses
                         ↓
┌──────────────────────────────────────────────────────────┐
│                  Utf8CsvParser                            │
├──────────────────────────────────────────────────────────┤
│ + TryReadField(out ROS<byte>): bool                     │
│ + IsEndOfRecord: bool                                    │
├──────────────────────────────────────────────────────────┤
│ - TryReadQuotedField(out ROS<byte>): bool               │
│ - TryReadUnquotedField(out ROS<byte>): bool             │
│ - StartsWithNewLine(): bool                             │
├──────────────────────────────────────────────────────────┤
│ - _buffer: ReadOnlySpan<byte>                           │
│ - _position: int                                         │
│ - _delimiter: byte                                       │
│ - _quote: byte                                           │
│ - _escape: byte                                          │
│ - _newLine: ReadOnlySpan<byte>                          │
└──────────────────────────────────────────────────────────┘
```

```
┌──────────────────────────────────────────────────────────┐
│                  CsvFieldWriter                           │
├──────────────────────────────────────────────────────────┤
│ + WriteInt32(int): void                                 │
│ + WriteInt64(long): void                                │
│ + WriteDouble(double): void                             │
│ + WriteDecimal(decimal): void                           │
│ + WriteBoolean(bool): void                              │
│ + WriteDateTime(DateTime): void                         │
│ + WriteDateTime(DateTime, format): void                 │
│ + WriteGuid(Guid): void                                 │
│ + WriteString(string): void                             │
│ + WriteEmpty(): void                                     │
│ + WriteRaw(ROS<byte>): void                             │
│ + EndRecord(): void                                      │
│ + Flush(): void                                          │
├──────────────────────────────────────────────────────────┤
│ - _writer: Utf8CsvWriter                                │
│ - _culture: CultureInfo                                 │
└──────────────────────────────────────────────────────────┘
                         │
                         │ uses
                         ↓
┌──────────────────────────────────────────────────────────┐
│                  Utf8CsvWriter                            │
├──────────────────────────────────────────────────────────┤
│ + WriteField(ROS<byte>): void                           │
│ + EndRecord(): void                                      │
│ + Flush(): void                                          │
├──────────────────────────────────────────────────────────┤
│ - WriteQuotedField(ROS<byte>): void                     │
│ - NeedsQuoting(ROS<byte>): bool                         │
│ - IsNumeric(ROS<byte>): bool                            │
│ - WriteByte(byte): void                                  │
│ - WriteBytes(ROS<byte>): void                           │
│ - EnsureCapacity(int): void                             │
├──────────────────────────────────────────────────────────┤
│ - _writer: IBufferWriter<byte>                          │
│ - _buffer: Span<byte>                                    │
│ - _position: int                                         │
│ - _delimiter: byte                                       │
│ - _quote: byte                                           │
│ - _newLine: ReadOnlySpan<byte>                          │
│ - _quoteMode: QuoteMode                                  │
│ - _firstField: bool                                      │
└──────────────────────────────────────────────────────────┘
```

## Converter System

```
┌──────────────────────────────────────────────────────────┐
│              ICsvConverter<T>                             │
├──────────────────────────────────────────────────────────┤
│ + TryParse(ROS<byte>, culture, out T): bool             │
│ + TryFormat(T, Span<byte>, out written, culture): bool  │
│ + GetMaxByteCount(T): int                               │
└──────────────────────────────────────────────────────────┘
                         △
                         │ implements
            ┌────────────┼────────────────────┐
            │            │                    │
┌───────────────┐  ┌────────────┐  ┌─────────────────┐
│Int32Converter │  │DateConverter│  │CustomConverter  │
├───────────────┤  ├────────────┤  ├─────────────────┤
│+ TryParse()   │  │+ TryParse()│  │+ TryParse()     │
│+ TryFormat()  │  │+ TryFormat()│ │+ TryFormat()    │
└───────────────┘  └────────────┘  └─────────────────┘

┌──────────────────────────────────────────────────────────┐
│                  CsvConverters                            │
│                  (Static Registry)                        │
├──────────────────────────────────────────────────────────┤
│ + Int32: ICsvConverter<int>                             │
│ + Int64: ICsvConverter<long>                            │
│ + Double: ICsvConverter<double>                         │
│ + Decimal: ICsvConverter<decimal>                       │
│ + Boolean: ICsvConverter<bool>                          │
│ + DateTime: ICsvConverter<DateTime>                     │
│ + Guid: ICsvConverter<Guid>                             │
│ + String: ICsvConverter<string>                         │
├──────────────────────────────────────────────────────────┤
│ + Get<T>(): ICsvConverter<T>?                           │
│ + Register<T>(ICsvConverter<T>): void                   │
└──────────────────────────────────────────────────────────┘
```

## Attribute System

```
┌──────────────────────────────────────────────────────────┐
│              CsvRecordAttribute                           │
├──────────────────────────────────────────────────────────┤
│ + Mode: CsvGenerationMode                               │
│ + ConverterType: Type?                                   │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│              CsvFieldAttribute                            │
├──────────────────────────────────────────────────────────┤
│ + Name: string?                                          │
│ + Index: int                                             │
│ + Optional: bool                                         │
│ + Default: object?                                       │
│ + Format: string?                                        │
│ + ConverterType: Type?                                   │
│ + Aliases: string[]?                                     │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│              CsvIgnoreAttribute                           │
├──────────────────────────────────────────────────────────┤
│ + Mode: CsvIgnoreMode                                    │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│         CsvValidationAttribute (abstract)                 │
├──────────────────────────────────────────────────────────┤
│ + IsValid(value, out errorMessage): bool (abstract)     │
└──────────────────────────────────────────────────────────┘
                         △
                         │ inherits
            ┌────────────┼────────────┐
            │                         │
┌───────────────────┐       ┌──────────────────┐
│CsvRequiredAttr    │       │ CsvRangeAttr     │
├───────────────────┤       ├──────────────────┤
│+ IsValid(): bool  │       │+ Min: double     │
└───────────────────┘       │+ Max: double     │
                            │+ IsValid(): bool │
                            └──────────────────┘
```

## Source Generator Architecture

```
┌──────────────────────────────────────────────────────────┐
│            CsvRecordGenerator                             │
│            : IIncrementalGenerator                        │
├──────────────────────────────────────────────────────────┤
│ + Initialize(context): void                             │
├──────────────────────────────────────────────────────────┤
│ - IsCsvRecordCandidate(node, ct): bool                  │
│ - GetSemanticModel(context, ct): RecordInfo?            │
│ - GenerateSource(context, recordInfo): void             │
└──────────────────────────────────────────────────────────┘
                         │
                         │ produces
                         ↓
┌──────────────────────────────────────────────────────────┐
│                 RecordInfo                                │
├──────────────────────────────────────────────────────────┤
│ + Symbol: INamedTypeSymbol                              │
│ + Fields: List<FieldInfo>                               │
│ + Mode: CsvGenerationMode                               │
│ + ConverterType: Type?                                   │
└──────────────────────────────────────────────────────────┘
                         │
                         │ contains
                         ↓
┌──────────────────────────────────────────────────────────┐
│                 FieldInfo                                 │
├──────────────────────────────────────────────────────────┤
│ + Property: IPropertySymbol                             │
│ + Name: string                                           │
│ + Index: int                                             │
│ + Type: ITypeSymbol                                      │
│ + IsOptional: bool                                       │
│ + Format: string?                                        │
│ + ConverterType: Type?                                   │
│ + Aliases: string[]                                      │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│              SourceBuilder                                │
├──────────────────────────────────────────────────────────┤
│ + AppendMetadata(RecordInfo): void                      │
│ + AppendReader(RecordInfo): void                        │
│ + AppendWriter(RecordInfo): void                        │
│ + AppendRegistration(RecordInfo): void                  │
│ + ToString(): string                                     │
└──────────────────────────────────────────────────────────┘
```

## Configuration and Options

```
┌──────────────────────────────────────────────────────────┐
│          CsvReaderOptions (record)                        │
├──────────────────────────────────────────────────────────┤
│ + Delimiter: byte                                        │
│ + Quote: byte                                            │
│ + Escape: byte                                           │
│ + NewLine: ReadOnlyMemory<byte>                         │
│ + Encoding: Encoding?                                    │
│ + DetectEncoding: bool                                   │
│ + HasHeaders: bool                                       │
│ + HeaderMode: HeaderMode                                 │
│ + HeaderComparer: StringComparer                         │
│ + TrimFields: bool                                       │
│ + AllowComments: bool                                    │
│ + CommentChar: byte                                      │
│ + Culture: CultureInfo                                   │
│ + ErrorHandling: ErrorHandling                           │
│ + MaxErrors: int                                         │
│ + BufferSize: int                                        │
│ + PoolRecords: bool                                      │
│ + MaxFieldSize: int                                      │
│ + BadDataHandling: BadDataHandling                       │
│ + IgnoreBlankLines: bool                                 │
│ + IgnoreQuotes: bool                                     │
├──────────────────────────────────────────────────────────┤
│ + Default: CsvReaderOptions {get;}                      │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│          CsvWriterOptions (record)                        │
├──────────────────────────────────────────────────────────┤
│ + Delimiter: byte                                        │
│ + Quote: byte                                            │
│ + Escape: byte                                           │
│ + NewLine: ReadOnlyMemory<byte>                         │
│ + WriteHeaders: bool                                     │
│ + CustomHeaders: IReadOnlyList<string>?                 │
│ + QuoteMode: QuoteMode                                   │
│ + TrimFields: bool                                       │
│ + Culture: CultureInfo                                   │
│ + BufferSize: int                                        │
│ + UseBufferPool: bool                                    │
│ + FlushMode: FlushMode                                   │
│ + AutoFlushInterval: int                                 │
├──────────────────────────────────────────────────────────┤
│ + Default: CsvWriterOptions {get;}                      │
└──────────────────────────────────────────────────────────┘
```

## Complete Usage Flow

```
User Code with [CsvRecord]
         │
         ↓
   Source Generator
    (Compile Time)
         │
         ↓
   Generated Reader/Writer
         │
         ↓
   CsvReader<T>.Create()
         │
         ↓
   BufferedStreamReader
         │
         ↓
   Utf8CsvParser
         │
         ↓
   CsvFieldReader
         │
         ↓
   Generated Read Method
         │
         ↓
   Type Converters
         │
         ↓
   User's T instance
```

## Package Structure

```
CsvHandler/
├── CsvHandler.csproj
│   └── Dependencies:
│       ├── System.Text.Json (ref only)
│       └── System.Memory
│
├── CsvHandler.SourceGeneration.csproj
│   └── Dependencies:
│       ├── Microsoft.CodeAnalysis.CSharp
│       └── CsvHandler (as analyzer)
│
├── CsvHandler.Compatibility.csproj
│   └── Dependencies:
│       ├── CsvHandler
│       └── CsvHelper (peer dependency)
│
└── CsvHandler.Benchmarks.csproj
    └── Dependencies:
        ├── CsvHandler
        ├── CsvHelper
        └── BenchmarkDotNet
```

## Thread Safety

All types follow these thread-safety patterns:

| Type | Thread Safety |
|------|---------------|
| CsvReader<T> | Not thread-safe (instance) |
| CsvWriter<T> | Not thread-safe (instance) |
| CsvReaderOptions | Immutable, thread-safe |
| CsvWriterOptions | Immutable, thread-safe |
| ICsvConverter<T> | Must be thread-safe |
| CsvConverters | Thread-safe (static) |
| Generated code | Thread-safe (static methods) |

Usage pattern:

```csharp
// ✅ Each thread gets own reader
await Parallel.ForEachAsync(files, async (file, ct) =>
{
    await using var reader = CsvReader.Create<Person>(file);
    await foreach (var person in reader.ReadAllAsync(ct))
    {
        ProcessPerson(person);
    }
});

// ❌ Sharing reader across threads
var reader = CsvReader.Create<Person>("file.csv");
await Parallel.ForEachAsync(records, async (record, ct) =>
{
    await reader.ReadAsync(ct); // WRONG: Race condition
});
```

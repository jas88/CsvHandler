# Core Implementation Architecture

## UTF-8 Processing Layer

The core of CsvHandler is built on direct UTF-8 byte processing for maximum performance.

### Utf8CsvParser

Low-level parser that tokenizes CSV data from UTF-8 bytes:

```csharp
namespace CsvHandler.Core;

/// <summary>
/// Low-level UTF-8 CSV parser
/// </summary>
internal ref struct Utf8CsvParser
{
    private ReadOnlySpan<byte> _buffer;
    private int _position;
    private readonly byte _delimiter;
    private readonly byte _quote;
    private readonly byte _escape;
    private readonly ReadOnlySpan<byte> _newLine;

    public Utf8CsvParser(
        ReadOnlySpan<byte> buffer,
        CsvReaderOptions options)
    {
        _buffer = buffer;
        _position = 0;
        _delimiter = options.Delimiter;
        _quote = options.Quote;
        _escape = options.Escape;
        _newLine = options.NewLine.Span;
    }

    /// <summary>
    /// Parse next field, returns false at end of record
    /// </summary>
    public bool TryReadField(out ReadOnlySpan<byte> field)
    {
        field = default;

        if (_position >= _buffer.Length)
            return false;

        // Handle quoted field
        if (_buffer[_position] == _quote)
        {
            return TryReadQuotedField(out field);
        }

        // Handle unquoted field
        return TryReadUnquotedField(out field);
    }

    private bool TryReadQuotedField(out ReadOnlySpan<byte> field)
    {
        _position++; // Skip opening quote
        var start = _position;

        while (_position < _buffer.Length)
        {
            if (_buffer[_position] == _quote)
            {
                // Check for escaped quote
                if (_position + 1 < _buffer.Length &&
                    _buffer[_position + 1] == _quote)
                {
                    _position += 2; // Skip escaped quote
                    continue;
                }

                // End of quoted field
                field = _buffer.Slice(start, _position - start);
                _position++; // Skip closing quote

                // Skip delimiter or newline
                if (_position < _buffer.Length)
                {
                    if (_buffer[_position] == _delimiter)
                    {
                        _position++;
                    }
                    else if (StartsWithNewLine())
                    {
                        _position += _newLine.Length;
                    }
                }

                return true;
            }

            _position++;
        }

        // Unterminated quoted field
        throw new CsvFormatException("Unterminated quoted field");
    }

    private bool TryReadUnquotedField(out ReadOnlySpan<byte> field)
    {
        var start = _position;

        while (_position < _buffer.Length)
        {
            if (_buffer[_position] == _delimiter)
            {
                field = _buffer.Slice(start, _position - start);
                _position++; // Skip delimiter
                return true;
            }

            if (StartsWithNewLine())
            {
                field = _buffer.Slice(start, _position - start);
                _position += _newLine.Length;
                return true;
            }

            _position++;
        }

        // Last field in buffer
        if (start < _buffer.Length)
        {
            field = _buffer.Slice(start);
            _position = _buffer.Length;
            return true;
        }

        field = default;
        return false;
    }

    private bool StartsWithNewLine()
    {
        return _buffer.Slice(_position).StartsWith(_newLine);
    }

    public bool IsEndOfRecord =>
        _position >= _buffer.Length || StartsWithNewLine();
}
```

### CsvFieldReader

High-level field reader with type conversion:

```csharp
namespace CsvHandler.Core;

/// <summary>
/// Reads and converts CSV fields from UTF-8 bytes
/// </summary>
public ref struct CsvFieldReader
{
    private Utf8CsvParser _parser;
    private readonly CultureInfo _culture;
    private int _fieldIndex;

    internal CsvFieldReader(
        ReadOnlySpan<byte> buffer,
        CsvReaderOptions options)
    {
        _parser = new Utf8CsvParser(buffer, options);
        _culture = options.Culture;
        _fieldIndex = 0;
    }

    public long RecordNumber { get; internal set; }

    // Int32
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadInt32(out int value)
    {
        if (!_parser.TryReadField(out var field))
        {
            value = default;
            return false;
        }

        _fieldIndex++;
        return Utf8Parser.TryParse(field, out value, out _);
    }

    // Int64
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadInt64(out long value)
    {
        if (!_parser.TryReadField(out var field))
        {
            value = default;
            return false;
        }

        _fieldIndex++;
        return Utf8Parser.TryParse(field, out value, out _);
    }

    // Double
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadDouble(out double value)
    {
        if (!_parser.TryReadField(out var field))
        {
            value = default;
            return false;
        }

        _fieldIndex++;
        return Utf8Parser.TryParse(field, out value, out _);
    }

    // Decimal
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadDecimal(out decimal value)
    {
        if (!_parser.TryReadField(out var field))
        {
            value = default;
            return false;
        }

        _fieldIndex++;
        return Utf8Parser.TryParse(field, out value, out _);
    }

    // Boolean
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadBoolean(out bool value)
    {
        if (!_parser.TryReadField(out var field))
        {
            value = default;
            return false;
        }

        _fieldIndex++;

        // Support various boolean formats
        if (field.Length == 1)
        {
            var c = field[0];
            if (c is (byte)'1' or (byte)'Y' or (byte)'y' or (byte)'T' or (byte)'t')
            {
                value = true;
                return true;
            }
            if (c is (byte)'0' or (byte)'N' or (byte)'n' or (byte)'F' or (byte)'f')
            {
                value = false;
                return true;
            }
        }

        return Utf8Parser.TryParse(field, out value, out _);
    }

    // DateTime
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadDateTime(out DateTime value)
    {
        if (!_parser.TryReadField(out var field))
        {
            value = default;
            return false;
        }

        _fieldIndex++;
        return Utf8Parser.TryParse(field, out value, out _, 'O');
    }

    // DateTime with format
    public bool TryReadDateTime(
        ReadOnlySpan<byte> format,
        out DateTime value)
    {
        if (!_parser.TryReadField(out var field))
        {
            value = default;
            return false;
        }

        _fieldIndex++;

        // Convert to string for custom format parsing
        // TODO: Optimize with custom UTF-8 parser
        var str = Encoding.UTF8.GetString(field);
        return DateTime.TryParseExact(
            str,
            Encoding.UTF8.GetString(format),
            _culture,
            DateTimeStyles.None,
            out value);
    }

    // Guid
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadGuid(out Guid value)
    {
        if (!_parser.TryReadField(out var field))
        {
            value = default;
            return false;
        }

        _fieldIndex++;
        return Utf8Parser.TryParse(field, out value, out _);
    }

    // String (most complex due to UTF-8 to UTF-16 conversion)
    public bool TryReadString(out string value)
    {
        if (!_parser.TryReadField(out var field))
        {
            value = string.Empty;
            return false;
        }

        _fieldIndex++;

        if (field.IsEmpty)
        {
            value = string.Empty;
            return true;
        }

        // Handle escaped quotes (replace "" with ")
        if (field.IndexOf((byte)'"') >= 0)
        {
            value = UnescapeQuotes(field);
            return true;
        }

        value = Encoding.UTF8.GetString(field);
        return true;
    }

    private static string UnescapeQuotes(ReadOnlySpan<byte> field)
    {
        // Allocate once with exact size needed
        var maxLength = Encoding.UTF8.GetMaxCharCount(field.Length);
        Span<char> buffer = stackalloc char[maxLength];

        var charsWritten = Encoding.UTF8.GetChars(field, buffer);
        var chars = buffer.Slice(0, charsWritten);

        // Replace "" with "
        if (chars.IndexOf("\"\"".AsSpan()) < 0)
        {
            return new string(chars);
        }

        return new string(chars).Replace("\"\"", "\"");
    }

    // Raw bytes (for custom converters)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadRaw(out ReadOnlySpan<byte> value)
    {
        if (!_parser.TryReadField(out value))
        {
            value = default;
            return false;
        }

        _fieldIndex++;
        return true;
    }
}
```

### Utf8CsvWriter

Low-level CSV writer:

```csharp
namespace CsvHandler.Core;

/// <summary>
/// Low-level UTF-8 CSV writer
/// </summary>
internal ref struct Utf8CsvWriter
{
    private readonly IBufferWriter<byte> _writer;
    private Span<byte> _buffer;
    private int _position;
    private readonly byte _delimiter;
    private readonly byte _quote;
    private readonly ReadOnlySpan<byte> _newLine;
    private readonly QuoteMode _quoteMode;
    private bool _firstField;

    public Utf8CsvWriter(
        IBufferWriter<byte> writer,
        CsvWriterOptions options)
    {
        _writer = writer;
        _buffer = writer.GetSpan();
        _position = 0;
        _delimiter = options.Delimiter;
        _quote = options.Quote;
        _newLine = options.NewLine.Span;
        _quoteMode = options.QuoteMode;
        _firstField = true;
    }

    public void WriteField(ReadOnlySpan<byte> field)
    {
        if (!_firstField)
        {
            WriteByte(_delimiter);
        }
        _firstField = false;

        // Determine if quoting needed
        var needsQuoting = _quoteMode switch
        {
            QuoteMode.Always => true,
            QuoteMode.Never => false,
            QuoteMode.WhenNeeded => NeedsQuoting(field),
            QuoteMode.NonNumeric => !IsNumeric(field),
            _ => false
        };

        if (needsQuoting)
        {
            WriteQuotedField(field);
        }
        else
        {
            WriteBytes(field);
        }
    }

    private void WriteQuotedField(ReadOnlySpan<byte> field)
    {
        WriteByte(_quote);

        // Escape quotes by doubling them
        var quotePos = field.IndexOf(_quote);
        if (quotePos < 0)
        {
            WriteBytes(field);
        }
        else
        {
            var remaining = field;
            while (quotePos >= 0)
            {
                WriteBytes(remaining.Slice(0, quotePos + 1));
                WriteByte(_quote); // Double the quote
                remaining = remaining.Slice(quotePos + 1);
                quotePos = remaining.IndexOf(_quote);
            }
            WriteBytes(remaining);
        }

        WriteByte(_quote);
    }

    public void EndRecord()
    {
        WriteBytes(_newLine);
        _firstField = true;
    }

    private void WriteByte(byte b)
    {
        EnsureCapacity(1);
        _buffer[_position++] = b;
    }

    private void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        EnsureCapacity(bytes.Length);
        bytes.CopyTo(_buffer.Slice(_position));
        _position += bytes.Length;
    }

    private void EnsureCapacity(int needed)
    {
        if (_position + needed > _buffer.Length)
        {
            Flush();
            _buffer = _writer.GetSpan(needed);
        }
    }

    public void Flush()
    {
        if (_position > 0)
        {
            _writer.Advance(_position);
            _position = 0;
            _buffer = _writer.GetSpan();
        }
    }

    private bool NeedsQuoting(ReadOnlySpan<byte> field)
    {
        return field.IndexOfAny(_delimiter, _quote, (byte)'\r', (byte)'\n') >= 0;
    }

    private static bool IsNumeric(ReadOnlySpan<byte> field)
    {
        if (field.IsEmpty)
            return false;

        foreach (var b in field)
        {
            if (b is < (byte)'0' or > (byte)'9'
                and not (byte)'.'
                and not (byte)'-'
                and not (byte)'+')
            {
                return false;
            }
        }

        return true;
    }
}
```

### CsvFieldWriter

High-level field writer with type formatting:

```csharp
namespace CsvHandler.Core;

/// <summary>
/// Writes and formats CSV fields to UTF-8 bytes
/// </summary>
public ref struct CsvFieldWriter
{
    private Utf8CsvWriter _writer;
    private readonly CultureInfo _culture;

    internal CsvFieldWriter(
        IBufferWriter<byte> buffer,
        CsvWriterOptions options)
    {
        _writer = new Utf8CsvWriter(buffer, options);
        _culture = options.Culture;
    }

    // Int32
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt32(int value)
    {
        Span<byte> buffer = stackalloc byte[11]; // Max digits for int32
        if (Utf8Formatter.TryFormat(value, buffer, out var written))
        {
            _writer.WriteField(buffer.Slice(0, written));
        }
    }

    // Int64
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt64(long value)
    {
        Span<byte> buffer = stackalloc byte[20]; // Max digits for int64
        if (Utf8Formatter.TryFormat(value, buffer, out var written))
        {
            _writer.WriteField(buffer.Slice(0, written));
        }
    }

    // Double
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDouble(double value)
    {
        Span<byte> buffer = stackalloc byte[32];
        if (Utf8Formatter.TryFormat(value, buffer, out var written, 'G'))
        {
            _writer.WriteField(buffer.Slice(0, written));
        }
    }

    // Decimal
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDecimal(decimal value)
    {
        Span<byte> buffer = stackalloc byte[32];
        if (Utf8Formatter.TryFormat(value, buffer, out var written))
        {
            _writer.WriteField(buffer.Slice(0, written));
        }
    }

    // Boolean
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBoolean(bool value)
    {
        _writer.WriteField(value ? "true"u8 : "false"u8);
    }

    // DateTime
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDateTime(DateTime value)
    {
        Span<byte> buffer = stackalloc byte[64];
        if (Utf8Formatter.TryFormat(value, buffer, out var written, 'O'))
        {
            _writer.WriteField(buffer.Slice(0, written));
        }
    }

    // DateTime with format
    public void WriteDateTime(DateTime value, ReadOnlySpan<byte> format)
    {
        // TODO: Optimize with custom UTF-8 formatter
        var str = value.ToString(
            Encoding.UTF8.GetString(format),
            _culture);

        WriteString(str);
    }

    // Guid
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteGuid(Guid value)
    {
        Span<byte> buffer = stackalloc byte[36];
        if (Utf8Formatter.TryFormat(value, buffer, out var written))
        {
            _writer.WriteField(buffer.Slice(0, written));
        }
    }

    // String
    public void WriteString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            _writer.WriteField(ReadOnlySpan<byte>.Empty);
            return;
        }

        // Estimate UTF-8 byte count
        var maxBytes = Encoding.UTF8.GetMaxByteCount(value.Length);

        if (maxBytes <= 256)
        {
            // Use stack for small strings
            Span<byte> buffer = stackalloc byte[maxBytes];
            var written = Encoding.UTF8.GetBytes(value, buffer);
            _writer.WriteField(buffer.Slice(0, written));
        }
        else
        {
            // Rent from pool for large strings
            var buffer = ArrayPool<byte>.Shared.Rent(maxBytes);
            try
            {
                var written = Encoding.UTF8.GetBytes(value, buffer);
                _writer.WriteField(buffer.AsSpan(0, written));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    // Empty field
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteEmpty()
    {
        _writer.WriteField(ReadOnlySpan<byte>.Empty);
    }

    // Raw bytes
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteRaw(ReadOnlySpan<byte> value)
    {
        _writer.WriteField(value);
    }

    // End record
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EndRecord()
    {
        _writer.EndRecord();
    }

    // Flush
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Flush()
    {
        _writer.Flush();
    }
}
```

## Streaming Infrastructure

### BufferedStreamReader

Efficient buffered reading with UTF-8 support:

```csharp
namespace CsvHandler.Core;

internal sealed class BufferedStreamReader : IDisposable
{
    private readonly Stream _stream;
    private readonly byte[] _buffer;
    private int _position;
    private int _length;
    private bool _isEndOfStream;

    public BufferedStreamReader(Stream stream, int bufferSize)
    {
        _stream = stream;
        _buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        _position = 0;
        _length = 0;
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(
        CancellationToken cancellationToken)
    {
        if (_position >= _length && _isEndOfStream)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        // If buffer is less than half full, compact it
        if (_position > _buffer.Length / 2)
        {
            var remaining = _length - _position;
            _buffer.AsSpan(_position, remaining).CopyTo(_buffer);
            _position = 0;
            _length = remaining;
        }

        // Fill buffer
        var bytesRead = await _stream.ReadAsync(
            _buffer.AsMemory(_length),
            cancellationToken);

        if (bytesRead == 0)
        {
            _isEndOfStream = true;
        }
        else
        {
            _length += bytesRead;
        }

        return _buffer.AsMemory(_position, _length - _position);
    }

    public void Advance(int count)
    {
        _position += count;
    }

    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_buffer);
    }
}
```

## Error Handling

```csharp
namespace CsvHandler;

public class CsvException : Exception
{
    public CsvException(string message) : base(message) { }

    public CsvException(string message, Exception inner)
        : base(message, inner) { }
}

public class CsvFormatException : CsvException
{
    public CsvFormatException(
        long recordNumber,
        int fieldIndex,
        string fieldName,
        string message)
        : base($"Record {recordNumber}, Field {fieldIndex} ({fieldName}): {message}")
    {
        RecordNumber = recordNumber;
        FieldIndex = fieldIndex;
        FieldName = fieldName;
    }

    public long RecordNumber { get; }
    public int FieldIndex { get; }
    public string FieldName { get; }
}

public sealed record CsvError(
    long RecordNumber,
    int FieldIndex,
    string FieldName,
    string Message);
```

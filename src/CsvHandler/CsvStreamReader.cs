using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsvHandler.Compat;

namespace CsvHandler;

/// <summary>
/// Low-level CSV stream reader that handles parsing CSV format into fields.
/// </summary>
internal sealed class CsvStreamReader : IDisposable, IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly CsvOptions _options;
    private readonly bool _leaveOpen;
    private readonly byte[] _buffer;
    private readonly Encoding _encoding;
    private readonly Decoder _decoder;

    private int _bufferPosition;
    private int _bufferLength;
    private long _bytesRead;
    private bool _disposed;
    private bool _firstLineRead; // Track if we've read the first line for BOM handling

    public long BytesRead => _bytesRead;

    public CsvStreamReader(Stream stream, CsvOptions options, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _leaveOpen = leaveOpen;
        _buffer = ArrayPool<byte>.Shared.Rent(options.BufferSize);
        _encoding = options.Encoding ?? Encoding.UTF8;
        _decoder = _encoding.GetDecoder();
    }

    /// <summary>
    /// Reads the next CSV record from the file asynchronously.
    /// A CSV record may span multiple physical lines if it contains quoted newlines.
    /// </summary>
    public async ValueTask<IReadOnlyList<string>?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        var firstLine = await ReadRawLineAsync(cancellationToken).ConfigureAwait(false);

        if (firstLine == null)
            return null;

        // Check if we need to read more lines for multiline quoted fields
        var completeLine = firstLine;
        while (HasUnclosedQuote(completeLine))
        {
            var nextLine = await ReadRawLineAsync(cancellationToken).ConfigureAwait(false);
            if (nextLine == null)
            {
                // File ended with unclosed quote - parse what we have
                break;
            }

            // Append with actual newline to preserve multiline field content
            completeLine += "\n" + nextLine;
        }

        return ParseLine(completeLine);
    }

    /// <summary>
    /// Reads the next CSV record from the file synchronously.
    /// A CSV record may span multiple physical lines if it contains quoted newlines.
    /// </summary>
    public IReadOnlyList<string>? ReadLine()
    {
        var firstLine = ReadRawLine();

        if (firstLine == null)
            return null;

        // Check if we need to read more lines for multiline quoted fields
        var completeLine = firstLine;
        while (HasUnclosedQuote(completeLine))
        {
            var nextLine = ReadRawLine();
            if (nextLine == null)
            {
                // File ended with unclosed quote - parse what we have
                break;
            }

            // Append with actual newline to preserve multiline field content
            completeLine += "\n" + nextLine;
        }

        return ParseLine(completeLine);
    }

    /// <summary>
    /// Check if a line has an unclosed quote (odd number of unescaped quotes).
    /// This is a simplified check - the full parsing logic is in ParseLine.
    /// </summary>
    private bool HasUnclosedQuote(string line)
    {
        bool inQuotes = false;
        bool previousWasEscape = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (previousWasEscape)
            {
                previousWasEscape = false;
                continue;
            }

            if (c == _options.Quote)
            {
                if (!inQuotes)
                {
                    inQuotes = true;
                }
                else
                {
                    // Check for doubled quote
                    if (i + 1 < line.Length && line[i + 1] == _options.Quote)
                    {
                        i++; // Skip the doubled quote
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
            }
            else if (c == _options.Escape && inQuotes && _options.Escape != _options.Quote)
            {
                // Check if this is escaping a quote
                if (i + 1 < line.Length && line[i + 1] == _options.Quote)
                {
                    previousWasEscape = true;
                    continue;
                }
            }
        }

        return inQuotes;
    }

    private async ValueTask<string?> ReadRawLineAsync(CancellationToken cancellationToken)
    {
        var lineBytes = new List<byte>();
        bool foundLine = false;

        while (true)
        {
            // Refill buffer if needed
            if (_bufferPosition >= _bufferLength)
            {
#if NETSTANDARD2_0
                _bufferLength = await _stream.ReadAsync(_buffer, 0, _buffer.Length, cancellationToken).ConfigureAwait(false);
#else
                _bufferLength = await _stream.ReadAsync(_buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
#endif
                _bufferPosition = 0;

                if (_bufferLength == 0)
                {
                    // End of stream
                    if (lineBytes.Count > 0)
                    {
                        var finalLine = _encoding.GetString(lineBytes.ToArray());
                        return StripBomIfFirstLine(finalLine);
                    }

                    return null;
                }

                _bytesRead += _bufferLength;
            }

            // Scan for line ending
            for (; _bufferPosition < _bufferLength; _bufferPosition++)
            {
                var b = _buffer[_bufferPosition];

                if (b == '\n')
                {
                    _bufferPosition++;
                    foundLine = true;
                    break;
                }

                if (b == '\r')
                {
                    _bufferPosition++;

                    // Check for \r\n
                    if (_bufferPosition < _bufferLength && _buffer[_bufferPosition] == '\n')
                        _bufferPosition++;

                    foundLine = true;
                    break;
                }

                lineBytes.Add(b);
            }

            if (foundLine)
                break;
        }

        var result = _encoding.GetString(lineBytes.ToArray());
        result = StripBomIfFirstLine(result);

        // Skip empty lines if configured
        if (_options.SkipEmptyLines && string.IsNullOrWhiteSpace(result))
            return await ReadRawLineAsync(cancellationToken).ConfigureAwait(false);

        // Skip comment lines if configured
        if (_options.CommentCharacter.HasValue && result.TrimStart().StartsWith(_options.CommentCharacter.Value.ToString(), StringComparison.Ordinal))
            return await ReadRawLineAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }

    private string? ReadRawLine()
    {
        var lineBytes = new List<byte>();
        bool foundLine = false;

        while (true)
        {
            // Refill buffer if needed
            if (_bufferPosition >= _bufferLength)
            {
                _bufferLength = _stream.Read(_buffer, 0, _buffer.Length);
                _bufferPosition = 0;

                if (_bufferLength == 0)
                {
                    // End of stream
                    if (lineBytes.Count > 0)
                    {
                        var finalLine = _encoding.GetString(lineBytes.ToArray());
                        return StripBomIfFirstLine(finalLine);
                    }

                    return null;
                }

                _bytesRead += _bufferLength;
            }

            // Scan for line ending
            for (; _bufferPosition < _bufferLength; _bufferPosition++)
            {
                var b = _buffer[_bufferPosition];

                if (b == '\n')
                {
                    _bufferPosition++;
                    foundLine = true;
                    break;
                }

                if (b == '\r')
                {
                    _bufferPosition++;

                    // Check for \r\n
                    if (_bufferPosition < _bufferLength && _buffer[_bufferPosition] == '\n')
                        _bufferPosition++;

                    foundLine = true;
                    break;
                }

                lineBytes.Add(b);
            }

            if (foundLine)
                break;
        }

        var result = _encoding.GetString(lineBytes.ToArray());
        result = StripBomIfFirstLine(result);

        // Skip empty lines if configured
        if (_options.SkipEmptyLines && string.IsNullOrWhiteSpace(result))
            return ReadRawLine();

        // Skip comment lines if configured
        if (_options.CommentCharacter.HasValue && result.TrimStart().StartsWith(_options.CommentCharacter.Value.ToString(), StringComparison.Ordinal))
            return ReadRawLine();

        return result;
    }

    /// <summary>
    /// Strip UTF-8 BOM from the first line if present.
    /// The BOM is the Unicode character U+FEFF.
    /// </summary>
    private string StripBomIfFirstLine(string line)
    {
        if (!_firstLineRead && line.Length > 0 && line[0] == '\uFEFF')
        {
            _firstLineRead = true;
            return line.Substring(1);
        }

        _firstLineRead = true;
        return line;
    }

    private List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;
        bool previousWasEscape = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (previousWasEscape)
            {
                currentField.Append(c);
                previousWasEscape = false;
                continue;
            }

            if (c == _options.Quote)
            {
                if (!inQuotes)
                {
                    // Start of quoted field
                    inQuotes = true;
                }
                else
                {
                    // End of quoted field (or doubled quote)
                    if (i + 1 < line.Length && line[i + 1] == _options.Quote)
                    {
                        // Doubled quote - add single quote
                        currentField.Append(_options.Quote);
                        i++; // Skip next quote
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                continue;
            }

            if (c == _options.Escape && inQuotes && _options.Escape != _options.Quote)
            {
                // Peek next character
                if (i + 1 < line.Length && line[i + 1] == _options.Quote)
                {
                    // Escaped quote
                    currentField.Append(_options.Quote);
                    i++; // Skip next character
                    continue;
                }

                previousWasEscape = true;
                continue;
            }

            if (c == _options.Delimiter && !inQuotes)
            {
                // Field complete
                fields.Add(currentField.ToString());
                currentField.Clear();
                continue;
            }

            currentField.Append(c);
        }

        // Add final field
        fields.Add(currentField.ToString());

        return fields;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        ArrayPool<byte>.Shared.Return(_buffer);

        if (!_leaveOpen)
            _stream.Dispose();

        _disposed = true;
    }

    public
#if NETSTANDARD2_0
        ValueTask
#else
        async ValueTask
#endif
        DisposeAsync()
    {
        if (_disposed)
#if NETSTANDARD2_0
            return default;
#else
            return;
#endif

        ArrayPool<byte>.Shared.Return(_buffer);

        if (!_leaveOpen)
        {
#if NETSTANDARD2_0
            _stream.Dispose();
#else
            await _stream.DisposeAsync().ConfigureAwait(false);
#endif
        }

        _disposed = true;

#if NETSTANDARD2_0
        return default;
#endif
    }
}

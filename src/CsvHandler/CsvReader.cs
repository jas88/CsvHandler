using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHandler;

/// <summary>
/// Provides high-performance CSV reading with dual-path design (source-generated + reflection).
/// </summary>
/// <typeparam name="T">The type to deserialize CSV rows into.</typeparam>
public sealed class CsvReader<T> : IDisposable, IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly CsvOptions _options;
    private readonly ICsvTypeHandler<T>? _typeHandler;
    private readonly bool _leaveOpen;
    private readonly List<CsvError> _errors;

    private CsvStreamReader? _streamReader;
    private long _lineNumber;
    private long _bytesRead;
    private bool _disposed;
    private bool _initialized;

    /// <summary>
    /// Gets the current line number (1-based).
    /// </summary>
    public long LineNumber => _lineNumber;

    /// <summary>
    /// Gets the number of bytes read from the stream.
    /// </summary>
    public long BytesRead => _bytesRead;

    /// <summary>
    /// Gets the read-only list of collected errors (when ErrorHandling is Collect).
    /// </summary>
    public IReadOnlyList<CsvError> Errors => _errors;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvReader{T}"/> class.
    /// </summary>
    private CsvReader(
        Stream stream,
        CsvOptions options,
        ICsvTypeHandler<T>? typeHandler,
        bool leaveOpen)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _typeHandler = typeHandler;
        _leaveOpen = leaveOpen;
        _errors = new List<CsvError>();

        _options.Validate();
    }

    /// <summary>
    /// Creates a CSV reader using source-generated type handler (AOT-safe).
    /// </summary>
    /// <param name="stream">The stream to read CSV data from.</param>
    /// <param name="context">The source-generated CSV context.</param>
    /// <param name="leaveOpen">Whether to leave the stream open after disposal.</param>
    public static CsvReader<T> Create(
        Stream stream,
        CsvContext context,
        bool leaveOpen = false)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(context);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(context);
#endif

        var typeHandler = context.GetTypeHandler<T>();
        return new CsvReader<T>(stream, context.Options ?? CsvOptions.Default, typeHandler, leaveOpen);
    }

    /// <summary>
    /// Creates a CSV reader using source-generated type handler with custom options (AOT-safe).
    /// </summary>
    /// <param name="stream">The stream to read CSV data from.</param>
    /// <param name="context">The source-generated CSV context.</param>
    /// <param name="options">The CSV reading options.</param>
    /// <param name="leaveOpen">Whether to leave the stream open after disposal.</param>
    public static CsvReader<T> Create(
        Stream stream,
        CsvContext context,
        CsvOptions options,
        bool leaveOpen = false)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(context);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(context);
#endif

        var typeHandler = context.GetTypeHandler<T>();
        return new CsvReader<T>(stream, options, typeHandler, leaveOpen);
    }

    /// <summary>
    /// Creates a CSV reader using reflection (not AOT-safe).
    /// </summary>
    /// <param name="stream">The stream to read CSV data from.</param>
    /// <param name="leaveOpen">Whether to leave the stream open after disposal.</param>
    [RequiresUnreferencedCode("Uses reflection to create type handler. Use Create(Stream, CsvContext) for AOT compatibility.")]
    [RequiresDynamicCode("May require dynamic code generation for type conversion. Use Create(Stream, CsvContext) for AOT compatibility.")]
    public static CsvReader<T> Create(
        Stream stream,
        bool leaveOpen = false)
    {
        return Create(stream, CsvOptions.Default, leaveOpen);
    }

    /// <summary>
    /// Creates a CSV reader using reflection with custom options (not AOT-safe).
    /// </summary>
    /// <param name="stream">The stream to read CSV data from.</param>
    /// <param name="options">The CSV reading options.</param>
    /// <param name="leaveOpen">Whether to leave the stream open after disposal.</param>
    [RequiresUnreferencedCode("Uses reflection to create type handler. Use Create(Stream, CsvContext, CsvOptions) for AOT compatibility.")]
    [RequiresDynamicCode("May require dynamic code generation for type conversion. Use Create(Stream, CsvContext, CsvOptions) for AOT compatibility.")]
    public static CsvReader<T> Create(
        Stream stream,
        CsvOptions options,
        bool leaveOpen = false)
    {
        // Use reflection-based type handler factory
        var typeHandler = CsvTypeHandlerFactory.CreateReflectionHandler<T>(options);
        return new CsvReader<T>(stream, options, typeHandler, leaveOpen);
    }

    /// <summary>
    /// Reads all CSV rows asynchronously as a stream.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>An async enumerable of deserialized objects.</returns>
    public async IAsyncEnumerable<T> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            T? item;
            try
            {
                item = await ReadNextInternalAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (HandleError(ex))
            {
                continue; // Skip this row
            }

            if (item == null)
                break;

            yield return item;
        }
    }

    /// <summary>
    /// Reads all CSV rows synchronously.
    /// </summary>
    /// <returns>An enumerable of deserialized objects.</returns>
    [RequiresUnreferencedCode("Synchronous reading uses reflection. Use ReadAllAsync with source-generated context for AOT.")]
    public IEnumerable<T> ReadAll()
    {
        EnsureInitialized();

        while (true)
        {
            T? item;
            try
            {
                item = ReadNextInternal();
            }
            catch (Exception ex) when (HandleError(ex))
            {
                continue; // Skip this row
            }

            if (item == null)
                break;

            yield return item;
        }
    }

    /// <summary>
    /// Reads the next CSV row synchronously.
    /// </summary>
    /// <returns>The deserialized object, or null if end of file.</returns>
    [RequiresUnreferencedCode("Synchronous reading uses reflection. Use ReadAllAsync with source-generated context for AOT.")]
    public T? ReadNext()
    {
        EnsureInitialized();

        try
        {
            return ReadNextInternal();
        }
        catch (Exception ex) when (HandleError(ex))
        {
            return default;
        }
    }

    /// <summary>
    /// Tries to read the next CSV row synchronously.
    /// </summary>
    /// <param name="result">The deserialized object, or default if end of file or error.</param>
    /// <returns>True if a row was read successfully; otherwise, false.</returns>
    [RequiresUnreferencedCode("Synchronous reading uses reflection. Use ReadAllAsync with source-generated context for AOT.")]
    public bool TryReadNext([MaybeNullWhen(false)] out T result)
    {
        result = default;

        try
        {
            EnsureInitialized();
            var item = ReadNextInternal();

            if (item == null)
                return false;

            result = item;
            return true;
        }
        catch (Exception ex)
        {
            if (!HandleError(ex))
                return false;

            // Try next row on skip
            return TryReadNext(out result);
        }
    }

    private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
            return;

        _streamReader = new CsvStreamReader(_stream, _options, leaveOpen: _leaveOpen);

        if (_options.HasHeaders && _typeHandler != null)
        {
            var headers = await _streamReader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (headers != null)
            {
                _typeHandler.SetHeaders(headers);
                _lineNumber++;
            }
        }

        _initialized = true;
    }

    private void EnsureInitialized()
    {
        if (_initialized)
            return;

        _streamReader = new CsvStreamReader(_stream, _options, leaveOpen: _leaveOpen);

        if (_options.HasHeaders && _typeHandler != null)
        {
            var headers = _streamReader.ReadLine();
            if (headers != null)
            {
                _typeHandler.SetHeaders(headers);
                _lineNumber++;
            }
        }

        _initialized = true;
    }

    private async ValueTask<T?> ReadNextInternalAsync(CancellationToken cancellationToken)
    {
        if (_streamReader == null)
            throw new InvalidOperationException("Reader not initialized");

        var fields = await _streamReader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

        if (fields == null)
            return default;

        _lineNumber++;
        _bytesRead = _streamReader.BytesRead;

        if (_typeHandler == null)
            throw new InvalidOperationException("Type handler not available");

        return _typeHandler.Deserialize(fields, _lineNumber);
    }

    private T? ReadNextInternal()
    {
        if (_streamReader == null)
            throw new InvalidOperationException("Reader not initialized");

        var fields = _streamReader.ReadLine();

        if (fields == null)
            return default;

        _lineNumber++;
        _bytesRead = _streamReader.BytesRead;

        if (_typeHandler == null)
            throw new InvalidOperationException("Type handler not available");

        return _typeHandler.Deserialize(fields, _lineNumber);
    }

    private bool HandleError(Exception ex)
    {
        var csvError = ex as CsvException;
        var error = csvError?.ToCsvError() ?? new CsvError(
            _lineNumber,
            0,
            ex.Message,
            CsvErrorType.ParsingError,
            innerException: ex);

        // Invoke callback if provided
        if (_options.OnError != null)
        {
            try
            {
                if (!_options.OnError(error))
                    return false; // Stop processing
            }
            catch
            {
                // Ignore callback errors
            }
        }

        switch (_options.ErrorHandling)
        {
            case CsvErrorHandling.Throw:
                return false; // Let exception propagate

            case CsvErrorHandling.Skip:
                return true; // Continue processing

            case CsvErrorHandling.Collect:
                if (_errors.Count < _options.MaxErrorCount)
                    _errors.Add(error);
                return true; // Continue processing

            default:
                return false;
        }
    }

    /// <summary>
    /// Disposes the reader and optionally the underlying stream.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _streamReader?.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Asynchronously disposes the reader and optionally the underlying stream.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        if (_streamReader != null)
            await _streamReader.DisposeAsync().ConfigureAwait(false);

        _disposed = true;
    }
}

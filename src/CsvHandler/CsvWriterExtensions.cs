using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CsvHandler.Core;

namespace CsvHandler;

/// <summary>
/// Extension methods for convenient CSV writing operations.
/// </summary>
public static class CsvWriterExtensions
{
    /// <summary>
    /// Writes a collection of records to a CSV file asynchronously (uses reflection, not AOT-safe).
    /// </summary>
    /// <typeparam name="T">The type of records to write.</typeparam>
    /// <param name="values">The records to write.</param>
    /// <param name="filePath">The file path to write to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [RequiresUnreferencedCode("Uses reflection. Use WriteCsvAsync(IEnumerable<T>, string, CsvContext) for AOT compatibility.")]
    [RequiresDynamicCode("Uses reflection. Use WriteCsvAsync(IEnumerable<T>, string, CsvContext) for AOT compatibility.")]
    public static async Task WriteCsvAsync<T>(
        this IEnumerable<T> values,
        string filePath,
        CancellationToken cancellationToken = default) where T : class
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(values);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(values);
#endif
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

#if NETSTANDARD2_0
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        using var writer = CsvWriter<T>.Create(stream);
#else
        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await using var writer = CsvWriter<T>.Create(stream);
#endif
        await writer.WriteAllAsync(ToAsyncEnumerable(values), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a collection of records to a CSV file asynchronously with a source-generated context (AOT-safe).
    /// </summary>
    /// <typeparam name="T">The type of records to write.</typeparam>
    /// <param name="values">The records to write.</param>
    /// <param name="filePath">The file path to write to.</param>
    /// <param name="context">The source-generated CSV context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task WriteCsvAsync<T>(
        this IEnumerable<T> values,
        string filePath,
        CsvContext context,
        CancellationToken cancellationToken = default) where T : class
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(context);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(values);
        ArgumentNullExceptionPolyfill.ThrowIfNull(context);
#endif
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

#if NETSTANDARD2_0
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        using var writer = CsvWriter<T>.Create(stream, context);
#else
        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await using var writer = CsvWriter<T>.Create(stream, context);
#endif
        await writer.WriteAllAsync(ToAsyncEnumerable(values), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a collection of records to a stream asynchronously (uses reflection, not AOT-safe).
    /// </summary>
    /// <typeparam name="T">The type of records to write.</typeparam>
    /// <param name="values">The records to write.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [RequiresUnreferencedCode("Uses reflection. Use WriteAsync(Stream, CsvContext) for AOT compatibility.")]
    [RequiresDynamicCode("Uses reflection. Use WriteAsync(Stream, CsvContext) for AOT compatibility.")]
    public static async Task WriteAsync<T>(
        this IEnumerable<T> values,
        Stream stream,
        CancellationToken cancellationToken = default) where T : class
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(stream);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(values);
        ArgumentNullExceptionPolyfill.ThrowIfNull(stream);
#endif

#if NETSTANDARD2_0
        using var writer = CsvWriter<T>.Create(stream, leaveOpen: true);
#else
        await using var writer = CsvWriter<T>.Create(stream, leaveOpen: true);
#endif
        await writer.WriteAllAsync(ToAsyncEnumerable(values), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a collection of records to a stream asynchronously with a source-generated context (AOT-safe).
    /// </summary>
    /// <typeparam name="T">The type of records to write.</typeparam>
    /// <param name="values">The records to write.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="context">The source-generated CSV context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task WriteAsync<T>(
        this IEnumerable<T> values,
        Stream stream,
        CsvContext context,
        CancellationToken cancellationToken = default) where T : class
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(context);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(values);
        ArgumentNullExceptionPolyfill.ThrowIfNull(stream);
        ArgumentNullExceptionPolyfill.ThrowIfNull(context);
#endif

#if NETSTANDARD2_0
        using var writer = CsvWriter<T>.Create(stream, context, leaveOpen: true);
#else
        await using var writer = CsvWriter<T>.Create(stream, context, leaveOpen: true);
#endif
        await writer.WriteAllAsync(ToAsyncEnumerable(values), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a collection of records to a stream asynchronously with custom options (uses reflection, not AOT-safe).
    /// </summary>
    /// <typeparam name="T">The type of records to write.</typeparam>
    /// <param name="values">The records to write.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="options">Writer configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [RequiresUnreferencedCode("Uses reflection. Use WriteAsync(Stream, CsvWriterOptions, CsvContext) for AOT compatibility.")]
    [RequiresDynamicCode("Uses reflection. Use WriteAsync(Stream, CsvWriterOptions, CsvContext) for AOT compatibility.")]
    public static async Task WriteAsync<T>(
        this IEnumerable<T> values,
        Stream stream,
        CsvWriterOptions options,
        CancellationToken cancellationToken = default) where T : class
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(values);
        ArgumentNullExceptionPolyfill.ThrowIfNull(stream);
        ArgumentNullExceptionPolyfill.ThrowIfNull(options);
#endif

#if NETSTANDARD2_0
        using var writer = CsvWriter<T>.Create(stream, options, leaveOpen: true);
#else
        await using var writer = CsvWriter<T>.Create(stream, options, leaveOpen: true);
#endif
        await writer.WriteAllAsync(ToAsyncEnumerable(values), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a collection of records to a stream asynchronously with custom options and context (AOT-safe).
    /// </summary>
    /// <typeparam name="T">The type of records to write.</typeparam>
    /// <param name="values">The records to write.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="options">Writer configuration options.</param>
    /// <param name="context">The source-generated CSV context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task WriteAsync<T>(
        this IEnumerable<T> values,
        Stream stream,
        CsvWriterOptions options,
        CsvContext context,
        CancellationToken cancellationToken = default) where T : class
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(context);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(values);
        ArgumentNullExceptionPolyfill.ThrowIfNull(stream);
        ArgumentNullExceptionPolyfill.ThrowIfNull(options);
        ArgumentNullExceptionPolyfill.ThrowIfNull(context);
#endif

#if NETSTANDARD2_0
        using var writer = CsvWriter<T>.Create(stream, options, context, leaveOpen: true);
#else
        await using var writer = CsvWriter<T>.Create(stream, options, context, leaveOpen: true);
#endif
        await writer.WriteAllAsync(ToAsyncEnumerable(values), cancellationToken).ConfigureAwait(false);
    }

    // Helper method to convert IEnumerable to IAsyncEnumerable
    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.Yield(); // Allow cooperative scheduling
        }
    }
}

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHandler;

/// <summary>
/// Provides extension methods for <see cref="CsvReader{T}"/>.
/// </summary>
public static class CsvReaderExtensions
{
    /// <summary>
    /// Reads all rows and returns them as a list asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of objects to read.</typeparam>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of all rows.</returns>
    public static async Task<List<T>> ToListAsync<T>(
        this CsvReader<T> reader,
        CancellationToken cancellationToken = default)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(reader);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(reader);
#endif

        var list = new List<T>();

        await foreach (var item in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(item);
        }

        return list;
    }

    /// <summary>
    /// Reads all rows and returns them as an array asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of objects to read.</typeparam>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An array of all rows.</returns>
    public static async Task<T[]> ToArrayAsync<T>(
        this CsvReader<T> reader,
        CancellationToken cancellationToken = default)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(reader);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(reader);
#endif

        var list = await ToListAsync(reader, cancellationToken).ConfigureAwait(false);
        return list.ToArray();
    }

    /// <summary>
    /// Returns the first row from the CSV file asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of objects to read.</typeparam>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first row, or default if the file is empty.</returns>
    public static async ValueTask<T?> FirstOrDefaultAsync<T>(
        this CsvReader<T> reader,
        CancellationToken cancellationToken = default)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(reader);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(reader);
#endif

        await foreach (var item in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            return item;
        }

        return default;
    }

    /// <summary>
    /// Returns the first row from the CSV file asynchronously, or throws if empty.
    /// </summary>
    /// <typeparam name="T">The type of objects to read.</typeparam>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first row.</returns>
    /// <exception cref="InvalidOperationException">The CSV file is empty.</exception>
    public static async ValueTask<T> FirstAsync<T>(
        this CsvReader<T> reader,
        CancellationToken cancellationToken = default)
    {
        var result = await FirstOrDefaultAsync(reader, cancellationToken).ConfigureAwait(false);

        if (result == null)
            throw new InvalidOperationException("The CSV file contains no rows.");

        return result;
    }

    /// <summary>
    /// Counts the number of rows in the CSV file asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of objects to read.</typeparam>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of rows.</returns>
    public static async Task<int> CountAsync<T>(
        this CsvReader<T> reader,
        CancellationToken cancellationToken = default)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(reader);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(reader);
#endif

        int count = 0;

        await foreach (var _ in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Counts the number of rows in the CSV file that satisfy a condition asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of objects to read.</typeparam>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="predicate">The condition to test each row.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows that satisfy the condition.</returns>
    public static async Task<int> CountAsync<T>(
        this CsvReader<T> reader,
        Func<T, bool> predicate,
        CancellationToken cancellationToken = default)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(reader);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(reader);
#endif
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(predicate);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(predicate);
#endif

        int count = 0;

        await foreach (var item in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (predicate(item))
                count++;
        }

        return count;
    }

    /// <summary>
    /// Filters rows based on a predicate asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of objects to read.</typeparam>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="predicate">The condition to test each row.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of filtered rows.</returns>
    public static async IAsyncEnumerable<T> WhereAsync<T>(
        this CsvReader<T> reader,
        Func<T, bool> predicate,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(reader);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(reader);
#endif
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(predicate);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(predicate);
#endif

        await foreach (var item in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (predicate(item))
                yield return item;
        }
    }

    /// <summary>
    /// Projects each row to a new form asynchronously.
    /// </summary>
    /// <typeparam name="T">The source type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="selector">A transform function to apply to each row.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of transformed rows.</returns>
    public static async IAsyncEnumerable<TResult> SelectAsync<T, TResult>(
        this CsvReader<T> reader,
        Func<T, TResult> selector,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(reader);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(reader);
#endif
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(selector);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(selector);
#endif

        await foreach (var item in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return selector(item);
        }
    }

    /// <summary>
    /// Skips a specified number of rows asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of objects to read.</typeparam>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="count">The number of rows to skip.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable that skips the specified number of rows.</returns>
    public static async IAsyncEnumerable<T> SkipAsync<T>(
        this CsvReader<T> reader,
        int count,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(reader);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(reader);
#endif
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");

        int skipped = 0;

        await foreach (var item in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (skipped < count)
            {
                skipped++;
                continue;
            }

            yield return item;
        }
    }

    /// <summary>
    /// Takes a specified number of rows asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of objects to read.</typeparam>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="count">The number of rows to take.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of the specified number of rows.</returns>
    public static async IAsyncEnumerable<T> TakeAsync<T>(
        this CsvReader<T> reader,
        int count,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(reader);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(reader);
#endif
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");

        int taken = 0;

        await foreach (var item in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (taken >= count)
                break;

            yield return item;
            taken++;
        }
    }

    /// <summary>
    /// Determines whether any row satisfies a condition asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of objects to read.</typeparam>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="predicate">The condition to test each row.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if any row satisfies the condition; otherwise, false.</returns>
    public static async Task<bool> AnyAsync<T>(
        this CsvReader<T> reader,
        Func<T, bool> predicate,
        CancellationToken cancellationToken = default)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(reader);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(reader);
#endif
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(predicate);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(predicate);
#endif

        await foreach (var item in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (predicate(item))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether all rows satisfy a condition asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of objects to read.</typeparam>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="predicate">The condition to test each row.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if all rows satisfy the condition; otherwise, false.</returns>
    public static async Task<bool> AllAsync<T>(
        this CsvReader<T> reader,
        Func<T, bool> predicate,
        CancellationToken cancellationToken = default)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(reader);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(reader);
#endif
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(predicate);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(predicate);
#endif

        await foreach (var item in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!predicate(item))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Executes an action for each row asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of objects to read.</typeparam>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="action">The action to execute for each row.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task ForEachAsync<T>(
        this CsvReader<T> reader,
        Action<T> action,
        CancellationToken cancellationToken = default)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(reader);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(reader);
#endif
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(action);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(action);
#endif

        await foreach (var item in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            action(item);
        }
    }

    /// <summary>
    /// Executes an async action for each row asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of objects to read.</typeparam>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="action">The async action to execute for each row.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task ForEachAsync<T>(
        this CsvReader<T> reader,
        Func<T, Task> action,
        CancellationToken cancellationToken = default)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(reader);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(reader);
#endif
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(action);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(action);
#endif

        await foreach (var item in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            await action(item).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Converts an async enumerable to a list asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">The async enumerable source.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list containing all elements.</returns>
    public static async Task<List<T>> ToListAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(source);
#endif

        var list = new List<T>();

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            list.Add(item);
        }

        return list;
    }
}

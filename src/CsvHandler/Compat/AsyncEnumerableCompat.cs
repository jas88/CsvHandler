// Copyright (c) 2025 CsvHandler Contributors
// Licensed under the MIT License

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHandler.Compat
{
    /// <summary>
    /// Extension methods for IAsyncEnumerable&lt;T&gt; compatibility.
    /// </summary>
    internal static class AsyncEnumerableCompat
    {
        /// <summary>
        /// Configures how awaits on the tasks returned from an async iteration are performed.
        /// </summary>
        public static ConfiguredCancelableAsyncEnumerable<T> WithCancellation<T>(
            this IAsyncEnumerable<T> source,
            CancellationToken cancellationToken)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return new ConfiguredCancelableAsyncEnumerable<T>(source, cancellationToken, continueOnCapturedContext: true);
        }

        /// <summary>
        /// Configures how awaits on the tasks returned from an async iteration are performed.
        /// </summary>
        public static ConfiguredCancelableAsyncEnumerable<T> ConfigureAwait<T>(
            this IAsyncEnumerable<T> source,
            bool continueOnCapturedContext)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return new ConfiguredCancelableAsyncEnumerable<T>(source, CancellationToken.None, continueOnCapturedContext);
        }

        /// <summary>
        /// Materializes an async enumerable to a list.
        /// </summary>
        public static async Task<List<T>> ToListAsync<T>(
            this IAsyncEnumerable<T> source,
            CancellationToken cancellationToken = default)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var list = new List<T>();

            await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                list.Add(item);
            }

            return list;
        }

        /// <summary>
        /// Materializes an async enumerable to an array.
        /// </summary>
        public static async Task<T[]> ToArrayAsync<T>(
            this IAsyncEnumerable<T> source,
            CancellationToken cancellationToken = default)
        {
            var list = await source.ToListAsync(cancellationToken).ConfigureAwait(false);
            return list.ToArray();
        }

        /// <summary>
        /// Counts the number of elements in an async enumerable.
        /// </summary>
        public static async Task<int> CountAsync<T>(
            this IAsyncEnumerable<T> source,
            CancellationToken cancellationToken = default)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            int count = 0;

            await foreach (var _ in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                count++;
            }

            return count;
        }

        /// <summary>
        /// Returns the first element of an async enumerable.
        /// </summary>
        public static async Task<T> FirstAsync<T>(
            this IAsyncEnumerable<T> source,
            CancellationToken cancellationToken = default)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

            if (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                return enumerator.Current;
            }

            throw new InvalidOperationException("Sequence contains no elements");
        }

        /// <summary>
        /// Returns the first element of an async enumerable, or a default value if empty.
        /// </summary>
        public static async Task<T?> FirstOrDefaultAsync<T>(
            this IAsyncEnumerable<T> source,
            CancellationToken cancellationToken = default)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

            if (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                return enumerator.Current;
            }

            return default;
        }
    }

    /// <summary>
    /// Provides an awaitable async enumerable that enables cancelable iteration and configured awaits.
    /// </summary>
    public readonly struct ConfiguredCancelableAsyncEnumerable<T>
    {
        private readonly IAsyncEnumerable<T> _enumerable;
        private readonly CancellationToken _cancellationToken;
        private readonly bool _continueOnCapturedContext;

        internal ConfiguredCancelableAsyncEnumerable(
            IAsyncEnumerable<T> enumerable,
            CancellationToken cancellationToken,
            bool continueOnCapturedContext)
        {
            _enumerable = enumerable;
            _cancellationToken = cancellationToken;
            _continueOnCapturedContext = continueOnCapturedContext;
        }

        /// <summary>
        /// Configures how awaits on the tasks returned from an async iteration are performed.
        /// </summary>
        public ConfiguredCancelableAsyncEnumerable<T> ConfigureAwait(bool continueOnCapturedContext)
        {
            return new ConfiguredCancelableAsyncEnumerable<T>(
                _enumerable,
                _cancellationToken,
                continueOnCapturedContext);
        }

        /// <summary>
        /// Sets the CancellationToken to be used for iteration.
        /// </summary>
        public ConfiguredCancelableAsyncEnumerable<T> WithCancellation(CancellationToken cancellationToken)
        {
            return new ConfiguredCancelableAsyncEnumerable<T>(
                _enumerable,
                cancellationToken,
                _continueOnCapturedContext);
        }

        /// <summary>
        /// Gets an async enumerator for this enumerable.
        /// </summary>
        public Enumerator GetAsyncEnumerator()
        {
            return new Enumerator(_enumerable.GetAsyncEnumerator(_cancellationToken), _continueOnCapturedContext);
        }

        /// <summary>
        /// Async enumerator wrapper with configured await behavior.
        /// </summary>
        public readonly struct Enumerator : IAsyncDisposable
        {
            private readonly IAsyncEnumerator<T> _enumerator;
            private readonly bool _continueOnCapturedContext;

            internal Enumerator(IAsyncEnumerator<T> enumerator, bool continueOnCapturedContext)
            {
                _enumerator = enumerator;
                _continueOnCapturedContext = continueOnCapturedContext;
            }

            /// <summary>
            /// Gets the current element.
            /// </summary>
            public T Current => _enumerator.Current;

            /// <summary>
            /// Advances the enumerator to the next element.
            /// </summary>
            public ConfiguredValueTaskAwaitable<bool> MoveNextAsync()
            {
                return _enumerator.MoveNextAsync().ConfigureAwait(_continueOnCapturedContext);
            }

            /// <summary>
            /// Disposes the enumerator asynchronously.
            /// </summary>
            public ConfiguredValueTaskAwaitable DisposeAsync()
            {
                return _enumerator.DisposeAsync().ConfigureAwait(_continueOnCapturedContext);
            }
        }
    }
}

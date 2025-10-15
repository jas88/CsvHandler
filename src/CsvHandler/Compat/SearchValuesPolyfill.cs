// Copyright (c) 2025 CsvHandler Contributors
// Licensed under the MIT License

// SearchValues<T> polyfill for net6.0
// SearchValues was introduced in .NET 8.0

#if NET6_0

using System;
using System.Runtime.CompilerServices;

namespace System.Buffers
{
    /// <summary>
    /// Provides an efficient way to search for specific values in spans.
    /// This is a simplified polyfill for net6.0 that lacks the SIMD optimizations of .NET 8.0.
    /// </summary>
    /// <typeparam name="T">The type of values to search for.</typeparam>
    internal sealed class SearchValues<T> where T : IEquatable<T>
    {
        private readonly T[] _values;

        private SearchValues(T[] values)
        {
            _values = values;
        }

        /// <summary>
        /// Creates a SearchValues instance for the specified set of values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SearchValues<T> Create(ReadOnlySpan<T> values)
        {
            T[] array = values.ToArray();
            return new SearchValues<T>(array);
        }

        /// <summary>
        /// Creates a SearchValues instance for the specified set of values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SearchValues<T> Create(T[] values)
        {
            return new SearchValues<T>(values);
        }

        /// <summary>
        /// Searches for any of the values in the specified span.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(T value)
        {
            return Array.IndexOf(_values, value) >= 0;
        }

        /// <summary>
        /// Gets the values being searched for.
        /// </summary>
        internal ReadOnlySpan<T> Values => _values;
    }

    /// <summary>
    /// Extension methods for SearchValues compatibility.
    /// </summary>
    internal static class SearchValuesExtensions
    {
        /// <summary>
        /// Searches for the first index of any value from the SearchValues.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfAny<T>(this ReadOnlySpan<T> span, SearchValues<T> values) where T : IEquatable<T>
        {
            ReadOnlySpan<T> searchValues = values.Values;
            for (int i = 0; i < span.Length; i++)
            {
                for (int j = 0; j < searchValues.Length; j++)
                {
                    if (span[i].Equals(searchValues[j]))
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// Searches for the first index of any value NOT in the SearchValues.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfAnyExcept<T>(this ReadOnlySpan<T> span, SearchValues<T> values) where T : IEquatable<T>
        {
            ReadOnlySpan<T> searchValues = values.Values;
            for (int i = 0; i < span.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < searchValues.Length; j++)
                {
                    if (span[i].Equals(searchValues[j]))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}

#endif

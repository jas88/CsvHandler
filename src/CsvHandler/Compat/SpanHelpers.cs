// Copyright (c) 2025 CsvHandler Contributors
// Licensed under the MIT License

#if NETSTANDARD2_0

using System;
using System.Runtime.CompilerServices;

namespace CsvHandler.Compat;

/// <summary>
    /// Span helper methods for netstandard2.0 compatibility.
    /// These polyfills provide missing APIs that are built-in on modern .NET.
    /// </summary>
    internal static class SpanHelpers
    {
        /// <summary>
        /// Searches for the first occurrence of any of the specified values within the span.
        /// Polyfill for MemoryExtensions.IndexOfAny on netstandard2.0.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfAny(ReadOnlySpan<byte> span, byte value0, byte value1)
        {
            for (int i = 0; i < span.Length; i++)
            {
                byte b = span[i];
                if (b == value0 || b == value1)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Searches for the first occurrence of any of the specified values within the span.
        /// Polyfill for MemoryExtensions.IndexOfAny on netstandard2.0.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfAny(ReadOnlySpan<byte> span, byte value0, byte value1, byte value2)
        {
            for (int i = 0; i < span.Length; i++)
            {
                byte b = span[i];
                if (b == value0 || b == value1 || b == value2)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Searches for the first occurrence of any of the specified values within the span.
        /// Polyfill for MemoryExtensions.IndexOfAny on netstandard2.0.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfAny(ReadOnlySpan<byte> span, byte value0, byte value1, byte value2, byte value3)
        {
            for (int i = 0; i < span.Length; i++)
            {
                byte b = span[i];
                if (b == value0 || b == value1 || b == value2 || b == value3)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Searches for the first occurrence of any of the specified values within the span.
        /// Polyfill for MemoryExtensions.IndexOfAny with ReadOnlySpan&lt;byte&gt; values.
        /// </summary>
        public static int IndexOfAny(ReadOnlySpan<byte> span, ReadOnlySpan<byte> values)
        {
            for (int i = 0; i < span.Length; i++)
            {
                byte b = span[i];
                if (Contains(values, b))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Searches for the last occurrence of the specified value within the span.
        /// Polyfill for MemoryExtensions.LastIndexOf on netstandard2.0.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LastIndexOf(ReadOnlySpan<byte> span, byte value)
        {
            for (int i = span.Length - 1; i >= 0; i--)
            {
                if (span[i] == value)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Determines whether the span contains the specified value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(ReadOnlySpan<byte> span, byte value)
        {
            return span.IndexOf(value) >= 0;
        }

        /// <summary>
        /// Counts the number of occurrences of the specified value in the span.
        /// </summary>
        public static int Count(ReadOnlySpan<byte> span, byte value)
        {
            int count = 0;
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] == value)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Trims whitespace from the start of the span.
        /// Whitespace: space (0x20), tab (0x09), CR (0x0D), LF (0x0A).
        /// </summary>
        public static ReadOnlySpan<byte> TrimStart(ReadOnlySpan<byte> span)
        {
            int start = 0;
            while (start < span.Length && IsWhitespace(span[start]))
                start++;

            return span.Slice(start);
        }

        /// <summary>
        /// Trims whitespace from the end of the span.
        /// Whitespace: space (0x20), tab (0x09), CR (0x0D), LF (0x0A).
        /// </summary>
        public static ReadOnlySpan<byte> TrimEnd(ReadOnlySpan<byte> span)
        {
            int end = span.Length - 1;
            while (end >= 0 && IsWhitespace(span[end]))
                end--;

            return span.Slice(0, end + 1);
        }

        /// <summary>
        /// Trims whitespace from both ends of the span.
        /// Whitespace: space (0x20), tab (0x09), CR (0x0D), LF (0x0A).
        /// </summary>
        public static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> span)
        {
            return TrimEnd(TrimStart(span));
        }

        /// <summary>
        /// Checks if a byte represents whitespace (space, tab, CR, LF).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsWhitespace(byte value)
        {
            return value == 0x20 || value == 0x09 || value == 0x0D || value == 0x0A;
        }

        /// <summary>
        /// Checks if two spans overlap in memory.
        /// </summary>
        public static bool Overlaps<T>(ReadOnlySpan<T> span, ReadOnlySpan<T> other)
        {
            if (span.IsEmpty || other.IsEmpty)
                return false;

            // Check if memory regions overlap
            ref readonly T spanStart = ref span[0];
            ref readonly T spanEnd = ref span[span.Length - 1];
            ref readonly T otherStart = ref other[0];
            ref readonly T otherEnd = ref other[other.Length - 1];

            return !Unsafe.IsAddressLessThan(ref Unsafe.AsRef(in spanEnd), ref Unsafe.AsRef(in otherStart)) &&
                   !Unsafe.IsAddressLessThan(ref Unsafe.AsRef(in otherEnd), ref Unsafe.AsRef(in spanStart));
        }

        /// <summary>
        /// Reverses the elements in the span.
        /// </summary>
        public static void Reverse<T>(Span<T> span)
        {
            int i = 0;
            int j = span.Length - 1;

            while (i < j)
            {
                T temp = span[i];
                span[i] = span[j];
                span[j] = temp;
                i++;
                j--;
            }
        }

        /// <summary>
        /// Fills the span with the specified value.
        /// Polyfill for Span&lt;T&gt;.Fill on netstandard2.0.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Fill<T>(Span<T> span, T value)
        {
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = value;
            }
        }

        /// <summary>
        /// Clears the span (fills with default values).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Clear<T>(Span<T> span)
        {
            span.Fill(default!);
        }

        /// <summary>
        /// Compares two spans for equality using the default equality comparer.
        /// </summary>
        public static bool SequenceEqual<T>(ReadOnlySpan<T> span, ReadOnlySpan<T> other)
            where T : IEquatable<T>
        {
            if (span.Length != other.Length)
                return false;

            for (int i = 0; i < span.Length; i++)
            {
                if (!span[i].Equals(other[i]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Compares two byte spans lexicographically.
        /// Returns: negative if span &lt; other, zero if equal, positive if span &gt; other.
        /// </summary>
        public static int SequenceCompareTo(ReadOnlySpan<byte> span, ReadOnlySpan<byte> other)
        {
            int minLength = Math.Min(span.Length, other.Length);

            for (int i = 0; i < minLength; i++)
            {
                int cmp = span[i].CompareTo(other[i]);
                if (cmp != 0)
                    return cmp;
            }

            return span.Length.CompareTo(other.Length);
        }

        /// <summary>
        /// Determines whether the span starts with the specified prefix.
        /// </summary>
        public static bool StartsWith(ReadOnlySpan<byte> span, ReadOnlySpan<byte> value)
        {
            if (value.Length > span.Length)
                return false;

            return span.Slice(0, value.Length).SequenceEqual(value);
        }

        /// <summary>
        /// Determines whether the span ends with the specified suffix.
        /// </summary>
        public static bool EndsWith(ReadOnlySpan<byte> span, ReadOnlySpan<byte> value)
        {
            if (value.Length > span.Length)
                return false;

            return span.Slice(span.Length - value.Length).SequenceEqual(value);
    }
}

#endif

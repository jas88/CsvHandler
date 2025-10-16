// Copyright (c) 2025 CsvHandler Contributors
// Licensed under the MIT License

// Extension methods to support Range/Index indexing on netstandard2.0

#if NETSTANDARD2_0

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Provides compiler support for Range and Index operations.
    /// The C# compiler generates calls to RuntimeHelpers.GetSubArray for array[range] syntax.
    /// </summary>
    internal static class RuntimeHelpers
    {
        /// <summary>
        /// Gets a subarray using Range indexing.
        /// The C# compiler translates array[range] to RuntimeHelpers.GetSubArray(array, range).
        /// </summary>
        public static T[] GetSubArray<T>(T[] array, Range range)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            var (offset, length) = range.GetOffsetAndLength(array.Length);

            if (length == 0)
            {
                return Array.Empty<T>();
            }

            T[] result = new T[length];
            Array.Copy(array, offset, result, 0, length);
            return result;
        }
    }
}

namespace System
{
    /// <summary>
    /// Extension methods to enable Range indexing on arrays and strings.
    /// </summary>
    public static class RangeExtensions
    {
        /// <summary>
        /// Gets a subarray using a Range.
        /// </summary>
        public static T[] Slice<T>(this T[] array, Range range)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            var (offset, length) = range.GetOffsetAndLength(array.Length);

            if (length == 0)
            {
                return Array.Empty<T>();
            }

            T[] result = new T[length];
            Array.Copy(array, offset, result, 0, length);
            return result;
        }

        /// <summary>
        /// Gets a substring using a Range.
        /// </summary>
        public static string Slice(this string str, Range range)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));

            var (offset, length) = range.GetOffsetAndLength(str.Length);
            return str.Substring(offset, length);
        }

        /// <summary>
        /// Gets a span slice using a Range.
        /// </summary>
        public static Span<T> Slice<T>(this Span<T> span, Range range)
        {
            var (offset, length) = range.GetOffsetAndLength(span.Length);
            return span.Slice(offset, length);
        }

        /// <summary>
        /// Gets a read-only span slice using a Range.
        /// </summary>
        public static ReadOnlySpan<T> Slice<T>(this ReadOnlySpan<T> span, Range range)
        {
            var (offset, length) = range.GetOffsetAndLength(span.Length);
            return span.Slice(offset, length);
        }

        /// <summary>
        /// Gets a memory slice using a Range.
        /// </summary>
        public static Memory<T> Slice<T>(this Memory<T> memory, Range range)
        {
            var (offset, length) = range.GetOffsetAndLength(memory.Length);
            return memory.Slice(offset, length);
        }

        /// <summary>
        /// Gets a read-only memory slice using a Range.
        /// </summary>
        public static ReadOnlyMemory<T> Slice<T>(this ReadOnlyMemory<T> memory, Range range)
        {
            var (offset, length) = range.GetOffsetAndLength(memory.Length);
            return memory.Slice(offset, length);
        }
    }
}

#endif

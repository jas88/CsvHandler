// Copyright (c) 2025 CsvHandler Contributors
// Licensed under the MIT License

using System;
using System.Runtime.CompilerServices;

#if NET8_0_OR_GREATER
using System.Buffers;
#endif

namespace CsvHandler.Compat
{
    /// <summary>
    /// Compatibility layer for modern collection features.
    /// Uses SearchValues on .NET 8+, falls back to manual search on older platforms.
    /// </summary>
    internal static class ModernCollections
    {
#if NET8_0_OR_GREATER
        // .NET 8+: Use highly optimized SearchValues

        private static readonly SearchValues<byte> CommonDelimiters = SearchValues.Create(new byte[]
        {
            (byte)',',  // Comma
            (byte)';',  // Semicolon
            (byte)'\t', // Tab
            (byte)'|',  // Pipe
            (byte)'\n', // Line Feed
            (byte)'\r', // Carriage Return
            (byte)'"'   // Quote
        });

        /// <summary>
        /// Searches for any CSV-related special character (delimiters, quotes, line breaks).
        /// Uses SearchValues for maximum performance on .NET 8+.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfAnyDelimiterOrSpecialChar(ReadOnlySpan<byte> span)
        {
            return span.IndexOfAny(CommonDelimiters);
        }

        /// <summary>
        /// Searches for a specific delimiter or line break characters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfDelimiterOrNewLine(ReadOnlySpan<byte> span, byte delimiter)
        {
            // Create SearchValues for the specific delimiter + newline chars
            var searchValues = SearchValues.Create(stackalloc byte[] { delimiter, (byte)'\n', (byte)'\r' });
            return span.IndexOfAny(searchValues);
        }

        /// <summary>
        /// Searches for quote or line break characters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfQuoteOrNewLine(ReadOnlySpan<byte> span)
        {
            var searchValues = SearchValues.Create(stackalloc byte[] { (byte)'"', (byte)'\n', (byte)'\r' });
            return span.IndexOfAny(searchValues);
        }

#elif NET6_0_OR_GREATER
        // .NET 6-7: Use built-in IndexOfAny (SIMD-optimized but not as fast as SearchValues)

        /// <summary>
        /// Searches for any CSV-related special character (delimiters, quotes, line breaks).
        /// Uses built-in IndexOfAny with SIMD on .NET 6+.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfAnyDelimiterOrSpecialChar(ReadOnlySpan<byte> span)
        {
            // IndexOfAny is SIMD-optimized on .NET 6+ for up to 5 values
            return span.IndexOfAny((byte)',', (byte)';', (byte)'\t', (byte)'|', (byte)'"');
        }

        /// <summary>
        /// Searches for a specific delimiter or line break characters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfDelimiterOrNewLine(ReadOnlySpan<byte> span, byte delimiter)
        {
            return span.IndexOfAny(delimiter, (byte)'\n', (byte)'\r');
        }

        /// <summary>
        /// Searches for quote or line break characters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfQuoteOrNewLine(ReadOnlySpan<byte> span)
        {
            return span.IndexOfAny((byte)'"', (byte)'\n', (byte)'\r');
        }

#else
        // netstandard2.0: Manual search (no SIMD, but still reasonably fast)

        /// <summary>
        /// Searches for any CSV-related special character (delimiters, quotes, line breaks).
        /// Manual implementation for netstandard2.0.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfAnyDelimiterOrSpecialChar(ReadOnlySpan<byte> span)
        {
            return SpanHelpers.IndexOfAny(span, (byte)',', (byte)';', (byte)'\t', (byte)'|');
        }

        /// <summary>
        /// Searches for a specific delimiter or line break characters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfDelimiterOrNewLine(ReadOnlySpan<byte> span, byte delimiter)
        {
            return SpanHelpers.IndexOfAny(span, delimiter, (byte)'\n', (byte)'\r');
        }

        /// <summary>
        /// Searches for quote or line break characters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfQuoteOrNewLine(ReadOnlySpan<byte> span)
        {
            return SpanHelpers.IndexOfAny(span, (byte)'"', (byte)'\n', (byte)'\r');
        }
#endif

        /// <summary>
        /// Searches for the end of a line (LF or CRLF).
        /// Returns the index of the line feed character, or -1 if not found.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfLineEnd(ReadOnlySpan<byte> span)
        {
#if NET6_0_OR_GREATER
            return span.IndexOfAny((byte)'\n', (byte)'\r');
#else
            return SpanHelpers.IndexOfAny(span, (byte)'\n', (byte)'\r');
#endif
        }

        /// <summary>
        /// Checks if the span starts with a BOM (Byte Order Mark).
        /// UTF-8 BOM: 0xEF 0xBB 0xBF
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool StartsWithBom(ReadOnlySpan<byte> span)
        {
            return span.Length >= 3 &&
                   span[0] == 0xEF &&
                   span[1] == 0xBB &&
                   span[2] == 0xBF;
        }

        /// <summary>
        /// Strips the UTF-8 BOM if present at the start of the span.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<byte> StripBom(ReadOnlySpan<byte> span)
        {
            return StartsWithBom(span) ? span.Slice(3) : span;
        }

        /// <summary>
        /// Counts the number of lines in a CSV span (counts line feed characters).
        /// </summary>
        internal static int CountLines(ReadOnlySpan<byte> span)
        {
            int count = 0;
            int position = 0;

            while (position < span.Length)
            {
                int lfIndex = span.Slice(position).IndexOf((byte)'\n');
                if (lfIndex < 0)
                    break;

                count++;
                position += lfIndex + 1;
            }

            // If the span doesn't end with a newline, the last line doesn't have one
            if (span.Length > 0 && span[span.Length - 1] != (byte)'\n')
                count++;

            return count;
        }

        /// <summary>
        /// Determines the line ending style used in the CSV data.
        /// Returns: 0 for LF, 1 for CRLF, -1 for unknown/mixed.
        /// </summary>
        internal static int DetectLineEnding(ReadOnlySpan<byte> span)
        {
            bool hasCRLF = false;
            bool hasLF = false;

            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] == (byte)'\n')
                {
                    if (i > 0 && span[i - 1] == (byte)'\r')
                    {
                        hasCRLF = true;
                    }
                    else
                    {
                        hasLF = true;
                    }

                    // If we've seen both styles, it's mixed
                    if (hasCRLF && hasLF)
                        return -1;
                }
            }

            if (hasCRLF)
                return 1; // CRLF
            if (hasLF)
                return 0; // LF

            return -1; // Unknown
        }
    }
}

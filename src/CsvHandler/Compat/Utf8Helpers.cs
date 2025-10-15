// Copyright (c) 2025 CsvHandler Contributors
// Licensed under the MIT License

using System;
using System.Runtime.CompilerServices;

namespace CsvHandler.Compat
{
    /// <summary>
    /// UTF-8 helper utilities with multi-targeting support.
    /// On net6.0+, uses u8 literals. On netstandard2.0, uses pre-encoded byte arrays.
    /// </summary>
    internal static class Utf8Helpers
    {
#if NET6_0_OR_GREATER
        // Modern: Use UTF-8 string literals (zero-cost, compile-time)

        /// <summary>Comma delimiter (,)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<byte> Comma => ","u8;

        /// <summary>Semicolon delimiter (;)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<byte> Semicolon => ";"u8;

        /// <summary>Tab delimiter (\t)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<byte> Tab => "\t"u8;

        /// <summary>Pipe delimiter (|)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<byte> Pipe => "|"u8;

        /// <summary>Double quote (")</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<byte> Quote => "\""u8;

        /// <summary>Carriage Return + Line Feed (\r\n)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<byte> CRLF => "\r\n"u8;

        /// <summary>Line Feed (\n)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<byte> LF => "\n"u8;

        /// <summary>Carriage Return (\r)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<byte> CR => "\r"u8;

        /// <summary>Boolean true literal</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<byte> True => "true"u8;

        /// <summary>Boolean false literal</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<byte> False => "false"u8;

        /// <summary>Null literal for CSV</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<byte> Null => "null"u8;

        /// <summary>Empty string (zero bytes)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<byte> Empty => ""u8;
#else
        // Legacy: Use pre-encoded byte arrays (small runtime cost)

        private static readonly byte[] CommaBytes = new byte[] { 0x2C };                // ","
        private static readonly byte[] SemicolonBytes = new byte[] { 0x3B };            // ";"
        private static readonly byte[] TabBytes = new byte[] { 0x09 };                  // "\t"
        private static readonly byte[] PipeBytes = new byte[] { 0x7C };                 // "|"
        private static readonly byte[] QuoteBytes = new byte[] { 0x22 };                // "\""
        private static readonly byte[] CRLFBytes = new byte[] { 0x0D, 0x0A };          // "\r\n"
        private static readonly byte[] LFBytes = new byte[] { 0x0A };                   // "\n"
        private static readonly byte[] CRBytes = new byte[] { 0x0D };                   // "\r"
        private static readonly byte[] TrueBytes = new byte[] { 0x74, 0x72, 0x75, 0x65 }; // "true"
        private static readonly byte[] FalseBytes = new byte[] { 0x66, 0x61, 0x6C, 0x73, 0x65 }; // "false"
        private static readonly byte[] NullBytes = new byte[] { 0x6E, 0x75, 0x6C, 0x6C }; // "null"
        private static readonly byte[] EmptyBytes = Array.Empty<byte>();

        /// <summary>Comma delimiter (,)</summary>
        internal static ReadOnlySpan<byte> Comma => CommaBytes;

        /// <summary>Semicolon delimiter (;)</summary>
        internal static ReadOnlySpan<byte> Semicolon => SemicolonBytes;

        /// <summary>Tab delimiter (\t)</summary>
        internal static ReadOnlySpan<byte> Tab => TabBytes;

        /// <summary>Pipe delimiter (|)</summary>
        internal static ReadOnlySpan<byte> Pipe => PipeBytes;

        /// <summary>Double quote (")</summary>
        internal static ReadOnlySpan<byte> Quote => QuoteBytes;

        /// <summary>Carriage Return + Line Feed (\r\n)</summary>
        internal static ReadOnlySpan<byte> CRLF => CRLFBytes;

        /// <summary>Line Feed (\n)</summary>
        internal static ReadOnlySpan<byte> LF => LFBytes;

        /// <summary>Carriage Return (\r)</summary>
        internal static ReadOnlySpan<byte> CR => CRBytes;

        /// <summary>Boolean true literal</summary>
        internal static ReadOnlySpan<byte> True => TrueBytes;

        /// <summary>Boolean false literal</summary>
        internal static ReadOnlySpan<byte> False => FalseBytes;

        /// <summary>Null literal for CSV</summary>
        internal static ReadOnlySpan<byte> Null => NullBytes;

        /// <summary>Empty string (zero bytes)</summary>
        internal static ReadOnlySpan<byte> Empty => EmptyBytes;
#endif

        /// <summary>
        /// Compares two UTF-8 byte sequences for equality (case-sensitive).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        {
            return left.SequenceEqual(right);
        }

        /// <summary>
        /// Compares two UTF-8 byte sequences for equality (case-insensitive ASCII).
        /// Only handles ASCII characters (0x00-0x7F).
        /// </summary>
        internal static bool EqualsIgnoreCaseAscii(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        {
            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                byte l = left[i];
                byte r = right[i];

                // Convert ASCII uppercase to lowercase
                if ((uint)(l - 'A') <= ('Z' - 'A'))
                    l = (byte)(l | 0x20);

                if ((uint)(r - 'A') <= ('Z' - 'A'))
                    r = (byte)(r | 0x20);

                if (l != r)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a byte represents an ASCII whitespace character.
        /// Whitespace: space (0x20), tab (0x09), CR (0x0D), LF (0x0A).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsWhitespace(byte value)
        {
            return value == 0x20 || value == 0x09 || value == 0x0D || value == 0x0A;
        }

        /// <summary>
        /// Checks if a byte represents an ASCII digit (0-9).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsDigit(byte value)
        {
            return (uint)(value - '0') <= 9;
        }

        /// <summary>
        /// Checks if a byte represents an ASCII letter (A-Z, a-z).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsLetter(byte value)
        {
            return ((uint)(value - 'A') <= ('Z' - 'A')) ||
                   ((uint)(value - 'a') <= ('z' - 'a'));
        }

        /// <summary>
        /// Checks if a byte represents an ASCII alphanumeric character (0-9, A-Z, a-z).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsAlphanumeric(byte value)
        {
            return IsDigit(value) || IsLetter(value);
        }
    }
}

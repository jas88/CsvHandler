// Copyright (c) 2025 CsvHandler Contributors
// Licensed under the MIT License

// SearchValues<T> polyfill for net6.0 and net7.0
// SearchValues was introduced in .NET 8.0

#if !NET8_0_OR_GREATER && NET6_0_OR_GREATER

using System;
using System.Runtime.CompilerServices;

namespace System.Buffers;

/// <summary>
/// Non-generic holder class for SearchValues instances.
/// This matches the .NET 8.0 API design.
/// </summary>
/// <typeparam name="T">The type of values to search for.</typeparam>
internal sealed class SearchValues<T> where T : IEquatable<T>
{
    private readonly T[] _values;

    internal SearchValues(T[] values)
    {
        _values = values;
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
/// Static factory class for creating SearchValues instances.
/// This matches the .NET 8.0 API where SearchValues is a non-generic static class.
/// </summary>
internal static class SearchValues
{
    /// <summary>
    /// Creates a SearchValues instance for the specified set of values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SearchValues<T> Create<T>(ReadOnlySpan<T> values) where T : IEquatable<T>
    {
        T[] array = values.ToArray();
        return new SearchValues<T>(array);
    }

    /// <summary>
    /// Creates a SearchValues instance for the specified set of values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SearchValues<T> Create<T>(T[] values) where T : IEquatable<T>
    {
        return new SearchValues<T>(values);
    }
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

#endif

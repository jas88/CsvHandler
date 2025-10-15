// ReSharper disable CheckNamespace

#if !NET5_0_OR_GREATER

namespace System;

/// <summary>
/// Polyfill for HashCode in netstandard2.0.
/// </summary>
internal struct HashCode
{
    private const uint Prime1 = 2654435761U;
    private const uint Prime2 = 2246822519U;
    private const uint Prime3 = 3266489917U;
    private const uint Prime4 = 668265263U;
    private const uint Prime5 = 374761393U;

    private uint _hash;

    public void Add<T>(T value)
    {
        _hash = RotateLeft(_hash + GetHashCodeValue(value) * Prime2, 13) * Prime1;
    }

    public void Add<T>(T value, System.Collections.Generic.IEqualityComparer<T>? comparer)
    {
        uint hashValue = comparer != null ? (uint)comparer.GetHashCode(value) : GetHashCodeValue(value);
        _hash = RotateLeft(_hash + hashValue * Prime2, 13) * Prime1;
    }

    public int ToHashCode()
    {
        uint hash = _hash;
        hash ^= hash >> 15;
        hash *= Prime2;
        hash ^= hash >> 13;
        hash *= Prime3;
        hash ^= hash >> 16;
        return (int)hash;
    }

    private static uint GetHashCodeValue<T>(T value)
    {
        return value == null ? 0 : (uint)value.GetHashCode();
    }

    private static uint RotateLeft(uint value, int count)
    {
        return (value << count) | (value >> (32 - count));
    }
}

#endif

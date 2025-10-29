using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CsvHandler.SourceGenerator.Models;

/// <summary>
/// Represents metadata for a CSV context type decorated with [CsvSerializable] attributes.
/// </summary>
internal readonly struct ContextModel : IEquatable<ContextModel>
{
    /// <summary>
    /// The name of the context type (without namespace).
    /// </summary>
    public string TypeName { get; init; }

    /// <summary>
    /// The fully qualified namespace of the context type.
    /// </summary>
    public string Namespace { get; init; }

    /// <summary>
    /// The type symbol for the context.
    /// </summary>
    public INamedTypeSymbol TypeSymbol { get; init; }

    /// <summary>
    /// Collection of record types registered via [CsvSerializable] attributes.
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> RegisteredTypes { get; init; }

    /// <summary>
    /// Whether the type is declared as partial.
    /// </summary>
    public bool IsPartial { get; init; }

    /// <summary>
    /// Location for diagnostic reporting.
    /// </summary>
    public Location Location { get; init; }

    /// <summary>
    /// Gets the fully qualified type name.
    /// </summary>
    public string GetFullyQualifiedName()
    {
        return string.IsNullOrEmpty(Namespace)
            ? TypeName
            : $"{Namespace}.{TypeName}";
    }

    public bool Equals(ContextModel other)
    {
        return TypeName == other.TypeName &&
               Namespace == other.Namespace &&
               SymbolEqualityComparer.Default.Equals(TypeSymbol, other.TypeSymbol) &&
               TypesEqual(RegisteredTypes, other.RegisteredTypes) &&
               IsPartial == other.IsPartial;
    }

    private static bool TypesEqual(ImmutableArray<INamedTypeSymbol> left, ImmutableArray<INamedTypeSymbol> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is ContextModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TypeName);
        hash.Add(Namespace);
        hash.Add(TypeSymbol, SymbolEqualityComparer.Default);
        hash.Add(IsPartial);

        foreach (var type in RegisteredTypes)
        {
            hash.Add(type, SymbolEqualityComparer.Default);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(ContextModel left, ContextModel right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ContextModel left, ContextModel right)
    {
        return !left.Equals(right);
    }
}

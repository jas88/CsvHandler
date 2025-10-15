using System;
using Microsoft.CodeAnalysis;

namespace CsvHandler.SourceGenerator.Models;

/// <summary>
/// Represents metadata for a single CSV field within a record.
/// </summary>
/// <remarks>
/// This is an equatable struct optimized for incremental generator caching.
/// All reference types use SymbolEqualityComparer for proper equality checks.
/// </remarks>
internal readonly struct FieldModel : IEquatable<FieldModel>
{
    /// <summary>
    /// The name of the field or property in the C# type.
    /// </summary>
    public string MemberName { get; init; }

    /// <summary>
    /// The CSV column name (may differ from member name).
    /// </summary>
    public string CsvName { get; init; }

    /// <summary>
    /// The type symbol of the field.
    /// </summary>
    public ITypeSymbol FieldType { get; init; }

    /// <summary>
    /// The order/index of this field in the CSV (0-based).
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Format string for serialization (e.g., date format).
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// Whether this field is nullable.
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Custom converter type symbol, if specified.
    /// </summary>
    public INamedTypeSymbol? ConverterType { get; init; }

    /// <summary>
    /// Whether this is a property (true) or field (false).
    /// </summary>
    public bool IsProperty { get; init; }

    /// <summary>
    /// Location for diagnostic reporting.
    /// </summary>
    public Location Location { get; init; }

    /// <summary>
    /// Gets the fully qualified type name for code generation.
    /// </summary>
    public string GetFullyQualifiedTypeName()
    {
        return FieldType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    /// <summary>
    /// Gets the minimal type name (without namespace) for code generation.
    /// </summary>
    public string GetMinimalTypeName()
    {
        return FieldType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
    }

    /// <summary>
    /// Checks if this field type is a primitive or well-known type.
    /// </summary>
    public bool IsWellKnownType()
    {
        var specialType = FieldType.SpecialType;

        return specialType switch
        {
            SpecialType.System_Boolean => true,
            SpecialType.System_Byte => true,
            SpecialType.System_SByte => true,
            SpecialType.System_Int16 => true,
            SpecialType.System_UInt16 => true,
            SpecialType.System_Int32 => true,
            SpecialType.System_UInt32 => true,
            SpecialType.System_Int64 => true,
            SpecialType.System_UInt64 => true,
            SpecialType.System_Single => true,
            SpecialType.System_Double => true,
            SpecialType.System_Decimal => true,
            SpecialType.System_Char => true,
            SpecialType.System_String => true,
            SpecialType.System_DateTime => true,
            _ => IsWellKnownNonPrimitiveType()
        };
    }

    private bool IsWellKnownNonPrimitiveType()
    {
        var typeString = FieldType.ToDisplayString();
        return typeString is "System.DateTimeOffset" or "System.Guid" or "System.TimeSpan";
    }

    public bool Equals(FieldModel other)
    {
        return MemberName == other.MemberName &&
               CsvName == other.CsvName &&
               SymbolEqualityComparer.Default.Equals(FieldType, other.FieldType) &&
               Order == other.Order &&
               Format == other.Format &&
               IsNullable == other.IsNullable &&
               SymbolEqualityComparer.Default.Equals(ConverterType, other.ConverterType) &&
               IsProperty == other.IsProperty;
    }

    public override bool Equals(object? obj)
    {
        return obj is FieldModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(MemberName);
        hash.Add(CsvName);
        hash.Add(FieldType, SymbolEqualityComparer.Default);
        hash.Add(Order);
        hash.Add(Format);
        hash.Add(IsNullable);
        hash.Add(ConverterType, SymbolEqualityComparer.Default);
        hash.Add(IsProperty);
        return hash.ToHashCode();
    }

    public static bool operator ==(FieldModel left, FieldModel right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(FieldModel left, FieldModel right)
    {
        return !left.Equals(right);
    }
}

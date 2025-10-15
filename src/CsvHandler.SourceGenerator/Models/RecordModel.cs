using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CsvHandler.SourceGenerator.Models;

/// <summary>
/// Represents metadata for a CSV record type.
/// </summary>
/// <remarks>
/// This is an equatable struct optimized for incremental generator caching.
/// Uses ImmutableArray with custom equality for field collections.
/// </remarks>
internal readonly struct RecordModel : IEquatable<RecordModel>
{
    /// <summary>
    /// The name of the type (without namespace).
    /// </summary>
    public string TypeName { get; init; }

    /// <summary>
    /// The fully qualified namespace of the type.
    /// </summary>
    public string Namespace { get; init; }

    /// <summary>
    /// The type symbol for the record.
    /// </summary>
    public INamedTypeSymbol TypeSymbol { get; init; }

    /// <summary>
    /// Collection of fields in this record.
    /// </summary>
    public ImmutableArray<FieldModel> Fields { get; init; }

    /// <summary>
    /// Whether the type is declared as partial.
    /// </summary>
    public bool IsPartial { get; init; }

    /// <summary>
    /// Whether the type is a record type.
    /// </summary>
    public bool IsRecord { get; init; }

    /// <summary>
    /// The delimiter character configured in [CsvRecord].
    /// </summary>
    public char Delimiter { get; init; }

    /// <summary>
    /// Whether to include a header row.
    /// </summary>
    public bool HasHeader { get; init; }

    /// <summary>
    /// Whether to use strict mode (throw on format errors).
    /// </summary>
    public bool StrictMode { get; init; }

    /// <summary>
    /// The culture name for parsing (e.g., "en-US").
    /// </summary>
    public string? CultureName { get; init; }

    /// <summary>
    /// Whether to trim whitespace from fields.
    /// </summary>
    public bool TrimWhitespace { get; init; }

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

    /// <summary>
    /// Gets required namespace usings for generated code.
    /// </summary>
    public ImmutableArray<string> GetRequiredUsings()
    {
        var usings = ImmutableArray.CreateBuilder<string>();

        usings.Add("System");
        usings.Add("System.Buffers");
        usings.Add("System.Text");

        // Check if we need additional namespaces based on field types
        foreach (var field in Fields)
        {
            var typeString = field.FieldType.ToDisplayString();

            if (typeString.Contains("DateTimeOffset"))
            {
                usings.Add("System");
            }

            if (typeString.Contains("Guid"))
            {
                usings.Add("System");
            }

            if (field.ConverterType != null)
            {
                var converterNamespace = field.ConverterType.ContainingNamespace.ToDisplayString();
                if (!string.IsNullOrEmpty(converterNamespace))
                {
                    usings.Add(converterNamespace);
                }
            }
        }

        return usings.Distinct().OrderBy(u => u).ToImmutableArray();
    }

    public bool Equals(RecordModel other)
    {
        return TypeName == other.TypeName &&
               Namespace == other.Namespace &&
               SymbolEqualityComparer.Default.Equals(TypeSymbol, other.TypeSymbol) &&
               FieldsEqual(Fields, other.Fields) &&
               IsPartial == other.IsPartial &&
               IsRecord == other.IsRecord &&
               Delimiter == other.Delimiter &&
               HasHeader == other.HasHeader &&
               StrictMode == other.StrictMode &&
               CultureName == other.CultureName &&
               TrimWhitespace == other.TrimWhitespace;
    }

    private static bool FieldsEqual(ImmutableArray<FieldModel> left, ImmutableArray<FieldModel> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (!left[i].Equals(right[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is RecordModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TypeName);
        hash.Add(Namespace);
        hash.Add(TypeSymbol, SymbolEqualityComparer.Default);
        hash.Add(IsPartial);
        hash.Add(IsRecord);
        hash.Add(Delimiter);
        hash.Add(HasHeader);
        hash.Add(StrictMode);
        hash.Add(CultureName);
        hash.Add(TrimWhitespace);

        // Hash field collection
        foreach (var field in Fields)
        {
            hash.Add(field);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(RecordModel left, RecordModel right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(RecordModel left, RecordModel right)
    {
        return !left.Equals(right);
    }
}

using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CsvHandler.SourceGenerator.Models;
using CsvHandler.SourceGenerator.Helpers;

namespace CsvHandler.SourceGenerator;

/// <summary>
/// Incremental source generator for CSV record types.
/// </summary>
/// <remarks>
/// This generator uses ForAttributeWithMetadataName for optimal performance (99x faster than syntax walking).
/// It generates partial class implementations with CSV reading/writing capabilities.
/// </remarks>
[Generator]
public sealed class CsvSourceGenerator : IIncrementalGenerator
{
    private const string CsvRecordAttributeName = "CsvHandler.Attributes.CsvRecordAttribute";
    private const string CsvFieldAttributeName = "CsvHandler.Attributes.CsvFieldAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Note: We don't generate attribute stubs because the real attributes exist in CsvHandler.Attributes namespace

        // Use ForAttributeWithMetadataName for high performance
        var recordTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: CsvRecordAttributeName,
                predicate: static (node, _) => IsCandidateSyntax(node),
                transform: static (context, cancellationToken) => TransformRecord(context, cancellationToken))
            .Where(static model => model.HasValue)
            .Select(static (model, _) => model!.Value);

        // Register source output for each record type
        context.RegisterSourceOutput(recordTypes, GenerateSource);
    }

    /// <summary>
    /// Fast predicate to filter candidate syntax nodes.
    /// </summary>
    private static bool IsCandidateSyntax(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax or RecordDeclarationSyntax;
    }

    /// <summary>
    /// Transforms semantic model into our cached RecordModel structure.
    /// </summary>
    private static RecordModel? TransformRecord(GeneratorAttributeSyntaxContext context, System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        // Note: Diagnostics reporting moved to GenerateSource phase
        // because GeneratorAttributeSyntaxContext doesn't support ReportDiagnostic

        // Validate type is a class or record
        if (typeSymbol.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return null;
        }

        // Check if type is partial
        bool isPartial = IsPartial(context.TargetNode);
        if (!isPartial)
        {
            return null;
        }

        // Check if type is nested
        if (typeSymbol.ContainingType != null)
        {
            return null;
        }

        // Extract CsvRecord attribute parameters
        var recordAttribute = context.Attributes.First();
        char delimiter = GetAttributeValue(recordAttribute, "Delimiter", ',');
        bool hasHeader = GetAttributeValue(recordAttribute, "HasHeaders", true);
        bool strictMode = GetAttributeValue(recordAttribute, "StrictMode", false);
        string? cultureName = GetAttributeValue<string?>(recordAttribute, "CultureName", null);
        bool trimWhitespace = GetAttributeValue(recordAttribute, "TrimWhitespace", true);

        // Extract fields
        var fields = ExtractFields(typeSymbol, cancellationToken);

        // Validation will happen in GenerateSource phase

        return new RecordModel
        {
            TypeName = typeSymbol.Name,
            Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
            TypeSymbol = typeSymbol,
            Fields = fields,
            IsPartial = isPartial,
            IsRecord = context.TargetNode is RecordDeclarationSyntax,
            Delimiter = delimiter,
            HasHeader = hasHeader,
            StrictMode = strictMode,
            CultureName = cultureName,
            TrimWhitespace = trimWhitespace,
            Location = context.TargetNode.GetLocation()
        };
    }

    /// <summary>
    /// Extracts field metadata from type members.
    /// </summary>
    private static ImmutableArray<FieldModel> ExtractFields(
        INamedTypeSymbol typeSymbol,
        System.Threading.CancellationToken cancellationToken)
    {
        var fields = ImmutableArray.CreateBuilder<FieldModel>();

        foreach (var member in typeSymbol.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();

            ITypeSymbol? memberType = null;
            bool isProperty = false;
            Location? location = null;

            if (member is IFieldSymbol fieldSymbol)
            {
                memberType = fieldSymbol.Type;
                isProperty = false;
                location = fieldSymbol.Locations.FirstOrDefault();
            }
            else if (member is IPropertySymbol propertySymbol)
            {
                memberType = propertySymbol.Type;
                isProperty = true;
                location = propertySymbol.Locations.FirstOrDefault();
            }
            else
            {
                continue;
            }

            // Find CsvField attribute
            var csvFieldAttr = member.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == CsvFieldAttributeName);

            if (csvFieldAttr == null)
            {
                continue;
            }

            // Extract attribute values
            int order = GetAttributeValue(csvFieldAttr, "Order", -1);
            string? csvName = GetAttributeValue<string?>(csvFieldAttr, "Name", null) ?? member.Name;
            string? format = GetAttributeValue<string?>(csvFieldAttr, "Format", null);
            var converterType = GetAttributeValue<INamedTypeSymbol?>(csvFieldAttr, "ConverterType", null);

            // Validate field type
            bool isNullable = memberType.NullableAnnotation == NullableAnnotation.Annotated;
            if (!IsValidCsvType(memberType))
            {
                continue;
            }

            fields.Add(new FieldModel
            {
                MemberName = member.Name,
                CsvName = csvName,
                FieldType = memberType,
                Order = order,
                Format = format,
                IsNullable = isNullable,
                ConverterType = converterType,
                IsProperty = isProperty,
                Location = location ?? Location.None
            });
        }

        // Sort by order
        return fields.OrderBy(f => f.Order).ToImmutableArray();
    }

    /// <summary>
    /// Checks if a type is supported for CSV serialization.
    /// </summary>
    private static bool IsValidCsvType(ITypeSymbol type)
    {
        // Handle nullable types
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullableType)
        {
            type = nullableType.TypeArguments[0];
        }

        return type.SpecialType switch
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
            _ => IsWellKnownType(type)
        };
    }

    private static bool IsWellKnownType(ITypeSymbol type)
    {
        var typeString = type.ToDisplayString();
        return typeString is "System.DateTimeOffset" or "System.Guid" or "System.TimeSpan";
    }

    /// <summary>
    /// Checks if a syntax node has partial modifier.
    /// </summary>
    private static bool IsPartial(SyntaxNode node)
    {
        return node switch
        {
            ClassDeclarationSyntax classDecl => classDecl.Modifiers.Any(m => m.Text == "partial"),
            RecordDeclarationSyntax recordDecl => recordDecl.Modifiers.Any(m => m.Text == "partial"),
            _ => false
        };
    }

    /// <summary>
    /// Gets attribute constructor or named parameter value.
    /// </summary>
    private static T GetAttributeValue<T>(AttributeData attribute, string parameterName, T defaultValue)
    {
        // Check named arguments first
        var namedArg = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == parameterName);
        if (!namedArg.Equals(default))
        {
            return namedArg.Value.Value is T value ? value : defaultValue;
        }

        return defaultValue;
    }

    /// <summary>
    /// Generates the source code for a record model.
    /// </summary>
    private static void GenerateSource(SourceProductionContext context, RecordModel model)
    {
        // Validate model and report diagnostics
        if (!ValidateModel(context, model))
        {
            return;
        }

        var code = new CodeBuilder();

        // Build required usings list
        var usings = model.GetRequiredUsings().ToList();
        usings.Add("System.Buffers.Text");
        usings.Add("System.Collections.Generic");
        usings.Add("System.Globalization");
        usings.Add("System.IO");
        usings.Add("System.Runtime.CompilerServices");
        usings.Add("System.Threading");
        usings.Add("System.Threading.Tasks");
        usings.Add("CsvHandler.Core");

        // Generate usings and namespace
        code.AppendNamespace(model.Namespace, usings.Distinct());

        // Generate type declaration
        code.AppendXmlDoc($"Auto-generated CSV handler for {model.TypeName}.");
        code.AppendTypeDeclaration(model.TypeName, model.IsRecord, isPartial: true);

        // Generate reader methods
        CodeEmitter.EmitReader(model, code);
        CodeEmitter.EmitAsyncReader(model, code);

        // Generate writer methods
        CodeEmitter.EmitWriter(model, code);
        CodeEmitter.EmitAsyncWriter(model, code);

        // Close type
        code.CloseBrace();

        // Close namespace
        code.CloseBrace();

        // Add source to compilation
        var fileName = $"{model.TypeName}.CsvHandler.g.cs";
        context.AddSource(fileName, code.ToString());
    }

    /// <summary>
    /// Validates the record model and reports diagnostics.
    /// </summary>
    private static bool ValidateModel(SourceProductionContext context, RecordModel model)
    {
        bool isValid = true;

        // CSV005: Check if type has any fields
        if (model.Fields.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.NoFieldsFound,
                model.Location,
                model.TypeName));
            return false;
        }

        // CSV002: Check for duplicate field orders
        var duplicateOrders = model.Fields
            .GroupBy(f => f.Order)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in duplicateOrders)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.DuplicateFieldOrder,
                group.First().Location,
                group.Key,
                model.TypeName));
            isValid = false;
        }

        // CSV006: Check for negative field orders
        foreach (var field in model.Fields)
        {
            if (field.Order < 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.InvalidFieldOrder,
                    field.Location,
                    field.MemberName,
                    model.TypeName,
                    field.Order));
                isValid = false;
            }
        }

        // CSV009: Check for duplicate field names
        var duplicateNames = model.Fields
            .GroupBy(f => f.CsvName)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in duplicateNames)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.DuplicateFieldName,
                group.First().Location,
                group.Key,
                model.TypeName));
            // This is a warning, not an error
        }

        return isValid;
    }

    /// <summary>
    /// Generates the CsvRecord and CsvField attribute definitions.
    /// </summary>
    private static string GenerateAttributeSource()
    {
        return """
// <auto-generated/>
#nullable enable

using System;

namespace CsvHandler.Attributes
{
    /// <summary>
    /// Marks a type for CSV source generation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    internal sealed class CsvRecordAttribute : Attribute
    {
        /// <summary>
        /// CSV delimiter character (default: ',').
        /// </summary>
        public char Delimiter { get; set; } = ',';

        /// <summary>
        /// Whether the CSV includes a header row (default: true).
        /// </summary>
        public bool HasHeaders { get; set; } = true;

        /// <summary>
        /// Whether to use strict mode and throw on format errors (default: false).
        /// </summary>
        public bool StrictMode { get; set; } = false;

        /// <summary>
        /// Culture name for parsing (e.g., "en-US"). Null uses invariant culture.
        /// </summary>
        public string? CultureName { get; set; }

        /// <summary>
        /// Whether to trim whitespace from fields (default: true).
        /// </summary>
        public bool TrimWhitespace { get; set; } = true;
    }

    /// <summary>
    /// Marks a field or property for CSV serialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    internal sealed class CsvFieldAttribute : Attribute
    {
        /// <summary>
        /// Order/index of this field in the CSV (0-based).
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// CSV column name (defaults to member name).
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Format string for serialization (e.g., date format).
        /// </summary>
        public string? Format { get; set; }

        /// <summary>
        /// Custom converter type (must implement ICsvConverter of T).
        /// </summary>
        public Type? ConverterType { get; set; }

        /// <summary>
        /// Whether this field is required during deserialization.
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Default value when field is empty or missing.
        /// </summary>
        public object? DefaultValue { get; set; }
    }
}
""";
    }
}

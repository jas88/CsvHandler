using Microsoft.CodeAnalysis;

namespace CsvHandler.SourceGenerator;

/// <summary>
/// Diagnostic descriptors for CSV source generator errors and warnings.
/// </summary>
internal static class Diagnostics
{
    private const string Category = "CsvHandler";

    /// <summary>
    /// CSV001: Types decorated with [CsvRecord] must be declared as partial.
    /// </summary>
    public static readonly DiagnosticDescriptor MustBePartial = new(
        id: "CSV001",
        title: "CsvRecord type must be partial",
        messageFormat: "Type '{0}' is decorated with [CsvRecord] but is not declared as partial",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types decorated with [CsvRecord] must be declared as partial to allow source generation.");

    /// <summary>
    /// CSV002: Duplicate field order detected.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateFieldOrder = new(
        id: "CSV002",
        title: "Duplicate field order",
        messageFormat: "Multiple fields have the same order value '{0}' in type '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each field must have a unique order value within a CSV record type.");

    /// <summary>
    /// CSV003: Field type is not supported for CSV serialization.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedFieldType = new(
        id: "CSV003",
        title: "Unsupported field type",
        messageFormat: "Field '{0}' in type '{1}' has unsupported type '{2}' for CSV serialization",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "CSV fields must be primitive types, strings, DateTime, DateTimeOffset, Guid, or nullable versions of these.");

    /// <summary>
    /// CSV004: CsvRecord cannot be applied to nested types.
    /// </summary>
    public static readonly DiagnosticDescriptor CannotBeNested = new(
        id: "CSV004",
        title: "CsvRecord cannot be applied to nested types",
        messageFormat: "Type '{0}' is nested within another type and cannot be decorated with [CsvRecord]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "CsvRecord attribute can only be applied to top-level types.");

    /// <summary>
    /// CSV005: No CSV fields found in record.
    /// </summary>
    public static readonly DiagnosticDescriptor NoFieldsFound = new(
        id: "CSV005",
        title: "No CSV fields found",
        messageFormat: "Type '{0}' has [CsvRecord] but contains no fields or properties decorated with [CsvField]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "CSV record types must contain at least one field or property decorated with [CsvField].");

    /// <summary>
    /// CSV006: Field order must be non-negative.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidFieldOrder = new(
        id: "CSV006",
        title: "Invalid field order",
        messageFormat: "Field '{0}' in type '{1}' has negative order value '{2}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Field order values must be non-negative integers.");

    /// <summary>
    /// CSV007: Custom converter type is invalid.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidConverterType = new(
        id: "CSV007",
        title: "Invalid converter type",
        messageFormat: "Field '{0}' in type '{1}' specifies invalid converter type '{2}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Custom converter must implement ICsvConverter<T> where T matches the field type.");

    /// <summary>
    /// CSV008: Field name cannot be empty.
    /// </summary>
    public static readonly DiagnosticDescriptor EmptyFieldName = new(
        id: "CSV008",
        title: "Empty field name",
        messageFormat: "Field in type '{0}' has an empty name specified in [CsvField]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "CSV field names cannot be empty strings.");

    /// <summary>
    /// CSV009: Duplicate field names detected.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateFieldName = new(
        id: "CSV009",
        title: "Duplicate field name",
        messageFormat: "Multiple fields have the same name '{0}' in type '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Multiple fields share the same CSV column name, which may cause ambiguity.");

    /// <summary>
    /// CSV010: Type must be a class or record.
    /// </summary>
    public static readonly DiagnosticDescriptor MustBeClassOrRecord = new(
        id: "CSV010",
        title: "Type must be class or record",
        messageFormat: "Type '{0}' decorated with [CsvRecord] must be a class or record type",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "CsvRecord attribute can only be applied to class or record types.");
}

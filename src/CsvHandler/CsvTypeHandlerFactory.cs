using System;
using System.Diagnostics.CodeAnalysis;

namespace CsvHandler;

/// <summary>
/// Factory for creating CSV type handlers via reflection (fallback path).
/// </summary>
[RequiresUnreferencedCode("Uses reflection to inspect type properties and fields.")]
[RequiresDynamicCode("May require dynamic code generation for value conversion.")]
internal static class CsvTypeHandlerFactory
{
    /// <summary>
    /// Creates a reflection-based type handler for the specified type.
    /// This is the fallback path when no source-generated context is available.
    /// </summary>
    [RequiresUnreferencedCode("Uses reflection to inspect type properties and fields.")]
    [RequiresDynamicCode("May require dynamic code generation for value conversion.")]
    public static ICsvTypeHandler<T> CreateReflectionHandler<T>(CsvOptions options)
    {
        return new ReflectionCsvTypeHandler<T>(options);
    }
}

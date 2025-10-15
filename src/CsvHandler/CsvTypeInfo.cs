// Copyright (c) 2024 CsvHandler
// Licensed under the MIT License

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CsvHandler;

/// <summary>
/// Non-generic base class for CSV type metadata.
/// </summary>
/// <remarks>
/// Provides common properties and functionality for CSV type metadata without
/// requiring generic type parameters. Used for runtime type access scenarios.
/// </remarks>
public abstract class CsvTypeInfo
{
    /// <summary>
    /// Gets the CLR type this metadata represents.
    /// </summary>
    public abstract Type Type { get; }

    /// <summary>
    /// Gets the collection of field metadata for this type.
    /// </summary>
    public abstract IReadOnlyList<CsvFieldInfo> Fields { get; }

    /// <summary>
    /// Gets the CSV options specific to this type.
    /// May be null to use context defaults.
    /// </summary>
    public virtual CsvOptions? Options { get; init; }

    /// <summary>
    /// Gets whether this type info supports synchronous reading.
    /// </summary>
    public abstract bool CanRead { get; }

    /// <summary>
    /// Gets whether this type info supports synchronous writing.
    /// </summary>
    public abstract bool CanWrite { get; }

    /// <summary>
    /// Gets whether this type info supports asynchronous streaming operations.
    /// </summary>
    public abstract bool CanStream { get; }
}

/// <summary>
/// Provides compile-time metadata and serialization delegates for a specific CSV type.
/// </summary>
/// <typeparam name="T">The type being serialized.</typeparam>
/// <remarks>
/// <para>
/// CsvTypeInfo is the core of the AOT-compatible serialization system. It contains:
/// </para>
/// <list type="bullet">
/// <item>Field metadata describing the CSV structure</item>
/// <item>Strongly-typed serialization delegates (no reflection)</item>
/// <item>Both synchronous and asynchronous reading/writing support</item>
/// <item>Type-specific options and configuration</item>
/// </list>
/// <para>
/// Instances are typically created by source generators and registered in a CsvContext.
/// </para>
/// </remarks>
public sealed class CsvTypeInfo<T> : CsvTypeInfo where T : class
{
    private List<CsvFieldInfo>? _fields;
    private IReadOnlyList<CsvFieldInfo>? _readOnlyFields;

    /// <inheritdoc/>
    public override Type Type => typeof(T);

    /// <inheritdoc/>
    public override IReadOnlyList<CsvFieldInfo> Fields
    {
        get
        {
            if (_readOnlyFields == null && _fields != null)
                _readOnlyFields = _fields.AsReadOnly();
            return _readOnlyFields ?? Array.Empty<CsvFieldInfo>();
        }
    }

    /// <summary>
    /// Gets or sets the mutable field collection.
    /// Used during type info construction by source generators.
    /// </summary>
    internal List<CsvFieldInfo> FieldsList
    {
        get => _fields ??= new List<CsvFieldInfo>();
        set => _fields = value;
    }

    /// <summary>
    /// Delegate for synchronous reading from a CSV data span.
    /// </summary>
    /// <remarks>
    /// The delegate receives a UTF-8 encoded CSV data span and returns a deserialized instance.
    /// This is the most efficient reading path for in-memory scenarios.
    /// </remarks>
    public CsvFieldReader<T>? Reader { get; init; }

    /// <summary>
    /// Delegate for synchronous writing to a buffer writer.
    /// </summary>
    /// <remarks>
    /// The delegate writes the object to an IBufferWriter, allowing zero-allocation
    /// serialization directly to the output buffer.
    /// </remarks>
    public Action<IBufferWriter<byte>, T>? Writer { get; init; }

    /// <summary>
    /// Delegate for asynchronous streaming reads from a stream.
    /// </summary>
    /// <remarks>
    /// Returns an IAsyncEnumerable that yields objects as they are parsed from the stream.
    /// Enables efficient processing of large CSV files without loading everything into memory.
    /// </remarks>
    public Func<Stream, IAsyncEnumerable<T>>? AsyncReader { get; init; }

    /// <summary>
    /// Delegate for asynchronous writing a single object to a stream.
    /// </summary>
    /// <remarks>
    /// Writes a single object to the stream asynchronously. For writing multiple objects,
    /// call this delegate multiple times or use a higher-level writer that buffers output.
    /// </remarks>
    public Func<Stream, T, ValueTask>? AsyncWriter { get; init; }

    /// <inheritdoc/>
    public override bool CanRead => Reader != null || AsyncReader != null;

    /// <inheritdoc/>
    public override bool CanWrite => Writer != null || AsyncWriter != null;

    /// <inheritdoc/>
    public override bool CanStream => AsyncReader != null || AsyncWriter != null;

    /// <summary>
    /// Creates a new instance with default configuration.
    /// </summary>
    public CsvTypeInfo()
    {
    }

    /// <summary>
    /// Validates that this type info is properly configured.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the type info is missing required configuration.
    /// </exception>
    internal void Validate()
    {
        if (!CanRead && !CanWrite)
            throw new InvalidOperationException(
                $"CsvTypeInfo<{typeof(T).Name}> must have at least one reader or writer delegate configured.");

        if (_fields == null || _fields.Count == 0)
            throw new InvalidOperationException(
                $"CsvTypeInfo<{typeof(T).Name}> must have at least one field defined.");

        // Validate field ordering
        var seenIndices = new HashSet<int>();
        foreach (var field in _fields)
        {
            if (field.Index < 0)
                throw new InvalidOperationException(
                    $"Field '{field.Name}' in type {typeof(T).Name} has invalid index {field.Index}.");

            if (!seenIndices.Add(field.Index))
                throw new InvalidOperationException(
                    $"Multiple fields in type {typeof(T).Name} have index {field.Index}.");
        }
    }
}

/// <summary>
/// Builder for constructing CsvTypeInfo instances.
/// </summary>
/// <typeparam name="T">The type being configured.</typeparam>
/// <remarks>
/// Provides a fluent API for building type information. Used by source generators
/// to construct type metadata in a type-safe manner.
/// </remarks>
public sealed class CsvTypeInfoBuilder<T> where T : class
{
    private readonly CsvTypeInfo<T> _typeInfo = new();

    /// <summary>
    /// Sets the synchronous reader delegate.
    /// </summary>
    public CsvTypeInfoBuilder<T> WithReader(CsvFieldReader<T> reader)
    {
        _typeInfo.FieldsList.Clear();
        var newTypeInfo = new CsvTypeInfo<T>
        {
            Reader = reader,
            FieldsList = _typeInfo.FieldsList
        };
        return this;
    }

    /// <summary>
    /// Sets the synchronous writer delegate.
    /// </summary>
    public CsvTypeInfoBuilder<T> WithWriter(Action<IBufferWriter<byte>, T> writer)
    {
        _typeInfo.FieldsList.Clear();
        var newTypeInfo = new CsvTypeInfo<T>
        {
            Writer = writer,
            FieldsList = _typeInfo.FieldsList
        };
        return this;
    }

    /// <summary>
    /// Adds a field to the type metadata.
    /// </summary>
    public CsvTypeInfoBuilder<T> AddField(CsvFieldInfo field)
    {
        _typeInfo.FieldsList.Add(field);
        return this;
    }

    /// <summary>
    /// Builds and validates the final CsvTypeInfo instance.
    /// </summary>
    public CsvTypeInfo<T> Build()
    {
        _typeInfo.Validate();
        return _typeInfo;
    }
}

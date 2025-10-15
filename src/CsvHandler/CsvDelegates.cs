// Copyright (c) 2025 CsvHandler Contributors
// Licensed under the MIT License

using System;

namespace CsvHandler;

/// <summary>
/// Delegate for reading a CSV field from a UTF-8 byte span and converting it to a typed object.
/// </summary>
/// <param name="fieldData">The UTF-8 encoded field data from the CSV.</param>
/// <returns>The parsed object value, or null.</returns>
public delegate object? CsvFieldReader(ReadOnlySpan<byte> fieldData);

/// <summary>
/// Delegate for reading a CSV field from a UTF-8 byte span and converting it to a typed value.
/// </summary>
/// <typeparam name="T">The type to convert the field data to.</typeparam>
/// <param name="fieldData">The UTF-8 encoded field data from the CSV.</param>
/// <returns>The parsed value of type T.</returns>
public delegate T CsvFieldReader<T>(ReadOnlySpan<byte> fieldData);

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace CsvHandler;

/// <summary>
/// Reflection-based CSV type handler (fallback when no source-generated context available).
/// </summary>
[RequiresUnreferencedCode("Uses reflection to access type members.")]
[RequiresDynamicCode("May require dynamic code generation for value conversion.")]
internal sealed class ReflectionCsvTypeHandler<T> : ICsvTypeHandler<T>
{
    private readonly CsvOptions _options;
    private readonly List<PropertyInfo> _properties;
    private readonly Dictionary<string, PropertyInfo> _propertyMap;
    private IReadOnlyList<string>? _headers;
    private PropertyInfo[]? _orderedProperties;

    public int ExpectedFieldCount => _properties.Count;

    public ReflectionCsvTypeHandler(CsvOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        var type = typeof(T);

        // Get all public instance properties with setters
        _properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.GetSetMethod() != null)
            .ToList();

        // Create case-insensitive property lookup
        _propertyMap = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in _properties)
        {
            _propertyMap[prop.Name] = prop;
        }

        if (_properties.Count == 0)
            throw new InvalidOperationException($"Type {type.Name} has no settable properties.");
    }

    public void SetHeaders(IReadOnlyList<string> headers)
    {
        _headers = headers ?? throw new ArgumentNullException(nameof(headers));

        // Map headers to properties
        _orderedProperties = new PropertyInfo[headers.Count];

        for (int i = 0; i < headers.Count; i++)
        {
            var headerName = headers[i].Trim();

            if (_propertyMap.TryGetValue(headerName, out var property))
            {
                _orderedProperties[i] = property;
            }
            else if (!_options.AllowJaggedRows)
            {
                throw new CsvException(
                    $"No property found for header '{headerName}' on type {typeof(T).Name}",
                    1,
                    0,
                    CsvErrorType.InvalidHeader,
                    headerName);
            }
        }
    }

    public T Deserialize(IReadOnlyList<string> fields, long lineNumber)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(fields);
#else
        ArgumentNullExceptionPolyfill.ThrowIfNull(fields);
#endif

        // Check field count
        var expectedCount = _orderedProperties?.Length ?? _properties.Count;
        if (!_options.AllowJaggedRows && fields.Count != expectedCount)
        {
            throw new CsvException(
                $"Expected {expectedCount} fields but found {fields.Count}",
                lineNumber,
                0,
                CsvErrorType.FieldCountMismatch);
        }

        // Create instance
        var instance = Activator.CreateInstance<T>();

        // Use header mapping if available
        if (_orderedProperties != null)
        {
            for (int i = 0; i < Math.Min(fields.Count, _orderedProperties.Length); i++)
            {
                var property = _orderedProperties[i];
                if (property == null)
                    continue;

                SetPropertyValue(instance, property, fields[i], lineNumber, i);
            }
        }
        else
        {
            // No headers - map by position
            for (int i = 0; i < Math.Min(fields.Count, _properties.Count); i++)
            {
                var property = _properties[i];
                SetPropertyValue(instance, property, fields[i], lineNumber, i);
            }
        }

        return instance;
    }

    private void SetPropertyValue(T instance, PropertyInfo property, string value, long lineNumber, int position)
    {
        try
        {
            // Apply trimming
            var trimmedValue = ApplyTrimming(value);

            // Handle null/empty values
            if (string.IsNullOrEmpty(trimmedValue))
            {
                if (IsNullableType(property.PropertyType))
                {
                    property.SetValue(instance, null);
                    return;
                }

                // Use default value for value types
                if (property.PropertyType.IsValueType)
                {
                    property.SetValue(instance, Activator.CreateInstance(property.PropertyType));
                    return;
                }

                property.SetValue(instance, null);
                return;
            }

            // Convert value to target type
            var convertedValue = ConvertValue(trimmedValue, property.PropertyType);
            property.SetValue(instance, convertedValue);
        }
        catch (Exception ex)
        {
            throw new CsvException(
                $"Failed to set property '{property.Name}': {ex.Message}",
                lineNumber,
                position,
                CsvErrorType.TypeConversion,
                property.Name,
                innerException: ex);
        }
    }

    private string ApplyTrimming(string value)
    {
        if (_options.TrimOptions == CsvTrimOptions.None)
            return value;

        if (_options.TrimOptions.HasFlag(CsvTrimOptions.All))
            return value.Trim();

        if (_options.TrimOptions.HasFlag(CsvTrimOptions.UnquotedOnly))
        {
            // Only trim if not quoted
            if (value.Length >= 2 && value[0] == _options.Quote && value[^1] == _options.Quote)
                return value;

            return value.Trim();
        }

        return value;
    }

    private object? ConvertValue(string value, Type targetType)
    {
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // String - no conversion needed
        if (underlyingType == typeof(string))
            return value;

        // Boolean
        if (underlyingType == typeof(bool))
        {
            if (bool.TryParse(value, out var boolValue))
                return boolValue;

            // Handle common boolean representations
            var lowerValue = value.ToLowerInvariant();
            if (lowerValue == "1" || lowerValue == "yes" || lowerValue == "y" || lowerValue == "true")
                return true;
            if (lowerValue == "0" || lowerValue == "no" || lowerValue == "n" || lowerValue == "false")
                return false;

            throw new FormatException($"Cannot convert '{value}' to boolean");
        }

        // Use IConvertible for primitive types
        if (underlyingType.IsPrimitive || underlyingType == typeof(decimal) || underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset))
        {
            return Convert.ChangeType(value, underlyingType, _options.Culture);
        }

        // Guid
        if (underlyingType == typeof(Guid))
            return Guid.Parse(value);

        // TimeSpan
        if (underlyingType == typeof(TimeSpan))
            return TimeSpan.Parse(value, _options.Culture);

#if NET6_0_OR_GREATER
        // DateOnly
        if (underlyingType == typeof(DateOnly))
            return DateOnly.Parse(value, _options.Culture);

        // TimeOnly
        if (underlyingType == typeof(TimeOnly))
            return TimeOnly.Parse(value, _options.Culture);
#endif

        // Enum
        if (underlyingType.IsEnum)
            return Enum.Parse(underlyingType, value, ignoreCase: true);

        // Fallback to Convert.ChangeType
        return Convert.ChangeType(value, underlyingType, _options.Culture);
    }

    private static bool IsNullableType(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }
}

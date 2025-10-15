namespace CsvHandler.Attributes;

/// <summary>
/// Specifies CSV column mapping for a property.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
#if CSVHANDLER_GENERATOR
internal
#else
public
#endif
sealed class CsvColumnAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the column name in the CSV file.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the column order (0-based index).
    /// </summary>
    public int Order { get; set; } = -1;

    /// <summary>
    /// Gets or sets whether this column is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the default value if the column is missing or empty.
    /// </summary>
    public object? DefaultValue { get; set; }
}

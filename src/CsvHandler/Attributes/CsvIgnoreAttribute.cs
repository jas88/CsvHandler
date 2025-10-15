using System;

namespace CsvHandler.Attributes;

/// <summary>
/// Marks a property or field to be excluded from CSV serialization and deserialization.
/// </summary>
/// <remarks>
/// Properties and fields marked with this attribute are completely ignored during CSV operations.
/// This is useful for internal state, computed properties, or data that should not be persisted.
/// </remarks>
/// <example>
/// <code>
/// [CsvRecord]
/// public partial class User
/// {
///     [CsvField(Order = 0)]
///     public string Username { get; set; }
///
///     [CsvField(Order = 1)]
///     public string Email { get; set; }
///
///     // This property is not included in CSV
///     [CsvIgnore]
///     public string PasswordHash { get; set; }
///
///     // Computed properties can be ignored
///     [CsvIgnore]
///     public string DisplayName => $"{FirstName} {LastName}";
///
///     // Internal state
///     [CsvIgnore]
///     public DateTime LastModified { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
#if CSVHANDLER_GENERATOR
internal
#else
public
#endif
sealed class CsvIgnoreAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CsvIgnoreAttribute"/> class.
    /// </summary>
    public CsvIgnoreAttribute()
    {
    }
}

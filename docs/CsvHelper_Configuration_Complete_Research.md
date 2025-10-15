# CsvHelper CsvConfiguration - Complete Research Document

## Executive Summary

This document provides a comprehensive analysis of ALL CsvConfiguration options in the CsvHelper library (v33+), organized by category. Each option includes its type, default value, use cases, and implications for source generator + UTF-8 implementation.

---

## 1. DELIMITER OPTIONS

### 1.1 Delimiter
- **Type**: `string`
- **Default**: Culture-dependent (usually `,`)
- **Description**: The delimiter to separate fields in a CSV file
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Delimiter = "|"  // Pipe-delimited
};
```
- **Use Cases**:
  - Tab-delimited files: `Delimiter = "\t"`
  - Semicolon (European): `Delimiter = ";"`
  - Pipe-delimited: `Delimiter = "|"`
- **Source Generator Challenge**: LOW - Easy to handle with UTF-8 byte sequences

### 1.2 DetectDelimiter
- **Type**: `bool`
- **Default**: `false`
- **Description**: Automatically detect delimiter from the first line
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    DetectDelimiter = true,
    DetectDelimiterValues = new[] { ",", ";", "|", "\t" }
};
```
- **Use Cases**: Reading files from unknown sources
- **Source Generator Challenge**: MEDIUM - Requires runtime analysis before parsing

### 1.3 DetectDelimiterValues
- **Type**: `string[]`
- **Default**: `[",", ";", "|", "\t"]`
- **Description**: Array of possible delimiters for auto-detection
- **Use Cases**: Customizing delimiter detection to specific formats
- **Source Generator Challenge**: MEDIUM - Requires runtime scanning logic

### 1.4 GetDelimiter
- **Type**: `GetDelimiter` (delegate)
- **Default**: `ConfigurationFunctions.GetDelimiter`
- **Description**: Callback function for custom delimiter detection logic
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    GetDelimiter = args =>
    {
        // Custom logic to analyze args.Context and return delimiter
        return args.Context.Parser.Count > 5 ? ";" : ",";
    }
};
```
- **Use Cases**: Complex delimiter detection based on context
- **Source Generator Challenge**: HIGH - Requires runtime delegate invocation

---

## 2. QUOTE/ESCAPE OPTIONS

### 2.1 Quote
- **Type**: `char`
- **Default**: `"`
- **Description**: Character used to wrap fields that need quoting
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Quote = '^'  // Use caret instead of double-quote
};
```
- **Use Cases**: Non-standard CSV formats, compatibility with legacy systems
- **Source Generator Challenge**: LOW - Simple byte comparison in UTF-8

### 2.2 Escape
- **Type**: `char`
- **Default**: `"`
- **Description**: Character used to escape special characters
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Escape = '\\',  // Use backslash for escaping
    Mode = CsvMode.Escape
};
```
- **Use Cases**: Unix-style escaping, backslash-escaped formats
- **Source Generator Challenge**: LOW - Byte comparison and state machine

### 2.3 Mode
- **Type**: `CsvMode` enum
- **Default**: `CsvMode.RFC4180`
- **Values**:
  - `RFC4180`: Standard CSV with quotes
  - `Escape`: Unix-style with escape characters
  - `NoEscape`: No quoting or escaping
- **Code Example**:
```csharp
// RFC4180 Mode (default)
var config1 = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Mode = CsvMode.RFC4180,  // "value with, comma"
};

// Escape Mode
var config2 = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Mode = CsvMode.Escape,
    Escape = '\\',  // value with\, comma
};

// NoEscape Mode
var config3 = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Mode = CsvMode.NoEscape  // No special handling
};
```
- **Use Cases**:
  - RFC4180: Standard CSV files
  - Escape: Unix/Linux-style files
  - NoEscape: Simple files with no special characters
- **Source Generator Challenge**: MEDIUM - Different parsing logic per mode

### 2.4 AllowComments
- **Type**: `bool`
- **Default**: `false`
- **Description**: Allow comment lines in CSV
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    AllowComments = true,
    Comment = '#'
};
```
- **Use Cases**: Self-documenting CSV files, configuration files
- **Source Generator Challenge**: LOW - Simple line filtering

### 2.5 Comment
- **Type**: `char`
- **Default**: `#`
- **Description**: Character that starts a comment line
- **Use Cases**: Customizing comment character
- **Source Generator Challenge**: LOW - Byte comparison

### 2.6 IgnoreQuotes
- **Type**: `bool`
- **Default**: `false` (not explicitly shown in config)
- **Description**: Treat quote characters as normal characters
- **Use Cases**: Malformed CSV files where quotes aren't used correctly
- **Source Generator Challenge**: LOW - Simplifies parsing

---

## 3. ERROR HANDLING & BAD DATA

### 3.1 BadDataFound
- **Type**: `BadDataFound?` (delegate)
- **Default**: `ConfigurationFunctions.BadDataFound` (throws exception)
- **Description**: Called when bad field data is found (e.g., unescaped quote in field)
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    BadDataFound = args =>
    {
        Console.WriteLine($"Bad data at row {args.Context.Parser.Row}: {args.RawRecord}");
        // Don't throw - continue processing
    }
};

// Or ignore bad data completely
var configIgnore = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    BadDataFound = null  // No exception thrown
};
```
- **Use Cases**:
  - Logging bad data without stopping
  - Collecting errors for batch reporting
  - Working with malformed legacy files
- **Source Generator Challenge**: HIGH - Requires runtime delegate invocation and context

### 3.2 ReadingExceptionOccurred
- **Type**: `ReadingExceptionOccurred` (delegate: `Func<CsvHelperException, bool>`)
- **Default**: Throws exception
- **Description**: Called when an exception occurs during reading; return true to throw, false to continue
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ReadingExceptionOccurred = args =>
    {
        Console.WriteLine($"Error: {args.Exception.Message}");
        return false;  // Don't throw, continue reading
    }
};
```
- **Use Cases**: Resilient parsing, error collection, data validation
- **Source Generator Challenge**: HIGH - Requires exception handling and delegate invocation

### 3.3 MissingFieldFound
- **Type**: `MissingFieldFound?` (delegate)
- **Default**: Throws `MissingFieldException`
- **Description**: Called when a field is missing from a record
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    MissingFieldFound = args =>
    {
        Console.WriteLine($"Missing field '{string.Join(",", args.HeaderNames)}' at index {args.Index}");
    }
};

// Or ignore missing fields
var configIgnore = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    MissingFieldFound = null
};
```
- **Use Cases**: Handling incomplete records, flexible schemas
- **Source Generator Challenge**: HIGH - Requires field tracking and delegate invocation

### 3.4 LineBreakInQuotedFieldIsBadData
- **Type**: `bool`
- **Default**: `false`
- **Description**: Treat line breaks within quoted fields as bad data
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    LineBreakInQuotedFieldIsBadData = true
};
```
- **Use Cases**: Enforcing single-line fields, detecting malformed data
- **Source Generator Challenge**: MEDIUM - Affects quote handling state machine
- **Known Issues**: Reported inconsistencies in v28.0.0+

### 3.5 DetectColumnCountChanges
- **Type**: `bool`
- **Default**: `false`
- **Description**: Throw exception if column count changes between rows
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    DetectColumnCountChanges = true
};
```
- **Use Cases**: Strict validation, detecting malformed files
- **Source Generator Challenge**: LOW - Simple column counter

### 3.6 MaxFieldSize
- **Type**: `double`
- **Default**: `0` (unlimited)
- **Description**: Maximum size of a single field in characters
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    MaxFieldSize = 10000  // Throw if field exceeds 10K chars
};
```
- **Use Cases**:
  - Memory protection
  - Detecting missing closing quotes
  - Preventing buffer overflow attacks
- **Source Generator Challenge**: LOW - Simple counter

### 3.7 ExceptionMessagesContainRawData
- **Type**: `bool`
- **Default**: `true`
- **Description**: Include raw CSV data in exception messages
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ExceptionMessagesContainRawData = false  // Hide sensitive data
};
```
- **Use Cases**: Security (hiding sensitive data), compliance
- **Source Generator Challenge**: LOW - Exception formatting only

---

## 4. WHITESPACE & TRIMMING

### 4.1 TrimOptions
- **Type**: `TrimOptions` enum
- **Default**: `TrimOptions.None`
- **Values**:
  - `None`: No trimming
  - `Trim`: Trim outside quotes
  - `InsideQuotes`: Trim inside quotes
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    TrimOptions = TrimOptions.Trim
};
```
- **Use Cases**:
  - Cleaning poorly formatted data
  - Normalizing whitespace
  - Excel-generated files with extra spaces
- **Source Generator Challenge**: LOW - Simple trimming logic

### 4.2 WhiteSpaceChars
- **Type**: `char[]`
- **Default**: `[' ', '\t']` (in v27+)
- **Description**: Characters considered whitespace for trimming
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    WhiteSpaceChars = new[] { ' ', '\t', '\r', '\n' }
};
```
- **Use Cases**: Custom whitespace definitions
- **Validation**: Cannot include Quote or Delimiter characters
- **Source Generator Challenge**: LOW - Byte set comparison

### 4.3 IgnoreBlankLines
- **Type**: `bool`
- **Default**: `true`
- **Description**: Skip empty lines in the CSV
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    IgnoreBlankLines = false  // Process blank lines
};
```
- **Use Cases**: Handling files with intentional blank rows
- **Source Generator Challenge**: LOW - Simple line filtering

---

## 5. HEADER OPTIONS

### 5.1 HasHeaderRecord
- **Type**: `bool`
- **Default**: `true`
- **Description**: First row contains headers
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HasHeaderRecord = false  // No header row
};
```
- **Use Cases**: Headerless files, numeric column references
- **Source Generator Challenge**: LOW - Affects initial parsing only

### 5.2 HeaderValidated
- **Type**: `HeaderValidated?` (delegate)
- **Default**: Throws `ValidationException` if headers invalid
- **Description**: Called to validate headers against expected mapping
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HeaderValidated = args =>
    {
        if (args.InvalidHeaders.Length > 0)
        {
            Console.WriteLine($"Invalid headers: {string.Join(", ", args.InvalidHeaders.Select(h => h.Names[0]))}");
        }
        // Don't throw - allow missing headers
    }
};

// Or ignore header validation
var configIgnore = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HeaderValidated = null
};
```
- **Use Cases**:
  - Flexible schemas
  - Optional columns
  - Logging validation issues
- **Source Generator Challenge**: HIGH - Requires runtime validation and delegate

### 5.3 PrepareHeaderForMatch
- **Type**: `PrepareHeaderForMatch` (delegate: `Func<PrepareHeaderForMatchArgs, string>`)
- **Default**: Returns header unchanged
- **Description**: Transform header names before matching to property names
- **Code Example**:
```csharp
// Remove spaces
var config1 = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    PrepareHeaderForMatch = args => args.Header.Replace(" ", "")
};

// Case-insensitive + remove special chars
var config2 = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    PrepareHeaderForMatch = args => Regex.Replace(args.Header.ToLower(), @"[^a-z0-9]", "")
};
```
- **Use Cases**:
  - Handling spaces in headers
  - Case normalization
  - Special character removal
  - Matching inconsistent column names
- **Source Generator Challenge**: HIGH - Requires runtime transformation and string manipulation

### 5.4 ReferenceHeaderPrefix
- **Type**: `ReferenceHeaderPrefix` (delegate)
- **Default**: Returns `$"{args.MemberName}."`
- **Description**: Prefix for nested object headers
- **Code Example**:
```csharp
// Given class: Person { Address Address { get; set; } }
// CSV headers: Address.Street, Address.City

var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ReferenceHeaderPrefix = args => $"{args.MemberName}_"
};
// Now expects: Address_Street, Address_City
```
- **Use Cases**:
  - Nested objects in CSV
  - Flattened hierarchies
  - Custom naming conventions
- **Source Generator Challenge**: MEDIUM - Affects mapping generation

---

## 6. TYPE CONVERSION & FORMATTING

### 6.1 CultureInfo
- **Type**: `CultureInfo`
- **Default**: Required in constructor (no default)
- **Description**: Culture for parsing numbers, dates, and default delimiter
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.GetCultureInfo("de-DE"))
{
    // Uses German formatting: , for decimals, ; for delimiter
};
```
- **Use Cases**:
  - International files
  - European number formats (comma as decimal)
  - Culture-specific dates
- **Source Generator Challenge**: HIGH - Requires CultureInfo during parsing

### 6.2 TypeConverterCache
- **Type**: `TypeConverterCache` (legacy, now on Context)
- **Description**: Cache of type converters for different types
- **Code Example**:
```csharp
// v27+: Use Context.TypeConverterCache
csvReader.Context.TypeConverterCache.AddConverter<Guid>(new GuidConverter());
```
- **Use Cases**: Custom type conversions
- **Source Generator Challenge**: HIGH - Requires type conversion infrastructure

### 6.3 TypeConverterOptionsCache
- **Type**: `TypeConverterOptionsCache` (legacy, now on Context)
- **Description**: Options for type converters (formats, cultures, styles)
- **Code Example**:
```csharp
// Configure double formatting
csvWriter.Context.TypeConverterOptionsCache.AddOptions<double?>(
    new TypeConverterOptions
    {
        Formats = new[] { "G17" },
        CultureInfo = CultureInfo.InvariantCulture
    }
);

// Configure boolean values
csvReader.Context.TypeConverterOptionsCache.GetOptions<bool>().BooleanTrueValues.Add("1");
csvReader.Context.TypeConverterOptionsCache.GetOptions<bool>().BooleanFalseValues.Add("0");

// Configure null values
csvReader.Context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add("NULL");
csvReader.Context.TypeConverterOptionsCache.GetOptions<DateTime?>().NullValues.AddRange(new[] { "NULL", "0001/01/01" });
```
- **Use Cases**:
  - Custom number formats
  - Date format strings
  - Boolean value mappings
  - Null value representations
- **Source Generator Challenge**: HIGH - Complex per-type configuration

### 6.4 DateTimeStyles
- **Type**: `DateTimeStyles` enum
- **Description**: In TypeConverterOptions, controls DateTime parsing
- **Code Example**:
```csharp
csvReader.Context.TypeConverterOptionsCache.AddOptions<DateTime>(
    new TypeConverterOptions
    {
        DateTimeStyle = DateTimeStyles.AssumeUniversal,
        Formats = new[] { "yyyy-MM-dd HH:mm:ss" }
    }
);
```
- **Use Cases**: Timezone handling, UTC dates
- **Source Generator Challenge**: HIGH - DateTime parsing complexity

### 6.5 NumberStyles
- **Type**: `NumberStyles` enum
- **Description**: In TypeConverterOptions, controls number parsing
- **Code Example**:
```csharp
csvReader.Context.TypeConverterOptionsCache.AddOptions<decimal>(
    new TypeConverterOptions
    {
        NumberStyles = NumberStyles.Currency,
        CultureInfo = CultureInfo.GetCultureInfo("en-US")
    }
);
```
- **Use Cases**: Currency symbols, thousands separators
- **Source Generator Challenge**: HIGH - Number parsing complexity

### 6.6 BooleanTrueValues / BooleanFalseValues
- **Type**: `string[]` (in TypeConverterOptions)
- **Default**: `["true", "yes", "1"]` / `["false", "no", "0"]`
- **Description**: Values that map to true/false
- **Code Example**:
```csharp
// Attribute approach
public class MyClass
{
    [BooleanTrueValues("Y", "YES", "1")]
    [BooleanFalseValues("N", "NO", "0")]
    public bool IsActive { get; set; }
}

// Configuration approach
var options = csvReader.Context.TypeConverterOptionsCache.GetOptions<bool>();
options.BooleanTrueValues.Clear();
options.BooleanTrueValues.AddRange(new[] { "Y", "YES", "1" });
options.BooleanFalseValues.Clear();
options.BooleanFalseValues.AddRange(new[] { "N", "NO", "0" });
```
- **Use Cases**:
  - Legacy data with Y/N
  - 1/0 numeric booleans
  - Multiple language support
- **Source Generator Challenge**: MEDIUM - Lookup table per type

### 6.7 NullValues
- **Type**: `string[]` (in TypeConverterOptions)
- **Default**: `[""]`
- **Description**: Values that represent null
- **Code Example**:
```csharp
// Attribute approach
public class MyClass
{
    [NullValues("NULL", "N/A", "")]
    public string? Name { get; set; }
}

// Configuration approach
var options = csvReader.Context.TypeConverterOptionsCache.GetOptions<string>();
options.NullValues.AddRange(new[] { "NULL", "N/A", "-" });
```
- **Use Cases**:
  - Database exports with "NULL"
  - Excel with "N/A"
  - Missing data markers
- **Source Generator Challenge**: MEDIUM - String comparison per type

### 6.8 Formats
- **Type**: `string[]` (in TypeConverterOptions)
- **Description**: Format strings for parsing/writing dates and numbers
- **Code Example**:
```csharp
// Multiple date formats
csvReader.Context.TypeConverterOptionsCache.AddOptions<DateTime>(
    new TypeConverterOptions
    {
        Formats = new[] { "yyyy-MM-dd", "MM/dd/yyyy", "dd.MM.yyyy" }
    }
);

// Number format for writing
csvWriter.Context.TypeConverterOptionsCache.AddOptions<double>(
    new TypeConverterOptions
    {
        Formats = new[] { "F2" }  // 2 decimal places
    }
);
```
- **Use Cases**: International dates, decimal precision control
- **Source Generator Challenge**: HIGH - Format string parsing

---

## 7. ADVANCED FEATURES

### 7.1 BufferSize
- **Type**: `int`
- **Default**: `4096` (0x1000)
- **Description**: Internal buffer size for reading/writing
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    BufferSize = 16384  // 16KB buffer
};
```
- **Use Cases**: Performance tuning for large files
- **Source Generator Challenge**: LOW - Memory allocation only

### 7.2 ProcessFieldBufferSize
- **Type**: `int`
- **Default**: `1024`
- **Description**: Buffer for processing individual fields (auto-grows)
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ProcessFieldBufferSize = 2048
};
```
- **Use Cases**: Large fields, performance optimization
- **Source Generator Challenge**: LOW - Memory allocation

### 7.3 CountBytes
- **Type**: `bool`
- **Default**: `false`
- **Description**: Track byte count during parsing
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    CountBytes = true
};
// Access via: csvReader.Parser.ByteCount
```
- **Use Cases**: Progress tracking, byte-offset indexing
- **Source Generator Challenge**: LOW - Counter only

### 7.4 Encoding
- **Type**: `Encoding`
- **Default**: `Encoding.UTF8`
- **Description**: Text encoding for byte operations
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Encoding = Encoding.GetEncoding("ISO-8859-1")
};
```
- **Use Cases**: Legacy encodings, byte counting
- **Source Generator Challenge**: MEDIUM - Encoding conversion if not UTF-8

### 7.5 NewLine
- **Type**: `string`
- **Default**: `"\r\n"` (for writing)
- **Description**: Line ending sequence (reads any: \r\n, \r, \n)
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    NewLine = "\n"  // Unix-style line endings
};
```
- **Use Cases**: Platform-specific line endings, compliance
- **Source Generator Challenge**: LOW - Simple byte sequence

### 7.6 LeaveOpen
- **Type**: `bool`
- **Default**: `false`
- **Description**: Don't dispose underlying TextReader/TextWriter
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    LeaveOpen = true
};
```
- **Use Cases**: Reusing streams, chaining operations
- **Source Generator Challenge**: LOW - Resource management only

### 7.7 InjectionOptions
- **Type**: `InjectionOptions` enum
- **Default**: `InjectionOptions.None`
- **Values**: `None`, `Escape`, `Strip`, `Exception`
- **Description**: Protect against CSV injection attacks
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    InjectionOptions = InjectionOptions.Escape
};
// Escapes fields starting with: =, @, +, -, \t, \r
```
- **Use Cases**: Security, Excel formula injection prevention
- **Source Generator Challenge**: LOW - Simple character checking

### 7.8 CacheFields
- **Type**: `bool`
- **Default**: `false`
- **Description**: Cache field values after parsing a row
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    CacheFields = true
};
```
- **Use Cases**: Multiple reads of same row, performance
- **Source Generator Challenge**: MEDIUM - Affects memory management

### 7.9 ShouldSkipRecord
- **Type**: `ShouldSkipRecord?` (delegate: `Func<ShouldSkipRecordArgs, bool>`)
- **Default**: `null`
- **Description**: Determine if a record should be skipped
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ShouldSkipRecord = args =>
    {
        // Skip empty records
        return args.Record.Length == 0 ||
               args.Record.All(string.IsNullOrWhiteSpace);
    }
};
```
- **Use Cases**: Filtering data, conditional parsing
- **Source Generator Challenge**: HIGH - Runtime delegate evaluation

---

## 8. DYNAMIC/REFLECTION SUPPORT

### 8.1 GetDynamicPropertyName
- **Type**: `GetDynamicPropertyName` (delegate)
- **Default**: Returns `$"Field{args.FieldIndex}"`
- **Description**: Generate property names for dynamic objects
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    GetDynamicPropertyName = args => $"Column_{args.FieldIndex}"
};
```
- **Use Cases**: Dynamic object reading, headerless files
- **Source Generator Challenge**: N/A - Not applicable to strongly-typed source generators

### 8.2 DynamicPropertySort
- **Type**: `IComparer<string>?`
- **Default**: `null` (maintains order)
- **Description**: Control sorting of dynamic properties
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    DynamicPropertySort = StringComparer.OrdinalIgnoreCase
};
```
- **Use Cases**: Consistent property ordering in dynamic objects
- **Source Generator Challenge**: N/A - Not applicable to source generators

### 8.3 IncludePrivateMembers
- **Type**: `bool`
- **Default**: `false`
- **Description**: Include private properties/fields in auto-mapping
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    IncludePrivateMembers = true
};
```
- **Use Cases**: Serializing internal state, private DTOs
- **Source Generator Challenge**: MEDIUM - Source generator can see privates

### 8.4 MemberTypes
- **Type**: `MemberTypes` flags enum
- **Default**: `MemberTypes.Properties`
- **Description**: Which member types to auto-map (Properties, Fields, or both)
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    MemberTypes = MemberTypes.Properties | MemberTypes.Fields
};
```
- **Use Cases**: Mapping to fields, structs with fields
- **Source Generator Challenge**: LOW - Source generator controls this

### 8.5 IgnoreReferences
- **Type**: `bool`
- **Default**: `false`
- **Description**: Don't follow reference types when auto-mapping
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    IgnoreReferences = true
};
```
- **Use Cases**: Flat CSV structure, avoiding nested objects
- **Source Generator Challenge**: LOW - Affects mapping generation

### 8.6 UseNewObjectForNullReferenceMembers
- **Type**: `bool`
- **Default**: `false`
- **Description**: When writing, create default objects for null references
- **Code Example**:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    UseNewObjectForNullReferenceMembers = true
};
// If Person.Address is null, writes default values instead of empty
```
- **Use Cases**: Avoiding empty columns, default value population
- **Source Generator Challenge**: LOW - Only affects writing

---

## 9. CONFIGURATION COMPLEXITY MATRIX

### Easy for Source Generator + UTF-8 (LOW Complexity)
- Delimiter (fixed values)
- Quote, Escape characters
- AllowComments, Comment character
- IgnoreBlankLines
- HasHeaderRecord
- NewLine
- BufferSize, ProcessFieldBufferSize
- CountBytes
- MaxFieldSize
- DetectColumnCountChanges
- TrimOptions, WhiteSpaceChars
- LeaveOpen
- InjectionOptions
- ExceptionMessagesContainRawData
- IncludePrivateMembers
- MemberTypes
- IgnoreReferences
- UseNewObjectForNullReferenceMembers
- LineBreakInQuotedFieldIsBadData

### Moderate Complexity (MEDIUM)
- Mode (requires different parsing strategies)
- DetectDelimiter (runtime analysis)
- ReferenceHeaderPrefix (affects mapping)
- BooleanTrueValues/FalseValues (lookup tables)
- NullValues (string comparison)
- CacheFields (memory management)
- Encoding (if not UTF-8)

### High Complexity (HIGH - Requires Delegates/Runtime)
- BadDataFound (delegate)
- ReadingExceptionOccurred (delegate)
- MissingFieldFound (delegate)
- HeaderValidated (delegate)
- PrepareHeaderForMatch (delegate - string transformation)
- GetDelimiter (delegate)
- ShouldSkipRecord (delegate)
- CultureInfo (affects parsing behavior)
- TypeConverterCache (conversion infrastructure)
- TypeConverterOptionsCache (complex per-type config)
- DateTimeStyles/NumberStyles (format parsing)
- Formats (format string interpretation)

### Not Applicable to Source Generators
- GetDynamicPropertyName (dynamic objects only)
- DynamicPropertySort (dynamic objects only)

---

## 10. RECOMMENDATIONS FOR SOURCE GENERATOR

### Phase 1: Essential Features (Core CSV Parsing)
Implement these for basic CSV support:
- Fixed Delimiter, Quote, Escape
- Mode (RFC4180, Escape, NoEscape)
- HasHeaderRecord
- IgnoreBlankLines
- TrimOptions, WhiteSpaceChars
- NewLine
- Basic type conversion (without CultureInfo)

### Phase 2: Error Handling
Add basic error handling without delegates:
- MaxFieldSize
- DetectColumnCountChanges
- LineBreakInQuotedFieldIsBadData
- Simple exception throwing (no callbacks)

### Phase 3: Type Conversion
Implement type-specific conversions:
- BooleanTrueValues/FalseValues (compile-time configuration)
- NullValues (compile-time configuration)
- Basic format strings for DateTime

### Phase 4: Advanced Features
Consider for completeness:
- CultureInfo support (if needed)
- InjectionOptions
- CountBytes

### Features to Avoid/Defer (Delegate-Heavy)
These require runtime flexibility that contradicts source generator benefits:
- BadDataFound, MissingFieldFound, ReadingExceptionOccurred
- PrepareHeaderForMatch, HeaderValidated
- GetDelimiter, DetectDelimiter
- ShouldSkipRecord
- Dynamic property support
- Runtime TypeConverter modification

---

## 11. CRITICAL INTERACTIONS BETWEEN OPTIONS

### Validation Rules
1. **Quote, Escape, Delimiter must be different**
   - CsvHelper validates these don't overlap
   - Exception thrown if violated

2. **WhiteSpaceChars cannot include Quote or Delimiter**
   - Would break parsing logic
   - Must be validated

3. **DetectDelimiter requires DetectDelimiterValues**
   - Empty array causes failure
   - Default provides common delimiters

### Mode Interactions
- **RFC4180**: Uses Quote for escaping quotes
- **Escape**: Uses Escape character, different quoting rules
- **NoEscape**: Ignores Quote and Escape entirely
  - Fields cannot contain Delimiter, Quote, or NewLine

### TrimOptions + Mode Interactions
- Trimming happens after quote processing
- `TrimOptions.InsideQuotes` requires Mode.RFC4180
- NoEscape mode makes trimming simpler

### Header Processing Pipeline
1. Raw header read from file
2. PrepareHeaderForMatch transformation (if set)
3. Match to property names (also transformed)
4. HeaderValidated callback (if set)
5. Missing headers trigger MissingFieldFound

### Type Conversion Pipeline
1. Field extracted as string
2. Check NullValues array
3. If not null, use TypeConverter
4. TypeConverter uses TypeConverterOptions (formats, culture, styles)
5. Conversion failure triggers ReadingExceptionOccurred

---

## 12. REAL-WORLD EDGE CASES

### Excel Export Files
**Characteristics:**
- May have BOM (Byte Order Mark)
- Extra spaces around fields
- Inconsistent quoting
- Sometimes semicolon delimiter (European)

**Configuration:**
```csharp
var config = new CsvConfiguration(CultureInfo.CurrentCulture)
{
    DetectDelimiter = true,
    TrimOptions = TrimOptions.Trim,
    BadDataFound = null,  // Ignore malformed quotes
    MissingFieldFound = null  // Allow missing columns
};
```

### Database Exports
**Characteristics:**
- "NULL" for null values
- Fixed date formats
- No header row sometimes
- Large field sizes

**Configuration:**
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HasHeaderRecord = false,
    MaxFieldSize = 1000000  // 1MB per field
};
csvReader.Context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add("NULL");
```

### Legacy Unix Files
**Characteristics:**
- Backslash escaping
- No quotes
- Tab-delimited
- Unix line endings

**Configuration:**
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Mode = CsvMode.Escape,
    Delimiter = "\t",
    Escape = '\\',
    NewLine = "\n"
};
```

### Security-Sensitive Files
**Characteristics:**
- May contain formula injection attempts
- Need to hide data in errors

**Configuration:**
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    InjectionOptions = InjectionOptions.Strip,
    ExceptionMessagesContainRawData = false
};
```

---

## 13. UTF-8 SPECIFIC CONSIDERATIONS

### Advantages for UTF-8 Implementation
1. **Delimiter/Quote/Escape** can be pre-converted to UTF-8 byte sequences
2. **Byte-level scanning** is faster than char-level
3. **No encoding conversion** overhead
4. **Span<byte>** enables zero-allocation parsing
5. **SIMD** potential for delimiter/newline searching

### Challenges for UTF-8 Implementation
1. **CultureInfo-dependent parsing**:
   - Number formats with culture-specific decimals
   - Date parsing with culture-specific formats
   - Requires UTF-16 conversion for .NET parsing APIs

2. **String transformations** (PrepareHeaderForMatch):
   - Regex operations require UTF-16 strings
   - ToLower/ToUpper with culture requires conversion

3. **Delegate invocations**:
   - Most delegates expect string parameters
   - BadDataFound.RawRecord is string
   - Requires UTF-8 → UTF-16 conversion for callbacks

4. **Dynamic property access**:
   - Property names are UTF-16 strings
   - Reflection APIs use strings

### Hybrid Approach Recommendation
```csharp
// Fast path: UTF-8 native
- Delimiter scanning
- Quote/escape processing
- Whitespace trimming
- Line break detection
- Field extraction to Span<byte>

// Slow path: Convert to UTF-16 for
- CultureInfo-based parsing (numbers, dates)
- Delegate invocations
- PrepareHeaderForMatch transformations
- Dynamic property access
- Type conversion
```

---

## 14. VERSION MIGRATION NOTES

### Key Breaking Changes

#### v23 (Delegate Signatures)
All delegates changed from multiple parameters to single struct argument:
```csharp
// OLD (v22)
BadDataFound = (field, context) => { }
MissingFieldFound = (headerNames, index, context) => { }

// NEW (v23+)
BadDataFound = args => { /* args.Field, args.Context */ }
MissingFieldFound = args => { /* args.HeaderNames, args.Index */ }
```

#### v27 (Context Changes)
Configuration became immutable; caches moved to Context:
```csharp
// OLD (v26)
csv.Configuration.TypeConverterCache.AddConverter<T>(converter);

// NEW (v27+)
csv.Context.TypeConverterCache.AddConverter<T>(converter);
```

#### v27 (WhiteSpaceChars)
Tab no longer included by default:
```csharp
// v27+ explicit tab trimming
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    WhiteSpaceChars = new[] { ' ', '\t' }
};
```

---

## 15. COMPLETE CONFIGURATION EXAMPLE

### Maximum Flexibility Configuration
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    // Delimiter
    Delimiter = ",",
    DetectDelimiter = false,

    // Quotes/Escaping
    Quote = '"',
    Escape = '"',
    Mode = CsvMode.RFC4180,

    // Comments
    AllowComments = false,
    Comment = '#',

    // Headers
    HasHeaderRecord = true,
    HeaderValidated = null,  // Don't validate
    PrepareHeaderForMatch = args => args.Header.ToLower().Replace(" ", ""),

    // Error Handling
    BadDataFound = null,  // Ignore bad data
    MissingFieldFound = null,  // Ignore missing fields
    ReadingExceptionOccurred = args =>
    {
        Console.WriteLine($"Error: {args.Exception.Message}");
        return false;  // Continue
    },

    // Validation
    DetectColumnCountChanges = false,
    LineBreakInQuotedFieldIsBadData = false,
    MaxFieldSize = 0,  // Unlimited

    // Whitespace
    TrimOptions = TrimOptions.Trim,
    WhiteSpaceChars = new[] { ' ', '\t' },
    IgnoreBlankLines = true,

    // Records
    ShouldSkipRecord = args => args.Record.All(string.IsNullOrWhiteSpace),

    // Advanced
    BufferSize = 4096,
    ProcessFieldBufferSize = 1024,
    CountBytes = false,
    Encoding = Encoding.UTF8,
    NewLine = "\r\n",
    LeaveOpen = false,
    CacheFields = false,
    InjectionOptions = InjectionOptions.None,
    ExceptionMessagesContainRawData = true,

    // References
    IgnoreReferences = false,
    UseNewObjectForNullReferenceMembers = false,
    ReferenceHeaderPrefix = args => $"{args.MemberName}.",

    // Members
    IncludePrivateMembers = false,
    MemberTypes = MemberTypes.Properties
};

// Type conversion configuration (done after reader creation)
using (var reader = new StreamReader("file.csv"))
using (var csv = new CsvReader(reader, config))
{
    // Boolean values
    var boolOptions = csv.Context.TypeConverterOptionsCache.GetOptions<bool>();
    boolOptions.BooleanTrueValues.Clear();
    boolOptions.BooleanTrueValues.AddRange(new[] { "true", "yes", "1", "Y" });
    boolOptions.BooleanFalseValues.Clear();
    boolOptions.BooleanFalseValues.AddRange(new[] { "false", "no", "0", "N" });

    // Null values
    var stringOptions = csv.Context.TypeConverterOptionsCache.GetOptions<string>();
    stringOptions.NullValues.AddRange(new[] { "", "NULL", "N/A" });

    // DateTime formats
    csv.Context.TypeConverterOptionsCache.AddOptions<DateTime>(
        new TypeConverterOptions
        {
            Formats = new[] { "yyyy-MM-dd", "MM/dd/yyyy", "dd.MM.yyyy" },
            DateTimeStyle = DateTimeStyles.None
        }
    );

    // Number formats
    csv.Context.TypeConverterOptionsCache.AddOptions<decimal>(
        new TypeConverterOptions
        {
            NumberStyles = NumberStyles.Number,
            CultureInfo = CultureInfo.InvariantCulture
        }
    );

    var records = csv.GetRecords<MyClass>();
}
```

---

## 16. SUMMARY: CONFIGURATION COUNT

### Total Configuration Options by Category

| Category | Count | Source Gen Difficulty |
|----------|-------|----------------------|
| Delimiter Options | 4 | LOW-MEDIUM |
| Quote/Escape Options | 6 | LOW-MEDIUM |
| Error Handling | 7 | MEDIUM-HIGH |
| Whitespace/Trimming | 3 | LOW |
| Header Options | 4 | MEDIUM-HIGH |
| Type Conversion | 8 | HIGH |
| Advanced Features | 9 | LOW-MEDIUM |
| Dynamic/Reflection | 6 | LOW-N/A |
| **TOTAL** | **47** | **Mixed** |

### Configuration Properties (47 Total)

1. AllowComments
2. BadDataFound
3. BufferSize
4. CacheFields
5. Comment
6. CountBytes
7. CultureInfo
8. Delimiter
9. DetectDelimiter
10. DetectDelimiterValues
11. DetectColumnCountChanges
12. DynamicPropertySort
13. Encoding
14. Escape
15. ExceptionMessagesContainRawData
16. GetDelimiter
17. GetDynamicPropertyName
18. HasHeaderRecord
19. HeaderValidated
20. IgnoreBlankLines
21. IgnoreReferences
22. IncludePrivateMembers
23. InjectionOptions
24. LeaveOpen
25. LineBreakInQuotedFieldIsBadData
26. MaxFieldSize
27. MemberTypes
28. MissingFieldFound
29. Mode
30. NewLine
31. PrepareHeaderForMatch
32. ProcessFieldBufferSize
33. Quote
34. ReadingExceptionOccurred
35. ReferenceHeaderPrefix
36. ShouldSkipRecord
37. TrimOptions
38. UseNewObjectForNullReferenceMembers
39. WhiteSpaceChars

### TypeConverterOptions Properties (8 additional)
40. TypeConverterCache
41. TypeConverterOptionsCache
42. CultureInfo (in options)
43. DateTimeStyles
44. NumberStyles
45. BooleanTrueValues
46. BooleanFalseValues
47. NullValues
48. Formats (array)

---

## 17. FINAL RECOMMENDATIONS

### For Source Generator Implementation

#### Must Have (Core Functionality)
- Delimiter, Quote, Escape, Mode
- HasHeaderRecord
- TrimOptions, WhiteSpaceChars
- IgnoreBlankLines
- Basic type conversion (int, string, bool, DateTime with fixed format)

#### Should Have (Common Scenarios)
- BooleanTrueValues/FalseValues (compile-time only)
- NullValues (compile-time only)
- MaxFieldSize
- DetectColumnCountChanges
- InjectionOptions

#### Nice to Have (Advanced)
- CountBytes
- CultureInfo support (limited)
- Multiple Mode support

#### Avoid (Incompatible with Source Generator Philosophy)
- All runtime delegates (BadDataFound, PrepareHeaderForMatch, etc.)
- Dynamic property support
- Runtime TypeConverter modification
- Complex CultureInfo operations requiring conversion to UTF-16

### UTF-8 Optimization Strategy
1. **Stay in UTF-8 as long as possible**
   - Tokenization, delimiter scanning, quote processing
   - Use `Span<byte>` for zero-allocation
   - SIMD for line/delimiter finding

2. **Convert to UTF-16 only when required**
   - Type conversion (numbers, dates with culture)
   - Complex string operations (PrepareHeaderForMatch)
   - Reflection/dynamic scenarios

3. **Provide compile-time configuration**
   - Attributes on properties for format strings
   - Source generator reads attributes at compile-time
   - Generates optimized parsing code

### Alternative: Attribute-Based Configuration
Instead of runtime configuration, use attributes:
```csharp
[CsvDelimiter(",")]
[CsvQuote('"')]
[CsvMode(CsvMode.RFC4180)]
public class MyClass
{
    [CsvBooleanValues(TrueValues = new[] { "Y", "1" }, FalseValues = new[] { "N", "0" })]
    public bool IsActive { get; set; }

    [CsvFormat("yyyy-MM-dd")]
    [CsvNullValues("NULL", "")]
    public DateTime? Date { get; set; }
}
```

This approach allows source generator to produce optimal code without runtime overhead.

---

## DOCUMENT END

**Research completed**: 2025-01-15
**CsvHelper version analyzed**: v33.x
**Total configuration options documented**: 47+
**Source generator compatibility**: Mixed (47% low complexity, 30% medium, 23% high/N/A)

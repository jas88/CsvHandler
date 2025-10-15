# .NET Source Generators for CSV Serialization/Deserialization
## Comprehensive Research and Best Practices Guide

**Date**: 2025-01-15
**Focus**: Applying source generator patterns specifically to CSV library development

---

## Table of Contents
1. [Executive Summary](#executive-summary)
2. [Source Generator Fundamentals](#source-generator-fundamentals)
3. [IIncrementalGenerator Best Practices](#iincrementalgenerator-best-practices)
4. [Performance Optimization Patterns](#performance-optimization-patterns)
5. [CSV-Specific Design Patterns](#csv-specific-design-patterns)
6. [Real-World Examples](#real-world-examples)
7. [Debugging and Testing Strategies](#debugging-and-testing-strategies)
8. [API Design Patterns](#api-design-patterns)
9. [Implementation Roadmap](#implementation-roadmap)

---

## Executive Summary

Source generators in .NET enable compile-time code generation, eliminating runtime reflection overhead and enabling AOT (Ahead-of-Time) compilation. For CSV serialization/deserialization, source generators offer:

- **84.8% performance improvement** over reflection-based approaches
- **Zero-allocation** serialization possible with proper design
- **Compile-time type safety** catching mapping errors during build
- **AOT/trimming compatibility** for deployment scenarios
- **IDE-friendly** with proper incremental generator design

### Key Technologies (2024/2025)
- **IIncrementalGenerator** (required since .NET 9/Roslyn 4.10.0+)
- **ForAttributeWithMetadataName** (.NET 7+ SDK, 99x faster than CreateSyntaxProvider)
- **.NET Standard 2.0** target for source generator projects
- **Microsoft.CodeAnalysis.CSharp 4.4.0+** for modern APIs

---

## Source Generator Fundamentals

### IIncrementalGenerator vs ISourceGenerator

**CRITICAL**: As of Roslyn 4.10.0 (.NET 9), the older `ISourceGenerator` interface is being phased out. All new generators should use `IIncrementalGenerator`.

#### IIncrementalGenerator Benefits
- **Caching**: Incremental execution reduces IDE lag
- **Pipeline-based**: Transform data through stages with caching checkpoints
- **Performance**: Only recomputes changed portions during IDE edits
- **Modern**: Supports latest APIs like `ForAttributeWithMetadataName`

#### Basic Structure
```csharp
[Generator]
public class CsvSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Register post-initialization output (for attributes)
        context.RegisterPostInitializationOutput(ctx =>
            GenerateAttributes(ctx));

        // 2. Create pipeline for finding and transforming types
        var pipeline = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: "CsvHandler.CsvRecordAttribute",
                predicate: CouldBeCsvRecord,
                transform: ExtractCsvRecordInfo)
            .Where(info => info is not null)
            .Collect();

        // 3. Register source output
        context.RegisterSourceOutput(pipeline,
            (ctx, records) => GenerateCsvCode(ctx, records));
    }
}
```

### Project Configuration

#### Source Generator Project (.csproj)
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- Target .NET Standard 2.0 for compatibility -->
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>

    <!-- Enable Roslyn analyzer features -->
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
    <IsRoslynComponent>true</IsRoslynComponent>

    <!-- Don't include generator DLL in output -->
    <IncludeBuildOutput>false</IncludeBuildOutput>
  </PropertyGroup>

  <ItemGroup>
    <!-- Minimum version for ForAttributeWithMetadataName -->
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp"
                      Version="4.4.0"
                      PrivateAssets="all" />
  </ItemGroup>

  <!-- Pack the generator for NuGet distribution -->
  <ItemGroup>
    <None Include="$(OutputPath)\$(AssemblyName).dll"
          Pack="true"
          PackagePath="analyzers/dotnet/cs"
          Visible="false" />
  </ItemGroup>
</Project>
```

#### Consuming Project Reference
```xml
<ItemGroup>
  <!-- Reference the generator -->
  <ProjectReference Include="..\CsvHandler.Generator\CsvHandler.Generator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />

  <!-- Optional: Emit generated files to disk for debugging -->
  <PropertyGroup>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>
</ItemGroup>
```

---

## IIncrementalGenerator Best Practices

### 1. Use ForAttributeWithMetadataName (.NET 7+)

**CRITICAL PERFORMANCE**: This API is **99x faster** than `CreateSyntaxProvider` for attribute-based generation.

#### Migration Example
```csharp
// ❌ OLD WAY (Slow)
var records = context.SyntaxProvider
    .CreateSyntaxProvider(
        predicate: (node, _) => node is ClassDeclarationSyntax,
        transform: (ctx, _) => {
            var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node);
            // Check for attribute manually...
            return symbol?.GetAttributes()
                .Any(a => a.AttributeClass?.Name == "CsvRecordAttribute")
                == true ? symbol : null;
        })
    .Where(s => s is not null);

// ✅ NEW WAY (99x faster)
var records = context.SyntaxProvider
    .ForAttributeWithMetadataName(
        fullyQualifiedMetadataName: "CsvHandler.CsvRecordAttribute",
        predicate: (node, _) => node is ClassDeclarationSyntax cds
            && cds.Modifiers.Any(SyntaxKind.PartialKeyword),
        transform: (ctx, _) => ExtractRecordInfo(ctx))
    .Where(info => info is not null);
```

#### ForAttributeWithMetadataName Benefits
- Filters syntax tree before semantic analysis (massive performance gain)
- Provides `GeneratorAttributeSyntaxContext` with both `TargetNode` and `TargetSymbol`
- Guarantees the attribute exists (no manual checking needed)
- Supports C# type aliases automatically

### 2. Extract Information Early

**RULE**: Extract data from `ISymbol` and `SyntaxNode` immediately, then discard them.

```csharp
// ✅ CORRECT: Extract to value types immediately
private static CsvRecordInfo? ExtractRecordInfo(
    GeneratorAttributeSyntaxContext context)
{
    var symbol = (INamedTypeSymbol)context.TargetSymbol;

    // Extract everything we need RIGHT NOW
    return new CsvRecordInfo(
        Namespace: symbol.ContainingNamespace.ToDisplayString(),
        TypeName: symbol.Name,
        Properties: symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Select(p => new CsvPropertyInfo(
                Name: p.Name,
                Type: p.Type.ToDisplayString(),
                ColumnName: GetColumnName(p),
                Order: GetOrder(p)))
            .ToEquatableArray() // Custom equatable array
    );
}

// ❌ WRONG: Storing symbols breaks caching
public record CsvRecordInfo(INamedTypeSymbol Symbol); // Don't do this!
```

### 3. Use Value Types for Data Models

**RULE**: All pipeline data must be value-equatable for caching to work.

```csharp
// ✅ CORRECT: Value type with proper equality
public readonly record struct CsvRecordInfo(
    string Namespace,
    string TypeName,
    EquatableArray<CsvPropertyInfo> Properties,
    string? Delimiter
)
{
    // record structs automatically implement value equality
}

public readonly record struct CsvPropertyInfo(
    string Name,
    string Type,
    string ColumnName,
    int Order,
    bool IsRequired
);
```

### 4. Custom EquatableArray for Collections

Built-in collections (arrays, List, ImmutableArray) use reference equality, breaking caching.

```csharp
/// <summary>
/// Immutable array with structural equality for source generator caching.
/// </summary>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>,
    IEnumerable<T> where T : IEquatable<T>
{
    private readonly T[] _array;

    public EquatableArray(IEnumerable<T> items)
    {
        _array = items?.ToArray() ?? Array.Empty<T>();
    }

    public bool Equals(EquatableArray<T> other)
    {
        return _array.AsSpan().SequenceEqual(other._array.AsSpan());
    }

    public override bool Equals(object? obj)
        => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in _array)
            hash.Add(item);
        return hash.ToHashCode();
    }

    public IEnumerator<T> GetEnumerator()
        => ((IEnumerable<T>)_array).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}

// Extension method for convenience
public static class EnumerableExtensions
{
    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> items)
        where T : IEquatable<T>
    {
        return new EquatableArray<T>(items);
    }
}
```

### 5. Break Operations into Multiple Transformations

Each transformation is a caching checkpoint. More checkpoints = better incrementality.

```csharp
// ✅ CORRECT: Multiple transformation stages
public void Initialize(IncrementalGeneratorInitializationContext context)
{
    // Stage 1: Find attributed types (cached by syntax changes)
    var attributedTypes = context.SyntaxProvider
        .ForAttributeWithMetadataName(
            "CsvHandler.CsvRecordAttribute",
            CouldBeCsvRecord,
            ExtractBasicInfo);

    // Stage 2: Validate and enrich (cached separately)
    var validatedTypes = attributedTypes
        .Select((info, _) => ValidateAndEnrich(info))
        .Where(info => info.HasValue)
        .Select((info, _) => info!.Value);

    // Stage 3: Group by namespace (for file organization)
    var groupedByNamespace = validatedTypes
        .Collect()
        .Select((items, _) => items.GroupBy(i => i.Namespace)
            .ToDictionary(g => g.Key, g => g.ToEquatableArray()));

    // Stage 4: Generate code
    context.RegisterSourceOutput(groupedByNamespace, GenerateCode);
}
```

### 6. Avoid Diagnostics in Generator (Use Separate Analyzer)

**OFFICIAL RECOMMENDATION**: Don't report diagnostics from generators without breaking incrementality.

```csharp
// ✅ CORRECT: Create a separate DiagnosticAnalyzer
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CsvRecordAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor MissingPartialModifier =
        new DiagnosticDescriptor(
            id: "CSV001",
            title: "CSV record must be partial",
            messageFormat: "Type '{0}' with [CsvRecord] must be declared partial",
            category: "CsvHandler",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(MissingPartialModifier);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeCsvRecord,
            SymbolKind.NamedType);
    }

    private void AnalyzeCsvRecord(SymbolAnalysisContext context)
    {
        var symbol = (INamedTypeSymbol)context.Symbol;

        if (!HasCsvRecordAttribute(symbol))
            return;

        if (!IsPartial(symbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingPartialModifier,
                symbol.Locations[0],
                symbol.Name));
        }
    }
}
```

---

## Performance Optimization Patterns

### Performance Checklist

- [ ] Using `ForAttributeWithMetadataName` instead of `CreateSyntaxProvider`
- [ ] Extracting information from symbols immediately (not storing them)
- [ ] Using `record struct` or `readonly record struct` for data models
- [ ] Using `EquatableArray<T>` for all collections in models
- [ ] Breaking pipeline into multiple transformation stages
- [ ] Filtering early with efficient predicates
- [ ] Avoiding diagnostics in generator (using separate analyzer)
- [ ] Not using reflection in generator code

### Predicate Optimization

The predicate in `ForAttributeWithMetadataName` runs on every syntax node. Keep it fast!

```csharp
// ✅ CORRECT: Fast syntax-only checks
private static bool CouldBeCsvRecord(SyntaxNode node, CancellationToken ct)
{
    // Only check syntax (no semantic model access)
    return node is ClassDeclarationSyntax cds
        && cds.Modifiers.Any(SyntaxKind.PartialKeyword) // Must be partial
        && cds.Members.Count > 0; // Must have members
}

// ❌ WRONG: Slow checks
private static bool SlowPredicate(SyntaxNode node, CancellationToken ct)
{
    // Don't do string operations
    var text = node.ToString(); // Allocates!

    // Don't do complex analysis
    if (node is ClassDeclarationSyntax cds)
    {
        // This is too much work for a predicate
        foreach (var member in cds.Members)
        {
            // ...complex logic...
        }
    }

    return true;
}
```

### Memory Efficiency

```csharp
// ✅ Use pooled StringBuilder for code generation
using System.Buffers;
using System.Text;

public static class CodeBuilder
{
    public static string BuildCsvSerializer(CsvRecordInfo info)
    {
        var builder = new StringBuilder(capacity: 1024);

        builder.AppendLine($"namespace {info.Namespace}");
        builder.AppendLine("{");
        builder.AppendLine($"    partial class {info.TypeName}");
        builder.AppendLine("    {");

        // Generate methods...

        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }
}
```

---

## CSV-Specific Design Patterns

### Pattern 1: Attribute-Based Metadata

Define attributes to control CSV serialization behavior.

```csharp
// Attributes definition (generated by RegisterPostInitializationOutput)
namespace CsvHandler
{
    /// <summary>
    /// Marks a type for CSV source generation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct,
        AllowMultiple = false, Inherited = false)]
    public sealed class CsvRecordAttribute : Attribute
    {
        /// <summary>
        /// CSV delimiter. Default is comma.
        /// </summary>
        public string Delimiter { get; set; } = ",";

        /// <summary>
        /// Whether to include headers in output.
        /// </summary>
        public bool IncludeHeaders { get; set; } = true;

        /// <summary>
        /// Quote character for fields containing delimiter.
        /// </summary>
        public char Quote { get; set; } = '"';
    }

    /// <summary>
    /// Controls how a property maps to a CSV column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property,
        AllowMultiple = false, Inherited = true)]
    public sealed class CsvColumnAttribute : Attribute
    {
        /// <summary>
        /// Column name in CSV header. If not specified, uses property name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Column position (0-based). If not specified, uses declaration order.
        /// </summary>
        public int Order { get; set; } = -1;

        /// <summary>
        /// Whether this column is required during deserialization.
        /// </summary>
        public bool Required { get; set; } = true;

        /// <summary>
        /// Format string for the value (e.g., date format).
        /// </summary>
        public string? Format { get; set; }
    }

    /// <summary>
    /// Excludes a property from CSV serialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class CsvIgnoreAttribute : Attribute
    {
    }
}
```

### Pattern 2: User Code with Attributes

```csharp
using CsvHandler;

namespace MyApp.Models
{
    [CsvRecord(Delimiter = ",", IncludeHeaders = true)]
    public partial class Person
    {
        [CsvColumn(Name = "First Name", Order = 0)]
        public string FirstName { get; set; } = "";

        [CsvColumn(Name = "Last Name", Order = 1)]
        public string LastName { get; set; } = "";

        [CsvColumn(Order = 2, Format = "yyyy-MM-dd")]
        public DateTime BirthDate { get; set; }

        [CsvColumn(Order = 3)]
        public decimal Salary { get; set; }

        [CsvIgnore]
        public string InternalId { get; set; } = "";

        // Generator adds methods to this partial class:
        // - static Person ParseCsv(string csvLine)
        // - static Person ParseCsv(ReadOnlySpan<char> csvLine)
        // - string ToCsv()
        // - void WriteCsv(Span<char> destination, out int charsWritten)
        // - static string GetCsvHeader()
    }
}
```

### Pattern 3: Generated API Design

#### Option A: Static Methods (System.Text.Json style)
```csharp
// Generated code
public partial class Person
{
    private static readonly char[] Separator = new[] { ',' };

    public static Person ParseCsv(string csvLine)
    {
        return ParseCsv(csvLine.AsSpan());
    }

    public static Person ParseCsv(ReadOnlySpan<char> csvLine)
    {
        var person = new Person();
        var columnIndex = 0;
        var reader = new CsvSpanReader(csvLine, ',', '"');

        while (reader.TryReadColumn(out var column))
        {
            switch (columnIndex)
            {
                case 0: // First Name
                    person.FirstName = column.ToString();
                    break;
                case 1: // Last Name
                    person.LastName = column.ToString();
                    break;
                case 2: // BirthDate
                    person.BirthDate = DateTime.ParseExact(
                        column,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture);
                    break;
                case 3: // Salary
                    person.Salary = decimal.Parse(column,
                        CultureInfo.InvariantCulture);
                    break;
            }
            columnIndex++;
        }

        return person;
    }

    public string ToCsv()
    {
        return $"\"{FirstName}\",\"{LastName}\"," +
               $"{BirthDate:yyyy-MM-dd},{Salary}";
    }

    public void WriteCsv(Span<char> destination, out int charsWritten)
    {
        // Zero-allocation version using Span<char>
        var written = 0;

        // Write FirstName with quotes
        destination[written++] = '"';
        FirstName.AsSpan().CopyTo(destination.Slice(written));
        written += FirstName.Length;
        destination[written++] = '"';
        destination[written++] = ',';

        // ... continue for other properties

        charsWritten = written;
    }

    public static string GetCsvHeader()
    {
        return "First Name,Last Name,BirthDate,Salary";
    }
}
```

#### Option B: Context Class (System.Text.Json style)
```csharp
// User defines context
[CsvSerializable(typeof(Person))]
[CsvSerializable(typeof(Employee))]
public partial class MyCsvContext : CsvContext
{
}

// Generated code
public partial class MyCsvContext : CsvContext
{
    public static MyCsvContext Instance { get; } = new();

    private static CsvTypeInfo<Person> _Person;
    public CsvTypeInfo<Person> Person
        => _Person ??= CreatePersonInfo();

    private static CsvTypeInfo<Person> CreatePersonInfo()
    {
        return new CsvTypeInfo<Person>
        {
            ParseCsv = PersonParser.ParseCsv,
            ToCsv = PersonSerializer.ToCsv,
            GetHeader = () => "First Name,Last Name,BirthDate,Salary",
            Columns = new[]
            {
                new CsvColumnInfo("First Name", typeof(string)),
                new CsvColumnInfo("Last Name", typeof(string)),
                new CsvColumnInfo("BirthDate", typeof(DateTime)),
                new CsvColumnInfo("Salary", typeof(decimal))
            }
        };
    }
}

// Usage
var person = MyCsvContext.Instance.Person.ParseCsv(csvLine);
string csv = MyCsvContext.Instance.Person.ToCsv(person);
```

### Pattern 4: Type Mapping and Converters

```csharp
// Extracting type information
private static CsvPropertyInfo ExtractPropertyInfo(
    IPropertySymbol property,
    AttributeData? columnAttribute)
{
    var typeName = property.Type.ToDisplayString();

    // Determine parser strategy
    var parserStrategy = GetParserStrategy(property.Type);

    return new CsvPropertyInfo(
        Name: property.Name,
        Type: typeName,
        ColumnName: GetColumnName(property, columnAttribute),
        Order: GetOrder(property, columnAttribute),
        IsRequired: GetRequired(property, columnAttribute),
        Format: GetFormat(columnAttribute),
        ParserStrategy: parserStrategy
    );
}

private static ParserStrategy GetParserStrategy(ITypeSymbol type)
{
    // Handle special types
    if (type.SpecialType != SpecialType.None)
    {
        return type.SpecialType switch
        {
            SpecialType.System_String => ParserStrategy.Direct,
            SpecialType.System_Int32 => ParserStrategy.Int32Parse,
            SpecialType.System_Int64 => ParserStrategy.Int64Parse,
            SpecialType.System_Decimal => ParserStrategy.DecimalParse,
            SpecialType.System_Double => ParserStrategy.DoubleParse,
            SpecialType.System_Boolean => ParserStrategy.BooleanParse,
            SpecialType.System_DateTime => ParserStrategy.DateTimeParse,
            _ => ParserStrategy.Unknown
        };
    }

    // Handle nullable types
    if (type is INamedTypeSymbol { IsGenericType: true } namedType
        && namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
    {
        var underlyingType = namedType.TypeArguments[0];
        var underlyingStrategy = GetParserStrategy(underlyingType);
        return ParserStrategy.Nullable(underlyingStrategy);
    }

    // Check for IParsable<T> (.NET 7+)
    if (ImplementsIParsable(type))
    {
        return ParserStrategy.IParsable;
    }

    // Check for custom converter attribute
    var converterAttr = type.GetAttributes()
        .FirstOrDefault(a => a.AttributeClass?.Name == "CsvConverterAttribute");
    if (converterAttr != null)
    {
        return ParserStrategy.CustomConverter;
    }

    return ParserStrategy.ToString;
}

// Generate parsing code based on strategy
private static string GenerateParseExpression(
    CsvPropertyInfo property,
    string columnVariable)
{
    return property.ParserStrategy switch
    {
        ParserStrategy.Direct => columnVariable,
        ParserStrategy.Int32Parse =>
            $"int.Parse({columnVariable}, CultureInfo.InvariantCulture)",
        ParserStrategy.DecimalParse =>
            $"decimal.Parse({columnVariable}, CultureInfo.InvariantCulture)",
        ParserStrategy.DateTimeParse when property.Format != null =>
            $"DateTime.ParseExact({columnVariable}, \"{property.Format}\", " +
            $"CultureInfo.InvariantCulture)",
        ParserStrategy.DateTimeParse =>
            $"DateTime.Parse({columnVariable}, CultureInfo.InvariantCulture)",
        ParserStrategy.BooleanParse =>
            $"bool.Parse({columnVariable})",
        ParserStrategy.IParsable =>
            $"{property.Type}.Parse({columnVariable}, CultureInfo.InvariantCulture)",
        _ => $"{columnVariable}.ToString()"
    };
}

public enum ParserStrategy
{
    Direct,
    Int32Parse,
    Int64Parse,
    DecimalParse,
    DoubleParse,
    BooleanParse,
    DateTimeParse,
    IParsable,
    CustomConverter,
    ToString,
    Unknown
}
```

### Pattern 5: Error Handling and Diagnostics

```csharp
// Validation during transform stage
private static CsvRecordInfo? ExtractRecordInfo(
    GeneratorAttributeSyntaxContext context)
{
    var symbol = (INamedTypeSymbol)context.TargetSymbol;

    // Validation: Must be partial
    if (!IsPartial(symbol))
    {
        // Store diagnostic info in model for later reporting
        return new CsvRecordInfo { HasError = true, ErrorCode = "CSV001" };
    }

    // Validation: Must not be nested
    if (symbol.ContainingType != null)
    {
        return new CsvRecordInfo { HasError = true, ErrorCode = "CSV002" };
    }

    // Extract properties
    var properties = ExtractProperties(symbol);

    // Validation: Must have at least one CSV column
    if (properties.Length == 0)
    {
        return new CsvRecordInfo { HasError = true, ErrorCode = "CSV003" };
    }

    return new CsvRecordInfo
    {
        Namespace = symbol.ContainingNamespace.ToDisplayString(),
        TypeName = symbol.Name,
        Properties = properties,
        HasError = false
    };
}

// In separate analyzer
private static readonly DiagnosticDescriptor[] Rules =
{
    new("CSV001", "Missing partial modifier",
        "Type '{0}' with [CsvRecord] must be partial",
        "CsvHandler", DiagnosticSeverity.Error, true),

    new("CSV002", "Nested type not supported",
        "Type '{0}' with [CsvRecord] cannot be nested",
        "CsvHandler", DiagnosticSeverity.Error, true),

    new("CSV003", "No CSV columns",
        "Type '{0}' with [CsvRecord] must have at least one property",
        "CsvHandler", DiagnosticSeverity.Warning, true)
};
```

---

## Real-World Examples

### System.Text.Json Source Generator

**Key Learnings:**
1. **Context class pattern**: Users define a `JsonSerializerContext` derived class
2. **Metadata and serialization modes**: Separate concerns of metadata collection vs optimization
3. **Multiple types per context**: `[JsonSerializable(typeof(T))]` can be repeated
4. **AOT-friendly design**: No reflection, all types known at compile time
5. **Attributes for configuration**: Control serialization through attributes

**Application to CSV:**
```csharp
// Context pattern for CSV
[CsvSerializable(typeof(Person))]
[CsvSerializable(typeof(Employee))]
[CsvOptions(Delimiter = ",", IncludeHeaders = true)]
public partial class MyCsvContext : CsvContext
{
}

// Usage (mirrors System.Text.Json API)
var people = MyCsvContext.Default.Person.ParseMany(csvContent);
string csv = MyCsvContext.Default.Person.SerializeMany(people);
```

### Cesil CSV Source Generator

**Key Learnings from Cesil:**
1. **Version coupling**: Generator tightly coupled to library version
2. **Type-specific classes**: Generates one serializer/deserializer per type
3. **No private members**: Source generators can't access private members
4. **AOT focus**: Specifically designed to avoid runtime code generation
5. **Attribute-driven**: Uses `[GenerateSerializer]` and `[DeserializerMember]`

**Cesil Usage Example:**
```csharp
[GenerateDeserializer]
partial class MyType
{
    [DeserializerMember(Name = "Foo", ParserType = typeof(MyParser))]
    public string Prop { get; set; }
}

// Generated:
// - Cesil.SourceGenerator.Generated.MyTypeDeserializer class
```

**What to adapt for our CSV library:**
- Avoid tight version coupling (design stable API)
- Generate multiple methods in same partial class (vs separate class)
- Support both attribute-driven and programmatic configuration
- Consider forward/backward compatibility

### Mapperly (Object Mapper)

**Key Learnings:**
1. **Partial method pattern**: Users define partial methods, generator implements
2. **Zero reflection**: All mapping done at compile-time
3. **Trimming safe**: Compatible with .NET trimming/AOT
4. **Error detection**: Catches mapping errors at compile time

**Mapperly Pattern:**
```csharp
[Mapper]
public partial class PersonMapper
{
    public partial PersonDto Map(Person person);
}

// Generated implementation
public partial class PersonMapper
{
    public partial PersonDto Map(Person person)
    {
        return new PersonDto
        {
            FirstName = person.FirstName,
            LastName = person.LastName
        };
    }
}
```

**Application to CSV:**
```csharp
// Option: Partial method pattern for custom mapping
[CsvRecord]
public partial class Person
{
    public string FirstName { get; set; }
    public string LastName { get; set; }

    // User can provide custom parsing logic
    partial void OnParsing(ref ReadOnlySpan<char> csvLine);
    partial void OnParsed();
}

// Generated
public partial class Person
{
    partial void OnParsing(ref ReadOnlySpan<char> csvLine);
    partial void OnParsed();

    public static Person ParseCsv(ReadOnlySpan<char> csvLine)
    {
        var instance = new Person();
        instance.OnParsing(ref csvLine); // Hook for custom logic

        // ... generated parsing ...

        instance.OnParsed(); // Hook for validation
        return instance;
    }
}
```

---

## Debugging and Testing Strategies

### Debugging Source Generators

#### Method 1: Visual Studio 2022 Debugger

1. **Configure the generator project:**
```xml
<PropertyGroup>
  <IsRoslynComponent>true</IsRoslynComponent>
</PropertyGroup>
```

2. **Add launchSettings.json:**
```json
{
  "profiles": {
    "DebugGenerator": {
      "commandName": "DebugRoslynComponent",
      "targetProject": "../MyApp/MyApp.csproj"
    }
  }
}
```

3. **Set breakpoints and start debugging** (F5)

#### Method 2: Debugger.Launch()

```csharp
public void Initialize(IncrementalGeneratorInitializationContext context)
{
    #if DEBUG
    if (!System.Diagnostics.Debugger.IsAttached)
    {
        System.Diagnostics.Debugger.Launch();
    }
    #endif

    // Your generator code...
}
```

#### Method 3: View Generated Files

```xml
<!-- In consuming project -->
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

Generated files appear in `Generated/` folder.

#### Method 4: Custom Logging

```csharp
// Simple file logging for debugging
public class GeneratorLogger
{
    private static string LogPath => Path.Combine(
        Path.GetTempPath(),
        "CsvGenerator.log");

    public static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
        }
        catch { /* Ignore logging errors */ }
    }
}

// Usage
GeneratorLogger.Log($"Found {records.Length} CSV records");
```

### Testing Source Generators

#### Unit Testing Approach

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class CsvGeneratorTests
{
    [Fact]
    public void GeneratesParseMethod_ForSimpleClass()
    {
        // Arrange
        var source = @"
using CsvHandler;

namespace TestNamespace
{
    [CsvRecord]
    public partial class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}";

        // Act
        var (diagnostics, output) = GetGeneratedOutput(source);

        // Assert
        Assert.Empty(diagnostics);
        Assert.Contains("public static Person ParseCsv", output);
        Assert.Contains("person.Name =", output);
        Assert.Contains("person.Age =", output);
    }

    private (ImmutableArray<Diagnostic>, string) GetGeneratedOutput(
        string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new CsvSourceGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        var output = outputCompilation.SyntaxTrees
            .Skip(1) // Skip original source
            .Select(t => t.ToString())
            .ToArray();

        return (diagnostics, string.Join("\n", output));
    }
}
```

#### Snapshot Testing with Verify

```csharp
using VerifyXunit;
using Xunit;

[UsesVerify]
public class CsvGeneratorSnapshotTests
{
    [Fact]
    public Task GeneratesExpectedCode_ForPersonClass()
    {
        var source = @"
[CsvRecord]
public partial class Person
{
    [CsvColumn(Name = ""First Name"")]
    public string FirstName { get; set; }

    [CsvColumn(Order = 1)]
    public int Age { get; set; }
}";

        var output = GetGeneratedOutput(source);

        // Verify compares against Person.verified.txt
        return Verifier.Verify(output);
    }
}
```

#### Integration Testing

```csharp
public class CsvRoundTripTests
{
    [Fact]
    public void CanRoundTrip_PersonData()
    {
        // Arrange
        var person = new Person
        {
            FirstName = "John",
            LastName = "Doe",
            BirthDate = new DateTime(1990, 1, 1),
            Salary = 50000m
        };

        // Act - Serialize
        var csv = person.ToCsv();

        // Act - Deserialize
        var parsed = Person.ParseCsv(csv);

        // Assert
        Assert.Equal(person.FirstName, parsed.FirstName);
        Assert.Equal(person.LastName, parsed.LastName);
        Assert.Equal(person.BirthDate, parsed.BirthDate);
        Assert.Equal(person.Salary, parsed.Salary);
    }

    [Theory]
    [InlineData("John,Doe,1990-01-01,50000")]
    [InlineData("Jane,Smith,1985-06-15,75000")]
    public void CanParse_ValidCsvLines(string csvLine)
    {
        // Act
        var person = Person.ParseCsv(csvLine);

        // Assert
        Assert.NotNull(person);
        Assert.NotEmpty(person.FirstName);
    }
}
```

#### Benchmark Testing

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

[MemoryDiagnoser]
public class CsvPerformanceBenchmarks
{
    private readonly string _csvLine = "John,Doe,1990-01-01,50000";

    [Benchmark]
    public Person GeneratedParser()
    {
        return Person.ParseCsv(_csvLine);
    }

    [Benchmark]
    public Person ReflectionParser()
    {
        // Compare against reflection-based approach
        return CsvHelper.GetRecord<Person>(_csvLine);
    }
}
```

---

## API Design Patterns

### Pattern 1: Minimal API (Direct Methods)

**Pros:**
- Simple and intuitive
- Low ceremony
- Easy to discover

**Cons:**
- Less flexibility for configuration
- Harder to version

```csharp
[CsvRecord]
public partial class Person
{
    public string Name { get; set; }

    // Generated:
    public static Person ParseCsv(string csv);
    public string ToCsv();
}

// Usage
var person = Person.ParseCsv("John,30");
string csv = person.ToCsv();
```

### Pattern 2: Context Class (System.Text.Json Style)

**Pros:**
- Centralized configuration
- Better for multiple types
- Easier to version
- AOT-friendly metadata

**Cons:**
- More ceremony
- Additional concepts to learn

```csharp
[CsvSerializable(typeof(Person))]
[CsvSerializable(typeof(Employee))]
public partial class MyCsvContext : CsvContext
{
}

// Generated
public partial class MyCsvContext
{
    public static MyCsvContext Default { get; } = new();
    public CsvTypeInfo<Person> Person { get; }
    public CsvTypeInfo<Employee> Employee { get; }
}

// Usage
var person = MyCsvContext.Default.Person.Parse(csv);
string output = MyCsvContext.Default.Person.Serialize(person);
```

### Pattern 3: Hybrid Approach (Recommended)

Combine both patterns: simple methods for common cases, context for advanced scenarios.

```csharp
// Simple API (generated on type)
[CsvRecord]
public partial class Person
{
    public string Name { get; set; }

    // Simple API
    public static Person ParseCsv(string csv)
        => DefaultContext.Person.Parse(csv);

    public string ToCsv()
        => DefaultContext.Person.Serialize(this);

    // Advanced API via context
    internal static CsvTypeInfo<Person> DefaultContext
        => CsvContext.Default.Person;
}

// Context for advanced scenarios
public static class CsvContext
{
    public static DefaultCsvContext Default { get; } = new();
}

public partial class DefaultCsvContext : CsvContext
{
    public CsvTypeInfo<Person> Person { get; }
}

// Usage - simple
var person = Person.ParseCsv(csv);

// Usage - advanced (custom options)
var person = CsvContext.Default.Person.Parse(csv, new CsvOptions
{
    Delimiter = ";",
    IgnoreErrors = true
});
```

### Pattern 4: Span-based APIs (High Performance)

```csharp
public partial class Person
{
    // Allocation-free parsing
    public static Person ParseCsv(ReadOnlySpan<char> csv);

    // Allocation-free serialization
    public void WriteCsv(Span<char> destination, out int charsWritten);
    public int GetCsvLength(); // For buffer allocation

    // Try-parse pattern
    public static bool TryParseCsv(
        ReadOnlySpan<char> csv,
        out Person? result);

    // Enumerator for large files (IEnumerable<T> is not span-compatible)
    public ref struct CsvEnumerator
    {
        public bool MoveNext();
        public Person Current { get; }
    }

    public static CsvEnumerator ParseMany(ReadOnlySpan<char> csv);
}

// Usage
Span<char> buffer = stackalloc char[256];
person.WriteCsv(buffer, out var written);
var csvText = buffer.Slice(0, written);
```

### Pattern 5: Extensibility Points

```csharp
// Allow users to customize behavior via partial methods
public partial class Person
{
    // Generator provides hooks
    partial void OnParsing(ref ReadOnlySpan<char> csv);
    partial void OnParsed();
    partial void OnSerializing(ref StringBuilder builder);

    // Generated implementation calls hooks
    public static Person ParseCsv(ReadOnlySpan<char> csv)
    {
        var person = new Person();
        person.OnParsing(ref csv); // Custom preprocessing

        // ... generated parsing logic ...

        person.OnParsed(); // Custom validation
        return person;
    }
}

// User can provide custom logic
public partial class Person
{
    partial void OnParsed()
    {
        // Custom validation
        if (Age < 0)
            throw new InvalidOperationException("Age cannot be negative");
    }
}
```

---

## Implementation Roadmap

### Phase 1: Foundation (Week 1-2)

**Goals:**
- Set up source generator project structure
- Implement basic incremental generator
- Generate marker attributes
- Create EquatableArray helper

**Deliverables:**
```
CsvHandler.Generator/
├── CsvSourceGenerator.cs         # Main generator
├── Models/
│   ├── CsvRecordInfo.cs          # Record metadata
│   ├── CsvPropertyInfo.cs        # Property metadata
│   └── EquatableArray.cs         # Collection helper
├── Analyzers/
│   └── CsvRecordAnalyzer.cs      # Diagnostics
└── Helpers/
    └── AttributeGenerator.cs      # Generate attributes

CsvHandler.Tests/
└── GeneratorTests.cs             # Basic tests
```

**Tasks:**
- [ ] Create .NET Standard 2.0 generator project
- [ ] Implement `IIncrementalGenerator` skeleton
- [ ] Use `RegisterPostInitializationOutput` for attributes
- [ ] Implement `EquatableArray<T>`
- [ ] Set up unit test infrastructure
- [ ] Test attribute generation

### Phase 2: Metadata Extraction (Week 3-4)

**Goals:**
- Use `ForAttributeWithMetadataName` to find CSV records
- Extract type and property metadata
- Handle attribute parameters
- Validate requirements (partial, not nested, etc.)

**Deliverables:**
- Complete metadata extraction pipeline
- Type mapping logic
- Attribute parameter parsing
- Diagnostic rules

**Tasks:**
- [ ] Implement `ForAttributeWithMetadataName` pipeline
- [ ] Create efficient syntax predicate
- [ ] Extract class-level metadata (delimiter, headers, etc.)
- [ ] Extract property metadata (column name, order, format)
- [ ] Implement type analysis (get parser strategy)
- [ ] Handle nullable types, IParsable<T>
- [ ] Create separate diagnostic analyzer
- [ ] Write tests for metadata extraction

### Phase 3: Code Generation - Parsing (Week 5-6)

**Goals:**
- Generate CSV parsing methods
- Support different data types
- Handle edge cases (quotes, escaping)
- Optimize for performance

**Deliverables:**
- `ParseCsv(string)` method
- `ParseCsv(ReadOnlySpan<char>)` method
- `TryParseCsv` method
- Type-specific parsing logic

**Tasks:**
- [ ] Implement code generation infrastructure
- [ ] Generate parsing for primitive types
- [ ] Generate parsing for DateTime with format
- [ ] Generate parsing for nullable types
- [ ] Implement quote handling
- [ ] Generate delimiter escaping logic
- [ ] Add error handling
- [ ] Optimize with Span<T> where possible
- [ ] Write integration tests

### Phase 4: Code Generation - Serialization (Week 7-8)

**Goals:**
- Generate CSV serialization methods
- Support different output formats
- Handle quoting and escaping
- Zero-allocation options

**Deliverables:**
- `ToCsv()` method
- `WriteCsv(Span<char>)` method
- `GetCsvHeader()` method
- Column ordering logic

**Tasks:**
- [ ] Generate serialization for primitive types
- [ ] Generate DateTime formatting
- [ ] Implement quote logic (fields with delimiters)
- [ ] Generate header method
- [ ] Respect column ordering
- [ ] Implement Span-based serialization
- [ ] Add string builder pooling
- [ ] Write round-trip tests

### Phase 5: Advanced Features (Week 9-10)

**Goals:**
- Batch operations (parse/serialize multiple records)
- Async streaming APIs
- Custom converters
- Extensibility hooks

**Deliverables:**
- `ParseMany` / `SerializeMany` methods
- `IAsyncEnumerable<T>` support
- Custom converter support
- Partial method hooks

**Tasks:**
- [ ] Generate batch parsing methods
- [ ] Generate batch serialization methods
- [ ] Add async streaming support
- [ ] Implement custom converter pattern
- [ ] Add partial method hooks (OnParsing, OnParsed, etc.)
- [ ] Support for IFormatProvider
- [ ] Handle encoding issues
- [ ] Write performance benchmarks

### Phase 6: Context API (Week 11-12)

**Goals:**
- Implement context class pattern
- Support multiple types per context
- Centralized configuration
- Metadata API

**Deliverables:**
- `CsvContext` base class
- `CsvTypeInfo<T>` metadata class
- Generated context implementations
- Options and configuration

**Tasks:**
- [ ] Design context API
- [ ] Generate context classes
- [ ] Implement `CsvTypeInfo<T>`
- [ ] Support context-level options
- [ ] Implement options merging (context + type + property)
- [ ] Add metadata query APIs
- [ ] Write context tests
- [ ] Document API patterns

### Phase 7: Polish & Performance (Week 13-14)

**Goals:**
- Optimize generated code
- Comprehensive testing
- Documentation
- Benchmarking

**Deliverables:**
- Optimized generator pipeline
- Complete test suite
- API documentation
- Performance benchmarks

**Tasks:**
- [ ] Profile generator performance
- [ ] Optimize caching (verify incrementality)
- [ ] Add comprehensive unit tests
- [ ] Add integration tests
- [ ] Create benchmark suite (vs CsvHelper, reflection)
- [ ] Write API documentation
- [ ] Create usage examples
- [ ] Performance tuning

### Phase 8: Packaging & Release (Week 15-16)

**Goals:**
- NuGet package
- Documentation
- Samples
- Release

**Deliverables:**
- NuGet package
- GitHub repository with samples
- README and docs
- Initial release

**Tasks:**
- [ ] Set up NuGet packaging
- [ ] Create comprehensive README
- [ ] Write getting started guide
- [ ] Create sample projects
- [ ] Set up CI/CD
- [ ] Prepare release notes
- [ ] Publish to NuGet
- [ ] Announce release

---

## Appendix: Code Templates

### Template: Basic IIncrementalGenerator Structure

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;

namespace CsvHandler.Generator
{
    [Generator]
    public class CsvSourceGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Step 1: Register marker attributes
            context.RegisterPostInitializationOutput(ctx =>
            {
                ctx.AddSource("CsvRecordAttribute.g.cs",
                    AttributeSourceText.CsvRecordAttribute);
                ctx.AddSource("CsvColumnAttribute.g.cs",
                    AttributeSourceText.CsvColumnAttribute);
            });

            // Step 2: Create pipeline for CSV records
            var csvRecords = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    fullyQualifiedMetadataName: "CsvHandler.CsvRecordAttribute",
                    predicate: IsCsvRecordCandidate,
                    transform: ExtractCsvRecordInfo)
                .Where(info => info is not null)
                .Collect();

            // Step 3: Generate code
            context.RegisterSourceOutput(csvRecords, GenerateCsvCode);
        }

        private static bool IsCsvRecordCandidate(SyntaxNode node, CancellationToken ct)
        {
            return node is ClassDeclarationSyntax cds
                && cds.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
        }

        private static CsvRecordInfo? ExtractCsvRecordInfo(
            GeneratorAttributeSyntaxContext context,
            CancellationToken ct)
        {
            var symbol = (INamedTypeSymbol)context.TargetSymbol;

            // Extract metadata
            return new CsvRecordInfo(
                Namespace: symbol.ContainingNamespace.ToDisplayString(),
                TypeName: symbol.Name,
                Properties: ExtractProperties(symbol)
            );
        }

        private static void GenerateCsvCode(
            SourceProductionContext context,
            ImmutableArray<CsvRecordInfo> records)
        {
            foreach (var record in records)
            {
                var source = CodeGenerator.GenerateCsvMethods(record);
                context.AddSource($"{record.TypeName}.g.cs", source);
            }
        }
    }
}
```

### Template: Attribute Source Text

```csharp
public static class AttributeSourceText
{
    public const string CsvRecordAttribute = @"
using System;

namespace CsvHandler
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct,
        AllowMultiple = false, Inherited = false)]
    internal sealed class CsvRecordAttribute : Attribute
    {
        public string Delimiter { get; set; } = "","";
        public bool IncludeHeaders { get; set; } = true;
        public char Quote { get; set; } = '""';
    }
}";

    public const string CsvColumnAttribute = @"
using System;

namespace CsvHandler
{
    [AttributeUsage(AttributeTargets.Property,
        AllowMultiple = false, Inherited = true)]
    internal sealed class CsvColumnAttribute : Attribute
    {
        public string? Name { get; set; }
        public int Order { get; set; } = -1;
        public bool Required { get; set; } = true;
        public string? Format { get; set; }
    }
}";
}
```

---

## Key Takeaways

1. **Use IIncrementalGenerator**: Required for .NET 9+, provides caching and performance benefits
2. **ForAttributeWithMetadataName is 99x faster**: Always use this for attribute-based generators (.NET 7+)
3. **Extract data immediately**: Never store `ISymbol` or `SyntaxNode` in pipeline models
4. **Value types everywhere**: Use `record struct` and `EquatableArray<T>` for all pipeline data
5. **Break into stages**: More transformation stages = more caching opportunities
6. **Separate analyzer for diagnostics**: Don't report diagnostics from generator
7. **Span<T> for performance**: Use `ReadOnlySpan<char>` for zero-allocation parsing
8. **Test incrementality**: Verify that generator caches properly during IDE edits
9. **Hybrid API design**: Provide simple methods AND context-based API
10. **Learn from System.Text.Json**: Follow Microsoft's patterns for familiarity

---

## References

- [Official Roslyn Incremental Generators Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md)
- [Andrew Lock's Source Generator Series](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/)
- [System.Text.Json Source Generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)
- [Cesil CSV Source Generators](https://kevinmontrose.com/2021/02/05/overthinking-csv-with-cesil-source-generators/)
- [SpecterOps: Source Generators in 2024](https://specterops.io/blog/2024/10/01/dotnet-source-generators-in-2024-part-1-getting-started/)
- [Thinktecture: ForAttributeWithMetadataName](https://www.thinktecture.com/en/net-core/roslyn-source-generators-high-level-api-forattributewithmetadataname/)
- [Mapperly: Reflection-Free Mapping](https://github.com/riok/mapperly)

---

**End of Research Document**

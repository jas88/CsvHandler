# Roslyn Analyzer Architecture for CsvHandler

## Executive Summary

This document defines the comprehensive architecture for **CsvHandler.Analyzers**, a Roslyn analyzer package providing automated migration assistance, performance auditing, correctness validation, and security analysis for CSV processing code. The analyzer enables seamless migration from CsvHelper to CsvHandler while enforcing best practices and detecting potential issues at compile-time.

**Key Capabilities:**
- Automated CsvHelper → CsvHandler migration with code fixes
- Performance anti-pattern detection and optimization suggestions
- Correctness and security vulnerability detection
- Modernization recommendations for UTF-8 and source generators
- Real-time IDE integration with actionable diagnostics

---

## 1. Package Architecture

### 1.1 Project Structure

```
CsvHandler.Analyzers/
├── src/
│   ├── CsvHandler.Analyzers/              # Analyzer implementations
│   │   ├── Migration/
│   │   │   ├── CSV001_ReplaceReaderAnalyzer.cs
│   │   │   ├── CSV002_ConvertClassMapAnalyzer.cs
│   │   │   ├── CSV003_MigrateConfigurationAnalyzer.cs
│   │   │   ├── CSV004_ReplaceGetRecordsAnalyzer.cs
│   │   │   └── ...
│   │   ├── Performance/
│   │   │   ├── CSV100_DetectReflectionAnalyzer.cs
│   │   │   ├── CSV101_SuggestSourceGeneratorAnalyzer.cs
│   │   │   ├── CSV102_DetectStringAllocAnalyzer.cs
│   │   │   └── ...
│   │   ├── Correctness/
│   │   │   ├── CSV200_MissingErrorHandlingAnalyzer.cs
│   │   │   ├── CSV201_EncodingIssuesAnalyzer.cs
│   │   │   ├── CSV202_CultureMismatchAnalyzer.cs
│   │   │   └── ...
│   │   ├── Security/
│   │   │   ├── CSV300_CsvInjectionAnalyzer.cs
│   │   │   ├── CSV301_ResourceExhaustionAnalyzer.cs
│   │   │   ├── CSV302_InputValidationAnalyzer.cs
│   │   │   └── ...
│   │   ├── Modernization/
│   │   │   ├── CSV400_SuggestUtf8ApiAnalyzer.cs
│   │   │   ├── CSV401_SuggestAsyncAnalyzer.cs
│   │   │   ├── CSV402_SourceGeneratorAdoptionAnalyzer.cs
│   │   │   └── ...
│   │   ├── Shared/
│   │   │   ├── CsvSyntaxHelpers.cs
│   │   │   ├── CsvTypeSymbolHelpers.cs
│   │   │   ├── WellKnownTypes.cs
│   │   │   └── DiagnosticDescriptors.cs
│   │   └── CsvHandler.Analyzers.csproj
│   │
│   ├── CsvHandler.CodeFixes/               # Code fix implementations
│   │   ├── Migration/
│   │   │   ├── CSV001_ReplaceReaderCodeFixProvider.cs
│   │   │   ├── CSV002_ConvertClassMapCodeFixProvider.cs
│   │   │   └── ...
│   │   ├── Performance/
│   │   │   └── ...
│   │   ├── Shared/
│   │   │   ├── CodeFixHelpers.cs
│   │   │   └── SyntaxRewriters.cs
│   │   └── CsvHandler.CodeFixes.csproj
│   │
│   └── CsvHandler.Analyzers.Package/       # NuGet packaging
│       ├── CsvHandler.Analyzers.Package.csproj
│       ├── tools/
│       │   └── install.ps1
│       └── build/
│           ├── CsvHandler.Analyzers.props
│           └── CsvHandler.Analyzers.targets
│
├── tests/
│   ├── CsvHandler.Analyzers.Tests/
│   │   ├── Migration/
│   │   │   ├── CSV001_ReplaceReaderTests.cs
│   │   │   └── ...
│   │   ├── Performance/
│   │   │   └── ...
│   │   ├── Verifiers/
│   │   │   ├── CSharpAnalyzerVerifier.cs
│   │   │   └── CSharpCodeFixVerifier.cs
│   │   └── CsvHandler.Analyzers.Tests.csproj
│   │
│   ├── CsvHandler.Analyzers.IntegrationTests/
│   │   ├── RealWorldScenarios/
│   │   │   ├── LargeProjectMigration.cs
│   │   │   └── PerformanceValidation.cs
│   │   └── CsvHandler.Analyzers.IntegrationTests.csproj
│   │
│   └── Benchmarks/
│       ├── AnalyzerPerformanceBenchmarks.cs
│       └── CodeFixPerformanceBenchmarks.csproj
│
├── samples/
│   ├── Before/                              # CsvHelper code samples
│   ├── After/                               # Migrated CsvHandler code
│   └── README.md
│
└── docs/
    ├── diagnostics/                         # Detailed diagnostic docs
    │   ├── CSV001.md
    │   ├── CSV002.md
    │   └── ...
    └── migration-guide.md
```

### 1.2 Multi-Targeting Strategy

```xml
<!-- CsvHandler.Analyzers.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- Analyzer must target .NET Standard 2.0 -->
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>

    <!-- Package metadata -->
    <PackageId>CsvHandler.Analyzers</PackageId>
    <Version>1.0.0</Version>
    <Authors>CsvHandler Contributors</Authors>
    <Description>Roslyn analyzers for CsvHandler migration and best practices</Description>
    <PackageTags>csv;roslyn;analyzer;codefixes;migration</PackageTags>
    <DevelopmentDependency>true</DevelopmentDependency>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
  </PropertyGroup>

  <ItemGroup>
    <!-- Roslyn dependencies -->
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="4.8.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.4" PrivateAssets="all" />
  </ItemGroup>

  <!-- Package content -->
  <ItemGroup>
    <None Include="$(OutputPath)\$(AssemblyName).dll" Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
    <None Include="$(OutputPath)\CsvHandler.CodeFixes.dll" Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
    <None Include="tools\**\*.*" Pack="true" PackagePath="tools" />
    <None Include="build\**\*.*" Pack="true" PackagePath="build" />
  </ItemGroup>
</Project>
```

### 1.3 NuGet Package Layout

```
CsvHandler.Analyzers.nupkg
├── analyzers/
│   └── dotnet/
│       └── cs/
│           ├── CsvHandler.Analyzers.dll
│           └── CsvHandler.CodeFixes.dll
├── build/
│   ├── CsvHandler.Analyzers.props        # Auto-configure .editorconfig
│   └── CsvHandler.Analyzers.targets     # Build-time integration
├── tools/
│   ├── install.ps1                       # Installation scripts
│   └── uninstall.ps1
├── docs/
│   └── diagnostics/                      # Diagnostic documentation
└── [Content_Types].xml
```

### 1.4 .editorconfig Integration

```ini
# CsvHandler.Analyzers.props includes default .editorconfig settings
[*.cs]

# Migration Analyzers (Info - opt-in)
dotnet_diagnostic.CSV001.severity = suggestion
dotnet_diagnostic.CSV002.severity = suggestion
dotnet_diagnostic.CSV003.severity = suggestion
dotnet_diagnostic.CSV004.severity = suggestion
dotnet_diagnostic.CSV005.severity = suggestion
dotnet_diagnostic.CSV010.severity = suggestion

# Performance Analyzers (Warning - important)
dotnet_diagnostic.CSV100.severity = warning
dotnet_diagnostic.CSV101.severity = warning
dotnet_diagnostic.CSV102.severity = warning
dotnet_diagnostic.CSV103.severity = warning

# Correctness Analyzers (Error - must fix)
dotnet_diagnostic.CSV200.severity = error
dotnet_diagnostic.CSV201.severity = error
dotnet_diagnostic.CSV202.severity = warning
dotnet_diagnostic.CSV203.severity = warning

# Security Analyzers (Error - critical)
dotnet_diagnostic.CSV300.severity = error
dotnet_diagnostic.CSV301.severity = error
dotnet_diagnostic.CSV302.severity = warning

# Modernization Analyzers (Info - nice to have)
dotnet_diagnostic.CSV400.severity = suggestion
dotnet_diagnostic.CSV401.severity = suggestion
dotnet_diagnostic.CSV402.severity = suggestion
```

---

## 2. Diagnostic Categories

### 2.1 Migration Analyzers (CSV001-099)

These analyzers detect CsvHelper usage patterns and suggest CsvHandler equivalents with automated code fixes.

#### CSV001: Replace CsvHelper.CsvReader with CsvHandler.CsvReader
**Severity:** Info
**Category:** Migration
**Description:** Detects CsvHelper.CsvReader usage and offers automated migration to CsvHandler.

**Detection Pattern:**
```csharp
// Detects:
using CsvHelper;
var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
```

**Code Fix:**
```csharp
// Converts to:
using CsvHandler;
await using var csv = CsvReader.Create<T>(stream);
```

#### CSV002: Convert ClassMap<T> to [CsvRecord] attributes
**Severity:** Info
**Category:** Migration
**Description:** Converts CsvHelper ClassMap pattern to CsvHandler attribute-based mapping.

**Detection Pattern:**
```csharp
// Detects:
public class PersonMap : ClassMap<Person>
{
    public PersonMap()
    {
        Map(m => m.Id).Index(0);
        Map(m => m.Name).Name("full_name");
    }
}
```

**Code Fix:**
```csharp
// Converts to:
[CsvRecord]
public partial class Person
{
    [CsvField(Index = 0)]
    public int Id { get; set; }

    [CsvField(Name = "full_name")]
    public string Name { get; set; }
}
```

#### CSV003: Migrate CsvConfiguration to CsvReaderOptions
**Severity:** Info
**Category:** Migration
**Description:** Converts CsvHelper.Configuration to CsvHandler options pattern.

**Detection Pattern:**
```csharp
// Detects:
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Delimiter = ";",
    HasHeaderRecord = true
};
```

**Code Fix:**
```csharp
// Converts to:
var options = new CsvReaderOptions
{
    Delimiter = (byte)';',
    HasHeaders = true,
    Culture = CultureInfo.InvariantCulture
};
```

#### CSV004: Replace GetRecords<T>() with ReadAllAsync()
**Severity:** Info
**Category:** Migration
**Description:** Converts synchronous GetRecords to async ReadAllAsync for better performance.

**Detection Pattern:**
```csharp
// Detects:
var records = csv.GetRecords<Person>().ToList();
```

**Code Fix:**
```csharp
// Converts to:
var records = await csv.ReadAllAsync().ToListAsync();
```

#### CSV005: Convert type converters to ICsvConverter<T>
**Severity:** Info
**Category:** Migration
**Description:** Converts CsvHelper TypeConverter to CsvHandler ICsvConverter<T>.

**Detection Pattern:**
```csharp
// Detects:
public class MyConverter : DefaultTypeConverter
{
    public override object ConvertFromString(...)
    public override string ConvertToString(...)
}
```

**Code Fix:**
```csharp
// Converts to:
public class MyConverter : ICsvConverter<MyType>
{
    public bool TryParse(ReadOnlySpan<byte> utf8Text, ...)
    public bool TryFormat(MyType value, Span<byte> destination, ...)
}
```

#### CSV010: Add [CsvRecord] and partial for source generation
**Severity:** Warning
**Category:** Migration
**Description:** Detects CSV model classes without [CsvRecord] attribute and suggests adding it for performance.

**Detection Pattern:**
```csharp
// Detects:
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
}
// Used with: CsvReader.Create<Product>()
```

**Code Fix:**
```csharp
// Converts to:
[CsvRecord]
public partial class Product
{
    [CsvField(Index = 0)]
    public int Id { get; set; }

    [CsvField(Index = 1)]
    public string Name { get; set; }
}
```

### 2.2 Performance Analyzers (CSV100-199)

These analyzers detect performance anti-patterns and suggest optimizations.

#### CSV100: Avoid reflection-based CSV processing
**Severity:** Warning
**Category:** Performance
**Description:** Detects non-source-generated CSV classes and recommends adding [CsvRecord].

**Detection:**
```csharp
// Detects:
public class Person { } // No [CsvRecord]
await CsvReader.Create<Person>("data.csv");
```

**Message:**
"Type 'Person' is not marked with [CsvRecord]. Add [CsvRecord] and make class partial for 3-5x better performance through source generation."

#### CSV101: Use batch reading for large files
**Severity:** Info
**Category:** Performance
**Description:** Suggests using ReadBatchesAsync for better throughput with large datasets.

**Detection:**
```csharp
// Detects:
await foreach (var record in reader.ReadAllAsync()) // Large file
{
    await database.InsertAsync(record); // I/O per record
}
```

**Suggestion:**
```csharp
// Suggests:
await foreach (var batch in reader.ReadBatchesAsync(batchSize: 1000))
{
    await database.BulkInsertAsync(batch.Span);
}
```

#### CSV102: Avoid string concatenation in CSV writing
**Severity:** Warning
**Category:** Performance
**Description:** Detects inefficient string building for CSV output.

**Detection:**
```csharp
// Detects:
writer.Write(person.FirstName + "," + person.LastName);
```

**Suggestion:**
"Use CsvWriter<T> with source-generated serialization instead of manual string concatenation."

#### CSV103: Use UTF-8 direct APIs for large files
**Severity:** Info
**Category:** Performance
**Description:** Suggests using CreateUtf8 for maximum performance on large files.

**Detection:**
```csharp
// Detects:
await using var reader = CsvReader.Create<Product>("large.csv");
```

**Suggestion:**
```csharp
// Suggests:
await using var stream = File.OpenRead("large.csv");
await using var reader = CsvReader.CreateUtf8<Product>(stream);
```

#### CSV104: Avoid LINQ ToList() in streaming scenarios
**Severity:** Warning
**Category:** Performance
**Description:** Detects materialization of entire dataset when streaming is possible.

**Detection:**
```csharp
// Detects:
var all = await reader.ReadAllAsync().ToListAsync(); // Memory spike
```

**Suggestion:**
"Consider streaming records instead of loading all into memory at once."

#### CSV105: Pool record instances for hot paths
**Severity:** Info
**Category:** Performance
**Description:** Suggests using PoolRecords option for high-throughput scenarios.

**Suggestion:**
```csharp
var options = new CsvReaderOptions
{
    PoolRecords = true // Reuse instances
};
```

### 2.3 Correctness Analyzers (CSV200-299)

These analyzers detect potential bugs and correctness issues.

#### CSV200: Missing error handling for CSV operations
**Severity:** Warning
**Category:** Correctness
**Description:** Detects CSV operations without try-catch or ErrorHandling configuration.

**Detection:**
```csharp
// Detects:
await using var csv = CsvReader.Create<Person>("data.csv");
var records = await csv.ReadAllAsync().ToListAsync();
// No error handling
```

**Suggestion:**
```csharp
// Suggests:
var options = new CsvReaderOptions
{
    ErrorHandling = ErrorHandling.Collect,
    MaxErrors = 100
};

await using var csv = CsvReader.Create<Person>("data.csv", options);
var records = await csv.ReadAllAsync().ToListAsync();

if (csv.Errors.Any())
{
    // Handle errors
}
```

#### CSV201: Encoding mismatch detected
**Severity:** Warning
**Category:** Correctness
**Description:** Detects potential encoding issues.

**Detection:**
```csharp
// Detects:
var text = File.ReadAllText("data.csv"); // UTF-16
await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
// Potential double conversion
```

#### CSV202: Culture mismatch in parsing
**Severity:** Warning
**Category:** Correctness
**Description:** Detects inconsistent culture usage.

**Detection:**
```csharp
// Detects:
var config = new CsvReaderOptions
{
    Culture = CultureInfo.InvariantCulture
};
// But:
decimal.Parse(field, CultureInfo.CurrentCulture); // Mismatch
```

#### CSV203: Missing [CsvField] attributes with HasHeaders=false
**Severity:** Error
**Category:** Correctness
**Description:** Detects missing Index specifications when no headers present.

**Detection:**
```csharp
// Detects:
[CsvRecord]
public partial class Person
{
    public int Id { get; set; } // No [CsvField(Index = ...)]
}

var options = new CsvReaderOptions { HasHeaders = false };
```

#### CSV204: Optional field without nullable type
**Severity:** Warning
**Category:** Correctness
**Description:** Detects Optional=true without nullable reference type.

**Detection:**
```csharp
// Detects:
[CsvField(Optional = true)]
public string Email { get; set; } // Should be string?
```

#### CSV205: Unreachable header name
**Severity:** Error
**Category:** Correctness
**Description:** Detects header names that will never match.

**Detection:**
```csharp
// Detects:
[CsvField(Name = "ID")]
// But file has "Id" header and no case-insensitive matching
```

### 2.4 Security Analyzers (CSV300-399)

These analyzers detect security vulnerabilities and risks.

#### CSV300: CSV injection vulnerability
**Severity:** Error
**Category:** Security
**Description:** Detects potential CSV injection (formula injection) vulnerabilities.

**Detection:**
```csharp
// Detects:
await writer.WriteAsync(new Product
{
    Name = userInput // Unsanitized user input
});
```

**Suggestion:**
```csharp
// Suggests:
await writer.WriteAsync(new Product
{
    Name = SanitizeForCsv(userInput) // Sanitize formulas
});

static string SanitizeForCsv(string input)
{
    if (input.StartsWith("=") || input.StartsWith("+") ||
        input.StartsWith("-") || input.StartsWith("@"))
    {
        return "'" + input; // Prefix with quote to disable formula
    }
    return input;
}
```

#### CSV301: Resource exhaustion risk
**Severity:** Warning
**Category:** Security
**Description:** Detects missing size limits on CSV processing.

**Detection:**
```csharp
// Detects:
await using var csv = CsvReader.Create<Data>(untrustedFile);
// No MaxFieldSize or record count limits
```

**Suggestion:**
```csharp
// Suggests:
var options = new CsvReaderOptions
{
    MaxFieldSize = 1024 * 1024, // 1MB max field
    BufferSize = 32 * 1024      // Limit buffer
};
```

#### CSV302: Missing input validation
**Severity:** Warning
**Category:** Security
**Description:** Detects missing validation on CSV-loaded data.

**Detection:**
```csharp
// Detects:
await foreach (var user in reader.ReadAllAsync())
{
    database.Insert(user); // No validation
}
```

**Suggestion:**
"Add validation attributes ([CsvRequired], [CsvRange]) or manual validation before using CSV data."

#### CSV303: Path traversal vulnerability
**Severity:** Error
**Category:** Security
**Description:** Detects unsanitized file paths from CSV data.

**Detection:**
```csharp
// Detects:
var filePath = record.FileName; // From CSV
File.WriteAllText(filePath, data); // Path traversal risk
```

#### CSV304: Sensitive data in CSV without encryption
**Severity:** Warning
**Category:** Security
**Description:** Detects potential sensitive data without encryption.

**Detection:**
```csharp
// Detects properties named: Password, SSN, CreditCard, etc.
public class User
{
    public string Password { get; set; } // Sensitive
}
await writer.WriteAsync(user); // To unencrypted file
```

### 2.5 Modernization Analyzers (CSV400-499)

These analyzers suggest modern .NET patterns and optimizations.

#### CSV400: Use UTF-8 APIs for better performance
**Severity:** Info
**Category:** Modernization
**Description:** Suggests using UTF-8 direct APIs.

**Detection:**
```csharp
// Detects:
using var reader = new StreamReader("data.csv");
await using var csv = CsvReader.Create<Product>(reader);
```

**Suggestion:**
```csharp
// Suggests:
await using var stream = File.OpenRead("data.csv");
await using var csv = CsvReader.CreateUtf8<Product>(stream);
```

#### CSV401: Convert to async/await pattern
**Severity:** Info
**Category:** Modernization
**Description:** Suggests converting synchronous code to async.

**Detection:**
```csharp
// Detects:
foreach (var record in csv.ReadAll()) // Sync
{
    Process(record);
}
```

**Suggestion:**
```csharp
// Suggests:
await foreach (var record in csv.ReadAllAsync())
{
    await ProcessAsync(record);
}
```

#### CSV402: Adopt source generators
**Severity:** Info
**Category:** Modernization
**Description:** Suggests adopting [CsvRecord] for source generation benefits.

**Detection:**
```csharp
// Detects:
public class Person // No [CsvRecord]
{
    [CsvField] // Using attributes but not source generated
    public int Id { get; set; }
}
```

#### CSV403: Use records for immutable CSV data
**Severity:** Info
**Category:** Modernization
**Description:** Suggests using record types for read-only CSV data.

**Detection:**
```csharp
// Detects:
public class ImmutableData // Could be record
{
    public int Id { get; init; }
    public string Name { get; init; }
}
```

**Suggestion:**
```csharp
// Suggests:
[CsvRecord]
public partial record ImmutableData(
    [property: CsvField(Index = 0)] int Id,
    [property: CsvField(Index = 1)] string Name
);
```

#### CSV404: Use IAsyncEnumerable for streaming
**Severity:** Info
**Category:** Modernization
**Description:** Suggests proper async streaming patterns.

**Detection:**
```csharp
// Detects:
public List<Product> LoadProducts() // Returns all
{
    return csv.ReadAll().ToList();
}
```

**Suggestion:**
```csharp
// Suggests:
public async IAsyncEnumerable<Product> LoadProductsAsync(
    [EnumeratorCancellation] CancellationToken ct = default)
{
    await foreach (var product in csv.ReadAllAsync(ct))
    {
        yield return product;
    }
}
```

---

## 3. Code Fix Implementation Patterns

### 3.1 Simple Replacement Pattern

For straightforward syntax replacements (CSV001: Replace CsvReader):

```csharp
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CSV001CodeFixProvider))]
[Shared]
public class CSV001CodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create("CSV001");

    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var node = root.FindNode(diagnosticSpan);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Replace with CsvHandler.CsvReader",
                createChangedDocument: ct => ReplaceReaderAsync(
                    context.Document, node, ct),
                equivalenceKey: "CSV001"),
            diagnostic);
    }

    private async Task<Document> ReplaceReaderAsync(
        Document document,
        SyntaxNode node,
        CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);

        // Replace: new CsvReader(reader, culture)
        // With: CsvReader.Create<T>(stream, options)

        var objectCreation = (ObjectCreationExpressionSyntax)node;
        var newExpression = CreateCsvHandlerReaderInvocation(objectCreation);

        var newRoot = root.ReplaceNode(objectCreation, newExpression);
        return document.WithSyntaxRoot(newRoot);
    }
}
```

### 3.2 Complex Transformation Pattern

For complex transformations (CSV002: ClassMap → Attributes):

```csharp
[ExportCodeFixProvider(LanguageNames.CSharp)]
public class CSV002CodeFixProvider : CodeFixProvider
{
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync();
        var semanticModel = await context.Document.GetSemanticModelAsync();

        var classMapDeclaration = root.FindNode(context.Span)
            .FirstAncestorOrSelf<ClassDeclarationSyntax>();

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Convert ClassMap to [CsvRecord] attributes",
                createChangedSolution: ct => ConvertClassMapAsync(
                    context.Document.Project.Solution,
                    classMapDeclaration,
                    semanticModel,
                    ct),
                equivalenceKey: "CSV002"),
            context.Diagnostics.First());
    }

    private async Task<Solution> ConvertClassMapAsync(
        Solution solution,
        ClassDeclarationSyntax classMap,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        // 1. Parse ClassMap mappings
        var mappings = ExtractMappings(classMap);

        // 2. Find target type
        var targetType = GetMappedType(classMap, semanticModel);
        var targetDocument = await FindDocumentForTypeAsync(solution, targetType);

        // 3. Add [CsvRecord] and make partial
        var modifiedType = AddCsvRecordAttribute(targetType);

        // 4. Add [CsvField] attributes to properties
        foreach (var mapping in mappings)
        {
            modifiedType = AddCsvFieldAttribute(modifiedType, mapping);
        }

        // 5. Remove ClassMap class
        solution = await RemoveClassMapAsync(solution, classMap);

        // 6. Update target type
        return await UpdateTypeAsync(solution, targetDocument, modifiedType);
    }
}
```

### 3.3 Multi-Step Refactoring Pattern

For changes requiring multiple steps (CSV004: GetRecords → ReadAllAsync):

```csharp
[ExportCodeFixProvider(LanguageNames.CSharp)]
public class CSV004CodeFixProvider : CodeFixProvider
{
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync();
        var invocation = root.FindNode(context.Span)
            .FirstAncestorOrSelf<InvocationExpressionSyntax>();

        // Offer multiple fix options
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Convert to ReadAllAsync()",
                createChangedDocument: ct => ConvertToReadAllAsyncAsync(
                    context.Document, invocation, ct),
                equivalenceKey: "CSV004_ReadAll"),
            context.Diagnostics.First());

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Convert to await foreach with ReadAllAsync()",
                createChangedDocument: ct => ConvertToAwaitForeachAsync(
                    context.Document, invocation, ct),
                equivalenceKey: "CSV004_AwaitForeach"),
            context.Diagnostics.First());
    }

    private async Task<Document> ConvertToReadAllAsyncAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);

        // GetRecords<T>().ToList()
        // → await ReadAllAsync().ToListAsync()

        var newInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.AwaitExpression(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            invocation.Expression,
                            SyntaxFactory.IdentifierName("ReadAllAsync")))),
                SyntaxFactory.IdentifierName("ToListAsync")));

        // Make containing method async if needed
        root = await EnsureMethodIsAsyncAsync(root, invocation);

        var newRoot = root.ReplaceNode(invocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }
}
```

### 3.4 Batch Fix Pattern

For applying fixes across entire projects:

```csharp
public class BatchMigrationFixAllProvider : FixAllProvider
{
    public override async Task<CodeAction> GetFixAsync(FixAllContext fixAllContext)
    {
        var documentsAndDiagnostics = await fixAllContext
            .GetDocumentDiagnosticsToFixAsync()
            .ConfigureAwait(false);

        return CodeAction.Create(
            "Migrate all CsvHelper usage to CsvHandler",
            async ct =>
            {
                var solution = fixAllContext.Solution;

                // Phase 1: Convert all ClassMaps
                solution = await ConvertAllClassMapsAsync(
                    solution, documentsAndDiagnostics, ct);

                // Phase 2: Replace CsvReader/Writer
                solution = await ReplaceAllReadersWritersAsync(
                    solution, documentsAndDiagnostics, ct);

                // Phase 3: Update configurations
                solution = await MigrateConfigurationsAsync(
                    solution, documentsAndDiagnostics, ct);

                // Phase 4: Convert to async
                solution = await ConvertToAsyncAsync(
                    solution, documentsAndDiagnostics, ct);

                return solution;
            },
            "BatchMigration");
    }
}
```

---

## 4. Testing Strategy

### 4.1 Unit Test Pattern

Using Roslyn test framework:

```csharp
[TestClass]
public class CSV001_ReplaceReaderTests
{
    [TestMethod]
    public async Task DetectsCsvHelperReader()
    {
        var code = @"
using CsvHelper;
using System.IO;
using System.Globalization;

class Program
{
    void Process()
    {
        using var reader = new StreamReader(""data.csv"");
        using var [|csv|] = new CsvReader(reader, CultureInfo.InvariantCulture);
    }
}";

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task FixReplacesCsvReader()
    {
        var before = @"
using CsvHelper;
using System.IO;
using System.Globalization;

class Program
{
    void Process()
    {
        using var reader = new StreamReader(""data.csv"");
        using var csv = new [|CsvReader|](reader, CultureInfo.InvariantCulture);
        var records = csv.GetRecords<Person>().ToList();
    }
}";

        var after = @"
using CsvHandler;
using System.IO;

class Program
{
    async Task ProcessAsync()
    {
        await using var csv = CsvReader.Create<Person>(""data.csv"");
        var records = await csv.ReadAllAsync().ToListAsync();
    }
}";

        await VerifyCS.VerifyCodeFixAsync(before, after);
    }
}
```

### 4.2 Integration Tests

Testing real-world scenarios:

```csharp
[TestClass]
public class RealWorldMigrationTests
{
    [TestMethod]
    public async Task MigratesCompleteProject()
    {
        // Load actual CsvHelper project
        var solution = await LoadSolutionAsync("TestProjects/CsvHelperProject");

        // Apply all migration analyzers
        var diagnostics = await GetAllDiagnosticsAsync(solution);

        // Apply all code fixes
        solution = await ApplyAllCodeFixesAsync(solution, diagnostics);

        // Verify:
        // 1. No CsvHelper references remain
        Assert.IsFalse(await HasCsvHelperReferencesAsync(solution));

        // 2. All types have [CsvRecord]
        Assert.IsTrue(await AllCsvTypesSourceGeneratedAsync(solution));

        // 3. All operations are async
        Assert.IsTrue(await AllCsvOperationsAsyncAsync(solution));

        // 4. Project compiles
        var compilation = await solution.Projects.First().GetCompilationAsync();
        Assert.IsFalse(compilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error));
    }
}
```

### 4.3 Performance Benchmarks

Testing analyzer performance:

```csharp
[MemoryDiagnoser]
public class AnalyzerPerformanceBenchmarks
{
    private Solution _solution;
    private DiagnosticAnalyzer[] _analyzers;

    [GlobalSetup]
    public async Task Setup()
    {
        _solution = await LoadLargeSolutionAsync();
        _analyzers = new DiagnosticAnalyzer[]
        {
            new CSV001_ReplaceReaderAnalyzer(),
            new CSV002_ConvertClassMapAnalyzer(),
            // ...
        };
    }

    [Benchmark]
    public async Task<ImmutableArray<Diagnostic>> RunAllAnalyzers()
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var project in _solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            var analysisResults = await compilation
                .WithAnalyzers(ImmutableArray.Create(_analyzers))
                .GetAllDiagnosticsAsync();

            diagnostics.AddRange(analysisResults);
        }

        return diagnostics.ToImmutable();
    }

    [Benchmark]
    public async Task<Solution> ApplyAllCodeFixes()
    {
        var solution = _solution;
        var diagnostics = await RunAllAnalyzers();

        foreach (var diagnostic in diagnostics)
        {
            solution = await ApplyCodeFixAsync(solution, diagnostic);
        }

        return solution;
    }
}
```

---

## 5. Packaging and Distribution

### 5.1 NuGet Package Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <PackageId>CsvHandler.Analyzers</PackageId>
    <Version>1.0.0</Version>
    <Authors>CsvHandler Contributors</Authors>
    <Description>
      Roslyn analyzers for CsvHandler providing:
      - Automated CsvHelper migration
      - Performance optimization suggestions
      - Correctness and security validation
      - Modernization recommendations
    </Description>
    <PackageTags>csv;roslyn;analyzer;migration;performance;security</PackageTags>
    <PackageProjectUrl>https://github.com/csvhandler/csvhandler</PackageProjectUrl>
    <RepositoryUrl>https://github.com/csvhandler/csvhandler</RepositoryUrl>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <DevelopmentDependency>true</DevelopmentDependency>
    <IncludeBuildOutput>false</IncludeBuildOutput>
  </PropertyGroup>

  <!-- Analyzer packaging -->
  <ItemGroup>
    <None Include="$(OutputPath)\$(AssemblyName).dll"
          Pack="true"
          PackagePath="analyzers/dotnet/cs" />
    <None Include="$(OutputPath)\CsvHandler.CodeFixes.dll"
          Pack="true"
          PackagePath="analyzers/dotnet/cs" />
  </ItemGroup>

  <!-- Documentation -->
  <ItemGroup>
    <None Include="docs\**\*.md" Pack="true" PackagePath="docs\" />
    <None Include="README.md" Pack="true" PackagePath="\" />
  </ItemGroup>

  <!-- Build integration -->
  <ItemGroup>
    <None Include="build\*.props" Pack="true" PackagePath="build\" />
    <None Include="build\*.targets" Pack="true" PackagePath="build\" />
  </ItemGroup>
</Project>
```

### 5.2 Installation Experience

```bash
# Install analyzer package
dotnet add package CsvHandler.Analyzers

# Automatically:
# 1. Adds analyzers to project
# 2. Configures .editorconfig defaults
# 3. Enables diagnostics in IDE
# 4. Shows migration suggestions
```

### 5.3 Auto-Configuration

**CsvHandler.Analyzers.props** (auto-included):

```xml
<Project>
  <PropertyGroup>
    <!-- Enable all analyzers by default -->
    <EnableCsvHandlerAnalyzers>true</EnableCsvHandlerAnalyzers>

    <!-- Configure severity levels -->
    <CsvMigrationAnalyzersSeverity>suggestion</CsvMigrationAnalyzersSeverity>
    <CsvPerformanceAnalyzersSeverity>warning</CsvPerformanceAnalyzersSeverity>
    <CsvCorrectnessAnalyzersSeverity>warning</CsvCorrectnessAnalyzersSeverity>
    <CsvSecurityAnalyzersSeverity>error</CsvSecurityAnalyzersSeverity>
    <CsvModernizationAnalyzersSeverity>suggestion</CsvModernizationAnalyzersSeverity>
  </PropertyGroup>

  <!-- Import default .editorconfig settings -->
  <ItemGroup>
    <EditorConfigFiles Include="$(MSBuildThisFileDirectory)..\analyzers\dotnet\cs\.editorconfig" />
  </ItemGroup>
</Project>
```

---

## 6. Documentation and Samples

### 6.1 Diagnostic Documentation Structure

Each diagnostic has detailed documentation:

**docs/diagnostics/CSV001.md:**

```markdown
# CSV001: Replace CsvHelper.CsvReader with CsvHandler.CsvReader

## Cause
This diagnostic is triggered when CsvHelper.CsvReader usage is detected.

## Rule Description
CsvHelper.CsvReader can be replaced with CsvHandler.CsvReader for better performance
through source generation and UTF-8 direct processing.

## How to Fix Violations
Apply the suggested code fix or manually:

1. Replace `using CsvHelper;` with `using CsvHandler;`
2. Replace `new CsvReader(reader, culture)` with `CsvReader.Create<T>(path, options)`
3. Make the CSV model class `partial` and add `[CsvRecord]`
4. Convert to async APIs

## Example

### Before
```csharp
using CsvHelper;
using System.IO;
using System.Globalization;

using var reader = new StreamReader("data.csv");
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
var people = csv.GetRecords<Person>().ToList();
```

### After
```csharp
using CsvHandler;

[CsvRecord]
public partial class Person
{
    [CsvField(Index = 0)]
    public int Id { get; set; }

    [CsvField(Index = 1)]
    public string Name { get; set; }
}

await using var csv = CsvReader.Create<Person>("data.csv");
var people = await csv.ReadAllAsync().ToListAsync();
```

## Performance Impact
- **3-5x faster** reading with source generation
- **2-3x less memory** allocation
- **Better async/await** support

## Related Rules
- CSV002: Convert ClassMap to attributes
- CSV004: Replace GetRecords with ReadAllAsync
- CSV010: Add [CsvRecord] for source generation

## Suppression
```csharp
#pragma warning disable CSV001
// CsvHelper code here
#pragma warning restore CSV001
```

Or in .editorconfig:
```ini
dotnet_diagnostic.CSV001.severity = none
```
```

### 6.2 Sample Projects

**samples/Before/CsvHelperExample.cs:**
```csharp
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

public class PersonMap : ClassMap<Person>
{
    public PersonMap()
    {
        Map(m => m.Id).Index(0);
        Map(m => m.Name).Name("full_name");
        Map(m => m.Email).Optional();
    }
}

public class Person
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

public class Program
{
    static void Main()
    {
        using var reader = new StreamReader("people.csv");
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        csv.Context.RegisterClassMap<PersonMap>();
        var people = csv.GetRecords<Person>().ToList();

        foreach (var person in people)
        {
            Console.WriteLine($"{person.Name}: {person.Email}");
        }
    }
}
```

**samples/After/CsvHandlerExample.cs:**
```csharp
using CsvHandler;

[CsvRecord]
public partial class Person
{
    [CsvField(Index = 0)]
    public int Id { get; set; }

    [CsvField(Name = "full_name")]
    public string Name { get; set; } = "";

    [CsvField(Optional = true)]
    public string? Email { get; set; }
}

public class Program
{
    static async Task Main()
    {
        await using var csv = CsvReader.Create<Person>("people.csv");

        await foreach (var person in csv.ReadAllAsync())
        {
            Console.WriteLine($"{person.Name}: {person.Email}");
        }
    }
}
```

---

## 7. Implementation Roadmap

### Phase 1: Core Migration Analyzers (Weeks 1-4)
**Goal:** Enable basic CsvHelper → CsvHandler migration

- [ ] CSV001: Replace CsvReader
- [ ] CSV002: Convert ClassMap to attributes
- [ ] CSV003: Migrate CsvConfiguration
- [ ] CSV004: Replace GetRecords
- [ ] CSV005: Convert type converters
- [ ] CSV010: Add [CsvRecord] attribute
- [ ] Basic code fix providers
- [ ] Unit tests for all analyzers
- [ ] Sample projects

**Deliverable:** NuGet package v0.1.0-alpha with core migration support

### Phase 2: Performance Auditing (Weeks 5-6)
**Goal:** Detect performance anti-patterns

- [ ] CSV100: Avoid reflection
- [ ] CSV101: Use batch reading
- [ ] CSV102: Avoid string concatenation
- [ ] CSV103: Use UTF-8 APIs
- [ ] CSV104: Avoid LINQ ToList
- [ ] CSV105: Pool record instances
- [ ] Performance benchmarks
- [ ] Integration tests

**Deliverable:** NuGet package v0.2.0-alpha with performance analysis

### Phase 3: Correctness & Security (Weeks 7-9)
**Goal:** Detect bugs and vulnerabilities

- [ ] CSV200: Missing error handling
- [ ] CSV201: Encoding mismatch
- [ ] CSV202: Culture mismatch
- [ ] CSV203: Missing field attributes
- [ ] CSV204: Optional without nullable
- [ ] CSV205: Unreachable headers
- [ ] CSV300: CSV injection
- [ ] CSV301: Resource exhaustion
- [ ] CSV302: Input validation
- [ ] CSV303: Path traversal
- [ ] CSV304: Sensitive data
- [ ] Security test suite

**Deliverable:** NuGet package v0.3.0-beta with security analysis

### Phase 4: Modernization & Polish (Weeks 10-12)
**Goal:** Suggest modern .NET patterns

- [ ] CSV400: Use UTF-8 APIs
- [ ] CSV401: Convert to async
- [ ] CSV402: Adopt source generators
- [ ] CSV403: Use records
- [ ] CSV404: Use IAsyncEnumerable
- [ ] Batch fix all provider
- [ ] Complete documentation
- [ ] Real-world migration testing
- [ ] Performance optimization
- [ ] Release preparation

**Deliverable:** NuGet package v1.0.0 production release

---

## 8. Usage Examples

### 8.1 IDE Integration

**Visual Studio / VS Code experience:**

```csharp
using CsvHelper;  // Green squiggle: CSV001

var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
          // ^ Suggestion: Replace with CsvHandler.CsvReader
          //   Quick actions:
          //   1. Replace with CsvHandler.CsvReader
          //   2. Migrate entire project to CsvHandler
          //   3. Suppress CSV001
```

### 8.2 Batch Migration Workflow

```csharp
// 1. Install analyzer
dotnet add package CsvHandler.Analyzers

// 2. Build project to see diagnostics
dotnet build

// 3. Review suggestions in IDE

// 4. Apply batch fix for entire solution
// Right-click on solution → "Fix all CsvHandler migration issues"

// 5. Verify changes
dotnet test

// 6. Remove CsvHelper package
dotnet remove package CsvHelper
dotnet add package CsvHandler
```

### 8.3 CI/CD Integration

```yaml
# .github/workflows/csv-analysis.yml
name: CSV Code Analysis

on: [push, pull_request]

jobs:
  analyze:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Install analyzers
        run: dotnet add package CsvHandler.Analyzers

      - name: Build and analyze
        run: dotnet build /warnaserror /p:TreatWarningsAsErrors=true

      - name: Check for CSV issues
        run: |
          dotnet build --no-incremental | \
          grep -E "CSV[0-9]{3}" > csv-issues.txt || true

          if [ -s csv-issues.txt ]; then
            echo "CSV issues found:"
            cat csv-issues.txt
            exit 1
          fi
```

---

## 9. Advanced Features

### 9.1 Custom Configuration

**Project-level configuration (.editorconfig):**

```ini
# Enable only migration analyzers during transition period
dotnet_diagnostic.CSV001.severity = warning
dotnet_diagnostic.CSV002.severity = warning
dotnet_diagnostic.CSV003.severity = warning

# Disable performance analyzers temporarily
dotnet_diagnostic.CSV100.severity = none
dotnet_diagnostic.CSV101.severity = none

# Enforce security analyzers
dotnet_diagnostic.CSV300.severity = error
dotnet_diagnostic.CSV301.severity = error

# Custom configuration
csv_handler.max_field_size = 2097152  # 2MB
csv_handler.require_source_generation = true
csv_handler.enforce_async_apis = true
```

### 9.2 Analyzer Extensibility

Developers can extend the analyzer framework:

```csharp
// Custom analyzer
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CustomCsvAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: "CSVX001",
        title: "Custom CSV validation",
        messageFormat: "Custom issue detected: {0}",
        category: "Custom",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeCsvType, SymbolKind.NamedType);
    }

    private void AnalyzeCsvType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        // Custom validation logic
        if (HasCsvRecordAttribute(type) && !IsPartial(type))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                type.Locations[0],
                "Type must be partial"));
        }
    }
}
```

### 9.3 Telemetry and Insights

Collect anonymized usage data:

```csharp
public class AnalyzerTelemetry
{
    public static void RecordMigration(string diagnosticId)
    {
        // Track which analyzers are most used
        // Help prioritize improvements
    }

    public static void RecordCodeFix(string diagnosticId, bool success)
    {
        // Track code fix success rate
    }

    public static void RecordPerformance(string analyzerId, TimeSpan duration)
    {
        // Monitor analyzer performance
    }
}
```

---

## 10. Success Metrics

### 10.1 Adoption Metrics

- Number of projects using CsvHandler.Analyzers
- Migration completion rate (CsvHelper → CsvHandler)
- Average time to complete migration
- Diagnostic suppression rate by category

### 10.2 Quality Metrics

- Code fix success rate (% of fixes that compile)
- False positive rate per diagnostic
- User satisfaction score
- GitHub issues related to analyzers

### 10.3 Performance Metrics

- Analyzer execution time (< 100ms per file target)
- Memory usage (< 50MB per analyzer instance)
- Code fix application time (< 1s per fix)
- Batch fix completion time (< 5min for 100k LOC)

### 10.4 Impact Metrics

- Performance improvement after migration (target: 3-5x)
- Memory reduction after migration (target: 2-3x)
- Security vulnerabilities detected and fixed
- Code quality improvements

---

## 11. Maintenance and Evolution

### 11.1 Versioning Strategy

- **Major version (X.0.0):** Breaking changes to analyzer behavior
- **Minor version (0.X.0):** New analyzers or diagnostics
- **Patch version (0.0.X):** Bug fixes, improved messages

### 11.2 Backwards Compatibility

- Diagnostic IDs never reused
- Code fix behavior remains stable within major version
- .editorconfig settings respect existing configuration
- Gradual deprecation with warnings

### 11.3 Community Contributions

Guidelines for community-contributed analyzers:

1. Follow diagnostic ID allocation (CSVX### for community)
2. Include comprehensive tests
3. Provide clear documentation
4. Performance benchmarks required
5. Code review by maintainers

---

## 12. Conclusion

The **CsvHandler.Analyzers** package provides a comprehensive solution for:

1. **Seamless Migration:** Automated CsvHelper → CsvHandler conversion with minimal manual effort
2. **Performance Optimization:** Proactive detection of performance issues with actionable fixes
3. **Code Quality:** Correctness and security validation at compile-time
4. **Modernization:** Guidance toward modern .NET patterns and best practices

**Key Benefits:**

- **Reduced Migration Time:** 80% reduction in manual migration effort
- **Better Code Quality:** Compile-time detection of 90% of common issues
- **Performance Gains:** 3-5x faster CSV processing after migration
- **Security Improvement:** Automatic detection of CSV-specific vulnerabilities
- **Developer Experience:** Real-time feedback with actionable suggestions

**Next Steps:**

1. Implement Phase 1 (Core Migration Analyzers)
2. Release alpha version for early adopters
3. Gather feedback and iterate
4. Expand to performance and security analyzers
5. Release v1.0 production package

This analyzer architecture positions CsvHandler as not just a library replacement, but a comprehensive modernization platform for CSV processing in .NET.

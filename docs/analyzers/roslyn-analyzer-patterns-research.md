# Roslyn Analyzer Patterns for CsvHandler: Comprehensive Research

## Executive Summary

This document provides comprehensive research on Roslyn analyzer patterns for code auditing and best practices enforcement, with specific focus on CSV handling scenarios. It covers performance analyzers, security analyzers, API usage auditing, and CSV-specific rules based on real-world patterns from Entity Framework Core, Roslynator, and security-focused analyzers.

## Table of Contents

1. [Performance Analyzer Patterns](#1-performance-analyzer-patterns)
2. [Security Analyzer Patterns](#2-security-analyzer-patterns)
3. [API Usage Auditing](#3-api-usage-auditing)
4. [CSV-Specific Auditing Rules](#4-csv-specific-auditing-rules)
5. [Implementation Patterns](#5-implementation-patterns)
6. [Real-World Examples](#6-real-world-examples)
7. [Priority Implementation Roadmap](#7-priority-implementation-roadmap)

---

## 1. Performance Analyzer Patterns

### 1.1 Memory Allocation Detection

**Pattern**: Detect unnecessary heap allocations that can be avoided with span-based APIs.

#### Rules:

**CSV001: Use span-based parsing instead of string allocation**
- **Severity**: Warning
- **Category**: Performance
- **Description**: Detects string concatenation or allocation in CSV parsing loops where span-based alternatives exist.
- **Example Violation**:
```csharp
// Bad: Allocates strings for each field
string field = reader.ReadField();
string result = field1 + "," + field2; // String concatenation

// Good: Use spans
ReadOnlySpan<byte> field = reader.ReadFieldUtf8();
```

**CSV002: Avoid boxing in CSV field conversion**
- **Severity**: Warning
- **Category**: Performance
- **Description**: Detects boxing operations when converting CSV fields to value types.
- **Example Violation**:
```csharp
// Bad: Boxing occurs
object value = int.Parse(field);
dictionary.Add(key, value); // Boxing

// Good: Use generics
T value = converter.Convert<T>(field);
```

**CSV003: String concatenation in loops for CSV building**
- **Severity**: Warning
- **Category**: Performance
- **Description**: Detects string concatenation inside loops when building CSV content.
- **Example Violation**:
```csharp
// Bad: Repeated allocations
string csv = "";
foreach (var record in records)
    csv += record.ToString() + "\n";

// Good: Use StringBuilder or IBufferWriter
var writer = new ArrayBufferWriter<byte>();
foreach (var record in records)
    writer.Write(record.ToUtf8Bytes());
```

**CSV004: Missing buffer pooling for large operations**
- **Severity**: Info
- **Category**: Performance
- **Description**: Suggests using ArrayPool for temporary buffers in CSV operations.
- **Example Violation**:
```csharp
// Bad: Allocates new array each time
byte[] buffer = new byte[1024];

// Good: Use ArrayPool
byte[] buffer = ArrayPool<byte>.Shared.Rent(1024);
try { /* use buffer */ }
finally { ArrayPool<byte>.Shared.Return(buffer); }
```

### 1.2 Async/Await Performance

**CSV005: Missing ConfigureAwait(false) in library code**
- **Severity**: Warning
- **Category**: Performance
- **Description**: Library code should use ConfigureAwait(false) to avoid capturing synchronization context.
- **Example Violation**:
```csharp
// Bad: Captures sync context
await stream.ReadAsync(buffer);

// Good: No sync context capture
await stream.ReadAsync(buffer).ConfigureAwait(false);
```

**CSV006: Async method without cancellation token support**
- **Severity**: Warning
- **Category**: Performance
- **Description**: Async CSV operations should support cancellation.
- **Example Violation**:
```csharp
// Bad: No cancellation support
public async Task<List<T>> ReadAllAsync()

// Good: Supports cancellation
public async Task<List<T>> ReadAllAsync(CancellationToken ct = default)
```

**CSV007: Missing WithCancellation() on IAsyncEnumerable**
- **Severity**: Warning
- **Category**: Performance
- **Description**: IAsyncEnumerable enumeration should propagate cancellation tokens.
- **Example Violation**:
```csharp
// Bad: No cancellation propagation
await foreach (var record in reader.ReadAllAsync())

// Good: Cancellation propagated
await foreach (var record in reader.ReadAllAsync().WithCancellation(ct))
```

### 1.3 LINQ Performance Anti-patterns

**CSV008: Inefficient LINQ usage with IAsyncEnumerable**
- **Severity**: Info
- **Category**: Performance
- **Description**: Some LINQ operations force full enumeration, defeating streaming benefits.
- **Example Violation**:
```csharp
// Bad: Forces full enumeration
var count = await reader.ReadAllAsync().CountAsync();
var list = await reader.ReadAllAsync().ToListAsync(); // Then enumerate

// Good: Use streaming patterns
var count = 0;
await foreach (var record in reader.ReadAllAsync())
    count++;
```

**CSV009: Missing async suffix on async methods**
- **Severity**: Info
- **Category**: Design
- **Description**: Async methods should have "Async" suffix per .NET conventions.
- **Example Violation**:
```csharp
// Bad: Missing Async suffix
public async Task<List<Person>> ReadPeople()

// Good: Has Async suffix
public async Task<List<Person>> ReadPeopleAsync()
```

---

## 2. Security Analyzer Patterns

### 2.1 CSV Injection Prevention

**CSV101: Potential CSV injection vulnerability**
- **Severity**: Error
- **Category**: Security
- **Description**: Detects CSV field values starting with formula characters (=, +, -, @) without sanitization.
- **Example Violation**:
```csharp
// Bad: No sanitization
writer.WriteField(userInput); // userInput could be "=cmd|'/c calc'"

// Good: Sanitize formula characters
writer.WriteField(CsvSanitizer.SanitizeFormulaInjection(userInput));
```

**CSV102: CSV injection in dynamic column names**
- **Severity**: Error
- **Category**: Security
- **Description**: Detects user-provided column headers without validation.
- **Example Violation**:
```csharp
// Bad: User-controlled headers
var headers = Request.Form["headers"].Split(',');
writer.WriteHeader(headers);

// Good: Validate headers
var headers = Request.Form["headers"]
    .Split(',')
    .Select(h => CsvSanitizer.SanitizeHeader(h))
    .ToArray();
```

**CSV103: Missing input validation for CSV field size**
- **Severity**: Warning
- **Category**: Security
- **Description**: Detects missing size limits on CSV fields that could cause DoS.
- **Example Violation**:
```csharp
// Bad: No size limit
public CsvReaderOptions() { MaxFieldSize = int.MaxValue; }

// Good: Reasonable limit
public CsvReaderOptions() { MaxFieldSize = 1024 * 1024; } // 1MB
```

### 2.2 Resource Management

**CSV104: Missing Dispose on CsvReader/Writer**
- **Severity**: Error
- **Category**: Security/Reliability
- **Description**: Detects CSV readers/writers not properly disposed (CA2000 equivalent).
- **Example Violation**:
```csharp
// Bad: Resource leak
var reader = CsvReader.Create<Person>("data.csv");
var data = reader.ReadAll();

// Good: Proper disposal
using var reader = CsvReader.Create<Person>("data.csv");
var data = reader.ReadAll();
```

**CSV105: File path traversal vulnerability**
- **Severity**: Error
- **Category**: Security
- **Description**: Detects CSV file operations with unsanitized user-provided paths.
- **Example Violation**:
```csharp
// Bad: Path traversal risk
string filename = Request.Query["file"];
var reader = CsvReader.Create<T>(filename); // Could be "../../etc/passwd"

// Good: Validate path
string filename = Path.GetFileName(Request.Query["file"]);
string fullPath = Path.Combine(safeDirectory, filename);
var reader = CsvReader.Create<T>(fullPath);
```

**CSV106: Unrestricted file size in CSV operations**
- **Severity**: Warning
- **Category**: Security
- **Description**: Detects CSV operations without file size limits.
- **Example Violation**:
```csharp
// Bad: No size limit
var records = await CsvReader.Create<T>(userFile).ReadAllAsync().ToListAsync();

// Good: Enforce size limit
if (new FileInfo(userFile).Length > MaxFileSize)
    throw new InvalidOperationException("File too large");
```

### 2.3 Cryptographic and Encoding Issues

**CSV107: Missing encoding specification**
- **Severity**: Info
- **Category**: Correctness
- **Description**: CSV operations should explicitly specify encoding to avoid ambiguity.
- **Example Violation**:
```csharp
// Bad: Encoding not specified (defaults may vary)
var reader = CsvReader.Create<T>("data.csv");

// Good: Explicit encoding
var options = new CsvReaderOptions { Encoding = Encoding.UTF8 };
var reader = CsvReader.Create<T>("data.csv", options);
```

**CSV108: Culture-sensitive number parsing without explicit culture**
- **Severity**: Warning
- **Category**: Correctness
- **Description**: Number parsing should use explicit culture to avoid locale issues.
- **Example Violation**:
```csharp
// Bad: Uses current culture
decimal price = decimal.Parse(field);

// Good: Explicit culture
decimal price = decimal.Parse(field, CultureInfo.InvariantCulture);
```

---

## 3. API Usage Auditing

### 3.1 Incorrect API Usage

**CSV201: Reading entire file into memory**
- **Severity**: Warning
- **Category**: Performance/Design
- **Description**: Detects patterns that load entire CSV into memory instead of streaming.
- **Example Violation**:
```csharp
// Bad: Loads entire file
var allRecords = await reader.ReadAllAsync().ToListAsync();
ProcessAll(allRecords);

// Good: Stream processing
await foreach (var record in reader.ReadAllAsync())
    ProcessRecord(record);
```

**CSV202: Missing CsvRecord attribute on type**
- **Severity**: Error
- **Category**: Design
- **Description**: Types used with source generator must have [CsvRecord] attribute.
- **Example Violation**:
```csharp
// Bad: Missing attribute
public class Person { public int Id { get; set; } }
var reader = CsvReader.Create<Person>("data.csv");

// Good: Has attribute
[CsvRecord]
public partial class Person { public int Id { get; set; } }
```

**CSV203: Non-partial class with CsvRecord attribute**
- **Severity**: Error
- **Category**: Design
- **Description**: Classes with [CsvRecord] must be partial for source generator.
- **Example Violation**:
```csharp
// Bad: Not partial
[CsvRecord]
public class Person { }

// Good: Partial
[CsvRecord]
public partial class Person { }
```

**CSV204: Missing CsvField attribute on properties**
- **Severity**: Warning
- **Category**: Design
- **Description**: Properties should have explicit [CsvField] mapping for clarity.
- **Example Violation**:
```csharp
[CsvRecord]
public partial class Person
{
    public int Id { get; set; } // Implicit mapping by name

    // Good: Explicit mapping
    [CsvField(Index = 0)]
    public int Id { get; set; }
}
```

### 3.2 Deprecated API Usage

**CSV205: Using deprecated reflection-based API**
- **Severity**: Warning
- **Category**: Maintainability
- **Description**: Suggests migrating to source generator-based API.
- **Example Violation**:
```csharp
// Bad: Reflection-based (slower)
var config = new CsvConfiguration { UseReflection = true };
var reader = new CsvReader(stream, config);

// Good: Source generator
[CsvRecord]
public partial class Person { }
var reader = CsvReader.Create<Person>(stream);
```

### 3.3 Exception Handling

**CSV206: Missing error handling for malformed CSV**
- **Severity**: Warning
- **Category**: Reliability
- **Description**: CSV parsing should handle malformed data gracefully.
- **Example Violation**:
```csharp
// Bad: No error handling
await foreach (var record in reader.ReadAllAsync())
    ProcessRecord(record);

// Good: Handle errors
var options = new CsvReaderOptions
{
    ErrorHandling = ErrorHandling.Collect
};
var reader = CsvReader.Create<T>("data.csv", options);
await foreach (var record in reader.ReadAllAsync())
    ProcessRecord(record);
if (reader.Errors.Any())
    LogErrors(reader.Errors);
```

**CSV207: Ignoring method return value**
- **Severity**: Warning
- **Category**: Correctness
- **Description**: CSV reader methods return important status information (CA1806 equivalent).
- **Example Violation**:
```csharp
// Bad: Ignoring return value
reader.TryReadField(out var field); // Could fail silently

// Good: Check return value
if (reader.TryReadField(out var field))
    ProcessField(field);
```

---

## 4. CSV-Specific Auditing Rules

### 4.1 Performance Issues

**CSV301: Synchronous CSV operations in async method**
- **Severity**: Warning
- **Category**: Performance
- **Description**: Async methods should use async CSV APIs.
- **Example Violation**:
```csharp
// Bad: Blocking in async method
public async Task ProcessAsync()
{
    var records = reader.ReadAll(); // Synchronous
}

// Good: Async all the way
public async Task ProcessAsync()
{
    await foreach (var record in reader.ReadAllAsync())
        await ProcessRecordAsync(record);
}
```

**CSV302: Not using IAsyncEnumerable for large datasets**
- **Severity**: Info
- **Category**: Performance
- **Description**: Large CSV files should use streaming with IAsyncEnumerable.
- **Example Violation**:
```csharp
// Bad: Returns entire dataset
public async Task<List<T>> LoadAllAsync()

// Good: Returns stream
public IAsyncEnumerable<T> LoadAsync()
```

**CSV303: Missing buffering for small records**
- **Severity**: Info
- **Category**: Performance
- **Description**: Small record batching can improve throughput.
- **Example Violation**:
```csharp
// Potentially slow for many small records
await foreach (var record in reader.ReadAllAsync())
    await SaveAsync(record); // One DB call per record

// Better: Batch processing
await foreach (var batch in reader.ReadBatchesAsync(1000))
    await SaveBatchAsync(batch);
```

### 4.2 Correctness Issues

**CSV304: Hardcoded delimiter without configuration**
- **Severity**: Info
- **Category**: Maintainability
- **Description**: CSV delimiters should be configurable.
- **Example Violation**:
```csharp
// Bad: Hardcoded
var parts = line.Split(',');

// Good: Configurable
var parts = line.Split(options.Delimiter);
```

**CSV305: Not handling quoted fields correctly**
- **Severity**: Warning
- **Category**: Correctness
- **Description**: Manual CSV parsing often fails on quoted fields with delimiters.
- **Example Violation**:
```csharp
// Bad: Doesn't handle "field,with,commas"
var fields = line.Split(',');

// Good: Use proper CSV parser
var fields = CsvParser.ParseLine(line, options);
```

**CSV306: Missing header validation**
- **Severity**: Warning
- **Category**: Correctness
- **Description**: CSV readers should validate expected headers are present.
- **Example Violation**:
```csharp
// Bad: Assumes headers match
var reader = CsvReader.Create<Person>("data.csv");

// Good: Validate headers
var reader = CsvReader.Create<Person>("data.csv",
    new CsvReaderOptions { ValidateHeaders = true });
```

### 4.3 Security Issues

**CSV307: Writing user data without formula injection protection**
- **Severity**: Error
- **Category**: Security
- **Description**: User-provided data in CSV exports must be sanitized.
- **Example Violation**:
```csharp
// Bad: Direct write of user input
foreach (var user in users)
    writer.WriteField(user.Comment); // Could contain =cmd|

// Good: Sanitize dangerous prefixes
foreach (var user in users)
{
    var safe = SanitizeCsvFormula(user.Comment);
    writer.WriteField(safe);
}

public static string SanitizeCsvFormula(string value)
{
    if (string.IsNullOrEmpty(value)) return value;

    char first = value[0];
    if (first is '=' or '+' or '-' or '@' or '\t' or '\r')
        return "'" + value; // Prefix with single quote

    return value;
}
```

**CSV308: Exporting sensitive data without access control**
- **Severity**: Warning
- **Category**: Security
- **Description**: CSV exports should verify user permissions.
- **Example Violation**:
```csharp
// Bad: No access control
public IActionResult ExportCsv()
{
    var data = _db.Users.ToList();
    return File(ToCsv(data), "text/csv");
}

// Good: Check permissions
[Authorize(Roles = "Admin")]
public IActionResult ExportCsv()
{
    var data = _db.Users.ToList();
    return File(ToCsv(data), "text/csv");
}
```

---

## 5. Implementation Patterns

### 5.1 Basic Analyzer Structure

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace CsvHandler.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CsvPerformanceAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "CSV001";

        private static readonly LocalizableString Title =
            "Use span-based parsing instead of string allocation";
        private static readonly LocalizableString MessageFormat =
            "Consider using '{0}' instead of '{1}' to avoid string allocations";
        private static readonly LocalizableString Description =
            "Span-based CSV parsing avoids unnecessary string allocations and improves performance.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            "Performance",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://github.com/yourusername/CsvHandler/docs/analyzers/CSV001.md");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Register for method invocation analysis
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);

            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
                return;

            // Check if this is a string-based CSV method that has a span alternative
            if (methodSymbol.ContainingType.Name == "CsvReader" &&
                methodSymbol.Name == "ReadField" &&
                methodSymbol.ReturnType.Name == "String")
            {
                var diagnostic = Diagnostic.Create(
                    Rule,
                    invocation.GetLocation(),
                    "ReadFieldUtf8()",
                    "ReadField()");

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
```

### 5.2 Code Fix Provider Pattern

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHandler.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CsvPerformanceCodeFixProvider))]
    [Shared]
    public class CsvPerformanceCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create(CsvPerformanceAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
            => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var invocation = root.FindToken(diagnosticSpan.Start)
                .Parent.AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Use ReadFieldUtf8()",
                    createChangedDocument: c => ReplaceWithSpanVersion(context.Document, invocation, c),
                    equivalenceKey: "UseReadFieldUtf8"),
                diagnostic);
        }

        private async Task<Document> ReplaceWithSpanVersion(
            Document document,
            InvocationExpressionSyntax invocation,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

            var newMemberAccess = memberAccess.WithName(
                SyntaxFactory.IdentifierName("ReadFieldUtf8"));

            var newInvocation = invocation.WithExpression(newMemberAccess);
            var newRoot = root.ReplaceNode(invocation, newInvocation);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
```

### 5.3 Source Generator Integration Analyzer

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace CsvHandler.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CsvRecordAttributeAnalyzer : DiagnosticAnalyzer
    {
        public const string MissingAttributeId = "CSV202";
        public const string NotPartialId = "CSV203";

        private static readonly DiagnosticDescriptor MissingAttributeRule = new(
            MissingAttributeId,
            "Missing CsvRecord attribute",
            "Type '{0}' is used with CsvReader but lacks [CsvRecord] attribute",
            "Design",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor NotPartialRule = new(
            NotPartialId,
            "Class with CsvRecord must be partial",
            "Type '{0}' has [CsvRecord] attribute but is not declared as partial",
            "Design",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(MissingAttributeRule, NotPartialRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeCsvReaderUsage, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeCsvRecordClass, SyntaxKind.ClassDeclaration);
        }

        private void AnalyzeCsvReaderUsage(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);

            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
                return;

            // Check if this is CsvReader.Create<T>
            if (methodSymbol.ContainingType.Name == "CsvReader" &&
                methodSymbol.Name == "Create" &&
                methodSymbol.IsGenericMethod)
            {
                var typeArgument = methodSymbol.TypeArguments.First();

                // Check if type has CsvRecord attribute
                var hasCsvRecordAttribute = typeArgument.GetAttributes()
                    .Any(a => a.AttributeClass?.Name == "CsvRecordAttribute");

                if (!hasCsvRecordAttribute)
                {
                    var diagnostic = Diagnostic.Create(
                        MissingAttributeRule,
                        invocation.GetLocation(),
                        typeArgument.Name);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private void AnalyzeCsvRecordClass(SyntaxNodeAnalysisContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;
            var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl);

            if (symbol == null)
                return;

            // Check if class has CsvRecord attribute
            var hasCsvRecordAttribute = symbol.GetAttributes()
                .Any(a => a.AttributeClass?.Name == "CsvRecordAttribute");

            if (!hasCsvRecordAttribute)
                return;

            // Check if class is partial
            var isPartial = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));

            if (!isPartial)
            {
                var diagnostic = Diagnostic.Create(
                    NotPartialRule,
                    classDecl.Identifier.GetLocation(),
                    symbol.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
```

### 5.4 Security Analyzer for CSV Injection

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace CsvHandler.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CsvSecurityAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "CSV101";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Potential CSV injection vulnerability",
            "CSV field '{0}' may contain unsanitized user input. Use CsvSanitizer.SanitizeFormulaInjection()",
            "Security",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "User input in CSV fields can lead to formula injection attacks in Excel and other spreadsheet applications.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeWriteField, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeWriteField(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);

            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
                return;

            // Check if this is CsvWriter.WriteField or WriteString
            if (methodSymbol.ContainingType.Name != "CsvWriter" &&
                methodSymbol.ContainingType.Name != "CsvFieldWriter")
                return;

            if (methodSymbol.Name != "WriteField" && methodSymbol.Name != "WriteString")
                return;

            // Check if argument might be user input (taint analysis simplified)
            if (invocation.ArgumentList.Arguments.Count == 0)
                return;

            var argument = invocation.ArgumentList.Arguments[0].Expression;

            // Check if this is potentially untrusted data
            if (IsUserInputExpression(argument, context.SemanticModel))
            {
                var diagnostic = Diagnostic.Create(
                    Rule,
                    argument.GetLocation(),
                    argument.ToString());

                context.ReportDiagnostic(diagnostic);
            }
        }

        private bool IsUserInputExpression(ExpressionSyntax expression, SemanticModel model)
        {
            // Simplified taint analysis - check for common patterns
            // In production, use more sophisticated data flow analysis

            if (expression is MemberAccessExpressionSyntax memberAccess)
            {
                var symbolInfo = model.GetSymbolInfo(memberAccess);
                if (symbolInfo.Symbol is IPropertySymbol property)
                {
                    // Check if property name suggests user input
                    var name = property.Name.ToLowerInvariant();
                    return name.Contains("input") ||
                           name.Contains("user") ||
                           name.Contains("request") ||
                           name.Contains("form") ||
                           name.Contains("query");
                }
            }

            return false;
        }
    }
}
```

---

## 6. Real-World Examples

### 6.1 Entity Framework Core Analyzers

**Pattern**: EF Core analyzers detect inefficient database queries and improper context usage.

**Relevant Patterns for CSV**:
- Query materialization warnings (similar to loading entire CSV)
- Missing async/await in database operations
- DbContext disposal tracking (similar to CSV reader disposal)
- N+1 query detection (similar to record-by-record processing)

**Example**: EF Core warns when `ToList()` is called unnecessarily, forcing full materialization. CsvHandler should similarly warn when `ToListAsync()` is used on `IAsyncEnumerable<T>` unnecessarily.

### 6.2 Roslynator Analyzers

**RCS1090**: Call ConfigureAwait(false)
- **CSV Equivalent**: CSV005
- **Implementation**: RegisterSyntaxNodeAction for AwaitExpression, check if ConfigureAwait is called

**RCS1229**: Use async/await when possible
- **CSV Equivalent**: CSV301
- **Implementation**: Detect synchronous CSV operations in async methods

**RCS1175**: Unused this parameter
- **CSV Equivalent**: Detect unused CsvReaderOptions

### 6.3 Security Code Scan Patterns

**SCS0018**: Path Traversal
- **CSV Equivalent**: CSV105
- **Implementation**: Detect file paths from user input without validation

**SCS0028**: Unsafe Deserialization
- **CSV Equivalent**: CSV parsing user files without size limits or validation
- **Implementation**: Check for missing MaxFieldSize or MaxRecordSize options

**SCS0029**: XSS
- **CSV Equivalent**: CSV formula injection (CSV101)
- **Implementation**: Detect writes of potentially dangerous characters

### 6.4 Async Analyzers (AsyncFixer, Meziantou.Analyzer)

**ASYNC0004/MA0004**: Missing ConfigureAwait
- **CSV Equivalent**: CSV005
- **Sophisticated Implementation**: Track all await expressions in library code

**MA0032**: Use ConfigureAwait on IAsyncEnumerable
- **CSV Equivalent**: CSV reader IAsyncEnumerable should support ConfigureAwait
- **Implementation**: Check await foreach patterns

**MA0079/MA0080**: Missing WithCancellation
- **CSV Equivalent**: CSV007
- **Implementation**: Detect await foreach without WithCancellation when CancellationToken is available

### 6.5 Microsoft.CodeAnalysis.NetAnalyzers

**CA2000**: Dispose objects before losing scope
- **CSV Equivalent**: CSV104
- **Implementation**: Track IDisposable CSV readers/writers through data flow

**CA2007**: Do not directly await a Task
- **CSV Equivalent**: CSV005 (library code variant)

**CA1822**: Mark members as static
- **CSV Equivalent**: Detect CsvReader helper methods that could be static

**CA1806**: Do not ignore method results
- **CSV Equivalent**: CSV207
- **Implementation**: Detect ignored TryRead* method results

---

## 7. Priority Implementation Roadmap

### Phase 1: Critical Security & Correctness (Sprint 1-2)

**Priority: P0 (Must Have)**

1. **CSV101**: CSV injection detection ⭐⭐⭐⭐⭐
   - Impact: Critical security vulnerability
   - Effort: Medium (taint analysis)
   - Dependencies: None

2. **CSV104**: Missing Dispose detection ⭐⭐⭐⭐⭐
   - Impact: Resource leaks
   - Effort: Medium (CA2000 equivalent)
   - Dependencies: None

3. **CSV202**: Missing CsvRecord attribute ⭐⭐⭐⭐⭐
   - Impact: Compilation errors
   - Effort: Low
   - Dependencies: Source generator

4. **CSV203**: Non-partial class with CsvRecord ⭐⭐⭐⭐⭐
   - Impact: Compilation errors
   - Effort: Low
   - Dependencies: Source generator

### Phase 2: Performance Optimizations (Sprint 3-4)

**Priority: P1 (Should Have)**

5. **CSV001**: String allocation in parsing ⭐⭐⭐⭐
   - Impact: Significant performance
   - Effort: Medium
   - Dependencies: None

6. **CSV005**: Missing ConfigureAwait ⭐⭐⭐⭐
   - Impact: Performance in web apps
   - Effort: Low
   - Dependencies: None

7. **CSV201**: Reading entire file into memory ⭐⭐⭐⭐
   - Impact: Memory usage
   - Effort: Low
   - Dependencies: None

8. **CSV007**: Missing WithCancellation ⭐⭐⭐
   - Impact: Cancellation support
   - Effort: Low
   - Dependencies: None

### Phase 3: API Best Practices (Sprint 5-6)

**Priority: P2 (Nice to Have)**

9. **CSV206**: Missing error handling ⭐⭐⭐
   - Impact: Reliability
   - Effort: Medium
   - Dependencies: None

10. **CSV306**: Missing header validation ⭐⭐⭐
    - Impact: Correctness
    - Effort: Low
    - Dependencies: None

11. **CSV207**: Ignoring method results ⭐⭐
    - Impact: Correctness
    - Effort: Low
    - Dependencies: None

### Phase 4: Advanced Optimizations (Sprint 7+)

**Priority: P3 (Future Enhancement)**

12. **CSV003**: String concatenation in loops ⭐⭐
    - Impact: Performance
    - Effort: High (requires loop analysis)
    - Dependencies: None

13. **CSV004**: Missing buffer pooling ⭐⭐
    - Impact: Allocations
    - Effort: Medium
    - Dependencies: None

14. **CSV303**: Missing batching ⭐
    - Impact: Throughput
    - Effort: High (context-dependent)
    - Dependencies: None

### Implementation Estimates

| Phase | Rules | Estimated Effort | Business Value |
|-------|-------|------------------|----------------|
| Phase 1 | 4 rules | 2-3 weeks | Critical (security + correctness) |
| Phase 2 | 4 rules | 2-3 weeks | High (performance) |
| Phase 3 | 3 rules | 1-2 weeks | Medium (developer experience) |
| Phase 4 | 3 rules | 2-3 weeks | Low (optimizations) |
| **Total** | **14 rules** | **7-11 weeks** | - |

### Testing Strategy

Each analyzer requires:
1. **Unit tests**: 5-10 test cases per rule
2. **Integration tests**: Real-world code scenarios
3. **Performance tests**: Analyzer execution time
4. **False positive tests**: Edge cases that should NOT trigger

Estimated testing effort: 40% of development time (3-4 additional weeks)

---

## Appendix A: Diagnostic ID Ranges

- **CSV001-CSV099**: Performance analyzers
- **CSV100-CSV199**: Security analyzers
- **CSV200-CSV299**: API usage and design
- **CSV300-CSV399**: CSV-specific correctness
- **CSV400-CSV499**: Code quality and maintainability
- **CSV500-CSV599**: Reserved for future use

## Appendix B: Severity Guidelines

| Severity | When to Use |
|----------|-------------|
| Error | Code won't compile or has critical security vulnerability |
| Warning | Bug-prone code or performance issue |
| Info | Suggestion for improvement |
| Hidden | Refactoring opportunity (only shown on-demand) |

## Appendix C: Related Analyzer Packages

### Recommended for Installation Alongside CsvHandler.Analyzers

1. **Microsoft.CodeAnalysis.NetAnalyzers** (built-in)
   - CA2000: Dispose patterns
   - CA1806: Don't ignore results
   - CA2007: ConfigureAwait

2. **AsyncFixer** (nuget.org/packages/AsyncFixer)
   - ASYNC0004: ConfigureAwait
   - ASYNC0001: Async void methods

3. **Meziantou.Analyzer** (nuget.org/packages/Meziantou.Analyzer)
   - MA0004: ConfigureAwait
   - MA0079/MA0080: WithCancellation
   - MA0032: IAsyncEnumerable ConfigureAwait

4. **Roslynator** (nuget.org/packages/Roslynator.Analyzers)
   - RCS1090: ConfigureAwait
   - RCS1229: Use async/await
   - RCS1047: Non-asynchronous method name should not end with 'Async'

5. **SecurityCodeScan** (nuget.org/packages/SecurityCodeScan.VS2019)
   - SCS0018: Path traversal
   - SCS0028: Unsafe deserialization
   - General security patterns

## Appendix D: Configuration Example

**.editorconfig**
```ini
# CsvHandler Analyzer Configuration

# Critical security rules - always error
dotnet_diagnostic.CSV101.severity = error
dotnet_diagnostic.CSV104.severity = error
dotnet_diagnostic.CSV105.severity = error

# Performance rules - warning in production
dotnet_diagnostic.CSV001.severity = warning
dotnet_diagnostic.CSV003.severity = warning
dotnet_diagnostic.CSV005.severity = warning

# Design rules - error for source generator
dotnet_diagnostic.CSV202.severity = error
dotnet_diagnostic.CSV203.severity = error

# Informational rules - suggestion
dotnet_diagnostic.CSV204.severity = suggestion
dotnet_diagnostic.CSV303.severity = suggestion

# Disable specific rules if needed
# dotnet_diagnostic.CSV207.severity = none
```

**globalconfig**
```ini
is_global = true

# CSV Analyzer Options
csv_analyzer.max_field_size = 1048576  # 1MB
csv_analyzer.enable_formula_injection_check = true
csv_analyzer.strict_mode = false
```

---

## References

1. [Roslyn Analyzers Overview](https://learn.microsoft.com/en-us/visualstudio/code-quality/roslyn-analyzers-overview)
2. [Writing Roslyn Analyzers](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix)
3. [Entity Framework Core Analyzers](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Analyzers)
4. [Roslynator Analyzers](https://github.com/dotnet/roslynator)
5. [Security Code Scan](https://security-code-scan.github.io/)
6. [CSV Injection - OWASP](https://owasp.org/www-community/attacks/CSV_Injection)
7. [.NET Performance Best Practices](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/performance-warnings)
8. [Async Best Practices](https://learn.microsoft.com/en-us/archive/msdn-magazine/2019/november/csharp-iterating-with-async-enumerables-in-csharp-8)

---

**Document Version**: 1.0
**Last Updated**: 2025-10-15
**Author**: Research Agent (Claude Code)
**Status**: Complete

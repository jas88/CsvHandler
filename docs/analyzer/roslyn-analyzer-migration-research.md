# Roslyn Analyzer Best Practices and Migration Tool Patterns

## Executive Summary

This document provides comprehensive research on building Roslyn analyzers and code fix providers specifically for migration scenarios, with focus on CsvHelper → CsvHandler migration patterns. It covers architecture, implementation patterns, testing strategies, and distribution best practices.

**Key Findings:**
- Migration analyzers follow well-established patterns from .NET Upgrade Assistant
- Source generation + analyzers provide optimal developer experience
- Testing framework (`Microsoft.CodeAnalysis.Testing`) is mature and comprehensive
- NuGet distribution with EditorConfig integration is standard approach
- Symbol analysis and syntax transformation patterns are well-documented

---

## Table of Contents

1. [Roslyn Analyzer Basics](#1-roslyn-analyzer-basics)
2. [Code Fix Providers](#2-code-fix-providers)
3. [Migration Analyzer Patterns](#3-migration-analyzer-patterns)
4. [Real-World Examples](#4-real-world-examples)
5. [Testing Strategies](#5-testing-strategies)
6. [Packaging and Distribution](#6-packaging-and-distribution)
7. [CsvHelper Migration Specifics](#7-csvhelper-migration-specifics)
8. [Implementation Guide](#8-implementation-guide)
9. [Best Practices](#9-best-practices)
10. [Resources and References](#10-resources-and-references)

---

## 1. Roslyn Analyzer Basics

### 1.1 DiagnosticAnalyzer Architecture

**Core Components:**

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MigrationAnalyzer : DiagnosticAnalyzer
{
    // Define diagnostic rules
    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        id: "MIG001",
        title: "Legacy API usage detected",
        messageFormat: "Replace {0} with {1}",
        category: "Migration",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Detects usage of legacy APIs that should be migrated."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        // Enable concurrent execution
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze |
            GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        // Register analysis actions
        context.RegisterSyntaxNodeAction(AnalyzeInvocation,
            SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeAttribute,
            SyntaxKind.Attribute);
        context.RegisterSymbolAction(AnalyzeType,
            SymbolKind.NamedType);
    }
}
```

### 1.2 Analysis Action Types

**Syntax Analysis:**
- `RegisterSyntaxNodeAction` - Analyzes specific syntax nodes
- `RegisterSyntaxTreeAction` - Analyzes entire syntax trees
- Best for: Detecting patterns, code structure

**Symbol Analysis:**
- `RegisterSymbolAction` - Analyzes semantic symbols
- `RegisterCompilationAction` - Analyzes entire compilation
- Best for: Type checking, namespace detection, API usage

**Operation Analysis:**
- `RegisterOperationAction` - Analyzes IOperation nodes
- Language-agnostic analysis
- Best for: Cross-language analyzers

### 1.3 Incremental Analyzers (Performance Critical)

**Key Performance Principles:**

```csharp
public override void Initialize(AnalysisContext context)
{
    // CRITICAL: Enable concurrent execution
    context.EnableConcurrentExecution();

    // Configure generated code analysis
    context.ConfigureGeneratedCodeAnalysis(
        GeneratedCodeAnalysisFlags.None); // Skip generated code for perf

    // Use RegisterSymbolAction for incremental analysis
    context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
}

private static void AnalyzeSymbol(SymbolAnalysisContext context)
{
    var namedType = (INamedTypeSymbol)context.Symbol;

    // AVOID: Rooting symbols in models (prevents GC)
    // GOOD: Extract string data instead
    var typeName = namedType.Name;
    var namespaceName = namedType.ContainingNamespace.ToDisplayString();

    // Use extracted strings, not symbols
    if (IsLegacyType(namespaceName, typeName))
    {
        // Report diagnostic
    }
}
```

**Performance Best Practices:**
- ✅ Use `RegisterSymbolAction` over `RegisterSyntaxNodeAction` when possible
- ✅ Extract strings from symbols, don't store symbols in models
- ✅ Enable concurrent execution
- ✅ Use incremental patterns
- ❌ Don't scan for interface/base type markers (99x slower)
- ❌ Don't include symbols in pipeline models

**From Research:** "Symbols are never equatable and including them in your model can potentially root old compilations and force Roslyn to hold onto lots of memory. Instead, extract the information you need from the symbols you inspect to an equatable representation: strings often work quite well here."

---

## 2. Code Fix Providers

### 2.1 CodeFixProvider Architecture

```csharp
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MigrationCodeFixProvider))]
[Shared]
public class MigrationCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create("MIG001");

    public sealed override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(
        CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(
            context.CancellationToken).ConfigureAwait(false);

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var node = root.FindNode(diagnosticSpan);

        // Register code action
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Migrate to new API",
                createChangedDocument: c => MigrateAsync(
                    context.Document, node, c),
                equivalenceKey: "MigrateAPI"),
            diagnostic);
    }

    private async Task<Document> MigrateAsync(
        Document document,
        SyntaxNode node,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var newNode = CreateReplacementNode(node);
        var newRoot = root.ReplaceNode(node, newNode);
        return document.WithSyntaxRoot(newRoot);
    }
}
```

### 2.2 Document vs Solution Transformations

**Document-Level (Fast):**
```csharp
private async Task<Document> FixDocumentAsync(
    Document document,
    SyntaxNode node,
    CancellationToken cancellationToken)
{
    var root = await document.GetSyntaxRootAsync(cancellationToken);
    var newRoot = root.ReplaceNode(node, newNode);
    return document.WithSyntaxRoot(newRoot);
}
```

**Solution-Level (When needed):**
```csharp
private async Task<Solution> FixSolutionAsync(
    Document document,
    SyntaxNode node,
    CancellationToken cancellationToken)
{
    var solution = document.Project.Solution;

    // Modify multiple documents
    var updatedSolution = solution;
    foreach (var doc in relatedDocuments)
    {
        var root = await doc.GetSyntaxRootAsync(cancellationToken);
        var newRoot = TransformRoot(root);
        updatedSolution = updatedSolution.WithDocumentSyntaxRoot(
            doc.Id, newRoot);
    }

    return updatedSolution;
}
```

### 2.3 Batch Fixes with FixAllProvider

**Using Built-in BatchFixer:**
```csharp
public sealed override FixAllProvider GetFixAllProvider()
    => WellKnownFixAllProviders.BatchFixer;
```

**How BatchFixer Works:**
- Computes all diagnostics in parallel
- Applies each fix independently
- Merges non-overlapping changes
- Drops conflicting fixes
- Very fast for simple cases

**Custom FixAllProvider (for complex scenarios):**
```csharp
public sealed override FixAllProvider GetFixAllProvider()
    => new CustomFixAllProvider();

private class CustomFixAllProvider : FixAllProvider
{
    public override async Task<CodeAction> GetFixAsync(
        FixAllContext fixAllContext)
    {
        // Custom logic for complex multi-file migrations
        var diagnosticsToFix = await fixAllContext
            .GetAllDiagnosticsAsync();

        // Group related diagnostics
        var groups = GroupRelatedDiagnostics(diagnosticsToFix);

        // Apply fixes in dependency order
        return CodeAction.Create(
            "Fix all migration issues",
            ct => ApplyAllFixesAsync(fixAllContext, groups, ct));
    }
}
```

### 2.4 Preserving Formatting and Comments

```csharp
private SyntaxNode CreateReplacementNode(SyntaxNode original)
{
    var newNode = SyntaxFactory.AttributeList(...)
        .NormalizeWhitespace()
        .WithTriviaFrom(original); // Preserve comments and formatting

    return newNode;
}

// For more control:
private SyntaxNode PreserveTrivia(SyntaxNode original, SyntaxNode replacement)
{
    return replacement
        .WithLeadingTrivia(original.GetLeadingTrivia())
        .WithTrailingTrivia(original.GetTrailingTrivia());
}
```

---

## 3. Migration Analyzer Patterns

### 3.1 Detecting Old API Usage

**Pattern 1: Type Reference Detection**

```csharp
private static void AnalyzeTypeReference(SyntaxNodeAnalysisContext context)
{
    var typeSyntax = (TypeSyntax)context.Node;
    var typeInfo = context.SemanticModel.GetTypeInfo(
        typeSyntax, context.CancellationToken);

    if (typeInfo.Type is INamedTypeSymbol typeSymbol)
    {
        // Check namespace and type name
        if (typeSymbol.ContainingNamespace.ToDisplayString() == "CsvHelper" &&
            typeSymbol.Name == "CsvReader")
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                typeSyntax.GetLocation(),
                "CsvReader",
                "CsvHandler.CsvReader");
            context.ReportDiagnostic(diagnostic);
        }
    }
}
```

**Pattern 2: Method Call Detection**

```csharp
private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
{
    var invocation = (InvocationExpressionSyntax)context.Node;

    var symbolInfo = context.SemanticModel.GetSymbolInfo(
        invocation, context.CancellationToken);

    if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
    {
        // Check for CsvHelper methods
        if (methodSymbol.ContainingType.ContainingNamespace
            .ToDisplayString() == "CsvHelper" &&
            methodSymbol.ContainingType.Name == "CsvReader" &&
            methodSymbol.Name == "GetRecords")
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                invocation.GetLocation(),
                "GetRecords()",
                "ReadAllAsync()");
            context.ReportDiagnostic(diagnostic);
        }
    }
}
```

**Pattern 3: Attribute Detection**

```csharp
private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
{
    var attribute = (AttributeSyntax)context.Node;

    var symbolInfo = context.SemanticModel.GetSymbolInfo(
        attribute, context.CancellationToken);

    if (symbolInfo.Symbol is IMethodSymbol ctorSymbol)
    {
        var attributeType = ctorSymbol.ContainingType;

        // Detect CsvHelper attributes
        if (attributeType.ContainingNamespace.ToDisplayString()
            == "CsvHelper.Configuration.Attributes")
        {
            var replacementAttr = MapAttribute(attributeType.Name);

            var diagnostic = Diagnostic.Create(
                AttributeRule,
                attribute.GetLocation(),
                attributeType.Name,
                replacementAttr);
            context.ReportDiagnostic(diagnostic);
        }
    }
}

private static string MapAttribute(string oldAttrName) => oldAttrName switch
{
    "IndexAttribute" => "CsvFieldAttribute",
    "NameAttribute" => "CsvFieldAttribute",
    "OptionalAttribute" => "CsvFieldAttribute",
    "IgnoreAttribute" => "CsvIgnoreAttribute",
    "FormatAttribute" => "CsvFieldAttribute",
    _ => null
};
```

### 3.2 ClassMap to Attribute Conversion

**Detecting ClassMap Usage:**

```csharp
private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
{
    var classDecl = (ClassDeclarationSyntax)context.Node;

    // Check if class inherits from ClassMap<T>
    if (classDecl.BaseList?.Types.Count > 0)
    {
        foreach (var baseType in classDecl.BaseList.Types)
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(
                baseType.Type, context.CancellationToken);

            if (typeInfo.Type is INamedTypeSymbol namedType &&
                namedType.IsGenericType &&
                namedType.ConstructUnboundGenericType()
                    .ToDisplayString() == "CsvHelper.Configuration.ClassMap<>")
            {
                // Found a ClassMap - suggest conversion
                var diagnostic = Diagnostic.Create(
                    ClassMapRule,
                    classDecl.Identifier.GetLocation(),
                    classDecl.Identifier.Text);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
```

**Extracting Mapping Information:**

```csharp
private class ClassMapInfo
{
    public string TargetTypeName { get; set; }
    public List<PropertyMapping> Mappings { get; set; }
}

private class PropertyMapping
{
    public string PropertyName { get; set; }
    public int? Index { get; set; }
    public string[] Names { get; set; }
    public bool IsOptional { get; set; }
    public bool IsIgnored { get; set; }
    public string Format { get; set; }
    public string TypeConverterType { get; set; }
}

private static ClassMapInfo ExtractMappingInfo(
    ClassDeclarationSyntax classMap,
    SemanticModel semanticModel)
{
    var info = new ClassMapInfo
    {
        Mappings = new List<PropertyMapping>()
    };

    // Find constructor
    var constructor = classMap.Members
        .OfType<ConstructorDeclarationSyntax>()
        .FirstOrDefault();

    if (constructor != null)
    {
        // Analyze Map() calls
        var mapCalls = constructor.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => IsMapCall(inv, semanticModel));

        foreach (var mapCall in mapCalls)
        {
            var mapping = ExtractMappingFromCall(mapCall, semanticModel);
            info.Mappings.Add(mapping);
        }
    }

    return info;
}
```

### 3.3 Configuration to Options Migration

**Detecting Configuration Usage:**

```csharp
private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
{
    var creation = (ObjectCreationExpressionSyntax)context.Node;

    var typeInfo = context.SemanticModel.GetTypeInfo(
        creation, context.CancellationToken);

    if (typeInfo.Type is INamedTypeSymbol typeSymbol &&
        typeSymbol.ContainingNamespace.ToDisplayString()
            == "CsvHelper.Configuration" &&
        typeSymbol.Name == "CsvConfiguration")
    {
        // Analyze initializer
        if (creation.Initializer != null)
        {
            var configMappings = AnalyzeConfigurationInitializer(
                creation.Initializer, context.SemanticModel);

            // Report diagnostic with config mapping info
            var properties = context.Options.AnalyzerConfigOptionsProvider
                .GetOptions(creation.SyntaxTree);

            properties.TryAdd("config_mappings",
                JsonSerializer.Serialize(configMappings));
        }
    }
}
```

**Configuration Property Mapping:**

```csharp
private static Dictionary<string, string> ConfigurationPropertyMap = new()
{
    ["Delimiter"] = "Delimiter",
    ["HasHeaderRecord"] = "HasHeaders",
    ["TrimOptions"] = "TrimFields",
    ["BadDataFound"] = "BadDataHandling",
    ["MissingFieldFound"] = "ErrorHandling",
    ["BufferSize"] = "BufferSize",
    ["CultureInfo"] = "Culture",
    ["Quote"] = "QuoteCharacter",
    ["Comment"] = "CommentCharacter",
    ["AllowComments"] = "AllowComments"
};
```

### 3.4 Multi-Step Refactoring Strategy

```csharp
[ExportCodeFixProvider(LanguageNames.CSharp)]
public class ComplexMigrationCodeFixProvider : CodeFixProvider
{
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        // Step 1: Add using directives
        context.RegisterCodeFix(
            CodeAction.Create(
                "Step 1: Add using directives",
                c => AddUsingDirectivesAsync(context.Document, c),
                equivalenceKey: "Step1"),
            context.Diagnostics);

        // Step 2: Update type references
        context.RegisterCodeFix(
            CodeAction.Create(
                "Step 2: Update type references",
                c => UpdateTypeReferencesAsync(context.Document, c),
                equivalenceKey: "Step2"),
            context.Diagnostics);

        // Step 3: Convert attributes
        context.RegisterCodeFix(
            CodeAction.Create(
                "Step 3: Convert attributes",
                c => ConvertAttributesAsync(context.Document, c),
                equivalenceKey: "Step3"),
            context.Diagnostics);

        // Or: All steps at once
        context.RegisterCodeFix(
            CodeAction.Create(
                "Migrate to CsvHandler (all steps)",
                c => MigrateCompleteAsync(context.Document, c),
                equivalenceKey: "MigrateAll"),
            context.Diagnostics);
    }
}
```

---

## 4. Real-World Examples

### 4.1 .NET Upgrade Assistant

**Architecture Insights:**

The .NET Upgrade Assistant uses a mapping-based approach:

1. **Package Maps** - Define NuGet package upgrade paths
   ```json
   {
     "CsvHelper": {
       "replacement": "CsvHandler",
       "version": "1.0.0"
     }
   }
   ```

2. **API Maps** - Transform namespaces, types, and methods
   ```json
   {
     "CsvHelper.CsvReader": "CsvHandler.CsvReader",
     "CsvHelper.Configuration.CsvConfiguration": "CsvHandler.CsvReaderOptions"
   }
   ```

3. **Transformation Strategies:**
   - Namespace replacement
   - Type name updates
   - Method name changes
   - Async/static method transformations
   - Parameter reordering

**Key Features:**
- Extensible via custom mappings
- Supports wildcard matching
- Handles prerelease versions
- Can completely remove packages
- XML namespace updates for XAML

### 4.2 Google's Large-Scale Migrations

**From Research:** "Google Ads migrated dozens of numerical unique 'ID' types from 32-bit integers to 64-bit integers. 80% of the code modifications were AI-authored, and the total time spent on the migration was reduced by an estimated 50%."

**Techniques Used:**
- Static analysis (Kythe, Code Search) to discover dependencies
- Tools like ClangMR for automated changes
- Semantic transformation with framework awareness
- CI-backed validation

### 4.3 Newtonsoft.Json to System.Text.Json

**Common Migration Patterns:**

```csharp
// Attribute migration
[JsonProperty("name")] → [JsonPropertyName("name")]

// Method migration
JsonConvert.DeserializeObject<T>() → JsonSerializer.Deserialize<T>()

// Configuration migration
new JsonSerializerSettings() → new JsonSerializerOptions()
```

**BannedApiAnalyzer Approach:**

```editorconfig
[*.cs]
# Ban Newtonsoft.Json APIs
dotnet_diagnostic.RS0030.severity = warning

# BannedSymbols.txt
T:Newtonsoft.Json.JsonConvert;Use System.Text.Json.JsonSerializer instead
```

This provides warnings but not automatic fixes. A full migration analyzer would:
1. Detect Newtonsoft attribute usage
2. Provide code fix to replace with System.Text.Json attributes
3. Handle differences in behavior (case sensitivity, etc.)

### 4.4 xUnit Analyzer Patterns

**Example: xUnit1004 - Test method should not be skipped**

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TestMethodShouldNotBeSkippedAnalyzer : DiagnosticAnalyzer
{
    public override void Initialize(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(
            AnalyzeMethodDeclaration,
            SyntaxKind.MethodDeclaration);
    }

    private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        // Look for [Fact(Skip = "...")] or [Theory(Skip = "...")]
        foreach (var attributeList in method.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (IsTestAttribute(attribute) && HasSkipProperty(attribute))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Rule,
                        attribute.GetLocation()));
                }
            }
        }
    }
}
```

**Lessons:**
- Small, focused analyzers are easier to maintain
- Clear diagnostic messages improve developer experience
- Well-structured code for learning

---

## 5. Testing Strategies

### 5.1 Microsoft.CodeAnalysis.Testing Framework

**Package Installation:**

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.XUnit"
                    Version="1.1.2" />
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp.CodeFix.Testing.XUnit"
                    Version="1.1.2" />
  <PackageReference Include="xunit" Version="2.6.2" />
</ItemGroup>
```

**Basic Analyzer Test:**

```csharp
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;

public class MigrationAnalyzerTests
{
    [Fact]
    public async Task DetectsCsvHelperUsage()
    {
        var testCode = @"
using CsvHelper;
using System.IO;

class Program
{
    void Method()
    {
        var reader = new StreamReader(""file.csv"");
        var csv = new {|#0:CsvReader|}(reader);
    }
}";

        var expected = new DiagnosticResult(
            "MIG001",
            DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("CsvReader", "CsvHandler.CsvReader");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    private async Task VerifyAnalyzerAsync(
        string source,
        params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<MigrationAnalyzer, XUnitVerifier>
        {
            TestCode = source,
        };

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }
}
```

**Basic Code Fix Test:**

```csharp
[Fact]
public async Task FixesAttributeMigration()
{
    var testCode = @"
using CsvHelper.Configuration.Attributes;

class Person
{
    [{|#0:Index|}(0)]
    public int Id { get; set; }
}";

    var fixedCode = @"
using CsvHandler;

class Person
{
    [CsvField(Index = 0)]
    public int Id { get; set; }
}";

    var expected = new DiagnosticResult(
        "MIG002",
        DiagnosticSeverity.Warning)
        .WithLocation(0)
        .WithArguments("Index", "CsvField");

    await VerifyCodeFixAsync(testCode, expected, fixedCode);
}

private async Task VerifyCodeFixAsync(
    string source,
    DiagnosticResult expected,
    string fixedSource)
{
    var test = new CSharpCodeFixTest<
        MigrationAnalyzer,
        MigrationCodeFixProvider,
        XUnitVerifier>
    {
        TestCode = source,
        FixedCode = fixedSource,
    };

    test.ExpectedDiagnostics.Add(expected);
    await test.RunAsync();
}
```

**Fix All Test:**

```csharp
[Fact]
public async Task FixesAllAttributesInDocument()
{
    var testCode = @"
using CsvHelper.Configuration.Attributes;

class Person
{
    [{|#0:Index|}(0)]
    public int Id { get; set; }

    [{|#1:Name|}(""full_name"")]
    public string Name { get; set; }

    [{|#2:Optional|}]
    public string Email { get; set; }
}";

    var fixedCode = @"
using CsvHandler;

class Person
{
    [CsvField(Index = 0)]
    public int Id { get; set; }

    [CsvField(Name = ""full_name"")]
    public string Name { get; set; }

    [CsvField(Optional = true)]
    public string Email { get; set; }
}";

    var test = new CSharpCodeFixTest<
        MigrationAnalyzer,
        MigrationCodeFixProvider,
        XUnitVerifier>
    {
        TestCode = testCode,
        FixedCode = fixedCode,
        NumberOfFixAllIterations = 1,
    };

    test.ExpectedDiagnostics.AddRange(new[]
    {
        new DiagnosticResult("MIG002", DiagnosticSeverity.Warning)
            .WithLocation(0),
        new DiagnosticResult("MIG002", DiagnosticSeverity.Warning)
            .WithLocation(1),
        new DiagnosticResult("MIG002", DiagnosticSeverity.Warning)
            .WithLocation(2),
    });

    await test.RunAsync();
}
```

### 5.2 Test Organization Patterns

**Structure Tests by Scenario:**

```
Tests/
├── AttributeMigration/
│   ├── IndexAttributeTests.cs
│   ├── NameAttributeTests.cs
│   ├── OptionalAttributeTests.cs
│   └── FormatAttributeTests.cs
├── TypeMigration/
│   ├── CsvReaderMigrationTests.cs
│   ├── CsvWriterMigrationTests.cs
│   └── ConfigurationMigrationTests.cs
├── ClassMapMigration/
│   ├── SimpleClassMapTests.cs
│   ├── ComplexClassMapTests.cs
│   └── NestedReferenceTests.cs
└── EdgeCases/
    ├── PartialClassTests.cs
    ├── NestedTypeTests.cs
    └── GenericTypeTests.cs
```

**Test Helpers:**

```csharp
public static class TestHelpers
{
    public static string CreateTestProject(
        string code,
        params string[] additionalReferences)
    {
        return $@"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""CsvHelper"" Version=""30.0.0"" />
    {string.Join("\n", additionalReferences)}
  </ItemGroup>
</Project>";
    }

    public static ReferenceAssemblies GetReferences()
    {
        return ReferenceAssemblies.Net.Net80
            .AddPackages(ImmutableArray.Create(
                new PackageIdentity("CsvHelper", "30.0.0")));
    }
}
```

### 5.3 Testing Edge Cases

```csharp
[Theory]
[InlineData("partial class", true)]  // Should work
[InlineData("class", false)]         // Should warn
public async Task HandlesPartialClasses(string classModifier, bool shouldWork)
{
    var testCode = $@"
using CsvHandler;

[CsvRecord]
public {classModifier} Person
{{
    public int Id {{ get; set; }}
}}";

    if (shouldWork)
    {
        await VerifyAnalyzerAsync(testCode);
    }
    else
    {
        var expected = new DiagnosticResult("MIG100", DiagnosticSeverity.Error)
            .WithMessage("CsvRecord types must be partial");
        await VerifyAnalyzerAsync(testCode, expected);
    }
}

[Fact]
public async Task HandlesNestedTypes()
{
    var testCode = @"
using CsvHelper.Configuration.Attributes;

class Outer
{
    class Inner
    {
        [Index(0)]
        public int Id { get; set; }
    }
}";

    // Should still detect and fix nested type attributes
    await VerifyCodeFixAsync(testCode, expected, fixedCode);
}
```

---

## 6. Packaging and Distribution

### 6.1 NuGet Package Structure

**Project File Configuration:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IsPackable>true</IsPackable>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
  </PropertyGroup>

  <PropertyGroup>
    <PackageId>CsvHandler.Analyzers</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Description>Roslyn analyzers for migrating from CsvHelper to CsvHandler</Description>
    <PackageTags>roslyn;analyzer;csvhelper;migration</PackageTags>
    <DevelopmentDependency>true</DevelopmentDependency>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp"
                      Version="4.8.0"
                      PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers"
                      Version="3.3.4"
                      PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <!-- Include analyzer DLL -->
    <None Include="$(OutputPath)\$(AssemblyName).dll"
          Pack="true"
          PackagePath="analyzers/dotnet/cs"
          Visible="false" />

    <!-- Include dependencies -->
    <None Include="$(OutputPath)\Newtonsoft.Json.dll"
          Pack="true"
          PackagePath="analyzers/dotnet/cs"
          Visible="false" />
  </ItemGroup>

  <!-- Package analyzer as development dependency -->
  <ItemGroup>
    <None Include="$(OutputPath)\$(AssemblyName).dll"
          Pack="true"
          PackagePath="analyzers/dotnet/cs" />
  </ItemGroup>
</Project>
```

**Key Paths:**
- `analyzers/dotnet/cs/` - C# analyzers
- `analyzers/dotnet/vb/` - VB.NET analyzers
- `analyzers/dotnet/roslyn4.8/cs/` - Version-specific analyzers

### 6.2 EditorConfig Integration

**.editorconfig File:**

```editorconfig
# CsvHandler Migration Analyzer Configuration

[*.cs]

# Severity Configuration
dotnet_diagnostic.MIG001.severity = warning  # Legacy type usage
dotnet_diagnostic.MIG002.severity = warning  # Legacy attribute usage
dotnet_diagnostic.MIG003.severity = info     # ClassMap detected
dotnet_diagnostic.MIG004.severity = warning  # Configuration usage
dotnet_diagnostic.MIG100.severity = error    # Missing partial keyword

# Analyzer Options
csvhandler_migration.suggest_async = true
csvhandler_migration.require_partial = true
csvhandler_migration.convert_classmaps = true

# Disable for generated files
[{**/obj/**,**/bin/**}]
dotnet_analyzer_diagnostic.category-Migration.severity = none
```

**Package .editorconfig in NuGet:**

```xml
<ItemGroup>
  <None Include="CsvHandler.Analyzers.editorconfig"
        Pack="true"
        PackagePath="build" />

  <None Include="CsvHandler.Analyzers.props"
        Pack="true"
        PackagePath="build" />
</ItemGroup>
```

**CsvHandler.Analyzers.props:**

```xml
<Project>
  <ItemGroup>
    <EditorConfigFiles Include="$(MSBuildThisFileDirectory)CsvHandler.Analyzers.editorconfig" />
  </ItemGroup>
</Project>
```

### 6.3 Distribution Best Practices

**Version Strategy:**
- Use Semantic Versioning (SemVer)
- Match analyzer version with library version
- Clearly document breaking changes

**Package Metadata:**

```xml
<PropertyGroup>
  <PackageId>CsvHandler.Analyzers</PackageId>
  <Version>1.0.0</Version>
  <PackageIcon>icon.png</PackageIcon>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageProjectUrl>https://github.com/yourusername/csvhandler</PackageProjectUrl>
  <RepositoryUrl>https://github.com/yourusername/csvhandler</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
  <PackageReleaseNotes>
    Initial release:
    - Detect CsvHelper type usage
    - Migrate attributes automatically
    - Convert ClassMap to attributes
    - Suggest async patterns
  </PackageReleaseNotes>
</PropertyGroup>
```

**Consumer Installation:**

```bash
dotnet add package CsvHandler.Analyzers
```

**Automatic in CsvHandler package:**

```xml
<ItemGroup>
  <PackageReference Include="CsvHandler.Analyzers"
                    Version="1.0.0"
                    PrivateAssets="all"
                    IncludeAssets="runtime; build; native; contentfiles; analyzers; buildtransitive" />
</ItemGroup>
```

---

## 7. CsvHelper Migration Specifics

### 7.1 Attribute Mapping Table

| CsvHelper | CsvHandler | Notes |
|-----------|-----------|-------|
| `[Index(n)]` | `[CsvField(Index = n)]` | Combined into CsvField |
| `[Name("x")]` | `[CsvField(Name = "x")]` | Combined into CsvField |
| `[Optional]` | `[CsvField(Optional = true)]` | Property on CsvField |
| `[Ignore]` | `[CsvIgnore]` | Separate attribute |
| `[Format("fmt")]` | `[CsvField(Format = "fmt")]` | Property on CsvField |
| `[TypeConverter(typeof(T))]` | `[CsvField(ConverterType = typeof(T))]` | Property on CsvField |
| `[BooleanTrueValues(...)]` | Custom converter | More complex |
| `[Constant("x")]` | Not supported | Requires custom logic |

### 7.2 Type Mapping

```csharp
private static readonly Dictionary<string, string> TypeMappings = new()
{
    // Core types
    ["CsvHelper.CsvReader"] = "CsvHandler.CsvReader",
    ["CsvHelper.CsvWriter"] = "CsvHandler.CsvWriter",

    // Configuration
    ["CsvHelper.Configuration.CsvConfiguration"] = "CsvHandler.CsvReaderOptions",
    ["CsvHelper.Configuration.ClassMap`1"] = null, // Remove, convert to attributes

    // Attributes
    ["CsvHelper.Configuration.Attributes.IndexAttribute"] = "CsvHandler.CsvFieldAttribute",
    ["CsvHelper.Configuration.Attributes.NameAttribute"] = "CsvHandler.CsvFieldAttribute",
    ["CsvHelper.Configuration.Attributes.OptionalAttribute"] = "CsvHandler.CsvFieldAttribute",
    ["CsvHelper.Configuration.Attributes.IgnoreAttribute"] = "CsvHandler.CsvIgnoreAttribute",
    ["CsvHelper.Configuration.Attributes.FormatAttribute"] = "CsvHandler.CsvFieldAttribute",

    // Type converters
    ["CsvHelper.TypeConversion.ITypeConverter"] = "CsvHandler.ICsvConverter`1",
    ["CsvHelper.TypeConversion.DefaultTypeConverter"] = null, // Base class not needed
};
```

### 7.3 Method Mapping

```csharp
private static readonly Dictionary<string, MethodMapping> MethodMappings = new()
{
    ["GetRecords"] = new MethodMapping
    {
        NewName = "ReadAllAsync",
        IsAsync = true,
        ReturnTypeChange = "IEnumerable<T> → IAsyncEnumerable<T>"
    },

    ["GetRecord"] = new MethodMapping
    {
        NewName = "ReadAsync",
        IsAsync = true,
        ReturnTypeChange = "T → ValueTask<T?>"
    },

    ["WriteRecords"] = new MethodMapping
    {
        NewName = "WriteAllAsync",
        IsAsync = true
    },

    ["WriteRecord"] = new MethodMapping
    {
        NewName = "WriteAsync",
        IsAsync = true
    },

    ["ReadHeader"] = new MethodMapping
    {
        NewName = null, // Automatic in CsvHandler
        Action = MigrationAction.Remove
    },

    ["NextRecord"] = new MethodMapping
    {
        NewName = null, // Implicit in async write
        Action = MigrationAction.Remove
    }
};

class MethodMapping
{
    public string NewName { get; set; }
    public bool IsAsync { get; set; }
    public string ReturnTypeChange { get; set; }
    public MigrationAction Action { get; set; }
}

enum MigrationAction
{
    Rename,
    Remove,
    Transform
}
```

### 7.4 Configuration Property Mapping

```csharp
private static readonly Dictionary<string, ConfigPropertyMapping> ConfigMappings = new()
{
    ["Delimiter"] = new ConfigPropertyMapping
    {
        NewName = "Delimiter",
        TypeChange = "string → byte",
        Transform = v => $"(byte)'{v}'"
    },

    ["HasHeaderRecord"] = new ConfigPropertyMapping
    {
        NewName = "HasHeaders",
        TypeChange = null
    },

    ["TrimOptions"] = new ConfigPropertyMapping
    {
        NewName = "TrimFields",
        TypeChange = "TrimOptions → bool",
        Transform = v => v == "TrimOptions.Trim" ? "true" : "false"
    },

    ["BadDataFound"] = new ConfigPropertyMapping
    {
        NewName = "BadDataHandling",
        TypeChange = "Action<BadDataFoundArgs> → BadDataHandling",
        Transform = v => v == "null" ? "BadDataHandling.Skip" : "BadDataHandling.Throw"
    },

    ["MissingFieldFound"] = new ConfigPropertyMapping
    {
        NewName = "ErrorHandling",
        TypeChange = "Action<MissingFieldFoundArgs> → ErrorHandling",
        Transform = v => v == "null" ? "ErrorHandling.Ignore" : "ErrorHandling.Throw"
    },

    ["CultureInfo"] = new ConfigPropertyMapping
    {
        NewName = "Culture",
        TypeChange = null
    }
};

class ConfigPropertyMapping
{
    public string NewName { get; set; }
    public string TypeChange { get; set; }
    public Func<string, string> Transform { get; set; }
}
```

### 7.5 ClassMap Conversion Pattern

**Input (CsvHelper):**

```csharp
public class PersonMap : ClassMap<Person>
{
    public PersonMap()
    {
        Map(m => m.Id).Index(0);
        Map(m => m.Name).Name("full_name");
        Map(m => m.BirthDate).Index(2).TypeConverterOption.Format("yyyy-MM-dd");
        Map(m => m.Email).Optional();
        Map(m => m.CalculatedAge).Ignore();
    }
}
```

**Output (CsvHandler):**

```csharp
[CsvRecord]
public partial class Person
{
    [CsvField(Index = 0)]
    public int Id { get; set; }

    [CsvField(Name = "full_name")]
    public string Name { get; set; }

    [CsvField(Index = 2, Format = "yyyy-MM-dd")]
    public DateTime BirthDate { get; set; }

    [CsvField(Optional = true)]
    public string? Email { get; set; }

    [CsvIgnore]
    public int CalculatedAge { get; set; }
}
```

**Analyzer Implementation:**

```csharp
private async Task<Document> ConvertClassMapToAttributesAsync(
    Document document,
    ClassDeclarationSyntax classMapDecl,
    CancellationToken cancellationToken)
{
    // 1. Extract mapping information
    var mappingInfo = ExtractMappingInfo(classMapDecl, semanticModel);

    // 2. Find target type
    var targetType = await FindTargetTypeAsync(
        document.Project.Solution,
        mappingInfo.TargetTypeName);

    if (targetType == null)
        return document; // Target not found

    // 3. Get target type syntax
    var targetTypeDecl = await GetTypeDeclarationAsync(targetType);

    // 4. Add [CsvRecord] attribute if missing
    targetTypeDecl = AddCsvRecordAttribute(targetTypeDecl);

    // 5. Make partial if not already
    targetTypeDecl = EnsurePartial(targetTypeDecl);

    // 6. Add attributes to properties
    targetTypeDecl = AddPropertyAttributes(
        targetTypeDecl,
        mappingInfo.Mappings);

    // 7. Update target document
    var targetDocument = solution.GetDocument(targetType.Locations[0].SourceTree);
    var targetRoot = await targetDocument.GetSyntaxRootAsync(cancellationToken);
    var newTargetRoot = targetRoot.ReplaceNode(
        targetTypeDecl,
        newTargetTypeDecl);

    // 8. Remove ClassMap class
    var root = await document.GetSyntaxRootAsync(cancellationToken);
    var newRoot = root.RemoveNode(classMapDecl, SyntaxRemoveOptions.KeepNoTrivia);

    // 9. Return updated solution
    var solution = document.Project.Solution;
    solution = solution.WithDocumentSyntaxRoot(targetDocument.Id, newTargetRoot);
    solution = solution.WithDocumentSyntaxRoot(document.Id, newRoot);

    return solution.GetDocument(document.Id);
}
```

---

## 8. Implementation Guide

### 8.1 Project Structure

```
CsvHandler.Analyzers/
├── CsvHandler.Analyzers/
│   ├── Analyzers/
│   │   ├── AttributeMigrationAnalyzer.cs
│   │   ├── TypeMigrationAnalyzer.cs
│   │   ├── ClassMapAnalyzer.cs
│   │   ├── ConfigurationAnalyzer.cs
│   │   └── MethodCallAnalyzer.cs
│   ├── CodeFixProviders/
│   │   ├── AttributeMigrationCodeFixProvider.cs
│   │   ├── TypeMigrationCodeFixProvider.cs
│   │   ├── ClassMapCodeFixProvider.cs
│   │   └── ConfigurationCodeFixProvider.cs
│   ├── Helpers/
│   │   ├── SyntaxHelper.cs
│   │   ├── SymbolHelper.cs
│   │   └── MappingExtractor.cs
│   └── Resources/
│       └── DiagnosticDescriptors.cs
├── CsvHandler.Analyzers.Tests/
│   ├── Analyzers/
│   ├── CodeFixProviders/
│   ├── Helpers/
│   └── TestHelpers.cs
└── CsvHandler.Analyzers.Package/
    ├── CsvHandler.Analyzers.nuspec
    ├── CsvHandler.Analyzers.editorconfig
    └── CsvHandler.Analyzers.props
```

### 8.2 Diagnostic ID Scheme

```csharp
public static class DiagnosticIds
{
    // Type migrations (MIG001-099)
    public const string LegacyTypeUsage = "MIG001";
    public const string LegacyNamespaceUsage = "MIG002";

    // Attribute migrations (MIG100-199)
    public const string LegacyAttributeUsage = "MIG100";
    public const string CombineAttributes = "MIG101";
    public const string MissingCsvRecord = "MIG102";
    public const string MissingPartial = "MIG103";

    // ClassMap migrations (MIG200-299)
    public const string ClassMapDetected = "MIG200";
    public const string ClassMapConversionAvailable = "MIG201";

    // Configuration migrations (MIG300-399)
    public const string ConfigurationUsage = "MIG300";
    public const string DeprecatedConfigProperty = "MIG301";

    // Method migrations (MIG400-499)
    public const string SyncMethodUsage = "MIG400";
    public const string DeprecatedMethod = "MIG401";

    // Best practices (MIG500-599)
    public const string PreferAsync = "MIG500";
    public const string ConsiderBuffering = "MIG501";
}
```

### 8.3 Implementation Steps

**Phase 1: Basic Type Detection (Week 1-2)**

1. ✅ Create analyzer project
2. ✅ Detect CsvHelper namespace usage
3. ✅ Detect CsvReader/CsvWriter type usage
4. ✅ Basic diagnostic reporting
5. ✅ Write unit tests

**Phase 2: Attribute Migration (Week 3-4)**

1. ✅ Detect CsvHelper attributes
2. ✅ Implement attribute code fixes
3. ✅ Handle combined attributes (Index + Name → CsvField)
4. ✅ Add [CsvRecord] and partial
5. ✅ Test edge cases

**Phase 3: ClassMap Conversion (Week 5-6)**

1. ✅ Detect ClassMap inheritance
2. ✅ Extract mapping information
3. ✅ Find target types
4. ✅ Generate attributes on target
5. ✅ Remove ClassMap class

**Phase 4: Configuration & Methods (Week 7-8)**

1. ✅ Detect CsvConfiguration usage
2. ✅ Map configuration properties
3. ✅ Detect method calls
4. ✅ Suggest async alternatives
5. ✅ Comprehensive testing

**Phase 5: Polish & Package (Week 9-10)**

1. ✅ Fix All provider
2. ✅ EditorConfig integration
3. ✅ NuGet packaging
4. ✅ Documentation
5. ✅ Release

### 8.4 Code Snippets Library

**Detecting Type in Namespace:**

```csharp
private static bool IsLegacyType(INamedTypeSymbol typeSymbol)
{
    return typeSymbol.ContainingNamespace.ToDisplayString()
        .StartsWith("CsvHelper");
}
```

**Creating Attribute Syntax:**

```csharp
private AttributeListSyntax CreateCsvFieldAttribute(
    int? index,
    string name,
    bool optional,
    string format)
{
    var arguments = new List<AttributeArgumentSyntax>();

    if (index.HasValue)
    {
        arguments.Add(SyntaxFactory.AttributeArgument(
            SyntaxFactory.NameColon("Index"),
            null,
            SyntaxFactory.LiteralExpression(
                SyntaxKind.NumericLiteralExpression,
                SyntaxFactory.Literal(index.Value))));
    }

    if (!string.IsNullOrEmpty(name))
    {
        arguments.Add(SyntaxFactory.AttributeArgument(
            SyntaxFactory.NameColon("Name"),
            null,
            SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(name))));
    }

    if (optional)
    {
        arguments.Add(SyntaxFactory.AttributeArgument(
            SyntaxFactory.NameColon("Optional"),
            null,
            SyntaxFactory.LiteralExpression(
                SyntaxKind.TrueLiteralExpression)));
    }

    if (!string.IsNullOrEmpty(format))
    {
        arguments.Add(SyntaxFactory.AttributeArgument(
            SyntaxFactory.NameColon("Format"),
            null,
            SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(format))));
    }

    return SyntaxFactory.AttributeList(
        SyntaxFactory.SingletonSeparatedList(
            SyntaxFactory.Attribute(
                SyntaxFactory.IdentifierName("CsvField"),
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SeparatedList(arguments)))))
        .NormalizeWhitespace();
}
```

**Adding Using Directive:**

```csharp
private CompilationUnitSyntax AddUsingDirective(
    CompilationUnitSyntax root,
    string namespaceName)
{
    // Check if using already exists
    if (root.Usings.Any(u => u.Name.ToString() == namespaceName))
        return root;

    var newUsing = SyntaxFactory.UsingDirective(
        SyntaxFactory.IdentifierName(namespaceName))
        .NormalizeWhitespace();

    return root.AddUsings(newUsing);
}
```

**Making Class Partial:**

```csharp
private ClassDeclarationSyntax MakePartial(ClassDeclarationSyntax classDecl)
{
    if (classDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
        return classDecl;

    var partialModifier = SyntaxFactory.Token(SyntaxKind.PartialKeyword);

    return classDecl.AddModifiers(partialModifier);
}
```

---

## 9. Best Practices

### 9.1 Performance

1. **Enable Concurrent Execution**
   ```csharp
   context.EnableConcurrentExecution();
   ```

2. **Use Symbol Analysis Over Syntax**
   - Symbols are cached and shared
   - More efficient than syntax tree walking

3. **Avoid Rooting Symbols**
   - Extract strings, don't store symbols
   - Prevents memory leaks

4. **Use Incremental Patterns**
   - Register specific syntax kinds
   - Don't analyze everything

5. **Skip Generated Code**
   ```csharp
   context.ConfigureGeneratedCodeAnalysis(
       GeneratedCodeAnalysisFlags.None);
   ```

### 9.2 User Experience

1. **Clear Diagnostic Messages**
   ```csharp
   var diagnostic = Diagnostic.Create(
       Rule,
       location,
       "Replace IndexAttribute with CsvFieldAttribute(Index = {0})",
       index);
   ```

2. **Provide Multiple Fix Options**
   - Quick fix (single item)
   - Fix document
   - Fix project
   - Fix solution

3. **Preserve Code Style**
   - Use `.NormalizeWhitespace()`
   - Use `.WithTriviaFrom(original)`
   - Match existing formatting

4. **Non-Breaking Suggestions**
   - Use `DiagnosticSeverity.Info` for suggestions
   - Use `DiagnosticSeverity.Warning` for recommended changes
   - Use `DiagnosticSeverity.Error` only for breaking issues

### 9.3 Maintainability

1. **Separate Concerns**
   - One analyzer per concern
   - One code fix per diagnostic
   - Clear helper classes

2. **Comprehensive Tests**
   - Test each diagnostic
   - Test each code fix
   - Test Fix All
   - Test edge cases

3. **Version Carefully**
   - Semantic versioning
   - Document breaking changes
   - Provide migration guides

4. **Document Diagnostics**
   ```csharp
   private static readonly DiagnosticDescriptor Rule = new(
       id: "MIG001",
       title: "Legacy CsvHelper type usage",
       messageFormat: "Type '{0}' is from CsvHelper. Consider migrating to '{1}'",
       category: "Migration",
       defaultSeverity: DiagnosticSeverity.Warning,
       isEnabledByDefault: true,
       description: "CsvHelper types should be migrated to CsvHandler for better performance.",
       helpLinkUri: "https://docs.csvhandler.dev/migration/mig001");
   ```

### 9.4 Testing

1. **Test Matrix**
   - ✅ Happy path
   - ✅ Edge cases
   - ✅ Error conditions
   - ✅ Performance
   - ✅ Partial classes
   - ✅ Nested types
   - ✅ Generic types

2. **Use Real-World Scenarios**
   - Test against actual CsvHelper code
   - Include complex ClassMaps
   - Test large files

3. **Regression Testing**
   - Keep all tests passing
   - Add test for each bug fix
   - CI/CD integration

### 9.5 Distribution

1. **Clear Documentation**
   - README with examples
   - Migration guide
   - Diagnostic reference

2. **Changelog**
   - Keep updated
   - Link to issues
   - Migration instructions

3. **Versioning Strategy**
   - Match library version
   - Clear compatibility notes
   - Pre-release for testing

---

## 10. Resources and References

### 10.1 Official Documentation

- **Roslyn Repository:** https://github.com/dotnet/roslyn
- **Roslyn SDK:** https://github.com/dotnet/roslyn-sdk
- **How to Write an Analyzer:** https://github.com/dotnet/roslyn/blob/main/docs/wiki/How-To-Write-a-C%23-Analyzer-and-Code-Fix.md
- **Microsoft.CodeAnalysis.Testing:** https://github.com/dotnet/roslyn-sdk/tree/main/src/Microsoft.CodeAnalysis.Testing

### 10.2 Learning Resources

- **Learn Roslyn Now Series:** https://joshvarty.com/learn-roslyn-now/
- **Roslyn Quoter:** http://roslynquoter.azurewebsites.net/ (Generate SyntaxFactory code)
- **SharpLab:** https://sharplab.io/ (View syntax trees)

### 10.3 Example Projects

- **xUnit Analyzers:** https://github.com/xunit/xunit.analyzers
- **.NET Upgrade Assistant:** https://github.com/dotnet/upgrade-assistant
- **StyleCop Analyzers:** https://github.com/DotNetAnalyzers/StyleCopAnalyzers
- **Roslynator:** https://github.com/JosefPihrt/Roslynator

### 10.4 Tools

- **Roslyn Syntax Visualizer** - Visual Studio extension
- **Roslyn Quoter** - Generate SyntaxFactory code
- **SharpLab** - Online C# playground with IL/syntax tree view

### 10.5 Communities

- **Roslyn Discussions:** https://github.com/dotnet/roslyn/discussions
- **Stack Overflow:** [roslyn-code-analysis] tag
- **Discord:** .NET Discord server (#roslyn channel)

### 10.6 Books

- *".NET Development Using the Compiler API"* by Jason Bock
- *"Roslyn Cookbook"* by Manish Vasani

---

## Appendix A: Complete Analyzer Example

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
    public class CsvHelperAttributeMigrationAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MIG100";

        private static readonly LocalizableString Title =
            "Legacy CsvHelper attribute detected";

        private static readonly LocalizableString MessageFormat =
            "Replace {0} with CsvHandler.{1}";

        private static readonly LocalizableString Description =
            "CsvHelper attributes should be migrated to CsvHandler attributes.";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            Title,
            MessageFormat,
            "Migration",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://docs.csvhandler.dev/migration/mig100");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(
                GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(
                AnalyzeAttribute,
                SyntaxKind.Attribute);
        }

        private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
        {
            var attribute = (AttributeSyntax)context.Node;

            var symbolInfo = context.SemanticModel.GetSymbolInfo(
                attribute,
                context.CancellationToken);

            if (symbolInfo.Symbol is not IMethodSymbol ctorSymbol)
                return;

            var attributeType = ctorSymbol.ContainingType;
            var namespaceName = attributeType.ContainingNamespace.ToDisplayString();

            if (namespaceName != "CsvHelper.Configuration.Attributes")
                return;

            var attributeName = attributeType.Name;
            var (newAttributeName, propertyName) = MapAttribute(attributeName);

            if (newAttributeName == null)
                return;

            var diagnostic = Diagnostic.Create(
                Rule,
                attribute.GetLocation(),
                attributeName,
                newAttributeName);

            context.ReportDiagnostic(diagnostic);
        }

        private static (string newAttribute, string property) MapAttribute(string oldName)
        {
            return oldName switch
            {
                "IndexAttribute" => ("CsvFieldAttribute", "Index"),
                "NameAttribute" => ("CsvFieldAttribute", "Name"),
                "OptionalAttribute" => ("CsvFieldAttribute", "Optional"),
                "IgnoreAttribute" => ("CsvIgnoreAttribute", null),
                "FormatAttribute" => ("CsvFieldAttribute", "Format"),
                _ => (null, null)
            };
        }
    }
}
```

---

## Appendix B: Complete Code Fix Example

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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CsvHelperAttributeMigrationCodeFixProvider))]
    [Shared]
    public class CsvHelperAttributeMigrationCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create("MIG100");

        public sealed override FixAllProvider GetFixAllProvider()
            => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(
                context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var attribute = root.FindNode(diagnosticSpan)
                .AncestorsAndSelf()
                .OfType<AttributeSyntax>()
                .First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Migrate to CsvHandler attribute",
                    createChangedDocument: c => MigrateAttributeAsync(
                        context.Document,
                        attribute,
                        c),
                    equivalenceKey: "MigrateAttribute"),
                diagnostic);
        }

        private async Task<Document> MigrateAttributeAsync(
            Document document,
            AttributeSyntax attribute,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            // Get attribute type
            var symbolInfo = semanticModel.GetSymbolInfo(attribute, cancellationToken);
            var attributeType = ((IMethodSymbol)symbolInfo.Symbol).ContainingType;

            // Create replacement
            var newAttribute = CreateReplacementAttribute(
                attribute,
                attributeType.Name);

            // Add using directive if needed
            var compilationUnit = root as CompilationUnitSyntax;
            compilationUnit = AddUsingDirective(compilationUnit, "CsvHandler");

            // Replace attribute
            var newRoot = compilationUnit.ReplaceNode(attribute, newAttribute);

            return document.WithSyntaxRoot(newRoot);
        }

        private AttributeSyntax CreateReplacementAttribute(
            AttributeSyntax original,
            string oldAttributeName)
        {
            var (newName, propertyName) = MapAttributeName(oldAttributeName);

            if (propertyName == null)
            {
                // Simple replacement (e.g., Ignore → CsvIgnore)
                return SyntaxFactory.Attribute(
                        SyntaxFactory.IdentifierName(newName.Replace("Attribute", "")))
                    .WithTriviaFrom(original);
            }

            // Convert to property syntax (e.g., Index(0) → CsvField(Index = 0))
            var arguments = original.ArgumentList?.Arguments ??
                SyntaxFactory.SeparatedList<AttributeArgumentSyntax>();

            var newArguments = arguments.Select(arg =>
            {
                if (arg.NameColon == null && arg.NameEquals == null)
                {
                    // Positional argument → named argument
                    return arg.WithNameColon(
                        SyntaxFactory.NameColon(propertyName));
                }
                return arg;
            });

            return SyntaxFactory.Attribute(
                    SyntaxFactory.IdentifierName(newName.Replace("Attribute", "")),
                    SyntaxFactory.AttributeArgumentList(
                        SyntaxFactory.SeparatedList(newArguments)))
                .NormalizeWhitespace()
                .WithTriviaFrom(original);
        }

        private CompilationUnitSyntax AddUsingDirective(
            CompilationUnitSyntax root,
            string namespaceName)
        {
            if (root.Usings.Any(u => u.Name.ToString() == namespaceName))
                return root;

            var newUsing = SyntaxFactory.UsingDirective(
                    SyntaxFactory.IdentifierName(namespaceName))
                .NormalizeWhitespace();

            return root.AddUsings(newUsing);
        }

        private (string newName, string property) MapAttributeName(string oldName)
        {
            return oldName switch
            {
                "IndexAttribute" => ("CsvFieldAttribute", "Index"),
                "NameAttribute" => ("CsvFieldAttribute", "Name"),
                "OptionalAttribute" => ("CsvFieldAttribute", "Optional"),
                "IgnoreAttribute" => ("CsvIgnoreAttribute", null),
                "FormatAttribute" => ("CsvFieldAttribute", "Format"),
                _ => (null, null)
            };
        }
    }
}
```

---

## Conclusion

This research provides a comprehensive foundation for building Roslyn analyzers and code fix providers for migrating from CsvHelper to CsvHandler. Key takeaways:

1. **Proven Patterns Exist** - Migration analyzers follow well-established patterns
2. **Testing is Critical** - Microsoft.CodeAnalysis.Testing makes this straightforward
3. **Performance Matters** - Use symbol analysis and incremental patterns
4. **User Experience is Key** - Clear messages, multiple fix options, preserve formatting
5. **Distribution is Standard** - NuGet with EditorConfig integration

The CsvHelper → CsvHandler migration is well-suited for this approach due to:
- Clear attribute mappings
- Similar API structure
- Well-defined transformation rules
- Incremental migration path

**Recommended Next Steps:**
1. Implement basic type detection analyzer
2. Add attribute migration code fixes
3. Build comprehensive test suite
4. Tackle ClassMap conversion
5. Package and distribute

**Success Metrics:**
- ✅ 90%+ of migrations automated
- ✅ Clear error messages for manual cases
- ✅ Preserves code formatting
- ✅ Fix All works correctly
- ✅ Fast enough for real-time analysis

---

*Document Version: 1.0*
*Last Updated: 2025-10-15*
*Research conducted for CsvHandler project*

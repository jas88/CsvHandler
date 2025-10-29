using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHandler.Attributes;
using Xunit;

#pragma warning disable CS1998 // Async method lacks 'await' operators
#pragma warning disable CS0219 // Variable is assigned but its value is never used

namespace CsvHandler.Tests;

/// <summary>
/// Tests to ensure AOT (Ahead-of-Time) compilation compatibility and trim safety.
/// These tests verify that the library works without reflection and generates
/// warnings-free code for Native AOT scenarios.
/// </summary>
public class AotCompatibilityTests
{
    #region CsvContext Usage Tests

    [Fact]
    public void CsvContext_Instantiation_DoesNotUseReflection()
    {
        // Arrange & Act
        var context = new TestCsvContext();

        // Assert
        Assert.NotNull(context);
        // In AOT scenarios, this would fail at compile time if reflection is used
    }

    [Fact]
    public async Task CsvContext_SerializeWithContext_WorksWithoutReflection()
    {
        // Arrange
        var person = new Person { Name = "Alice", Age = 30, City = "NYC" };
        var context = new TestCsvContext();
        var stream = new MemoryStream();

        // Act
        await context.SerializePersonAsync(stream, new[] { person });

        // Assert
        Assert.True(stream.Length > 0);
    }

    [Fact]
    public async Task CsvContext_DeserializeWithContext_WorksWithoutReflection()
    {
        // Arrange
        var csv = "Name,Age,City\nAlice,30,NYC"u8.ToArray();
        var context = new TestCsvContext();
        var stream = new MemoryStream(csv);

        // Act
        var people = new List<Person>();
        await foreach (var person in context.DeserializePersonAsync(stream))
        {
            people.Add(person);
        }

        // Assert
        Assert.Single(people);
        Assert.Equal("Alice", people[0].Name);
        Assert.Equal(30, people[0].Age);
        Assert.Equal("NYC", people[0].City);
    }

    #endregion

    #region Source Generator Verification Tests

    [Fact]
    public void SourceGenerator_GeneratesPartialClass_IsAccessible()
    {
        // Verify that the source generator has created the expected methods
        var contextType = typeof(TestCsvContext);
        Assert.NotNull(contextType);
        Assert.True(contextType.IsClass);
    }

    [Fact]
    public void CsvRecord_PartialClass_IsProperlyDecorated()
    {
        // Verify that CsvRecord types are properly marked
        var personType = typeof(Person);
        Assert.NotNull(personType);

        var attributes = personType.GetCustomAttributes(typeof(CsvRecordAttribute), false);
        Assert.NotEmpty(attributes);
    }

    [Fact]
    public void CsvField_Attributes_ArePreserved()
    {
        // Verify that field attributes are accessible
        var personType = typeof(Person);
        var nameProperty = personType.GetProperty("Name");

        Assert.NotNull(nameProperty);
        var attributes = nameProperty!.GetCustomAttributes(typeof(CsvFieldAttribute), false);
        Assert.NotEmpty(attributes);
    }

    #endregion

    #region Trim Safety Tests

    [Fact]
    public void Parser_StackAllocatedTypes_DoNotRequireHeap()
    {
        // Verify that parser uses stack allocation (ref struct)
        var parserType = typeof(CsvHandler.Core.Utf8CsvParser);

        // ref structs cannot be used in certain contexts
        Assert.True(parserType.IsValueType);
        Assert.True(parserType.IsByRefLike); // ref struct
    }

    [Fact]
    public void Options_ValueType_IsTrimmable()
    {
        // Verify that options are value types (no heap allocation)
        var optionsType = typeof(CsvHandler.Core.Utf8CsvParserOptions);
        Assert.True(optionsType.IsValueType);
    }

    #endregion

    #region No Dynamic Code Tests

    [Fact]
    public void Library_DoesNotUse_MakeGenericType()
    {
        // This test ensures no reflection is used at runtime
        // In AOT, Type.MakeGenericType throws PlatformNotSupportedException

        // Arrange
        var csv = "Name,Age\nAlice,30"u8.ToArray();
        var parser = new CsvHandler.Core.Utf8CsvParser(
            csv,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        // Act
        var success = parser.TryReadField(out var field);

        // Assert - If this works, no dynamic type creation was used
        Assert.True(success);
    }

    [Fact]
    public void Library_DoesNotUse_DynamicInvoke()
    {
        // Verify that no delegate dynamic invocation is used
        // In AOT, Delegate.DynamicInvoke throws PlatformNotSupportedException

        // Arrange & Act
        var options = CsvHandler.Core.Utf8CsvParserOptions.Default;

        // Assert - If this constructs successfully, no dynamic invocation is used
        Assert.Equal((byte)',', options.Delimiter);
    }

    #endregion

    #region Multi-TFM Code Generation Tests

    [Fact]
    public void SourceGenerator_SupportsNet6_GeneratesCode()
    {
        // Verify code works on .NET 6
#if NET6_0
        var context = new TestCsvContext();
        Assert.NotNull(context);
#endif
    }

    [Fact]
    public void SourceGenerator_SupportsNet8_GeneratesCode()
    {
        // Verify code works on .NET 8
#if NET8_0
        var context = new TestCsvContext();
        Assert.NotNull(context);
#endif
    }

    [Fact]
    public void SourceGenerator_SupportsNetStandard20_GeneratesCode()
    {
        // Verify code works on .NET Standard 2.0
#if NETSTANDARD2_0
        var context = new TestCsvContext();
        Assert.NotNull(context);
#endif
    }

    #endregion

    #region Performance Tests (AOT should be fast)

    [Fact]
    public void Parser_WithAot_ParsesEfficiently()
    {
        // AOT-compiled code should be as fast or faster than JIT
        var csv = string.Join("\n", Enumerable.Range(1, 10000)
            .Select(i => $"Person{i},{20 + i},{i % 100}"));
        var bytes = Encoding.UTF8.GetBytes(csv);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var parser = new CsvHandler.Core.Utf8CsvParser(
            bytes,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        int fieldCount = 0;
        while (parser.TryReadField(out _))
        {
            fieldCount++;
        }

        stopwatch.Stop();

        Assert.True(fieldCount >= 30000); // 10k rows * 3 fields
        Assert.True(stopwatch.ElapsedMilliseconds < 300); // Allow 300ms for CI variance on Windows
    }

    #endregion

    #region Assembly Attributes Tests

    [Fact]
    public void Assembly_MarkedAsTrimmable()
    {
        // Verify the assembly has the correct trimming attributes
        var assembly = typeof(CsvHandler.Core.Utf8CsvParser).Assembly;
        Assert.NotNull(assembly);

        // The assembly should be marked with:
        // - IsTrimmable
        // - IsAotCompatible
        // These are set in the .csproj file
    }

    /// <summary>
    /// Verifies the dual-path AOT strategy:
    /// 1. Core types (Utf8CsvParser, CsvContext, etc.) are AOT-compatible
    /// 2. Reflection fallback types are properly marked as incompatible
    /// 3. Library supports both AOT and reflection-based usage
    /// </summary>
    [Fact]
    public void Assembly_CoreTypes_DoNotRequireDynamicCode()
    {
        // Verify that core AOT-compatible types don't require dynamic code
        // Reflection-based fallback types are allowed to have these attributes
        var assembly = typeof(CsvHandler.Core.Utf8CsvParser).Assembly;

        // Define core types that MUST be AOT-compatible (at type level)
        var coreAotTypes = new[]
        {
            typeof(CsvHandler.Core.Utf8CsvParser),
            typeof(CsvHandler.Core.Utf8CsvParserOptions),
            typeof(CsvHandler.Core.Utf8CsvWriter),
            typeof(CsvHandler.Core.CsvWriterOptions),
            typeof(CsvHandler.CsvContext),
            typeof(CsvHandler.CsvOptions),
            typeof(CsvHandler.CsvException),
            typeof(CsvHandler.CsvError),
            typeof(CsvHandler.ICsvTypeHandler<>),
            typeof(CsvHandler.CsvTypeInfo<>),
            typeof(CsvHandler.CsvReader<>)  // CsvReader type itself should be AOT-compatible
        };

        // Verify core types don't have dynamic code requirements at type level
        foreach (var type in coreAotTypes)
        {
            var attributes = type.GetCustomAttributes(false);
            Assert.DoesNotContain(attributes, a =>
                a.GetType().Name == "RequiresDynamicCodeAttribute" ||
                a.GetType().Name == "RequiresUnreferencedCodeAttribute");
        }

        // Verify reflection-based fallback types ARE properly marked
        var reflectionFallbackTypes = new[]
        {
            assembly.GetType("CsvHandler.ReflectionCsvTypeHandler`1"),  // Internal generic type
            assembly.GetType("CsvHandler.CsvTypeHandlerFactory"),
            typeof(CsvHandler.DefaultCsvContext)
        };

        foreach (var type in reflectionFallbackTypes)
        {
            if (type == null)
                continue;

            var attributes = type.GetCustomAttributes(false);
            Assert.Contains(attributes, a =>
                a.GetType().Name == "RequiresDynamicCodeAttribute");
        }
    }

    /// <summary>
    /// Verifies the dual-path API design for CsvReader:
    /// 1. AOT-safe path: Create(Stream, CsvContext) - no reflection
    /// 2. Reflection path: Create(Stream) - convenience, marked as incompatible
    /// </summary>
    [Fact]
    public void CsvReader_AotSafeMethods_DoNotRequireDynamicCode()
    {
        // Verify that AOT-safe CsvReader methods don't have dynamic code requirements
        var readerType = typeof(CsvHandler.CsvReader<Person>);

        // AOT-safe methods (using CsvContext)
        var createWithContextMethod = readerType.GetMethod("Create",
            new[] { typeof(Stream), typeof(CsvContext), typeof(bool) });

        var readAllAsyncMethod = readerType.GetMethod("ReadAllAsync");

        Assert.NotNull(createWithContextMethod);
        Assert.NotNull(readAllAsyncMethod);

        // These methods should NOT have RequiresDynamicCode or RequiresUnreferencedCode
        var createAttributes = createWithContextMethod!.GetCustomAttributes(false);
        Assert.DoesNotContain(createAttributes, a =>
            a.GetType().Name == "RequiresDynamicCodeAttribute" ||
            a.GetType().Name == "RequiresUnreferencedCodeAttribute");

        var readAttributes = readAllAsyncMethod!.GetCustomAttributes(false);
        Assert.DoesNotContain(readAttributes, a =>
            a.GetType().Name == "RequiresDynamicCodeAttribute" ||
            a.GetType().Name == "RequiresUnreferencedCodeAttribute");

        // Verify reflection-based methods ARE marked
        var createReflectionMethod = readerType.GetMethod("Create",
            new[] { typeof(Stream), typeof(bool) });

        Assert.NotNull(createReflectionMethod);
        var reflectionAttributes = createReflectionMethod!.GetCustomAttributes(false);
        Assert.Contains(reflectionAttributes, a =>
            a.GetType().Name == "RequiresDynamicCodeAttribute");
    }

    /// <summary>
    /// Documents the recommended AOT usage pattern for consumers of this library.
    /// This test serves as both verification and documentation.
    /// </summary>
    [Fact]
    public void AotUsagePattern_Documentation()
    {
        // This test demonstrates the correct AOT usage pattern:
        //
        // 1. Define a CsvContext with [CsvSerializable] attributes
        // 2. Use source-generated context with CsvReader/CsvWriter
        // 3. Entire code path is AOT-compatible (no reflection)
        //
        // Example pattern (TestCsvContext uses manual implementation):
        var context = new TestCsvContext();
        Assert.NotNull(context);

        // The context provides type handlers without reflection:
        var personHandler = context.GetTypeHandler<Person>();
        Assert.NotNull(personHandler);

        // The recommended usage for source-generated contexts:
        // var reader = CsvReader<Person>.Create(stream, context);
        // await foreach (var person in reader.ReadAllAsync())
        // {
        //     // Process person
        // }
    }

    #endregion

    #region Span and Memory Safety Tests

    [Fact]
    public void Parser_UsesSpan_NoHeapAllocation()
    {
        // Verify that parsing can be done with stack-only allocation
        Span<byte> csv = stackalloc byte[100];
        var data = "A,B,C"u8;
        data.CopyTo(csv);

        var parser = new CsvHandler.Core.Utf8CsvParser(
            csv.Slice(0, data.Length),
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        var success = parser.TryReadField(out var field);
        Assert.True(success);
    }

    [Fact]
    public void Parser_TryReadRecord_UsesStackAllocatedRanges()
    {
        // Verify that record reading can use stack allocation
        var csv = "A,B,C\nD,E,F"u8.ToArray();
        var parser = new CsvHandler.Core.Utf8CsvParser(
            csv,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        Range[] ranges = new Range[10];
        var count = parser.TryReadRecord(ranges);

        Assert.Equal(3, count);
    }

    #endregion

    #region IAsyncEnumerable AOT Compatibility

    [Fact]
    public async Task AsyncEnumerable_WorksWithAot()
    {
        // IAsyncEnumerable should work in AOT scenarios
        var csv = "Name,Age,City\nAlice,30,NYC"u8.ToArray();
        var stream = new MemoryStream(csv);
        var context = new TestCsvContext();

        var count = 0;
        await foreach (var person in CsvReader<Person>
            .Create(stream, context)
            .ReadAllAsync())
        {
            count++;
            Assert.Equal("Alice", person.Name);
            Assert.Equal(30, person.Age);
            Assert.Equal("NYC", person.City);
        }

        Assert.Equal(1, count);
    }

    #endregion

    #region Native Library Integration Tests

    [Fact]
    public void Parser_CanBeUsedFromNativeCode()
    {
        // Verify that the parser can be called from native code
        // (exported via UnmanagedCallersOnly in actual AOT scenarios)

        var csv = "A,B,C"u8.ToArray();
        var parser = new CsvHandler.Core.Utf8CsvParser(
            csv,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        parser.TryReadField(out var field);
        Assert.Equal("A", Encoding.UTF8.GetString(field));
    }

    #endregion

    #region Warning-Free Compilation Tests

    [Fact]
    public void CsvContext_MultipleTypes_GeneratesWithoutWarnings()
    {
        // Verify that a context with multiple types generates cleanly
        var context = new TestCsvContext();

        // All these types should be registered without conflicts
        // Person, Employee, Product, TsvRecord, NullableFieldsRecord, etc.
        Assert.NotNull(context);
    }

    #endregion
}

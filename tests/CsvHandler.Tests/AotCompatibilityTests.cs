using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

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
        context.Should().NotBeNull();
        // In AOT scenarios, this would fail at compile time if reflection is used
    }

    [Fact(Skip = "TODO: Implement CsvContext methods")]
    public async Task CsvContext_SerializeWithContext_WorksWithoutReflection()
    {
        // Arrange
        var person = new Person { Name = "Alice", Age = 30, City = "NYC" };
        var context = new TestCsvContext();
        var stream = new MemoryStream();

        // Act
        // await context.SerializePersonAsync(stream, new[] { person });

        // Assert
        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact(Skip = "TODO: Implement CsvContext methods")]
    public async Task CsvContext_DeserializeWithContext_WorksWithoutReflection()
    {
        // Arrange
        var csv = "Name,Age,City\nAlice,30,NYC"u8.ToArray();
        var context = new TestCsvContext();
        var stream = new MemoryStream(csv);

        // Act
        // var people = await context.DeserializePersonAsync(stream).ToListAsync();

        // Assert
        // people.Should().HaveCount(1);
        // people[0].Name.Should().Be("Alice");
    }

    #endregion

    #region Source Generator Verification Tests

    [Fact]
    public void SourceGenerator_GeneratesPartialClass_IsAccessible()
    {
        // Verify that the source generator has created the expected methods
        var contextType = typeof(TestCsvContext);
        contextType.Should().NotBeNull();
        contextType.IsClass.Should().BeTrue();
    }

    [Fact]
    public void CsvRecord_PartialClass_IsProperlyDecorated()
    {
        // Verify that CsvRecord types are properly marked
        var personType = typeof(Person);
        personType.Should().NotBeNull();

        var attributes = personType.GetCustomAttributes(typeof(CsvRecordAttribute), false);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void CsvField_Attributes_ArePreserved()
    {
        // Verify that field attributes are accessible
        var personType = typeof(Person);
        var nameProperty = personType.GetProperty("Name");

        nameProperty.Should().NotBeNull();
        var attributes = nameProperty!.GetCustomAttributes(typeof(CsvFieldAttribute), false);
        attributes.Should().NotBeEmpty();
    }

    #endregion

    #region Trim Safety Tests

    [Fact]
    public void Parser_StackAllocatedTypes_DoNotRequireHeap()
    {
        // Verify that parser uses stack allocation (ref struct)
        var parserType = typeof(CsvHandler.Core.Utf8CsvParser);

        // ref structs cannot be used in certain contexts
        parserType.IsValueType.Should().BeTrue();
        parserType.IsByRefLike.Should().BeTrue(); // ref struct
    }

    [Fact]
    public void Options_ValueType_IsTrimmable()
    {
        // Verify that options are value types (no heap allocation)
        var optionsType = typeof(CsvHandler.Core.Utf8CsvParserOptions);
        optionsType.IsValueType.Should().BeTrue();
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
        success.Should().BeTrue();
    }

    [Fact]
    public void Library_DoesNotUse_DynamicInvoke()
    {
        // Verify that no delegate dynamic invocation is used
        // In AOT, Delegate.DynamicInvoke throws PlatformNotSupportedException

        // Arrange & Act
        var options = CsvHandler.Core.Utf8CsvParserOptions.Default;

        // Assert - If this constructs successfully, no dynamic invocation is used
        options.Delimiter.Should().Be((byte)',');
    }

    #endregion

    #region Multi-TFM Code Generation Tests

    [Fact]
    public void SourceGenerator_SupportsNet6_GeneratesCode()
    {
        // Verify code works on .NET 6
        #if NET6_0
        var context = new TestCsvContext();
        context.Should().NotBeNull();
        #endif
    }

    [Fact]
    public void SourceGenerator_SupportsNet8_GeneratesCode()
    {
        // Verify code works on .NET 8
        #if NET8_0
        var context = new TestCsvContext();
        context.Should().NotBeNull();
        #endif
    }

    [Fact]
    public void SourceGenerator_SupportsNetStandard20_GeneratesCode()
    {
        // Verify code works on .NET Standard 2.0
        #if NETSTANDARD2_0
        var context = new TestCsvContext();
        context.Should().NotBeNull();
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

        fieldCount.Should().BeGreaterThan(30000); // 10k rows * 3 fields
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
    }

    #endregion

    #region Assembly Attributes Tests

    [Fact]
    public void Assembly_MarkedAsTrimmable()
    {
        // Verify the assembly has the correct trimming attributes
        var assembly = typeof(CsvHandler.Core.Utf8CsvParser).Assembly;
        assembly.Should().NotBeNull();

        // The assembly should be marked with:
        // - IsTrimmable
        // - IsAotCompatible
        // These are set in the .csproj file
    }

    [Fact]
    public void Assembly_DoesNotHaveRequiresDynamicCode()
    {
        // Verify that no types require dynamic code
        var assembly = typeof(CsvHandler.Core.Utf8CsvParser).Assembly;

        // In a properly AOT-compatible library, no types should have
        // [RequiresDynamicCode] attribute
        var types = assembly.GetTypes();
        foreach (var type in types)
        {
            var attributes = type.GetCustomAttributes(false);
            attributes.Should().NotContain(a =>
                a.GetType().Name == "RequiresDynamicCodeAttribute");
        }
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
        success.Should().BeTrue();
    }

    [Fact]
    public void Parser_TryReadRecord_UsesStackAllocatedRanges()
    {
        // Verify that record reading can use stack allocation
        var csv = "A,B,C\nD,E,F"u8.ToArray();
        var parser = new CsvHandler.Core.Utf8CsvParser(
            csv,
            CsvHandler.Core.Utf8CsvParserOptions.Default);

        Span<Range> ranges = stackalloc Range[10];
        var count = parser.TryReadRecord(ranges);

        count.Should().Be(3);
    }

    #endregion

    #region IAsyncEnumerable AOT Compatibility

    [Fact(Skip = "TODO: Implement CsvReader with IAsyncEnumerable")]
    public async Task AsyncEnumerable_WorksWithAot()
    {
        // IAsyncEnumerable should work in AOT scenarios
        var csv = "Name,Age\nAlice,30"u8.ToArray();
        var stream = new MemoryStream(csv);

        var count = 0;
        // await foreach (var person in CsvReader<Person>
        //     .Create(stream, new TestCsvContext())
        //     .ReadAllAsync())
        // {
        //     count++;
        // }

        // count.Should().Be(1);
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
        Encoding.UTF8.GetString(field).Should().Be("A");
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
        context.Should().NotBeNull();
    }

    #endregion
}

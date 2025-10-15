# Trimming and Native AOT Architecture - Visual Diagrams

**Related**: 09-trimming-aot-architecture.md
**Date**: 2025-10-15

---

## 1. Multi-Tier Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    CsvHandler Library                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  Tier 1: Full Native AOT (net8.0+)                      │   │
│  ├─────────────────────────────────────────────────────────┤   │
│  │  • CsvReader<T>.Create(stream, context)                 │   │
│  │  • CsvWriter<T>.Create(stream, context)                 │   │
│  │  • CsvContext-based metadata                            │   │
│  │  • Zero reflection, full trimming                       │   │
│  │  • Binary: 4.1 MB, Startup: 85ms, Throughput: 2.6M/s   │   │
│  └─────────────────────────────────────────────────────────┘   │
│                             │                                    │
│                             ↓                                    │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  Tier 2: Trim-Compatible (net6.0, net7.0)              │   │
│  ├─────────────────────────────────────────────────────────┤   │
│  │  • CsvReader<T>.Create(stream, context) ← Preferred     │   │
│  │  • CsvReaderReflection ← Available with warnings       │   │
│  │  • PublishTrimmed=true supported                        │   │
│  │  • [RequiresUnreferencedCode] annotations              │   │
│  │  • Binary: 3.8 MB, Startup: 250ms, Throughput: 2.2M/s  │   │
│  └─────────────────────────────────────────────────────────┘   │
│                             │                                    │
│                             ↓                                    │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  Tier 3: Full Compatibility (netstandard2.0)           │   │
│  ├─────────────────────────────────────────────────────────┤   │
│  │  • CsvReaderReflection only                             │   │
│  │  • No source generation                                 │   │
│  │  • No trimming support                                  │   │
│  │  • Uses System.Memory polyfill                          │   │
│  │  • Binary: 15.2 MB, Startup: 420ms, Throughput: 476k/s │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 2. Source Generator Pattern

```
┌─────────────────────────────────────────────────────────────────┐
│  User Code (Compile Time)                                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  [CsvSerializable(typeof(Person))]                              │
│  [CsvSerializable(typeof(Product))]                             │
│  public partial class AppCsvContext : CsvContext                │
│  {                                                               │
│      // Source generator fills this in                          │
│  }                                                               │
│                                                                  │
│  public class Person                                             │
│  {                                                               │
│      public int Id { get; set; }                                │
│      public string Name { get; set; }                           │
│  }                                                               │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                            │
                            │ (Build Time)
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│  CsvHandler.Generator (Source Generator)                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  1. IIncrementalGenerator.Initialize()                          │
│  2. ForAttributeWithMetadataName("CsvSerializable")             │
│  3. Extract type metadata (Person, Product)                     │
│  4. Generate code:                                              │
│     • AppCsvContext.Default property                            │
│     • CsvTypeInfo<Person> Person property                       │
│     • Deserialize method (ReadOnlySpan<byte> → Person)          │
│     • Serialize method (Person → IBufferWriter<byte>)           │
│     • GetHeader method                                          │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                            │
                            │ (Generated)
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│  Generated Code (AppCsvContext.g.cs)                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  partial class AppCsvContext                                     │
│  {                                                               │
│      public static AppCsvContext Default { get; } = new();      │
│                                                                  │
│      public CsvTypeInfo<Person> Person                          │
│          => _Person ??= CreatePersonInfo();                     │
│                                                                  │
│      private static CsvTypeInfo<Person> CreatePersonInfo()      │
│      {                                                           │
│          return new CsvTypeInfo<Person>                         │
│          {                                                       │
│              Deserialize = (line, opts) =>                      │
│              {                                                   │
│                  var person = new Person();                     │
│                  var fields = line.Split(opts.Delimiter);       │
│                                                                  │
│                  // DIRECT PROPERTY ACCESS - NO REFLECTION      │
│                  person.Id = Utf8Parser.Parse<int>(fields[0]);  │
│                  person.Name = Utf8.GetString(fields[1]);       │
│                                                                  │
│                  return person;                                 │
│              },                                                  │
│              Serialize = (person, writer, opts) =>              │
│              {                                                   │
│                  // DIRECT PROPERTY ACCESS - NO REFLECTION      │
│                  Utf8Formatter.Format(person.Id, writer);       │
│                  writer.Write(opts.Delimiter);                  │
│                  WriteEscaped(person.Name, writer);             │
│              }                                                   │
│          };                                                      │
│      }                                                           │
│  }                                                               │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                            │
                            │ (Runtime)
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│  User Application (Runtime - Zero Reflection)                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  var reader = CsvReader<Person>.Create(                         │
│      stream,                                                     │
│      AppCsvContext.Default);  ← Uses generated code             │
│                                                                  │
│  await foreach (var person in reader.ReadAllAsync())            │
│  {                                                               │
│      // person.Id, person.Name already populated                │
│      // via generated Deserialize method                        │
│  }                                                               │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 3. Dual API Surface Design

```
┌──────────────────────────────────────────────────────────────────┐
│                     CsvHandler Public API                         │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │  PRIMARY API: Source-Generated (AOT-Safe)                  │ │
│  ├────────────────────────────────────────────────────────────┤ │
│  │                                                             │ │
│  │  public sealed class CsvReader<T> where T : class          │ │
│  │  {                                                          │ │
│  │      public static CsvReader<T> Create(                    │ │
│  │          Stream stream,                                     │ │
│  │          CsvContext context)  ← Requires context           │ │
│  │      {                                                      │ │
│  │          // Uses context.GetTypeInfo<T>()                  │ │
│  │          // Zero reflection                                │ │
│  │      }                                                      │ │
│  │                                                             │ │
│  │      public IEnumerable<T> ReadAll()                       │ │
│  │      {                                                      │ │
│  │          // Calls typeInfo.Deserialize (generated code)    │ │
│  │      }                                                      │ │
│  │  }                                                          │ │
│  │                                                             │ │
│  │  ✅ AOT-compatible                                         │ │
│  │  ✅ Trim-safe                                              │ │
│  │  ✅ Zero reflection                                        │ │
│  │  ✅ Best performance                                       │ │
│  │                                                             │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                   │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │  FALLBACK API: Reflection-Based (Compatibility)            │ │
│  ├────────────────────────────────────────────────────────────┤ │
│  │                                                             │ │
│  │  [RequiresUnreferencedCode("Uses reflection")]            │ │
│  │  [RequiresDynamicCode("May generate code at runtime")]    │ │
│  │  public sealed class CsvReaderReflection                   │ │
│  │  {                                                          │ │
│  │      public IEnumerable<T> GetRecords<T>()                 │ │
│  │          where T : class, new()                            │ │
│  │      {                                                      │ │
│  │          // Uses reflection                                │ │
│  │          var properties = typeof(T).GetProperties();       │ │
│  │          // ... property.SetValue(obj, value) ...          │ │
│  │      }                                                      │ │
│  │                                                             │ │
│  │      public IEnumerable<object> GetRecords(Type type)      │ │
│  │      {                                                      │ │
│  │          // Runtime type support                           │ │
│  │      }                                                      │ │
│  │                                                             │ │
│  │      public IEnumerable<dynamic> GetRecordsDynamic()       │ │
│  │      {                                                      │ │
│  │          // Not available in AOT                           │ │
│  │      }                                                      │ │
│  │  }                                                          │ │
│  │                                                             │ │
│  │  ⚠️ NOT AOT-compatible                                    │ │
│  │  ⚠️ NOT trim-safe                                         │ │
│  │  ⚠️ Compiler warnings                                     │ │
│  │  ⚠️ Slower performance                                    │ │
│  │                                                             │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

---

## 4. Compile-Time to Runtime Flow

```
┌─────────────────────────────────────────────────────────────────┐
│  COMPILE TIME                                                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  User writes:                                                    │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  [CsvSerializable(typeof(Person))]                        │  │
│  │  partial class AppCsvContext : CsvContext { }             │  │
│  │                                                            │  │
│  │  public class Person                                       │  │
│  │  {                                                         │  │
│  │      public int Id { get; set; }                          │  │
│  │      public string Name { get; set; }                     │  │
│  │  }                                                         │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│                    ↓ (Source Generator)                         │
│                                                                  │
│  Generator produces:                                             │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  partial class AppCsvContext                              │  │
│  │  {                                                         │  │
│  │      public CsvTypeInfo<Person> Person                    │  │
│  │      {                                                     │  │
│  │          get                                               │  │
│  │          {                                                 │  │
│  │              return new CsvTypeInfo<Person>               │  │
│  │              {                                             │  │
│  │                  Deserialize = (line, opts) =>            │  │
│  │                  {                                         │  │
│  │                      var p = new Person();                │  │
│  │                      var fields = line.Split(...);        │  │
│  │                      p.Id = Parse<int>(fields[0]);        │  │
│  │                      p.Name = GetString(fields[1]);       │  │
│  │                      return p;                            │  │
│  │                  }                                         │  │
│  │              };                                            │  │
│  │          }                                                 │  │
│  │      }                                                     │  │
│  │  }                                                         │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│                    ↓ (Compiler)                                 │
│                                                                  │
│  Compiled to IL:                                                 │
│  • Direct property assignments (ldarg, stfld)                   │
│  • No reflection metadata                                       │
│  • Inlined method calls                                         │
│  • AOT-ready                                                    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│  RUNTIME                                                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  User calls:                                                     │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  var reader = CsvReader<Person>.Create(                   │  │
│  │      stream,                                               │  │
│  │      AppCsvContext.Default);                              │  │
│  │                                                            │  │
│  │  await foreach (var person in reader.ReadAllAsync())      │  │
│  │  {                                                         │  │
│  │      Console.WriteLine(person.Name);                      │  │
│  │  }                                                         │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│                    ↓                                            │
│                                                                  │
│  Execution flow:                                                 │
│  1. CsvReader<Person>.Create()                                  │
│     • Calls context.GetTypeInfo<Person>()                       │
│     • Returns pre-generated CsvTypeInfo<Person>                 │
│     • NO REFLECTION LOOKUP                                      │
│                                                                  │
│  2. reader.ReadAllAsync()                                       │
│     • Reads CSV bytes                                           │
│     • Calls typeInfo.Deserialize(line, options)                 │
│     • Executes generated lambda:                                │
│       ┌────────────────────────────────────────────────────┐   │
│       │  var p = new Person();                             │   │
│       │  var fields = line.Split(delimiter);               │   │
│       │  p.Id = Utf8Parser.Parse<int>(fields[0]);          │   │
│       │  p.Name = Encoding.UTF8.GetString(fields[1]);      │   │
│       │  return p;                                          │   │
│       └────────────────────────────────────────────────────┘   │
│     • NO REFLECTION                                             │
│     • Direct property access                                    │
│     • Zero allocations (except Person instance)                 │
│                                                                  │
│  3. yield return person                                         │
│     • Fully-populated Person object                             │
│     • Ready to use                                              │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

**Key Insight:** The reflection happens at **compile time** (via source generator), not runtime. The generated code uses **direct property access** in the final binary.

---

## 5. Dynamic Type Handling (AOT vs Non-AOT)

```
┌─────────────────────────────────────────────────────────────────┐
│  Dynamic Type Scenarios                                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  net8.0 with PublishAot=true (Native AOT)                │  │
│  ├──────────────────────────────────────────────────────────┤  │
│  │                                                            │  │
│  │  ❌ GetRecords<dynamic>() - NOT AVAILABLE                │  │
│  │     Throws: NotSupportedException                         │  │
│  │     Reason: DLR not supported in AOT                      │  │
│  │                                                            │  │
│  │  ❌ GetRecords(Type) - NOT AVAILABLE                     │  │
│  │     Throws: NotSupportedException                         │  │
│  │     Reason: Runtime type creation requires reflection     │  │
│  │                                                            │  │
│  │  ❌ FastDynamicObject - NOT AVAILABLE                    │  │
│  │     Throws: NotSupportedException                         │  │
│  │     Reason: Implements IDynamicMetaObjectProvider         │  │
│  │                                                            │  │
│  │  ✅ Dictionary<string, string> - WORKS                   │  │
│  │     var records = stream.ReadAsDictionaries();            │  │
│  │     foreach (var row in records)                          │  │
│  │     {                                                      │  │
│  │         row["Name"] // String indexer - AOT-safe          │  │
│  │     }                                                      │  │
│  │                                                            │  │
│  │  ✅ Source-Generated Types - WORKS (Recommended)         │  │
│  │     [CsvSerializable(typeof(Person))]                     │  │
│  │     partial class AppCsvContext : CsvContext { }          │  │
│  │                                                            │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  net6.0/net7.0 or net8.0 without AOT                     │  │
│  ├──────────────────────────────────────────────────────────┤  │
│  │                                                            │  │
│  │  ⚠️ GetRecords<dynamic>() - WORKS with warning          │  │
│  │     [RequiresUnreferencedCode]                            │  │
│  │     [RequiresDynamicCode]                                 │  │
│  │     Compiler warning if trimming enabled                  │  │
│  │                                                            │  │
│  │  ⚠️ GetRecords(Type) - WORKS with warning               │  │
│  │     [RequiresUnreferencedCode]                            │  │
│  │     Compiler warning if trimming enabled                  │  │
│  │                                                            │  │
│  │  ⚠️ FastDynamicObject - WORKS with warning              │  │
│  │     [RequiresDynamicCode]                                 │  │
│  │     Increases binary size with trimming                   │  │
│  │                                                            │  │
│  │  ✅ Dictionary<string, string> - WORKS (no warnings)     │  │
│  │                                                            │  │
│  │  ✅ Source-Generated Types - WORKS (Recommended)         │  │
│  │                                                            │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  netstandard2.0                                           │  │
│  ├──────────────────────────────────────────────────────────┤  │
│  │                                                            │  │
│  │  ✅ GetRecords<dynamic>() - WORKS (no warnings)          │  │
│  │     No trimming support, so no restrictions               │  │
│  │                                                            │  │
│  │  ✅ GetRecords(Type) - WORKS (no warnings)               │  │
│  │                                                            │  │
│  │  ✅ FastDynamicObject - WORKS (no warnings)              │  │
│  │                                                            │  │
│  │  ✅ Dictionary<string, string> - WORKS                   │  │
│  │                                                            │  │
│  │  ❌ Source-Generated Types - NOT AVAILABLE               │  │
│  │     Source generators require net6.0+ SDK                 │  │
│  │     (Library can be netstandard2.0, but consumer needs   │  │
│  │      net6.0+ to use source generation)                    │  │
│  │                                                            │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 6. Trimming Analysis Flow

```
┌─────────────────────────────────────────────────────────────────┐
│  Trimming Analysis Process                                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  User Project:                                                   │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  <PropertyGroup>                                          │  │
│  │    <PublishTrimmed>true</PublishTrimmed>                 │  │
│  │    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>         │  │
│  │  </PropertyGroup>                                         │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│                    ↓ (dotnet publish)                           │
│                                                                  │
│  Trimmer Analysis:                                               │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  1. Scan all referenced assemblies                        │  │
│  │  2. Find root methods (entry points)                      │  │
│  │  3. Build call graph                                      │  │
│  │  4. Check for [RequiresUnreferencedCode] attributes      │  │
│  │  5. Warn if unreachable code accesses trimmed members    │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│                    ↓                                            │
│                                                                  │
│  CsvHandler Source-Gen Path:                                    │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  ✅ CsvReader<T>.Create(stream, context)                 │  │
│  │     • No [RequiresUnreferencedCode] attribute            │  │
│  │     • All code paths trim-safe                            │  │
│  │     • No warnings                                         │  │
│  │                                                            │  │
│  │  ✅ CsvTypeInfo<T>.Deserialize                           │  │
│  │     • Generated code - direct property access             │  │
│  │     • No reflection                                       │  │
│  │     • No warnings                                         │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│  CsvHandler Reflection Path:                                    │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  ⚠️ CsvReaderReflection.GetRecords<T>()                 │  │
│  │     • Has [RequiresUnreferencedCode] attribute           │  │
│  │     • Trimmer emits warning IL2026:                      │  │
│  │       "Method uses reflection which is not trim-safe"    │  │
│  │                                                            │  │
│  │  ⚠️ CsvReaderReflection.GetRecordsDynamic()             │  │
│  │     • Has [RequiresDynamicCode] attribute                │  │
│  │     • Trimmer emits warning IL3050:                      │  │
│  │       "Method requires dynamic code generation"          │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│                    ↓                                            │
│                                                                  │
│  User Options:                                                   │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  Option 1: Fix warnings (Recommended)                     │  │
│  │    • Migrate to CsvReader<T> with CsvContext             │  │
│  │    • Remove reflection API usage                          │  │
│  │    • Warnings disappear                                   │  │
│  │                                                            │  │
│  │  Option 2: Suppress warnings                              │  │
│  │    #pragma warning disable IL2026, IL3050                 │  │
│  │    var records = reader.GetRecords<Person>();            │  │
│  │    #pragma warning restore IL2026, IL3050                 │  │
│  │    • Warnings suppressed                                  │  │
│  │    • Increased binary size                                │  │
│  │    • Reflection preserved                                 │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 7. Performance Comparison Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│  Startup Time (Lower is Better)                                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Reflection-based    ████████████████████████████  420ms        │
│  (no trim)                                                       │
│                                                                  │
│  Source-gen (JIT)    ████████████  250ms                        │
│                                                                  │
│  Source-gen (AOT)    ████  85ms  ← 80% faster                   │
│                                                                  │
├─────────────────────────────────────────────────────────────────┤
│  Binary Size (Lower is Better)                                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Reflection-based    ████████████████████████████  15.2 MB      │
│  (no trim)                                                       │
│                                                                  │
│  Source-gen          ██████  3.8 MB  ← 75% smaller              │
│  + PublishTrimmed                                                │
│                                                                  │
│  Source-gen          ███████  4.1 MB  ← 73% smaller             │
│  + PublishAot                                                    │
│                                                                  │
├─────────────────────────────────────────────────────────────────┤
│  Throughput - 1M Records (Higher is Better)                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Reflection-based    ████████████  476k rec/s                   │
│  (no trim)                                                       │
│                                                                  │
│  Source-gen (JIT)    ███████████████████████████  2.2M rec/s    │
│                                            ← 4.6x faster         │
│                                                                  │
│  Source-gen (AOT)    ████████████████████████████████  2.6M     │
│                                              ← 5.5x faster       │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 8. Migration Timeline

```
┌─────────────────────────────────────────────────────────────────┐
│  CsvHandler Version Timeline                                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  v1.0 (Now)                                                      │
│  ├─ Introduce source-generated API                              │
│  ├─ CsvReader<T>/CsvWriter<T> with CsvContext                   │
│  ├─ Reflection APIs continue working                            │
│  └─ NO BREAKING CHANGES                                         │
│      │                                                           │
│      │ (3 months)                                                │
│      ↓                                                           │
│  v1.1                                                            │
│  ├─ Add [RequiresUnreferencedCode] to reflection APIs           │
│  ├─ Compiler warnings for reflection usage                      │
│  ├─ Documentation on AOT compatibility                           │
│  └─ NO BREAKING CHANGES (warnings only)                         │
│      │                                                           │
│      │ (6 months - community feedback)                          │
│      ↓                                                           │
│  v1.2-1.5                                                        │
│  ├─ Roslyn analyzers suggesting migration                       │
│  ├─ Code fixes to generate CsvContext                           │
│  ├─ Migration guides                                            │
│  ├─ Performance benchmarks                                      │
│  └─ NO BREAKING CHANGES                                         │
│      │                                                           │
│      │ (12 months - major version)                              │
│      ↓                                                           │
│  v2.0                                                            │
│  ├─ BREAKING: GetRecords<dynamic>() [Obsolete(error: true)]    │
│  ├─ BREAKING: Reflection APIs in separate package               │
│  ├─ Main package: Source-gen only                               │
│  └─ BREAKING CHANGES - migration required                       │
│                                                                  │
│  Package Structure:                                              │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  CsvHandler (main package)                                │  │
│  │  • Source-gen API only                                    │  │
│  │  • Trim-safe, AOT-compatible                              │  │
│  └──────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  CsvHandler.Reflection (optional)                         │  │
│  │  • Reflection fallback API                                │  │
│  │  • For compatibility scenarios                            │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

These diagrams provide visual clarity on the architecture, helping developers understand:

1. How the multi-tier system works
2. How source generation eliminates reflection
3. The dual API surface design
4. Compile-time vs runtime flow
5. Dynamic type handling across frameworks
6. Trimming analysis process
7. Performance improvements
8. Migration timeline

All diagrams use ASCII art for maximum portability and can be viewed in any text editor or markdown viewer.

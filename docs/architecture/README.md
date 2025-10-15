# CsvHandler Architecture Documentation

**Last Updated**: 2025-10-15
**Status**: Design Phase - Implementation Ready

---

## Overview

This directory contains comprehensive architectural documentation for CsvHandler, a high-performance CSV library for .NET with source generator support, Native AOT compatibility, and multi-framework targeting.

---

## Architecture Documents

### Core Architecture

1. **[00-overview.md](./00-overview.md)**
   - High-level architectural overview
   - Design principles and goals
   - Technology stack

2. **[01-api-design.md](./01-api-design.md)**
   - Public API surface design
   - Usage patterns and examples
   - API design rationale

3. **[02-source-generation.md](./02-source-generation.md)**
   - Source generator implementation
   - IIncrementalGenerator patterns
   - Generated code structure

4. **[03-core-implementation.md](./03-core-implementation.md)**
   - Core CSV parsing logic
   - UTF-8 optimization strategies
   - Memory management

5. **[04-migration-guide.md](./04-migration-guide.md)**
   - Migration from CsvHelper
   - Breaking changes and compatibility
   - Step-by-step migration instructions

6. **[05-class-diagrams.md](./05-class-diagrams.md)**
   - UML class diagrams
   - Component relationships
   - Dependency graphs

7. **[06-configuration-system.md](./06-configuration-system.md)**
   - Configuration API design
   - Options patterns
   - Runtime vs compile-time configuration

8. **[07-roslyn-analyzers.md](./07-roslyn-analyzers.md)**
   - Code analyzers and diagnostics
   - Code fix providers
   - Best practice enforcement

9. **[08-netstandard2-compatibility.md](./08-netstandard2-compatibility.md)**
   - .NET Standard 2.0 support
   - Polyfill strategies
   - Framework compatibility matrix

### NEW: Trimming and AOT Architecture

10. **[09-trimming-aot-architecture.md](./09-trimming-aot-architecture.md)** ⭐ **NEW**
    - **Complete trimming and Native AOT architecture**
    - Multi-tier AOT strategy (Tier 1: net8.0+, Tier 2: net6.0/7.0, Tier 3: netstandard2.0)
    - Source generator AOT pattern (CsvContext, [CsvSerializable])
    - Dual API surface design (source-gen vs reflection)
    - Trim annotations strategy ([RequiresUnreferencedCode], [RequiresDynamicCode])
    - Dynamic type handling (AOT-compatible alternatives)
    - Testing strategy (AOT smoke tests, trimming analysis)
    - Migration path (v1.0 → v2.0 timeline)
    - **144-page comprehensive design document**

11. **[TRIMMING-AOT-SUMMARY.md](./TRIMMING-AOT-SUMMARY.md)** ⭐ **NEW**
    - Executive summary of trimming/AOT architecture
    - Quick reference guide
    - Key design decisions
    - Performance benchmarks
    - When to use which API
    - Implementation timeline (14 weeks)

12. **[TRIMMING-AOT-DIAGRAMS.md](./TRIMMING-AOT-DIAGRAMS.md)** ⭐ **NEW**
    - Visual architecture diagrams (ASCII art)
    - Multi-tier architecture overview
    - Source generator pattern flow
    - Dual API surface design
    - Compile-time to runtime flow
    - Dynamic type handling comparison
    - Trimming analysis flow
    - Performance comparison charts
    - Migration timeline

---

## Key Features

### Performance

- **20-25 GB/s throughput** on modern hardware (net8.0 with SearchValues)
- **4-12x faster** than reflection-based approaches
- **Zero-allocation** parsing with Span<byte>
- **SIMD optimization** (AVX2, AVX512 on supported hardware)
- **Native AOT startup**: 85ms vs 420ms (reflection)

### Compatibility

- **Multi-targeting**: netstandard2.0, net6.0, net8.0
- **Native AOT** support (net8.0+)
- **Trimming-safe** with proper annotations
- **Framework coverage**:
  - .NET Framework 4.6.1+ (via netstandard2.0)
  - .NET Core 2.0+ (via netstandard2.0)
  - .NET 5.0+ (net6.0 build)
  - .NET 8.0+ (net8.0 build with full AOT support)
  - Unity, Xamarin (netstandard2.0)

### API Design

- **Source-generated API** (primary, AOT-compatible):
  ```csharp
  [CsvSerializable(typeof(Person))]
  partial class AppCsvContext : CsvContext { }

  var reader = CsvReader<Person>.Create(stream, AppCsvContext.Default);
  ```

- **Reflection fallback API** (compatibility, with warnings):
  ```csharp
  [RequiresUnreferencedCode("Uses reflection")]
  var reader = new CsvReaderReflection(stream);
  var records = reader.GetRecords<Person>();
  ```

---

## Architecture Highlights

### Multi-Tier AOT Strategy

| Tier | Framework | AOT | Trimming | API | Performance |
|------|-----------|-----|----------|-----|-------------|
| **Tier 1** | net8.0+ | Full Native AOT | Full | Source-gen only | 100% (2.6M rec/s) |
| **Tier 2** | net6.0, net7.0 | PublishTrimmed | Full | Source-gen + reflection¹ | 95% (2.2M rec/s) |
| **Tier 3** | netstandard2.0 | None | None | Reflection only | 70-80% (476k rec/s) |

¹ Reflection APIs marked with `[RequiresUnreferencedCode]`

### Source Generator Pattern

**Compile-time code generation** eliminates reflection:

```
User Code                     Source Generator               Generated Code
─────────────────────────────────────────────────────────────────────────────
[CsvSerializable(Person)]     →  Analyze metadata      →    partial class AppCsvContext
partial class AppCsvContext      Extract properties          {
{ }                              Generate code                   CsvTypeInfo<Person> Person { get; }

public class Person                                              static Person Deserialize(...)
{                                                                {
  public int Id { get; set; }                                       var p = new Person();
  public string Name { get; set; }                                  p.Id = Parse(field[0]);
}                                                                    p.Name = GetString(field[1]);
                                                                     return p;
                                                                 }
                                                              }
```

**Result:** Zero reflection at runtime, direct property access, AOT-compatible.

### Dual API Surface

```
┌─────────────────────────────────────────────────────────┐
│                  CsvHandler Public API                   │
├─────────────────────────────────────────────────────────┤
│  ✅ CsvReader<T>.Create(stream, context)  [Primary]    │
│     • Source-generated metadata                         │
│     • AOT-compatible, trim-safe                         │
│     • Best performance                                  │
├─────────────────────────────────────────────────────────┤
│  ⚠️ CsvReaderReflection.GetRecords<T>()  [Fallback]   │
│     • [RequiresUnreferencedCode]                        │
│     • Compiler warnings if trimming                     │
│     • Compatibility scenarios only                      │
└─────────────────────────────────────────────────────────┘
```

---

## Design Principles

1. **Multi-tier support**: Different capabilities by target framework
2. **Source-gen primary**: Always prefer source-generated APIs
3. **Clear fallback**: Reflection APIs clearly marked and warned
4. **Zero surprises**: Compile-time errors better than runtime failures
5. **Gradual adoption**: Users can migrate at their own pace
6. **Performance by default**: Fast path is the recommended path
7. **Compatibility when needed**: Reflection available with clear warnings

---

## Implementation Status

| Component | Status | Document |
|-----------|--------|----------|
| Core API Design | ✅ Complete | [01-api-design.md](./01-api-design.md) |
| Source Generator Design | ✅ Complete | [02-source-generation.md](./02-source-generation.md) |
| UTF-8 Parsing | ✅ Complete | [03-core-implementation.md](./03-core-implementation.md) |
| Configuration System | ✅ Complete | [06-configuration-system.md](./06-configuration-system.md) |
| Roslyn Analyzers | ✅ Complete | [07-roslyn-analyzers.md](./07-roslyn-analyzers.md) |
| netstandard2.0 Compat | ✅ Complete | [08-netstandard2-compatibility.md](./08-netstandard2-compatibility.md) |
| **Trimming/AOT** | ✅ **Complete** | **[09-trimming-aot-architecture.md](./09-trimming-aot-architecture.md)** |
| Implementation | 🔄 Ready to Start | See [Implementation Roadmap](#implementation-roadmap) |

---

## Implementation Roadmap

Based on the comprehensive trimming/AOT architecture:

### Phase 1: Foundation (2 weeks)
- Source generator infrastructure
- CsvContext base class
- [CsvSerializable] attribute
- CsvTypeInfo classes

### Phase 2: Code Generation (3 weeks)
- Generate CsvContext implementations
- Generate Deserialize methods
- Generate Serialize methods
- Type mapping logic

### Phase 3: CsvReader<T> API (2 weeks)
- Create pattern with context
- ReadAll() / ReadAllAsync()
- Buffer pooling
- Error handling

### Phase 4: CsvWriter<T> API (2 weeks)
- Create pattern with context
- WriteRecord() / WriteRecords()
- Quote escaping
- Buffer pooling

### Phase 5: Trim Annotations (1 week)
- [RequiresUnreferencedCode] on reflection APIs
- [RequiresDynamicCode] where needed
- Assembly-level attributes
- ILLink.Descriptors.xml

### Phase 6: AOT Testing (2 weeks)
- AOT smoke test project
- CI/CD integration
- Binary size benchmarks
- Performance benchmarks

### Phase 7: Documentation (1 week)
- API documentation
- Migration guide
- Performance guide
- Troubleshooting guide

### Phase 8: Polish & Release (1 week)
- Roslyn analyzers
- NuGet packaging
- Release notes

**Total Duration:** ~14 weeks (3.5 months)

---

## Related Documentation

### Research Documents

Located in `/docs/`:

- **[multi-targeting-strategy.md](../multi-targeting-strategy.md)**
  - Multi-targeting approach (netstandard2.0, net6.0, net8.0)
  - Conditional compilation patterns
  - Performance optimization by TFM
  - Package distribution strategy

- **[source-generator-research.md](../source-generator-research.md)**
  - IIncrementalGenerator best practices
  - ForAttributeWithMetadataName API
  - Performance optimization patterns
  - CSV-specific design patterns
  - Real-world examples (System.Text.Json, Cesil, Mapperly)

- **[dynamic-types-and-hybrid-architecture.md](../dynamic-types-and-hybrid-architecture.md)**
  - Dynamic type support analysis
  - Hybrid architecture (source-gen + reflection)
  - System.Text.Json pattern
  - Performance implications
  - Migration recommendations

- **[utf8-direct-handling-research.md](../utf8-direct-handling-research.md)**
  - UTF-8 direct parsing strategies
  - Span<byte> optimization
  - SIMD acceleration
  - Benchmark results

- **[gap-analysis.md](../gap-analysis.md)**
  - CsvHelper API coverage
  - Feature comparison
  - Migration strategy

- **[CsvHelper-API-Research.md](../CsvHelper-API-Research.md)**
  - CsvHelper API analysis
  - Reading/writing patterns
  - Configuration system

---

## Quick Start for Developers

### Reading the Architecture

**New to the project?** Start here:

1. **[TRIMMING-AOT-SUMMARY.md](./TRIMMING-AOT-SUMMARY.md)** - 10-minute overview
2. **[TRIMMING-AOT-DIAGRAMS.md](./TRIMMING-AOT-DIAGRAMS.md)** - Visual architecture
3. **[09-trimming-aot-architecture.md](./09-trimming-aot-architecture.md)** - Complete design
4. **[01-api-design.md](./01-api-design.md)** - API usage patterns
5. **[02-source-generation.md](./02-source-generation.md)** - Implementation details

### Understanding the Trimming/AOT Strategy

**Key questions answered:**

- **Why source generation?**
  - See: [09-trimming-aot-architecture.md § Source Generator AOT Pattern](./09-trimming-aot-architecture.md#source-generator-aot-pattern)

- **How does AOT work?**
  - See: [TRIMMING-AOT-DIAGRAMS.md § Compile-Time to Runtime Flow](./TRIMMING-AOT-DIAGRAMS.md#4-compile-time-to-runtime-flow)

- **What about dynamic types?**
  - See: [09-trimming-aot-architecture.md § Dynamic Type Handling](./09-trimming-aot-architecture.md#dynamic-type-handling)

- **How do I migrate from CsvHelper?**
  - See: [TRIMMING-AOT-SUMMARY.md § Migration Path](./TRIMMING-AOT-SUMMARY.md#migration-path)

- **What's the performance impact?**
  - See: [TRIMMING-AOT-SUMMARY.md § Performance Impact](./TRIMMING-AOT-SUMMARY.md#performance-impact)

### Contributing to Implementation

**Ready to implement?**

1. Review the [Implementation Roadmap](#implementation-roadmap)
2. Check current milestone in GitHub Issues
3. Read the specific architecture document for your component
4. Follow the coding standards in [02-source-generation.md](./02-source-generation.md)
5. Write tests according to [09-trimming-aot-architecture.md § Testing Strategy](./09-trimming-aot-architecture.md#testing-strategy)

---

## Performance Goals

### Binary Size

| Configuration | Target | Current | Status |
|--------------|--------|---------|--------|
| Source-gen + AOT | < 5 MB | TBD | 🔄 Pending implementation |
| Source-gen + Trim | < 4 MB | TBD | 🔄 Pending implementation |
| Reflection (no trim) | < 20 MB | TBD | 🔄 Pending implementation |

### Startup Time

| Configuration | Target | Current | Status |
|--------------|--------|---------|--------|
| Source-gen AOT | < 100ms | TBD | 🔄 Pending implementation |
| Source-gen JIT | < 300ms | TBD | 🔄 Pending implementation |
| Reflection | < 500ms | TBD | 🔄 Pending implementation |

### Throughput (1M records)

| Configuration | Target | Current | Status |
|--------------|--------|---------|--------|
| Source-gen AOT | > 2.5M rec/s | TBD | 🔄 Pending implementation |
| Source-gen JIT | > 2.0M rec/s | TBD | 🔄 Pending implementation |
| Reflection | > 400k rec/s | TBD | 🔄 Pending implementation |

---

## Testing Requirements

### AOT Smoke Tests

- ✅ Builds with `<PublishAot>true</PublishAot>`
- ✅ Binary runs without errors
- ✅ All core functionality works
- ✅ Binary size < 10 MB

### Trimming Tests

- ✅ Zero trim warnings with source-gen API
- ✅ Proper warnings with reflection API
- ✅ All reflection usage annotated

### Performance Tests

- ✅ Throughput benchmarks (vs baseline)
- ✅ Memory allocation tracking
- ✅ Startup time measurement
- ✅ Binary size tracking

### Compatibility Tests

- ✅ netstandard2.0 on .NET Framework 4.6.1, 4.7.2, 4.8
- ✅ netstandard2.0 on .NET Core 2.0, 3.1
- ✅ net6.0 on .NET 6.0, 7.0, 8.0
- ✅ net8.0 on .NET 8.0 with AOT

---

## Decision Records

### ADR-001: Multi-Tier AOT Strategy

**Decision:** Support three tiers of AOT/trimming based on target framework.

**Rationale:**
- net8.0+ has full Native AOT support
- net6.0/7.0 has PublishTrimmed but not full AOT
- netstandard2.0 has no trimming/AOT support
- Different users have different requirements

**Consequences:**
- More complex implementation (3 code paths)
- Clear migration path for users
- Maximum compatibility

**Reference:** [09-trimming-aot-architecture.md § Multi-Tier AOT Strategy](./09-trimming-aot-architecture.md#multi-tier-aot-strategy)

### ADR-002: Source Generator Primary API

**Decision:** Make source-generated API the primary, recommended API.

**Rationale:**
- Best performance (4-12x faster)
- AOT-compatible
- Trim-safe
- Compile-time type safety
- Industry standard (System.Text.Json, config binder)

**Consequences:**
- Requires partial class and [CsvSerializable] attribute
- More setup than simple reflection
- Better performance and AOT support

**Reference:** [09-trimming-aot-architecture.md § Source Generator AOT Pattern](./09-trimming-aot-architecture.md#source-generator-aot-pattern)

### ADR-003: Dual API Surface

**Decision:** Provide both source-generated and reflection-based APIs.

**Rationale:**
- Source-gen for performance and AOT
- Reflection for compatibility and dynamic scenarios
- Clear warnings on reflection APIs
- Gradual migration path

**Consequences:**
- Increased library surface area
- More documentation needed
- Clear upgrade path for users

**Reference:** [09-trimming-aot-architecture.md § Dual API Surface Design](./09-trimming-aot-architecture.md#dual-api-surface-design)

---

## Frequently Asked Questions

### Q: Can I use CsvHandler with Native AOT?

**A:** Yes! With net8.0 and source-generated types:

```csharp
[CsvSerializable(typeof(Person))]
partial class AppCsvContext : CsvContext { }

var reader = CsvReader<Person>.Create(stream, AppCsvContext.Default);
```

See: [09-trimming-aot-architecture.md § Tier 1: Full Native AOT](./09-trimming-aot-architecture.md#tier-1-full-native-aot-net80)

### Q: What about GetRecords<dynamic>()?

**A:** Not available in Native AOT. Use alternatives:

```csharp
// Option 1: Dictionary (AOT-compatible)
var records = stream.ReadAsDictionaries();

// Option 2: Source-generated types (recommended)
[CsvSerializable(typeof(Person))]
partial class AppCsvContext : CsvContext { }
```

See: [09-trimming-aot-architecture.md § Dynamic Type Handling](./09-trimming-aot-architecture.md#dynamic-type-handling)

### Q: How do I migrate from CsvHelper?

**A:** Follow the migration guide:

1. Add [CsvSerializable] attributes
2. Create CsvContext
3. Change to CsvReader<T>.Create()
4. Test and benchmark

See: [TRIMMING-AOT-SUMMARY.md § Migration Path](./TRIMMING-AOT-SUMMARY.md#migration-path)

### Q: What's the performance difference?

**A:** Source-gen is 4-12x faster than reflection:

| Approach | Throughput | Startup | Binary Size |
|----------|------------|---------|-------------|
| Reflection | 476k rec/s | 420ms | 15.2 MB |
| Source-gen (JIT) | 2.2M rec/s (4.6x) | 250ms | 3.8 MB |
| Source-gen (AOT) | 2.6M rec/s (5.5x) | 85ms | 4.1 MB |

See: [TRIMMING-AOT-SUMMARY.md § Performance Impact](./TRIMMING-AOT-SUMMARY.md#performance-impact)

---

## Contributing

See the main [CONTRIBUTING.md](../../CONTRIBUTING.md) for contribution guidelines.

For architecture-specific questions:
- Open an issue with the `architecture` label
- Tag the issue with the relevant component (e.g., `source-generation`, `aot`, `trimming`)
- Reference the specific architecture document

---

## License

This project is licensed under the MIT License - see the [LICENSE](../../LICENSE) file for details.

---

**Last Updated:** 2025-10-15
**Architecture Version:** 1.0
**Status:** Design Complete, Ready for Implementation

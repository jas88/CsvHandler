# CsvHandler Architecture Overview

## Executive Summary

CsvHandler is a modern .NET CSV library designed as a high-performance, type-safe alternative to CsvHelper. It leverages source generators, UTF-8 processing, and modern .NET primitives to achieve superior performance while maintaining API familiarity for CsvHelper users.

## Design Principles

1. **Performance First**: Direct UTF-8 processing, zero-allocation parsing, span-based APIs
2. **Type Safety**: Source generators eliminate runtime reflection
3. **Developer Experience**: Familiar API surface for easy migration from CsvHelper
4. **Correctness**: RFC 4180 compliant with configurable extensions
5. **Modern .NET**: Target .NET 6+ to leverage latest runtime improvements

## Key Architectural Decisions

### ADR-001: Source Generators for Type Safety
**Decision**: Use source generators to generate serialization/deserialization code at compile time.

**Rationale**:
- Eliminates runtime reflection overhead
- Enables compile-time validation of mappings
- Provides IDE IntelliSense support
- Matches approach of System.Text.Json.SourceGeneration

**Trade-offs**:
- Requires .NET 5+ and C# 9+
- Increased build time (minimal)
- Must provide reflection fallback for dynamic scenarios

### ADR-002: UTF-8 Native Processing
**Decision**: Work with UTF-8 bytes directly using `Span<byte>` and `ReadOnlySpan<byte>`.

**Rationale**:
- Avoids UTF-16 string allocation and conversion
- 40-60% memory reduction for ASCII/UTF-8 content
- Leverages high-performance UTF-8 parsing in .NET 6+
- Aligns with modern .NET APIs (System.Text.Json, Kestrel, etc.)

**Trade-offs**:
- API complexity slightly higher than string-based APIs
- Requires understanding of span-based programming
- Provide convenience overloads for string compatibility

### ADR-003: Dual API Surface
**Decision**: Provide both high-performance span-based APIs and convenient string-based APIs.

**Rationale**:
- Span APIs for performance-critical code
- String APIs for ease of use and migration
- String APIs delegate to span implementations

**Trade-offs**:
- Larger API surface to maintain
- Clear documentation needed to guide users

### ADR-004: Streaming-First Design
**Decision**: Design all APIs around streaming with `IAsyncEnumerable<T>` and `IBufferWriter<byte>`.

**Rationale**:
- Handle files larger than memory
- Natural async/await integration
- Composable with LINQ and async streams
- Backpressure handling built-in

**Trade-offs**:
- Requires async understanding
- Provide synchronous overloads for simple cases

### ADR-005: Configuration via Options Pattern
**Decision**: Use immutable options objects following .NET options pattern.

**Rationale**:
- Thread-safe by default
- Clear configuration scope
- Familiar pattern for .NET developers
- Easy to test and reason about

## System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        User Code                             │
└─────────────────────────────────────────────────────────────┘
                              │
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                      Public API Layer                        │
│  • CsvReader<T>         • CsvWriter<T>                      │
│  • CsvConfiguration     • Attributes                        │
└─────────────────────────────────────────────────────────────┘
                              │
                    ┌─────────┴─────────┐
                    ↓                   ↓
┌──────────────────────────┐  ┌─────────────────────────┐
│   Source Generated Code   │  │   Reflection Fallback   │
│  • Type-specific parsers  │  │  • Dynamic parsing      │
│  • Type-specific writers  │  │  • Expression trees     │
└──────────────────────────┘  └─────────────────────────┘
                    │                   │
                    └─────────┬─────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                    Core Processing Layer                     │
│  • Utf8CsvParser          • Utf8CsvWriter                   │
│  • Field tokenizer        • Field formatter                 │
│  • Quote/escape handling  • Buffer management               │
└─────────────────────────────────────────────────────────────┘
                              │
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                     Infrastructure Layer                     │
│  • Stream wrappers        • Buffer pools                    │
│  • Encoding detection     • Error handling                  │
└─────────────────────────────────────────────────────────────┘
```

## Project Structure

```
src/
├── CsvHandler/                          # Core library
│   ├── CsvReader.cs                     # Main reader API
│   ├── CsvWriter.cs                     # Main writer API
│   ├── CsvConfiguration.cs              # Configuration
│   ├── Attributes/                      # Mapping attributes
│   ├── Core/                            # UTF-8 processing
│   │   ├── Utf8CsvParser.cs
│   │   ├── Utf8CsvWriter.cs
│   │   └── CsvTokenizer.cs
│   ├── Converters/                      # Type converters
│   └── Reflection/                      # Fallback support
│
├── CsvHandler.SourceGeneration/         # Source generators
│   ├── CsvSerializerGenerator.cs
│   ├── Models/                          # Generator models
│   └── Templates/                       # Code templates
│
└── CsvHandler.Compatibility/            # CsvHelper compatibility
    ├── CsvHelperAdapter.cs
    └── Migration/

tests/
├── CsvHandler.Tests/
├── CsvHandler.SourceGeneration.Tests/
├── CsvHandler.Benchmarks/
└── CsvHandler.Compatibility.Tests/
```

## Technology Stack

- **Target Framework**: .NET 6.0, .NET 7.0, .NET 8.0+
- **Language**: C# 11+ (for required modifiers, generic math)
- **Build**: SDK-style projects, source generators
- **Testing**: xUnit, BenchmarkDotNet, Verify
- **CI/CD**: GitHub Actions

## Performance Targets

Based on CsvHelper as baseline (1.0x):

| Scenario | Target | Rationale |
|----------|--------|-----------|
| Read ASCII | 2.5x | Direct UTF-8, no string allocation |
| Read Unicode | 1.8x | UTF-8 native, reduced allocations |
| Write ASCII | 2.0x | UTF-8 direct, buffer pooling |
| Memory | 0.4x | Span-based, no intermediate strings |
| Startup | 1.0x | Source gen eliminates reflection delay |

## Compatibility Strategy

### Phase 1: Core API Parity
- Basic read/write operations
- Common configuration options
- Standard type conversions

### Phase 2: Advanced Features
- Custom converters
- Complex mapping scenarios
- Header handling variants

### Phase 3: Full Compatibility Layer
- CsvHelper namespace adapters
- Migration tool
- Side-by-side usage guide

## Next Steps

1. Review and approve architectural decisions
2. Create detailed API specifications
3. Implement proof-of-concept for core scenarios
4. Benchmark against CsvHelper
5. Iterate based on performance results

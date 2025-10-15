# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CsvHandler is a C# library/application for CSV file processing. The repository is currently empty and awaiting initial implementation.

## Development Commands

### Building
```bash
dotnet build
```

### Running Tests
```bash
dotnet test                           # Run all tests
dotnet test --filter "FullyQualifiedName~TestClassName"  # Run specific test class
dotnet test --logger "console;verbosity=detailed"        # Detailed test output
```

### Running the Application
```bash
dotnet run --project <ProjectName>
```

## Architecture Guidelines

### Expected Structure
- **Core Library**: CSV parsing, serialization, and validation logic
- **Tests**: Unit tests for CSV handling functionality
- **Performance**: Consider streaming for large CSV files
- **Encoding**: UTF-8 by default, with support for other encodings
- **Error Handling**: Graceful handling of malformed CSV data

### CSV Handling Considerations
- Quote escaping and embedded delimiters
- Header row detection and parsing
- Column type inference
- Memory-efficient processing for large files
- RFC 4180 compliance for CSV format

### Code Organization
- Separate parsing logic from business logic
- Use interfaces for testability
- Consider async/await for I/O operations
- Implement IDisposable for file handles

## File Organization
- `/src` - Source code files
- `/tests` - Unit and integration tests
- `/docs` - Documentation files
- `/benchmarks` - Performance benchmarks (if needed)

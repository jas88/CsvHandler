# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- Updated SDK to .NET 10.0.100
- Updated library target frameworks to netstandard2.0, net8.0, net9.0, net10.0 (removed EOL net6.0)
- Updated test and application projects to target net10.0 only
- Removed Microsoft.SourceLink.GitHub package (functionality now integrated in .NET SDK 8+)
- Migrated to MinVer for automated version generation
- Implemented central package management via Directory.Packages.props
- Updated CI workflow to use setup-dotnet's built-in caching with Directory.Packages.props

### Added
- MinVer configuration for semantic versioning from git tags
- Directory.Packages.props for central package version management

### Removed
- Manual version properties from Directory.Build.props (now managed by MinVer)
- Explicit package versions from .csproj files (now centrally managed)
- net6.0 target framework (reached end of life November 12, 2024)

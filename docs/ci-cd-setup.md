# CI/CD Pipeline Setup Guide

This document describes the GitHub Actions CI/CD pipeline configuration for CsvHandler.

## Overview

The CI/CD pipeline provides:
- Multi-OS testing (Ubuntu, Windows, macOS)
- Multi-framework testing (.NET 6.0 and 8.0)
- Code coverage reporting via Codecov
- Automated NuGet package publishing
- AOT compilation verification
- Code quality checks

## Workflow Files

### Main CI/CD Workflow (.github/workflows/ci.yml)

The main workflow consists of three jobs:

#### 1. build-and-test

Runs on every push and pull request to main/develop branches.

**Matrix Strategy:**
- OS: ubuntu-latest, windows-latest, macos-latest
- .NET Versions: 6.0.x, 8.0.x

**Steps:**
1. Checkout code with submodules
2. Setup .NET SDK (both specified version and global.json)
3. Restore dependencies
4. Build in Release configuration
5. Run tests
6. Collect code coverage (Ubuntu + .NET 8.0 only)
7. Upload coverage to Codecov
8. Test AOT compilation (Ubuntu only)
9. Pack NuGet packages (Ubuntu + .NET 8.0 only)
10. Upload artifacts

**Coverage Collection:**
- Uses XPlat Code Coverage collector
- Generates multiple formats: OpenCover, Cobertura, JSON, LCOV
- Only runs on Ubuntu with .NET 8.0 to avoid redundant coverage data

#### 2. code-quality

Runs after build-and-test job succeeds.

**Checks:**
- Code formatting verification (`dotnet format --verify-no-changes`)
- Code analyzer rules enforcement
- Build with TreatWarningsAsErrors=true

#### 3. publish-nuget

Runs only on GitHub releases.

**Requirements:**
- All tests must pass
- Code quality checks must pass
- Release must be published

**Steps:**
1. Download NuGet packages from artifacts
2. Publish packages to NuGet.org
3. Attach packages to GitHub release

## Codecov Configuration

### Setup Steps

1. **Create Codecov Account:**
   - Go to https://codecov.io
   - Sign in with GitHub
   - Add the CsvHandler repository

2. **Get Upload Token:**
   - Navigate to repository settings in Codecov
   - Copy the upload token

3. **Add GitHub Secret:**
   - Go to GitHub repository settings
   - Navigate to Secrets and variables > Actions
   - Create new secret: `CODECOV_TOKEN`
   - Paste the Codecov upload token

4. **Update README Badge:**
   - Replace `YOUR_CODECOV_TOKEN` in README.md with your Codecov token
   - The token in the badge URL is for viewing only, not uploading

### codecov.yml Configuration

The `codecov.yml` file in the repository root configures:

**Coverage Targets:**
- Project coverage target: 80%
- Patch coverage target: 80%
- Acceptable threshold: 5% for project, 10% for patch

**Ignored Paths:**
- Test projects
- Generated code files
- Example projects
- Documentation

**Coverage Components:**
- CsvHandler Core (src/CsvHandler)
- Source Generator (src/CsvHandler.SourceGenerator)

## NuGet Publishing Setup

### Prerequisites

1. **Create NuGet Account:**
   - Sign up at https://www.nuget.org
   - Verify your email

2. **Generate API Key:**
   - Navigate to account settings
   - Select API Keys
   - Create new key with push permissions
   - Scope: Push new packages and package versions
   - Glob pattern: CsvHandler*

3. **Add GitHub Secret:**
   - Go to GitHub repository settings
   - Navigate to Secrets and variables > Actions
   - Create new secret: `NUGET_API_KEY`
   - Paste your NuGet API key

### Publishing Process

**Automatic Publishing:**
1. Create a new release on GitHub
2. Tag the release (e.g., v0.1.0)
3. Publish the release
4. CI/CD pipeline automatically builds and publishes to NuGet.org

**Package Metadata:**
All package metadata is defined in `Directory.Build.props`:
- Version numbers
- Author information
- License (MIT)
- Project URLs
- Package tags
- Icon and README

### Release Checklist

Before creating a release:

- [ ] Update version in `Directory.Build.props`
- [ ] Update CHANGELOG.md
- [ ] Ensure all tests pass
- [ ] Verify code coverage meets threshold
- [ ] Review package metadata
- [ ] Create Git tag matching version
- [ ] Create GitHub release
- [ ] Monitor CI/CD pipeline
- [ ] Verify package on NuGet.org

## Badge Configuration

Update the badges in README.md:

```markdown
[![CI/CD](https://github.com/jas88/CsvHandler/workflows/CI%2FCD/badge.svg)](https://github.com/jas88/CsvHandler/actions)
[![codecov](https://codecov.io/gh/jas88/CsvHandler/branch/main/graph/badge.svg?token=YOUR_TOKEN)](https://codecov.io/gh/jas88/CsvHandler)
[![NuGet Version](https://img.shields.io/nuget/v/CsvHandler.svg)](https://www.nuget.org/packages/CsvHandler/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/CsvHandler.svg)](https://www.nuget.org/packages/CsvHandler/)
[![.NET Version](https://img.shields.io/badge/.NET-6.0%20%7C%208.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
```

Replace:
- `jas88` with your GitHub username
- `YOUR_TOKEN` with your Codecov repository token (viewing token, not upload token)
- `CsvHandler` with your package name if different

## Monitoring

### GitHub Actions

Monitor builds at:
- https://github.com/jas88/CsvHandler/actions

View:
- Build status
- Test results
- Coverage trends
- Deployment history

### Codecov Dashboard

View coverage reports at:
- https://codecov.io/gh/jas88/CsvHandler

Features:
- Line-by-line coverage
- File coverage percentages
- Coverage trends over time
- Pull request coverage diffs
- Component-based coverage

### NuGet.org

Monitor package at:
- https://www.nuget.org/packages/CsvHandler

View:
- Download statistics
- Version history
- Package dependencies
- Framework compatibility

## Troubleshooting

### Coverage Upload Fails

**Issue:** Codecov upload fails with authentication error

**Solution:**
1. Verify CODECOV_TOKEN secret is set correctly
2. Check token hasn't expired
3. Ensure token has upload permissions
4. Try regenerating token in Codecov dashboard

### NuGet Push Fails

**Issue:** Package push to NuGet.org fails

**Solutions:**
1. Verify NUGET_API_KEY secret is correct
2. Check API key hasn't expired
3. Ensure package version doesn't already exist
4. Verify API key has push permissions for the package ID

### Build Fails on Specific OS

**Issue:** Tests pass on Ubuntu but fail on Windows/macOS

**Solutions:**
1. Check for path separator issues (`/` vs `\`)
2. Review line ending differences (CRLF vs LF)
3. Check for case-sensitive file system assumptions
4. Review platform-specific behavior in tests

### AOT Compilation Fails

**Issue:** AOT compilation test fails

**Solutions:**
1. Review AOT compatibility warnings
2. Check for reflection usage
3. Verify trim annotations are correct
4. Review source generator output

## Local Testing

Test the CI/CD pipeline locally before pushing:

### Build and Test
```bash
# Restore dependencies
dotnet restore

# Build
dotnet build --configuration Release

# Run tests
dotnet test --configuration Release --no-build
```

### Coverage Report
```bash
# Run tests with coverage
dotnet test --configuration Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage

# View coverage report (requires reportgenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator \
  -reports:"./coverage/**/coverage.cobertura.xml" \
  -targetdir:"./coverage/report" \
  -reporttypes:Html

# Open report
open ./coverage/report/index.html  # macOS
start ./coverage/report/index.html # Windows
xdg-open ./coverage/report/index.html # Linux
```

### Package Creation
```bash
# Pack NuGet packages
dotnet pack --configuration Release --output ./artifacts

# Inspect package contents
dotnet tool install -g dotnet-validate
dotnet validate package local ./artifacts/CsvHandler.0.1.0-alpha.nupkg
```

### AOT Compilation
```bash
# Test AOT compilation
dotnet publish tests/CsvHandler.AotTests/CsvHandler.AotTests.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained
```

## Performance Considerations

### Build Time Optimization

The pipeline uses several strategies to minimize build time:

1. **Parallel Jobs:** Different OS and .NET version combinations run in parallel
2. **Caching:** NuGet packages are cached by actions/setup-dotnet
3. **Conditional Execution:** Coverage and packaging only on specific matrix combinations
4. **Artifact Reuse:** Build artifacts are reused across jobs

### Coverage Collection

Coverage is only collected once (Ubuntu + .NET 8.0) because:
- Coverage data is platform-independent for managed code
- Reduces build time significantly
- Avoids redundant coverage uploads

## Security Best Practices

1. **Secrets Management:**
   - Never commit API keys or tokens
   - Use GitHub encrypted secrets
   - Rotate tokens regularly
   - Use minimal permission scopes

2. **Code Signing:**
   - Consider signing NuGet packages
   - Use Azure Key Vault for private keys
   - Document signing procedures

3. **Dependency Scanning:**
   - Consider adding dependency vulnerability scanning
   - Review security advisories regularly
   - Keep dependencies updated

## Future Enhancements

Potential additions to the pipeline:

- [ ] Automated dependency updates (Dependabot)
- [ ] Security scanning (CodeQL)
- [ ] Performance benchmarking
- [ ] API documentation generation
- [ ] Docker image publishing
- [ ] Pre-release package publishing
- [ ] Integration testing with external services
- [ ] Artifact signing
- [ ] Release notes automation

## Support

For issues with the CI/CD pipeline:
1. Check GitHub Actions logs
2. Review Codecov error messages
3. Verify secret configurations
4. Open issue on GitHub with relevant logs

## References

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Codecov Documentation](https://docs.codecov.com/)
- [NuGet Documentation](https://docs.microsoft.com/en-us/nuget/)
- [.NET SDK Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)

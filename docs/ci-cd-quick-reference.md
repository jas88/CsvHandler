# CI/CD Pipeline Quick Reference

## Pipeline Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    CI/CD Pipeline                            │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  Triggers:                                                    │
│    • Push to main/develop                                    │
│    • Pull request to main/develop                           │
│    • Release published                                       │
│    • Manual workflow dispatch                                │
│                                                               │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  Job 1: Build and Test (Matrix Strategy)                    │
│  ┌──────────────────────────────────────────────────┐       │
│  │  OS: ubuntu, windows, macos                       │       │
│  │  .NET: 6.0.x, 8.0.x                              │       │
│  │  Total: 6 parallel jobs                          │       │
│  └──────────────────────────────────────────────────┘       │
│       │                                                       │
│       ├─► Checkout code (with submodules)                   │
│       ├─► Setup .NET SDK                                     │
│       ├─► Restore dependencies                              │
│       ├─► Build (Release)                                    │
│       ├─► Run tests                                          │
│       ├─► Collect coverage (Ubuntu + .NET 8.0 only)        │
│       ├─► Upload to Codecov                                 │
│       ├─► Test AOT compilation (Ubuntu only)                │
│       ├─► Pack NuGet packages (Ubuntu + .NET 8.0 only)     │
│       └─► Upload artifacts                                   │
│                                                               │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  Job 2: Code Quality (Runs after Job 1)                     │
│       │                                                       │
│       ├─► Check code formatting                             │
│       └─► Run analyzers (enforce warnings as errors)        │
│                                                               │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  Job 3: Publish to NuGet (Release only)                     │
│       │                                                       │
│       ├─► Download artifacts from Job 1                     │
│       ├─► Push to NuGet.org                                 │
│       └─► Attach packages to GitHub release                 │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

## Build Matrix

| OS            | .NET 6.0 | .NET 8.0 | Coverage | AOT Test | Pack |
|---------------|----------|----------|----------|----------|------|
| ubuntu-latest | ✅       | ✅       | ✅*      | ✅*      | ✅*  |
| windows-latest| ✅       | ✅       | -        | -        | -    |
| macos-latest  | ✅       | ✅       | -        | -        | -    |

*Only on .NET 8.0

## Trigger Conditions

### Push/Pull Request
- Runs: `build-and-test` + `code-quality`
- Artifacts: Test results, coverage reports
- Duration: ~5-10 minutes

### Release Published
- Runs: `build-and-test` + `code-quality` + `publish-nuget`
- Artifacts: NuGet packages on NuGet.org
- Duration: ~10-15 minutes (including approval if environment configured)

### Manual Dispatch
- Runs: `build-and-test` + `code-quality`
- Use case: Testing changes without committing

## Required Secrets

| Secret Name      | Purpose                    | Where to Get                          |
|------------------|----------------------------|---------------------------------------|
| CODECOV_TOKEN    | Upload coverage reports    | https://codecov.io (repository setup) |
| NUGET_API_KEY    | Publish packages to NuGet  | https://www.nuget.org (API keys)     |

## Environment Variables

| Variable                         | Value | Purpose                    |
|----------------------------------|-------|----------------------------|
| DOTNET_SKIP_FIRST_TIME_EXPERIENCE| true  | Faster SDK initialization  |
| DOTNET_CLI_TELEMETRY_OPTOUT      | true  | Disable telemetry          |
| DOTNET_NOLOGO                    | true  | Hide .NET logo in output   |
| CI                               | true  | Enable CI-specific behavior|

## Coverage Configuration

### Coverage Targets (codecov.yml)

| Metric        | Target | Threshold | Status |
|---------------|--------|-----------|--------|
| Project       | 80%    | ±5%       | ❌ Fail|
| Patch (new)   | 80%    | ±10%      | ❌ Fail|
| Changes       | -      | -         | ℹ️ Info|

### Ignored Paths

- `tests/**/*` - Test projects
- `examples/**/*` - Example code
- `docs/**/*` - Documentation
- `**/*.Designer.cs` - Generated designer files
- `**/*.g.cs` - Generated source files

## Artifacts

### Build Artifacts

| Artifact Name           | Contents                    | Retention | When Generated      |
|-------------------------|-----------------------------|-----------|---------------------|
| nuget-packages          | *.nupkg, *.snupkg          | 7 days    | Every build (Ubuntu)|
| test-results-{os}-{tfm} | *.trx (test results)       | 90 days   | Every build         |

## Common Commands

### Local Testing

```bash
# Run full test suite
dotnet test --configuration Release

# Run with coverage
dotnet test --configuration Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage

# Build Release configuration
dotnet build --configuration Release

# Check code formatting
dotnet format --verify-no-changes

# Pack NuGet packages
dotnet pack --configuration Release --output ./artifacts

# Test AOT compilation
dotnet publish tests/CsvHandler.AotTests/CsvHandler.AotTests.csproj -c Release
```

### Workflow Management

```bash
# Trigger manual workflow run
gh workflow run ci.yml

# List workflow runs
gh run list --workflow=ci.yml

# View workflow run details
gh run view <run-id>

# Watch workflow run
gh run watch <run-id>

# Download artifacts
gh run download <run-id>
```

## Release Process

### 1. Prepare Release

```bash
# Update version in Directory.Build.props
<VersionPrefix>0.1.0</VersionPrefix>
<VersionSuffix>alpha</VersionSuffix>  # Remove for stable release

# Update CHANGELOG.md
# Commit changes
git add Directory.Build.props CHANGELOG.md
git commit -m "Bump version to 0.1.0-alpha"
git push origin main
```

### 2. Create Release

```bash
# Create and push tag
git tag -a v0.1.0-alpha -m "Release v0.1.0-alpha"
git push origin v0.1.0-alpha

# Or use GitHub CLI
gh release create v0.1.0-alpha \
  --title "v0.1.0-alpha - Initial Alpha Release" \
  --notes "Release notes here..." \
  --prerelease
```

### 3. Monitor Pipeline

```bash
# Watch the release workflow
gh run watch

# Check NuGet.org after completion
open https://www.nuget.org/packages/CsvHandler
```

## Troubleshooting

### Build Fails on Specific OS

**Check:**
- File path separators (`/` vs `\`)
- Line endings (LF vs CRLF)
- Case sensitivity in file names

**Debug:**
```yaml
- name: Debug Info
  run: |
    dotnet --info
    dotnet --list-sdks
    ls -la
```

### Coverage Upload Fails

**Check:**
- CODECOV_TOKEN is set
- Coverage files exist: `find ./coverage -name "*.xml"`
- Repository is public or token is valid

**Debug:**
```yaml
- name: List Coverage Files
  run: find ./coverage -type f
```

### NuGet Push Fails

**Check:**
- NUGET_API_KEY is set and valid
- Package version doesn't already exist
- API key has push permissions
- Glob pattern matches package ID

**Retry:**
```bash
# Manual push for debugging
dotnet nuget push ./artifacts/CsvHandler.0.1.0.nupkg \
  --api-key $NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate \
  --verbosity detailed
```

## Badge URLs

### CI/CD Status
```markdown
[![CI/CD](https://github.com/jas88/CsvHandler/workflows/CI%2FCD/badge.svg)](https://github.com/jas88/CsvHandler/actions)
```

### Code Coverage
```markdown
[![codecov](https://codecov.io/gh/jas88/CsvHandler/branch/main/graph/badge.svg?token=TOKEN)](https://codecov.io/gh/jas88/CsvHandler)
```

### NuGet Package
```markdown
[![NuGet Version](https://img.shields.io/nuget/v/CsvHandler.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/CsvHandler/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/CsvHandler.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/CsvHandler/)
```

### .NET Version
```markdown
[![.NET](https://img.shields.io/badge/.NET-6.0%20%7C%208.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
```

## Performance Optimization

### Build Time
- **Parallel jobs:** 6 concurrent jobs (3 OS × 2 .NET versions)
- **Caching:** NuGet packages cached by actions/setup-dotnet
- **Conditional steps:** Coverage and packaging only on Ubuntu + .NET 8.0
- **Typical duration:** 5-10 minutes for PR, 10-15 minutes for release

### Coverage Collection
- **Single collection:** Only Ubuntu + .NET 8.0 (coverage is platform-independent)
- **Multiple formats:** OpenCover, Cobertura, JSON, LCOV (for compatibility)
- **Parallel upload:** Doesn't block other matrix jobs

## Monitoring

### GitHub Actions
- **URL:** https://github.com/jas88/CsvHandler/actions
- **Checks:** Build status, test results, deployment history

### Codecov
- **URL:** https://codecov.io/gh/jas88/CsvHandler
- **Checks:** Coverage trends, file coverage, PR diffs

### NuGet.org
- **URL:** https://www.nuget.org/packages/CsvHandler
- **Checks:** Downloads, versions, dependencies

## Security

### Best Practices
- ✅ Secrets stored in GitHub encrypted secrets
- ✅ API keys with minimal permissions
- ✅ Production environment protection
- ✅ No secrets in logs or artifacts
- ✅ Token rotation reminders

### Audit
```bash
# Check secret usage in workflows
grep -r "secrets\." .github/workflows/

# Review API key permissions
# Visit: https://www.nuget.org/account/apikeys
```

## Resources

- **Documentation:** `/docs/ci-cd-setup.md`
- **Secret Setup:** `/docs/setup-secrets.md`
- **GitHub Actions:** https://docs.github.com/en/actions
- **Codecov:** https://docs.codecov.com
- **NuGet:** https://docs.microsoft.com/en-us/nuget/

## Quick Links

| Resource              | URL                                                      |
|-----------------------|----------------------------------------------------------|
| Workflow File         | `.github/workflows/ci.yml`                              |
| Codecov Config        | `codecov.yml`                                           |
| Actions Dashboard     | https://github.com/jas88/CsvHandler/actions             |
| Codecov Dashboard     | https://codecov.io/gh/jas88/CsvHandler                  |
| NuGet Package         | https://www.nuget.org/packages/CsvHandler               |
| Repository Settings   | https://github.com/jas88/CsvHandler/settings            |

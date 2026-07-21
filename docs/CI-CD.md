# CI/CD Pipeline Documentation

## Overview

The AutoMapper Roslyn Analyzer project uses GitHub Actions for continuous integration and deployment. The pipeline ensures code quality, security, and automated releases.

## 🔄 Workflows

### 1. Main CI/CD Pipeline (`.github/workflows/ci.yml`)

**Triggers:**

- Push to `main` and `develop` branches
- Pull requests to `main` and `develop` branches

**Jobs:**

#### Build and Test

- Sets up .NET 10.0 environment
- Restores NuGet packages with caching
- Builds analyzer and test projects in Release configuration with `-warnaserror`
- Runs unit tests with code coverage collection
- Runs generated trust artifact checks for `docs/RULE_CATALOG.md` and the sample diagnostics snapshot
- Uploads coverage reports to Codecov
- Builds samples, where analyzer warnings are expected demonstration output

#### Package Analyzer

- Runs on pull requests and pushes
- Creates NuGet packages for analyzer and code fixes
- Verifies package contents include the analyzer DLL, README, and icon
- Uploads packages as build artifacts on pushes to `main`

#### Package Compatibility Contract

- Reusable workflow: `.github/workflows/package-compatibility.yml` (also called from the release workflow before publish)
- Reads the verified matrix from `tools/package-compatibility.json` (currently `net48`/AutoMapper 10.1.1, `net6.0`/12.0.1, and `net8.0`/`net9.0`/`net10.0` with AutoMapper 14.0.0)
- Downloads the exact packed `.nupkg` artifact and verifies its SHA-256 against the value recorded at pack time
- Builds healthy and intentionally broken consumer projects for each matrix case against the packed analyzer
- Asserts the broken consumer fails specifically with `AM001` and the healthy consumer builds clean, proving the packaged analyzer loads and behaves correctly on every supported target

### 2. CodeQL Security Analysis (`.github/workflows/codeql.yml`)

**Triggers:**

- Push to `main` and `develop` branches
- Pull requests to `main` branch
- Weekly schedule (Mondays at 2:30 AM UTC)

**Features:**

- Advanced security scanning for C# code
- Detects potential security vulnerabilities
- Integrates with GitHub Security tab

### 3. Release to NuGet (`.github/workflows/release.yml`)

**Triggers:**

- Semantic version tags such as `v2.30.23`

**Features:**

- Builds and tests with .NET 10.0.
- Packs with the version extracted from the tag.
- Runs the package compatibility contract (same reusable workflow as CI) against the exact packed bytes before publish.
- Publishes to NuGet and creates a GitHub release.

### 4. Dependency Updates (`.github/dependabot.yml`)

**Automated Updates:**

- NuGet packages (weekly on Mondays)
- GitHub Actions (weekly on Mondays)
- Creates pull requests for updates
- Assigns to georgepwall1991

## 📊 Quality Gates

### Code Coverage

- Target: 80% project coverage and 75% patch coverage
- Tool: Coverlet with XPlat Code Coverage
- Reporting: Codecov integration
- Configuration: `coverlet.runsettings`

### Security Scanning

- **CodeQL**: Static analysis for C# security issues
- **Dependabot**: Automated dependency updates

### Code Quality

- **PR review**: Exact-head GitHub Codex review is required before merge; the repository does not run an automatic Claude review workflow
- **Roslyn Analyzers**: Static code analysis
- **Rule catalog tests**: Descriptor, docs, sample, package, and workflow drift detection
- **Generated trust artifacts**: CI fails if `docs/RULE_CATALOG.md`, `tests/AutoMapperAnalyzer.Tests/Snapshots/sample-diagnostics.json`, or the compatibility documentation drift from implementation
- **Package compatibility contract**: CI (and the release workflow before publish) proves the exact packed analyzer loads and diagnoses correctly in `net48`, `net6.0`, `net8.0`, `net9.0`, and `net10.0` consumer projects per `tools/package-compatibility.json`
- **Warnings as errors**: CI builds fail on unexpected warnings outside the managed test warning baseline in `docs/WARNING_BASELINE.md`

## 🚀 Release Process

### Manual Release

1. Ensure all tests pass
2. Update version numbers and release notes
3. Tag the release with `vMajor.Minor.Patch`
4. Push the tag
5. The release pipeline automatically:
   - Creates NuGet packages
   - Publishes to NuGet.org
   - Creates GitHub release

### Package Versioning

- **Format**: Major.Minor.Patch (SemVer)
- **Current**: 2.30.84
- **Pre-release**: 2.30.84-preview, 2.30.84-beta

## 🔧 Configuration

### Required Secrets

Configure these in GitHub repository settings:

| Secret | Description | Required For |
|--------|-------------|--------------|
| `NUGET_API_KEY` | NuGet.org API key for publishing | Release pipeline |
| `CODECOV_TOKEN` | Codecov upload token | Coverage reporting |

### Environment Variables

| Variable | Value | Description |
|----------|-------|-------------|
| `DOTNET_VERSION` | '10.0.x' | .NET SDK version |
| `SOLUTION_FILE` | 'automapper-analyser.sln' | Solution file path |

`global.json` pins the local SDK feature band to `10.0.200` with `latestFeature` roll-forward so developer machines can
use patched 10.0 SDKs such as `10.0.203` without invalid SDK-version warnings.

## 📝 Pipeline Files

```
.github/
├── workflows/
│   ├── ci.yml              # Main CI/CD pipeline
│   ├── release.yml         # NuGet release pipeline
│   └── codeql.yml          # Security analysis
├── dependabot.yml          # Dependency updates
└── CODEOWNERS              # Code review assignments

docs/
├── CI-CD.md               # This documentation
├── RULE_CATALOG.md        # Generated rule/fixer trust catalog
└── WARNING_BASELINE.md    # Managed warning suppressions

coverlet.runsettings       # Code coverage configuration
```

## 🔍 Monitoring

### Build Status

- Check GitHub Actions tab for build status
- Monitor test results and coverage reports
- Review security scan results

### Quality Metrics

- **Codecov**: Coverage trends and reports
- **GitHub Security**: Vulnerability alerts

### Performance Monitoring

- Build time optimization
- Test execution time tracking
- Package size monitoring

## 🛠️ Local Development

### Running CI Steps Locally

```bash
# Restore and build
dotnet restore
dotnet build --configuration Release

# Run tests with coverage
dotnet test --configuration Release --collect:"XPlat Code Coverage"

# Verify generated trust artifacts
dotnet run --project tools/AnalyzerVerifier -- --check-catalog --check-snapshots --check-compatibility

# Update generated trust artifacts after intentional rule/sample changes
dotnet run --project tools/AnalyzerVerifier -- --update-catalog --update-snapshots

# Pack for release
dotnet pack --configuration Release --output ./packages

# Verify the packed analyzer against one compatibility case (matrix in tools/package-compatibility.json)
dotnet run --project tools/AnalyzerVerifier -- --verify-package-compatibility ./packages/AutoMapperAnalyzer.Analyzers.2.30.84.nupkg --case net10-am14

# Run samples
dotnet run --project samples/AutoMapperAnalyzer.Samples
```

### Testing Pipeline Changes

1. Create feature branch
2. Modify workflow files
3. Open pull request
4. Verify pipeline runs correctly
5. Merge after approval

## 📚 Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [.NET Build Tasks](https://docs.microsoft.com/en-us/dotnet/core/tools/)
- [Codecov Documentation](https://docs.codecov.io/)
- [Dependabot Configuration](https://docs.github.com/en/code-security/supply-chain-security/keeping-your-dependencies-updated-automatically)

---

*Last Updated: April 26, 2026*
*Pipeline Version: 1.1*

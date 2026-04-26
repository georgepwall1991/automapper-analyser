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
- Uploads coverage reports to Codecov
- Builds samples, where analyzer warnings are expected demonstration output

#### Package Analyzer

- Runs only on pushes to `main` branch
- Creates NuGet packages for analyzer and code fixes
- Verifies package contents include the analyzer DLL, README, and icon
- Uploads packages as build artifacts

### 2. Simple Build (`.github/workflows/simple-build.yml`)

- Mirrors the main build/test path on .NET 10.0.
- Treats unexpected build warnings as errors.

### 3. CodeQL Security Analysis (`.github/workflows/codeql.yml`)

**Triggers:**

- Push to `main` and `develop` branches
- Pull requests to `main` branch
- Weekly schedule (Mondays at 2:30 AM UTC)

**Features:**

- Advanced security scanning for C# code
- Detects potential security vulnerabilities
- Integrates with GitHub Security tab

### 4. Release to NuGet (`.github/workflows/release.yml`)

**Triggers:**

- Semantic version tags such as `v2.30.5`

**Features:**

- Builds and tests with .NET 10.0.
- Packs with the version extracted from the tag.
- Publishes to NuGet and creates a GitHub release.

### 5. Dependency Updates (`.github/dependabot.yml`)

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

- **Roslyn Analyzers**: Static code analysis
- **Rule catalog tests**: Descriptor, docs, sample, package, and workflow drift detection
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
- **Current**: 2.30.5
- **Pre-release**: 2.30.5-preview, 2.30.5-beta

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
│   ├── simple-build.yml    # Secondary build validation
│   ├── release.yml         # NuGet release pipeline
│   └── codeql.yml          # Security analysis
├── dependabot.yml          # Dependency updates
└── CODEOWNERS              # Code review assignments

docs/
├── CI-CD.md               # This documentation
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

# Pack for release
dotnet pack --configuration Release --output ./packages

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

*Last Updated: November 19, 2025*  
*Pipeline Version: 1.0*

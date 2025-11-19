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

- Sets up .NET 9.0 environment
- Restores NuGet packages with caching
- Builds solution in Release configuration
- Runs unit tests with code coverage collection
- Uploads coverage reports to Codecov

#### Package Analyzer

- Runs only on pushes to `main` branch
- Creates NuGet packages for analyzer and code fixes
- Uploads packages as build artifacts

#### Validate Samples

- Builds and runs sample scenarios
- Ensures examples work correctly
- Validates analyzer behavior with real code

#### Security Scan

- Runs Trivy vulnerability scanner
- Uploads results to GitHub Security tab
- Scans for security vulnerabilities in dependencies

#### Code Quality Analysis

- Integrates with SonarCloud for code quality metrics
- Analyzes code for bugs, vulnerabilities, and code smells
- Tracks technical debt and maintainability

#### Release

- Triggers only with `[release]` in commit message
- Publishes packages to NuGet.org
- Creates GitHub releases with artifacts

### 2. CodeQL Security Analysis (`.github/workflows/codeql.yml`)

**Triggers:**

- Push to `main` and `develop` branches
- Pull requests to `main` branch
- Weekly schedule (Mondays at 2:30 AM UTC)

**Features:**

- Advanced security scanning for C# code
- Detects potential security vulnerabilities
- Integrates with GitHub Security tab

### 3. Dependency Updates (`.github/dependabot.yml`)

**Automated Updates:**

- NuGet packages (weekly on Mondays)
- GitHub Actions (weekly on Mondays)
- Creates pull requests for updates
- Assigns to georgepwall1991

## 📊 Quality Gates

### Code Coverage

- Target: >90% code coverage
- Tool: Coverlet with XPlat Code Coverage
- Reporting: Codecov integration
- Configuration: `coverlet.runsettings`

### Security Scanning

- **Trivy**: Vulnerability scanning for dependencies
- **CodeQL**: Static analysis for C# security issues
- **Dependabot**: Automated dependency updates

### Code Quality

- **SonarCloud**: Code quality and maintainability
- **Roslyn Analyzers**: Static code analysis
- **EditorConfig**: Consistent code formatting

## 🚀 Release Process

### Manual Release

1. Ensure all tests pass
2. Update version numbers in project files
3. Commit with `[release]` in message
4. Push to `main` branch
5. Pipeline automatically:
   - Creates NuGet packages
   - Publishes to NuGet.org
   - Creates GitHub release

### Package Versioning

- **Format**: Major.Minor.Patch (SemVer)
- **Current**: 2.4.1
- **Pre-release**: 2.4.1-preview, 2.4.1-beta

## 🔧 Configuration

### Required Secrets

Configure these in GitHub repository settings:

| Secret | Description | Required For |
|--------|-------------|--------------|
| `NUGET_API_KEY` | NuGet.org API key for publishing | Release pipeline |
| `SONAR_TOKEN` | SonarCloud authentication token | Code quality analysis |
| `CODECOV_TOKEN` | Codecov upload token | Coverage reporting |

### Environment Variables

| Variable | Value | Description |
|----------|-------|-------------|
| `DOTNET_VERSION` | '9.0.x' | .NET SDK version |
| `SOLUTION_FILE` | 'automapper-analyser.sln' | Solution file path |

## 📝 Pipeline Files

```
.github/
├── workflows/
│   ├── ci.yml              # Main CI/CD pipeline
│   └── codeql.yml          # Security analysis
├── dependabot.yml          # Dependency updates
└── CODEOWNERS              # Code review assignments

docs/
├── CI-CD.md               # This documentation
└── CONTRIBUTING.md        # Contribution guidelines

coverlet.runsettings       # Code coverage configuration
```

## 🔍 Monitoring

### Build Status

- Check GitHub Actions tab for build status
- Monitor test results and coverage reports
- Review security scan results

### Quality Metrics

- **SonarCloud**: Code quality dashboard
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
- [SonarCloud Integration](https://sonarcloud.io/documentation/)
- [Codecov Documentation](https://docs.codecov.io/)
- [Dependabot Configuration](https://docs.github.com/en/code-security/supply-chain-security/keeping-your-dependencies-updated-automatically)

---

*Last Updated: November 19, 2025*  
*Pipeline Version: 1.0*

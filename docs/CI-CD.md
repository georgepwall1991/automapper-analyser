# CI/CD Pipeline Documentation

## Overview

The AutoMapper Roslyn Analyzer project uses GitHub Actions for continuous integration and deployment. The pipeline ensures code quality, security, and automated releases.

## 🔄 Workflows

### 1. Main CI/CD Pipeline (`.github/workflows/ci.yml` + `.github/workflows/ci-pr.yml`)

**Triggers:**

- `ci.yml`: push to `main` and `develop` branches (includes the package compatibility contract)
- `ci-pr.yml`: pull requests to `main` and `develop` branches (same Build and Test + Package Analyzer jobs, without the compatibility contract)

The split exists because GitHub cannot resolve local reusable workflows (`uses: ./.github/workflows/...`) from `pull_request` merge refs — including the compatibility job in the PR workflow makes every PR run die with `startup_failure`. Pushes to `main` still run the full contract, and `release.yml` gates every publish on it as well.

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

- Runs on pushes to `main`/`develop` only (not on pull requests — see the trigger note above)
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
- **Current**: 2.30.96
- **Pre-release**: 2.30.96-preview, 2.30.96-beta

### Trusted Publishing (not enabled — requires an account-side policy first)

Publishing currently uses a long-lived API key in `secrets.NUGET_API_KEY`. NuGet.org supports Trusted
Publishing, which replaces that with a short-lived OIDC-issued key, removing the stored secret entirely.

**This is deliberately not wired up**, because the workflow change alone would break releases. NuGet
issues the temporary key only when an incoming OIDC token matches a policy that must already exist on
nuget.org, and only the package owner can create it. Switching `release.yml` first would mean the next
tag builds, tests, verifies compatibility, and then fails at the push step.

**Step 1 — create the policy (nuget.org, owner only).** Sign in, open the account menu → *Trusted
Publishing*, and add a policy:

| Field | Value |
| --- | --- |
| Repository Owner | `georgepwall1991` |
| Repository | `automapper-analyser` |
| Workflow File | `release.yml` (file name only, no path) |
| Environment | leave empty — this workflow uses no GitHub environment |

If the *Trusted Publishing* option is absent, the feature has not reached the account yet; it is being
rolled out gradually. Policies on private repositories start in a 7-day provisional state and become
permanent after the first successful publish.

**Step 2 — add `NUGET_USER`** to repository secrets: the nuget.org *profile name*, not an email
address. `NuGet/login@v1` requires it and fails the login if it is empty.

**Step 3 — edit the `publish` job in `release.yml`.** Three edits, not a wholesale replacement — the
job already has `steps:` including the checkout and artifact download that produce
`steps.package.outputs.path`.

Add a job-level `permissions` block. Job-level permissions **replace** the workflow defaults rather
than adding to them, so `contents: write` must be restated: the same job creates the GitHub release
with `softprops/action-gh-release`, which fails without it.

```yaml
  publish:
    name: Publish verified package
    needs: [package, compatibility]
    runs-on: ubuntu-latest
    permissions:
      contents: write      # required by the GitHub release step later in this job
      id-token: write      # OIDC token issuance for the NuGet token exchange
```

Insert the login step immediately before the existing push step. The issued key is single-use and
expires after an hour, so it must not be requested earlier in the job:

```yaml
      - name: NuGet login (OIDC to short-lived key)
        uses: NuGet/login@v1
        id: nuget-login
        with:
          user: ${{ secrets.NUGET_USER }}
```

Then change only the `--api-key` argument of the existing push step, leaving the rest as-is:

```yaml
          --api-key ${{ steps.nuget-login.outputs.NUGET_API_KEY }}
```

`NUGET_API_KEY` can be deleted from repository secrets once a release has published successfully this
way.

### Coverage

`codecov.yml` declares a project target of 80% with 2% slack. `scripts/check-coverage.sh` enforces that
floor in CI directly from the coverage report, reading the threshold **out of `codecov.yml`** rather than
repeating it — two copies of a number that must agree is how the number stops agreeing.

The gate runs in `ci.yml`, `ci-pr.yml`, **and `release.yml`** — before packing. A `v*` tag can point at
a commit that never ran branch CI, and the release workflow is what publishes, so leaving it ungated
would mean the one artifact that reaches consumers was the one never checked.

This exists because the declared target was previously enforced only by the hosted service: the upload
step sets `fail_ci_if_error: false`, so a failed or missing upload does not fail the build, and the
status check can be absent on a fork or without a token. The published target was aspirational.

The current figure is deliberately not recorded here. A coverage percentage written into a document is
stale on the next commit; the gate reports the live value on every run.

### Version bump checklist

Bumping a version touches more than version strings. In particular:

- **Rewrite the README `## Latest Release` section body, not just its heading.** The heading and the
  prose beneath it are edited separately, so bumping only the heading silently attributes the previous
  release's changes to the new version. This README ships as the NuGet package readme, so the wrong
  summary reaches consumers on the package page. Independent review has caught this twice.
- Extending `tools/package-compatibility.json` requires updating `CompatibilityContractTests`, which
  pins the advertised matrix on purpose so it cannot drift silently.
- Run `dotnet run --project tools/AnalyzerVerifier -- --update-catalog --update-compatibility` after
  version or matrix changes, then re-run the `--check-*` modes.

## 🔧 Configuration

### Required Secrets

Configure these in GitHub repository settings:

| Secret | Description | Required For |
|--------|-------------|--------------|
| `NUGET_API_KEY` | NuGet.org API key for publishing. Removable after migrating to Trusted Publishing | Release pipeline |
| `NUGET_USER` | nuget.org profile name (not an email address). Only needed after migrating to Trusted Publishing | Release pipeline, post-migration |
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
dotnet run --project tools/AnalyzerVerifier -- --verify-package-compatibility ./packages/AutoMapperAnalyzer.Analyzers.2.30.96.nupkg --case net10-am14

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

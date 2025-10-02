# 🚀 Release and Publishing Guide

## Automated Releases with Semantic Versioning

This guide explains how to publish new versions of AutoMapperAnalyzer.Analyzers to NuGet.org using automated GitHub Actions workflows and semantic versioning.

## 🎯 Quick Start: Automated Releases (Recommended)

**The easiest way to publish a new version is via Git tags:**

```bash
# 1. Update CHANGELOG.md with your changes
# 2. Commit everything
git add .
git commit -m "chore: Prepare release v2.1.0"

# 3. Create and push a version tag
git tag v2.1.0
git push origin v2.1.0

# 4. GitHub Actions automatically:
#    ✅ Runs all tests
#    ✅ Builds the package
#    ✅ Publishes to NuGet.org
#    ✅ Creates a GitHub Release
```

**That's it!** Within 2-3 minutes, your package will be live on NuGet.org.

---

## 🔐 Security First

**⚠️ NEVER commit API keys to version control!**

The NuGet API key is stored as a GitHub repository secret (`NUGET_API_KEY`). Only repository administrators can set this secret.

---

## 📋 Detailed Release Process

### Step 1: Update CHANGELOG.md

Before creating a release, update `CHANGELOG.md` with your changes:

```markdown
## [2.1.0] - 2025-10-15

### Added
- New analyzer rule AM040 for detecting missing profile registration

### Fixed
- Fixed null reference issue in AM020 nested object mapping

### Changed
- Improved performance of AM001 type mismatch detection
```

### Step 2: Commit Your Changes

```bash
git add CHANGELOG.md
git add <any other files>
git commit -m "chore: Prepare release v2.1.0"
git push origin main
```

### Step 3: Create and Push a Version Tag

```bash
# Create the tag
git tag v2.1.0 -m "Release version 2.1.0"

# Push the tag to GitHub
git push origin v2.1.0
```

### Step 4: Monitor the Release Workflow

The release workflow will automatically start. You can monitor it at:
```
https://github.com/georgepwall1991/automapper-analyser/actions
```

Or use the GitHub CLI:
```bash
gh run watch
```

### Step 5: Verify the Release

Once the workflow completes (2-3 minutes):

1. **Check NuGet.org**: https://www.nuget.org/packages/AutoMapperAnalyzer.Analyzers/
2. **Check GitHub Releases**: https://github.com/georgepwall1991/automapper-analyser/releases
3. **Test Installation**:
   ```bash
   dotnet add package AutoMapperAnalyzer.Analyzers --version 2.1.0
   ```

---

## 🔢 Semantic Versioning

We follow [Semantic Versioning](https://semver.org/) with the format `MAJOR.MINOR.PATCH`:

### When to Increment Each Number

**MAJOR (2.x.x)** - Breaking changes:
- Removing or renaming analyzer diagnostic IDs
- Changing analyzer behavior that would break existing code
- Removing public APIs

**MINOR (x.1.x)** - New features (backwards compatible):
- Adding new analyzer rules (AM041, AM042, etc.)
- Adding new code fix providers
- Adding new public APIs or features

**PATCH (x.x.1)** - Bug fixes and minor improvements:
- Fixing false positives/negatives in analyzers
- Improving error messages
- Documentation updates
- Performance improvements

### Pre-release Versions

For alpha/beta testing, use tags like:
```bash
git tag v2.1.0-alpha.1
git tag v2.1.0-beta.1
git tag v2.1.0-rc.1
```

Pre-releases will be marked as such on GitHub and NuGet.

---

## 🔧 Manual Publishing (Fallback Option)

If you need to publish manually (e.g., GitHub Actions is down), you can use the `Publish.ps1` script:

### Prerequisites

Set the NuGet API key as an environment variable:

```powershell
# PowerShell
$env:NUGET_API_KEY = "your-api-key-here"
```

```bash
# Bash/Zsh
export NUGET_API_KEY="your-api-key-here"
```

### Manual Publish Steps

```powershell
# Clean, build, pack, and publish
dotnet clean
dotnet build src/AutoMapperAnalyzer.Analyzers/AutoMapperAnalyzer.Analyzers.csproj --configuration Release
dotnet pack src/AutoMapperAnalyzer.Analyzers/AutoMapperAnalyzer.Analyzers.csproj --configuration Release --output ./nupkg /p:PackageVersion=2.1.0
dotnet nuget push ./nupkg/*.nupkg --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json
```

**Note**: Local builds will generate packages with version `2.0.YYYYMMDD-local` unless you explicitly set `/p:PackageVersion=X.Y.Z`.

---

## 🛡️ Security Features

- ✅ **No hardcoded API keys** in source code
- ✅ **GitHub Secrets** for secure key storage in CI/CD
- ✅ **Environment variables** for local manual publishing
- ✅ **Automated testing** before every release
- ✅ **Safe for version control** - no secrets committed

---

## 🚨 Troubleshooting

### Release Workflow Fails

**Check the workflow logs:**
```bash
gh run list --limit 5
gh run view <run-id> --log
```

**Common issues:**
- Tests failing: Fix the failing tests before releasing
- NuGet API key expired: Update the `NUGET_API_KEY` secret in GitHub repository settings
- Version already exists: You can't republish the same version; increment the version number

### Package Already Exists Error

```
error: Response status code does not indicate success: 409 (Conflict)
```

**Solution:** The version you're trying to publish already exists on NuGet.org. You must increment the version number.

### Tag Already Exists

```
fatal: tag 'v2.1.0' already exists
```

**Solution:** Either delete the tag or use a different version:
```bash
# Delete local and remote tag
git tag -d v2.1.0
git push origin :refs/tags/v2.1.0

# Or use a new version
git tag v2.1.1
```

### GitHub Actions Not Triggering

**Solution:** Ensure you pushed the tag to the remote repository:
```bash
git push origin v2.1.0
```

### Setting GitHub Secret (For Administrators)

If the `NUGET_API_KEY` secret needs to be updated:

1. Go to repository Settings → Secrets and variables → Actions
2. Update the `NUGET_API_KEY` secret with your NuGet API key
3. Or use the GitHub CLI:
   ```bash
   gh secret set NUGET_API_KEY
   ```

---

## 🔗 Useful Links

- **NuGet Package**: [AutoMapperAnalyzer.Analyzers](https://www.nuget.org/packages/AutoMapperAnalyzer.Analyzers/)
- **GitHub Releases**: [automapper-analyser/releases](https://github.com/georgepwall1991/automapper-analyser/releases)
- **GitHub Actions**: [automapper-analyser/actions](https://github.com/georgepwall1991/automapper-analyser/actions)
- **NuGet API Keys**: [NuGet Account](https://www.nuget.org/account/apikeys)
- **Semantic Versioning**: [semver.org](https://semver.org/)
- **Keep a Changelog**: [keepachangelog.com](https://keepachangelog.com/)

---

## 📈 Version History

View all published versions at:
- **NuGet.org**: https://www.nuget.org/packages/AutoMapperAnalyzer.Analyzers/
- **GitHub Releases**: https://github.com/georgepwall1991/automapper-analyser/releases

The automated release process ensures consistent, tested releases with proper versioning! 
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.0] - TBD

### Added
- **Automated Release Pipeline**: GitHub Actions workflow for automated releases triggered by Git tags
- **Semantic Versioning**: Transitioned from date-based versioning to semantic versioning (2.x.x)
- **CHANGELOG.md**: Comprehensive changelog following Keep a Changelog format
- **Helper Classes**: Created reusable utility classes for code fix providers
  - `CodeFixSyntaxHelper`: Reusable syntax generation methods
  - `TypeConversionHelper`: Centralized type conversion and default value logic

### Changed
- **Major Code Refactoring**: Refactored all 7 CodeFix providers for improved maintainability
  - AM011_UnmappedRequiredPropertyCodeFixProvider: 278 → 125 lines (45% reduction)
  - AM004_MissingDestinationPropertyCodeFixProvider: 218 → 111 lines (49% reduction)
  - AM005_CaseSensitivityMismatchCodeFixProvider: 170 → 123 lines (28% reduction)
  - AM003_CollectionTypeIncompatibilityCodeFixProvider: 274 → 188 lines (31% reduction)
  - AM001_PropertyTypeMismatchCodeFixProvider: 304 → 163 lines (46% reduction)
  - AM030_CustomTypeConverterCodeFixProvider: 381 → 228 lines (40% reduction)
  - AM020_NestedObjectMappingCodeFixProvider: 307 → 248 lines (19% reduction)
  - **Total**: 1,932 → 1,186 lines (38.6% overall reduction)
- **Unified Package Structure**: Merged CodeFixes into Analyzers project for single unified package
- **Simplified Build Process**: Removed manual DLL copying, improved MSBuild targets
- **Improved CI/CD**: Enhanced workflows to verify package installation from NuGet feed

### Fixed
- **Package Installation**: CodeFix providers now properly included and functional when installed via NuGet
- **Cross-Platform Compatibility**: Verified installation works on .NET Framework 4.8, .NET 6.0, and .NET 8.0+
- **Test Coverage**: All 131 tests passing with 100% success rate

### Removed
- **Separate CodeFixes Package**: Eliminated AutoMapperAnalyzer.CodeFixes project in favor of unified package
- **Date-Based Versioning**: Replaced with semantic versioning for better version management
- **Manual Package Structure**: Removed fragile manual DLL copying from .csproj

## [1.0.x] - 2024-2025 (Date-Based Versions)

### Summary of Pre-2.0 Development
Previous versions used date-based versioning (1.0.YYYYMMDD). Major accomplishments:

- **AM001**: Property Type Mismatch detection with code fixes
- **AM002**: Nullable Compatibility analysis
- **AM003**: Collection Type Incompatibility detection with code fixes
- **AM004**: Missing Destination Property detection with code fixes
- **AM005**: Case Sensitivity Mismatch detection with code fixes
- **AM011**: Unmapped Required Property detection with code fixes
- **AM020**: Nested Object Mapping detection with code fixes
- **AM021**: Collection Element Mismatch detection
- **AM022**: Infinite Recursion Risk detection
- **AM030**: Custom Type Converter analysis with code fixes

### Infrastructure
- Comprehensive test suite (131 tests)
- CI/CD pipeline with GitHub Actions
- Multi-framework compatibility testing
- Code coverage reporting
- Sample projects demonstrating analyzer capabilities

---

## Release Process

### For Maintainers

To create a new release:

1. Update CHANGELOG.md with the new version and release notes
2. Commit all changes: `git commit -m "chore: Prepare release v2.x.x"`
3. Create and push a version tag:
   ```bash
   git tag v2.x.x
   git push origin v2.x.x
   ```
4. GitHub Actions will automatically:
   - Run full test suite
   - Build the package with the version from the tag
   - Publish to NuGet.org
   - Create a GitHub Release with the package attached

### Version Numbering

We follow [Semantic Versioning](https://semver.org/):

- **MAJOR** (2.x.x): Breaking changes to analyzer rules or APIs
- **MINOR** (x.1.x): New analyzer rules, new code fixes, or new features
- **PATCH** (x.x.1): Bug fixes, documentation updates, or minor improvements

### Pre-release Versions

For pre-release testing, use tags like `v2.1.0-alpha.1` or `v2.1.0-beta.1`:

```bash
git tag v2.1.0-beta.1
git push origin v2.1.0-beta.1
```

Pre-releases will be marked as "pre-release" on GitHub and can be installed via:
```bash
dotnet add package AutoMapperAnalyzer.Analyzers --version 2.1.0-beta.1
```

---

[Unreleased]: https://github.com/georgepwall1991/automapper-analyser/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/georgepwall1991/automapper-analyser/releases/tag/v2.0.0

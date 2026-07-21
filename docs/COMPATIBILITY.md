# AutoMapper Analyzer compatibility contract

Framework support is a release-blocking, package-level contract. CI builds one `.nupkg`, then installs and builds consumers against these exact target-framework and AutoMapper combinations before the same package bytes can be published.

## Supported combinations

This table is generated from [`tools/package-compatibility.json`](../tools/package-compatibility.json). Edit the manifest, then run the update command below; do not edit the table directly.

<!-- compatibility-matrix:start -->
| Case | Runner | Target | AutoMapper |
| --- | --- | --- | --- |
| `net48-am10` | `windows-latest` | `net48` | `10.1.1` |
| `net6-am12` | `ubuntu-latest` | `net6.0` | `12.0.1` |
| `net8-am14` | `ubuntu-latest` | `net8.0` | `14.0.0` |
| `net9-am14` | `ubuntu-latest` | `net9.0` | `14.0.0` |
| `net10-am14` | `ubuntu-latest` | `net10.0` | `14.0.0` |
<!-- compatibility-matrix:end -->

The analyzer package targets `netstandard2.0`. The matrix above verifies that the packaged analyzer loads and behaves correctly for each consumer target when built by the configured CI SDK. It does not claim coverage for historical Visual Studio, Rider, OmniSharp, or other IDE hosts that the workflow does not run.

## What each case proves

For every case, the verifier reads the package ID and version from the `.nuspec` inside the supplied `.nupkg`, then creates two isolated consumers under `artifacts/package-compatibility/`:

- A healthy string-to-string mapping must build successfully without any `AM###`, `AD0001`, or `CS8032` diagnostics.
- An intentionally broken string-to-int mapping must fail with `AM001` and without analyzer exception or load failures.

Unrelated SDK lifecycle and NuGet advisory warnings do not fail the contract. Analyzer diagnostics and analyzer-load failures do.

## Local verification

Check manifest validation and documentation drift:

```bash
dotnet run --project tools/AnalyzerVerifier/AnalyzerVerifier.csproj -- \
  --check-compatibility
```

Regenerate this table after an intentional manifest change:

```bash
dotnet run --project tools/AnalyzerVerifier/AnalyzerVerifier.csproj -- \
  --update-compatibility
```

Print the GitHub Actions matrix:

```bash
dotnet run --project tools/AnalyzerVerifier/AnalyzerVerifier.csproj -- \
  --print-compatibility-matrix
```

Verify one case against an already packed artifact:

```bash
dotnet run --project tools/AnalyzerVerifier/AnalyzerVerifier.csproj -- \
  --verify-package-compatibility artifacts/package/AutoMapperAnalyzer.Analyzers.999.0.0-smoke.nupkg \
  --case net10-am14
```

The verifier consumes the supplied package; it does not repack the analyzer. This preserves the release invariant that all compatibility evidence and publication refer to the same artifact bytes.

## Installation

Reference an AutoMapper version from the supported table and install the analyzer as a private development asset:

```xml
<PackageReference Include="AutoMapperAnalyzer.Analyzers" Version="&lt;version&gt;">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

For a compatibility failure, report the case ID, target framework, AutoMapper version, build SDK version, and complete build output in a [GitHub issue](https://github.com/georgepwall1991/automapper-analyser/issues).

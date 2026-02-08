# AutoMapper Analyzer Samples

This project demonstrates analyzer diagnostics and code-fix scenarios for `AutoMapperAnalyzer.Analyzers`.

## Purpose

- Show realistic mapping mistakes and their diagnostics.
- Provide side-by-side "correct" examples.
- Validate sample behavior against the sample `.editorconfig` severity policy.

## Severity Policy

`/Users/georgewall/RiderProjects/automapper-analyser/samples/AutoMapperAnalyzer.Samples/.editorconfig` sets AutoMapper diagnostics to `warning` severity for sample code.

That keeps builds successful while still surfacing diagnostics in the IDE and build output.

## Implemented Rules Demonstrated

The sample project currently demonstrates these implemented rules:

- `AM001` Property type mismatch
- `AM002` Nullable compatibility mismatch
- `AM003` Collection container incompatibility
- `AM004` Missing destination/source member intent issues
- `AM005` Case-sensitivity mismatch
- `AM006` Unmapped destination property
- `AM011` Unmapped required destination property
- `AM020` Missing nested object mapping
- `AM021` Collection element mismatch
- `AM022` Infinite recursion risk
- `AM030` Custom converter quality/usage issues
- `AM031` Performance anti-patterns in mapping expressions
- `AM041` Duplicate mapping registration
- `AM050` Redundant `MapFrom` configuration

## Notes on Rule Ownership

Recent analyzer ownership hardening changed expectations in samples:

- Property conversion mismatches like `string -> DateTime` are owned by `AM001` (not `AM030`).
- `AM030` focuses on converter implementation quality and converter usage (for example, null handling and unused converters).
- Collection container mismatches are owned by `AM003`; element mismatches are owned by `AM021`.

## Build and Verify

From repository root:

```bash
dotnet build samples/AutoMapperAnalyzer.Samples/AutoMapperAnalyzer.Samples.csproj --configuration Release
```

Expected behavior:

- Build succeeds.
- Diagnostics appear as `warning AM###` per sample `.editorconfig`.
- No AutoMapper analyzer diagnostics are elevated to errors in samples.

## Structure

```
samples/AutoMapperAnalyzer.Samples/
├── Program.cs
├── SampleUsage.cs
├── TypeSafety/
├── MissingProperties/
├── UnmappedDestination/
├── ComplexTypes/
├── Conversions/
├── Configuration/
└── Performance/
```

## Tips

- Use `Ctrl+.` (VS Code) or `Alt+Enter` (Rider) on diagnostics to inspect code fixes.
- Prefer keeping sample code intentionally broken only where a diagnostic is being demonstrated.
- Keep comments synchronized with real analyzer ownership and diagnostic IDs.

## Current Version Context

- Last Updated: 2026-02-08
- Analyzer Version Context: `v2.26.0`
- AutoMapper Version in samples: `14.0.0`

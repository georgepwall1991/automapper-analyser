# Improvement Plan (TDD)

This plan breaks work into small, test-first tasks. Each task follows the TDD loop: write/extend a failing test, implement the smallest change, refactor, and keep coverage stable or improving.

## Principles
- Red-green-refactor for every change.
- Prefer semantic model over string parsing.
- Cache where analysis repeats; measure with tests when feasible.
- Keep PRs small and focused; update docs/help links alongside behavior changes.

## Analyzer Robustness
- [x] CreateMap detection: rely on semantic checks (method symbol + AutoMapper types); remove string fallbacks.
- [~] ForMember/ConvertUsing parsing: replace string Contains with lambda + symbol resolution (AM004/AM005/AM021/AM030). (Partial: AM005 uses helper; AM004 inspects lambda parameter access; AM030 parses invocation syntax in config lambdas.)
- [x] AM021: detect element mappings across file/scope (limited compilation scan with caching).
- [x] AM021: report multiple element mismatches in a single mapping (fix TODOs in tests).
- [ ] AM022: recognize `PreserveReferences()`/`MaxDepth()` and common ignore patterns (reduce false positives).

## Type Compatibility & Conversions
- [ ] Refactor implicit conversion checks to SpecialType-based matrix (unify with `AreNumericTypesCompatible`).
- [ ] Tighten nullable compatibility logic using `NullableAnnotation` consistently.

## Performance & Architecture
- [ ] Add `CompilationStartAction` to locate AutoMapper symbols once per compilation.
- [ ] Add caching for `GetMappableProperties` and `GetCollectionElementType` keyed by `ITypeSymbol` with `SymbolEqualityComparer.Default`.
- [ ] Scope analysis to relevant files/nodes; avoid scanning entire trees where not needed.

## Code Fixes
- [ ] AM001: ensure replacements preserve existing invocation chains; add tests for chaining.
- [ ] AM021: add code fix to scaffold element conversion (`Select` + conversion or `ConvertUsing`).
- [ ] AM030: add fix suggestions when converter is present but not wired (`ConvertUsing<T>` snippet).

## Testing (TDD)
- [ ] Implement real code-fix tests using `CSharpCodeFixTest` and migrate existing code fix tests.
- [ ] Add negative tests (non-AutoMapper `CreateMap`, complex lambdas, nested profiles) to prevent regressions.
- [ ] Unskip/replace TODO tests in AM021 to validate multiple diagnostics and element mapping detection.

## CI & Quality Gates
- [ ] Add `dotnet format --verify-no-changes` to CI.
- [ ] Test matrix for `net8.0` and `net9.0` for analyzer projects and tests.
- [ ] Fail on analyzer categories set to error in `.editorconfig` to match README expectations.

## Documentation & Help Links
- [ ] Add `HelpLinkUri` for each DiagnosticDescriptor to anchors in `docs/DIAGNOSTIC_RULES.md`.
- [ ] Update samples to show recommended suppressions with justification.

## Next Steps
Start with Analyzer Robustness (CreateMap + ForMember semantics), then migrate code-fix tests, followed by AM021 improvements and performance caching.

---

## Status Update (latest)

What’s done (tests first, then implementation):
- Hardened CreateMap detection (semantic-only):
  - Tests: `tests/Infrastructure/CreateMapDetectionTests.cs` (positive in Profile; negative on non-AutoMapper receiver).
  - Code: `AutoMapperAnalysisHelpers.IsCreateMapInvocation` now checks method symbol, receiver type, and enclosing `Profile` inheritance.
- AM005 explicit mapping semantics:
  - Tests: added constant-mapping case to suppress casing diagnostic.
  - Code: `AM005_CaseSensitivityMismatchAnalyzer` uses semantic helper for `ForMember` on destination.
- AM021 element mapping robustness:
  - Tests: enabled “explicit element mapping” case; added “multiple collection issues” case.
  - Code: skips diagnostics when `CreateMap<elementSrc, elementDest>` exists anywhere in the compilation; reports multiple.

- AM030 ConvertUsing detection improvements:
  - Tests: added global `ConvertUsing(src => ...)` and property-level `opt.ConvertUsing(new IValueConverter<,>(), s => s.Member)` cases; both suppress missing-converter diagnostics.
  - Code: `HasConvertUsingInForMember` now traverses the configuration lambda syntax to find `ConvertUsing`/`MapFrom` invocations (no string matching).

- AM004 custom mapping semantics:
  - Tests: added “unrelated object same-name property” to ensure we still report missing source property; added static helper method in `MapFrom` to ensure property usage is detected through method calls.
  - Code: `AM004` now walks `ForMember` config lambdas, verifying that member access is on the lambda parameter (e.g., `src.ImportantData`), not any identifier.

All tests pass (except two intentionally skipped AM030 advanced tests).

Notes and tips for continuation:
- ForMember/ConvertUsing parsing (remaining): extend `AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember` to inspect second-argument lambdas semantically (e.g., detect `ConvertUsing`, complex `MapFrom` chains) and use SemanticModel to confirm AutoMapper symbols when feasible. Add focused tests in AM004 next.
- AM001 numeric conversions: add tests for implicit numeric widening and refactor to a SpecialType-based matrix; unify with `AreNumericTypesCompatible`.
- Performance: introduce `CompilationStartAction` and symbol/type caches after behavior is green to avoid conflating perf with logic changes.
- Code fixes: prioritize AM021 element conversion scaffolding; add real `CSharpCodeFixTest` coverage.

All tests green locally (140 passed, 2 skipped for advanced AM030 cases).

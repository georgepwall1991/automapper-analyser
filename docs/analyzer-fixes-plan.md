# Analyzer Improvement Plan

This document captures the incremental plan to address the issues identified during the in-depth review of the AutoMapper analyzer suite.

## Goals
- Restore analyzer accuracy for real-world AutoMapper usage (read-only members, multi-file profiles, nested mappings, custom converters).
- Produce actionable, compilable code fixes.
- Replace placeholder testing infrastructure with working analyzer/code-fix tests to prevent regressions.
- Keep the solution shippable at each increment (builds succeed, test suites pass).

## Incremental Work Plan

### 1. Property Discovery & Nullable Accuracy
- Split `GetMappableProperties` into source/destination variants so read-only source properties and set-only destination properties are evaluated correctly.
- Update analyzers (AM001, AM002, AM003, AM011, AM020, AM021, AM030, AM031) to use the appropriate accessor rules.
- Add regression tests that cover read-only/`init` source members and write-only destination members.

### 2. Reliable CreateMap Lookup & Type Identity
- Introduce a central `CreateMapRegistry` that scans the Roslyn `Compilation` for AutoMapper mappings (using fully-qualified names, considering partial classes and separate files).
- Replace the per-file searches in AM001 and AM020 with the registry.
- Guard against namespace collisions by comparing `SymbolEqualityComparer.Default` on the resolved type symbols.
- Extend unit tests to cover profiles spread across multiple files and namespaces.

### 3. AM001 Code Fix Enhancements
- Analyze actual source/destination type pairs when building fixes.
- Offer targeted fixes:
  - Generate explicit casts or conversion expressions only when safe.
  - Suggest default values that respect destination type nullability.
  - Provide “ignore property” as a separate fix option.
- Implement real code-fix tests (after Step 5) to validate the generated syntax.

### 4. AM030 Converter Diagnostics Refinement
- Check for existing `CreateMap<TSource, TDestination>` (direct or nested) before flagging missing converters.
- Treat complex object mismatches as AM020 responsibilities to avoid duplicate or misleading diagnostics.
- Add coverage for profiles where converters coexist with nested mappings.

### 5. Test Infrastructure Overhaul
- Replace the placeholder `CodeFixTestFramework` and `MultiAnalyzerTestRunner` with standard `CSharpAnalysisTest`/`CSharpCodeFixTest` harnesses from `Microsoft.CodeAnalysis.Testing`.
- Port existing analyzer and code-fix tests to the new harness, ensuring they execute both diagnostics and code fixes end-to-end.
- Add CI-friendly helpers for adding common references (AutoMapper assemblies, etc.).

### 6. Verification & Documentation
- Run the full test suite (analyzers + code fixes) and ensure consistent success locally.
- Document new helper utilities and testing approach in `docs/` and cross-link from `README.md` if helpful.
- Update release notes / changelog stubs with noteworthy fixes once all increments land.

## Deliverables per Increment
Each increment should finish with:
- Updated implementation.
- Passing unit tests relevant to the change.
- Updated documentation when behavior or usage changes.

## Out of Scope
- Automatic detection of `ConvertUsing` across external assemblies (future enhancement).
- Non-C# language support.

## Tracking
Progress will be tracked on branch `feature/analyzer-improvements` with commits scoped to the increments above.

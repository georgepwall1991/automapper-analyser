# Changelog

## Unreleased

### Added

- Added generated rule catalog and sample diagnostics snapshot checks to prevent descriptor, fixer, docs, and sample-output drift.
- Added package smoke tests that install the packed analyzer into temporary `net8.0`, `net9.0`, and `net10.0` consumer projects and assert `AM001` fires from the NuGet package.
- Added analyzer release tracking files for shipped diagnostic IDs.

### Changed

- Consolidated PR validation into the main CI workflow and removed the duplicate simple-build workflow.

## [2.30.5] - 2026-04-26

### Changed

- Hardened AM030 unused-converter analysis so `ConvertUsing(typeof(MyConverter))` counts as real converter usage.
- Added AM030 regression coverage for type-based `ConvertUsing` configuration.
- Updated AM030 rule docs and analyzer health status to document supported converter usage forms.

### Validation

- Targeted AM030 analyzer tests.
- Full `net10.0` solution test suite.
- `git diff --check`.

## [2.30.4] - 2026-04-26

### Changed

- Hardened AM022 recursion diagnostics so unrelated self-referencing or circular graphs no longer report unless the recursive path is convention-mapped.
- Added AM022 regression coverage for mismatched recursive member names, independent circular graphs, and ignored indirect recursive members.
- Updated AM022 rule docs and analyzer health status to describe the mapped-member precision boundary.

### Validation

- Targeted AM022 analyzer and code fix tests.
- Full `net10.0` solution test suite.
- `git diff --check`.

## [2.30.3] - 2026-04-26

### Changed

- Hardened AM011 required-member diagnostics so explicit `ForPath` configuration no longer reports as unmapped.
- Added AM011 regression coverage for direct and nested `ForPath` destination-member configuration.
- Updated AM011 rule docs and analyzer health status to document explicit configuration and manual-review fixer boundaries.

### Validation

- Targeted AM011 analyzer tests.
- Full `net10.0` solution test suite.
- `git diff --check`.

## [2.30.2] - 2026-04-26

### Changed

- Hardened AM003 collection-container diagnostics so implicitly assignable source collections no longer require unnecessary explicit mapping.
- Added AM003 regression coverage for array-to-interface and set-to-read-only-interface collection shapes.
- Updated AM003 rule docs and analyzer health status to document the safe assignable boundary.

### Validation

- Targeted AM003 analyzer and code fix tests.
- Full `net10.0` solution test suite.
- `git diff --check`.

## [2.30.1] - 2026-04-26

### Changed

- Aligned AM002 documentation with its shipped descriptor split: nullable source to non-nullable destination is `Error`, while non-nullable source to nullable destination is `Info`.
- Added AM002 nullable-context regression coverage for oblivious reference types and nullable value types.
- Updated analyzer health and README rule metadata so public severity guidance matches the implementation.

### Validation

- Targeted AM002 analyzer tests.
- Full `net10.0` solution test suite.
- `git diff --check`.

## [2.30.0] - 2026-04-26

### Fixed

- Hardened AM001, AM005, AM006, AM011, and AM021 fixer behavior with safer action selection.

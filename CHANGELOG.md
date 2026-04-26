# Changelog

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

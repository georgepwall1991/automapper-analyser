# Changelog

## Unreleased

### Changed

### Validation

## [2.30.15] - 2026-05-04

### Changed

- Added descriptor-specific code-fix trust metadata so analyzer-only AM002 and AM030 descriptors are reported as no-fix in generated trust artifacts.
- Hardened AM003 collection container fixes so destination collection interfaces use executable `ToList()` or concrete `HashSet<T>` rewrites instead of invalid interface constructors.
- Hardened AM021 simple element conversion fixes so set and read-only set destinations use concrete `HashSet<T>` mappings while list-like interfaces keep `ToList()`.
- Clarified AM004 and AM006 manual-review code action titles, prefixed AM005 equivalence keys, and updated docs, analyzer health notes, rule catalog, and sample diagnostic snapshots.
- Added regression coverage for unsupported AM002/AM030 descriptors, AM003/AM021 interface collection branches, and AM004/AM006 data-integrity code action UX.

### Validation

- Targeted AM002, AM003, AM004, AM005, AM006, AM021, AM030, and RuleCatalog tests.
- Full `net10.0` solution test suite.
- Rule catalog and sample diagnostics snapshot checks.
- `git diff --check`.

## [2.30.14] - 2026-05-04

### Changed

- Hardened AM021 so parent `ReverseMap()` collection mappings check the reverse element direction when only the forward element map exists.
- Kept plain bidirectional AM021 element-map misses to one focused forward diagnostic until the forward direction is configured.
- Added AM021 regression coverage for reverse-only element gaps, fully configured reverse maps, and non-duplicated forward diagnostics.
- Updated AM021 docs, analyzer health notes, and release metadata for the reverse-map element boundary.

### Validation

- Targeted AM021 analyzer and code fix tests.
- Full `net10.0` solution test suite.
- Rule catalog and sample diagnostics snapshot checks.
- `git diff --check`.

## [2.30.13] - 2026-05-04

### Changed

- Hardened AM041 duplicate-map diagnostics so constructed generic and array source/destination types, including multidimensional array ranks, are included in the reported mapping labels.
- Added AM041 regression coverage for generic, single-dimensional array, and multidimensional array duplicate labels.
- Updated AM041 docs and analyzer health status to describe the improved duplicate-map diagnostic labels.

### Validation

- Targeted AM041 analyzer and code fix tests.
- Full `net10.0` solution test suite.
- Rule catalog and sample diagnostics snapshot checks.
- `git diff --check`.

## [2.30.12] - 2026-05-03

### Changed

- Hardened AM030 unused-converter analysis so simple `ITypeConverter<TSource, TDestination>` locals, fields, and properties initialized with concrete converters count as `ConvertUsing` usage.
- Added AM030 regression coverage for interface-typed local, field, and property converter usage.
- Updated AM030 docs and analyzer health status to describe the supported interface-typed converter usage boundary.
- Hardened AM021 dictionary element mismatch code fixes so `KeyValuePair<TKey, TValue>` diagnostics no longer offer unsafe element `CreateMap` suggestions.
- Added AM021 regression coverage proving dictionary mismatch diagnostics keep only the manual ignore action.
- Updated AM021 docs and analyzer health status to describe the dictionary fixer safety boundary.

### Validation

- Targeted AM030 analyzer tests.
- Targeted AM021 analyzer and code fix tests.
- Full `net10.0` solution test suite.
- Rule catalog and sample diagnostics snapshot checks.
- `git diff --check`.

## [2.30.11] - 2026-04-27

### Changed

- Added AM001 code fixes for enum-to-string property mappings using `ToString()`.
- Added AM001 code fixes for string-to-enum property mappings using null-guarded, fully qualified `Enum.Parse<TEnum>()`.
- Updated analyzer health status to record the AM001 enum conversion fixer hardening pass.

### Validation

- Targeted AM001 code fix tests.
- Full `net10.0` solution test suite.
- `git diff --check`.

## [2.30.10] - 2026-04-26

### Changed

- Hardened AM050 redundant `MapFrom` detection so cleanup diagnostics require proven source/destination property type compatibility.
- Added AM050 regression coverage for string-based `ForMember` destination members with compatible and incompatible same-name properties.
- Preserved property-specific AM050 code action titles for string-based destination members.
- Updated analyzer health status to record the AM050 false-positive hardening pass.

### Validation

- Targeted AM050 analyzer and code fix tests.
- Full `net10.0` solution test suite.
- `git diff --check`.

## [2.30.9] - 2026-04-26

### Changed

- Aligned AM004 and AM005 rule docs with shipped Warning severity and descriptor categories.
- Documented AM031's mixed Warning/Info descriptor severity in the rule docs.
- Added a rule-catalog trust test that prevents documented severity lines from drifting away from shipped descriptors.
- Updated analyzer health status to record the AM004/AM005 documentation hardening pass.

### Validation

- Targeted rule catalog trust tests.
- Full `net10.0` solution test suite.
- `git diff --check`.

## [2.30.8] - 2026-04-26

### Changed

- Hardened AM030 nullable-source converter fixes so generated null guards use `global::System.ArgumentNullException` without adding or reordering `using System`.
- Added AM030 code fix regression coverage for absent, existing, and global `System` usings, file-scoped namespaces, expression-bodied converters, and multi-diagnostic fixer behavior.
- Updated AM030 rule docs and analyzer health status to document the less invasive null-guard fixer.

### Validation

- Targeted AM030 code fix tests.
- Full `net10.0` solution test suite.
- `git diff --check`.

## [2.30.7] - 2026-04-26

### Changed

- Hardened AM022 recursion diagnostics so indirect cycles require the nested `CreateMap` chain AutoMapper would use.
- Suppressed AM022 when forward mappings use `PreserveReferences` or `ConvertUsing` to own recursion behavior.
- Added AM022 regression coverage for missing nested maps, semantic `PreserveReferences`, `ConvertUsing`, `ConstructUsing` boundaries, and updated indirect-cycle fixer fixtures.
- Updated AM022 rule docs and analyzer health status to document supported recursion controls and nested-map boundaries.

### Validation

- Targeted AM022 analyzer and code fix tests.
- Full `net10.0` solution test suite.
- `git diff --check`.

## [2.30.6] - 2026-04-26

### Added

- Added generated rule catalog and sample diagnostics snapshot checks to prevent descriptor, fixer, docs, and sample-output drift.
- Added package smoke tests that install the packed analyzer into temporary `net8.0`, `net9.0`, and `net10.0` consumer projects and assert `AM001` fires from the NuGet package.
- Added analyzer release tracking files for shipped diagnostic IDs.

### Changed

- Hardened AM031 multiple-enumeration analysis so diagnostics normalize source-rooted collection paths regardless of lambda parameter name.
- Hardened AM031 cache fixes so nested source collections are rewritten safely and captured collections no longer offer invalid source-parameter cache actions.
- Added AM031 Task-valued source-property `.Result` detection.
- Updated AM031 rule docs and analyzer health status to document cache-fixer safety boundaries.
- Consolidated PR validation into the main CI workflow and removed the duplicate simple-build workflow.

### Validation

- Targeted AM031 analyzer and code fix tests.
- Full `net10.0` solution test suite.
- Rule catalog and sample diagnostics snapshot checks.
- `git diff --check`.

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

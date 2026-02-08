# Analyzer Review Tracker

Tracks analyzer-by-analyzer improvement passes focused on false positives, contradictory diagnostics, and code-fix reliability.

## Completed

| Analyzer | Area | PR | Release | Notes |
|---|---|---|---|---|
| AM011 | Data Integrity | [#48](https://github.com/georgepwall1991/automapper-analyser/pull/48) | [v2.9.0](https://github.com/georgepwall1991/automapper-analyser/releases/tag/v2.9.0) | Required-property mapping false-positive reductions; ctor/custom conversion handling. |
| AM006 | Data Integrity | [#48](https://github.com/georgepwall1991/automapper-analyser/pull/48) | [v2.9.0](https://github.com/georgepwall1991/automapper-analyser/releases/tag/v2.9.0) | Removed duplicate/contradictory reporting overlap with AM011 on required members. |
| AM004 | Data Integrity | [#48](https://github.com/georgepwall1991/automapper-analyser/pull/48) | [v2.9.0](https://github.com/georgepwall1991/automapper-analyser/releases/tag/v2.9.0) | Direction-aware construct/convert handling; fixer bulk logic aligned to analyzer behavior. |
| AM005 | Data Integrity | [#48](https://github.com/georgepwall1991/automapper-analyser/pull/48) | [v2.9.0](https://github.com/georgepwall1991/automapper-analyser/releases/tag/v2.9.0) | Added reverse-map analysis + de-duplication; fixed per-diagnostic code action binding. |
| AM041 | Configuration | [#49](https://github.com/georgepwall1991/automapper-analyser/pull/49) | [v2.10.0](https://github.com/georgepwall1991/automapper-analyser/releases/tag/v2.10.0) | Non-AutoMapper false-positive guard and safer reverse-map duplicate rewrite behavior. |
| AM050 | Configuration | [#50](https://github.com/georgepwall1991/automapper-analyser/pull/50) | [v2.11.0](https://github.com/georgepwall1991/automapper-analyser/releases/tag/v2.11.0) | Reduced syntax-only false positives via AutoMapper symbol checks and lambda-source validation. |
| AM020 | Complex Mappings | [#51](https://github.com/georgepwall1991/automapper-analyser/pull/51) | [v2.12.0](https://github.com/georgepwall1991/automapper-analyser/releases/tag/v2.12.0) | Unified analyzer/fixer suppression logic and direction-aware handling for `ForMember`/`ForPath`/construct-convert configuration. |
| AM021 | Complex Mappings | [#52](https://github.com/georgepwall1991/automapper-analyser/pull/52) | [v2.13.0](https://github.com/georgepwall1991/automapper-analyser/releases/tag/v2.13.0) | Direction-aware reverse-map handling, better custom-conversion suppression, and safer fixer type inference/output for collection element mismatches. |
| AM022 | Complex Mappings | [#53](https://github.com/georgepwall1991/automapper-analyser/pull/53) | [v2.14.0](https://github.com/georgepwall1991/automapper-analyser/releases/tag/v2.14.0) | Direction-aware reverse-map suppression, stricter ignore-all handling for recursion risks, and collection-aware recursion fixer behavior. |
| AM030 | Complex Mappings | [#54](https://github.com/georgepwall1991/automapper-analyser/pull/54) | [v2.15.0](https://github.com/georgepwall1991/automapper-analyser/releases/tag/v2.15.0) | Exact `ForMember` property matching (including case-insensitive suppression), `Ignore`/`ConstructUsing` suppression support, stronger converter signature/null-check analysis, and safer code-fix routing for missing-converter diagnostics. |
| AM031 | Performance | [#55](https://github.com/georgepwall1991/automapper-analyser/pull/55) | [v2.16.0](https://github.com/georgepwall1991/automapper-analyser/releases/tag/v2.16.0) | Stricter AutoMapper semantic checks, reduced heuristic false positives (`Random`/reflection/database), richer diagnostic metadata for fixer routing, and broader performance regression coverage (`Task.Wait`, parenthesized lambdas, `DateTime.UtcNow`). |
| AM001 | Type Safety | [#56](https://github.com/georgepwall1991/automapper-analyser/pull/56) | [v2.17.0](https://github.com/georgepwall1991/automapper-analyser/releases/tag/v2.17.0) | Added semantic AutoMapper invocation guards, fixed AM001/AM002 overlap for nullable + incompatible types, improved fixer conversion safety/routing, and expanded AM001 regression coverage. |
| AM002 | Type Safety | [#57](https://github.com/georgepwall1991/automapper-analyser/pull/57) | [v2.18.0](https://github.com/georgepwall1991/automapper-analyser/releases/tag/v2.18.0) | Added strict AutoMapper symbol checks, parenthesized-lambda `ForMember` suppression, metadata-backed fixer extraction, and regression coverage for lookalike APIs and nullable+incompatible scenarios. |
| AM003 | Type Safety | [#58](https://github.com/georgepwall1991/automapper-analyser/pull/58) | [v2.19.0](https://github.com/georgepwall1991/automapper-analyser/releases/tag/v2.19.0) | Added semantic AutoMapper guards, robust parenthesized-lambda suppression, exact generic collection kind checks, compatibility metadata aliases, and expanded false-positive regression coverage. |

## In Progress

| Analyzer | Area | Branch | Goal |
|---|---|---|---|
| AM010 | Data Integrity | `codex/am010-next-pass` | Next analyzer pass for false positives/fixer reliability improvements. |

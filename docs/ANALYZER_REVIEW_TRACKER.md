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

## In Progress

| Analyzer | Area | Branch | Goal |
|---|---|---|---|
| AM020 | Complex Mappings | `codex/am020-next-pass` | Next analyzer pass for false positives/fixer reliability improvements. |

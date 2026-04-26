# Analyzer Health

Reviewed: 2026-04-26

This is a deliberately harsh health audit for the 14 implemented AutoMapper analyzer rule IDs in this repository. Several rule IDs expose multiple diagnostic descriptors, especially `AM002`, `AM022`, `AM030`, and `AM031`; the scorecard rates the public rule ID as the user experiences it.

Every implemented rule currently has an analyzer and a code fix provider. Scores are 1-5, where `5` means reference-quality and hard to improve, `3` means usable but meaningfully incomplete, and `1` means unreliable or underbuilt.

## Rubric

| Metric | Meaning |
| --- | --- |
| Analyzer | Semantic depth, AutoMapper-awareness, fluent-chain handling, reverse-map handling, ownership boundaries, and diagnostic placement accuracy. |
| False Positives | Conservatism around lookalike APIs, explicit configuration, custom construction/conversion, naming conventions, intentional usage, and ambiguous AutoMapper behavior. |
| Fix Strategy | Safety, completeness, idempotence, diagnostic-specific routing, Fix All behavior, and whether generated code is executable rather than placeholder-heavy. |
| Tests | Strength of analyzer, fixer, negative, edge-case, reverse-map, conflict/ownership, helper, and regression tests. |
| Docs/Samples | Clarity and consistency of rule docs, samples, metadata, documented safe cases, severity accuracy, suppression guidance, and documented non-goals. |
| Importance | User-facing usefulness based on frequency, severity, runtime failure/data-loss risk, performance impact, and actionability. |

Harsh calibration notes:

- A `5` is rare. For a fixable rule it requires broad analyzer coverage plus dedicated fixer coverage and strong negative tests.
- Since all current rules have code fix providers, Fix Strategy is scored on the actual executable safety of the fixer, not just provider existence.
- Multi-diagnostic IDs are penalized when one ID mixes substantially different product concepts or severities without equally strong docs/tests for each branch.
- Info rules are scored by product value, not implementation effort. A healthy cleanup rule can still have low Importance.
- Docs/Samples score existence and quality. Broad docs and samples do not get a free `5` when descriptor severities, README tables, and implementation details drift.

Priority is a planning signal: `High` means the analyzer is important and has meaningful health gaps, `Medium` means useful follow-up work is warranted, and `Low` means no immediate work is needed.

## Scorecard

| Rule | Title | Domain | Severity | Analyzer | False Positives | Fix Strategy | Tests | Docs/Samples | Importance | Priority | Notes |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |
| AM001 | Property type mismatch | Type Safety | Error | 4 | 4 | 4 | 4 | 4 | 5 | Low | Strong semantic AutoMapper gating, ownership handoff to AM002/AM020/AM021, and solid fixer coverage; remaining gaps are mostly advanced conversion semantics and richer compile-time conversion modeling. |
| AM002 | Nullable compatibility issue | Type Safety | Error/Info | 4 | 4 | 4 | 5 | 4 | 5 | Low | Descriptor-accurate docs now call out the Error/Info split, and regression tests cover oblivious reference nullability plus nullable value types in disabled nullable contexts. Remaining opportunities are advanced generic/nullability-flow semantics. |
| AM003 | Collection type incompatibility | Type Safety | Error | 4 | 4 | 4 | 5 | 4 | 4 | Low | Now suppresses container diagnostics when the source collection is implicitly assignable to the destination contract, with regression coverage for array/interface and set/read-only interface shapes. Remaining opportunities are broader custom/immutable collection semantics. |
| AM004 | Source property has no corresponding destination property | Data Integrity | Warning | 4 | 4 | 4 | 5 | 5 | 5 | Low | One of the strongest rules: reverse maps, records, inheritance, flattening, ctor params, custom construction, and fixer behavior have extensive coverage. Rule docs now match shipped Warning severity/category metadata and are protected by catalog severity drift tests. |
| AM005 | Property names differ only in casing | Data Integrity | Warning | 4 | 4 | 4 | 4 | 4 | 3 | Low | Focused and reasonably conservative with explicit mapping, source ignore, reverse-map, and executable fixer tests; rule docs now match shipped Warning severity/category metadata and the recommended warning configuration. |
| AM006 | Destination property is not mapped | Data Integrity | Info | 4 | 4 | 4 | 4 | 4 | 4 | Low | Solid non-required counterpart to AM011 with flattening, reverse-map, ForPath, lookalike API, fuzzy-match, and bulk-ignore coverage; analyzer test count is lighter than AM004 but the risk is lower. |
| AM011 | Required destination property is not mapped | Data Integrity | Error | 4 | 5 | 3 | 5 | 5 | 5 | Low | Important runtime-failure guardrail with required-member, reverse-map, constructor, custom-construction, and direct/nested ForPath coverage; fixer default/ignore actions are now documented as manual-review scaffolds. |
| AM020 | Nested object mapping configuration missing | Complex Mappings | Warning | 4 | 4 | 4 | 5 | 5 | 5 | Low | The reference example in this repo: broad tests cover separate profiles, reverse maps, inheritance, records, interfaces, internal members, ForPath/string paths, and construction/conversion suppression. |
| AM021 | Collection element type incompatibility | Complex Mappings | Warning | 4 | 4 | 4 | 4 | 4 | 4 | Low | Good AM003 boundary discipline and recent fixer hardening for case-only names plus queue/stack output shape; keep expanding dictionary/custom-collection and reverse-map edge cases. |
| AM022 | Infinite recursion risk | Complex Mappings | Warning | 4 | 4 | 4 | 5 | 5 | 4 | Low | Recursion diagnostics now require convention-mapped paths plus configured nested `CreateMap` chains for indirect cycles, and suppress forward `MaxDepth`, `PreserveReferences`, `ConvertUsing`, ignores, collections, and reverse-map boundaries. Remaining risk is the intentionally heuristic nature of graph analysis. |
| AM030 | Custom type converter issues | Custom Conversions | Error/Warning/Info | 3 | 4 | 4 | 4 | 4 | 3 | Low | Nullable-source converter fixes now insert fully qualified null guards without adding or reordering `using System`, with coverage for existing/global usings, file-scoped namespaces, expression-bodied converters, and multi-diagnostic fixes. Remaining opportunities are mostly external DI/service-provider wiring and the mixed-concept shape of the single AM030 ID. |
| AM031 | Performance warnings in mapping expressions | Performance | Warning/Info | 4 | 4 | 4 | 5 | 4 | 4 | Low | Multiple-enumeration diagnostics now normalize source-rooted collection paths, cache rewrites support nested source collections, unsafe captured-collection cache actions are suppressed, and Task-valued source-property `.Result` is covered. Remaining risk is mainly the intentionally heuristic nature of broad performance smells. |
| AM041 | Duplicate mapping registration | Configuration | Warning | 4 | 4 | 4 | 4 | 4 | 4 | Low | Strong compilation-wide registry, reverse-map awareness, cross-profile duplicate detection, and removal fixer coverage. Remaining risk is mainly nuanced intentional override/configuration ordering cases. |
| AM050 | Redundant MapFrom configuration | Configuration | Info | 3 | 3 | 4 | 3 | 3 | 2 | Low | Safe cleanup rule with idempotent fixer tests, but product importance is low and analyzer semantics are intentionally narrow around direct same-name property lambdas. |

## Planning Shortlist

The next improvement batch should focus on rules where user impact and health gaps overlap:

| Priority | Rules | Work |
| --- | --- | --- |
| High | None | No implemented rule currently looks both high-impact and unhealthy enough to demand urgent intervention before normal release work. |
| Medium | None | No medium-priority rule remains after the AM022 recursion-boundary pass. |
| Low | AM001, AM002, AM003, AM004, AM005, AM006, AM011, AM020, AM021, AM022, AM030, AM031, AM041, AM050 | Treat as currently acceptable, reference examples, or low-impact cleanup rules. Improve opportunistically when touching nearby code. |

## Cross-Cutting Findings

- Public docs are useful and now have a severity drift guard for rule documentation. `AM004`, `AM005`, and `AM031` rule-doc severity text matches shipped descriptor metadata, while README/package version references remain covered by existing trust tests.
- Analyzer ownership is a real strength. The conflict tests and shared helpers make `AM001`/`AM002`/`AM003`/`AM020`/`AM021` boundaries much healthier than a file-count audit would suggest.
- The project now has a checked-in `RuleCatalog` health contract plus generated `docs/RULE_CATALOG.md` and sample diagnostic snapshots that tie rule IDs to descriptors, fixers, docs anchors, sample paths, and fixer trust levels.
- Diagnostic placement is generally at the mapping invocation or mapping lambda, not always the precise property/member token. That is acceptable for many AutoMapper configuration rules, but high-volume rules benefit from tighter placement when practical.
- Several fixers intentionally produce advisory/default mapping scaffolds. That is fine, and docs/code-action titles now distinguish "safe executable rewrite" from "starter mapping the developer must review."

## Verification Baseline

Architecture-style coverage currently comes from analyzer/fixer tests, conflict ownership tests, helper tests, sample projects, documentation, the checked-in `RuleCatalog`, generated trust artifacts, package smoke tests, and the deterministic `tools/AnalyzerVerifier` checks.

Current local verification:

- `/opt/homebrew/bin/dotnet test automapper-analyser.sln --no-restore --framework net10.0` passed: 672 passed, 0 skipped, 0 failed.
- The trust-first pass removed active skipped tests, added drift validation, and moved intentional analyzer-test warnings into an explicit test-project warning baseline.
- `/opt/homebrew/bin/dotnet --list-runtimes` shows only .NET 10 runtimes in this local environment, so broader runtime verification remains blocked by missing .NET 8 and .NET 9 runtimes.

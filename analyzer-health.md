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
| AM004 | Source property has no corresponding destination property | Data Integrity | Warning | 4 | 4 | 4 | 5 | 4 | 5 | Low | One of the strongest rules: reverse maps, records, inheritance, flattening, ctor params, custom construction, and fixer behavior have extensive coverage. Keep docs aligned because README/rule docs still imply older severity language in spots. |
| AM005 | Property names differ only in casing | Data Integrity | Warning | 4 | 4 | 4 | 4 | 3 | 3 | Low | Focused and reasonably conservative with explicit mapping, source ignore, reverse-map, and executable fixer tests; docs/samples understate current Warning severity and should better explain intentional naming policies. |
| AM006 | Destination property is not mapped | Data Integrity | Info | 4 | 4 | 4 | 4 | 4 | 4 | Low | Solid non-required counterpart to AM011 with flattening, reverse-map, ForPath, lookalike API, fuzzy-match, and bulk-ignore coverage; analyzer test count is lighter than AM004 but the risk is lower. |
| AM011 | Required destination property is not mapped | Data Integrity | Error | 4 | 4 | 3 | 4 | 4 | 5 | Medium | Important runtime-failure guardrail with good required-member and reverse-map coverage; fixer still leans on default/constant/custom mapping scaffolds, so domain-safe mapping remains partly manual. |
| AM020 | Nested object mapping configuration missing | Complex Mappings | Warning | 4 | 4 | 4 | 5 | 5 | 5 | Low | The reference example in this repo: broad tests cover separate profiles, reverse maps, inheritance, records, interfaces, internal members, ForPath/string paths, and construction/conversion suppression. |
| AM021 | Collection element type incompatibility | Complex Mappings | Warning | 4 | 4 | 4 | 4 | 4 | 4 | Low | Good AM003 boundary discipline and recent fixer hardening for case-only names plus queue/stack output shape; keep expanding dictionary/custom-collection and reverse-map edge cases. |
| AM022 | Infinite recursion risk | Complex Mappings | Warning | 3 | 3 | 4 | 4 | 4 | 4 | Medium | Useful and well tested for common cycles, MaxDepth, Ignore, collections, and reverse-map boundaries, but recursion analysis remains heuristic and may miss or over-report nuanced graph/DTO ownership patterns. |
| AM030 | Custom type converter issues | Custom Conversions | Error/Warning/Info | 3 | 3 | 3 | 3 | 3 | 3 | Medium | The rule now focuses on converter implementation quality, null handling, and unused converters, but the single ID mixes distinct concepts and unused-converter analysis can be noisy when converters are wired externally. |
| AM031 | Performance warnings in mapping expressions | Performance | Warning/Info | 3 | 3 | 3 | 4 | 3 | 4 | Medium | Broad, valuable smell detector with many targeted tests, but expensive-operation heuristics are inherently fuzzy and fixer safety varies by issue type; docs should make the supported boundaries more explicit. |
| AM041 | Duplicate mapping registration | Configuration | Warning | 4 | 4 | 4 | 4 | 4 | 4 | Low | Strong compilation-wide registry, reverse-map awareness, cross-profile duplicate detection, and removal fixer coverage. Remaining risk is mainly nuanced intentional override/configuration ordering cases. |
| AM050 | Redundant MapFrom configuration | Configuration | Info | 3 | 3 | 4 | 3 | 3 | 2 | Low | Safe cleanup rule with idempotent fixer tests, but product importance is low and analyzer semantics are intentionally narrow around direct same-name property lambdas. |

## Planning Shortlist

The next improvement batch should focus on rules where user impact and health gaps overlap:

| Priority | Rules | Work |
| --- | --- | --- |
| High | None | No implemented rule currently looks both high-impact and unhealthy enough to demand urgent intervention before normal release work. |
| Medium | AM011, AM022, AM030, AM031 | Expand required-member/converter/performance boundary tests, and make manual-vs-executable fixer expectations explicit in docs. |
| Low | AM001, AM002, AM003, AM004, AM005, AM006, AM020, AM021, AM041, AM050 | Treat as currently acceptable, reference examples, or low-impact cleanup rules. Improve opportunistically when touching nearby code. |

## Cross-Cutting Findings

- Public docs are useful but drift from implementation in several places. `AM004` and `AM005` remain obvious severity/wording mismatches between descriptors, README tables, and rule docs, while `AM002` has been realigned with its shipped Error/Info descriptors.
- Analyzer ownership is a real strength. The conflict tests and shared helpers make `AM001`/`AM002`/`AM003`/`AM020`/`AM021` boundaries much healthier than a file-count audit would suggest.
- The project has no generated rule catalog health contract like LinqContraband's catalog tooling. Rule metadata is distributed across descriptors, README, docs, samples, and the manual `AnalyzerVerifier`.
- Diagnostic placement is generally at the mapping invocation or mapping lambda, not always the precise property/member token. That is acceptable for many AutoMapper configuration rules, but high-volume rules benefit from tighter placement when practical.
- Several fixers intentionally produce advisory/default mapping scaffolds. That is fine, but docs should distinguish "safe executable rewrite" from "starter mapping the developer must review."

## Verification Baseline

Architecture-style coverage currently comes from analyzer/fixer tests, conflict ownership tests, helper tests, sample projects, documentation, and the manual `tools/AnalyzerVerifier` project. There is no checked-in rule-catalog generator or sample diagnostics verifier equivalent to the LinqContraband baseline.

Current local verification:

- `/opt/homebrew/bin/dotnet test automapper-analyser.sln --no-restore --framework net10.0` passed: 638 passed, 8 skipped, 0 failed.
- The restore/test run reported existing warnings worth tracking: AutoMapper 14.0.0 has advisory `GHSA-rvv3-g6hj-g44x`, several analyzer packaging/test-infrastructure warnings are present, and skipped-test warnings cover AM001, AM031, and AM050 scenarios.
- `/opt/homebrew/bin/dotnet --list-runtimes` shows only .NET 10 runtimes in this local environment, so broader runtime verification remains blocked by missing .NET 8 and .NET 9 runtimes.

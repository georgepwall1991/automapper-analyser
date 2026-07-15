# Analyzer Health

Reviewed: 2026-07-15 (performance `ForPath` fixer parity prepared as **2.30.67**)

This is a deliberately harsh health audit for the **21** implemented AutoMapper analyzer rule IDs in this repository (16 before the 2.30.62 performance split). Several rule IDs still expose multiple diagnostic descriptors, especially `AM002` and `AM022`; the scorecard rates the public rule ID as the user experiences it.

Every implemented rule currently has an analyzer and a code fix provider. Scores are 1-5, where `5` means reference-quality and hard to improve, `3` means usable but meaningfully incomplete, and `1` means unreliable or underbuilt.

**Ship status:** production-acceptable. **2.30.67** performance `ForPath` Ignore scaffolds; **2.30.66** AM022 downstream cycle breakers; **2.30.65** AM004/AM006 sibling recompute; **2.30.64** AM001 property tokens. Every rule now has minimum health 4. Full suite green; catalog/snapshots current.

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
| AM001 | Property type mismatch | Type Safety | Error | 4 | 4 | 5 | 5 | 4 | 5 | Low | Direction-preserving ReverseMap keys; collection-only generic deferral; fixer peels nullables, invariant-culture Parse/ToString, keywords, DateTime/Uri/bool/Guid. **Fix Strategy 5**: Convert-all/Ignore-all + nested Fix individual. **2.30.64**: destination property-token placement + MappingInvocation metadata + sibling recompute for aggregates. |
| AM002 | Nullable compatibility issue | Type Safety | Error/Info | 4 | 4 | 4 | 5 | 4 | 5 | Low | Descriptor-accurate docs now call out the Error/Info split, reverse-map nullable-to-non-nullable losses report and fix even when forward-side nullability policy is configured before `ReverseMap()`, pass-through and different-member nullable `MapFrom` bodies no longer hide nullable-to-non-nullable errors even when the same-name source member is non-nullable, diagnostics name the actual nullable source member for explicit different-source mappings, constructed generic source/destination labels such as `Source<T>` and `Destination<T>` are preserved, null-forgiving-only generic `MapFrom` bodies now report instead of treating compiler suppression as runtime null handling, semantic string literal/`nameof(...)`/const destination-member selectors and typed-lambda safe mappings are recognized as explicit nullability configuration, helper methods inside member options no longer masquerade as AutoMapper null handlers or mapping configuration, unsafe `NullSubstitute` values still report while assignable fallback values and typed value-type defaults are respected, unguarded nullable receiver dereferences inside `MapFrom` still report even when the final mapped value has a different type, while guarded dereferences and nullable value `GetValueOrDefault()` stay quiet, member-level converter/resolver ownership is respected including generic member-resolver `MapFrom<TResolver, TSourceMember>(...)` forms while generic expression `MapFrom<TSourceMember>(...)` overloads remain analyzable, top-level `ForPath` including typed-lambda paths respects null handling while child-only `ForPath` does not suppress parent nullability, repeated destination-member configuration uses the later effective mapping, the fixer preserves existing member options/source expressions and emits fully qualified framework defaults or `default!` for generic/reference fallback defaults when adding defaults without appending behind `Condition`/`PreCondition`, and catalog trust now marks the Info descriptor as analyzer-only. Remaining opportunities are advanced generic/nullability-flow semantics. |
| AM003 | Collection type incompatibility | Type Safety | Error | 4 | 4 | 4 | 5 | 4 | 4 | Low | Shared container-incompatibility predicate with AM021 (HashSet/Queue/Stack/SortedSet/LinkedList/Immutable*/FrozenSet). Combined container+element mismatches report AM003 only; CreateRange fixes still convert elements when a named conversion exists. Sample isolates same-element List→HashSet. Custom-collection edge cases remain. |
| AM004 | Source property has no corresponding destination property | Data Integrity | Warning | 4 | 4 | 5 | 5 | 5 | 5 | Low | Unique-best fuzzy, reverse-map, property-token placement. **Fix Strategy 5 (2.30.65)**: same-document sibling recompute offers Map-all/DoNotValidate-all from one property-token caret. Catalog Scaffold remains accurate. |
| AM005 | Property names differ only in casing | Data Integrity | Warning | 4 | 4 | 4 | 4 | 4 | 3 | Low | Focused and reasonably conservative with explicit mapping including direct string literal/`nameof(...)`/const destination-member forms and typed-lambda selectors including typed top-level `ForPath`, semantic string/`nameof(...)` source-member ignores, custom construction/conversion suppression, reverse-map, source-property diagnostic placement including positional-record source parameters, metadata-backed fixer routing from property-token diagnostics, executable fixer tests, and rule-prefixed code-action equivalence keys; rule docs now match shipped Warning severity/category metadata and the recommended warning configuration. |
| AM006 | Destination property is not mapped | Data Integrity | Info | 4 | 4 | 5 | 5 | 4 | 4 | Low | Non-required counterpart to AM011. **Fix Strategy 5 (2.30.65)**: same-document sibling recompute for Map-all/Ignore-all from one property-token caret. Shared unmapped-destination helper with analyzer. |
| AM011 | Required destination property is not mapped | Data Integrity | Error | 4 | 5 | 4 | 5 | 5 | 5 | Low | Important runtime-failure guardrail; per-property fuzzy now resolves ReverseMap types (same as aggregate). Fix Strategy 4: Map-all only when every property has unique fuzzy; mixed/default bulk uses honest Scaffold-all + (manual review); Ignore-all labeled manual review; stale diagnostics withhold. Residual: primary single-property path still scaffolds defaults. |
| AM020 | Nested object mapping configuration missing | Complex Mappings | Warning | 4 | 4 | 4 | 5 | 5 | 5 | Low | Reference analyzer; fixer now shares public+internal property discovery with the analyzer (internal nested CreateMap fix covered). Catalog trust is `LikelyRewrite` (constructor-body insertion remains a structural limit). |
| AM021 | Collection element type incompatibility | Complex Mappings | Warning | 4 | 4 | 4 | 5 | 4 | 4 | Low | Defers fully when AM003 owns container incompatibility (shared helper). Same-container element mismatches, dictionary axes, reverse gaps, and implicit element conversions remain AM021. List simple Select rewrites now refuse non-compiling Parse destinations (string-source gate matches dictionaries). |
| AM022 | Infinite recursion risk | Complex Mappings | Warning | 4 | 4 | 4 | 5 | 4 | 4 | Low | Requires configured nested CreateMap chains for indirect cycles; strong semantic suppressions. **2.30.66**: direction-aware graph traversal stops at downstream `MaxDepth`, `PreserveReferences`, and `ConvertUsing` maps, with all-registrations protection for ambiguous duplicates. Catalog `Scaffold` matches hard-coded `MaxDepth(2)` policy; graph-edge Ignore remains manual review. |
| AM030 | Invalid type converter implementation | Custom Conversions | Error | 4 | 4 | 4 | 4 | 4 | 3 | Low | Analyzer-only by design; AM001/AM020/AM021 own missing-converter mapping. Split from AM032/AM033 is clean in catalog and trust tests. Direct AM030 coverage includes empty implementation, wrong return type, missing `ResolutionContext`, and non-public `Convert` (each paired with the matching compiler error). |
| AM032 | Type converter null handling | Custom Conversions | Warning | 4 | 4 | 4 | 5 | 4 | 4 | Low | Best-in-class null-guard detection with catalog `LikelyRewrite` trust. Recognizes `ThrowIfNull*`, switch/pattern/coalesce/conditional-access guards, and pure nullable→nullable source pass-through (`return source` / `=> source`). Residual: heuristic null-flow edge cases; the auto-fix still inserts `ArgumentNullException` (appropriate when a diagnostic fires for non-nullable destinations without intentional null handling). |
| AM033 | Unused type converter | Custom Conversions | Info | 4 | 4 | 4 | 4 | 4 | 2 | Low | Unused-converter diagnostics now have their own Info rule ID and remain analyzer-only, and the shared converter code-fix provider no longer advertises AM033 as fixable. Usage analysis recognizes generic, instance, type-based including parenthesized/casted `typeof(...)` and simple explicit or implicit `Type` locals/fields/properties initialized from, expression-bodied to, or getter-bodied to `typeof(...)`, parameterless `Type` factory methods and local functions that expression-body or return `typeof(...)`, simple interface-typed locals/fields/properties, and DI/service-provider interface handles passed to `ConvertUsing(...)`. Product importance is intentionally lower because this is cleanup guidance. |
| AM031 | Multiple enumeration | Performance | Warning | 4 | 4 | 4 | 5 | 4 | 4 | Low | Single-concept after 2.30.62 ID split (was multi-descriptor umbrella). **2.30.67**: ForMember keeps cache/Remove/Ignore actions; ForPath now offers an executable Ignore scaffold while cache and convention removal remain conservatively withheld. Scaffold trust. |
| AM034 | Expensive operation in mapping | Performance | Warning | 4 | 4 | 4 | 5 | 4 | 4 | Low | Split from AM031 in 2.30.62. **2.30.67**: shared fixer offers Ignore on ForMember and ForPath, plus convention-safe Remove on ForMember. Residual: heuristic smell catalog breadth. |
| AM035 | Expensive computation in mapping | Performance | Warning | 4 | 4 | 4 | 4 | 4 | 3 | Low | Split from AM031 in 2.30.62. Shared ForPath Ignore scaffold; narrower surface than AM034. |
| AM036 | Sync-over-async in mapping | Performance | Warning | 4 | 4 | 4 | 5 | 4 | 4 | Low | Split from AM031 in 2.30.62. Task.Result/Wait/WaitAll/WaitAny/GetResult including source Task properties; shared ForPath Ignore scaffold. |
| AM037 | Complex LINQ in mapping | Performance | Warning | 4 | 4 | 4 | 4 | 4 | 3 | Low | Split from AM031 in 2.30.62. Real Enumerable/Queryable SelectMany gate; shared ForPath Ignore scaffold. |
| AM038 | Non-deterministic operation in mapping | Performance | Info | 4 | 4 | 4 | 5 | 4 | 3 | Low | Split from AM031 in 2.30.62. Info severity; ForMember/ForPath scaffold fixes. |
| AM041 | Duplicate mapping registration | Configuration | Warning | 4 | 4 | 4 | 4 | 4 | 4 | Low | Compilation-wide registry now peels parentheses in `GetReverseMapInvocation`, so `(CreateMap<S,D>()).ReverseMap()` registers reverse duplicates. SafeRewrite removal withholds chained/nested/arg-position config. Remaining risk is nuanced intentional override ordering. |
| AM050 | Redundant MapFrom configuration | Configuration | Info | 4 | 4 | 4 | 4 | 4 | 2 | Low | Safe cleanup rule now requires proven source/destination type compatibility including nullable reference annotations, string literal and `nameof(...)`/constant `ForMember` members resolved through the effective mapping direction including `ReverseMap()` segments with direct reverse-map `nameof(...)`/const analyzer and code-fix coverage, parenthesized/typed lambda parameter shapes — `o.MapFrom((s) => s.Name)` and `o.MapFrom((Source s) => s.Name)` — with `ForMember`/`ForPath` code-fix parity, and parenthesized member bodies such as `s => (s.Name)`/`d => (d.Name)` alongside the simple `s => s.Name` shape. Multi-parameter parenthesized lambdas are intentionally ignored so AutoMapper's `(src, ctx) => ...` overload stays quiet. The automatic `ForMember`/`ForPath` removal code fix now withholds when the options lambda carries sibling configuration (`Condition`, `NullSubstitute`, `PreCondition`, `UseDestinationValue`, `Ignore`, etc.) so policy overrides cannot be silently dropped; simple-MapFrom shapes still receive the automatic action. Product importance remains low. |

## Planning Shortlist

The next improvement batch should focus on rules where user impact and health gaps overlap:

| Priority | Rules | Work |
| --- | --- | --- |
| High | None | No release-blocking gaps after 2.30.57–2.30.61 (audit ownership/safety + AM001 correctness + fixer UX honesty/polish). |
| Medium | None | Closed in 2.30.57–2.30.61: AM003/AM021 ownership, AM020 fixer parity, AM021 Parse gate, AM041 paren reverse, AM011 reverse fuzzy / honest Map-all, AM001 ReverseMap+Nullable+Convert-all, AM022 MaxDepth titles/order, AM031 multi-enum + lightbulb order, catalog trust honesty. |
| Low | See Working Base | Opportunistic residuals only — ranked table below. |

## Working Base — Open Residuals (ranked)

Use this table as the hardening queue. Ranking = **Importance × (5 − min(Analyzer, FP, Fix, Tests, Docs))** from the scorecard (higher = better next target). Only items with concrete code residual or multi-concept product tax are listed; no speculative score moves.

| Rank | Rule | Signal | Residual (evidence-backed) | Likely score move if closed |
| --- | --- | --- | --- | --- |
| 1 | **AM011** | gap=5, min=4 | Primary single-property path still scaffolds defaults when fuzzy fails. | Fix stays 4 until less scaffold-heavy without lying |
| 2 | **AM032** | gap=4, min=4 | Classic net48-safe if-throw; not destination-aware. | Fix 4→5 if destination-aware policies are safe |
| 3 | **AM022** | gap=4, min=4 | Downstream global cycle breakers shipped 2.30.66. Member-level graph/dataflow refinements remain opportunistic. | FP 4→5 needs evidence-backed dataflow precision |
| 4 | **AM001** | gap=4, min=4 | Property-token placement shipped 2.30.64. Residual: advanced conversion modelling only with user repros. | — |
| 5 | **AM002** | gap=5, min=4 | Advanced generic/nullability-flow only — wait for real user FP/FN. | — until evidence |
| 6 | **AM020 / AM021 / AM003** | gap=5/4 | Custom-collection / constructor-body insert structural limits. | Opportunistic |
| 7 | **AM041 / AM050** | gap=4/2 | SafeRewrite nuance / low Importance. | Low ROI |
| 8 | **AM033 / AM005 / AM030** | gap=2–3 | Importance-limited. | Product priority only |

**Recommended next hardening batch:**

1. **AM011 less scaffold-heavy single-property fixes**
2. **AM032 destination-aware null fix**
3. **AM022 member-level graph/dataflow precision only with a concrete repro**

Do **not** open advanced AM001/AM002 conversion/nullability modelling without a filed false-positive/false-negative repro.

## Prioritized Fix Backlog

The Planning Shortlist above summarises overall rule priority; this backlog is the concrete punch list of specific items surfaced during the 2026-05-14 re-review and the 2026-06-13 (v2.30.54) hardening pass. Grading: **P0** = release-blocking; **P1** = fix in the next health pass; **P2** = opportunistic when adjacent work is open; **P3** = directional, longer-horizon.

### P0 — Release-blocking

- _None._ No rule currently has a correctness regression or user-visible defect severe enough to block a release.

### P1 — Fix in the next health pass

- _None._ The 2026-05-14 P1 entries (AM030 dead descriptor, AM002 category drift in docs) have all been resolved in 2.30.24 and 2.30.25 respectively. P2/P3 items below remain.

### P2 — Opportunistic open items (working base)

Active queue (also summarized in Working Base). Prefer one focus per hardening batch:

- _AM031 ID split — resolved in 2.30.62._ AM031 multi-enum; AM034–AM038 independent IDs.
- _AM022 graph-aware Ignore — resolved in 2.30.63._ Multi-type cycle edges offered for Ignore.
- _AM022 downstream cycle-breaker precision — resolved in 2.30.66._ Multi-map traversal honours direction-specific `MaxDepth`, `PreserveReferences`, and `ConvertUsing`; False Positives 3→4.
- _AM004 / AM006 same-document sibling recompute — resolved in 2.30.65._
- _AM031 / AM034–AM038 `ForPath` fixer parity — resolved in 2.30.67._ Nested paths receive an executable Ignore scaffold; caching and convention removal remain safely withheld.
- **AM032 destination-aware null fixes.** Emit stays classic net48 `if-throw`; not destination-nullability-aware. Analyzer ThrowIfNull* recognition is already strong.

Resolved historical P2 (keep for audit trail):

- _AM003 / AM004 docs drift — resolved in hitlist #2._
- _AM001↔AM002 joint conflict test — resolved in hitlist #3._
- _AM030 signature-depth tests — resolved in hitlist #4._
- _AM004 unique-best fuzzy gate — resolved in hitlist #1._
- _AM032 nullable pass-through — resolved in hitlist #5._
- _AM050 sibling-config direct coverage — resolved in 2.30.26._
- _AM041 `ForPath` chained-config test — resolved in 2.30.27._
- _AM031 remove-`ForMember` safety — resolved in 2.30.28._
- _AM004 / AM005 / AM006 / AM011 diagnostic placement — resolved in 2.30.32._
- _AM001 property-token diagnostic placement — resolved in 2.30.64._
- _AM011 honest Map-all / Scaffold-all — resolved in 2.30.59._
- _AM001 Convert-all / Ignore-all multi-property UX — resolved in 2.30.60._
- _AM022 MaxDepth title/order honesty — resolved in 2.30.59–2.30.61._

### P3 — Directional, longer-horizon

Resolved historical P3 (audit trail):

- _Calibrate AM021 / AM022 Tests scores — resolved in 2.30.34._
- _Split AM030 into separate IDs — resolved in 2.30.35._
- _AM003 immutable/frozen container fixes — resolved in 2.30.33._
- _AM002 generic type-parameter labels/defaults — resolved in 2.30.36._
- _AM001 exact numeric conversion modelling — resolved in 2.30.37._
- _AM031 LINQ namesake precision — resolved in 2.30.38._
- _AM001 implicit conversion modelling — resolved in 2.30.40._
- _AM021 implicit element conversion modelling — resolved in 2.30.41._
- _AM020 implicit nested conversion modelling — resolved in 2.30.42._

Still open (do not start without a repro or explicit product decision):

- **AM002 generic / nullability flow semantics.** Advanced generic/nullability-flow only after a real user FP/FN class.
- **AM001 explicit/domain conversion guidance.** Predefined + compiler-known implicits are done; domain/explicit guidance needs concrete cases.
- **AM022 / AM031 dataflow-grade heuristics.** High effort, diminishing returns until a concrete false-positive pattern is filed (prefer P2 ID-split / graph-Ignore first).
- **AM020 constructor-body CreateMap insert.** Structural host limit; catalog `LikelyRewrite` already honest.


## Reanalysis Changelog (2026-07-15 → 2.30.67 performance `ForPath` fixer parity)

| Rules | Finding | Change | Score impact |
| --- | --- | --- | --- |
| AM031 / AM034–AM038 | Nested `ForPath` diagnostics had no executable remediation even though AutoMapper path options support Ignore | Preserve the original nested selector and options parameter while replacing only `MapFrom` with an Ignore scaffold; cache and Remove stay ForMember-only | Fix Strategy 3→4 |

## Reanalysis Changelog (2026-07-13 → 2.30.66 AM022 downstream cycle breakers)

| Rules | Finding | Change | Score impact |
| --- | --- | --- | --- |
| AM022 | Root maps still warned when a downstream map already bounded or owned recursion | Direction-aware registry metadata stops traversal at semantic `MaxDepth`, `PreserveReferences`, and `ConvertUsing` edges, including direct same-block local configuration; ambiguous duplicates require every registration to be constrained | False Positives 3→4 |

## Reanalysis Changelog (2026-07-08 → 2.30.65 AM004/AM006 sibling recompute)

| Rules | Finding | Change | Score impact |
| --- | --- | --- | --- |
| AM004/AM006 | Aggregates required multi-diag pile-up for same-document property tokens | AM011-style live unmapped-set recompute from one caret | Fix Strategy 4→5 |

## Reanalysis Changelog (2026-07-08 → 2.30.64 AM001 property-token placement)

| Rules | Finding | Change | Score impact |
| --- | --- | --- | --- |
| AM001 | Diagnostics on CreateMap invocation | Destination property-token location + MappingInvocation metadata; fixer sibling recompute for aggregates | Placement residual closed |

## Reanalysis Changelog (2026-07-08 → 2.30.63 AM022 graph-aware Ignore)

| Rules | Finding | Change | Score impact |
| --- | --- | --- | --- |
| AM022 | Multi-type cycles MaxDepth-only (Ignore was dest self-ref only) | Fixer reuses `FindRecursiveDestinationProperties` for cycle-edge Ignore | Fix Strategy 3→4 |

## Reanalysis Changelog (2026-07-08 → 2.30.62 AM031 ID split)

| Rules | Finding | Change | Score impact |
| --- | --- | --- | --- |
| AM031/AM034–AM038 | Six concepts shared public ID AM031 (Analyzer/Docs multi-concept tax) | Independent IDs: AM031 multi-enum; AM034 expensive op; AM035 computation; AM036 sync-over-async; AM037 complex LINQ; AM038 non-deterministic | AM031 Analyzer/Docs 3→4; five new scorecard rows |

## Reanalysis Changelog (2026-07-08 monitor re-eval @ 2.30.61)

Evidence-only monitor re-eval of shipped `2.30.61` @ `7b419dc` (no analyzer source changes in this pass).

| Rules | Finding | Change | Score impact |
| --- | --- | --- | --- |
| Monitor | Verification baseline still cited 2.30.55 / 1352 tests; shortlist frozen at 2.30.57 while 2.30.58–2.30.61 shipped | Refreshed verification (1381 green), Working Base residual table, P2 open queue | No score changes |
| AM031 / AM022 | Still the only min-health-3 rules (gap=8) | Ranked as next-batch recommendations | Confirmed, not rescaled |
| AM001 | Fix Strategy 5 + Convert-all remains accurate post-2.30.60; residual is invocation placement | Notes/shortlist already match; placement stays open P2 | No score changes |
| AM032 | Batch 3 tried ThrowIfNull emit then reverted to net48 classic if-throw | Trust summary already correct; destination-aware fix still open | No score changes |

## Reanalysis Changelog (2026-07-08 full audit → 2.30.57; then UX batches → 2.30.61)

Full rule+fixer reanalysis driven by four parallel subagent audits (Type Safety, Data Integrity, Complex Mappings+Converters, Config/Performance) against v2.30.56, with implementation of the resulting hitlist, then same-day fixer UX batches 2.30.58–2.30.61.

| Rules | Finding | Change | Score impact |
| --- | --- | --- | --- |
| AM003/AM021 | Immutable/frozen container deferral drifted → double-report | Shared `AreCollectionTypesIncompatible` + ownership tests | Ownership restored; notes updated |
| AM020 | Fixer public-only vs analyzer public+internal → silent no-op | Shared helpers + internal nested fix test | Catalog SafeRewrite→LikelyRewrite |
| AM021 | List Parse from non-string could not compile | `IsSafeAxisConversion` on simple Select path | Fix safety improved |
| AM041 | Parenthesized ReverseMap not registered | Peel parens in `GetReverseMapInvocation` | Analyzer FN closed |
| AM011 | Reverse per-property fuzzy used forward-only types | `ResolveCreateMapTypesWithReverse` | Reverse fuzzy works |
| AM031 | Multi-enum de-dupe dropped second collection; fake docs IDs | Report all keys; delete AM031.00x docs | Analyzer/Docs 4→3 (multi-concept tax) |
| AM001/AM022/AM004/AM006/AM050 | Docs/trust honesty gaps at audit time | Parse docs, MaxDepth(2), aggregate docs, Scaffold catalog | Intermediate Fix dips later recovered for AM001 (→5 in 2.30.60 Convert-all); AM022 Fix stays 3 Scaffold |
| AM001 (2.30.58) | ReverseMap key + Nullable false negatives; weak conversion emit | Direction-preserving keys; collection-only generic deferral; invariant Parse/ToString recipes | Notes updated; Fix path strengthened |
| Fixer UX (2.30.59–61) | Scaffold oversell / silent no-ops / lightbulb order | Honest titles, withhold unsafe inserts, AM001 Convert-all, AM031/AM022 order, AM003/AM021 keyword titles | AM001 Fix → 5; others honesty without score inflation |

## Reanalysis Changelog (2026-07-06)

Full rule+fixer reanalysis driven by four parallel subagent audits (Type Safety, Data Integrity, Complex Mappings, Configuration/Performance) against v2.30.55 at commit `b138fa8`.

| Rules | Finding | Change | Score impact |
| --- | --- | --- | --- |
| Monitor | All 16 rules remain production-acceptable; 1352 tests green; catalog/snapshot verifier up to date. | Refreshed review date, verification baseline, and scorecard notes. | No release-blocking gaps. |
| AM003 | `TypeSafetyExamples` AM003 sample is also an AM021 element mismatch. | Docs/Samples 4→3; tightened Notes. | AM003 Docs/Samples −1 |
| AM004 | Rule docs omit aggregate/nested fixer UX and property-token placement. | Docs/Samples 5→4; tightened Notes. | AM004 Docs/Samples −1 |
| AM022 | Graph analysis is inherently heuristic despite 53 tests. | False Positives 4→3; tightened Notes. | AM022 False Positives −1 |
| AM030 | ~5 direct tests in shared 146-test bucket. | Tests 4→3; tightened Notes. | AM030 Tests −1 |
| AM032 | Null-flow heuristic + throw-only fix policy risk. | False Positives 4→3; tightened Notes. | AM032 False Positives −1 |

## Fixer Trust Summary (v2.30.67)

| Rule | Fixable? | Catalog trust | Notes |
| --- | --- | --- | --- |
| AM001 | Yes | LikelyRewrite | Conversion fixes when pattern known (`Parse`); ignore-only for speculative conversions |
| AM002 | Partial | Scaffold | Error descriptor only; Info (`NonNullableToNullable`) is analyzer-only |
| AM003 | Yes | LikelyRewrite | Executable container rewrites including element Select for immutable destinations |
| AM004–AM006, AM011 | Yes | Scaffold / LikelyRewrite | Aggregate + nested submenu for 2+ properties (AM004/006/011) |
| AM005 | Yes | LikelyRewrite | Single explicit `ForMember` rewrite; keyword-escaped source |
| AM020 | Yes | LikelyRewrite | Adds missing `CreateMap<,>()` pairs; public+internal property parity |
| AM021 | Yes | LikelyRewrite | Executable `ToDictionary`/Select rewrites; Parse gated to string sources |
| AM022 | Yes | Scaffold | `MaxDepth(2)` + graph-aware Ignore on cycle edges (manual review) |
| AM030, AM033 | No | NoFix | Analyzer-only by design |
| AM032 | Yes | LikelyRewrite | Inserts `ArgumentNullException` guard |
| AM031 | Yes | Scaffold | Multiple enumeration; cache rewrite + Ignore/Remove on ForMember; Ignore scaffold on ForPath |
| AM034–AM038 | Yes | Scaffold | Shared performance fixer; ForMember Ignore/Remove scaffolds; ForPath Ignore scaffold |
| AM041, AM050 | Yes | SafeRewrite | Conservative removal/withhold for chained config |

## Cross-Cutting Findings

- A new `RuleCatalogTests.Analyzers_ShouldRegisterEveryDeclaredDiagnosticDescriptor` trust drift guard enforces a two-part contract: every `public static DiagnosticDescriptor` field on a shipped analyzer must appear in that analyzer's `SupportedDiagnostics` *or* be explicitly marked `[Obsolete]`, and no descriptor can be both registered and Obsolete.
- A new `RuleCatalogTests.RuleDocs_ShouldDocumentDescriptorCategories` trust drift guard asserts that the `**Category**:` line in each rule's `docs/DIAGNOSTIC_RULES.md` section names every distinct `descriptor.Category` for that rule. This mirrors the existing severity drift guard. The 2.30.25 pass that introduced the guard corrected six drifted rule-doc category lines: AM002 (`TypeSafety` → `NullSafety`), AM011 (`DataIntegrity` → `RequiredProperties`), AM020 (`ComplexMappings` → `NestedObjects`), AM021 (`ComplexMappings` → `Collections`), AM022 (`ComplexMappings` → `Recursion`), AM030 (`CustomConversions` → `Converters`). The descriptors themselves were unchanged; only the docs had drifted away from the shipped categories. The 2.30.24 pass that introduced the guard also marked two orphans it surfaced as `[Obsolete]`: `AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule` (the explicit P1 backlog item; AM001/AM020/AM021 already own the missing-converter ownership lane) and `AM003_CollectionTypeIncompatibilityAnalyzer.CollectionElementIncompatibilityRule` (same defect class; element ownership long since moved to AM021's identically named live descriptor). The descriptors are retained for binary compatibility, the Obsolete attribute makes the legacy intent explicit, and the drift guard prevents new relics from slipping in silently.
- Public docs are useful and now have a severity drift guard for rule documentation. `AM004`, `AM005`, and `AM031` rule-doc severity text matches shipped descriptor metadata, while README/package version references remain covered by existing trust tests.
- CI, release, and CodeQL workflows now use current major action pins with Node.js 24-compatible releases, removing the runtime deprecation warnings seen during the 2.30.38 release/CI run while preserving the existing build, package, release, and smoke-test behavior.
- AM001 fixer coverage now includes enum-to-string and null-guarded string-to-enum property mismatches, so the documented enum conversion scenario has an executable code action instead of only an ignore fallback.
- AM001 no longer offers a speculative reference-cast code action for unrelated reference/framework conversions such as `Uri` to `string`; those diagnostics keep the manual-review ignore action only.
- AM001 numeric compatibility now mirrors C# predefined implicit numeric conversions. `double`/`float` to `decimal` report and receive explicit cast fixes, same-width signed/unsigned pairs still report, and valid widenings such as `char` to `int` stay quiet.
- AM001 now also respects compiler-known implicit conversions from Roslyn conversion classification, including user-defined value-object conversions such as `Money` to `decimal`, while explicit-only conversions still report.
- AM001 reuses the shared namespace-aware built-in classifier when deciding whether a mismatch is a conversion problem or an AM020 nested-object problem. Actual framework scalars/value objects such as `System.DateOnly`, `System.TimeOnly`, and `System.Uri` mapped to domain types now stay in AM001, while user-defined same-short-name domain types still flow to AM020 when appropriate.
- AM002 now keeps the `Error` descriptor on the scaffold/default-value path while marking the non-nullable-to-nullable `Info` descriptor as analyzer-only in descriptor-aware trust metadata.
- AM002 now inspects explicit top-level `ForMember` and top-level `ForPath` bodies semantically: null-handling expressions such as coalescing, safe and assignable AutoMapper `NullSubstitute`, typed value-type defaults such as `default(int)`, guarded nullable dereferences, nullable value `GetValueOrDefault()`, AutoMapper `Ignore`, custom resolver/converter forms including generic member resolvers, and proven non-null-producing resolver expressions stay quiet, including when `ForMember` targets the destination by lambda, string literal, `nameof(...)`, or const string member name, while pass-through, null-forgiving-only generic source members, unguarded nullable-receiver dereferences even through different source members or different final return types, different-member nullable source mappings, generic expression `MapFrom<TSourceMember>(...)` overloads, unsafe `NullSubstitute(null/default)`, helper calls named `Ignore`/`NullSubstitute`/`MapFrom`, or child-only `ForPath` configuration still report based on the actual top-level mapped expression and name the actual nullable source member. Constructed generic map labels such as `Source<T>` and `Destination<T>` are preserved in diagnostics. Repeated destination-member configuration is evaluated and fixed from the later effective mapping. The default-value fixer also preserves existing member options and coalesces safe nullable source expressions with fully qualified framework defaults or `default!` for generic/reference fallback defaults when adding null handling without reusing child `ForPath` calls, shadowing the member-options lambda parameter, appending behind `Condition`/`PreCondition` guards, or offering unsafe null coalescing after nullable receiver dereferences.
- AM003 collection container fixes now preserve the AM003/AM021 ownership split while producing executable mappings for destination collection interfaces such as `IList<T>` and `ISet<T>`, concrete BCL containers such as `SortedSet<T>` and `LinkedList<T>`, immutable/frozen destination containers such as `ImmutableList<T>`, `ImmutableArray<T>`, `ImmutableHashSet<T>`, and `FrozenSet<T>`, and withholding automatic constructor rewrites for unsupported custom collection destination types.
- AM020 now respects compiler-known implicit nested conversions from Roslyn conversion classification, including user-defined nested-object conversions, while explicit-only nested conversions still report. Its code fix mirrors the same conversion check so a CreateMap with one real missing nested map and one implicitly convertible nested property only generates the real missing map. Analyzer and fixer built-in checks are namespace-aware, so actual `System.Guid`, `System.DateOnly`, `System.TimeOnly`, `System.Uri`, and other framework types are skipped without suppressing user-defined nested types that share the same short names.
- AM004/AM006 suppression actions now use clearer manual-review titles, AM004 catalog trust no longer overstates its `DoNotValidate()` fallback as a direct rewrite, and AM004 reverse-map fuzzy mapping is covered directly.
- AM006 now suppresses unmapped-destination diagnostics when `ConvertUsing` owns destination creation, suppresses `ConstructUsing` only for destination members explicitly initialized in every returned object initializer so partial or branch-specific construction still reports untouched members, keeps framework scalar/value types such as `System.DateOnly` out of flattening suppression, and resolves reverse-map types before offering per-property fuzzy source-member suggestions.
- Source-member ignore detection used by AM004, AM005, and adjacent source-validation paths recognizes semantic string constants such as `nameof(Source.TempData)` in `ForSourceMember(...).DoNotValidate()` calls instead of only lambda member selectors; direct helper coverage now locks lambda, string-literal path, `nameof(...)`, and const string extraction.
- Shared destination-member configuration detection used by AM001, AM003, AM005, AM006, AM011, AM020, AM021, and adjacent rules recognizes semantic string constants such as `nameof(Destination.FirstName)` as explicit configuration instead of only lambda and string-literal member selectors; direct helper coverage now locks both `nameof(...)` and const string path extraction, and AM005's explicit mapping action also uses a rule-prefixed equivalence key matching the rest of the code-fix providers.
- AM050 now treats redundant cleanup as a proven safe rewrite: string literal and `nameof(...)`/constant destination members are resolved through `CreateMap<TSource, TDestination>()`, mismatched same-name types are suppressed, and fixer titles retain the destination member name.
- AM050 source/destination lambda extraction now recognises parenthesized and typed lambda parameter forms (`(s) => s.Name` and `(Source s) => s.Name`) plus parenthesized member bodies (`s => (s.Name)` and `d => (d.Name)`), so redundant `MapFrom` configuration no longer slips past when callers spell out the lambda. Multi-parameter parenthesized lambdas (such as the `(src, ctx) => ...` `IMemberConfigurationExpression` overload) intentionally stay outside the analyzer's scope.
- AM050's `ForMember`-removal code fix is now withheld whenever the options lambda contains sibling configuration besides the redundant `MapFrom` (e.g. `Condition`, `NullSubstitute`, `PreCondition`, `UseDestinationValue`, `Ignore`). The diagnostic still fires so the user can remove the redundant `MapFrom` manually without losing the sibling policy. Simple-MapFrom shapes (`o => o.MapFrom(...)` or single-statement blocks) still receive the automatic action.
- AM030/AM032/AM033 now split converter-quality diagnostics by concept: invalid converter implementation (`AM030`, Error, analyzer-only), converter null handling (`AM032`, Warning, executable null-guard fix), and unused converter declaration (`AM033`, Info, analyzer-only). The shared converter code-fix provider advertises only the fixable AM032 ID, and the catalog trust test now prevents NoFix rule entries from forcing analyzer-only IDs into `FixableDiagnosticIds`.
- AM033 avoids unused-converter false positives when a converter is deliberately stored behind `ITypeConverter<TSource, TDestination>` before being passed to AutoMapper, and type-based `ConvertUsing(typeof(MyConverter))` registrations stay recognized even when the `typeof(...)` expression is parenthesized, cast to `Type`, or stored in a simple `Type` local before being passed to `ConvertUsing(...)`.
- AM033 also treats any `ConvertUsing(...)` argument whose resolved type is the interface `ITypeConverter<TSource, TDestination>` itself as covering every declared concrete implementation of that interface pair. That closes the DI/service-provider false-positive class — for example `public TestProfile(ITypeConverter<string, DateTime> converter)` or `services.Resolve<ITypeConverter<string, DateTime>>()` — while declared converters whose `<TSource, TDestination>` pair is not referenced by any `ConvertUsing` call still report.
- AM032 converter null-handling detection recognizes `ArgumentNullException.ThrowIfNull(source)`, `ArgumentException.ThrowIfNullOrEmpty(source)`, and `ArgumentException.ThrowIfNullOrWhiteSpace(source)` as explicit null guards on the converter's source parameter, closing the false-positive class where modern guard clauses replaced the older `if (source == null) throw ...` shape. Recognition also covers named-argument shapes such as `ThrowIfNull(argument: source)` and `ThrowIfNull(paramName: ..., argument: source)` so swapping or omitting optional labels does not re-introduce the false positive. Conditional access now suppresses only when it participates in a null guard such as `source?.Length is null`, `source?.Length is > 0`, `source?.Trim() == null`, `string.IsNullOrWhiteSpace(source?.Trim())`, `!(source?.Length > 0)`, `(source?.Length > 0) == false`, `(source?.Length > 0) is false`, or `(source?.Length).HasValue`, binds a pattern variable then checks that variable such as `source?.Length is var length && length > 0`, is stored in an initialized, split-assigned, or boolean guard local that is then null-guarded, `.HasValue`-guarded, relationally guarded before use, returned through a ternary fallback such as `trimmed is null ? string.Empty : trimmed`, or returned as a nullable-destination fallback after a positive local guard, feeds a null-tolerant TryParse fallback or success branch whose null-source path is source-free, passes conditional access to nullable parse provider/style arguments, guards a returning, throwing, ternary, switch expression, or switch statement path where null sources take a fallback path that does not use `source`, guards assignment to a fallback local that is returned after the branch including explicit `else` fallback assignments and harmless source-free statements before the fallback return, is paired with an explicit source-free fallback such as `source?.Trim() ?? string.Empty`, is coalesced through a simple local initialized from conditional access only when the fallback path is source-free, non-null, and the local has not been reassigned before the guard/fallback, is returned directly to a nullable destination, or is returned through a simple local initialized from the conditional access, including casted returns, branch returns, chained null-conditionals, and parenthesized local returns; guarded switch null arms/cases may fall through to later safe fallbacks when their `when` clauses are false, switch pattern-variable `when` guards such as `var length when length > 0` or `var length when length is null` are evaluated for null input in both switch expressions and switch statements, switch sections may break to later safe fallback returns, and null-excluding switch statements may fall through to later safe fallback returns. Standalone probes such as `_ = source?.Length` followed by unsafe source use, unsafe invocation or constructor arguments such as `DateTime.Parse(source?.Trim())`, `int.Parse(source?.Trim())`, `DateTime.Parse(trimmed)`, `new Uri(source?.Trim())`, or target-typed `new(source?.Trim())` in `Uri` converters, local guards that run after unsafe source use, member dereferences on maybe-null locals such as `trimmed.Length`, nested helper-only guards, coalesce fallbacks such as `source?.Trim() ?? source.Trim()`, `source?.Trim() ?? null`, or `source?.Trim() ?? null!`, coalesced guards whose null fallback enters an unsafe branch such as `(source?.Length ?? 1) > 0`, conditional fallback returns that can still fall through to unsafe source use, lifted inequality guards such as `source?.Length != 0 ? ... : ...`, conditional-access locals overwritten with unsafe source dereferences, nested local-function returns that do not represent the converter's return value, switch null arms/cases that still parse `source`, and reversed branches/null-comparison branches whose null path still parses `source` still trigger AM032.
- AM032 named-argument guard recognition now applies across the `ThrowIfNull*` family, including `ArgumentException.ThrowIfNullOrEmpty(paramName: ..., argument: source)` and `ArgumentException.ThrowIfNullOrWhiteSpace(paramName: ..., argument: source)`.
- AM031 analyzes `ForPath(... MapFrom(...))` in addition to `ForMember` and reports nested destination labels such as `Stats.Total`; `ForPath` diagnostics offer an executable Ignore scaffold while cache and convention-removal actions remain withheld because expression-tree statement lambdas do not compile and nested-path convention equivalence is not proven.
- AM031 multiple-enumeration tracking now also recognizes `Min`, `Max`, `Aggregate`, `LongCount`, `Single`, `SingleOrDefault`, `ToHashSet`, `ToDictionary`, and `ToLookup` as terminal enumeration calls, so shapes such as `src.Numbers.Min() + src.Numbers.Max()` or `src.Numbers.Single() + src.Numbers.ToHashSet().Count` now report instead of going silent. The whole enumeration-tracking set is gated on `System.Linq.Enumerable`/`System.Linq.Queryable` containing types so non-LINQ namesakes like `Math.Min`/`Math.Max` no longer false-positive, and lazy/intermediate operators (`Where`, `Select`, `OrderBy`, `GroupBy`, `Distinct`, …) intentionally stay off the terminal list.
- AM031 complex `SelectMany` operation diagnostics are now gated on `System.Linq.Enumerable`/`System.Linq.Queryable` containing types, so user-defined `SelectMany` namesakes with nested selector invocations no longer false-positive.
- AM031 expensive-computation detection now requires real `System.Linq.Enumerable.Range(...)`, so project-local `Enumerable.Range(...)` namesakes stay quiet.
- AM031 collection-key normalisation now peels chained pre-terminal LINQ receivers, so `src.Items.Where(x => x.Active).Count() + src.Items.Where(x => !x.Active).Any()` correctly normalises to the same source-rooted `Items` key and reports a multiple-enumeration diagnostic. Only invocations that resolve to known lazy operators on `System.Linq.Enumerable`/`System.Linq.Queryable` (`Where`, `Select`, `SelectMany`, `OrderBy[Descending]`, `ThenBy[Descending]`, `GroupBy`, `Distinct`, `Skip[While]/SkipLast`, `Take[While]/TakeLast`, `Reverse`, `Cast`, `OfType`, `DefaultIfEmpty`) are peeled, and the peeled root is only adopted when it normalises to a source-parameter-rooted member path, so arbitrary source-returning methods (`src.GetItems().Where(...).Count() + src.GetItems().Where(...).Any()` or `src.GetActiveItems()` vs `src.GetArchivedItems()`) and user-defined namesake extensions are not collapsed under a single receiver key. Single chained-LINQ terminals (`src.Items.Where(...).Count()`) still stay quiet.
- AM031 redundant-`ForMember` removal now requires a direct source-parameter member pass-through. It no longer drops mapping policy for transformed expressions (`src.Score + 1`) or captured same-name properties (`_scoreSource.Score`).
- AM031 non-deterministic and expensive-operation heuristics now include `DateTimeOffset.Now`/`UtcNow`, exact BCL `RandomNumberGenerator` calls, exact BCL `Stopwatch.GetTimestamp()`/`StartNew()`/`GetElapsedTime(...)` calls, exact BCL `Environment.GetCommandLineArgs()`/`GetEnvironmentVariable(...)`/`GetEnvironmentVariables(...)`/`SetEnvironmentVariable(...)`/`ExpandEnvironmentVariables(...)`/`GetFolderPath(...)`/`GetLogicalDrives()`, and exact BCL `Environment` state properties such as `MachineName`, `CurrentDirectory`, `ExitCode`, `TickCount`, and `TickCount64` while keeping source-rooted user `Stopwatch` namesakes quiet, keep read-only delegate calls rooted in the source mapping parameter quiet even when the lambda parameter is not named `src`, keep exact BCL `StringComparer.Compare(...)`/`Equals(...)`/`GetHashCode(...)`, `EqualityComparer<T>.Equals(...)`/`GetHashCode(...)`, `ReferenceEqualityComparer.Equals(...)`/`GetHashCode(...)`, and `Comparer<T>.Compare(...)` helper calls quiet only when the receiver is a known framework comparer singleton such as `StringComparer.OrdinalIgnoreCase`, `EqualityComparer<T>.Default`, `ReferenceEqualityComparer.Instance`, or `Comparer<T>.Default`, a readonly field initialized from one, or a get-only property initialized from or returning one, while injected BCL-typed comparer fields/properties and mutable comparer fields still report as external method calls, avoid treating pure `System.IO.Path.Combine(...)` string composition, in-memory `MemoryStream`, `StringReader`, `StringWriter`, `Stream` locals backed by `MemoryStream`, reader/writer helpers over direct or locally initialized `MemoryStream`, and `TextReader`/`TextWriter` locals backed by `StringReader`/`StringWriter` as file I/O while still allowing filesystem-touching `Path.Exists(...)`, `Path.GetTempFileName()`, `File.*`, `Directory.*`, exact BCL `FileInfo`/`DirectoryInfo` filesystem operations, inherited `FileSystemInfo.Delete()`/`Refresh()` calls, archive `System.IO.Compression.ZipFile` and `ZipFileExtensions` file operations, exact BCL `MemoryMappedFile` create/open/view operations, metadata properties such as `FileInfo.Length`, `FileInfo.IsReadOnly`, `FileInfo.Exists`, `DirectoryInfo.Exists`, timestamps, and attributes, and stream reads such as file-backed `StreamReader.ReadToEnd()` to report while keeping source-rooted user `DirectoryInfo` and `MemoryMappedFile` namesakes quiet, flag exact BCL compression streams (`GZipStream`, `DeflateStream`, `BrotliStream`, `ZLibStream`) read/write/copy calls while keeping source-rooted user compression stream namesakes quiet, flag exact BCL `System.Console` read/write and standard-stream open/set calls while keeping source-rooted user `Console` namesakes quiet, flag exact BCL reflection/runtime activation/expression compile operations (`object.GetType`, `System.Type`/`System.Reflection` metadata property access including member type metadata, parameter/generic metadata lookup, current-method lookup, runtime/declaration lookup and enumeration, custom-attribute data/static attribute lookup and definition checks, metadata-token/runtime-handle resolution, generic type/member construction, delegate binding, reflection invocation, assembly loading/probing/resource lookup including `AssemblyLoadContext`, dynamic code generation via `System.Reflection.Emit`, `Activator.CreateInstance`, `Expression.Compile`) while keeping user `Activator`, `Expression<T>`, and project-local reflection property/method/extension namesakes quiet, match framework HTTP calls by exact BCL type and request method (`System.Net.Http.HttpClient`, `System.Net.WebClient`, `System.Net.Http.HttpMessageInvoker`), method-gated exact BCL `HttpContent` body reads/copies, exact BCL `System.Net.Http.Json` JSON extension calls, and method-gated `System.Net.WebRequest`/`HttpWebRequest` response/request-stream calls so source-rooted user types such as `HttpClientCache`, `HttpContent`, and `WebRequest` do not become HTTP false positives and non-request/control/configuration methods such as `HttpClient.CancelPendingRequests()`, `HttpMessageInvoker.Dispose()`, `DefaultRequestHeaders.Clear()`, and parsed header collection mutators such as `DefaultRequestHeaders.UserAgent.ParseAdd(...)` stay quiet, flag exact BCL `System.Net.Dns` lookup calls while keeping source-rooted user `Dns` namesakes quiet, flag exact BCL `System.Net.Sockets.TcpClient`/`UdpClient`/`Socket`/`NetworkStream` network I/O and `System.Net.NetworkInformation.Ping.Send*` probes while keeping source-rooted user `TcpClient` and `Ping` namesakes quiet, flag exact BCL `System.Diagnostics.Process.Start(...)` launches, `Process.Kill(...)`/`CloseMainWindow()` control calls, `Process.WaitForExit(...)`/`WaitForInputIdle(...)` blocking waits, and `System.Environment.Exit(...)`/`FailFast(...)` process termination while keeping source-rooted user `Process` namesakes quiet, flag exact BCL `System.GC.Collect(...)`, `GC.WaitForPendingFinalizers()`, `GC.TryStartNoGCRegion(...)`, `GC.EndNoGCRegion()`, `GC.AddMemoryPressure(...)`, and `GC.RemoveMemoryPressure(...)` as GC operations, flag exact BCL `System.Threading.Tasks.Task.Run(...)`, `TaskFactory.StartNew(...)`, and `System.Threading.ThreadPool` queue/register-wait calls as background work scheduling, flag exact BCL `System.Text.Json.JsonSerializer` serialize/deserialize calls including `SerializeToNode(...)` and `DeserializeAsyncEnumerable(...)`, exact BCL `System.Xml.Serialization.XmlSerializer` serialize/deserialize calls, exact BCL `DataContractSerializer` and `DataContractJsonSerializer` `ReadObject(...)`/`WriteObject(...)` calls, and base-typed exact BCL `XmlObjectSerializer` calls as serialization operations while keeping inline project-local runtime serializer namesakes quiet, flag exact BCL `JsonDocument.Parse(...)`, `JsonNode.Parse(...)`, `XDocument`/`XElement` `Parse(...)`/`Load(...)`, and `XmlDocument.Load(...)`/`LoadXml(...)` calls as parsing operations while keeping project-local parse namesakes quiet, flag exact BCL `System.Text.RegularExpressions.Regex` match/replace/split calls as regex operations while keeping source-rooted user `Regex` namesakes quiet, flag exact BCL `System.Security.Cryptography.HashAlgorithm`/`HMAC*` compute-hash calls, `IncrementalHash` hash/HMAC calls, `MD5`/`SHA*` static hash-data calls, `Rfc2898DeriveBytes` and `PasswordDeriveBytes` key-derivation calls, `RSA`/`ECDsa`/`DSA` public-key encrypt/decrypt/sign/verify calls, `ECDiffieHellman.DeriveKey*` key-agreement calls, and `SymmetricAlgorithm.CreateEncryptor`/`CreateDecryptor` plus `ICryptoTransform.Transform*` symmetric transform calls as cryptographic operations while keeping user `SHA256`, `IncrementalHash`, `Rfc2898DeriveBytes`, `PasswordDeriveBytes`, `RSA`, `ECDsa`, `DSA`, `ECDiffieHellman`, `Aes`, and `ICryptoTransform` namesakes quiet, flag exact BCL `Thread.Sleep(...)`, `Thread.SpinWait(...)`, `Thread.Join(...)`, `SpinWait.SpinOnce(...)`, `SpinWait.SpinUntil(...)`, `WaitHandle.WaitOne(...)`, `Monitor.Wait(...)`, `SemaphoreSlim.Wait(...)`, `ManualResetEventSlim.Wait(...)`, and `ReaderWriterLockSlim.Enter*Lock(...)` blocking calls while keeping source-rooted user `Thread`/`SpinWait`/`WaitHandle`/`SemaphoreSlim`/`ReaderWriterLockSlim` namesakes quiet, and restrict database/SQL detection to concrete data-access type/name shapes and provider namespaces so source-rooted user namesakes such as `DbContextCache`, `ReportingDbContext`, `DbSet<T>`, `SqlConnection`, `Dapper.Cache`, `NHibernate.Cache`, and `EntityFrameworkQueryableExtensions` stay quiet while injected `*DbContext` collaborators, `Microsoft.EntityFrameworkCore.DbSet<T>`, `Dapper.SqlMapper`, `NHibernate.ISession`, `Microsoft.Data.SqlClient.SqlConnection`, and `Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions` still report.
- AM031 sync-over-async detection now includes static `Task.WaitAll(...)` and `Task.WaitAny(...)` invocations alongside `Task.Result`, instance `Task.Wait(...)`, and awaiter `GetResult()` calls, with direct coverage for expression-bodied `WaitAny` and Func-overload statement-bodied `WaitAll` MapFrom shapes.
- AM031 resource lookup detection now flags exact BCL `System.Resources.ResourceManager.GetString(...)`, `GetObject(...)`, `GetStream(...)`, and `GetResourceSet(...)` calls while keeping source-rooted project-local `ResourceManager` namesakes quiet.
- AM031 reflection detection now flags exact BCL `AssemblyName.GetAssemblyName(...)` assembly-name metadata probing, `Assembly.GetSatelliteAssembly(...)` satellite assembly probing, `Assembly.CreateInstance(...)` reflection activation, and `Assembly` file/module/forwarded-type metadata probes such as `GetModules(...)` while keeping project-local `AssemblyName`/`Assembly` namesakes quiet.
- AM031 file-I/O detection now flags exact BCL `MemoryMappedViewAccessor.Flush(...)` calls while keeping source-rooted project-local `MemoryMappedViewAccessor` namesakes quiet.
- AM031 file-I/O detection now flags exact BCL `FileStream.Flush(...)`, `SetLength(...)`, `Lock(...)`, and `Unlock(...)` calls while keeping source-rooted project-local `FileStream` namesakes quiet.
- AM031 file-I/O detection now flags file-backed `Stream.CopyTo(...)` operations while keeping `Stream` locals backed by `MemoryStream` quiet.
- AM021 now reports reverse-map collection element gaps when only the forward element map exists, keeps plain bidirectional misses to one forward diagnostic, and uses concrete `HashSet<T>` mappings for set destination simple conversions. Dictionary key/value diagnostics are decomposed by axis: simple key/value conversions get executable `ToDictionary(...)` rewrites, complex value-only gaps can offer the value `CreateMap`, and misleading `CreateMap<KeyValuePair<...>, KeyValuePair<...>>()` fixes are withheld.
- AM021 now respects compiler-known implicit element conversions from Roslyn conversion classification, including user-defined value-object conversions such as `Money` to `decimal`, while explicit-only element conversions and reverse-map directions where a forward implicit conversion is not reversible still report.
- AM021 simple-conversion fixes now emit fully qualified `global::System.Convert`, `global::System.DateTime`, and `global::System.Guid` calls instead of relying on `using System`, avoiding user-type shadowing in generated mappings. Immutable/frozen destination collections use fully qualified `ImmutableList.CreateRange(...)`, `ImmutableHashSet.CreateRange(...)`, and `FrozenSet.ToFrozenSet(...)` factory calls, while custom collection lookalikes stay on the manual-review ignore path.
- AM021's Tests score is now calibrated to 5, matching AM022 after the current coverage review found comparable analyzer breadth and stronger code-fix method count for AM021.
- AM022 ignore suppression now uses semantic AutoMapper method ownership, so helper calls such as `MappingHelpers.Ignore()` inside a `ForMember` options lambda no longer hide a real recursion diagnostic. AM022 also reuses the shared namespace-aware built-in classifier for simple-type pruning, keeping actual `System.Guid`/`DateTime`-style framework types out of recursion graphs without suppressing user-defined domain types that share those short names.
- AM041 duplicate-map diagnostics now include constructed generic type arguments and array element types/ranks, so collection duplicates name the actionable source/destination shapes instead of collapsing both sides to the metadata type name.
- AM041 now avoids offering the duplicate-map removal fix for `ReverseMap().ForMember(...)` style chains, including parenthesized chains, where the reverse direction carries additional configuration that cannot be safely rewritten.
- AM041 now also withholds the duplicate-map removal fix when the duplicate is a `CreateMap<TSource, TDestination>()` registration carrying chained mapping configuration — including `CreateMap<>().ForMember(...)`, parenthesized `(CreateMap<>()).ForMember(...)`, and `CreateMap<>().ReverseMap().ForMember(...)` shapes — so policy overrides such as `.ForMember(d => d.X, opt => opt.Ignore())` are no longer silently dropped by an automatic statement removal. Bare `CreateMap<TSource, TDestination>().ReverseMap()` reversals, including `(CreateMap<TSource, TDestination>()).ReverseMap()`, preserve the reverse direction through the safe swapped `CreateMap` rewrite.
- AM041 now resolves code-fix diagnostics to the actual nested `CreateMap`/`ReverseMap` invocation before checking removability, so duplicate mappings passed as arguments such as `Register(CreateMap<S, D>())` or `Register(CreateMap<S, D>().ReverseMap())`, plus variable-assigned duplicates such as `var reverse = CreateMap<S, D>().ReverseMap()`, still report but do not offer a destructive fix that would delete or strand the containing expression.
- AM050 redundant-`MapFrom` detection now uses nullability-aware type comparison, so nullable reference source members mapped to non-nullable destination members stay out of the cleanup path instead of being treated as convention-equivalent.
- Analyzer ownership is a real strength. The conflict tests and shared helpers make `AM001`/`AM002`/`AM003`/`AM020`/`AM021` boundaries much healthier than a file-count audit would suggest.
- The project now has a checked-in `RuleCatalog` health contract plus generated `docs/RULE_CATALOG.md` and sample diagnostic snapshots that tie rule IDs to descriptors, fixers, docs anchors, sample paths, and descriptor-aware fixer trust levels.
- Diagnostic placement has been tightened for high-volume data-integrity rules (`AM004`, `AM005`, `AM006`, and `AM011`) so diagnostics land on the offending source/destination property token while preserving mapping-invocation metadata for code-fix routing. Configuration and heuristic rules still report at mapping invocations or mapping lambdas when that is the actionable anchor.
- Several fixers intentionally produce advisory/default mapping scaffolds. That is fine, and docs/code-action titles now distinguish "safe executable rewrite" from "starter mapping the developer must review."

## Verification Baseline

Architecture-style coverage currently comes from analyzer/fixer tests, conflict ownership tests, helper tests, sample projects, documentation, the checked-in `RuleCatalog`, generated trust artifacts, package smoke tests, and the deterministic `tools/AnalyzerVerifier` checks.

Current local verification (**2026-07-15** against the **v2.30.67** release candidate):

- Package version in `src/AutoMapperAnalyzer.Analyzers/AutoMapperAnalyzer.Analyzers.csproj`: **2.30.67**.
- `dotnet build automapper-analyser.sln --configuration Release -warnaserror` passed: 0 warnings, 0 errors.
- Clean-branch full suite: **1407** passed, 0 skipped, 0 failed.
- `dotnet run --project tools/AnalyzerVerifier/AnalyzerVerifier.csproj --configuration Release --no-build -- --check-catalog --check-snapshots` passed: rule catalog and sample diagnostics snapshot are up to date.
- `git diff --check` passed.
- Targeted `dotnet format whitespace` completed without diffs in the changed C# files; the repository-wide check still exposes pre-existing line-ending drift outside this slice.
- Sample project emits AM0xx when analyzers run (`-p:RunAnalyzersDuringBuild=true`); local untracked `Directory.Build.props` (if present) may skip analyzers during CLI builds — unit tests + AnalyzerVerifier remain the verification source of truth.
- Approximate list-tests name hits (not exclusive filters; for trend only): AM001 57, AM002 83, AM003 61, AM004 87, AM005 34, AM006 43, AM011 45, AM020 97, AM021 64, AM022 53, AM030/32/33 bucket 152, AM031 292, AM041 35, AM050 48, RuleCatalog 10.
- Release metadata is synchronized for `v2.30.67`; tag/publication follows the merged PR.
- Historical baseline (2026-07-06 @ `b138fa8` / v2.30.55): **1352** tests green — superseded; do not use for planning.
- The trust-first pass removed active skipped tests, added drift validation, and moved intentional analyzer-test warnings into an explicit test-project warning baseline.
- `/usr/local/share/dotnet/dotnet --list-runtimes` shows only .NET 10 runtimes in this local environment, so broader multi-TFM runtime verification remains blocked by missing .NET 8 and .NET 9 runtimes.

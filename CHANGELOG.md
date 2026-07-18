# Changelog

## Unreleased

## [2.30.84] - 2026-07-18

AM021 nested collection element fix safety.

### Changed

- **Withhold ineffective nested maps**: AM021 no longer offers `CreateMap<TSourceElement, TDestinationElement>()` when either collection element is itself a generic collection or array. Registrations such as `CreateMap<List<string>, List<int>>()` do not supply the missing inner `string` to `int` conversion and could silence the diagnostic without making the mapping executable.
- **Manual-review boundary**: nested generic and array element mismatches retain the explicit Ignore action. Plain domain-object element pairs retain their existing element `CreateMap` action, while executable primitive conversions and dictionary handling remain unchanged.

### Validation

- AM021 analyzer, code-fix, and helper suite: **72** passed.
- Clean-branch full solution suite: **1693** passed, 0 skipped, 0 failed on `net10.0`.

## [2.30.83] - 2026-07-18

AM021 Stack conversion order safety.

### Fixed

- **Preserve LIFO order**: AM021 now appends `Reverse()` to the converted sequence before constructing an exact BCL `Stack<T>`, preventing the generated element-conversion fix from silently reversing top-to-bottom order.
- **Narrow collection semantics**: queue, array, list, set, immutable, dictionary, custom-collection, diagnostic-ownership, and v2.30.82 insertion-safety behavior remain unchanged.

### Validation

- AM021 analyzer, code-fix, and helper suite: **70** passed.
- Clean-branch full solution suite: **1691** passed, 0 skipped, 0 failed on `net10.0`.

## [2.30.82] - 2026-07-18

AM021 direct-statement and conditional-region safety.

### Changed

- **No deferred-map hoisting**: AM021 now offers its complex element `CreateMap<TSourceElement, TDestinationElement>()` action only when the diagnosed fluent mapping owns a direct constructor or method block statement. Mappings nested in callbacks, local functions, arguments, assignments, returns, and other expressions keep the in-place Ignore action without receiving a scope-changing registration.
- **Conditional-region safety**: direct statements inside or split by `#if`/`#else` now also keep only Ignore, preventing an insertion produced under one symbol set from changing another configuration. AM020 and AM021 share the same proven statement-region guard.
- **Direct-owner parity**: ordinary block statements, direct method statements, parenthesized mapping expressions, fluent chains, and unconditional mappings after completed conditional regions retain the complex element-map action.

### Validation

- AM021 analyzer, code-fix, and helper suite: **70** passed.
- Clean-branch full solution suite: **1691** passed, 0 skipped, 0 failed on `net10.0`.

## [2.30.81] - 2026-07-18

AM020 conditional block-body insertion safety.

### Changed

- **Conditional block safety**: AM020 now withholds nested-map insertion when the diagnosed `CreateMap` statement begins inside an open `#if` region or conditional directives split its fluent tokens, preventing a fix generated for one active symbol set from corrupting or omitting configuration in another.
- **Owner parity**: the fail-closed boundary applies to both constructor and method blocks. Ordinary unconditional block statements—including mappings after a completed conditional region—and the safe expression-bodied paths from 2.30.79–2.30.80 retain their existing fixes.

### Validation

- AM020 analyzer, code-fix, and helper suite: **115** passed.
- Clean-branch full solution suite: **1685** passed, 0 skipped, 0 failed on `net10.0`.

## [2.30.80] - 2026-07-18

AM020 expression-bodied void-method fixer parity.

### Changed

- **Expression-bodied void-method fix**: AM020 now expands a direct expression-bodied `void` method into a block, preserves the original root `CreateMap<TSource, TDestination>()` call as the first statement, and appends the missing nested registration after it.
- **Semantic safety boundary**: the rewrite requires Roslyn to resolve the method as returning `void`, requires the diagnosed map to own the complete arrow expression, and reuses AM020's stable receiver gate. Non-void methods, local functions, nested or deferred maps, computed/property/conditional/indexed receivers, and expression bodies split by `#if`/`#else` remain fixless. Comments before and after the arrow plus semicolon/trailing comments are retained.

### Validation

- AM020 analyzer, code-fix, and helper suite: **111** passed.
- Clean-branch full solution suite: **1681** passed, 0 skipped, 0 failed on `net10.0`.

## [2.30.79] - 2026-07-18

AM020 expression-bodied Profile constructor fixer parity.

### Changed

- **Expression-bodied constructor fix**: AM020 now expands an expression-bodied Profile constructor into a block, preserves the original root `CreateMap<TSource, TDestination>()` call as the first statement, and appends the missing nested registration after it.
- **Conservative structural boundary**: the rewrite reuses AM020's stable receiver gate. Computed, property, conditional, and indexed receivers remain fixless, and expression-bodied methods remain outside this constructor-only change.

### Validation

- AM020 analyzer and code-fix suite: **104** passed.
- Clean-branch full solution suite: **1674** passed, 0 skipped, 0 failed on `net10.0`.

## [2.30.78] - 2026-07-18

AM020 stable configuration receiver fixes.

### Changed

- **Receiver-preserving nested-map fix**: AM020 now offers its missing nested `CreateMap<TSource, TDestination>()` action for block-bodied constructor and method calls rooted at a stable `IMapperConfigurationExpression` parameter, local, or field, and emits the new registration through that same receiver.
- **Side-effect boundary**: invocation, property, conditional, and indexed receivers remain fixless, so applying the action never repeats potentially side-effecting receiver evaluation or silently changes configuration ownership. Existing bare and `this.CreateMap(...)` Profile fixes retain their behavior.

### Validation

- AM020 analyzer and code-fix suite: **100** passed.
- Clean-branch full solution suite: **1670** passed, 0 skipped, 0 failed on `net10.0`.

## [2.30.77] - 2026-07-18

AM041 mutually exclusive branch precision.

### Changed

- **Mutually exclusive registrations**: duplicate `CreateMap<TSource, TDestination>()` calls in opposite arms of the same `if`/`else`, including one `if`/`else if`/`else` chain, no longer emit AM041 when they share one executable body and therefore cannot run together.
- **Conservative conflict boundary**: independent `if` statements, registrations outside the branch chain, and registrations in different executable bodies still participate in duplicate detection. When an unconditional registration follows mutually exclusive alternatives, AM041 continues to report that real conflict and retains its removal fix.

### Validation

- AM041 analyzer and code-fix suite: **49** passed.
- Clean-branch full solution suite: **1667** passed, 0 skipped, 0 failed on `net10.0`.

## [2.30.76] - 2026-07-18

AM022 deferred root cycle-breaker parity.

### Changed

- **Deferred root policies**: a direct local rooted at `CreateMap<TSource, TDestination>()` now carries later same-block semantic `MaxDepth`, `PreserveReferences`, and `ConvertUsing` calls into AM022's root decision, matching the direction-aware registry behavior already used downstream.
- **Conservative execution boundary**: deferred policies must be the first executable statement after the mapping declaration, apart from inert local-function declarations or empty statements, and appear in a direct fluent receiver chain rooted at that local. Any intervening executable statement fails closed, covering constructors, accessors, user-defined execution, arbitrary wrappers, helper substitution, recursive effects, delegate invocation surfaces including `DynamicInvoke`, dynamic calls, and unresolved operations without an incomplete syntax blacklist. Conditional exits and conditional expressions cannot hide an unconstrained map. Policies applied only after `ReverseMap()` do not suppress the forward root diagnostic, and duplicate mapping directions remain diagnostic unless every registration carries the constraint.
- **Review routing**: removed the automatic Claude pull-request review workflow; exact-head GitHub Codex review remains the independent PR gate, while the separate opt-in `@claude` workflow remains available.

### Validation

- AM022 analyzer and code-fix suite: **131** passed.
- Clean-branch full solution suite: **1646** passed, 0 skipped, 0 failed on `net10.0`.

## [2.30.75] - 2026-07-18

AM022 constructor-parameter cycle ownership.

### Changed

- **AM022 `ForCtorParam` edges**: unique semantic AutoMapper constructor-parameter mappings with a direct source-property selector now participate in root and downstream recursion graphs when the selected constructor assigns that parameter to one destination property, whether writable, read-only, or the compiler-generated property of the same positional-record parameter.
- **Member override replay**: when a writable constructor-owned destination property also has an explicit `ForMember(...MapFrom(...))`, AM022 retains both the construction edge and the later member edge so post-construction recursion cannot be hidden by constructor analysis.
- **Deferred converter ownership**: `ConvertUsing` configured later through a direct local mapping variable remains authoritative for constrained constructor maps, matching the registry's existing deferred cycle-breaker model.
- **Constructor-aware cycle breaking**: `ForMember(...Ignore())` and `PreserveReferences` no longer hide a constructor-owned recursive edge because AutoMapper resolves constructor arguments before member mapping and before a destination instance can participate in reference tracking. `MaxDepth` remains effective for mixed constructor/member cycles, but not for self-cycles or multi-type cycles whose complete return path is constructor-owned. Constructor-owned diagnostics withhold all automatic actions; `ConvertUsing` remains the explicit construction-ownership escape hatch.
- **Conservative boundary**: duplicate constructor-parameter configuration, invalid parameter paths, transformed selectors, uninvoked local helpers, ambiguous/additional constructor uses, metadata-only ownership, and lookalike APIs remain outside inference. Ordinary member cycles still respect `MaxDepth` and `PreserveReferences`, and `ConvertUsing` continues to terminate every traversal shape.

### Validation

- AM022 analyzer and code-fix suite: **111** passed.
- Clean-branch full solution suite: **1626** passed, 0 skipped, 0 failed on `net10.0`.
- AutoMapper 14 runtime proofs: property Ignore left the constructor-created recursive value intact; `MaxDepth(2)` and `PreserveReferences()` both stack-overflowed on a self-cycle; `PreserveReferences()` also stack-overflowed on a mixed constructor/member cycle; `MaxDepth(2)` terminated mixed cycles but stack-overflowed when every cycle edge was constructor-owned. Reported constructor-owned diagnostics therefore expose no automatic fix.

## [2.30.74] - 2026-07-17

AM002 constructor-parameter null-safety ownership.

### Changed

- **AM002 `ForCtorParam` ownership**: semantic AutoMapper constructor-parameter mappings now participate in the existing null-expression analysis for exact literal, `nameof(...)`, and const destination parameter names. Unique direct top-level constructor assignments such as `Value = valueParam` connect differently named parameters to their destination properties; deferred lambda/local-function writes remain outside the boundary. Constructor ownership remains authoritative over same-name member/path configuration, matching AutoMapper's execution plan.
- **Direction and safety boundaries**: safe non-null-producing constructor expressions are evaluated against the single longest resolvable constructor, matching AutoMapper's runtime selection order; public readable source properties, inherited public source fields, public parameterless method and `GetX()` conventions at root and recursively flattened paths, semantic sibling configuration, optional/defaulted parameters, and `params` establish selectability. Wrong or differently cased parameter names, lookalike APIs, and the opposite `ReverseMap()` direction remain outside that ownership; configured names must resolve ordinally against the selected constructor before they can affect analysis or fixing; incomplete `ForCtorParam()` calls are ignored until their arguments are present instead of producing AD0001, and recursive collection shapes are bounded by visited symbol pairs rather than risking stack overflow. Constructor-only parameters are analyzed directly; a direct-only writable alias delegates only when its final value is guaranteed by an unvetoed convention mapping or by the last effective top-level `ForMember`/`ForPath` using unconditional `MapFrom`/`ConvertUsing`. `Ignore`, `Condition`, and `PreCondition` keep the constructor input analyzed, configuration order is respected, and any additional constructor use plus competing, other-instance, compound, or non-owned assignments fails closed; constructed generic destination members compare through original symbol definitions.
- **Constructor-aware fix**: the default-value action rewrites the existing `ForCtorParam(...MapFrom(...))` expression only when a provably non-null fallback exists; element-nullability diagnostics and constructor-only custom reference parameters without such a fallback expose no unsafe scaffold. Property Ignore is withheld when removing post-construction assignment would expose an unsafe nullable constructor alias, but remains available when the constructor alias is proven non-null. When a nullable method convention needs a `MapFrom` appended to existing non-veto member options, the fixer preserves the computed invocation (for example `src.GetValue()`) instead of reconstructing a nonexistent property access. A single directly executed aliased or helper-owned constructor `MapFrom` is likewise rewritten in place, preserving transformations instead of appending a replacement mapping; alias/helper-aware `Condition` and `PreCondition` vetoes withhold the default fix so assignment cannot be skipped after repair.
- **Review hardening**: directly executed map-wide `ForAllMembers` `Ignore`/`Condition`/`PreCondition` policies now veto convention and targeted post-construction ownership before a writable constructor alias can be delegated, while veto calls captured inside uninvoked lambdas or local functions remain deferred and do not affect ownership. Non-async local functions reached by direct callback execution are now followed semantically, including nested calls and direct aliases of the options parameter, with symbol-based recursion guards; options aliases are replayed in source order at each executed call so assignments establish ownership and later reassignment removes it; captured aliases and options-valued arguments flow into synchronously invoked local functions; captured receiver reassignments performed by those helpers are replayed at the call site before later configuration is evaluated; argument-side helper effects feed the containing callee after C# argument evaluation, and `ref`/`out` parameter mutations flow back to caller aliases, while nested calls expand child/argument invocations before their containing call to preserve C# evaluation order. Conditional or loop reassignment removes may-alias provenance only when the assignment is guaranteed on every path reaching that invocation; receivers that may instead point at another options instance make later safety-producing calls conditional alternatives rather than guaranteed configuration. Constructor `MapFrom` body extraction now consumes the same executed invocation stream, recognizing options aliases and invoked local helpers while excluding uninvoked/deferred calls and semantic lookalikes. Conditional `MapFrom` branches remain alternatives so a safe textual last branch cannot hide an unsafe runtime path, while a later unconditional mapping still replaces earlier candidates. Parenthesized option receivers preserve semantic alias identity, iterator-local vetoes reached through `System.Linq.Enumerable.ToList`/`ToArray` materialization are treated as synchronously executed, and member-option `NullSubstitute` suppresses only when its call is both directly and unconditionally executed; options calls after an `await` remain excluded while pre-suspension configuration stays active. Iterator bodies participate when a direct invocation or any semantic local target in its enclosing assignment chain may still reach source-ordered synchronous enumeration; conditional iterator-result reassignment preserves that possible path unless Roslyn endpoint reachability proves the reassignment dominates the enumeration, and per-member configuration shares the same deferred-scope boundary. Nested generic nullability traversal now carries variance polarity recursively: single contravariant consumer positions stay quiet while double contravariance restores the producer-side nullable-loss diagnostic. An executable AutoMapper 14 regression confirms that same-name `ForCtorParam` ownership consumes a later explicit member configuration, so the constructor remains authoritative.

### Validation

- AM002 analyzer and code-fix suite: **234** passed.
- Clean-branch full solution suite: **1600** passed, 0 skipped, 0 failed on `net10.0`.
- Public 2.30.73 repro: the safe constructor mapping emitted build-blocking AM002, while AutoMapper 14 validated and mapped null to `string.Empty` when only AM002 was suppressed.

## [2.30.73] - 2026-07-16

AM041 deferred `ReverseMap()` registration.

### Changed

- **AM041 local-symbol coverage**: a direct local rooted in semantic AutoMapper `CreateMap<S, D>()`, including a fluent configuration initializer, now registers later standalone `mapping.ReverseMap()` calls in the same block, so the reverse `D → S` direction participates in duplicate detection regardless of statement order.
- **Conservative boundary**: aliases, fields/properties, conditional or nested calls, assignments, and lookalike APIs remain excluded rather than introducing flow-sensitive guesses.
- **Executable fix**: when a standalone deferred `ReverseMap()` is the duplicate, the fixer removes that statement instead of rewriting it to the invalid expression statement `mapping;`.

### Validation

- AM041 analyzer and code-fix suite: **45** passed.
- Clean-branch full solution suite: **1450** passed, 0 skipped, 0 failed on `net10.0`.
- Public 2.30.72 repro: the deferred-local form emitted no AM041 while the equivalent fluent form did; both AutoMapper 14 configurations passed `AssertConfigurationIsValid()`.

## [2.30.72] - 2026-07-16

AM001 constructor-parameter ownership precision.

### Changed

- **AM001 `ForCtorParam` ownership**: semantic AutoMapper constructor-parameter configuration now suppresses the exact positional-record property mismatch it converts, including literal, `nameof(...)`, and const parameter names.
- **Direction and name boundaries**: wrong parameter names remain diagnostic, and configuration after `ReverseMap()` applies only to the reverse destination.
- **Stale fixer honesty**: an obsolete AM001 diagnostic no longer offers conversion or Ignore actions after live recomputation proves constructor ownership removed the mismatch.

### Validation

- AM001 analyzer and code-fix suite: **63** passed.
- Clean-branch full solution suite: **1440** passed, 0 skipped, 0 failed on `net10.0`.
- AutoMapper 14 runtime repro: configured mapping passes `AssertConfigurationIsValid()` and maps the expected value; the unconfigured control throws `AutoMapperConfigurationException`.

## [2.30.71] - 2026-07-16

AM022 downstream direct member-map cycle detection.

### Changed

- **AM022 downstream direct `ForMember(...MapFrom...)` edges**: uniquely registered forward maps now contribute strict direct property-to-property edges throughout the configured recursion graph, so cycles with renamed members on multiple legs report every owning `CreateMap`.
- **Conservative boundary**: duplicate mapping directions, reverse-generated mappings, duplicate destination configuration, transformed expressions, `ForPath` member inference, lookalike APIs, resolvers, and converters remain excluded; downstream `MaxDepth`, `PreserveReferences`, and `ConvertUsing` still terminate traversal.
- **Graph-breaking fix**: semantic `ForMember` and `ForPath` Ignore overrides remove the destination edge when downstream maps are replayed. One root Ignore action therefore clears all diagnostics for the broken cycle and is appended after an existing `MapFrom` so it is effective.

### Validation

- AM022 analyzer and code-fix suite: **85** passed.
- Full solution suite: **1433** passed, 0 skipped, 0 failed on `net10.0`.

## [2.30.70] - 2026-07-16

AM022 direct member-map cycle detection.

### Changed

- **AM022 direct `ForMember(...MapFrom...)` edges**: a uniquely configured, semantically AutoMapper-owned direct property mapping can now close the root recursion graph even when source and destination member names differ.
- **Conservative boundary**: only direct property-to-property expression lambdas on the current forward map participate. Duplicate destination configuration, transformed expressions, lookalike APIs, reverse segments, and downstream explicit-member inference remain excluded.
- **Effective Ignore fix**: the graph-aware Ignore action is appended after the existing `MapFrom` so the cycle breaker is the effective destination-member policy; MaxDepth remains first.

### Validation

- AM022 analyzer and code-fix suite: **75** passed.
- Clean-branch full suite: **1423** passed, 0 skipped, 0 failed.

## [2.30.69] - 2026-07-16

AM032 destination-aware null-fix policy.

### Changed

- **AM032 nullable destinations**: converters whose destination return type is semantically proven nullable now offer `return null` as the primary null guard, while retaining the existing exception guard as an explicit alternative.
- **Conservative boundary**: non-nullable, oblivious, and otherwise unproven destination types remain throw-only. Both generated forms compile for the supported net48 consumer baseline.

### Validation

- AM030/AM032 code-fix suite: **18** passed.
- Clean-branch full suite: **1414** passed, 0 skipped, 0 failed.

## [2.30.68] - 2026-07-15

AM011 single-property fixer trust hardening.

### Changed

- **AM011**: single-property diagnostics no longer manufacture required domain data with `string.Empty`, `0`, `false`, or `default` when no unique fuzzy source-property match exists.
- **Explicit fallback**: the fixer keeps a unique fuzzy mapping as the primary action and otherwise offers only the clearly labelled Ignore action for manual review. Aggregate Scaffold-all remains available for intentionally reviewing several missing members together.

### Validation

- AM011 analyzer and code-fix suite: **45** passed.
- Clean-branch full suite: **1410** passed, 0 skipped, 0 failed.

## [2.30.67] - 2026-07-15

Performance `ForPath` fixer parity for AM031 and AM034–AM038.

### Changed

- **AM031 / AM034–AM038**: nested `ForPath` performance diagnostics now offer an executable `Ignore()` scaffold labelled for manual review.
- **Conservative fixer boundary**: the action preserves the original nested destination selector and options parameter; caching remains `ForMember`-only because block-bodied expression trees do not compile, and convention removal remains withheld for nested paths.

### Validation

- Performance code-fix suite: **20** passed.
- Clean-branch full suite: **1410** passed, 0 skipped, 0 failed.

## [2.30.66] - 2026-07-13

AM022 downstream cycle-breaker precision for intentional circular mappings.

### Changed

- **AM022**: multi-map recursion traversal now stops when a downstream mapping direction is explicitly bounded by `MaxDepth`, preserves object identity with `PreserveReferences`, or owns construction through `ConvertUsing`.
- **AM022 directionality**: fluent or same-block local configuration after `ReverseMap()` constrains only the reverse mapping direction; semantic AutoMapper ownership prevents lookalike methods from suppressing diagnostics.
- **Duplicate registrations**: ambiguous mapping pairs suppress recursion only when every registration for that direction is explicitly constrained.

### Validation

- AM022 analyzer and code-fix suite: **66** passed.
- Full suite on `net10.0`: **1401** passed, 0 skipped, 0 failed.

## [2.30.65] - 2026-07-08

AM004/AM006 same-document sibling recompute for aggregate code actions.

### Changed

- **AM004 / AM006**: Map-all, DoNotValidate-all, and Ignore-all recompute live unmapped siblings from a single property-token caret (AM011-style), so same-document lightbulbs no longer require multi-diagnostic pile-up.
- **AM006**: shared `GetUnmappedDestinationProperties` with the analyzer for ownership-accurate recompute.

### Validation

- Full suite on `net10.0`: **1390** passed.

## [2.30.64] - 2026-07-08

AM001 property-token diagnostic placement with aggregate-preserving sibling recompute.

### Changed

- **AM001**: diagnostics land on the **destination property identifier** (not CreateMap), with `MappingInvocationStart/Length` for code-fix routing.
- **AM001 fixer**: recomputes all type mismatches on the map so Convert-all / Ignore-all still work from a single property-token caret (CodeFixContext same-span rule).

### Validation

- Full suite on `net10.0`: **1386** passed.

## [2.30.63] - 2026-07-08

AM022 graph-aware Ignore for multi-type circular maps.

### Changed

- **AM022**: code fix Ignore actions use the analyzer's recursive destination-property graph when the destination has no same-type self-reference, so multi-type cycles (A→B→C→A) offer Ignore on the cycle edge (e.g. `BReference`) instead of MaxDepth-only.
- Lightbulb titles use "circular property" (covers self-ref and graph edges). MaxDepth(2) scaffold remains best-first.

### Validation

- AM022 suite + full solution tests on `net10.0`.

## [2.30.62] - 2026-07-08

Split multi-concept AM031 performance diagnostics into independent public rule IDs.

### Changed

- **AM031**: public ID now covers **multiple enumeration** only (cache rewrite + scaffold actions unchanged).
- **AM034–AM038**: expensive operation, expensive computation, sync-over-async, complex LINQ, and non-deterministic operations each get their own ID, severity, docs anchor, and catalog entry (shared analyzer/fixer implementation).
- **Migration**: suppressions / `.editorconfig` entries for the old umbrella `AM031` now only silence **multiple enumeration**. Configure `AM034`–`AM038` explicitly for expensive ops, computation, sync-over-async, complex LINQ, and non-determinism.

### Validation

- Full suite on `net10.0`: **1384** passed.
- AnalyzerVerifier catalog/snapshots updated.

## [2.30.61] - 2026-07-08

Fixer UX executable polish (Batch 3).

### Changed

- **AM032**: null-guard remains net48-compatible classic `if-throw` (analyzer still recognizes ThrowIfNull when written by hand).
- **AM031**: lightbulb order is Cache → Remove convention ForMember → Ignore (manual review).
- **AM003 / AM021**: keyword-escaped MapFrom sources; humanized conversion titles (quoted property names / short conversion labels).

### Validation

- Full solution test suite (`net10.0`) green: 1381 passed.

## [2.30.60] - 2026-07-08

Fixer UX shared infrastructure (Batch 2).

### Changed

- **AM001 multi-property lightbulb**: Convert-all (when every mismatch has a conversion recipe) and Ignore-all with nested "Fix individual type mismatch…".
- **AM022**: MaxDepth scaffold is always registered before Ignore (single- and multi-property).
- **Shared** `CodeFixSyntaxHelper.AddUsingIfMissing` used by AM003/AM021/AM031.

### Validation

- Full solution test suite (`net10.0`) green: 1381 passed.

## [2.30.59] - 2026-07-08

Fixer UX honesty hardening (Batch 1).

### Changed

- **Fixer UX honesty (Batch 1)**: code actions no longer oversell scaffolds or advertise silent no-ops.
  - **AM011**: "Map all" only when every required property has a unique fuzzy source match; otherwise offers `Scaffold maps for all N required properties (manual review)`. Ignore-all titles include `(manual review)`. Stale diagnostics that no longer match the live unmapped set withhold fixes instead of scaffolding a second `ForMember`.
  - **AM004 / AM006**: aggregate DoNotValidate-all / Ignore-all titles include `(manual review)`.
  - **AM022**: MaxDepth action title is `Add MaxDepth(2) scaffold (review depth)` to match catalog Scaffold trust.
  - **AM031**: Ignore/Remove only register when the `ForMember` peel target exists (no silent no-op apply).
  - **AM020 / AM021**: CreateMap-insert actions only register when a constructor or method block statement list can host the insert; method/ctor hosts require bare/`this` CreateMap (not `cfg.CreateMap`) so inserts stay compile-safe.

### Validation

- Full solution test suite (`net10.0`) green: 1380 passed.
- Codex review: meaningful UX improvement; receiver-qualified CreateMap insert withheld.

## [2.30.58] - 2026-07-08

AM001 correctness and fixer hardening from cross-model review.

### Fixed

- **AM001 ReverseMap dedup**: mismatch keys preserve conversion direction so bidirectional mismatches (e.g. `string↔int`) report and can be fixed on both sides.
- **AM001 Nullable&lt;T&gt; false negative**: `IsGenericTypeMismatch` only defers collection generics to AM021; scalar `Nullable` pairs like `double?→decimal?` report again. Complex-type ownership peels `Nullable` first so scalars are not misrouted to AM020.

### Changed

- **AM001 fixer**: peels nullable wrappers before conversion selection; emits invariant-culture numeric/`DateTime` `ToString`/`Parse`; escapes keyword property names in `MapFrom` bodies; adds framework scalar recipes (`DateTime`, `Uri`, `bool`, `Guid`, `DateOnly`, `TimeOnly`).

### Validation

- AM001 suite green (52 tests).
- Full solution test suite run as part of release.

## [2.30.57] - 2026-07-08

Full analyzer+fixer reanalysis hardening from four parallel domain audits against v2.30.56.

### Changed

- **AM003/AM021 container ownership**: shared `AutoMapperAnalysisHelpers.AreCollectionTypesIncompatible` covers HashSet/Queue/Stack/SortedSet/LinkedList/Immutable*/FrozenSet so combined container+element mismatches report AM003 only (with element-aware CreateRange fixes).
- **AM020 fixer parity**: uses shared public+internal `GetMappableProperties` and namespace-aware `IsCollectionType` so internal nested map diagnostics receive a real CreateMap fix.
- **AM021 simple conversion safety**: list/array/set Select rewrites apply the same string-source gate as dictionary axes for `DateTime`/`Guid.Parse`.
- **AM041 parenthesized ReverseMap**: `GetReverseMapInvocation` peels parentheses so `(CreateMap<S,D>()).ReverseMap()` registers reverse duplicates.
- **AM011 reverse fuzzy**: per-property fixes resolve types via `ResolveCreateMapTypesWithReverse` (same as aggregate).
- **AM031 multi-enum**: reports every multiply-enumerated source-rooted collection in a lambda (no longer first-key only).
- **AM005 keyword escape**: MapFrom source identifiers use `EscapeIdentifier`.
- **Catalog trust**: AM020 `LikelyRewrite`, AM022 `Scaffold`; docs for AM001 Parse, AM022 MaxDepth(2), AM006 aggregate UX, AM050 sibling withhold; remove fake `AM031.00x` editorconfig IDs.
- **Release metadata**: bumped package/docs version references to 2.30.57.

### Validation

- Full solution test suite (`net10.0`) green: 1367 passed.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` passed.

## [2.30.56] - 2026-07-08

Analyzer hitlist hardening from the 2026-07-08 improvement plan.

### Changed

- **AM004 unique-best fuzzy gate**: per-property and aggregate "Map all" fuzzy destination suggestions now use `FindUniqueBestFuzzyMatch` (same as AM006/AM011). Name-distance ties no longer pick the first candidate in symbol order, so ambiguous destinations only offer `DoNotValidate` / "DoNotValidate all".
- **AM032 nullable pass-through**: pure `return source` / `=> source` converters with a nullable destination no longer raise AM032 — null-preserving pass-through is intentional null handling.
- **AM003 sample isolation**: `TypeSafetyExamples` container-mismatch sample uses same-element `List<string>` → `HashSet<string>` so it no longer doubles as an AM021 element mismatch.
- **AM004 docs**: rule docs document unique-best fuzzy mapping, aggregate Map-all/DoNotValidate-all, nested per-property submenu, and property-token placement.
- **Trust tests**: AM001↔AM002 ownership conflict coverage; AM030 signature-depth regressions (wrong return type, missing `ResolutionContext`, non-public `Convert`).
- **Release metadata**: bumped package/docs version references to 2.30.56.

### Validation

- Full solution test suite (`net10.0`) green: 1363 passed.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` passed.

## [2.30.55] - 2026-07-06

Analyzer-health full reanalysis. No analyzer, fixer, or test source changed.

### Changed

- **analyzer-health.md reanalysis**: four parallel rule+fixer audits refreshed scorecard notes, added Fixer Trust Summary and Reanalysis Changelog, and promoted three P3 findings to P2 (AM003/AM004 docs drift, AM001↔AM002 conflict test, AM030 signature-depth tests). Planning Shortlist now has Medium-priority work for the next hardening pass.
- **Release metadata**: bumped package/docs version references to 2.30.55.

### Validation

- Full solution test suite (`net10.0`) green: 1352 passed.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` passed.

## [2.30.54] - 2026-06-13

Broad analyzer-health hardening pass across type-safety, data-integrity, complex-mapping, converter, performance, and configuration rules.

### Changed

- **Cross-rule explicit configuration precision**: expanded semantic handling for typed/string/`nameof(...)`/const `ForMember` and `ForPath` selectors, typed `ConvertUsing` lambdas, constructors, resolvers, converters, and reverse-map boundaries so intentional AutoMapper configuration suppresses the owning diagnostic without masking neighboring rules.
- **Analyzer and fixer safety hardening**: improved false-positive guards, diagnostic placement, metadata routing, and automatic code-fix selection across AM001/AM002/AM003/AM004/AM005/AM006/AM011/AM020/AM021/AM022/AM030/AM031/AM032/AM033/AM041/AM050, including safer manual-review boundaries where a rewrite could drop mapping policy.
- **Regression baseline expansion**: added focused analyzer/code-fix coverage for typed top-level `ForPath`, typed `ConvertUsing`, qualified converter null guards, reverse-map and duplicate-map boundaries, collection conversion axes, and exact BCL performance heuristics; refreshed `analyzer-health.md` with the current verification counts.

### Validation

- Full solution test suite (`net10.0`) green: 1352 passed.
- Focused affected analyzer slice (`net10.0`) green: 471 passed.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- Analyzer and test projects build clean under `-warnaserror` (the release gate; the samples project intentionally carries diagnostics).
- `net10.0` package smoke green: the packed analyzer loads in a temporary consumer and raises AM001 as an error.

## [2.30.53] - 2026-06-09

Tighten AM032 nullable-source converter analysis around conditional access so safe guard shapes stay quiet while unsafe null propagation reports.

### Changed

- **AM032 conditional-access precision**: conditional access no longer suppresses diagnostics when the maybe-null result is passed directly or through a simple local into known null-intolerant APIs or constructors, such as `DateTime.Parse(source?.Trim())`, `int.Parse(source?.Trim())`, `DateTime.Parse(trimmed)`, `new Uri(source?.Trim())`, or target-typed `new(source?.Trim())` in `Uri` converters. Null-tolerant TryParse fallback flows, including success branches that safely reuse `source`, nullable parse provider/style arguments, `string.Concat(...)`, and nullable constructor targets stay quiet, while explicit null coalesce fallbacks, null-forgiven `?? null!` fallbacks, coalesced guards whose null fallback enters an unsafe branch, and null checks that exist only inside nested local functions or lambdas no longer count as guarding the converter body.
- **AM032 guarded-local coverage**: split-assigned conditional-access locals, boolean guard locals, and null-branch source-free fallback assignments now count when they are guarded before source use, guards that run after an unsafe source dereference no longer suppress AM032, member dereferences on maybe-null conditional-access locals still report, and nullable-destination converters may safely fall back to returning the guarded local on the null path.

### Validation

- Focused AM030/AM032/AM033 test slice (`net10.0`) green: 121 passed.
- Full solution test suite (`net10.0`) green: 985 passed.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.

## [2.30.52] - 2026-06-08

Final step of the code-fix item-picker redesign: extend the aggregate + nested submenu pattern to the sibling unmapped-property rules and consolidate the shared logic.

### Changed

- **AM006 & AM004 aggregate + nested code fixes**: extended the AM011 pattern to AM006 (unmapped destination properties) and AM004 (source properties with no destination). When 2+ such properties pile onto one `CreateMap` — the case for compiled/metadata model types (e.g. DTOs from a referenced assembly), where every diagnostic anchors to the `CreateMap` — the lightbulb now offers **"Ignore all" / "DoNotValidate all"**, an optional **"Map all from/to similar properties"** (only when every property has a fuzzy match, so the title is honest), and a nested **"Fix individual…"** submenu, instead of one entry per property. The single-property case is unchanged (flat).
- The group → flat/nested/aggregate orchestration now lives in `AutoMapperCodeFixProviderBase.RegisterGroupedPerPropertyFixesAsync`, shared by AM011/AM006/AM004; AM011 was refactored onto it with identical behaviour. Fuzzy-matched identifiers interpolated into `MapFrom`/`ForSourceMember` are keyword-escaped on all three rules.

### Validation

- Full solution test suite (`net10.0`) green.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- Analyzer and test projects build clean under `-warnaserror` (the release gate; the samples project intentionally carries diagnostics).

## [2.30.51] - 2026-06-07

Second step of the code-fix item-picker redesign: collapse the per-property AM011 fixes under one submenu so the lightbulb stays short when many required properties are unmapped.

### Changed

- **AM011 nested per-property submenu**: when 2+ required destination properties are unmapped on one `CreateMap`, the per-property scaffold/ignore actions are now grouped under a single **"Fix individual required property…"** submenu (each property nested under its own `Required property 'X'` entry), shown alongside the aggregate **"Map all / Ignore all"** actions. The lightbulb now presents three top-level entries instead of two-per-property, directly addressing the "item picker is so long" problem for maps with many required members. The single-unmapped-property case is unchanged — it stays a flat scaffold + ignore choice.

### Validation

- Full solution test suite (`net10.0`) green.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- Analyzer and test projects build clean under `-warnaserror` (the release gate; the samples project intentionally carries diagnostics).

## [2.30.50] - 2026-06-07

First step of the code-fix item-picker redesign: aggregate code fixes so a CreateMap with many unmapped required properties can be fixed in a single action instead of one property at a time.

### Added

- **AM011 aggregate code fixes**: when 2+ required destination properties are unmapped on one `CreateMap`, the lightbulb now offers **"Map all N unmapped required properties"** and **"Ignore all N unmapped required properties"**. Each folds one chained `.ForMember(...)` edit covering every flagged property (fuzzy-matched source, else a scaffolded default for "Map all"; `opt.Ignore()` for "Ignore all") and applies it as a single replacement that clears every diagnostic at once. The single-unmapped-property case is unchanged — it keeps the existing per-property scaffold/ignore actions, so no aggregate noise is added when there is nothing to batch.

### Validation

- Full solution test suite (`net10.0`) green.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- Analyzer and test projects build clean under `-warnaserror` (the release gate; the samples project intentionally carries diagnostics).

## [2.30.49] - 2026-06-07

Two new detection capabilities. Both premises and the generated fixes were verified against a real AutoMapper 14 runtime probe.

### Added

- **AM021**: Decomposes dictionary `KeyValuePair<TKey, TValue>` element types into independent key/value axes. Removes a false positive on idiomatic dictionary mappings (`Dictionary<K, Foo>` → `Dictionary<K, FooDto>` maps correctly when `CreateMap<Foo, FooDto>` is registered — previously reported because the analyzer looked for a non-existent `CreateMap<KeyValuePair<…>, KeyValuePair<…>>`) and adds executable fixes where only manual-ignore existed: a `ToDictionary(…)` projection for simple primitive key/value conversions (gated so `DateTime`/`Guid` `Parse` targets require a string source) and a decomposed `CreateMap<TValueSource, TValueDest>()` for a complex value with a pass-through key (never a `KeyValuePair` map). Generic-collection axes (e.g. `List<int>` vs `List<string>`) and both-axes-incompatible dictionaries keep the manual-review ignore action only.
- **AM002**: Detects collection element nullability loss — a nullable reference-type element mapped to a non-nullable element of the same type (e.g. `List<string?>` → `List<string>`, `string?[]` → `string[]`). Scoped to convention-based mappings and reference-type elements of the same underlying type; element-type mismatches and value-type nullables stay with `AM021`/`AM001`. The diagnostic offers only the manual-review ignore action (a `?? default` scaffold cannot fix element-level nullability).

### Validation

- Full solution test suite (`net10.0`) green.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `dotnet build -warnaserror` clean.

## [2.30.48] - 2026-06-04

### Changed

- **AM021 code fixes**: Simple element-conversion fixes now cover destination `ImmutableArray<T>` collections with fully qualified `ImmutableArray.CreateRange(...)` factory mappings.
- **AM021 code fixes**: `List<string>` to `ImmutableArray<int>` mappings now get an executable `Select(...)` plus `global::System.Convert.ToInt32(...)` rewrite instead of only the manual ignore action.
- **Docs**: Updated AM021 rule docs and release metadata to include the `ImmutableArray<T>` element-conversion path.
- **Tests**: Added AM021 code-fix regression coverage for mutable source collections mapped to immutable-array destinations.

### Validation

- PR #123 checks green: Build/Test, package analyzer, package smoke tests for `net8.0`, `net9.0`, and `net10.0`, Codecov patch, and Claude review.
- Full solution test suite (`net10.0`) green — 859 tests, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` clean.

## [2.30.47] - 2026-06-04

### Changed

- **AM003**: Collection-container diagnostics now cover `ImmutableArray<T>` when either the source or destination side uses it with a different collection container.
- **AM003 code fixes**: Destination `ImmutableArray<T>` mappings now receive a fully qualified `ImmutableArray.CreateRange(...)` factory fix.
- **AM003 code fixes**: Source-side `ImmutableArray<T>` mappings to known mutable destinations keep the existing safe constructor conversion path.
- **Docs**: Updated AM003 rule docs to list `ImmutableArray<T>` with the existing immutable/frozen container boundary.
- **Tests**: Added analyzer and code-fix regression coverage for both `List<T>` → `ImmutableArray<T>` and `ImmutableArray<T>` → `List<T>`.

### Validation

- PR #122 checks green: Build/Test, package analyzer, package smoke tests for `net8.0`, `net9.0`, and `net10.0`, Codecov patch, and Claude review.
- Full solution test suite (`net10.0`) green — 858 tests, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` clean.

## [2.30.46] - 2026-06-04

### Changed

- **AM050**: Redundant `MapFrom` detection now covers top-level `ForPath(dest => dest.Name, opt => opt.MapFrom(src => src.Name))` mappings when source and destination member names and types match.
- **AM050**: The automatic cleanup removes redundant top-level `ForPath` calls when the options lambda contains only the redundant `MapFrom`, preserving the existing sibling-configuration safety boundary.
- **AM050**: Nested `ForPath` destination paths stay out of scope because convention mapping equivalence is not guaranteed.
- **Docs**: Updated AM050 documentation to describe the top-level `ForPath` support and nested-path boundary.
- **Tests**: Added analyzer and code-fix regression coverage for top-level `ForPath` reporting, nested `ForPath` suppression, and safe removal at the end of a chain.

### Validation

- PR #121 checks green: Build/Test, package analyzer, package smoke tests for `net8.0`, `net9.0`, and `net10.0`, Codecov patch, and Claude review.
- Full solution test suite (`net10.0`) green — 854 tests, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` clean.

## [2.30.45] - 2026-06-04

### Changed

- **AM031**: Multiple-enumeration tracking now treats LINQ `SequenceEqual` as a terminal enumeration call.
- **AM031**: `SequenceEqual` tracks both sequence inputs, including captured/local collections passed as the second sequence.
- **AM031**: Static `Enumerable`/`Queryable` terminal calls are keyed to their source sequence arguments instead of the static type name, avoiding false positives when different source collections are enumerated.
- **Docs**: Updated AM031 terminal-operator documentation to include `SequenceEqual` and static LINQ source-argument tracking.
- **Tests**: Added regression coverage for receiver, argument, static, captured, and distinct-source static LINQ mappings.

### Validation

- PR #120 checks green: Build/Test, package analyzer, package smoke tests for `net8.0`, `net9.0`, and `net10.0`, Codecov patch, and Claude review.
- Full solution test suite (`net10.0`) green — 851 tests, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` clean.

## [2.30.44] - 2026-06-04

### Changed

- **AM031**: Multiple-enumeration tracking now treats LINQ `Contains`, `ElementAt`, and `ElementAtOrDefault` as terminal enumeration calls.
- **AM031**: Instance `Contains` calls on common linear collection types such as `List<T>` are tracked so `Contains(...) && Sum()` reports on the shared source collection.
- **Docs**: Updated AM031 terminal-operator documentation to include the new operators.
- **Tests**: Added regression coverage for `Contains`/`ElementAt*` and `List<T>.Contains(...) && Sum()` mappings.

### Validation

- PR #119 checks green: Build/Test, package analyzer, package smoke tests for `net8.0`, `net9.0`, and `net10.0`, Codecov patch, and Claude review.
- Full solution test suite (`net10.0`) green — 845 tests, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` clean.

## [2.30.43] - 2026-06-04

### Changed

- **AM031**: Sync-over-async diagnostics now detect `GetAwaiter().GetResult()` in `MapFrom` expressions.
- **AM031**: Detection covers framework `Task`, configured `Task`, `ValueTask`, and configured `ValueTask` awaiter shapes.
- **Docs**: Broadened AM031 wording from `Task.Result` to the sync-over-async family and refreshed the sample diagnostics snapshot.
- **Tests**: Added regression coverage for direct and configured task/value-task awaiter `GetResult()` calls.

### Validation

- PR #118 checks green: Build/Test, package analyzer, package smoke tests for `net8.0`, `net9.0`, and `net10.0`, Codecov patch, and Claude review.
- Full solution test suite (`net10.0`) green — 843 tests, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` clean.

## [2.30.42] - 2026-06-04

### Changed

- **AM020**: Nested object mapping checks now use Roslyn conversion classification after same-type, built-in, and collection ownership checks, so compiler-known implicit nested conversions are treated as safe.
- **AM020**: Nested value-object or DTO properties with user-defined implicit conversions no longer report missing nested-map diagnostics.
- **Tests**: Added regression coverage proving implicit nested conversions stay quiet while explicit-only nested conversions still report.

### Validation

- PR #117 checks green: Build/Test, package analyzer, package smoke tests for `net8.0`, `net9.0`, and `net10.0`, Codecov patch, and Claude review.
- Full solution test suite (`net10.0`) green — 839 tests, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` clean.

## [2.30.41] - 2026-06-04

### Changed

- **AM021**: Collection element compatibility now uses Roslyn conversion classification after the existing analyzer-specific checks, so compiler-known implicit element conversions are treated as safe.
- **AM021**: Value-object collection elements with user-defined implicit conversions, such as `List<Money>` to `List<decimal>`, no longer report collection element mismatch diagnostics.
- **Tests**: Added regression coverage proving implicit user-defined element conversions stay quiet while explicit-only element conversions still report.

### Validation

- PR #116 checks green: Build/Test, package analyzer, package smoke tests for `net8.0`, `net9.0`, and `net10.0`, Codecov patch, and Claude review.
- Full solution test suite (`net10.0`) green — 837 tests, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` clean.

## [2.30.40] - 2026-06-03

### Changed

- **AM001**: Property compatibility now uses Roslyn conversion classification after the existing analyzer-specific checks, so compiler-known implicit conversions are treated as safe.
- **AM001**: Value-object properties with user-defined implicit conversions, such as `Money` to `decimal`, no longer report type mismatch diagnostics.
- **Tests**: Added regression coverage proving implicit user-defined conversions stay quiet while explicit-only conversions still report.

### Validation

- PR #115 checks green: Build/Test, package analyzer, package smoke tests for `net8.0`, `net9.0`, and `net10.0`, Codecov patch, and Claude review.
- Full solution test suite (`net10.0`) green — 835 tests, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` clean.

## [2.30.39] - 2026-06-03

### Changed

- **CI/CD**: Refreshed CI, release, and CodeQL workflow action pins to current major versions with Node.js 24-compatible releases.
- **CI/CD**: Updated checkout/setup-dotnet, Codecov, CodeQL, cache, upload-artifact, and GitHub release actions while preserving existing workflow behavior.

### Validation

- PR #114 checks green: Build/Test, package analyzer, package smoke tests for `net8.0`, `net9.0`, and `net10.0`, Codecov patch, and Claude review.
- Full solution test suite (`net10.0`) green — 833 tests, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` clean.

## [2.30.38] - 2026-06-03

### Changed

- **AM031**: Complex LINQ `SelectMany` diagnostics now require the invoked method to resolve to `System.Linq.Enumerable` or `System.Linq.Queryable`.
- **AM031**: User-defined `SelectMany` namesakes with nested selector invocations stay quiet instead of being treated as complex LINQ performance smells.
- **Tests**: Added regression coverage for a source mapping that calls a custom `SelectMany` extension with nested selector logic.

### Validation

- Targeted AM031 analyzer tests green.
- Full solution test suite (`net10.0`) green — 833 tests, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` clean.

## [2.30.37] - 2026-06-03

### Changed

- **AM001**: Numeric compatibility now follows the C# predefined implicit numeric conversion table instead of a simplified widening-level model.
- **AM001**: `double`/`float` to `decimal` mappings now report as property type mismatches because C# requires an explicit conversion, while valid widenings such as `char` to `int` stay quiet.
- **AM001 code fixes**: Explicit numeric conversions that now report still receive the existing cast-based `MapFrom` fix, with direct regression coverage for `double` to `decimal`.

### Validation

- Targeted AM001 analyzer and code-fix tests green.
- Full solution test suite (`net10.0`) green — 832 tests, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` clean.

## [2.30.36] - 2026-06-03

### Changed

- **AM002**: Nullable compatibility diagnostics now preserve constructed generic source/destination type names, so generic mappings report actionable labels such as `Source<T>` and `Destination<T>` instead of collapsing to the metadata type name.
- **AM002 code fixes**: Default-value scaffolds now use `default!` for generic/reference fallback defaults where plain `default` would remain nullable, keeping generated `MapFrom` expressions analyzer-clean.
- **Tests**: Added AM002 regression coverage for generic nullable type-parameter mappings and null-forgiving fallback flow.

### Validation

- Targeted AM002 analyzer and code-fix tests green.
- Full solution test suite (`net10.0`) green — 827 tests, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` clean.

## [2.30.35] - 2026-06-03

### Changed

- **AM030/AM032/AM033**: Split the mixed AM030 converter diagnostics into separate public IDs: AM030 now covers invalid converter implementations, AM032 covers nullable-source converter null handling, and AM033 covers unused converter declarations.
- **Code fixes**: The existing converter null-guard code fix now routes through AM032 with an AM032-prefixed equivalence key; AM030 and AM033 remain analyzer-only.
- **Trust metadata/docs**: Rule catalog entries, rule docs, samples, and analyzer-health notes now expose independent severities, documentation anchors, and fixer trust levels for each converter diagnostic.

### Validation

- Targeted AM030/AM032/AM033 analyzer and code-fix tests green.
- Full solution test suite (`net10.0`) green — 824 tests, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` clean.

## [2.30.34] - 2026-06-03

### Changed

- **Analyzer health**: Calibrated AM021's Tests score from 4 to 5 so it aligns with AM022 under the same audit rubric.
- **Audit evidence**: Recorded the current AM021/AM022 method counts behind the calibration: AM021 has 29 analyzer test methods plus 20 code-fix test methods, while AM022 has 29 analyzer test methods plus 16 code-fix test methods.

### Validation

- Targeted AM021 and AM022 test filters green.
- Full solution test suite (`net10.0`) green — 824 tests, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` clean.

## [2.30.33] - 2026-06-03

### Changed

- **AM003**: Collection-container incompatibility diagnostics now cover immutable/frozen destination containers (`ImmutableList<T>`, `ImmutableHashSet<T>`, and `FrozenSet<T>`) when the source collection is not already assignable to the destination contract.
- **AM003 code fixes**: Immutable/frozen destination fixes now emit fully qualified factory calls (`ImmutableList.CreateRange(...)`, `ImmutableHashSet.CreateRange(...)`, and `FrozenSet.ToFrozenSet(...)`) instead of unsafe constructors or manual-only fallbacks.
- **Tests**: Added AM003 analyzer and code-fix regression coverage for immutable/frozen destination containers, including element conversion through the existing safe conversion pipeline.

### Validation

- Full solution test suite (`net10.0`) green — 824 tests, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` clean.

## [2.30.32] - 2026-06-03

### Changed

- **AM004**: Missing destination-property diagnostics now report on the offending source property identifier instead of the owning `CreateMap` call, making high-volume reports easier to triage in editors and generated snapshots.
- **AM006**: Unmapped destination-property diagnostics now report on the offending destination property identifier while preserving the existing mapping-level fallback when source syntax is unavailable.
- **Code fixes (shared)**: AM004/AM006 diagnostics now carry mapping invocation span metadata, so their existing code fixes can still find and update the owning `CreateMap` chain after diagnostics move to property declarations.
- **Tests**: Shared code-fix verification now tolerates line-ending-only generated-source diffs, keeping Windows and LF checkout behavior equivalent while still failing real generated-code differences.

### Validation

- Full solution test suite (`net10.0`) green — 819 tests, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `git diff --check` clean.

## [2.30.31] - 2026-06-02

### Changed

- **AM001**: Same-width signed/unsigned numeric pairs (e.g. `uint` → `int`, `short` → `ushort`, `byte` → `sbyte`) are now reported as incompatible. They share a conversion "level" and C# has no implicit conversion between them, so the previous `<=` level comparison wrongly stayed silent. The numeric model now requires a strict widening; same-type pairs and `decimal`-as-widest semantics are unchanged.
- **Code fixes (shared)**: Property names that are C# keywords (e.g. `@class`, `@event`) are now escaped in generated `ForMember`/`MapFrom` selectors and expressions, so the produced code compiles instead of emitting `dest.class`.
- **AM020**: The generated nested `CreateMap<,>()` now uses namespace-qualified, generic-aware type names (`ToMinimalDisplayString`) instead of the bare type name, so cross-namespace and generic nested objects produce compilable fixes.
- **AM003**: The collection element conversion now emits a `(Dest)x` cast only when the source element is implicitly convertible to the destination (e.g. a derived-to-base upcast); unrelated element types fall back to manual review instead of a speculative cast that fails to compile or throws.
- **AM031**: `ValueTask<T>.Result` synchronous access inside a mapping expression is now detected alongside `Task<T>.Result`.
- **AM006**: The analyzer and fixer now use the same destination accessor filter, so a write-only unmapped destination property is handled consistently.
- **AM041**: The duplicate-mapping removal fix is withheld when the duplicate `CreateMap` is assigned to a variable (no `ExpressionStatement` to remove), instead of offering an action that does nothing.
- **AM050 / `CreateMapRegistry`**: The AM050 analyzer callback and helpers are now `static`, and `CreateMapRegistry.GetDuplicateMappings` returns an `IReadOnlyDictionary` rather than exposing its internal mutable dictionary.
- **Helpers**: `IsBuiltInType` is now namespace-qualified (a user type named `Guid`/`DateTime` is no longer treated as built-in); `GetMappableProperties` deduplicates with a `HashSet` (O(n) instead of O(n²)); removed the dead `AddPropertiesToTypeAsync` helper.

### Validation

- Full solution test suite (`net10.0`) green — 818 tests, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `dotnet build -warnaserror` clean.
- `codex review --uncommitted` green per change.

## [2.30.30] - 2026-05-15

### Changed

- Added AM021 simple element-conversion code fixes for immutable and frozen destination collections: `ImmutableList<T>`, `ImmutableHashSet<T>`, and `FrozenSet<T>`.
- The new AM021 rewrites use fully qualified `ImmutableList.CreateRange(...)`, `ImmutableHashSet.CreateRange(...)`, and `FrozenSet.ToFrozenSet(...)` calls around the existing `Select(...)` conversion pipeline, keeping generated mappings executable without relying on user imports.
- Locked custom immutable-lookalike destination collections onto the manual-review ignore path so the fixer does not regress into name-based speculative rewrites.

### Validation

- Targeted AM021 immutable/frozen code-fix tests.
- Full solution test suite (`net10.0`) green.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `codex review --uncommitted` green.

## [2.30.29] - 2026-05-15

### Changed

- Hardened AM003's collection-container code fix so unsupported custom collection destination types keep only the manual-review ignore action instead of receiving speculative constructor rewrites that may not compile.
- Preserved safe AM003 automatic conversions for known BCL collection destinations, including `List<T>`, `HashSet<T>`, `Queue<T>`, `Stack<T>`, `SortedSet<T>`, and `LinkedList<T>`, while keeping the manual-review ignore action available alongside those rewrites.
- Updated AM003 docs and analyzer-health notes to document the custom-collection fixer boundary.

### Validation

- Targeted AM003 code-fix tests.
- Full solution test suite (`net10.0`) green.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- `codex review --uncommitted` green.

## [2.30.28] - 2026-05-14

### Changed

- Hardened AM031's redundant-`ForMember` removal code fix so the automatic action is only offered when the existing mapping is already convention-equivalent: a direct `MapFrom(src => src.Member)` on a compatible same-name source/destination member. Transformed expressions such as `src.Score + 1`, captured same-name properties, service calls, and other mapping policy now keep only the manual-review action instead of being silently dropped.
- Hardened AM001's property-mismatch fixer so unrelated reference/framework conversions such as `Uri` to `string` stay on the manual-review path instead of receiving a speculative cast action that would not compile or would misrepresent the mapping policy.
- Updated AM031 docs and analyzer-health notes to document the narrower fixer boundary.

### Validation

- Targeted AM001 and AM031 code-fix tests.
- Full solution test suite (`net10.0`) green.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- PR CI/CD and Claude Code Review green.

## [2.30.27] - 2026-05-14

### Changed

- Added direct AM041 code-fix coverage for the duplicate-`CreateMap` removal action being withheld when the duplicate carries chained `ForPath(...)` configuration. The generic `IsCreateMapWithUnsafeChainedConfiguration` algorithm in `AM041_DuplicateMappingCodeFixProvider` already covers this shape (any chained call besides bare `.ReverseMap()` is unsafe to remove); the new test locks the contract so a future refactor narrowing the structural check to a known-method list (e.g. `ForMember` only) would now fail tests.

### Validation

- Targeted AM041 code-fix test slice.
- Full solution test suite (`net10.0`) green.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.

## [2.30.26] - 2026-05-14

### Changed

- Added direct AM050 code-fix test coverage for the three sibling member-options previously only covered by the generic `ForMemberLambdaContainsOnlyTheMapFrom` structural check: `PreCondition`, `UseDestinationValue`, and `Ignore`. The structural check already withholds the automatic `ForMember`-removal action whenever the options lambda contains anything besides the redundant `MapFrom`; the new tests lock that contract directly so a future refactor narrowing the structural check to a known-sibling list would fail tests.

### Validation

- Targeted AM050 code-fix tests (5 sibling-config cases now green: Condition, NullSubstitute, PreCondition, UseDestinationValue, Ignore).
- Full solution test suite (`net10.0`) green.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.

## [2.30.25] - 2026-05-14

### Changed

- Corrected six rule documentation `**Category**:` lines in `docs/DIAGNOSTIC_RULES.md` to match the descriptor categories actually shipped: AM002 (TypeSafety → NullSafety), AM011 (DataIntegrity → RequiredProperties), AM020 (ComplexMappings → NestedObjects), AM021 (ComplexMappings → Collections), AM022 (ComplexMappings → Recursion), AM030 (CustomConversions → Converters). The descriptors themselves were unchanged; the docs had drifted away from the shipped categories, which made `dotnet_diagnostic.*.category` lookups and `.editorconfig` category-based suppressions in user docs misleading.
- Added `RuleCatalogTests.RuleDocs_ShouldDocumentDescriptorCategories` as a trust drift guard that asserts the `**Category**:` line in each rule's documentation section names every distinct `descriptor.Category` for that rule. This mirrors the existing severity drift guard and prevents future drift in either direction.

### Validation

- Targeted `RuleCatalogTests` slice.
- Full solution test suite (`net10.0`) green.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.

## [2.30.24] - 2026-05-14

### Changed

- Marked the unwired `AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule` `DiagnosticDescriptor` field as `[Obsolete]`. The field is never included in `SupportedDiagnostics`, never tracked in `AnalyzerReleases.Shipped.md`, and contradicts the documented ownership boundary in `docs/ARCHITECTURE.md` (AM001/AM020/AM021 own missing-converter mapping diagnostics, not AM030). The descriptor is retained for binary compatibility but the Obsolete attribute and the new trust drift guard ensure the legacy intent is now explicit and the field cannot be silently revived.
- Marked the unwired `AM003_CollectionTypeIncompatibilityAnalyzer.CollectionElementIncompatibilityRule` `DiagnosticDescriptor` field as `[Obsolete]`. Same defect class: declared but never wired, ownership long since moved to `AM021_CollectionElementMismatchAnalyzer`'s identically named live descriptor.
- Added `RuleCatalogTests.Analyzers_ShouldRegisterEveryDeclaredDiagnosticDescriptor` as a trust drift guard that enforces a two-part contract: every `public static DiagnosticDescriptor` field on a shipped `DiagnosticAnalyzer` must appear in that analyzer's `SupportedDiagnostics` *or* be explicitly marked `[Obsolete]`; and no descriptor can be both registered and Obsolete. Future relics of this shape now fail loudly in tests rather than silently inflating the apparent rule surface.

### Validation

- Targeted AM003, AM030, and `RuleCatalogTests` test slices.
- Full solution test suite (`net10.0`) green.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.

## [2.30.23] - 2026-05-14

### Changed

- Hardened the AM050 redundant-`MapFrom` code fix so the automatic removal of the enclosing `ForMember(...)` is withheld when the `ForMember`'s options lambda contains sibling configuration besides the redundant `MapFrom` (such as `Condition`, `NullSubstitute`, `PreCondition`, `UseDestinationValue`, `Ignore`, or any other member-options call). Removing the whole `ForMember` would silently drop that sibling policy, which is exactly the kind of unsafe rewrite AM050's `SafeRewrite` catalog trust label was meant to avoid.
- The simple shapes `o => o.MapFrom(s => s.Member)` (single-expression lambda body) and `o => { o.MapFrom(s => s.Member); }` (block with the redundant `MapFrom` as the only statement) still receive the automatic `ForMember`-removal fix.
- AM050 still reports the redundant `MapFrom` in the unsafe case; only the automatic action is suppressed so the user removes the redundant call manually while preserving sibling configuration.

### Validation

- Targeted AM050 code-fix tests.
- Full solution test suite (`net10.0`) green at 778 passing, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.

## [2.30.22] - 2026-05-14

### Changed

- Hardened AM031 multiple-enumeration tracking so chained pre-terminal LINQ receivers normalise back to the source-rooted collection path. Shapes such as `src.Items.Where(x => x.Active).Count() + src.Items.Where(x => !x.Active).Any()` now correctly report instead of slipping past because each terminal had a different stringified receiver key (`src.Items.Where(x => x.Active)` vs `src.Items.Where(x => !x.Active)`).
- Receiver peeling is restricted to invocations that resolve to known lazy operators on `System.Linq.Enumerable`/`System.Linq.Queryable` (`Where`, `Select`, `SelectMany`, `OrderBy[Descending]`, `ThenBy[Descending]`, `GroupBy`, `Distinct`, `Skip[While]/SkipLast`, `Take[While]/TakeLast`, `Reverse`, `Cast`, `OfType`, `DefaultIfEmpty`).
- The peeled root is only adopted as the tracking key when it normalises to a source-parameter-rooted member path. Otherwise the original (un-peeled) receiver string is used, so chains rooted at arbitrary source method calls (`src.GetItems().Where(x).Count() + src.GetItems().Where(y).Any()`) stay distinct, alongside user-defined namesake extensions.
- Single chained-LINQ terminals over a source collection (e.g. `src.Items.Where(...).Count()`) still stay quiet — the normalisation only matters once two or more terminals enumerate the same source-rooted collection.

### Validation

- Targeted AM031 analyzer tests.
- Full solution test suite (`net10.0`) green at 774 passing, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.

## [2.30.21] - 2026-05-14

### Changed

- Hardened AM050 redundant-`MapFrom` detection so parenthesized and typed lambda shapes — `o.MapFrom((s) => s.Name)` and `o.MapFrom((Source s) => s.Name)` — are recognised alongside the existing simple `s => s.Name` shape. Destination lambdas inside `ForMember(d => ...)` accept the same shapes.
- Multi-parameter parenthesized lambdas continue to be ignored, so AutoMapper's `(src, ctx) => ...` `IMemberConfigurationExpression` overload still stays quiet.

### Validation

- Targeted AM050 analyzer tests.
- Full solution test suite (`net10.0`) green at 769 passing, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.

## [2.30.20] - 2026-05-14

### Changed

- Hardened AM030 converter null-handling detection so the modern guard clauses `ArgumentNullException.ThrowIfNull(source)`, `ArgumentException.ThrowIfNullOrEmpty(source)`, and `ArgumentException.ThrowIfNullOrWhiteSpace(source)` count as explicit null handling alongside the existing `== null`/`!= null`, null patterns, `string.IsNullOrEmpty`/`IsNullOrWhiteSpace`, null-coalescing, and conditional-access shapes.
- Recognition resolves the guarded value through the `argument:` named argument when present, so the named-argument shape `ThrowIfNull(paramName: nameof(source), argument: source)` also stays quiet.
- Guard calls whose `argument`/first positional argument is unrelated to the converter's source parameter (e.g. `ArgumentNullException.ThrowIfNull(context)`) still trigger AM030 so converters that genuinely miss null handling keep reporting.

### Validation

- Targeted AM030 analyzer tests.
- Full solution test suite (`net10.0`) green at 766 passing, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.

## [2.30.19] - 2026-05-14

### Changed

- Hardened AM030 unused-converter detection so a declared `ITypeConverter<TSource, TDestination>` implementation is no longer reported as unused when any `ConvertUsing(...)` argument resolves to the interface `ITypeConverter<TSource, TDestination>` itself. This closes a DI/service-provider false-positive where `public TestProfile(ITypeConverter<string, DateTime> converter)` or `services.Resolve<ITypeConverter<string, DateTime>>()` returns an interface-typed handle whose concrete class is supplied at runtime.
- Declared converters whose `<TSource, TDestination>` pair is not referenced by any `ConvertUsing` call (concrete or interface-typed) still report, so genuinely unused converters remain flagged.

### Validation

- Targeted AM030 analyzer tests.
- Full solution test suite (`net10.0`) green at 762 passing, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.

## [2.30.18] - 2026-05-14

### Changed

- Hardened AM031 multiple-enumeration tracking to cover the remaining commonly used terminal LINQ operators: `Min`, `Max`, `Aggregate`, `LongCount`, `Single`, `SingleOrDefault`, `ToHashSet`, `ToDictionary`, and `ToLookup`.
- Mapping shapes such as `src.Numbers.Min() + src.Numbers.Max()` and `src.Numbers.Single() + src.Numbers.ToHashSet().Count` now correctly report AM031 instead of going silent.
- Hardened AM031 enumeration tracking to require a `System.Linq.Enumerable` or `System.Linq.Queryable` containing type, so non-LINQ namesakes like `Math.Min`/`Math.Max` inside a `MapFrom` body no longer false-positive.
- Lazy/intermediate operators (`Where`, `Select`, `OrderBy`, `GroupBy`, `Distinct`, etc.) intentionally remain off the terminal-enumeration list so `src.Numbers.Where(...)` followed by a single terminal call still stays quiet.

### Validation

- Targeted AM031 analyzer tests.
- Full solution test suite (`net10.0`) green at 759 passing, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.

## [2.30.17] - 2026-05-14

### Changed

- Hardened AM041 so duplicate `CreateMap<TSource, TDestination>()` registrations carrying chained `ForMember`, `ForPath`, or other configuration no longer offer an automatic removal that would silently drop mapping policy.
- Hardened AM041 so parenthesized chained `CreateMap<TSource, TDestination>()` duplicates and duplicates whose chain continues past `.ReverseMap()` stay on the manual-review path.
- The bare `CreateMap<TSource, TDestination>().ReverseMap()` reversal path remains unchanged and still receives the automatic type-swap action.

### Validation

- Targeted AM041 analyzer and code fix tests.
- Full solution test suite (`net10.0`) green at 755 passing, 0 skipped.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.

## [2.30.16] - 2026-05-07

### Changed

- Hardened AM006 so destination properties initialized through `ConstructUsing`, or fully owned through `ConvertUsing`, no longer report as unmapped while partial `ConstructUsing` maps still report untouched destination members.
- Hardened AM006 so every block-bodied `ConstructUsing` return must initialize a destination member before AM006 treats it as mapped.
- Hardened AM006 so repeated `ConstructUsing` configuration is judged from the later effective constructor expression.
- Updated AM006 docs and analyzer health notes to document the property-specific construction and conversion suppression boundary.
- Hardened AM041 so duplicate `ReverseMap()` diagnostics with chained reverse-side configuration do not offer an unsafe automatic removal.
- Hardened AM041 so parenthesized chained `ReverseMap()` configuration is also kept on the manual-review path.
- Hardened AM002 so pass-through `MapFrom(src => src.NullableMember)` no longer hides nullable-to-non-nullable diagnostics.
- Hardened AM002 so `MapFrom` expressions that dereference nullable receivers still report instead of being trusted solely because the final return type is non-nullable.
- Hardened AM002 so different-source nullable receiver dereferences such as `src.OtherName.Trim()` still report and name the nullable source member.
- Hardened AM002 so nullable receiver dereferences still report when the dereferenced expression returns a different destination type, such as `src.Name.Length`.
- Hardened AM002 so guarded nullable dereferences and nullable value `GetValueOrDefault()` remain recognized as safe explicit mappings.
- Hardened the AM002 default-value fixer so it replaces an unsafe existing member mapping instead of adding a second mapping that can be overridden.
- Hardened the AM002 default-value fixer so unsafe nullable receiver dereferences do not receive a misleading `expr ?? default` action after the dereference.
- Hardened the AM002 default-value fixer so generated `DateTime`, `DateTimeOffset`, and `Guid` defaults are fully qualified and do not depend on `using System`.
- Hardened AM002 so repeated destination-member configuration is analyzed and fixed from the later effective mapping instead of an earlier overridden mapping.
- Hardened AM002 so explicit mappings from a different nullable source member still report, helper methods named `Ignore` or `NullSubstitute` inside `MapFrom` bodies do not get mistaken for AutoMapper null-handling options, and the default-value fixer preserves existing member options and source expressions when it adds null handling.
- Hardened AM002 diagnostics so explicit different-source mappings name the actual nullable source member in the message.
- Hardened AM002 so generic expression overloads such as `MapFrom<TSourceMember>(...)` are inspected instead of treated as opaque resolver ownership.
- Hardened AM002 so assignable `NullSubstitute` fallback values are treated as safe even when their expression type is a derived type.
- Hardened AM002 so typed value-type defaults such as `NullSubstitute(default(int))` are treated as safe while nullable/reference defaults still report.
- Hardened AM002 so `NullSubstitute` does not hide unsafe nullable receiver dereferences inside an explicit `MapFrom` expression.
- Hardened AM002 so helper calls named `MapFrom` inside member options are not mistaken for AutoMapper mapping configuration.
- Hardened AM002 so custom resolver forms such as `MapFrom<TResolver>()` are treated as explicit mapping configuration instead of falling back to same-name convention nullability.
- Hardened AM002 so custom member resolver forms such as `MapFrom<TResolver, TSourceMember>(...)` also stay on the explicit resolver path.
- Hardened AM002 so member-level value converters such as `ConvertUsing<TConverter, TSourceMember>(...)` are treated as explicit mapping ownership.
- Hardened AM002 so `NullSubstitute(null)` and `NullSubstitute(default)` no longer suppress nullable-to-non-nullable diagnostics.
- Hardened the AM002 default-value fixer so existing child `ForPath` mappings are not reused as top-level null-handling targets and generated `MapFrom` lambdas avoid parameter-name collisions.
- Hardened the AM002 default-value fixer so existing `Condition`/`PreCondition` guards keep the default-value action off the automatic path.
- Hardened AM002 so child-only `ForPath` configuration does not suppress nullable top-level parent mappings.
- Hardened AM002 so top-level `ForPath` null handling is respected and unsafe top-level `ForPath` mappings are fixed in place.
- Hardened AM021 simple conversion fixes so generated `Convert`, `DateTime`, and `Guid` calls use fully qualified `global::System` APIs and no longer depend on `using System`.
- Hardened AM031 so `ForPath(... MapFrom(...))` expressions are analyzed alongside `ForMember`, with nested destination paths preserved in diagnostics.
- Hardened AM031 code fixes so `ForPath` diagnostics do not offer unsafe automatic rewrites; expression-tree `ForPath.MapFrom` cases stay analyzer-only.

### Validation

- Targeted AM006 analyzer tests.
- Targeted AM041 code fix tests.
- Targeted AM002 analyzer and code fix tests.
- Targeted AM021 analyzer and code fix tests.
- Targeted AM031 analyzer and code fix tests.

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

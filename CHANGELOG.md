# Changelog

## Unreleased

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

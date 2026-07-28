# Test Limitations

This project should not carry silent skipped tests. Historical `[Fact(Skip = ...)]` cases have been converted into one of:

- normal regression tests when the harness can now model the scenario;
- negative tests when the old skipped case described invalid analyzer behavior;
- documented warning-baseline entries in `docs/WARNING_BASELINE.md` when the limitation belongs to analyzer-test scaffolding rather than production analyzer behavior.

Known harness caveats remain documented in the test project warning baseline:

- analyzer-test helper types intentionally trigger Roslyn analyzer-authoring warnings;
- trust validation tests intentionally read repository files;
- AutoMapper 14 remains pinned for compatibility coverage while AutoMapper 15 introduces licensing/API changes.

## Analyzer throughput

`Performance/AnalyzerThroughputTests` measures the whole pack over a synthetic solution-sized fixture
(60 mapped type pairs, mixed nullable/collection/nested/enum/required shapes) and over a cyclic diamond
type graph that exercises AM022's traversal.

Nothing else here measures analyzer cost — every other test asserts diagnostics — so a change making the
pack several times slower would ship silently, on code that runs on every keystroke in every consuming
solution.

**The budget is a ratio, not a wall clock.** Analysis is timed against compiling the same input on the
same machine, so the measurement moves with the runner instead of against it. An absolute threshold
measures the CI agent more than the analyzers and is the usual reason timing tests get deleted.

Two methodology points, both learned by getting them wrong first:

- Roslyn memoises `GetDiagnostics()`, so timing a warmed compile against a fresh analyzer run measures
  caching. Each side gets its own cold `Compilation`, after a discarded warm-up pass for JIT.
- A fixture that produces no diagnostics makes the timing meaningless. The tests assert AM diagnostics
  were produced; that guard caught the diamond fixture being a DAG rather than cyclic, which meant AM022
  — the analyzer it exists to stress — never ran.

Three fixtures are measured. The mapping-dense one (60 type pairs) and the cyclic diamond stress
specific analyzers; the **mostly-unrelated** fixture (~2400 ordinary method calls around 60 mapped
pairs) is the realistic consumer shape, because every analyzer registers on `InvocationExpression` and
therefore visits every call in a solution, the vast majority of which have nothing to do with mapping.

Recorded baseline on this tree: solution-sized 2.8x–5.9x, cyclic diamond ~1.5x–1.9x, mostly-unrelated
~2.0x–2.3x across repeated runs on one machine. The budget is **20x**, roughly 3x above the noisy high, to catch an order-of-magnitude
regression without firing on runner noise. Tighten only with evidence from a quiet machine.

## Analyzer crash safety

`Robustness/AnalyzerCrashSafetyTests` drives every catalogued analyzer over code that does not compile,
is half-typed, or is hostile in shape — incomplete generic argument lists, dangling fluent chains,
selectors that select nothing, unresolved converters, open-generic `typeof`, and a type graph that is
cyclic through several shapes at once — and fails if any analyzer throws.

Per-rule tests feed analyzers well-formed code because they are testing diagnostics. An analyzer runs on
every keystroke, so it spends much of its life reading incomplete syntax and error types. An exception
there surfaces as `AD0001` in the user's Error List, and one AD0001 discredits all 23 rules at once
because the user cannot tell which analyzer misbehaved.

Exceptions are captured through `onAnalyzerException` rather than read from `AD0001`, so the result does
not depend on the compilation's diagnostic options. **There is deliberately no try/catch wrapper in the
analyzers**: Roslyn already contains analyzer exceptions, and swallowing them locally would hide real
defects from this suite while making the IDE quieter. These tests exist to find crashes, not mask them.

No crashes were found on the current tree. The suite was verified against an injected exception to
confirm it fails, with the offending analyzer named, rather than passing vacuously.

## Third-party corpus scanning

Every other verification path here reads code this project authored: the samples project, the test
suite, the snapshot baselines. That is a closed loop — it cannot surface a false positive nobody
imagined. The `IncludeMembers` defect fixed in 2.30.88 was an Error-severity build-breaker on a
documented AutoMapper feature and sat unnoticed through thirty-plus releases, because no third-party
mapping profile had ever been compiled against the analyzers.

`dotnet run --project tools/AnalyzerVerifier -- --scan-corpus <project-or-solution>` runs every
catalogued analyzer over an external codebase and reports what they say, with an optional JSON report.

It refuses to overstate coverage. Projects that fail to load, fail to compile, or crash an analyzer
(`AD0001`) are recorded and excluded from the scanned count, and the command exits non-zero rather than
returning a clean-looking report built on an incomplete semantic model.

**Scanning found a real defect immediately.** Against AutoMapper.Collection it reported AM041 seventeen
times for `CreateMap` calls in separate, independent `MapperConfiguration` instances — not duplicates.
That was reproduced with a focused test and is tracked as a false positive to fix.

**No CI corpus is wired yet, deliberately.** The obvious candidates — AutoMapper's own MIT-licensed
extension repositories — do not compile from a clean checkout at a pinned SHA: they reference
`AutoMapper.Internal`, which does not resolve under the `[15.0.1, 17.0.0)` AutoMapper range their own
manifests select. A scheduled job over those targets would upload zero-coverage reports that look like
clean scans, which is worse than no job. Identifying buildable pinned targets is outstanding work; the
tool is usable locally against any project today.

## Runtime verification of code-fix output

Fixer tests assert string equality between the produced document and a hand-written expected document.
That proves a fix matches what the test author wrote down; it does not prove the result is a mapping
AutoMapper accepts. The 2.30.83 `Stack<T>` ordering defect is the failure class: syntactically valid,
compile-clean, matching its expected text, and wrong at runtime.

`Infrastructure/CodeFixRuntimeVerifier` closes that gap by compiling fix output against the real
AutoMapper assembly, loading it, registering every declared `Profile`, and calling
`AssertConfigurationIsValid()`. `MapThroughFixedCode` additionally executes a mapping so ordering and
conversion behaviour can be asserted on real values.

`CodeFixRuntimeVerificationTests` proves the harness fails for the right reasons — output that does not
compile, output AutoMapper rejects semantically, and incorrect `Stack<T>` ordering — and runs the real
AM011 fixer end to end, executing what it produces rather than comparing it to text.

**Rollout is partial.** The harness exists and is proven; the 16 shipped fixers are not yet all routed
through it. Extending coverage fixer by fixer is follow-on work.

## Documented analyzer boundaries

`IncludeMembers` resolution (AM004/AM006/AM011) models only the directions that can **suppress** a
diagnostic: the included type's declared shape, its flattening convention, and an explicit
`ForMember`/`ForPath(... MapFrom(...))` on the uniquely registered child map.

It deliberately does **not** infer that a child map *fails* to supply a member — for example when the
child map ignores it via `ForMember(... Ignore())` or `ForAllMembers(... Ignore())`. AutoMapper does
reject those configurations at startup, so this is a real false negative, and
`IncludeMembersTests.*_KnownLimitation` assert the shipped behaviour rather than the ideal one.

### Deferred configuration through a mapping local (pre-existing, repo-wide)

`IncludeMembers` resolution walks the fluent chain rooted at the `CreateMap` invocation, so configuration
applied later through a local — `var map = CreateMap<S, D>(); map.IncludeMembers(s => s.Inner);` — is not
seen and the mapping is analysed as if it had no include.

This boundary is **not specific to `IncludeMembers`**. The same chain walk backs `ForMember`, `ForPath`,
`Ignore`, and `ForSourceMember` detection, and AM011 already reports a required member that a deferred
`map.ForMember(...)` supplies. Verified on this tree with a throwaway probe against code this change does
not touch. Closing it means resolving configuration by mapping symbol rather than by syntax ancestry —
shared mapping-model work that would change behaviour for every rule, not a fix belonging to this helper.

The same asymmetry governs selector interpretation. Only the plain member-access form (`s => s.Inner`,
optionally parenthesised or null-forgiven) is resolved; casts, explicit params arrays, collection
expressions, spreads, variables, and method calls all fail closed and suppress the mapping's diagnostics.
`IncludeMembersTests.*_FailsClosed` assert that behaviour. This over-suppresses on exotic selectors, but
suppression cannot break a consumer build whereas misreading a selector can.

The reason is asymmetric risk. Proving a member *is* supplied can only remove a diagnostic. Inferring
that one is *not* supplied adds diagnostics, and an approximation of the child map's member resolution
produced Error-severity false positives on valid mappings during review. Closing this properly requires
modelling child-map member resolution end to end (`ForMember`/`MapFrom`, `ForAllMembers`,
reverse-generated registrations, semantically bound `Ignore`), which belongs with a shared mapping model
rather than this helper.

The full suite is expected to run with `0` skipped tests.

# Test Limitations

This project should not carry silent skipped tests. Historical `[Fact(Skip = ...)]` cases have been converted into one of:

- normal regression tests when the harness can now model the scenario;
- negative tests when the old skipped case described invalid analyzer behavior;
- documented warning-baseline entries in `docs/WARNING_BASELINE.md` when the limitation belongs to analyzer-test scaffolding rather than production analyzer behavior.

Known harness caveats remain documented in the test project warning baseline:

- analyzer-test helper types intentionally trigger Roslyn analyzer-authoring warnings;
- trust validation tests intentionally read repository files;
- AutoMapper 14 remains pinned for compatibility coverage while AutoMapper 15 introduces licensing/API changes.

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

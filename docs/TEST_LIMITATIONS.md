# Test Limitations

This project should not carry silent skipped tests. Historical `[Fact(Skip = ...)]` cases have been converted into one of:

- normal regression tests when the harness can now model the scenario;
- negative tests when the old skipped case described invalid analyzer behavior;
- documented warning-baseline entries in `docs/WARNING_BASELINE.md` when the limitation belongs to analyzer-test scaffolding rather than production analyzer behavior.

Known harness caveats remain documented in the test project warning baseline:

- analyzer-test helper types intentionally trigger Roslyn analyzer-authoring warnings;
- trust validation tests intentionally read repository files;
- AutoMapper 14 remains pinned for compatibility coverage while AutoMapper 15 introduces licensing/API changes.

The full suite is expected to run with `0` skipped tests.

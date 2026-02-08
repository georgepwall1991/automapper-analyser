# Test Limitations

This document tracks known test limitations referenced by `[Fact(Skip = ...)]` messages.

1. **Field/dependency type resolution in analyzer tests**
   Some scenarios that rely on injected service fields (for example `_db`, `_service`) are difficult to model reliably in current analyzer test harness setups and can produce unstable symbol resolution.

2. **Code-fix context span/registration constraints**
   Some Roslyn testing combinations reject otherwise valid code-fix registrations when diagnostics are reported on larger spans or chain expressions. These scenarios are avoided in current integration tests until harness behavior is standardized.

3. **AM001 advanced expression-tree conversion patterns**
   Certain string-to-numeric conversion patterns and multi-diagnostic coordination paths are not consistently fixable in a deterministic single-pass code-fix test; those cases remain skipped until analyzer/fixer coordination is refactored.

4. **Invalid-converter and compiler-error co-reporting**
   Tests that intentionally create invalid converter implementations may emit both analyzer diagnostics and compiler errors, requiring explicit dual-diagnostic expectations and careful harness setup.

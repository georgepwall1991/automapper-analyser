# Warning Baseline

CI treats unexpected analyzer/test-project warnings as errors for the analyzer and test projects.

The test project intentionally suppresses a small baseline:

| Warning | Reason |
| --- | --- |
| `NU1903` | Tests and samples pin AutoMapper 14 for compatibility while AutoMapper 15 has licensing/API changes. |
| `RS1035` | Trust validation tests intentionally read repository files to detect docs/package drift. |
| `RS1038`, `RS1041`, `RS2008` | Test-only analyzer scaffolding and descriptor helpers are not shipped analyzer implementations. |
| `CA1305`, `CA1861` | Test source builders and assertion helpers prefer readability over production globalization/performance guidance. |

Production analyzer builds are expected to pass with `0` warnings under `-warnaserror`.

The production analyzer project intentionally suppresses a narrow authoring baseline:

| Warning | Reason |
| --- | --- |
| `RS1038` | The analyzer intentionally references compiler API shapes required for broad Roslyn compatibility. |
| `RS2001` | Several stable public rule IDs intentionally group multiple descriptors or legacy descriptors with different default severities. Release tracking is enabled through `AnalyzerReleases.Shipped.md`, but Roslyn's release-tracking metadata is one shape per rule ID. |

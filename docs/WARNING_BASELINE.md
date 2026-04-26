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

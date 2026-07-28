# AutoMapper Roslyn Analyzer

**Compile-time AutoMapper CreateMap validation for .NET** — a high-signal Roslyn analyzer that checks `Profile`, `CreateMap`, `ForMember`, `MapFrom`, `ReverseMap`, `ConvertUsing`, and `ProjectTo` configurations so type mismatches, nullable holes, unmapped required members, and missing registrations fail in the editor and CI, not as `AutoMapperMappingException` in production.

[![NuGet Version](https://img.shields.io/nuget/v/AutoMapperAnalyzer.Analyzers.svg?style=flat-square&logo=nuget&label=NuGet)](https://www.nuget.org/packages/AutoMapperAnalyzer.Analyzers/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/AutoMapperAnalyzer.Analyzers.svg?style=flat-square&logo=nuget&label=Downloads)](https://www.nuget.org/packages/AutoMapperAnalyzer.Analyzers/)
[![Build Status](https://img.shields.io/github/actions/workflow/status/georgepwall1991/automapper-analyser/ci.yml?style=flat-square&logo=github&label=Build)](https://github.com/georgepwall1991/automapper-analyser/actions)
[![Tests](https://img.shields.io/badge/Tests-1771%20passing%2C%200%20skipped-success?style=flat-square&logo=checkmarx)](https://github.com/georgepwall1991/automapper-analyser/actions)
[![.NET](https://img.shields.io/badge/.NET-4.8+%20%7C%206.0+%20%7C%208.0+%20%7C%209.0+%20%7C%2010.0+-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![Compatibility](https://img.shields.io/badge/Verified-net48%20%7C%20net6%20%7C%20net8%20%7C%20net9%20%7C%20net10-512BD4?style=flat-square&logo=dotnet)](docs/COMPATIBILITY.md)
[![License](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)
[![Coverage](https://img.shields.io/codecov/c/github/georgepwall1991/automapper-analyser?style=flat-square&logo=codecov&label=Coverage)](https://codecov.io/gh/georgepwall1991/automapper-analyser)

Catch AutoMapper configuration errors at compile-time before they cause runtime chaos.

## The problem

AutoMapper is powerful, but many mapping failures compile cleanly. A property type mismatch on `CreateMap`, a nullable source into a non-nullable destination, an unmapped required member, a nested object without its own map, or a `Map`/`ProjectTo` call with no reachable registration typically surfaces as a runtime exception, silent default, or production `NullReferenceException` — not as a compile error.

Runtime tests and manual review miss what static analysis can prove from your mapping configuration.

## What it catches

AutoMapperAnalyzer reports high-signal mapping problems early:

- property type mismatches on `CreateMap` (AM001) with conversion or Ignore code fixes
- nullable → non-nullable mappings that risk null failures (AM002)
- incompatible collection containers and element types (AM003 / AM021)
- missing source members, unmapped destinations, case-only name drift, and required members (AM004 / AM005 / AM006 / AM011)
- nested object maps that need an extra `CreateMap` (AM020)
- circular reference risk without `MaxDepth` / `PreserveReferences` (AM022)
- invalid, null-unsafe, or unused type converters (AM030 / AM032 / AM033)
- expensive or multi-enumeration mapping expressions (AM031 / AM034–AM038)
- duplicate registrations, redundant `MapFrom`, and unregistered `Map`/`ProjectTo` pairs (AM041 / AM050 / AM060)
- enum member name/value misalignment that silently corrupts mapped data (AM061)

When the analyzer cannot prove a mapping shape statically, it **stays quiet**. High-signal feedback, not noisy guesses. Code fixes are withheld when a rewrite would be side-effecting, incomplete, or behavior-changing without review.

## Install

```xml
<PackageReference Include="AutoMapperAnalyzer.Analyzers" Version="2.30.90">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

Or:

```bash
dotnet add package AutoMapperAnalyzer.Analyzers --version 2.30.90
```

**No runtime dependency** is added to your app. The package is a development-time Roslyn analyzer (plus code fixes). Open any file with AutoMapper configuration and diagnostics appear in supported IDEs and `dotnet build`.

See the [compatibility contract](docs/COMPATIBILITY.md) for verified target-framework and AutoMapper version pairings.

## See it work

Product-flow diagrams from real sample diagnostics (`tests/.../sample-diagnostics.json`):

### 1. Build / IDE diagnostics (CreateMap validation)

![AutoMapper Roslyn analyzer warnings for CreateMap validation — AM001 type mismatch, AM002 nullable, AM011 required property, AM060 unregistered Map/ProjectTo](https://raw.githubusercontent.com/georgepwall1991/automapper-analyser/main/assets/flow-ide-diagnostics.svg)

### 2. Before / after code fix (property type mismatch)

![Before and after: AutoMapper CreateMap string-to-int Age mismatch fixed with ForMember MapFrom TryParse (AM001)](https://raw.githubusercontent.com/georgepwall1991/automapper-analyser/main/assets/flow-before-after-fix.svg)

### 3. IDE + CI product loop

![AutoMapper analyzer product loop: IDE lightbulb diagnostics and CI package/sample compatibility gates for CreateMap Profiles](https://raw.githubusercontent.com/georgepwall1991/automapper-analyser/main/assets/flow-analyzer-ci-loop.svg)

## 30-second path

1. Reference the package with `PrivateAssets="all"`.
2. Keep your existing profiles, for example:

```csharp
public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<User, UserDto>();
    }
}
```

3. Build in the IDE or with `ContinuousIntegrationBuild=true` on the command line so analyzers run under this repo’s local defaults.
4. Fix any `AMxxx` diagnostics (most rules offer code fixes; review scaffold / likely-rewrite actions).
5. Optionally open the [samples project](samples/AutoMapperAnalyzer.Samples) to see the full rule set in one place:

```bash
dotnet build samples/AutoMapperAnalyzer.Samples
```

## Feature snapshot

| Area | What the analyzer does |
|------|------------------------|
| Type safety | Flags property type mismatches, nullable polarity, and collection container incompatibility. |
| Data integrity | Detects missing source members, unmapped destinations, case-only drift, and required members. |
| Nested / graph maps | Requires nested `CreateMap` pairs, analyzes collection elements, and warns on recursion risk. |
| Converters | Validates `ITypeConverter` / `IValueConverter` implementation, null handling, and usage. |
| Performance | Flags multi-enumeration, expensive work, sync-over-async, heavy LINQ, and non-determinism in maps. |
| Configuration | Finds duplicate `CreateMap`, redundant `MapFrom`, and `Map`/`ProjectTo` without a reachable registration. |
| Code fixes | Offers conversion, Ignore, nested-map, and scaffolding actions when a rewrite is safe enough to propose. |
| Honesty | Stays quiet when configuration is unprovable; withholds fixes that would drop sibling options or side effects. |

## Compatibility

| Platform | Version | AutoMapper (verified) |
|----------|---------|------------------------|
| .NET Framework | 4.8+ | 10.1.1+ |
| .NET | 6.0+ | 12.0.1+ |
| .NET | 8.0+ / 9.0+ / 10.0+ | 14.0.0+ |

Analyzer targets **.NET Standard 2.0**. Matrix details: [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md).

---

## Latest Release: v2.30.90

**Severity presets for brownfield adoption**

- Ships **`AutoMapperAnalyzer.Minimal.globalconfig`** (no rule reported at error severity) and **`AutoMapperAnalyzer.Recommended.globalconfig`** (shipped defaults, written out). Five rules default to `error`, so enabling 23 rules at once could break a large existing build with no documented ramp.
- Reference via `GlobalAnalyzerConfigFiles` with `GeneratePathProperty="true"`, or copy the lines into `.editorconfig`. See [Adopting in an existing codebase](#adopting-in-an-existing-codebase).
- Drift tests keep both presets in lockstep with the rule catalog. No analyzer rule ID, severity, or behaviour changes.

### Recent highlights

- **v2.30.90**: Severity presets (`Minimal`/`Recommended`) for brownfield adoption.
- **v2.30.89**: AutoMapper 15 and 16 added to the verified compatibility contract.
- **v2.30.88**: `IncludeMembers` awareness — AM004, AM006, and AM011 no longer report members supplied by an included source member.
- **v2.30.87**: Discoverability assets (metadata, funnel README, product-flow SVGs, pack verify).
- **v2.30.86**: AM020 capture-once computed receivers for nested-map fixes.
- **v2.30.85**: AM021 withholds ineffective element-map fixes for nested collection/array shapes.
- **v2.30.84**: AM060 unregistered `Map`/`ProjectTo`; AM061 enum member mismatch.
- **v2.30.62**: Performance rules split into AM031 + AM034–AM038.

Full history: [CHANGELOG.md](CHANGELOG.md).

---

## Complete Analyzer Coverage

| Rule | Description | Analyzer | Code Fix | Severity |
|------|-------------|----------|----------|----------|
| **Type Safety** | | | | |
| AM001 | Property Type Mismatch | ✅ | ✅ | Error |
| AM002 | Nullable→Non-nullable | ✅ | ✅ | Error / Info |
| AM003 | Collection Incompatibility | ✅ | ✅ | Error |
| AM061 | Enum Member Mismatch | ✅ | ✅ | Warning |
| **Data Integrity** | | | | |
| AM004 | Missing Destination Property | ✅ | ✅ | Warning |
| AM005 | Case Sensitivity Issues | ✅ | ✅ | Warning |
| AM006 | Unmapped Destination Property | ✅ | ✅ | Info |
| AM011 | Required Property Missing | ✅ | ✅ | Error |
| **Complex Mappings** | | | | |
| AM020 | Nested Object Issues | ✅ | ✅ | Warning |
| AM021 | Collection Element Mismatch | ✅ | ✅ | Warning |
| AM022 | Circular Reference Risk | ✅ | ✅ | Warning |
| AM030 | Invalid Type Converter Implementation | ✅ | — | Error |
| AM032 | Type Converter Null Handling | ✅ | ✅ | Warning |
| AM033 | Unused Type Converter | ✅ | — | Info |
| **Performance** | | | | |
| AM031 | Multiple Enumeration | ✅ | ✅ | Warning |
| AM034 | Expensive Operation | ✅ | ✅ | Warning |
| AM035 | Expensive Computation | ✅ | ✅ | Warning |
| AM036 | Sync-Over-Async | ✅ | ✅ | Warning |
| AM037 | Complex LINQ | ✅ | ✅ | Warning |
| AM038 | Non-Deterministic Operation | ✅ | ✅ | Info |
| **Configuration** | | | | |
| AM041 | Duplicate Mapping Registration | ✅ | ✅ | Warning |
| AM050 | Redundant MapFrom | ✅ | ✅ | Info |
| AM060 | Unregistered Type Map | ✅ | ✅ | Warning |

---

## Example: one CreateMap, many issues

```csharp
public class UserEntity
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Tags { get; set; }
    public Address HomeAddress { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string FullName { get; set; }
    public string Age { get; set; }
    public HashSet<int> Tags { get; set; }
    public AddressDto HomeAddress { get; set; }
}

cfg.CreateMap<UserEntity, UserDto>();
// AM002: FirstName nullable → non-nullable
// AM001 / related: type and shape mismatches on Age / Tags
// AM020: HomeAddress → AddressDto needs its own CreateMap
// AM006 / AM004 family: unmapped / missing members depending on direction
```

Code fixes can suggest `ForMember` conversions, `Ignore`, nested `CreateMap`, and aggregate map/ignore actions when safe enough to propose. Always review scaffold-level fixes.

---

## Severity and suppression

### Adopting in an existing codebase

Turning 23 rules on across a large solution at once is the fastest way to get the package uninstalled.
Five rules default to **error** (`AM001`, `AM002`, `AM003`, `AM011`, `AM030`), so a first
`dotnet add package` on a mature codebase can break the build immediately.

Two presets ship inside the package so you do not have to hand-write 23 severities:

| Preset | Behaviour | Use when |
| --- | --- | --- |
| `AutoMapperAnalyzer.Minimal.globalconfig` | **No rule is reported as an error.** Runtime-failure and data-loss rules report as warnings; everything else as suggestions. | Turning the analyzer on across an existing codebase |
| `AutoMapperAnalyzer.Recommended.globalconfig` | Matches the shipped defaults, written out so every rule is visible and tunable in one place. | New codebases, or once the warning count is under control |

**If you build with `TreatWarningsAsErrors`**, warnings are promoted to errors by your build settings,
independently of any analyzer configuration — no preset can override that. Minimal documents a
ready-made `WarningsNotAsErrors` list covering exactly the rules it sets to warning; copy it from the
top of the file.

**AM002 is deliberately absent from Recommended.** A severity setting is keyed by rule ID, but AM002
ships two descriptors at different severities (nullable-to-non-nullable as `Error`,
non-nullable-to-nullable as `Info`). One ID-level override would apply to both and silently promote the
informational one, failing builds on `string` → `string?` mappings that are safe today. Leaving it out
preserves its shipped per-descriptor behaviour. Minimal sets it to `suggestion`, which removes the
build-breaking descriptor without promoting the informational one.

Reference one from your project file:

```xml
<ItemGroup>
  <PackageReference Include="AutoMapperAnalyzer.Analyzers" Version="2.30.90"
                    PrivateAssets="all" GeneratePathProperty="true" />
  <GlobalAnalyzerConfigFiles
    Include="$(PkgAutoMapperAnalyzer_Analyzers)\config\AutoMapperAnalyzer.Minimal.globalconfig" />
</ItemGroup>
```

`GeneratePathProperty="true"` is what makes `$(PkgAutoMapperAnalyzer_Analyzers)` available. If you would
rather not depend on that, open either file in the package and copy its lines into your `.editorconfig` —
the presets are plain severity settings with no magic.

A workable ramp: start on Minimal, fix the warnings that matter to you, then switch to Recommended and
raise individual rules back to `error` as your codebase catches up.

### `.editorconfig`

```ini
dotnet_diagnostic.AM001.severity = error
dotnet_diagnostic.AM002.severity = error
dotnet_diagnostic.AM011.severity = error

dotnet_diagnostic.AM004.severity = warning
dotnet_diagnostic.AM005.severity = warning

dotnet_diagnostic.AM020.severity = suggestion
dotnet_diagnostic.AM021.severity = suggestion
```

### Selective suppression

```csharp
#pragma warning disable AM001 // Custom IValueConverter handles string→int
cfg.CreateMap<Source, Dest>();
#pragma warning restore AM001

[SuppressMessage("AutoMapper", "AM004:Missing destination property",
    Justification = "PII data intentionally excluded for GDPR compliance")]
public void ConfigureSafeUserMapping() { }
```

---

## Development experience

- **Visual Studio / Rider / VS Code (OmniSharp)**: diagnostics and lightbulb code fixes
- **CLI**: `dotnet build` (this repo disables analyzers on local CLI builds unless `ContinuousIntegrationBuild=true`)
- **Samples**: `dotnet build samples/AutoMapperAnalyzer.Samples`
- **Tests**: `dotnet test`

### CI / quality

- Multi-TFM test suite and package compatibility matrix
- Catalog, sample diagnostic snapshots, and package content verification
- Codecov coverage reporting

---

## Architecture notes

- Incremental Roslyn analysis with minimal IDE impact
- Central [rule catalog](docs/RULE_CATALOG.md) for descriptors, fixers, samples, and fix-trust levels
- Cross-platform behavior on Windows, macOS, and Linux
- Conservative fix selection: no silent no-op lightbulbs; withhold when ownership cannot be proven

---

## Contributing

```bash
git clone https://github.com/georgepwall1991/automapper-analyser.git
cd automapper-analyser
dotnet test
```

Useful contributions: edge-case scenarios, documentation, performance, and carefully scoped new rules.

---

## Deep dive

- [Architecture Guide](docs/ARCHITECTURE.md)
- [Diagnostic Rules](docs/DIAGNOSTIC_RULES.md)
- [Generated Rule Catalog](docs/RULE_CATALOG.md)
- [Sample Gallery](samples/AutoMapperAnalyzer.Samples/README.md)
- [CI/CD Pipeline](docs/CI-CD.md)
- [Compatibility Matrix](docs/COMPATIBILITY.md)
- [CHANGELOG](CHANGELOG.md)

## Community

- [Issues](https://github.com/georgepwall1991/automapper-analyser/issues)
- [Discussions](https://github.com/georgepwall1991/automapper-analyser/discussions)

## License

MIT — see [LICENSE](LICENSE).

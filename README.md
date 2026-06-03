# 🎯 AutoMapper Roslyn Analyzer

[![NuGet Version](https://img.shields.io/nuget/v/AutoMapperAnalyzer.Analyzers.svg?style=flat-square&logo=nuget&label=NuGet)](https://www.nuget.org/packages/AutoMapperAnalyzer.Analyzers/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/AutoMapperAnalyzer.Analyzers.svg?style=flat-square&logo=nuget&label=Downloads)](https://www.nuget.org/packages/AutoMapperAnalyzer.Analyzers/)
[![Build Status](https://img.shields.io/github/actions/workflow/status/georgepwall1991/automapper-analyser/ci.yml?style=flat-square&logo=github&label=Build)](https://github.com/georgepwall1991/automapper-analyser/actions)
[![Tests](https://img.shields.io/badge/Tests-819%20passing%2C%200%20skipped-success?style=flat-square&logo=checkmarx)](https://github.com/georgepwall1991/automapper-analyser/actions)
[![.NET](https://img.shields.io/badge/.NET-4.8+%20%7C%206.0+%20%7C%208.0+%20%7C%209.0+%20%7C%2010.0+-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)
[![Coverage](https://img.shields.io/codecov/c/github/georgepwall1991/automapper-analyser?style=flat-square&logo=codecov&label=Coverage)](https://codecov.io/gh/georgepwall1991/automapper-analyser)

> **✨ Catch AutoMapper configuration errors before they cause runtime chaos**  
> *A sophisticated Roslyn analyzer that transforms AutoMapper development from reactive debugging to proactive
prevention*

---

## 🎉 Latest Release: v2.30.32

**Property-level AM004/AM006 diagnostics**

✅ **Highlights**

- AM004 now reports missing destination-property diagnostics on the offending source property identifier.
- AM006 now reports unmapped destination-property diagnostics on the offending destination property identifier.
- Existing AM004/AM006 code fixes still locate the owning `CreateMap` chain through diagnostic span metadata.

🧪 **Validation**

- Full solution test validation passed on `net10.0` with 819 tests.
- AnalyzerVerifier `--check-catalog --check-snapshots` green.
- Codex review green per change.

### Recent Releases

- **v2.30.32**: AM004/AM006 diagnostics now point at the offending source/destination property identifiers while preserving code-fix routing through mapping invocation metadata.
- **v2.30.31**: Correctness/code-fix hardening from an adversarial audit — AM001 signed/unsigned numeric mismatches, keyword-name escaping, AM020 qualified/generic nested `CreateMap` names, AM003 implicit-conversion-gated element casts, AM031 `ValueTask.Result`.
- **v2.30.30**: Added AM021 simple-conversion fixes for `ImmutableList<T>`, `ImmutableHashSet<T>`, and `FrozenSet<T>` while keeping custom immutable-lookalikes manual-only.
- **v2.30.29**: Hardened AM003 custom collection fixer safety while preserving safe BCL collection constructor rewrites.
- **v2.30.28**: Tightened AM031 and AM001 fixer action selection so automatic rewrites stay executable and behavior-preserving.
- **v2.30.27**: Added a direct AM041 test locking the `ForPath` chained-configuration withhold behavior alongside the existing `ForMember`/parenthesized/`ReverseMap()`-chained coverage.
- **v2.30.26**: Locked AM050 sibling-config withhold behavior with direct `PreCondition`/`UseDestinationValue`/`Ignore` regression tests alongside the existing `Condition`/`NullSubstitute` cases.
- **v2.30.25**: Corrected six AM002/AM011/AM020/AM021/AM022/AM030 rule-docs category lines to match the shipped descriptor categories and added a category drift guard that prevents future doc/descriptor drift.
- **v2.30.24**: Marked unwired AM003/AM030 `DiagnosticDescriptor` relics `[Obsolete]` (binary compatibility preserved) and added a trust drift guard that fails when any shipped analyzer declares a `DiagnosticDescriptor` field outside its `SupportedDiagnostics` without an explicit Obsolete attribute.
- **v2.30.23**: AM050 code fix is withheld when the redundant-`MapFrom` `ForMember` lambda contains sibling configuration (`Condition`, `NullSubstitute`, …) that would otherwise be dropped.
- **v2.30.22**: AM031 normalises chained pre-terminal LINQ receivers so multiple enumerations of the same source-rooted collection report.
- **v2.30.21**: AM050 redundant-`MapFrom` detection now also fires on parenthesized and typed lambdas such as `o.MapFrom((Source s) => s.Name)`.
- **v2.30.20**: AM030 recognises `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrEmpty`, and `ArgumentException.ThrowIfNullOrWhiteSpace` as null guards on the converter's source parameter.
- **v2.30.19**: AM030 stops reporting concrete converters as unused when a matching `ITypeConverter<TSource, TDestination>` is passed to `ConvertUsing` through DI/service-locator shapes.
- **v2.30.18**: AM031 multiple-enumeration tracking covers `Min`, `Max`, `Aggregate`, `LongCount`, `Single`, `SingleOrDefault`, `ToHashSet`, `ToDictionary`, and `ToLookup`, with a `System.Linq.Enumerable`/`Queryable` namesake gate.
- **v2.30.17**: AM041 withholds the duplicate-removal fix when the duplicate `CreateMap<>()` carries chained mapping policy.
- **v2.30.16**: Analyzer precision hardening across AM002, AM006, AM021, AM031, and AM041.
- **v2.30.15**: Fixer UX trust hardening with descriptor-specific no-fix metadata and executable interface collection rewrites.
- **v2.30.14**: AM021 reverse-map collection element diagnostics catch missing reverse element maps without duplicate noise.
- **v2.30.13**: AM041 duplicate-map labels now preserve generic type arguments and array ranks.
- **v2.30.12**: AM030 interface-typed converter usage and AM021 dictionary fixer safety.
- **v2.30.11**: AM001 enum/string conversion fixes for direct property mismatch remediation.
- **v2.30.10**: AM050 proven redundant `MapFrom` cleanup for string-based members and type-safe suppressions.
- **v2.30.9**: AM004/AM005 severity documentation trust with descriptor-aligned rule docs.
- **v2.30.8**: AM030 null-guard fixer precision without invasive `using System` edits.
- **v2.30.7**: AM022 recursion boundary precision for nested-map chains and explicit recursion controls.
- **v2.30.6**: AM031 source collection cache precision with nested source paths and Task-property `.Result` coverage.
- **v2.30.5**: AM030 type-based converter usage precision for `ConvertUsing(typeof(...))`.
- **v2.30.4**: AM022 mapped recursion precision with unrelated-cycle false-positive reductions.
- **v2.30.3**: AM011 ForPath required-member boundary with explicit configuration coverage.
- **v2.30.2**: AM003 assignable collection boundary suppression with targeted regression coverage.
- **v2.30.1**: AM002 nullability contract alignment with descriptor-accurate docs and nullable-context regression coverage.
- **v2.30.0**: Fixer hardening for AM001, AM005, AM006, AM011, and AM021 with safer action selection.
- **v2.29.0**: Smart primary fix and reduced fixer noise across the main data-integrity fixers.
- **v2.28.2**: False-positive reduction, fixer UX improvements, and release workflow hardening.
- **v2.28.1**: Case-aware AM021 suppression and fixer reliability improvements.
- **v2.28.0**: Analyzer logic fixes and performance improvements.
- **v2.27.0**: AM050 nullable safety fix and broad regression coverage expansion.
- **v2.25.0**: Code-fix consolidation and sample verification.

---

## 🌟 Why This Matters

AutoMapper is powerful, but silent failures are its Achilles' heel. Properties that don't map, type mismatches that
throw at runtime, nullable violations that cause NullReferenceExceptions—these issues typically surface in production,
not during development.

**This analyzer changes that equation entirely.**

```csharp
// Before: 😰 Runtime surprise!
public void MapUserData()
{
    var user = mapper.Map<UserDto>(userEntity); 
    // 💥 NullReferenceException in production
    // 💥 Data loss from unmapped properties  
    // 💥 Type conversion failures
}

// After: 🛡️ Compile-time confidence!
public void MapUserData() 
{
    var user = mapper.Map<UserDto>(userEntity);
    // ✅ All mapping issues caught at compile-time
    // ✅ Code fixes suggest proper solutions
    // ✅ Ship with confidence
}
```

---

## ⚡ Quick Start

```bash
# Install via .NET CLI
dotnet add package AutoMapperAnalyzer.Analyzers

# Or via Package Manager Console
Install-Package AutoMapperAnalyzer.Analyzers
```

That's it! The analyzer automatically activates and starts checking your AutoMapper configurations. Open any file with
AutoMapper mappings and see diagnostics appear instantly.

**See it work:**

```csharp
var config = new MapperConfiguration(cfg =>
{
    cfg.CreateMap<User, UserDto>();
    //  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ AM001: Property 'Age' type mismatch
    //  💡 Press Ctrl+. for code fix suggestions
});
```

---

## 🚀 What You Get

### 🛡️ **Complete Type Safety**

- **AM001**: Property type mismatches with smart conversion suggestions
- **AM002**: Nullable-to-non-nullable mapping with null safety patterns
- **AM003**: Collection type incompatibility detection

### 🔍 **Zero Data Loss**

- **AM004**: Missing destination properties (prevent silent data loss)
- **AM006**: Unmapped destination properties (detect unintentional defaults)
- **AM011**: Required property validation (avoid runtime exceptions)
- **AM005**: Case sensitivity issues (cross-platform reliability)

### 🧩 **Complex Mapping Intelligence**

- **AM020**: Nested object mapping validation with CreateMap suggestions (supports internal properties & cross-profile
  detection)
- **AM021**: Collection element type analysis with conversion strategies
- **AM022**: Circular reference detection with MaxDepth recommendations
- **AM030**: Custom type converter analysis with null safety validation

### ⚡ **Instant Code Fixes**

Every analyzer comes with **intelligent code fixes** that don't just identify problems—they solve them:

```csharp
// Problem detected ⚠️
cfg.CreateMap<Source, Dest>();
//    ~~~~~~~~~~~~~~~~~~~~~~~~~ AM001: Property 'Age' type mismatch

// Code fix applied ✨
cfg.CreateMap<Source, Dest>()
   .ForMember(dest => dest.Age, opt => opt.MapFrom(src => 
       int.TryParse(src.Age, out var age) ? age : 0));
```

---

## 🎯 Real-World Impact

|    | Before                                 | After                            |
|----|----------------------------------------|----------------------------------|
| 🐛 | Runtime mapping failures               | ✅ Compile-time validation        |
| 🔍 | Manual debugging sessions              | ✅ Instant error highlights       |  
| 📝 | Guessing correct configurations        | ✅ Code fixes with best practices |
| ⚠️ | Production NullReferenceExceptions     | ✅ Null safety enforcement        |
| 📊 | Silent data loss                       | ✅ Missing property detection     |
| 🌐 | Cross-platform mapping inconsistencies | ✅ Case sensitivity validation    |

---

## 📦 Installation

### .NET CLI (Recommended)

```bash
dotnet add package AutoMapperAnalyzer.Analyzers
```

### Package Manager Console

```powershell
Install-Package AutoMapperAnalyzer.Analyzers
```

### Project File (For CI/CD)

```xml
<PackageReference Include="AutoMapperAnalyzer.Analyzers" Version="2.30.32">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

### ⚡ Universal Compatibility

| Platform       | Version | Support     | AutoMapper | CI/CD Status |
|----------------|---------|-------------|------------|--------------|
| .NET Framework | 4.8+    | 🟢 **Full** | 10.1.1+    | ✅ **Tested** |
| .NET           | 6.0+    | 🟢 **Full** | 12.0.1+    | ✅ **Tested** |
| .NET           | 8.0+    | 🟢 **Full** | 14.0.0+    | ✅ **Tested** |
| .NET           | 9.0+    | 🟢 **Full** | 14.0.0+    | ✅ **Tested** |
| .NET           | 10.0+   | 🟢 **Full** | 14.0.0+    | ✅ **Tested** |

*Analyzer targets .NET Standard 2.0 for maximum compatibility*  
*All platforms validated in automated CI/CD pipeline*

---

## 🎨 See It In Action

### ❌ **The Problems**

```csharp
public class UserEntity
{
    public int Id { get; set; }
    public string? FirstName { get; set; }  // Nullable
    public string LastName { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Tags { get; set; }  // Collection type
    public Address HomeAddress { get; set; }  // Complex object
}

public class UserDto  
{
    public int Id { get; set; }
    public string FirstName { get; set; }    // Non-nullable!
    public string FullName { get; set; }     // Different property!  
    public string Age { get; set; }          // Different type!
    public HashSet<int> Tags { get; set; }   // Incompatible collection!
    public AddressDto HomeAddress { get; set; }  // Needs explicit mapping!
}

// This configuration has MULTIPLE issues:
cfg.CreateMap<UserEntity, UserDto>();
//  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  🚨 AM002: FirstName nullable→non-nullable (NullReferenceException risk)
//  🚨 AM004: LastName will not be mapped (data loss)  
//  🚨 AM001: Age expects int but gets DateTime (runtime exception)
//  🚨 AM021: Tags List<string>→HashSet<int> incompatible (mapping failure)
//  🚨 AM020: HomeAddress→AddressDto needs CreateMap (runtime exception)
```

### ✅ **The Solutions** (Auto-Generated!)

```csharp  
// Code fixes automatically suggest:
cfg.CreateMap<UserEntity, UserDto>()
   .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstName ?? ""))
   .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"))  
   .ForMember(dest => dest.Age, opt => opt.MapFrom(src => 
       DateTime.Now.Year - src.CreatedAt.Year))
   .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => 
       src.Tags.Select((tag, index) => index).ToHashSet()));

// Separate mapping for complex types  
cfg.CreateMap<Address, AddressDto>();
```

---

## ⚙️ Fine-Tuned Control

### Severity Configuration (.editorconfig)

```ini
# Treat type safety as build errors
dotnet_diagnostic.AM001.severity = error
dotnet_diagnostic.AM002.severity = error  
dotnet_diagnostic.AM011.severity = error

# Data loss warnings  
dotnet_diagnostic.AM004.severity = warning
dotnet_diagnostic.AM005.severity = warning

# Suggestions for optimization
dotnet_diagnostic.AM020.severity = suggestion
dotnet_diagnostic.AM021.severity = suggestion  
```

### Selective Suppression

```csharp
// Suppress with clear justification
#pragma warning disable AM001 // Custom IValueConverter handles string→int
cfg.CreateMap<Source, Dest>();
#pragma warning restore AM001

// Method-level suppression
[SuppressMessage("AutoMapper", "AM004:Missing destination property",
    Justification = "PII data intentionally excluded for GDPR compliance")]
public void ConfigureSafeUserMapping() { }
```

---

## 📊 Complete Analyzer Coverage

| Rule                    | Description                    | Analyzer | Code Fix | Severity |
|-------------------------|--------------------------------|----------|----------|----------|
| **🔒 Type Safety**      |                                |          |          |
| AM001                   | Property Type Mismatch         | ✅        | ✅        | Error    |
| AM002                   | Nullable→Non-nullable          | ✅        | ✅        | Error / Info |
| AM003                   | Collection Incompatibility     | ✅        | ✅        | Error    |
| **📊 Data Integrity**   |                                |          |          |
| AM004                   | Missing Destination Property   | ✅        | ✅        | Warning  |
| AM005                   | Case Sensitivity Issues        | ✅        | ✅        | Warning  |
| AM006                   | Unmapped Destination Property  | ✅        | ✅        | Info     |
| AM011                   | Required Property Missing      | ✅        | ✅        | Error    |
| **🧩 Complex Mappings** |                                |          |          |
| AM020                   | Nested Object Issues           | ✅        | ✅        | Warning  |
| AM021                   | Collection Element Mismatch    | ✅        | ✅        | Warning  |
| AM022                   | Circular Reference Risk        | ✅        | ✅        | Warning  |
| AM030                   | Custom Type Converter Issues   | ✅        | ✅        | Error / Warning / Info |
| **⚡ Performance**       |                                |          |          |
| AM031                   | Performance Warnings           | ✅        | ✅        | Warning / Info |
| **⚙️ Configuration**    |                                |          |          |
| AM041                   | Duplicate Mapping Registration | ✅        | ✅        | Warning  |
| AM050                   | Redundant MapFrom              | ✅        | ✅        | Info     |
| **🚀 Future**           |                                |          |          |
| AM032+                  | Advanced Null Propagation      | 🔮       | 🔮       | -        |
| AM040+                  | Configuration Rules            | 🔮       | 🔮       | -        |
| AM050+                  | Advanced Optimizations         | 🔮       | 🔮       | -        |

---

## 🛠️ Development Experience

### IDE Integration

- **Visual Studio**: Full IntelliSense integration with lightbulb code fixes
- **VS Code**: Rich diagnostic experience via OmniSharp
- **JetBrains Rider**: Native analyzer support with quick-fix suggestions
- **Command Line**: Works seamlessly with `dotnet build`

### Testing Your Configuration

```bash
# Quick validation
dotnet build  # Analyzer runs automatically

# Comprehensive testing
git clone https://github.com/georgepwall1991/automapper-analyser.git
cd automapper-analyser
dotnet run --project samples/AutoMapperAnalyzer.Samples

# See all analyzer warnings in action
dotnet build samples/ --verbosity normal

# Run full test suite with coverage
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

### CI/CD & Quality Assurance

- **🔄 Automated Testing**: Every commit tested across multiple .NET versions
- **📊 Code Coverage**: Integrated with Codecov for comprehensive coverage tracking
- **🛡️ Quality Gates**: Build fails only on genuine errors, warnings are preserved
- **⚡ Cross-Platform**: Validated on Ubuntu (CI) and Windows (compatibility tests)
- **📈 Performance**: Incremental builds with analyzer caching for optimal speed

---

## 🏗️ Architecture Highlights

This isn't just another analyzer—it's built for **enterprise-grade reliability**:

- **🏎️ Performance-First**: Incremental analysis with minimal IDE impact
- **🔧 Extensible Design**: Clean plugin architecture for new rules
- **🧪 Battle-Tested**: release validation includes full suite coverage plus targeted regression tests for high-risk fixers
- **🌐 Cross-Platform**: Identical behavior on Windows, macOS, Linux
- **⚡ CI/CD Ready**: Automated GitHub Actions with codecov integration
- **📊 Code Coverage**: 55%+ coverage with comprehensive testing

---

## 🎯 What's Next

### Recently Completed ✅

- **v2.30.15**: Fixer UX trust hardening with descriptor-specific no-fix metadata and executable interface collection rewrites
- **v2.30.14**: AM021 reverse-map collection element diagnostics catch missing reverse element maps without duplicate noise
- **v2.30.13**: AM041 duplicate-map labels preserve generic type arguments and array ranks
- **v2.30.12**: AM030 interface-typed converter usage and AM021 dictionary fixer safety
- **v2.30.11**: AM001 enum/string conversion fixes for direct property mismatch remediation
- **v2.30.10**: AM050 proven redundant `MapFrom` cleanup for string-based members and type-safe suppressions
- **v2.30.9**: AM004/AM005 severity documentation trust with descriptor-aligned rule docs
- **v2.30.8**: AM030 null-guard fixer precision without invasive `using System` edits
- **v2.30.7**: AM022 recursion boundary precision for nested-map chains and explicit recursion controls
- **v2.30.6**: AM031 source collection cache precision with nested source paths and Task-property `.Result` coverage
- **v2.30.5**: AM030 type-based converter usage precision for `ConvertUsing(typeof(...))`
- **v2.30.4**: AM022 mapped recursion precision with unrelated-cycle false-positive reductions
- **v2.30.3**: AM011 ForPath required-member boundary with explicit-configuration regression coverage
- **v2.30.2**: AM003 assignable collection boundary suppression with targeted regression coverage
- **v2.30.1**: AM002 nullability contract alignment with descriptor-accurate docs and nullable-context regression coverage
- **v2.30.0**: Fixer hardening for AM001, AM005, AM006, AM011, and AM021 with safer action selection
- **v2.29.0**: Smart Primary Fix — reduced fixer noise to max 2 lightbulb options per diagnostic
- **v2.28.2**: False-positive reduction, fixer UX improvements, and release workflow hardening
- **v2.28.1**: Case-aware AM021 suppression and fixer accuracy improvements
- **v2.28.0**: Analyzer logic fixes and performance improvements
- **v2.27.0**: AM050 nullable safety fix and expanded regression coverage

### Phase 5B: Enhanced Analysis (Upcoming)

- **AM032**: Advanced null propagation patterns with smart fixes

### Phase 6: Configuration & Profile Analysis

- **AM040**: Profile registration analysis and auto-registration fixes

### Beyond Code Analysis

- **NuGet Package Templates**: Project templates with pre-configured analyzers
- **MSBuild Integration**: Custom build targets for mapping validation
- **Documentation Generation**: Auto-generate mapping documentation
- **Metrics Dashboard**: Build-time analysis reporting

---

## 🤝 Contributing

We're building something special, and **your expertise makes it better**.

**Quick Start Contributing:**

```bash
git clone https://github.com/georgepwall1991/automapper-analyser.git
cd automapper-analyser
dotnet test
```

**What We Need:**

- 🧪 More edge-case scenarios
- 📝 Documentation improvements
- 🚀 Performance optimizations
- 💡 New analyzer rule ideas

See our [Contributing Guide](docs/CONTRIBUTING.md) for detailed guidelines.

---

## 📚 Deep Dive Resources

- 📖 [**Architecture Guide**](docs/ARCHITECTURE.md) - How it all works under the hood
- 🔍 [**Diagnostic Rules**](docs/DIAGNOSTIC_RULES.md) - Complete rule reference
- 🧭 [**Generated Rule Catalog**](docs/RULE_CATALOG.md) - Descriptor, fixer, sample, and trust-level source of truth
- 🧪 [**Sample Gallery**](samples/AutoMapperAnalyzer.Samples/README.md) - Real-world scenarios
- 🚀 [**CI/CD Pipeline**](docs/CI-CD.md) - Our build and deployment process
- 📊 [**Compatibility Matrix**](docs/COMPATIBILITY.md) - Framework support details

---

## 💬 Community & Support

**Get Help:**

- 🐛 [**Issues**](https://github.com/georgepwall1991/automapper-analyser/issues) - Bug reports and feature requests
- 💬 [**Discussions**](https://github.com/georgepwall1991/automapper-analyser/discussions) - Questions and ideas
- 📖 [**Wiki**](https://github.com/georgepwall1991/automapper-analyser/wiki) - Comprehensive documentation

## 📄 License

**MIT License** - Use it anywhere, contribute back if you can.

---

<div align="center">

### ⭐ **Star this repo if it's saving you time!**

**Built with ❤️ by developers who've debugged too many AutoMapper issues**

[🚀 **Get Started Now**](#-installation) • [📖 **Read the Docs**](docs/) • [💬 **Join the Discussion
**](https://github.com/georgepwall1991/automapper-analyser/discussions)

</div>

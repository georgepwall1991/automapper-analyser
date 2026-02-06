# 🎯 AutoMapper Roslyn Analyzer

[![NuGet Version](https://img.shields.io/nuget/v/AutoMapperAnalyzer.Analyzers.svg?style=flat-square&logo=nuget&label=NuGet)](https://www.nuget.org/packages/AutoMapperAnalyzer.Analyzers/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/AutoMapperAnalyzer.Analyzers.svg?style=flat-square&logo=nuget&label=Downloads)](https://www.nuget.org/packages/AutoMapperAnalyzer.Analyzers/)
[![Build Status](https://img.shields.io/github/actions/workflow/status/georgepwall1991/automapper-analyser/ci.yml?style=flat-square&logo=github&label=Build)](https://github.com/georgepwall1991/automapper-analyser/actions)
[![Tests](https://img.shields.io/badge/Tests-436%20passing%2C%2012%20skipped-success?style=flat-square&logo=checkmarx)](https://github.com/georgepwall1991/automapper-analyser/actions)
[![.NET](https://img.shields.io/badge/.NET-4.8+%20%7C%206.0+%20%7C%208.0+%20%7C%209.0+%20%7C%2010.0+-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)
[![Coverage](https://img.shields.io/codecov/c/github/georgepwall1991/automapper-analyser?style=flat-square&logo=codecov&label=Coverage)](https://codecov.io/gh/georgepwall1991/automapper-analyser)

> **✨ Catch AutoMapper configuration errors before they cause runtime chaos**  
> *A sophisticated Roslyn analyzer that transforms AutoMapper development from reactive debugging to proactive
prevention*

---

## 🎉 Latest Release: v2.10.0

**AM041 Accuracy & Fix Safety**

🛡️ **Analyzer Improvements (AM041):**

- Suppressed false positives for non-AutoMapper `CreateMap` symbols.
- Improved symbol resolution robustness by handling candidate symbols during duplicate checks.

🔧 **Code Fix Improvements (AM041):**

- For duplicate `CreateMap<TSource, TDestination>().ReverseMap()`, fixer now preserves reverse-direction mapping by rewriting to `CreateMap<TDestination, TSource>()` instead of removing the statement.

✅ **Validation:**

- Added regression tests for non-AutoMapper `CreateMap` scenarios and member-access `CreateMap` reverse-map rewrite paths.
- All 448 tests passing locally (`436 passed`, `12 skipped`).

### Previous Release: v2.9.0

**Analyzer Accuracy & Contradiction Fixes**

- Improved AM004/AM005/AM006/AM011 detection and code-fix reliability.
- Removed contradictory diagnostics around required member handling.
- Added broad regression coverage for AM004/AM005/AM006/AM011.

### Previous Release: v2.8.0

**Architecture Refactoring & Enhanced Bulk Fixes**

- Refactored all code fix providers to use `AutoMapperCodeFixProviderBase`.
- Added enhanced bulk-fix UX for large mapping scenarios.
- Improved maintainability and consistency across fixer implementations.

### Previous Release: v2.7.0

**New Icon & Visual Update**

- **New Icon**: Updated NuGet package icon to a modern, high-quality design
- **Visual Identity**: Improved branding for the analyzer package

### Previous Release: v2.6.1

**Build & Stability Fixes**

🛠️ **Fixes:**

- Fixed build errors in AM002 (Nullable Compatibility) and AM004 (Missing Destination Property) code fix providers
- Resolved issues with SyntaxEditor usage in newer Roslyn versions
- Improved stability of bulk code fixes

### Previous Release: v2.6.0

**Bulk Code Fixes & Improved UX**

✨ **New Capabilities:**

- 📦 **Bulk Code Fixes (AM011)**: Fix all unmapped required properties in one click!
- 🗂️ **Action Grouping**: Reduced lightbulb menu clutter by grouping property-specific fixes.
- 🧠 **Smart Property Creation (AM004)**: Automatically detects missing destination properties and creates them in the destination class (even in separate files).
- 🔍 **Fuzzy Matching (AM011)**: Intelligent suggestions for unmapped required properties using Levenshtein distance matching (e.g., maps `UserName` to `Username`).
- 🛠️ **Type Converter Generation (AM030)**: Instead of just a comment, generates a complete `IValueConverter` class implementation and wires it up.
- ⚡ **Cross-File Performance Refactoring (AM031)**: Intelligently moves expensive computations from mapping profiles to source classes, handling cross-file modifications seamlessly.

### Previous Release: v2.5.0

- **Smart Code Fixers**: Advanced refactoring tools including fuzzy matching and property creation.
- **Refactoring**: Major improvements to code fix providers.

### Previous Release: v2.4.1

- 🚀 **.NET 10 Ready**: Verified compatibility metadata for upcoming .NET 10.
- 🧹 **Documentation Cleanup**: Removed outdated reports and guides.

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
<PackageReference Include="AutoMapperAnalyzer.Analyzers" Version="2.5.0">
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
//  🚨 AM030: Custom converter missing ConvertUsing configuration
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
| AM001                   | Property Type Mismatch         | ✅        | ✅        | Warning  |  
| AM002                   | Nullable→Non-nullable          | ✅        | ✅        | Warning  |
| AM003                   | Collection Incompatibility     | ✅        | ✅        | Warning  |
| **📊 Data Integrity**   |                                |          |          |
| AM004                   | Missing Destination Property   | ✅        | ✅        | Info     |
| AM005                   | Case Sensitivity Issues        | ✅        | ✅        | Info     |
| AM011                   | Required Property Missing      | ✅        | ✅        | Error    |
| **🧩 Complex Mappings** |                                |          |          |
| AM020                   | Nested Object Issues           | ✅        | ✅        | Warning  |
| AM021                   | Collection Element Mismatch    | ✅        | ✅        | Warning  |  
| AM022                   | Circular Reference Risk        | ✅        | ✅        | Warning  |
| AM030                   | Custom Type Converter Issues   | ✅        | ✅        | Warning  |
| **⚡ Performance**       |                                |          |          |
| AM031                   | Performance Warnings           | ✅        | ✅        | Warning  |
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
- **🧪 Battle-Tested**: 418 unit tests with 405 passing, 13 skipped for known limitations (97% passing)
- **🌐 Cross-Platform**: Identical behavior on Windows, macOS, Linux
- **⚡ CI/CD Ready**: Automated GitHub Actions with codecov integration
- **📊 Code Coverage**: 55%+ coverage with comprehensive testing

---

## 🎯 What's Next

### Recently Completed ✅

- **v2.5.0**: Smart Code Fixers & Advanced Refactoring
- **v2.4.1**: .NET 10 Compatibility & Maintenance
- **v2.4.0**: Configuration & Redundancy Analysis (AM041, AM050)
- **v2.3.2**: ReverseMap support & Performance optimizations
- **v2.2.0**: AM031 Performance warning analyzer

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

# 🎯 AutoMapper Roslyn Analyzer

[![Build Status](https://github.com/georgepwall1991/automapper-analyser/workflows/CI%2FCD%20Pipeline/badge.svg)](https://github.com/georgepwall1991/automapper-analyser/actions)
[![NuGet](https://img.shields.io/nuget/v/AutoMapperAnalyzer.Analyzers.svg)](https://www.nuget.org/packages/AutoMapperAnalyzer.Analyzers/)
[![Coverage](https://codecov.io/gh/georgepwall1991/automapper-analyser/branch/main/graph/badge.svg)](https://codecov.io/gh/georgepwall1991/automapper-analyser)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

> **✨ Catch AutoMapper configuration errors before they cause runtime chaos**  
> *A sophisticated Roslyn analyzer that transforms AutoMapper development from reactive debugging to proactive prevention*

---

## 🌟 Why This Matters

AutoMapper is powerful, but silent failures are its Achilles' heel. Properties that don't map, type mismatches that throw at runtime, nullable violations that cause NullReferenceExceptions—these issues typically surface in production, not during development.

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
- **AM020**: Nested object mapping validation with CreateMap suggestions
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

| Before | After |
|--------|--------|
| 🐛 Runtime mapping failures | ✅ Compile-time validation |
| 🔍 Manual debugging sessions | ✅ Instant error highlights |  
| 📝 Guessing correct configurations | ✅ Code fixes with best practices |
| ⚠️ Production NullReferenceExceptions | ✅ Null safety enforcement |
| 📊 Silent data loss | ✅ Missing property detection |
| 🌐 Cross-platform mapping inconsistencies | ✅ Case sensitivity validation |

---

## 📦 Installation

### Quick Start - Package Manager
```powershell
Install-Package AutoMapperAnalyzer.Analyzers
Install-Package AutoMapperAnalyzer.CodeFixes  
```

### .NET CLI
```bash
dotnet add package AutoMapperAnalyzer.Analyzers
dotnet add package AutoMapperAnalyzer.CodeFixes
```

### Project File (Recommended)
```xml
<PackageReference Include="AutoMapperAnalyzer.Analyzers" Version="2.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

### ⚡ Universal Compatibility

| Platform | Version | Support | AutoMapper | CI/CD Status |
|----------|---------|---------|------------|--------------|
| .NET Framework | 4.8+ | 🟢 **Full** | 10.1.1+ | ✅ **Tested** |
| .NET | 6.0+ | 🟢 **Full** | 12.0.1+ | ✅ **Tested** |
| .NET | 8.0+ | 🟢 **Full** | 14.0.0+ | ✅ **Tested** |
| .NET | 9.0+ | 🟢 **Full** | 14.0.0+ | ✅ **Tested** |

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

| Rule | Description | Analyzer | Code Fix | Severity |
|------|-------------|----------|----------|----------|
| **🔒 Type Safety** ||||
| AM001 | Property Type Mismatch | ✅ | ✅ | Warning |  
| AM002 | Nullable→Non-nullable | ✅ | ✅ | Warning |
| AM003 | Collection Incompatibility | ✅ | ✅ | Warning |
| **📊 Data Integrity** ||||  
| AM004 | Missing Destination Property | ✅ | ✅ | Info |
| AM005 | Case Sensitivity Issues | ✅ | ✅ | Info |
| AM011 | Required Property Missing | ✅ | ✅ | Error |
| **🧩 Complex Mappings** ||||
| AM020 | Nested Object Issues | ✅ | ✅ | Warning |
| AM021 | Collection Element Mismatch | ✅ | ✅ | Warning |  
| AM022 | Circular Reference Risk | ✅ | ✅ | Warning |
| AM030 | Custom Type Converter Issues | ✅ | ✅ | Warning |
| **🚀 Future** ||||
| AM031+ | Performance Analysis | 🔮 | 🔮 | - |
| AM040+ | Configuration Rules | 🔮 | 🔮 | - |
| AM050+ | Advanced Optimizations | 🔮 | 🔮 | - |

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
- **🧪 Battle-Tested**: 130+ unit tests covering edge cases (100% passing)
- **🌐 Cross-Platform**: Identical behavior on Windows, macOS, Linux
- **⚡ CI/CD Ready**: Automated GitHub Actions with codecov integration
- **📊 Code Coverage**: 55%+ coverage with comprehensive testing

---

## 🎯 What's Next

### Phase 5B: Enhanced Analysis (In Progress)
- **AM030**: Custom type converter validation ✅ **COMPLETE**
- **AM031**: Performance warning analysis with optimization suggestions
- **AM040**: Profile registration analysis and auto-registration fixes
- **AM041**: Conflicting mapping rule detection and resolution

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
dotnet test  # All 130+ tests should pass
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

[🚀 **Get Started Now**](#-installation) • [📖 **Read the Docs**](docs/) • [💬 **Join the Discussion**](https://github.com/georgepwall1991/automapper-analyser/discussions)

</div>

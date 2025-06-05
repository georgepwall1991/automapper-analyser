# AutoMapper Roslyn Analyzer

[![Build Status](https://github.com/georgepwall1991/automapper-analyser/workflows/CI%2FCD%20Pipeline/badge.svg)](https://github.com/georgepwall1991/automapper-analyser/actions)
[![NuGet](https://img.shields.io/nuget/v/AutoMapperAnalyzer.Analyzers.svg)](https://www.nuget.org/packages/AutoMapperAnalyzer.Analyzers/)
[![Coverage](https://codecov.io/gh/georgepwall1991/automapper-analyser/branch/main/graph/badge.svg)](https://codecov.io/gh/georgepwall1991/automapper-analyser)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

> 🔍 **Roslyn analyzer that detects AutoMapper configuration issues at compile-time to prevent runtime exceptions and
data loss.**

## 🚀 Features

### 🛡️ Type Safety Validation

- **AM001**: Property type mismatch detection
- **AM002**: Nullable to non-nullable assignment warnings
- **AM003**: Collection type incompatibility errors

### 🔍 Missing Property Detection

- **AM010**: Source properties not mapped to destination (data loss prevention)
- **AM011**: Required destination properties without source mapping
- **AM012**: Case sensitivity mismatches between properties

### ⚙️ Configuration Validation

- **AM040**: Missing AutoMapper profile registration
- **AM041**: Conflicting mapping rules for the same property
- **AM042**: Properties both ignored and explicitly mapped

### ⚡ Performance & Best Practices

- **AM050**: Static mapper usage detection (recommend dependency injection)
- **AM051**: Repeated mapping configuration warnings
- **AM052**: Missing null propagation in mapping chains

## 📦 Installation

### Package Manager

```powershell
Install-Package AutoMapperAnalyzer.Analyzers
Install-Package AutoMapperAnalyzer.CodeFixes
```

### .NET CLI

```bash
dotnet add package AutoMapperAnalyzer.Analyzers
dotnet add package AutoMapperAnalyzer.CodeFixes
```

### PackageReference

```xml
<PackageReference Include="AutoMapperAnalyzer.Analyzers" Version="1.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
<PackageReference Include="AutoMapperAnalyzer.CodeFixes" Version="1.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

## 🎯 Quick Start

Once installed, the analyzer automatically detects issues in your AutoMapper configurations:

### ❌ Problems Detected

```csharp
// AM001: Property type mismatch
class Source { public string Age { get; set; } }
class Dest { public int Age { get; set; } }

cfg.CreateMap<Source, Dest>(); // ❌ Warning: string -> int without converter

// AM002: Nullable to non-nullable
class Source { public string? Name { get; set; } }
class Dest { public string Name { get; set; } }

cfg.CreateMap<Source, Dest>(); // ❌ Warning: Potential NullReferenceException

// AM010: Missing destination property (data loss)
class Source { public string ImportantData { get; set; } }
class Dest { /* Missing ImportantData property */ }

cfg.CreateMap<Source, Dest>(); // ❌ Warning: Data loss potential
```

### ✅ Recommended Solutions

```csharp
// ✅ Explicit type conversion
cfg.CreateMap<Source, Dest>()
   .ForMember(dest => dest.Age, opt => opt.MapFrom(src => 
       int.TryParse(src.Age, out var age) ? age : 0));

// ✅ Null safety handling
cfg.CreateMap<Source, Dest>()
   .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? "Unknown"));

// ✅ Explicit data handling
cfg.CreateMap<Source, Dest>()
   .ForMember(dest => dest.Summary, opt => opt.MapFrom(src => 
       $"Name: {src.Name}, Data: {src.ImportantData}"));
```

## 🔧 Configuration

### Severity Levels

Configure diagnostic severity in your `.editorconfig`:

```ini
# Error level diagnostics (build failures)
dotnet_diagnostic.AM001.severity = error  # Type mismatches
dotnet_diagnostic.AM003.severity = error  # Collection incompatibility
dotnet_diagnostic.AM011.severity = error  # Missing required properties

# Warning level diagnostics  
dotnet_diagnostic.AM002.severity = warning  # Nullable issues
dotnet_diagnostic.AM010.severity = warning  # Data loss potential
dotnet_diagnostic.AM040.severity = warning  # Missing profiles

# Information level diagnostics
dotnet_diagnostic.AM012.severity = suggestion  # Case sensitivity
dotnet_diagnostic.AM050.severity = suggestion  # Performance hints
```

### Suppressing Diagnostics

```csharp
// Suppress specific diagnostics with justification
#pragma warning disable AM001 // Justification: Custom converter handles this
cfg.CreateMap<Source, Dest>();
#pragma warning restore AM001

// Suppress via attributes
[SuppressMessage("AutoMapper", "AM010:Missing destination property", 
    Justification = "Data intentionally excluded for security")]
public void ConfigureMapping() { }
```

## 📊 Supported Scenarios

| Scenario             | Analyzer Support | Code Fix Support       |
|----------------------|------------------|------------------------|
| Type Safety          | ✅ All cases      | ✅ Common patterns      |
| Missing Properties   | ✅ All cases      | ✅ Auto-mapping         |
| Configuration Issues | ✅ All cases      | ✅ Profile registration |
| Performance          | ✅ All cases      | ✅ DI patterns          |
| Custom Converters    | ✅ Detection      | 🚧 Planned             |
| EF Integration       | 🚧 Planned       | 🚧 Planned             |

## 🏗️ Building from Source

```bash
# Clone repository
git clone https://github.com/georgepwall1991/automapper-analyser.git
cd automapper-analyser

# Restore dependencies
dotnet restore

# Build solution
dotnet build --configuration Release

# Run tests
dotnet test --configuration Release

# Create packages
dotnet pack --configuration Release --output ./packages
```

## 🧪 Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run sample scenarios
dotnet run --project samples/AutoMapperAnalyzer.Samples
```

## 📝 Contributing

We welcome contributions! Please read our [Contributing Guidelines](CONTRIBUTING.md) for details.

### Development Requirements

- .NET 9.0 SDK
- Visual Studio 2022 or JetBrains Rider
- Basic knowledge of Roslyn analyzers

### Quick Contribution Steps

1. Fork the repository
2. Create a feature branch
3. Make your changes with tests
4. Ensure all tests pass
5. Submit a pull request

## 📚 Documentation

- [**Architecture Overview**](docs/ARCHITECTURE.md) - System design and components
- [**Diagnostic Rules**](docs/DIAGNOSTIC_RULES.md) - Complete list of analyzer rules
- [**CI/CD Pipeline**](docs/CI-CD.md) - Build and deployment process
- [**Sample Code**](samples/AutoMapperAnalyzer.Samples/README.md) - Example scenarios

## 🐛 Issues & Support

- 🐛 **Bug Reports**: [GitHub Issues](https://github.com/georgepwall1991/automapper-analyser/issues)
- 💡 **Feature Requests**: [GitHub Discussions](https://github.com/georgepwall1991/automapper-analyser/discussions)
- 📖 **Documentation**: [Wiki](https://github.com/georgepwall1991/automapper-analyser/wiki)
- 💬 **Community**: [AutoMapper Discord](https://discord.gg/automapper)

## 🎯 Next Steps

### Kick-off Workshop to Review Findings & Align on Remediation Priorities

Now that we have successfully implemented our core analyzers (AM001, AM002, AM003) with 100% test coverage, it's time to strategically plan our next development phase.

**Workshop Objectives:**
- Review current analyzer capabilities and performance
- Identify high-impact areas for improvement
- Align team on technical debt vs. new feature priorities
- Establish clear success metrics for next phase

### Build a Prioritized Backlog: Quick Wins (Linting, Logging), Strategic Refactors (Architecture, Process)

#### 🚀 Quick Wins (1-2 Sprint Capacity)

**Linting & Code Quality:**
- [ ] **AM004**: Missing Property Diagnostics - Detect data loss scenarios
- [ ] **AM005**: Case Sensitivity Mapping Issues  
- [ ] Enhanced diagnostic messages with actionable suggestions
- [ ] EditorConfig integration for severity customization

**Logging & Observability:**
- [ ] Analyzer performance metrics and telemetry
- [ ] Debug logging for complex mapping scenarios
- [ ] Test coverage reporting integration
- [ ] Build-time diagnostic statistics

#### 🏗️ Strategic Refactors (3-5 Sprint Capacity)

**Architecture Improvements:**
- [ ] Shared type compatibility engine across analyzers
- [ ] Plugin architecture for custom rule extensions
- [ ] Improved semantic model caching for performance
- [ ] Rule configuration system (severity, scope, exclusions)

**Process Enhancements:**
- [ ] Code fix providers for automatic remediation
- [ ] IDE integration improvements (Visual Studio, Rider)
- [ ] CI/CD pipeline optimizations
- [ ] Documentation automation and examples

#### 🎯 Success Metrics

**Technical Excellence:**
- Maintain 95%+ test coverage across all analyzers
- Sub-100ms analysis time for typical project files
- Zero false positives in common AutoMapper patterns
- 90%+ developer satisfaction with diagnostic accuracy

**Developer Experience:**
- One-click installation and configuration
- Actionable diagnostics with clear remediation steps
- Seamless IDE integration with immediate feedback
- Comprehensive documentation and examples

### 📋 Recommended Sprint Planning

1. **Sprint 1**: Complete AM004-AM005 + Enhanced messaging
2. **Sprint 2**: Code fix providers for AM001-AM003
3. **Sprint 3**: Architecture refactoring + performance optimization
4. **Sprint 4**: IDE integration + developer experience improvements

## 📈 Roadmap

### Phase 1: Foundation ✅

- [x] Core analyzer infrastructure
- [x] Type safety validation (AM001, AM002, AM003)
- [x] Comprehensive test framework
- [x] CI/CD pipeline with quality gates

### Phase 2: Advanced Features 🚧

- [ ] Missing property detection (AM004, AM005)
- [ ] Custom converter validation
- [ ] Complex nested object mapping
- [ ] Performance optimization hints
- [ ] Code fix providers

### Phase 3: Ecosystem Integration 📋

- [ ] Visual Studio extension
- [ ] JetBrains Rider plugin
- [ ] Azure DevOps integration
- [ ] SonarQube rules

## 🏆 Recognition

This project is part of the [AutoMapper](https://automapper.org/) ecosystem, helping developers write safer and more
maintainable mapping code.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Acknowledgments

- **George Wall** - Project maintainer and lead developer
- **Jimmy Bogard** - Creator of AutoMapper
- **AutoMapper Contributors** - For the excellent mapping library
- **Roslyn Team** - For the powerful analyzer framework
- **Community** - For feedback and contributions

---

<div align="center">

**[Documentation](docs/) • [Samples](samples/) • [Contributing](CONTRIBUTING.md) • [License](LICENSE)**

Made with ❤️ by the AutoMapper community

</div>

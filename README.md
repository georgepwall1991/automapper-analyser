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

## 📈 Roadmap

### Phase 1: Foundation ✅

- [x] Core analyzer infrastructure
- [x] Type safety validation
- [x] Missing property detection
- [x] Basic code fixes

### Phase 2: Advanced Features 🚧

- [ ] Custom converter validation
- [ ] Complex nested object mapping
- [ ] Performance optimization hints
- [ ] Entity Framework integration

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

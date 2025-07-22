# AutoMapper Roslyn Analyzer

[![Build Status](https://github.com/georgepwall1991/automapper-analyser/workflows/CI%2FCD%20Pipeline/badge.svg)](https://github.com/georgepwall1991/automapper-analyser/actions)
[![NuGet](https://img.shields.io/nuget/v/AutoMapperAnalyzer.Analyzers.svg)](https://www.nuget.org/packages/AutoMapperAnalyzer.Analyzers/)
[![Coverage](https://codecov.io/gh/georgepwall1991/automapper-analyser/branch/main/graph/badge.svg)](https://codecov.io/gh/georgepwall1991/automapper-analyser)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

> 🔍 **Roslyn analyzer that detects AutoMapper configuration issues at compile-time to prevent runtime exceptions and
data loss.**

## 🚀 Implemented Analyzer Rules

This analyzer currently implements the following diagnostic rules to help you catch AutoMapper issues at compile time.

### 🛡️ Type Safety Diagnostics

- **AM001: Property Type Mismatch**: Detects when source and destination properties have incompatible types without an
  explicit type converter.
- **AM002: Nullable to Non-Nullable Assignment**: Warns when a nullable source property is mapped to a non-nullable
  destination property without proper null handling.
- **AM003: Collection Type Incompatibility**: Finds incompatible collection types between source and destination (e.g.,
  `List<string>` to `HashSet<int>`).

### 🔍 Missing Property and Mapping Diagnostics

- **AM010: Missing Destination Property**: Warns about source properties that do not have a corresponding property in
  the destination type, preventing potential data loss. (Implemented as `AM004` in code)
- **AM011: Unmapped Required Property**: Generates an error when a `required` property in the destination type is not
  mapped, which would cause a runtime exception.
- **AM012: Case Sensitivity Mismatch**: Detects when source and destination property names differ only by case, which
  can lead to unexpected mapping behavior. (Implemented as `AM005` in code)

### 🧩 Collection and Complex Type Diagnostics

- **AM020: Nested Object Mapping Issues**: Detects when nested complex objects are used without a corresponding
  `CreateMap` call for them.
- **AM021: Collection Element Type Mismatch**: Identifies mismatched element types in collection mappings that require
  explicit conversion or CreateMap configuration.
- **AM022: Infinite Recursion Risk**: Detects potential infinite recursion scenarios in self-referencing or circular
  object mappings and suggests MaxDepth or Ignore configurations.

### 🔜 Future Rules

Support for more diagnostic rules is planned, including:

- Custom conversion validation
- Configuration issues (e.g., missing profiles)
- Performance and best practice recommendations
- Entity Framework-specific mapping problems

## 📦 Installation

### 🎯 Compatibility

The AutoMapper Analyzer is fully compatible with:

| Framework | Version | Status | Notes |
|-----------|---------|--------|-------|
| .NET Framework | 4.8+ | ✅ Fully Supported | Requires AutoMapper 10.x+ |
| .NET | 6.0+ | ✅ Fully Supported | LTS version recommended |
| .NET | 5.0+ | ✅ Fully Supported | Latest features supported |
| .NET Standard | 2.0+ | ✅ Fully Supported | Analyzer targets netstandard2.0 |

The analyzer itself targets **.NET Standard 2.0**, ensuring maximum compatibility across all modern .NET platforms.

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

### 🔧 Framework-Specific Notes

#### .NET Framework 4.8
- Use AutoMapper 10.x series for maximum compatibility
- Nullable reference types supported with C# 8.0+
- Full analyzer functionality available

#### .NET 6.0+
- LTS versions with full support
- Recommended for production applications
- All analyzer features work correctly

#### .NET 5.0+
- Latest analyzer features supported
- Best performance and compatibility
- Recommended for new projects

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

## 📊 Supported Diagnostics

| Rule ID | Description                     | Analyzer Status | Code Fix Status |
|---------|---------------------------------|-----------------|-----------------|
| AM001   | Property Type Mismatch          | ✅ Implemented   | ✅ Implemented  |
| AM002   | Nullable to Non-nullable        | ✅ Implemented   | ✅ Implemented  |
| AM003   | Collection Type Incompatibility | ✅ Implemented   | ✅ Implemented  |
| AM004   | Missing Destination Property    | ✅ Implemented   | ✅ Implemented  |
| AM005   | Case Sensitivity Mismatch       | ✅ Implemented   | ✅ Implemented  |
| AM011   | Unmapped Required Property      | ✅ Implemented   | ✅ Implemented  |
| AM020   | Nested Object Mapping Issues    | ✅ Implemented   | ✅ Implemented  |
| AM021   | Collection Element Mismatch     | ✅ Implemented   | ✅ Implemented  |
| AM022   | Infinite Recursion Risk         | ✅ Implemented   | ✅ Implemented  |
| AM030+  | Custom Conversion Rules         | 🚧 Planned      | 🚧 Planned      |
| AM040+  | Configuration Rules             | 🚧 Planned      | 🚧 Planned      |
| AM050+  | Performance Rules               | 🚧 Planned      | 🚧 Planned      |
| AM060+  | EF Integration Rules            | 🚧 Planned      | 🚧 Planned      |

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

### Review and Prioritization

With a solid foundation of type safety, property mapping, and complex type analyzers in place, the next phase of
development will focus on expanding coverage to configuration, performance, and other advanced scenarios.

### Prioritized Backlog

The following is a high-level view of planned features:

#### 🚀 Immediate Priorities

- **✅ Code Fixes**: All existing analyzers now have comprehensive code fix providers with multiple fix strategies.
- **Enhanced Diagnostics**: Improve diagnostic messages with more context and actionable suggestions.
- **EditorConfig Integration**: Ensure all analyzer severities can be customized via `.editorconfig`.

#### 🏗️ Future Milestones

- **Configuration Analyzers**:
    - **AM040**: Missing `Profile` registration.
    - **AM041**: Conflicting mapping rules.
- **Performance Analyzers**:
    - **AM050**: Detect static `Mapper.Map` usage.
    - **AM052**: Find mapping chains without null propagation.
- **Logging & Telemetry**:
    - Add performance metrics for analyzer execution.
    - Integrate build-time diagnostic statistics.
- **Architectural Scalability**: Refactor core analyzer components for better extensibility.
- **Performance Optimization**: Reduce memory footprint and improve analysis speed.
- **CI/CD Pipeline Enhancements**: Add automated release notes and documentation updates.
- **Test Suite Modernization**: Migrate to latest testing frameworks and patterns.
- **Code Fix Infrastructure**: Build robust infrastructure for suggesting complex code changes.

## 📜 License

This project is licensed under the [MIT License](LICENSE). 

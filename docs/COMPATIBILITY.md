# AutoMapper Analyzer - Framework Compatibility Guide

This document provides detailed information about framework compatibility and installation requirements for the AutoMapper Analyzer.

## 🎯 Supported Frameworks

### Summary

| Framework | Min Version | Status | AutoMapper Version | Notes |
|-----------|-------------|--------|-------------------|-------|
| .NET Framework | 4.8 | ✅ Fully Supported | 10.1.1+ | Requires C# 8.0+ for full features |
| .NET | 6.0 | ✅ Fully Supported | 12.0.1+ | LTS version recommended |
| .NET | 5.0 | ✅ Fully Supported | 13.0.0+ | Modern .NET, all features |
| .NET | 6.0 | ✅ Fully Supported | 13.0.0+ | LTS version |
| .NET | 7.0 | ✅ Fully Supported | 13.0.0+ | Latest features |
| .NET | 8.0 | ✅ Fully Supported | 14.0.0+ | LTS version |
| .NET | 9.0 | ✅ Fully Supported | 14.0.0+ | Current version |
| .NET | 10.0 | ✅ Fully Supported | 14.0.0+ | Latest version |

### Analyzer Target Framework

The AutoMapper Analyzer targets **.NET Standard 2.0**, which provides compatibility with:
- .NET Framework 4.6.1+
- .NET Core 2.0+
- .NET 5.0+
- Mono 5.4+
- Xamarin.iOS 10.14+
- Xamarin.Mac 3.8+
- Xamarin.Android 8.0+
- UWP 10.0.16299+

## 🔧 Installation Instructions

### .NET Framework 4.8

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="AutoMapper" Version="10.1.1" />
    <PackageReference Include="AutoMapperAnalyzer.Analyzers" Version="2.4.1">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

**Important Notes:**
- Use AutoMapper 10.x series for .NET Framework compatibility
- Enable nullable reference types for best analyzer performance
- Requires Visual Studio 2019 16.8+ or .NET SDK 5.0+

### .NET 6.0 (LTS)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="AutoMapper" Version="12.0.1" />
    <PackageReference Include="AutoMapperAnalyzer.Analyzers" Version="2.4.1">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

### .NET 5.0+

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="AutoMapper" Version="14.0.0" />
    <PackageReference Include="AutoMapperAnalyzer.Analyzers" Version="2.4.1">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

## 🧪 Testing Compatibility

### Automated Testing

The project includes automated compatibility tests that run on every CI build:

```powershell
# Run the comprehensive compatibility test
cd test-install
pwsh test-compatibility.ps1

# Test specific framework
dotnet build NetFrameworkTest/NetFrameworkTest.csproj
dotnet build NetCoreTest/NetCoreTest.csproj
dotnet build TestPackage/TestPackage.csproj
```

### Manual Verification

Create a test project to verify the analyzer works correctly:

```csharp
using AutoMapper;

public class TestProgram
{
    public static void Main()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // This should trigger AM001 - Property Type Mismatch
            cfg.CreateMap<Source, Destination>();
        });
        
        var mapper = config.CreateMapper();
        var result = mapper.Map<Destination>(new Source());
    }
}

public class Source
{
    public string Age { get; set; } = "25"; // String
    public string ExtraData { get; set; } = "test"; // Missing in destination
}

public class Destination
{
    public int Age { get; set; } // Int - should trigger AM001
}
```

**Expected Results:**
- **AM001 Error**: Property 'Age' has incompatible types
- **AM004 Warning**: Source property 'ExtraData' will not be mapped

## 📋 Feature Support Matrix

| Feature | .NET Framework 4.8 | .NET 6.0 | .NET 8.0+ |
|---------|-------------------|---------------|-----------|
| Type Safety Analysis (AM001-AM003) | ✅ | ✅ | ✅ |
| Missing Property Detection (AM004, AM010-AM012) | ✅ | ✅ | ✅ |
| Complex Type Mapping (AM020) | ✅ | ✅ | ✅ |
| Nullable Reference Types | ✅* | ✅ | ✅ |
| Generic Type Analysis | ✅ | ✅ | ✅ |
| Collection Type Validation | ✅ | ✅ | ✅ |
| Performance Optimizations | ⚠️ | ✅ | ✅ |

*Requires C# 8.0+ and nullable context enabled

## 🔍 IDE Support

### Visual Studio

| Version | .NET Framework | .NET Core | .NET 5+ |
|---------|---------------|-----------|---------|
| VS 2019 16.8+ | ✅ | ✅ | ✅ |
| VS 2022 | ✅ | ✅ | ✅ |

### JetBrains Rider

| Version | Support Level |
|---------|--------------|
| 2021.3+ | ✅ Full Support |
| 2022.x | ✅ Full Support |
| 2023.x+ | ✅ Full Support |

### Visual Studio Code

| Extension | Support Level |
|-----------|--------------|
| C# Extension | ✅ Full Support |
| OmniSharp | ✅ Full Support |

## 🚨 Known Issues

### .NET Framework Specific

1. **AutoMapper Version Constraint**
   - Use AutoMapper 10.x for .NET Framework 4.8
   - AutoMapper 12.0+ requires .NET Standard 2.1 (not supported by .NET Framework)

2. **Nullable Reference Types**
   - Requires Visual Studio 2019 16.8+ for full support
   - May show additional warnings in older tooling

### .NET 6.0+ Specific

1. **LTS Support**
   - .NET 6.0 and .NET 8.0 are Long Term Support versions
   - Recommended for production applications

## 🛠️ Troubleshooting

### Analyzer Not Working

1. **Check Project Reference**
   ```xml
   <ProjectReference Include="path/to/AutoMapperAnalyzer.Analyzers.csproj" 
                     OutputItemType="Analyzer" 
                     ReferenceOutputAssembly="false" />
   ```

2. **Verify Package Installation**
   ```xml
   <PackageReference Include="AutoMapperAnalyzer.Analyzers" Version="2.4.1">
     <PrivateAssets>all</PrivateAssets>
     <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
   </PackageReference>
   ```

3. **Clean and Rebuild**
   ```bash
   dotnet clean
   dotnet restore
   dotnet build
   ```

### Version Conflicts

1. **AutoMapper Version Mismatch**
   - Ensure AutoMapper version is compatible with your target framework
   - Check the compatibility matrix above

2. **Multiple Analyzer Versions**
   - Remove old analyzer references
   - Use consistent versioning across projects

### IDE Issues

1. **Visual Studio Not Showing Diagnostics**
   - Restart Visual Studio
   - Check Tools → Options → Text Editor → C# → Advanced → Enable analysis

2. **Rider Not Showing Analyzers**
   - Invalidate caches and restart
   - Check Settings → Languages & Frameworks → .NET → Code Analysis

## 📞 Support

For compatibility issues:

1. **Check Issues**: [GitHub Issues](https://github.com/georgepwall1991/automapper-analyser/issues)
2. **Create Issue**: Include framework version, AutoMapper version, and project file
3. **Run Diagnostics**: Use the compatibility test script for detailed information

## 🔄 Migration Guide

### From .NET Framework to .NET Core/5+

1. Update target framework in project file
2. Update AutoMapper to latest compatible version
3. Test analyzer functionality with build
4. Update any framework-specific code

### Upgrading AutoMapper Versions

1. Check compatibility matrix for your target framework
2. Update package reference
3. Test existing mappings
4. Address any new analyzer warnings

This guide ensures smooth installation and usage across all supported .NET frameworks.

---

**Last Updated**: November 19, 2025
**Version**: 2.4.1

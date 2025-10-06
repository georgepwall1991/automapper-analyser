# Troubleshooting Guide

Common issues and solutions for the AutoMapper Roslyn Analyzer.

## Table of Contents

- [Installation Issues](#installation-issues)
- [Analyzer Not Running](#analyzer-not-running)
- [False Positives](#false-positives)
- [Performance Issues](#performance-issues)
- [IDE Integration Problems](#ide-integration-problems)
- [Build Failures](#build-failures)
- [Code Fix Issues](#code-fix-issues)
- [Getting Help](#getting-help)

---

## Installation Issues

### Package Not Found

**Problem**: `dotnet add package AutoMapperAnalyzer.Analyzers` fails with "Unable to find package"

**Solution**:

1. **Check NuGet source**:
   ```bash
   dotnet nuget list source
   ```

   Ensure `https://api.nuget.org/v3/index.json` is in the list.

2. **Clear NuGet cache**:
   ```bash
   dotnet nuget locals all --clear
   dotnet restore
   ```

3. **Verify package exists**:
   ```bash
   dotnet search AutoMapperAnalyzer.Analyzers
   ```

---

### Package Installed But Not Working

**Problem**: Package installed successfully but no diagnostics appear.

**Solution**:

1. **Verify package reference** in `.csproj`:
   ```xml
   <PackageReference Include="AutoMapperAnalyzer.Analyzers" Version="2.2.0">
       <PrivateAssets>all</PrivateAssets>
       <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
   </PackageReference>
   ```

   **Critical**: Must include `analyzers` in `IncludeAssets`.

2. **Rebuild project**:
   ```bash
   dotnet clean
   dotnet build
   ```

3. **Restart IDE** (Visual Studio, Rider, or VS Code).

---

### Version Conflicts

**Problem**: Error message about incompatible AutoMapper version.

**Solution**:

The analyzer requires **AutoMapper 10.1.1+**. Check your AutoMapper version:

```xml
<PackageReference Include="AutoMapper" Version="14.0.0" />
```

**Supported Versions**:
- AutoMapper 10.1.1+ (for .NET Framework 4.8)
- AutoMapper 12.0.1+ (for .NET 6.0)
- AutoMapper 14.0.0+ (for .NET 8.0+)

**Update if needed**:
```bash
dotnet add package AutoMapper --version 14.0.0
```

---

## Analyzer Not Running

### No Diagnostics Appearing

**Problem**: Code has mapping issues but no warnings/errors show up.

**Checklist**:

1. **✅ Is AutoMapper code recognized?**

   Ensure your code uses standard AutoMapper patterns:
   ```csharp
   public class MyProfile : Profile
   {
       public MyProfile()
       {
           CreateMap<Source, Destination>();
       }
   }
   ```

   **Not Supported** (yet):
   ```csharp
   // Dynamic mapping (not analyzed)
   mapper.CreateMap<Source, Destination>();
   ```

2. **✅ Is the analyzer enabled?**

   Check `.editorconfig`:
   ```ini
   # Ensure analyzer is not disabled
   dotnet_diagnostic.AM001.severity = error  # Not 'none'
   ```

3. **✅ Are diagnostics suppressed?**

   Look for suppression attributes:
   ```csharp
   #pragma warning disable AM001  // ❌ This disables AM001
   CreateMap<Source, Destination>();
   #pragma warning restore AM001
   ```

4. **✅ Is build successful?**

   Analyzers only run during successful compilation. Fix compilation errors first.

5. **✅ Is IDE analyzer support enabled?**

   **Visual Studio**: Tools → Options → Text Editor → C# → Advanced → Enable analyzers ✅

   **Rider**: Settings → Editor → Inspection Settings → Enable solution-wide analysis ✅

   **VS Code**: Install C# extension with analyzer support

---

### Analyzer Runs in IDE But Not CLI

**Problem**: Diagnostics show in Visual Studio but `dotnet build` succeeds without warnings.

**Solution**:

IDE analyzers and build analyzers are **different execution contexts**.

1. **Check build configuration**:
   ```xml
   <PropertyGroup>
       <!-- Ensure warnings aren't suppressed in build -->
       <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
       <WarningLevel>4</WarningLevel>
   </PropertyGroup>
   ```

2. **Enable build verbosity**:
   ```bash
   dotnet build --verbosity normal
   ```

   Look for analyzer execution messages.

3. **Check `.editorconfig` vs project settings**:

   IDE may use `.editorconfig`, while CLI uses `.csproj`. Ensure consistency.

---

### Analyzer Runs in CLI But Not IDE

**Problem**: `dotnet build` shows diagnostics but IDE doesn't.

**Solution**:

1. **Restart IDE** (analyzers load at startup).

2. **Clear IDE cache**:

   **Visual Studio**: Tools → Options → Roslyn → Clear caches

   **Rider**: File → Invalidate Caches / Restart

   **VS Code**: Reload window (Cmd/Ctrl + Shift + P → "Reload Window")

3. **Check IDE analyzer settings**:

   **Visual Studio**: Ensure "Run code analysis in background" is enabled

   **Rider**: Ensure "Solution-wide analysis" is enabled

4. **Verify OmniSharp version** (VS Code):
   ```bash
   # In VS Code terminal
   dotnet --list-sdks
   ```

   OmniSharp requires .NET SDK 6.0+.

---

## False Positives

### Incorrect Type Mismatch Warnings

**Problem**: AM001 reports type mismatch but types are compatible.

**Example**:
```csharp
public class Source { public int Age { get; set; } }
public class Dest { public long Age { get; set; } }  // int → long is valid

CreateMap<Source, Dest>();
// ❌ AM001: Type mismatch (false positive)
```

**Why This Happens**:
Analyzer conservatively reports potential issues. `int → long` is safe at runtime but detected as incompatible.

**Solutions**:

**Option 1: Suppress with justification**
```csharp
#pragma warning disable AM001  // Justification: int→long is implicitly convertible
CreateMap<Source, Dest>();
#pragma warning restore AM001
```

**Option 2: Make explicit**
```csharp
CreateMap<Source, Dest>()
    .ForMember(dest => dest.Age, opt => opt.MapFrom(src => (long)src.Age));
```

**Option 3: Report as issue**
If you believe this is a genuine false positive, please report: https://github.com/georgepwall1991/automapper-analyser/issues

---

### False Positives with Custom Conventions

**Problem**: AM004 reports missing property but custom naming convention maps it.

**Example**:
```csharp
var config = new MapperConfiguration(cfg =>
{
    cfg.AddProfile<MyProfile>();
    cfg.RecognizePrefixes("str");  // Custom convention
});

public class Source { public string strName { get; set; } }
public class Dest { public string Name { get; set; } }

CreateMap<Source, Dest>();
// ℹ️ AM004: Property 'strName' not mapped (false positive - convention handles it)
```

**Solution**:

The analyzer doesn't analyze custom conventions (yet). Suppress with justification:

```csharp
#pragma warning disable AM004  // Justification: Custom prefix convention handles strName→Name
CreateMap<Source, Dest>();
#pragma warning restore AM004
```

---

### False Positives with Inheritance

**Problem**: AM020 reports missing CreateMap but base class mapping exists.

**Example**:
```csharp
public class BaseSource { }
public class DerivedSource : BaseSource { }

public class BaseDest { }
public class DerivedDest : BaseDest { }

CreateMap<BaseSource, BaseDest>();
CreateMap<Source, Dest>();  // Has DerivedSource property
// ⚠️ AM020: Missing CreateMap<DerivedSource, DerivedDest> (may be false positive)
```

**Solution**:

Analyzer doesn't analyze inheritance mappings. If AutoMapper handles this via `IncludeBase`, suppress:

```csharp
CreateMap<Source, Dest>()
    .IncludeBase<BaseSource, BaseDest>();
#pragma warning disable AM020  // Justification: Handled by base mapping
// Property mapping...
#pragma warning restore AM020
```

---

## Performance Issues

### Slow Build Times

**Problem**: Build takes significantly longer after adding analyzer.

**Diagnosis**:

1. **Enable analyzer profiling**:
   ```bash
   dotnet build /p:ReportAnalyzer=true
   ```

2. **Check performance report**:
   ```bash
   cat obj/Debug/net9.0/AnalyzerPerformance.txt
   ```

**Solutions**:

**Option 1: Disable expensive rules during development**

`.editorconfig`:
```ini
# Disable during active development
dotnet_diagnostic.AM020.severity = none
dotnet_diagnostic.AM022.severity = none

# Re-enable for CI/PR builds
```

**Option 2: Exclude generated files**

```xml
<PropertyGroup>
    <!-- Don't analyze generated code -->
    <EnforceExtendedAnalyzerRules>false</EnforceExtendedAnalyzerRules>
</PropertyGroup>
```

**Option 3: Use incremental build**

```bash
# Build only changed projects
dotnet build --no-restore
```

**Expected Performance**:
- **Small projects (<100 files)**: +0-2 seconds
- **Medium projects (100-1000 files)**: +2-5 seconds
- **Large projects (1000+ files)**: +5-15 seconds

**Unacceptable Performance**: >30 seconds on medium projects → Report issue

---

### IDE Lag/Freezing

**Problem**: IDE becomes unresponsive during typing.

**Solution**:

1. **Disable background analysis** (temporary):

   **Visual Studio**: Tools → Options → Text Editor → C# → Advanced → Disable "Enable full solution analysis"

   **Rider**: Settings → Editor → Inspection Settings → Disable "Enable solution-wide analysis"

2. **Increase IDE memory**:

   **Visual Studio**: Modify `devenv.exe.config` to increase heap size

   **Rider**: Help → Change Memory Settings → Increase to 4096MB

3. **Check concurrent analyzers**:

   Too many analyzers can overwhelm IDE. List analyzers:
   ```bash
   dotnet list package --include-transitive | grep Analyzer
   ```

4. **Update to latest IDE version** (better analyzer support).

---

## IDE Integration Problems

### Visual Studio

#### Analyzer Not Listed in References

**Problem**: AutoMapperAnalyzer doesn't appear under "Analyzers" in Solution Explorer.

**Solution**:

1. **Rebuild solution**: Analyzers load during build.

2. **Check package path**:
   ```bash
   ls ~/.nuget/packages/automapperanalyzer.analyzers/2.2.0/analyzers/dotnet/cs/
   ```

   Should contain `AutoMapperAnalyzer.Analyzers.dll`.

3. **Manually add analyzer reference** (rare):
   ```xml
   <ItemGroup>
       <Analyzer Include="$(NuGetPackageRoot)automapperanalyzer.analyzers/2.2.0/analyzers/dotnet/cs/AutoMapperAnalyzer.Analyzers.dll" />
   </ItemGroup>
   ```

---

#### Code Fixes Not Appearing

**Problem**: Diagnostics show but lightbulb (💡) doesn't offer fixes.

**Solution**:

1. **Verify code fix provider is loaded**:

   View → Other Windows → Diagnostic Tools → Search for "AutoMapperAnalyzer"

2. **Check cursor position**:

   Cursor must be on the `CreateMap` line for fixes to appear.

3. **Try keyboard shortcut**:
   - **Windows**: `Ctrl + .` (period)
   - **Mac**: `Cmd + .` (period)

4. **Restart Visual Studio** (code fix providers cache aggressively).

---

### JetBrains Rider

#### Analyzer Not Running

**Problem**: No diagnostics appear in Rider.

**Solution**:

1. **Enable Roslyn analyzers**:

   Settings → Editor → Inspection Settings → Roslyn → Enable "Use Roslyn analyzers"

2. **Invalidate caches**:

   File → Invalidate Caches / Restart → Invalidate and Restart

3. **Check solution-wide analysis**:

   Settings → Editor → Inspection Settings → Enable solution-wide analysis

4. **Verify Rider version**:

   Requires Rider 2021.3+ for full analyzer support.

---

### VS Code (OmniSharp)

#### Analyzers Not Working

**Problem**: VS Code doesn't show analyzer diagnostics.

**Solution**:

1. **Install C# extension**:
   ```
   Extensions → Search "C#" → Install Microsoft C# extension
   ```

2. **Enable Roslyn analyzers in OmniSharp**:

   `.vscode/settings.json`:
   ```json
   {
       "omnisharp.enableRoslynAnalyzers": true,
       "omnisharp.enableEditorConfigSupport": true
   }
   ```

3. **Restart OmniSharp**:

   Command Palette (Cmd/Ctrl + Shift + P) → "OmniSharp: Restart OmniSharp"

4. **Check OmniSharp log**:

   Output panel → Select "OmniSharp Log" → Look for analyzer loading messages

5. **Verify .NET SDK version**:
   ```bash
   dotnet --version  # Should be 6.0+
   ```

---

## Build Failures

### Build Fails with Analyzer Errors

**Problem**: Build fails because of analyzer diagnostics.

**Example**:
```
error AM001: Property 'Age' has incompatible types
```

**Understanding Severity**:
- **Error** (🔴): Prevents build (default: AM001, AM003, AM011)
- **Warning** (🟡): Build succeeds but shows warning
- **Info** (🔵): Informational only

**Solutions**:

**Option 1: Fix the issue** (recommended)
```csharp
CreateMap<Source, Dest>()
    .ForMember(dest => dest.Age, opt => opt.MapFrom(src =>
        int.TryParse(src.Age, out var age) ? age : 0));
```

**Option 2: Downgrade to warning** (temporary)

`.editorconfig`:
```ini
dotnet_diagnostic.AM001.severity = warning
```

**Option 3: Suppress specific instance**
```csharp
#pragma warning disable AM001  // Justification: Legacy system limitation
CreateMap<Source, Dest>();
#pragma warning restore AM001
```

**Option 4: Disable analyzer** (not recommended)

`.csproj`:
```xml
<PropertyGroup>
    <NoWarn>AM001</NoWarn>
</PropertyGroup>
```

---

### CI/CD Build Failures

**Problem**: Build passes locally but fails in CI.

**Common Causes**:

1. **Different .NET SDK version**:
   ```yaml
   # GitHub Actions example
   - uses: actions/setup-dotnet@v3
     with:
       dotnet-version: '9.0.x'  # Match local version
   ```

2. **Missing .editorconfig**:

   Ensure `.editorconfig` is committed to repository.

3. **NuGet restore issues**:
   ```yaml
   - name: Restore dependencies
     run: dotnet restore --verbosity normal
   ```

4. **Treat warnings as errors in CI**:
   ```xml
   <PropertyGroup Condition="'$(CI)' == 'true'">
       <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
   </PropertyGroup>
   ```

---

## Code Fix Issues

### Code Fix Produces Invalid Syntax

**Problem**: Applying code fix results in compilation error.

**Example**: Generated code has syntax error like missing semicolon.

**Solution**:

1. **Undo the fix**: `Ctrl/Cmd + Z`

2. **Report the issue** with:
   - Original code (before fix)
   - Code after fix
   - Expected result

   GitHub: https://github.com/georgepwall1991/automapper-analyser/issues

3. **Manual fix as workaround**:

   Apply the suggested logic manually until fix is updated.

---

### Code Fix Doesn't Preserve Formatting

**Problem**: Code fix works but changes indentation/formatting.

**Solution**:

This is a known limitation. After applying fix:

```bash
# Reformat with dotnet format
dotnet format
```

Or use IDE formatting:
- **Visual Studio**: `Ctrl + K, Ctrl + D`
- **Rider**: `Ctrl + Alt + L`
- **VS Code**: `Shift + Alt + F`

---

### Multiple Code Fixes Conflict

**Problem**: Applying multiple fixes creates conflicting code.

**Example**:
```csharp
CreateMap<Source, Dest>()
    .ForMember(dest => dest.Age, opt => opt.MapFrom(...))
    .ForMember(dest => dest.Age, opt => opt.MapFrom(...));  // ❌ Duplicate
```

**Solution**:

Apply fixes **one at a time** and rebuild after each:

```bash
# 1. Apply first fix
dotnet build

# 2. Apply second fix
dotnet build
```

Use "Fix All" carefully - review changes before committing.

---

## Getting Help

### Before Reporting an Issue

**Checklist**:

1. ✅ Search [existing issues](https://github.com/georgepwall1991/automapper-analyser/issues)
2. ✅ Verify you're using latest version:
   ```bash
   dotnet list package | grep AutoMapperAnalyzer
   ```
3. ✅ Create minimal reproduction (simplest code that shows issue)
4. ✅ Check [documentation](../README.md)

---

### Reporting Issues

**Include in report**:

1. **Environment**:
   ```bash
   dotnet --info
   # Paste output
   ```

2. **Package versions**:
   ```bash
   dotnet list package
   # Include AutoMapper and analyzer versions
   ```

3. **Minimal reproduction**:
   ```csharp
   public class Source { public string Age { get; set; } }
   public class Dest { public int Age { get; set; } }

   public class Profile : Profile
   {
       public Profile()
       {
           CreateMap<Source, Dest>();
           // Expected: AM001 diagnostic
           // Actual: No diagnostic
       }
   }
   ```

4. **Expected vs Actual behavior**

5. **Screenshots** (if relevant)

**Where to report**:
- Bugs: https://github.com/georgepwall1991/automapper-analyser/issues
- Questions: https://github.com/georgepwall1991/automapper-analyser/discussions

---

### Community Support

- **GitHub Discussions**: https://github.com/georgepwall1991/automapper-analyser/discussions
- **Stack Overflow**: Tag `automapper` + `roslyn-analyzer`
- **AutoMapper Discord**: https://discord.gg/automapper (mention analyzer)

---

## Diagnostic Information

### Collect Diagnostic Logs

**Build logs**:
```bash
dotnet build --verbosity diagnostic > build.log 2>&1
```

**MSBuild binary logs** (for deep debugging):
```bash
dotnet build -bl:msbuild.binlog
# Analyze with https://msbuildlog.com/
```

**Analyzer performance**:
```bash
dotnet build /p:ReportAnalyzer=true
cat obj/Debug/net9.0/AnalyzerPerformance.txt
```

---

## Known Limitations

### Not Currently Supported

1. **Dynamic mappings**:
   ```csharp
   mapper.CreateMap(sourceType, destType);  // ❌ Not analyzed
   ```

2. **Custom naming conventions** (partially supported):
   ```csharp
   cfg.RecognizePrefixes("str", "m_");  // ❌ Not detected
   ```

3. **Inheritance-based mappings**:
   ```csharp
   CreateMap<BaseSource, BaseDest>()
       .Include<DerivedSource, DerivedDest>();  // ⚠️ Limited support
   ```

4. **Conditional mappings**:
   ```csharp
   .ForMember(dest => dest.Age, opt => opt.Condition(src => src.Age > 0));
   // ⚠️ Condition not analyzed
   ```

5. **Value converters** (partial support):
   ```csharp
   .ForMember(dest => dest.Age, opt => opt.ConvertUsing<CustomConverter>());
   // ⚠️ Converter validation limited
   ```

---

## Quick Reference

### Common Error Codes

| Code | Issue | Quick Fix |
|------|-------|-----------|
| AM001 | Type mismatch | Add `.ForMember()` with conversion |
| AM002 | Nullable issue | Provide default or make nullable |
| AM003 | Collection incompatible | Convert collection elements |
| AM004 | Missing property | Add property or `.Ignore()` |
| AM011 | Required property | Provide value for required property |
| AM020 | Nested object | Add `CreateMap` for nested types |
| AM022 | Circular reference | Add `.MaxDepth()` |
| AM031 | Performance issue | Move operation before mapping |

---

**Last Updated**: 2025-06-10
**Version**: 2.2.0
**Need more help?** https://github.com/georgepwall1991/automapper-analyser/discussions

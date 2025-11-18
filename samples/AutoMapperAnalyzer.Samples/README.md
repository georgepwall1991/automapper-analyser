# AutoMapper Analyzer - Samples

This project demonstrates all analyzer rules provided by the **AutoMapperAnalyzer.Analyzers** package. Each example
shows both the **problematic code** that triggers a diagnostic and the **correct solution**.

## 🎯 Purpose

These samples serve two key purposes:

1. **Documentation** - Show developers what issues the analyzer detects and how to fix them
2. **Verification** - Prove that the analyzer works correctly when installed as a NuGet package

## 📋 Analyzer Rules Reference

### ✅ Implemented Rules (13 total)

| Rule ID   | Category      | Description                     | Demo File                                       | Pragma? |
|-----------|---------------|---------------------------------|-------------------------------------------------|---------|
| **AM001** | Type Safety   | Property Type Mismatch          | TypeSafety/TypeSafetyExamples.cs                | ✅       |
| **AM002** | Type Safety   | Nullable Compatibility          | TypeSafety/TypeSafetyExamples.cs                | ✅       |
| **AM003** | Type Safety   | Collection Type Incompatibility | TypeSafety/TypeSafetyExamples.cs                | ✅       |
| **AM004** | Properties    | Missing Destination Property    | MissingProperties/MissingPropertyExamples.cs    | ✅       |
| **AM005** | Properties    | Case Sensitivity Mismatch       | MissingProperties/MissingPropertyExamples.cs    | ✅       |
| **AM011** | Properties    | Unmapped Required Property      | MissingProperties/MissingPropertyExamples.cs    | ✅       |
| **AM020** | Complex Types | Nested Object Mapping Missing   | CodeFixDemo.cs                                  | ✅       |
| **AM021** | Complex Types | Collection Element Mismatch     | ComplexTypes/ComplexTypeMappingExamples.cs      | ✅       |
| **AM022** | Complex Types | Infinite Recursion Risk         | ComplexTypes/ComplexTypeMappingExamples.cs      | ✅       |
| **AM030** | Conversions   | Custom Type Converter Issues    | Conversions/TypeConverterExamples.cs            | ✅       |
| **AM031** | Performance   | Performance Warning             | Performance/PerformanceExamples.cs              | ✅       |
| **AM041** | Configuration | Duplicate Mapping Registration  | Configuration/AM041_DuplicateMappingExamples.cs | ✅       |
| **AM050** | Configuration | Redundant MapFrom Configuration | Configuration/AM050_RedundantMapFromExamples.cs | ✅       |

### 🔮 Future Implementation (Planned)

The following rules are demonstrated in sample files but **not yet implemented** in the analyzer:

- **AM032** - Advanced Null Propagation
- **AM040** - Missing Profile Registration (Configuration/)

## 🔧 Understanding #pragma Warnings

### Why Do We Use Pragmas?

The sample code intentionally contains **problematic AutoMapper configurations** to demonstrate what the analyzer
detects. However, we still want the project to build successfully.

```csharp
// ❌ This code has an issue that triggers AM001
#pragma warning disable AM001
cfg.CreateMap<StringSource, IntDestination>();  // Type mismatch!
#pragma warning restore AM001
```

**The pragma allows:**

- ✅ The build to succeed (no compilation errors)
- ✅ The diagnostic to still appear in your IDE
- ✅ The code fix to be testable and demonstrable
- ✅ CI/CD pipelines to pass while showing expected warnings

### Pattern We Follow

Each problematic example follows this pattern:

```csharp
/// <summary>
///     AM###: Rule Description
///     This should trigger AM### diagnostic
/// </summary>
public void ExampleMethod()
{
    var config = new MapperConfiguration(cfg =>
    {
        // ❌ AM###: Explanation of the problem
#pragma warning disable AM###
        cfg.CreateMap<Source, Destination>();  // Problematic code
#pragma warning restore AM###
    });

    // ... rest of example showing the runtime issue
}
```

## 🚀 How to Use These Samples

### 1. Install the Analyzer Package

```bash
cd samples/AutoMapperAnalyzer.Samples
dotnet build
```

The analyzer is automatically referenced via ProjectReference in development.

### 2. Observe Diagnostics in Your IDE

Open any example file and you'll see:

- 🔴 Squiggly lines under problematic code (even with pragmas)
- 💡 Light bulb suggestions for code fixes
- ℹ️ Hover tooltips explaining the issue

### 3. Test Code Fixes

1. Place your cursor on the diagnostic
2. Press `Ctrl+.` (VS Code) or `Alt+Enter` (Rider)
3. Select the suggested code fix
4. See the corrected code automatically generated

### 4. Compare with Correct Examples

Each file includes a `Correct*Examples` class showing the proper way to handle each scenario:

```csharp
public class TypeSafetyExamples
{
    // ❌ Demonstrates AM001 with pragma
    public void PropertyTypeMismatchExample() { ... }
}

public class CorrectTypeSafetyExamples
{
    // ✅ Shows the correct solution
    public void CorrectTypeConversionExample() { ... }
}
```

## 📁 Project Structure

```
samples/AutoMapperAnalyzer.Samples/
├── README.md                           # This file
├── Program.cs                          # Entry point (optional runner)
├── TypeSafety/
│   └── TypeSafetyExamples.cs          # AM001, AM002, AM003
├── MissingProperties/
│   └── MissingPropertyExamples.cs     # AM004, AM005, AM011
├── ComplexTypes/
│   └── ComplexTypeMappingExamples.cs  # AM021, AM022
├── Conversions/
│   └── TypeConverterExamples.cs       # AM030
├── CodeFixDemo.cs                      # AM020
├── Configuration/                      # ⚠️ Future features (AM040-042)
│   └── ConfigurationExamples.cs
└── Performance/                        # ⚠️ Future features (AM050-052)
    └── PerformanceExamples.cs
```

## 🧪 Testing the Package

When the analyzer is installed via NuGet (not ProjectReference), you can verify it works by:

### Step 1: Install from NuGet

```bash
dotnet add package AutoMapperAnalyzer.Analyzers --version 2.0.0
```

### Step 2: Build and Check Warnings

```bash
dotnet build --verbosity normal
```

Expected output:

```
TypeSafetyExamples.cs(19,13): warning AM001: Property 'Age' type mismatch...
MissingPropertyExamples.cs(20,13): warning AM004: Source property 'ImportantData'...
ComplexTypeMappingExamples.cs(21,13): warning AM021: Collection element types...
```

### Step 3: Verify in CI/CD

The CI pipeline (`../../.github/workflows/ci.yml`) includes a step that:

1. Builds the samples project
2. Expects analyzer warnings
3. Confirms the build succeeds with warnings present

```yaml
- name: Build samples (with analyzer warnings)
  run: |
    echo "Building samples (expects analyzer warnings for demonstration)..."
    dotnet build samples/AutoMapperAnalyzer.Samples --configuration Release
    echo "✅ Samples built successfully (analyzer warnings are expected)"
```

## 📚 Rule Categories

### 🛡️ Type Safety (AM001-003)

Catch type mismatches before runtime:

- String → Int without conversion
- Nullable → Non-nullable without handling
- List&lt;string&gt; → HashSet&lt;int&gt; without conversion

### 📝 Property Mapping (AM004-005, AM011)

Ensure all properties are correctly mapped:

- Detect properties that will be lost
- Find case sensitivity issues
- Identify required properties without values

### 🔗 Complex Types (AM020-022)

Handle nested objects and collections:

- Missing nested type mappings
- Collection element type mismatches
- Circular reference detection

### 🔄 Type Converters (AM030)

Validate custom type conversions:

- Missing converters for incompatible types
- Invalid converter implementations
- Null safety in conversions

## 💡 Tips for Using the Analyzer

1. **Don't Suppress Real Issues** - Use `#pragma` only for demonstration/testing, not production code
2. **Trust the Code Fixes** - The analyzer provides automatic fixes for most issues
3. **Review BEFORE Mapping** - The analyzer catches issues at compile-time, preventing runtime errors
4. **Test with Real Data** - Some issues only appear with null or edge-case values

## 🤝 Contributing Examples

To add a new example:

1. Add a new method to the appropriate examples file
2. Follow the pragma pattern shown above
3. Include both problematic and correct versions
4. Add clear comments explaining the issue
5. Update this README's rule table

## 📊 Quick Reference Card

| If you see... | It means...                  | Fix by...                                        |
|---------------|------------------------------|--------------------------------------------------|
| AM001         | Type mismatch                | Add `.ForMember()` with conversion               |
| AM002         | Nullable issue               | Add null handling with `??`                      |
| AM003         | Collection incompatible      | Use `.ToList()`, `.ToArray()`, or `.Select()`    |
| AM004         | Missing destination          | Add `.ForSourceMember()` with `.DoNotValidate()` |
| AM005         | Case mismatch                | Add explicit `.ForMember()` mapping              |
| AM011         | Required unmapped            | Add `.ForMember()` with default value            |
| AM020         | Nested object missing        | Add `CreateMap<Nested, NestedDto>()`             |
| AM021         | Collection elements mismatch | Add mapping for element types                    |
| AM022         | Circular reference           | Use `.PreserveReferences()` or break cycle       |
| AM030         | Converter needed             | Use `.ConvertUsing()` or `.MapFrom()`            |
| AM031         | Performance issue            | Cache result or compute before mapping           |
| AM041         | Duplicate mapping            | Remove duplicate `CreateMap`                     |
| AM050         | Redundant MapFrom            | Remove `ForMember` call                          |

---

**Last Updated**: 2025-11-18
**Analyzer Version**: 2.4.0+
**AutoMapper Version**: 14.0.0

For more information, see the [main project README](../../README.md).

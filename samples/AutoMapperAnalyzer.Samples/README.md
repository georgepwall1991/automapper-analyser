# 🔍 AutoMapper Analyzer Samples

This project contains comprehensive examples of AutoMapper configurations that demonstrate issues the **AutoMapper Roslyn Analyzer** will detect and prevent at compile-time.

## 🚀 Running the Samples

```bash
dotnet run --project samples/AutoMapperAnalyzer.Samples
```

## 📁 Sample Categories

### 🛡️ Type Safety Issues (`TypeSafety/`)

Examples that demonstrate type-related mapping problems:

- **AM001: Property Type Mismatch** - Source and destination properties have incompatible types
  ```csharp
  // ❌ Problem: string -> int without converter
  class Source { public string Age { get; set; } }
  class Dest { public int Age { get; set; } }
  ```

- **AM002: Nullable to Non-Nullable Assignment** - Mapping nullable to non-nullable without null handling
  ```csharp
  // ❌ Problem: Could throw NullReferenceException
  class Source { public string? Name { get; set; } }
  class Dest { public string Name { get; set; } }
  ```

- **AM003: Collection Type Incompatibility** - Incompatible collection types
  ```csharp
  // ❌ Problem: List<string> -> HashSet<int>
  class Source { public List<string> Items { get; set; } }
  class Dest { public HashSet<int> Items { get; set; } }
  ```

### 🔍 Missing Properties (`MissingProperties/`)

Examples that demonstrate data loss and missing property scenarios:

- **AM010: Missing Destination Property** - Source property without corresponding destination (data loss)
- **AM011: Unmapped Required Property** - Required destination property not mapped from source
- **AM012: Case Sensitivity Mismatch** - Properties differing only in casing

### ⚙️ Configuration Issues (`Configuration/`)

Examples that demonstrate AutoMapper configuration problems:

- **AM040: Missing Profile Registration** - Profile defined but not registered with mapper
- **AM041: Conflicting Mapping Rules** - Multiple conflicting mappings for same property
- **AM042: Ignore vs MapFrom Conflict** - Property both ignored and explicitly mapped

### ⚡ Performance Issues (`Performance/`)

Examples that demonstrate performance and best practice violations:

- **AM050: Static Mapper Usage** - Using direct mapper creation instead of dependency injection
- **AM051: Repeated Mapping Configuration** - Same mapping configured multiple times
- **AM052: Missing Null Propagation** - Mapping without proper null handling in chains

## 🎯 Expected Analyzer Diagnostics

When our analyzer is complete, these samples will trigger the following diagnostics:

| Sample Scenario | Diagnostic ID | Severity | Description |
|----------------|---------------|----------|-------------|
| `PropertyTypeMismatchExample()` | AM001 | Error | Property type mismatch without converter |
| `NullableToNonNullableExample()` | AM002 | Warning | Nullable to non-nullable without null handling |
| `CollectionTypeIncompatibilityExample()` | AM003 | Error | Incompatible collection types |
| `MissingDestinationPropertyExample()` | AM010 | Warning | Source property not mapped (data loss) |
| `UnmappedRequiredPropertyExample()` | AM011 | Error | Required property not mapped |
| `CaseSensitivityMismatchExample()` | AM012 | Info | Case sensitivity mismatch |
| `MissingProfileRegistrationExample()` | AM040 | Warning | Profile not registered |
| `ConflictingMappingRulesExample()` | AM041 | Error | Conflicting mapping rules |
| `IgnoreVsMapFromConflictExample()` | AM042 | Error | Ignore vs MapFrom conflict |
| `StaticMapperUsageExample()` | AM050 | Info | Static mapper usage |
| `RepeatedMappingConfigurationExample()` | AM051 | Warning | Repeated configuration |
| `MissingNullPropagationExample()` | AM052 | Warning | Missing null propagation |

## ✅ Correct Patterns

Each category also includes examples of **correct** AutoMapper usage patterns:

- `CorrectTypeSafetyExamples` - Proper type conversions and null handling
- `CorrectMissingPropertyExamples` - Explicit data loss handling and required field mapping
- `CorrectConfigurationExamples` - Proper profile registration and single mapping rules
- `CorrectPerformanceExamples` - Dependency injection and null-safe mappings

## 🔧 Testing Strategy

These samples serve multiple purposes:

1. **Manual Testing** - Run the application to see runtime behavior
2. **Analyzer Testing** - Each example will be used in analyzer unit tests
3. **Documentation** - Clear examples of what the analyzer catches
4. **Regression Testing** - Ensure analyzer doesn't produce false positives

## 📚 Sample Output

When you run the samples, you'll see output like:

```
🔍 AutoMapper Analyzer Sample Scenarios
=========================================

⚠️  Type Safety Issues:
  - Property type mismatch (string -> int)
Mapped: John, Age: 25
  - Nullable to non-nullable assignment  
Mapped: ID=1, Name=''
  - Collection type incompatibility
❌ Runtime error: Error mapping types...

⚠️  Missing Property Issues:
  - Missing destination property (data loss)
❌ ImportantData was lost in mapping!
```

## 🚧 Development Notes

- **Test-Driven Development**: Each sample scenario has corresponding unit tests in the test project
- **Incremental Implementation**: Samples are implemented before the analyzer rules
- **Real-World Scenarios**: Examples based on common AutoMapper misconfigurations
- **IDE Integration**: When analyzer is complete, these issues will show as diagnostics in IDEs

## 🔄 Usage in Analyzer Development

1. **Phase 2**: Use these samples to develop actual analyzer implementations
2. **Phase 3**: Verify code fixes work correctly with these examples  
3. **Phase 4**: Performance testing using these scenarios
4. **Phase 5**: Integration testing with real projects

---

*This sample repository is part of the **AutoMapper Roslyn Analyzer** project, designed to catch AutoMapper configuration issues at compile-time and prevent runtime exceptions.* 
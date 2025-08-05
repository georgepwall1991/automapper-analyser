# AutoMapper Analyzer - Claude AI Assistant Context

## 🎯 Project Overview
This is a comprehensive Roslyn analyzer project for AutoMapper that detects configuration issues at compile-time. The project follows Test-Driven Development (TDD) principles and has achieved significant functionality across multiple phases.

## 📊 Current Status (Phase 4+ Complete)

### ✅ Completed Features
- **Phase 1**: Foundation & TDD Infrastructure
- **Phase 2**: Core Type Safety Analyzers (AM001-AM003)
- **Phase 3**: Missing Property Analysis (AM004-AM005, AM011)
- **Phase 4**: Complex Type & Collection Analysis (AM020-AM022)

### 🧪 Active Analyzers
| Rule | Description | Code Fix | Status |
|------|-------------|----------|---------|
| AM001 | Property Type Mismatch | ✅ | Production |
| AM002 | Nullable Compatibility | ✅ | Production |
| AM003 | Collection Type Incompatibility | ✅ | Production |
| AM004 | Missing Destination Property | ✅ | Production |
| AM005 | Case Sensitivity Mismatch | ✅ | Production |
| AM011 | Unmapped Required Property | ✅ | Production |
| AM020 | Nested Object Mapping | ✅ | Production |
| AM021 | Collection Element Mismatch | ❌ | **Issues Present** |
| AM022 | Infinite Recursion Risk | ✅ | Production |

## 🚨 Immediate Issues to Address

### 1. Code Quality Issues
- **Null Reference Warnings**: Multiple CS8604 warnings in CodeFixes project
- **Missing XML Documentation**: CS1591 warnings across all code fix providers
- **AM021 Test Failures**: Collection element mismatch tests are commented out

### 2. Project Organization Issues
- **Outdated Sprint Tracking**: SPRINT_TRACKING.md shows July 2024 dates but project shows 2025 dates
- **Missing CLAUDE.md**: No central context file for AI assistant
- **Documentation Gaps**: Missing architecture and diagnostic rules documentation

## 🎯 Next Steps (Priority Order)

### Phase 5A: Code Quality & Stability (HIGH PRIORITY)
1. **Fix Null Reference Warnings**
   - Add null checks in all CodeFix providers
   - Implement proper null-conditional operators
   - Target files: `AM003`, `AM004`, `AM005`, `AM011` CodeFixProviders

2. **Complete XML Documentation**
   - Add XML comments to all public members
   - Document analyzer rules and code fix providers
   - Ensure documentation generation works correctly

3. **Fix AM021 Collection Element Tests**
   - Uncomment failing tests in `AM021_CollectionElementMismatchTests.cs`
   - Debug and resolve test failures
   - Ensure analyzer and code fixes work correctly

### Phase 5B: Enhanced Analysis (MEDIUM PRIORITY)
4. **AM030: Custom Type Converter Analysis**
   - Detect invalid/missing type converters
   - Validate converter signatures and null handling
   - Create converter suggestion code fixes

5. **AM031: Performance Warning Analysis**
   - Detect expensive operations in mapping expressions
   - Suggest caching and optimization patterns
   - Create performance-focused code fixes

### Phase 6: Configuration & Profile Analysis
6. **AM040: Missing Profile Registration**
   - Detect unregistered AutoMapper profiles
   - Validate profile discovery patterns
   - Create auto-registration code fixes

7. **AM041: Conflicting Mapping Rules**
   - Detect conflicting mapping configurations
   - Provide conflict resolution suggestions
   - Create rule cleanup code fixes

### Phase 7: Advanced Features
8. **AM050+: Performance & Best Practices**
   - Static mapper usage analysis (DI patterns)
   - Repeated configuration detection
   - Null propagation improvements

## 🔧 Development Commands

### Build & Test
```bash
# Clean build
dotnet clean
dotnet build

# Run all tests
dotnet test

# Run specific test category
dotnet test --filter "Category=AM021"
```

### Package Creation
```bash
# Create NuGet packages
dotnet pack --configuration Release

# Test package installation
cd test-install/NetCoreTest
dotnet add package AutoMapperAnalyzer.Analyzers
```

### Sample Testing
```bash
# Run samples to see analyzer in action
cd samples/AutoMapperAnalyzer.Samples
dotnet build --verbosity normal
```

## 📋 Project Structure

```
automapper-analyser/
├── src/
│   ├── AutoMapperAnalyzer.Analyzers/    # Core analyzer rules
│   └── AutoMapperAnalyzer.CodeFixes/    # Code fix providers
├── tests/
│   └── AutoMapperAnalyzer.Tests/        # Comprehensive test suite
├── samples/
│   └── AutoMapperAnalyzer.Samples/      # Example usage scenarios
├── docs/                                # Documentation (needs expansion)
├── test-install/                        # Package compatibility testing
└── nupkg/                              # Published packages
```

## 🎨 Code Style Guidelines

### Analyzer Development
- Follow TDD: Write failing tests first
- Each analyzer must have corresponding code fixes
- Maintain >90% test coverage
- Use semantic model analysis for type checking
- Handle edge cases gracefully

### Code Quality Standards
- Enable nullable reference types
- Add XML documentation for all public APIs
- Use consistent naming conventions (AM###)
- Follow .editorconfig rules
- Handle null cases defensively

## 🚀 Release Management

### Current Version Strategy
- Version format: `{Major}.{Minor}.{BuildNumber}`
- BuildNumber auto-increments with YYYYMMDD format
- Published on NuGet as `AutoMapperAnalyzer.Analyzers`

### Compatibility Matrix
| Platform | .NET Version | AutoMapper | Status |
|----------|--------------|------------|---------|
| .NET Framework | 4.8+ | 10.1.1+ | ✅ Supported |
| .NET | 6.0+ | 12.0.1+ | ✅ Supported |
| .NET | 8.0+ | 14.0.0+ | ✅ Supported |
| .NET | 9.0+ | 14.0.0+ | ✅ Supported |

## 🔍 Diagnostic ID Mapping

### Reserved ID Ranges
- **AM001-AM009**: Core type safety
- **AM010-AM019**: Property mapping
- **AM020-AM029**: Complex types & collections
- **AM030-AM039**: Custom conversions
- **AM040-AM049**: Configuration & profiles
- **AM050-AM059**: Performance & best practices
- **AM060-AM069**: Entity Framework integration

## 🧪 Testing Strategy

### Test Categories
1. **Unit Tests**: Individual analyzer logic
2. **Integration Tests**: Full diagnostic scenarios  
3. **CodeFix Tests**: Code transformation validation
4. **Performance Tests**: Large codebase analysis
5. **Compatibility Tests**: Cross-platform validation

### Test Organization
```
tests/AutoMapperAnalyzer.Tests/
├── AM###_AnalyzerTests.cs           # Core analyzer tests
├── AM###_CodeFixTests.cs            # Code fix tests
├── CodeFixes/AM###_CodeFixTests.cs  # Complex fix scenarios
├── Framework/                       # Test infrastructure
└── Helpers/                        # Test utilities
```

## 🎯 Success Metrics

### Quality Gates
- [ ] All compiler warnings resolved (CS8604, CS1591)
- [ ] Test coverage >95%
- [ ] All AM021 tests passing
- [ ] Performance <100ms per file
- [ ] Memory usage <50MB for large projects

### User Experience Goals
- Compile-time validation prevents runtime errors
- Intelligent code fixes reduce manual configuration
- Clear diagnostic messages with actionable suggestions
- Seamless IDE integration (VS, VS Code, Rider)

## 💡 Future Enhancements

### Documentation Expansion
- [ ] Complete architecture guide
- [ ] Diagnostic rules reference
- [ ] Integration examples
- [ ] Troubleshooting guide

### Advanced Features
- [ ] Custom attribute support
- [ ] Dynamic mapping analysis
- [ ] Dependency injection patterns
- [ ] Entity Framework integration

### Tooling Improvements
- [ ] Visual Studio extension
- [ ] MSBuild integration
- [ ] Configuration templates
- [ ] Metrics dashboard

---

## 🤖 Claude Assistant Notes

**Last Updated**: 2025-08-05  
**Project Health**: Good (with quality issues to address)  
**Priority Focus**: Fix null warnings and AM021 test failures before adding new features

When working on this project:
1. Always run tests before and after changes
2. Focus on code quality issues first
3. Follow TDD patterns for new analyzers
4. Update documentation as you implement features
5. Test package installation in test-install projects
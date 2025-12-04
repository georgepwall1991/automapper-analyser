# Contributing to AutoMapper Analyzer

Thank you for your interest in contributing to the AutoMapper Roslyn Analyzer! This document provides guidelines and instructions for contributing.

## Table of Contents

- [Getting Started](#getting-started)
- [Development Environment](#development-environment)
- [Project Structure](#project-structure)
- [Adding a New Analyzer](#adding-a-new-analyzer)
- [Adding a New Code Fix](#adding-a-new-code-fix)
- [Testing Guidelines](#testing-guidelines)
- [Code Style](#code-style)
- [Pull Request Process](#pull-request-process)

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally:
   ```bash
   git clone https://github.com/YOUR-USERNAME/automapper-analyser.git
   cd automapper-analyser
   ```
3. **Create a branch** for your feature or fix:
   ```bash
   git checkout -b feature/your-feature-name
   ```

## Development Environment

### Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or later
- An IDE with Roslyn support:
  - Visual Studio 2022 (recommended)
  - JetBrains Rider
  - VS Code with C# extension

### Building the Project

```bash
# Restore dependencies
dotnet restore

# Build all projects
dotnet build

# Run tests
dotnet test
```

### Debugging Analyzers

1. Set `AutoMapperAnalyzer.Analyzers` as the startup project
2. Press F5 to launch the experimental Visual Studio instance
3. Open a test project in the experimental instance
4. Set breakpoints in your analyzer code

## Project Structure

```
automapper-analyser/
├── src/
│   └── AutoMapperAnalyzer.Analyzers/
│       ├── TypeSafety/           # AM001-AM003 analyzers
│       ├── DataIntegrity/        # AM004-AM011 analyzers
│       ├── ComplexMappings/      # AM020-AM030 analyzers
│       ├── Performance/          # AM031 analyzer + strategies
│       ├── Configuration/        # AM041-AM050 analyzers
│       └── Helpers/              # Shared utilities
├── tests/
│   └── AutoMapperAnalyzer.Tests/
│       ├── TypeSafety/
│       ├── DataIntegrity/
│       ├── ComplexMappings/
│       ├── Performance/
│       ├── Configuration/
│       └── Framework/
├── samples/                      # Example usage
└── docs/                         # Documentation
```

### Key Helper Classes

- **`AutoMapperAnalyzerBase`**: Base class for analyzers with common functionality
- **`AutoMapperCodeFixProviderBase`**: Base class for code fix providers
- **`AutoMapperAnalysisHelpers`**: Shared analysis utilities
- **`AutoMapperConstants`**: Centralized string constants
- **`StringUtilities`**: String manipulation helpers (fuzzy matching, etc.)
- **`AnalyzerConfiguration`**: .editorconfig options support

## Adding a New Analyzer

### 1. Choose a Diagnostic ID

Follow the naming convention:
- **AM001-AM009**: Type Safety
- **AM010-AM019**: Data Integrity
- **AM020-AM039**: Complex Mappings
- **AM040-AM049**: Configuration
- **AM050+**: Other/Future

### 2. Create the Analyzer Class

```csharp
using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.YourCategory;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AMXXX_YourAnalyzer : AutoMapperAnalyzerBase
{
    public static readonly DiagnosticDescriptor Rule = new(
        "AMXXX",
        "Your Rule Title",
        "Your diagnostic message with {0} placeholders",
        AutoMapperConstants.CategoryYourCategory,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Detailed description of the issue.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    protected override void AnalyzeCreateMapInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        // Your analysis logic here
        // Use base class helpers:
        // - GetMappableProperties()
        // - IsPropertyConfigured()
        // - AreTypesCompatible()
        // - CreateDiagnostic()
    }
}
```

### 3. Add Tests

Create test file in `tests/AutoMapperAnalyzer.Tests/YourCategory/`:

```csharp
public class AMXXX_YourAnalyzerTests
{
    [Fact]
    public async Task YourScenario_ShouldReportDiagnostic()
    {
        var test = @"
            // Your test code here
        ";

        var expected = DiagnosticResult.CompilerWarning("AMXXX")
            .WithLocation(lineNumber, column)
            .WithArguments("arg1", "arg2");

        await new CSharpAnalyzerTest<AMXXX_YourAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ExpectedDiagnostics = { expected }
        }.RunAsync();
    }
}
```

## Adding a New Code Fix

### 1. Create the Code Fix Provider

```csharp
using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.YourCategory;

[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public class AMXXX_YourCodeFixProvider : AutoMapperCodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["AMXXX"];

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        await ProcessDiagnosticsAsync(
            context,
            propertyNames: ["PropertyName"],
            registerBulkFixes: null, // or implement bulk fixes
            registerPerPropertyFixes: (ctx, diagnostic, invocation, props, model, root) =>
            {
                // Register your fixes here
            });
    }
}
```

## Testing Guidelines

### Test Categories

1. **Positive Tests**: Verify the analyzer reports diagnostics correctly
2. **Negative Tests**: Verify no false positives
3. **Code Fix Tests**: Verify fixes produce correct code
4. **Edge Cases**: Test boundary conditions

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test category
dotnet test --filter "Category=TypeSafety"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Naming Convention

```
MethodName_Scenario_ExpectedBehavior
```

Example: `CreateMap_WithTypeMismatch_ReportsDiagnostic`

## Code Style

### General Guidelines

- Follow Microsoft C# coding conventions
- Use meaningful names for variables and methods
- Add XML documentation to all public APIs
- Keep methods focused and under 50 lines when possible

### Specific Rules

1. **Constants**: Use `AutoMapperConstants` for string literals
2. **String Operations**: Use `StringUtilities` for comparisons
3. **Nullability**: Enable nullable reference types, use proper annotations
4. **Collections**: Return `IReadOnlyList<T>` or `ImmutableArray<T>` from public APIs

### EditorConfig

The project includes `.editorconfig` for consistent formatting. Your IDE should automatically apply these rules.

## Pull Request Process

### Before Submitting

1. **Run all tests**: `dotnet test`
2. **Build in Release mode**: `dotnet build -c Release`
3. **Check for warnings**: Ensure no new warnings are introduced
4. **Update documentation** if needed

### PR Checklist

- [ ] Code follows the project style guidelines
- [ ] Tests are added for new functionality
- [ ] All tests pass
- [ ] XML documentation is added for public APIs
- [ ] CHANGELOG is updated (for significant changes)
- [ ] Commit messages are clear and descriptive

### Commit Message Format

```
type: brief description

Longer description if needed.

Fixes #issue-number
```

Types: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`

### Review Process

1. Submit your PR with a clear description
2. Address any feedback from reviewers
3. Once approved, a maintainer will merge your PR

## Questions?

- Open an issue for bugs or feature requests
- Start a discussion for questions
- Check existing issues before creating new ones

Thank you for contributing!

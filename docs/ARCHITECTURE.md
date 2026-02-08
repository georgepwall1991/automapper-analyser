# Architecture Guide

This document provides a comprehensive overview of the AutoMapper Roslyn Analyzer architecture, design patterns, and implementation details.

## Table of Contents

- [Overview](#overview)
- [Core Architecture](#core-architecture)
- [Analyzer Pattern](#analyzer-pattern)
- [Code Fix Pattern](#code-fix-pattern)
- [Helper Infrastructure](#helper-infrastructure)
- [Test Infrastructure](#test-infrastructure)
- [Build & Packaging](#build--packaging)
- [Performance Considerations](#performance-considerations)
- [Adding New Analyzers](#adding-new-analyzers)

---

## Overview

The AutoMapper Analyzer is a Roslyn-based static code analysis tool that detects AutoMapper configuration issues at compile-time. It follows Microsoft's Roslyn analyzer best practices and integrates seamlessly with the .NET build pipeline.

### Technology Stack

- **Target Framework**: .NET Standard 2.0 (maximum compatibility)
- **Roslyn Version**: Microsoft.CodeAnalysis 4.14.0
- **Test Framework**: XUnit with Microsoft.CodeAnalysis.Testing
- **Supported IDEs**: Visual Studio, VS Code (OmniSharp), JetBrains Rider
- **Packaging**: NuGet package with analyzer assets

### Project Structure

```
src/AutoMapperAnalyzer.Analyzers/
├── AM###_*Analyzer.cs           # Diagnostic analyzers
├── AM###_*CodeFixProvider.cs    # Code fix providers
├── Helpers/
│   ├── AutoMapperAnalysisHelpers.cs  # Core analysis utilities
│   ├── CreateMapRegistry.cs          # Cross-profile mapping tracking
│   ├── TypeConversionHelper.cs       # Type compatibility checks
│   └── CodeFixSyntaxHelper.cs        # Code fix syntax generation
└── AutoMapperAnalyzer.Analyzers.csproj
```

---

## Core Architecture

### Roslyn Analyzer Fundamentals

Roslyn analyzers operate on the **syntax tree** and **semantic model** of C# code:

1. **Syntax Tree**: Represents the structure of source code (nodes, tokens, trivia)
2. **Semantic Model**: Provides type information, symbol resolution, and semantic analysis
3. **Compilation**: Represents the entire compilation unit with all syntax trees

### Analysis Pipeline

```
Source Code
    ↓
Syntax Tree (parsed by Roslyn)
    ↓
Semantic Model (type resolution)
    ↓
Analyzer Execution (our code)
    ↓
Diagnostic Results (warnings/errors)
    ↓
Code Fix Providers (optional fixes)
```

### Semantic Gating and Ownership

Recent analyzer passes enforce two architectural rules across mapping analyzers:

1. **Strict semantic AutoMapper gating**
   - CreateMap/ForMember/ConvertUsing checks are symbol-based (`MappingChainAnalysisHelper.IsAutoMapperMethodInvocation`).
   - Name/text heuristics (`mapper`, `cfg`, `config`) are intentionally not used.
   - This reduces false positives from lookalike APIs.

2. **Single-owner diagnostics for overlap-prone patterns**
   - `AM003` owns collection container mismatches.
   - `AM021` owns collection element mismatches (when containers are otherwise compatible).
   - `AM030` owns converter-quality diagnostics only (invalid implementation, null handling, unused converter).
   - Property-level conversion absence is intentionally handled by `AM001`/`AM020`/`AM021`, not `AM030`.

### Diagnostic Lifecycle

1. **Registration**: Analyzers register callbacks for specific syntax kinds
2. **Analysis**: Callback executes when matching syntax is found
3. **Reporting**: Diagnostics are reported with location and metadata
4. **Code Fix**: User invokes code fix through IDE
5. **Application**: Code fix modifies syntax tree and returns new document

---

## Analyzer Pattern

All analyzers in this project follow a consistent pattern. Here's the anatomy of an analyzer:

### Standard Analyzer Structure

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM001_PropertyTypeMismatchAnalyzer : DiagnosticAnalyzer
{
    // 1. Diagnostic Descriptors
    public static readonly DiagnosticDescriptor PropertyTypeMismatchRule = new(
        "AM001",                                      // Unique ID
        "Property type mismatch",                     // Title
        "Property '{0}' has incompatible types...",   // Message format
        "AutoMapper.TypeSafety",                      // Category
        DiagnosticSeverity.Error,                     // Severity
        true,                                         // Enabled by default
        "Description of the issue..."                 // Description
    );

    // 2. Supported Diagnostics
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [PropertyTypeMismatchRule];

    // 3. Initialize (register callbacks)
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCreateMapInvocation,
            SyntaxKind.InvocationExpression);
    }

    // 4. Analysis Method (the core logic)
    private static void AnalyzeCreateMapInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Check if this is a CreateMap call
        if (!AutoMapperAnalysisHelpers.IsCreateMapInvocation(invocation,
            context.SemanticModel))
            return;

        // Get type arguments
        var (sourceType, destType) = AutoMapperAnalysisHelpers
            .GetCreateMapTypeArguments(invocation, context.SemanticModel);

        // Perform analysis...
        AnalyzePropertyMappings(context, invocation, sourceType, destType);
    }
}
```

### Key Components Explained

#### 1. Diagnostic Descriptors

Diagnostic descriptors define **what** the analyzer reports:

- **ID**: Must be unique (AM001, AM002, etc.)
- **Title**: Short description shown in IDE
- **Message**: Template string with placeholders (`{0}`, `{1}`, etc.)
- **Category**: Groups related diagnostics (e.g., `AutoMapper.TypeSafety`)
- **Severity**: `Error`, `Warning`, `Info`, or `Hidden`
- **IsEnabledByDefault**: Controls default activation
- **Description**: Detailed explanation for documentation

#### 2. Initialize Method

The `Initialize` method configures the analyzer:

```csharp
public override void Initialize(AnalysisContext context)
{
    // Don't analyze generated code
    context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

    // Enable parallel execution for performance
    context.EnableConcurrentExecution();

    // Register callbacks for specific syntax kinds
    context.RegisterSyntaxNodeAction(
        AnalyzeCreateMapInvocation,      // Callback method
        SyntaxKind.InvocationExpression  // Syntax kind to match
    );
}
```

**Important**: Always enable concurrent execution unless your analyzer maintains mutable state.

#### 3. Analysis Callback

The callback receives a `SyntaxNodeAnalysisContext` with:

- **Node**: The syntax node being analyzed
- **SemanticModel**: For type resolution and symbol lookup
- **Compilation**: The entire compilation context
- **ReportDiagnostic**: Method to report diagnostics

### Common Analysis Patterns

#### Pattern 1: Finding CreateMap Invocations

```csharp
private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
{
    var invocation = (InvocationExpressionSyntax)context.Node;

    // Use helper to identify CreateMap calls
    if (!AutoMapperAnalysisHelpers.IsCreateMapInvocation(
        invocation, context.SemanticModel))
        return;

    // Get type arguments (Source, Destination)
    var (sourceType, destType) = AutoMapperAnalysisHelpers
        .GetCreateMapTypeArguments(invocation, context.SemanticModel);
}
```

#### Pattern 2: Property Mapping Analysis

```csharp
private static void AnalyzePropertyMappings(
    SyntaxNodeAnalysisContext context,
    ITypeSymbol sourceType,
    ITypeSymbol destinationType)
{
    // Get mappable properties (readable source, writable destination)
    var sourceProps = AutoMapperAnalysisHelpers
        .GetMappableProperties(sourceType, requireSetter: false);
    var destProps = AutoMapperAnalysisHelpers
        .GetMappableProperties(destinationType, requireGetter: false);

    foreach (var sourceProp in sourceProps)
    {
        // Find matching destination property
        var destProp = destProps.FirstOrDefault(p =>
            p.Name == sourceProp.Name);

        if (destProp == null) continue;

        // Check if ForMember is configured
        if (AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember(
            invocation, sourceProp.Name, context.SemanticModel))
            continue;

        // Analyze compatibility...
        AnalyzePropertyCompatibility(context, sourceProp, destProp);
    }
}
```

#### Pattern 3: Type Compatibility Checking

```csharp
private static void AnalyzePropertyCompatibility(
    SyntaxNodeAnalysisContext context,
    IPropertySymbol sourceProperty,
    IPropertySymbol destProperty)
{
    // Exact match - no diagnostic needed
    if (SymbolEqualityComparer.Default.Equals(
        sourceProperty.Type, destProperty.Type))
        return;

    // Use helper for comprehensive compatibility check
    if (AutoMapperAnalysisHelpers.AreTypesCompatible(
        sourceProperty.Type, destProperty.Type))
        return;

    // Report diagnostic with property metadata
    var diagnostic = Diagnostic.Create(
        PropertyTypeMismatchRule,
        invocation.GetLocation(),
        sourceProperty.Name,                          // {0}
        sourceProperty.Type.ToDisplayString(),        // {1}
        destProperty.Type.ToDisplayString()           // {2}
    );

    context.ReportDiagnostic(diagnostic);
}
```

---

## Code Fix Pattern

Code fix providers offer automated solutions to diagnostics. They follow a standardized pattern:

### Standard Code Fix Structure

```csharp
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM001_PropertyTypeMismatchCodeFixProvider))]
[Shared]
public class AM001_PropertyTypeMismatchCodeFixProvider : CodeFixProvider
{
    // 1. Fixable Diagnostics
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        [AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule.Id];

    // 2. Fix All Provider (optional batch fixes)
    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    // 3. Register Code Fix
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(
            context.CancellationToken);
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the CreateMap invocation
        var invocation = root?.FindToken(diagnosticSpan.Start)
            .Parent?.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault();

        if (invocation == null) return;

        // Register fix action
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add ForMember configuration",
                createChangedDocument: c => AddForMemberAsync(
                    context.Document, invocation, diagnostic, c),
                equivalenceKey: "AddForMember"
            ),
            diagnostic
        );
    }

    // 4. Apply Code Fix
    private async Task<Document> AddForMemberAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

        // Build ForMember syntax
        var forMemberCall = CodeFixSyntaxHelper.CreateForMemberCall(
            propertyName,
            mappingExpression
        );

        // Add to method chain
        var newInvocation = invocation.WithExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                invocation,
                SyntaxFactory.IdentifierName("ForMember")
            )
        ).WithArgumentList(forMemberCall);

        // Replace syntax and return new document
        var newRoot = root.ReplaceNode(invocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }
}
```

### Key Code Fix Components

#### 1. FixableDiagnosticIds

Specifies which diagnostic IDs this provider can fix:

```csharp
public sealed override ImmutableArray<string> FixableDiagnosticIds =>
    [
        AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule.Id,
        AM001_PropertyTypeMismatchAnalyzer.NullableCompatibilityRule.Id
    ];
```

#### 2. FixAllProvider

Enables batch fixing across multiple files:

```csharp
public sealed override FixAllProvider GetFixAllProvider() =>
    WellKnownFixAllProviders.BatchFixer;  // Apply all fixes at once
```

#### 3. RegisterCodeFixesAsync

Registers one or more code actions:

```csharp
context.RegisterCodeFix(
    CodeAction.Create(
        title: "User-visible fix description",
        createChangedDocument: c => ApplyFixAsync(document, invocation, c),
        equivalenceKey: "UniqueFixIdentifier"
    ),
    diagnostic
);
```

**Multiple Fix Actions**: You can register multiple fixes for a single diagnostic:

```csharp
// Option 1: Add ForMember
context.RegisterCodeFix(
    CodeAction.Create(
        title: "Add ForMember with conversion",
        createChangedDocument: c => AddForMemberAsync(...),
        equivalenceKey: "AddForMember"
    ),
    diagnostic
);

// Option 2: Ignore property
context.RegisterCodeFix(
    CodeAction.Create(
        title: "Ignore this property",
        createChangedDocument: c => AddIgnoreAsync(...),
        equivalenceKey: "IgnoreProperty"
    ),
    diagnostic
);
```

### Code Fix Best Practices

1. **Preserve Formatting**: Use `.WithTrailingTrivia()` and `.WithLeadingTrivia()` to maintain code style
2. **Handle Edge Cases**: Check for null references, existing configurations, etc.
3. **Use Helpers**: Leverage `CodeFixSyntaxHelper` for common syntax patterns
4. **Test Thoroughly**: Every code fix must have comprehensive tests
5. **Equivalence Keys**: Use unique keys for different fix strategies

---

## Helper Infrastructure

The analyzer uses several helper classes to share common functionality:

### AutoMapperAnalysisHelpers

Located in `Helpers/AutoMapperAnalysisHelpers.cs`, this is the **core utility class**:

#### Key Methods

```csharp
public static class AutoMapperAnalysisHelpers
{
    // Identify CreateMap invocations
    public static bool IsCreateMapInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel);

    // Extract type arguments from CreateMap<TSource, TDestination>()
    public static (ITypeSymbol? sourceType, ITypeSymbol? destType)
        GetCreateMapTypeArguments(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel);

    // Get properties suitable for mapping
    public static IEnumerable<IPropertySymbol> GetMappableProperties(
        ITypeSymbol type,
        bool requireGetter = true,
        bool requireSetter = true);

    // Check if property is configured with ForMember
    public static bool IsPropertyConfiguredWithForMember(
        InvocationExpressionSyntax invocation,
        string propertyName,
        SemanticModel semanticModel);

    // Type compatibility checking
    public static bool AreTypesCompatible(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType);

    // Collection type detection
    public static bool IsCollectionType(ITypeSymbol type);

    // Get collection element type
    public static ITypeSymbol? GetCollectionElementType(ITypeSymbol type);

    // Check for existing CreateMap configuration
    public static bool HasExistingCreateMapForTypes(
        Compilation compilation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType);
}
```

### CreateMapRegistry

Tracks `CreateMap` calls across the entire compilation for cross-profile detection:

```csharp
public class CreateMapRegistry
{
    // Populate registry from compilation
    public static void PopulateFromCompilation(
        Compilation compilation,
        Dictionary<(string, string), Location> registry);

    // Check if mapping exists
    public static bool HasMapping(
        Dictionary<(string, string), Location> registry,
        ITypeSymbol sourceType,
        ITypeSymbol destType);
}
```

**Usage Pattern**:
1. Build registry once per compilation
2. Query registry in analyzers to detect cross-profile mappings
3. Used by AM020 (nested object mapping)

### TypeConversionHelper

Provides type conversion and compatibility logic:

```csharp
public static class TypeConversionHelper
{
    // Generate conversion expression for type mismatches
    public static string GenerateConversionExpression(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        string sourceExpression);

    // Check if types can be implicitly converted
    public static bool HasImplicitConversion(
        ITypeSymbol from,
        ITypeSymbol to);

    // Suggest appropriate conversion strategy
    public static ConversionStrategy GetConversionStrategy(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType);
}
```

### CodeFixSyntaxHelper

Builds common Roslyn syntax patterns for code fixes:

```csharp
public static class CodeFixSyntaxHelper
{
    // Create ForMember syntax
    public static InvocationExpressionSyntax CreateForMemberCall(
        string propertyName,
        ExpressionSyntax mappingExpression);

    // Create Ignore syntax
    public static InvocationExpressionSyntax CreateIgnoreCall(
        string propertyName);

    // Create MapFrom lambda
    public static ExpressionSyntax CreateMapFromLambda(
        string sourceParameter,
        string conversionExpression);
}
```

---

## Test Infrastructure

The project uses **Microsoft.CodeAnalysis.Testing** framework with XUnit.

### Test Pattern

```csharp
public class AM001_PropertyTypeMismatchTests
{
    [Fact]
    public async Task ShouldReportDiagnostic_WhenTypeMismatch()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source { public string Age { get; set; } }
                public class Dest { public int Age { get; set; } }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Dest>();
                    }
                }
            }
            """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM001_PropertyTypeMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule,
                line: 11,
                column: 13,
                "Age",
                "string",
                "int"
            )
            .RunAsync();
    }
}
```

### Code Fix Test Pattern

```csharp
public class AM001_CodeFixTests
{
    [Fact]
    public async Task ShouldFixTypeMismatch_WithConversion()
    {
        const string testCode = """
            // ... code with issue
            """;

        const string expectedFixedCode = """
            // ... code after fix
            """;

        await CodeFixVerifier<
                AM001_PropertyTypeMismatchAnalyzer,
                AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule)
                    .WithLocation(11, 13),
                expectedFixedCode
            );
    }
}
```

### Test Organization

```
tests/AutoMapperAnalyzer.Tests/
├── AM###_AnalyzerTests.cs      # Analyzer diagnostic tests
├── AM###_CodeFixTests.cs       # Code fix tests
├── Infrastructure/
│   ├── AnalyzerVerifier.cs     # Custom analyzer test helpers
│   └── CodeFixVerifier.cs      # Custom code fix test helpers
└── Framework/
    └── DiagnosticTestFramework.cs  # Fluent test API
```

---

## Build & Packaging

### NuGet Package Structure

The analyzer is distributed as a NuGet package with special structure:

```
AutoMapperAnalyzer.Analyzers.nupkg
├── analyzers/
│   └── dotnet/
│       └── cs/
│           └── AutoMapperAnalyzer.Analyzers.dll
├── README.md
├── icon.png
└── [package metadata]
```

**Key Configuration** (`.csproj`):

```xml
<PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
    <DevelopmentDependency>true</DevelopmentDependency>
    <PrivateAssets>all</PrivateAssets>
</PropertyGroup>

<ItemGroup>
    <None Include="bin\$(Configuration)\$(TargetFramework)\$(AssemblyName).dll"
          Pack="true"
          PackagePath="analyzers\dotnet\cs\$(AssemblyName).dll"
          Visible="false" />
</ItemGroup>
```

### Versioning Strategy

**Semantic Versioning**: `Major.Minor.BuildNumber`

- **Major**: Breaking changes
- **Minor**: New analyzers or features
- **BuildNumber**: Auto-generated from date (`YYYYMMDD`)

### Build Commands

```bash
# Clean build
dotnet clean
dotnet build

# Run tests
dotnet test

# Create NuGet package
dotnet pack --configuration Release

# Test package locally
cd test-install/NetCoreTest
dotnet add package AutoMapperAnalyzer.Analyzers --version 2.4.1-local
```

---

## Performance Considerations

### Analyzer Performance

Roslyn analyzers run **during compilation**, so performance is critical:

#### Best Practices

1. **Enable Concurrent Execution**
   ```csharp
   context.EnableConcurrentExecution();
   ```

2. **Avoid Expensive Operations**
   - Cache compilation-wide data
   - Use symbol comparisons, not string comparisons
   - Minimize syntax tree traversals

3. **Register Specific Syntax Kinds**
   ```csharp
   // Good: Specific
   context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);

   // Bad: Too broad
   context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CompilationUnit);
   ```

4. **Early Exit Conditions**
   ```csharp
   private static void Analyze(SyntaxNodeAnalysisContext context)
   {
       // Quick checks first
       if (!IsCreateMapInvocation(context.Node, context.SemanticModel))
           return;

       // Expensive analysis only if needed
       PerformDeepAnalysis(context);
   }
   ```

5. **Use Helpers for Shared Logic**
   - Reduces code duplication
   - Centralizes performance-critical paths
   - Easier to optimize once

### Memory Management

- **Avoid Mutable State**: Use immutable data structures
- **Dispose Resources**: Use `CancellationToken` for long operations
- **Limit Allocations**: Reuse collections where possible

### Profiling Analyzers

Use the Roslyn Analyzer Performance Profiler:

```bash
# Enable analyzer profiling
dotnet build /p:ReportAnalyzer=true

# View performance report
cat obj/Debug/AnalyzerPerformance.xml
```

---

## Adding New Analyzers

Follow this checklist when adding a new analyzer:

### Step 1: Create Analyzer Class

```csharp
// src/AutoMapperAnalyzer.Analyzers/AM###_[Name]Analyzer.cs
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM###_[Name]Analyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        "AM###",
        "Title",
        "Message format",
        "Category",
        DiagnosticSeverity.Warning,
        true,
        "Description"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        // Implementation
    }
}
```

### Step 2: Create Code Fix Provider

```csharp
// src/AutoMapperAnalyzer.Analyzers/AM###_[Name]CodeFixProvider.cs
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public class AM###_[Name]CodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        [AM###_[Name]Analyzer.Rule.Id];

    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        // Implementation
    }
}
```

### Step 3: Write Tests

Create both analyzer and code fix tests:

```csharp
// tests/AutoMapperAnalyzer.Tests/AM###_AnalyzerTests.cs
// tests/AutoMapperAnalyzer.Tests/AM###_CodeFixTests.cs
```

### Step 4: Update Documentation

- Add to `README.md` analyzer table
- Create entry in `DIAGNOSTIC_RULES.md`
- Update release notes

### Step 5: Test Across Platforms

```bash
# Run full test suite
dotnet test

# Test package installation
cd test-install/NetCoreTest
dotnet add package AutoMapperAnalyzer.Analyzers --version [version]
dotnet build
```

---

## Diagnostic ID Ranges

| Range | Category | Status |
|-------|----------|---------|
| AM001-AM009 | Core type safety | ✅ Implemented |
| AM010-AM019 | Property mapping | ✅ Implemented |
| AM020-AM029 | Complex types & collections | ✅ Implemented |
| AM030-AM039 | Custom conversions | ✅ Implemented |
| AM040-AM049 | Configuration & profiles | 📋 Planned |
| AM050-AM059 | Performance & best practices | 📋 Planned |
| AM060-AM069 | Entity Framework integration | 🔮 Future |

---

## Additional Resources

- [Roslyn Analyzer Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix)
- [AutoMapper Documentation](https://docs.automapper.org/)
- [Project README](../README.md)
- [Diagnostic Rules Reference](./DIAGNOSTIC_RULES.md)
- [Contributing Guide](../CONTRIBUTING.md)

---

**Last Updated**: 2025-11-19
**Maintainer**: George Wall
**Version**: 2.4.1

using AutoMapperAnalyzer.Tests.Framework;
using AutoMapperAnalyzer.Tests.Helpers;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace AutoMapperAnalyzer.Tests.Framework;

/// <summary>
/// Tests for the diagnostic test framework
/// </summary>
public class DiagnosticTestFrameworkTests
{
    [Fact]
    public void DiagnosticTestFramework_CanCreateAnalyzerTestRunner()
    {
        // Act
        var runner = DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>();

        // Assert
        Assert.NotNull(runner);
        Assert.IsType<AnalyzerTestRunner<TestAnalyzer>>(runner);
    }

    [Fact]
    public void DiagnosticTestFramework_CanCreateMultiAnalyzerTestRunner()
    {
        // Arrange
        var analyzer1 = new TestAnalyzer();
        var analyzer2 = new TestAnalyzer();

        // Act
        var runner = DiagnosticTestFramework.ForAnalyzers(analyzer1, analyzer2);

        // Assert
        Assert.NotNull(runner);
        Assert.IsType<MultiAnalyzerTestRunner>(runner);
    }

    [Fact]
    public void AnalyzerTestRunner_CanChainFluentCalls()
    {
        // Act
        var runner = DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>()
            .WithSource("public class Test { }")
            .ExpectNoDiagnostics();

        // Assert
        Assert.NotNull(runner);
    }

    [Fact]
    public void AnalyzerTestRunner_CanAddDiagnosticExpectations()
    {
        // Act
        var runner = DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>()
            .WithSource("public class Test { }")
            .ExpectDiagnostic(AutoMapperDiagnostics.PropertyTypeMismatch, 1, 1, "Test", "string", "int");

        // Assert
        Assert.NotNull(runner);
    }

    [Fact]
    public async Task AnalyzerTestRunner_ThrowsWhenNoSourceProvided()
    {
        // Arrange
        var runner = DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync());
    }

    [Fact]
    public async Task AnalyzerTestRunner_CanRunWithValidSource()
    {
        // Arrange
        var source = @"
using System;

public class TestClass
{
    public string Name { get; set; }
}";

        // Act & Assert (should not throw)
        await DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>()
            .WithSource(source)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AnalyzerTestRunner_CanUseScenarioBuilder()
    {
        // Arrange
        var scenario = new TestScenarioBuilder()
            .AddClass("Source", ("string", "Name"))
            .AddClass("Destination", ("string", "Name"));

        // Act & Assert (should not throw)
        await DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>()
            .WithScenario(scenario)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public void DiagnosticTestExtensions_CanCreateTypeMismatchScenario()
    {
        // Act
        var runner = DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>()
            .WithTypeMismatchScenario("string", "int", "Age");

        // Assert
        Assert.NotNull(runner);
    }

    [Fact]
    public void DiagnosticTestExtensions_CanCreateNullableScenario()
    {
        // Act
        var runner = DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>()
            .WithNullableScenario("Name");

        // Assert
        Assert.NotNull(runner);
    }

    [Fact]
    public void DiagnosticTestExtensions_CanCreateMissingPropertyScenario()
    {
        // Act
        var runner = DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>()
            .WithMissingPropertyScenario("ImportantData");

        // Assert
        Assert.NotNull(runner);
    }

    [Fact]
    public async Task DiagnosticTestExtensions_TypeMismatchScenario_GeneratesValidCode()
    {
        // Act & Assert (should not throw - validates the generated code compiles)
        await DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>()
            .WithTypeMismatchScenario("string", "int", "Age")
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task DiagnosticTestExtensions_NullableScenario_GeneratesValidCode()
    {
        // Act & Assert (should not throw - validates the generated code compiles)
        await DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>()
            .WithNullableScenario("Name")
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task DiagnosticTestExtensions_MissingPropertyScenario_GeneratesValidCode()
    {
        // Act & Assert (should not throw - validates the generated code compiles)
        await DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>()
            .WithMissingPropertyScenario("ImportantData")
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task MultiAnalyzerTestRunner_ThrowsNotImplemented()
    {
        // Arrange
        var runner = DiagnosticTestFramework.ForAnalyzers(new TestAnalyzer())
            .WithSource("public class Test { }");

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => runner.RunAsync());
    }
}

/// <summary>
/// Test analyzer that doesn't report any diagnostics (for framework testing)
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TestAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
        ImmutableArray<DiagnosticDescriptor>.Empty;

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        // No analysis - this is just for framework testing
    }
} 
using System.Collections.Immutable;
using AutoMapperAnalyzer.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Tests.Framework;

/// <summary>
///     Tests for the diagnostic test framework
/// </summary>
public class DiagnosticTestFrameworkTests
{
    [Fact]
    public void DiagnosticTestFramework_CanCreateAnalyzerTestRunner()
    {
        // Act
        AnalyzerTestRunner<TestAnalyzer> runner = DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>();

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
        MultiAnalyzerTestRunner runner = DiagnosticTestFramework.ForAnalyzers(analyzer1, analyzer2);

        // Assert
        Assert.NotNull(runner);
        Assert.IsType<MultiAnalyzerTestRunner>(runner);
    }

    [Fact]
    public void AnalyzerTestRunner_CanChainFluentCalls()
    {
        // Act
        AnalyzerTestRunner<TestAnalyzer> runner = DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>()
            .WithSource("public class Test { }")
            .ExpectNoDiagnostics();

        // Assert
        Assert.NotNull(runner);
    }

    [Fact]
    public void AnalyzerTestRunner_CanAddDiagnosticExpectations()
    {
        // Act
        AnalyzerTestRunner<TestAnalyzer> runner = DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>()
            .WithSource("public class Test { }")
            .ExpectDiagnostic(AutoMapperDiagnostics.PropertyTypeMismatch, 1, 1, "Test", "string", "int");

        // Assert
        Assert.NotNull(runner);
    }

    [Fact]
    public async Task AnalyzerTestRunner_ThrowsWhenNoSourceProvided()
    {
        // Arrange
        AnalyzerTestRunner<TestAnalyzer> runner = DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync());
    }

    [Fact]
    public async Task AnalyzerTestRunner_CanRunWithValidSource()
    {
        // Arrange
        string source = """

                        using System;

                        public class TestClass
                        {
                            public string Name { get; set; }
                        }
                        """;

        // Act & Assert (should not throw)
        await DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>()
            .WithSource(source)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AnalyzerTestRunner_CanUseScenarioBuilder()
    {
        // Arrange
        TestScenarioBuilder scenario = new TestScenarioBuilder()
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
        AnalyzerTestRunner<TestAnalyzer> runner = DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>()
            .WithTypeMismatchScenario("string", "int", "Age");

        // Assert
        Assert.NotNull(runner);
    }

    [Fact]
    public void DiagnosticTestExtensions_CanCreateNullableScenario()
    {
        // Act
        AnalyzerTestRunner<TestAnalyzer> runner = DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>()
            .WithNullableScenario();

        // Assert
        Assert.NotNull(runner);
    }

    [Fact]
    public void DiagnosticTestExtensions_CanCreateMissingPropertyScenario()
    {
        // Act
        AnalyzerTestRunner<TestAnalyzer> runner = DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>()
            .WithMissingPropertyScenario();

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
            .WithNullableScenario()
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task DiagnosticTestExtensions_MissingPropertyScenario_GeneratesValidCode()
    {
        // Act & Assert (should not throw - validates the generated code compiles)
        await DiagnosticTestFramework.ForAnalyzer<TestAnalyzer>()
            .WithMissingPropertyScenario()
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task MultiAnalyzerTestRunner_CanRun()
    {
        // Arrange
        MultiAnalyzerTestRunner runner = DiagnosticTestFramework.ForAnalyzers(new TestAnalyzer())
            .WithSource("public class Test { }");

        // Act & Assert (should not throw)
        await runner.RunAsync();
    }
}

/// <summary>
///     Test analyzer that doesn't report any diagnostics (for framework testing)
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

using System.Collections.Immutable;
using AutoMapperAnalyzer.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.Infrastructure;

/// <summary>
///     Tests to validate our test infrastructure
/// </summary>
public class TestInfrastructureTests
{
    [Fact]
    public void TestScenarioBuilder_CanBuildBasicClass()
    {
        // Arrange & Act
        string source = new TestScenarioBuilder()
            .AddClass("TestClass", ("string", "Name"), ("int", "Age"))
            .Build();

        // Assert
        Assert.Contains("public class TestClass", source);
        Assert.Contains("public string Name { get; set; }", source);
        Assert.Contains("public int Age { get; set; }", source);
    }

    [Fact]
    public void TestScenarioBuilder_CanBuildAutMapperProfile()
    {
        // Arrange & Act
        string source = new TestScenarioBuilder()
            .AddClass("Source", ("string", "Name"))
            .AddClass("Destination", ("string", "Name"))
            .AddMapping("Source", "Destination")
            .Build();

        // Assert
        Assert.Contains("CreateMap<Source, Destination>();", source);
        Assert.Contains("public class TestProfile_SourceToDestination : Profile", source);
    }

    [Fact]
    public void TestScenarioBuilder_TypeMismatchScenario_BuildsCorrectly()
    {
        // Arrange & Act
        string source = TestScenarioBuilder.CreateTypeMismatchScenario().Build();

        // Assert
        Assert.Contains("public class Source", source);
        Assert.Contains("public string Age { get; set; }", source);
        Assert.Contains("public class Destination", source);
        Assert.Contains("public int Age { get; set; }", source);
        Assert.Contains("CreateMap<Source, Destination>();", source);
    }

    [Fact]
    public void TestScenarioBuilder_NullableScenario_BuildsCorrectly()
    {
        // Arrange & Act
        string source = TestScenarioBuilder.CreateNullableScenario().Build();

        // Assert
        Assert.Contains("public string? Name { get; set; }", source);
        Assert.Contains("public string Name { get; set; }", source);
    }

    [Fact]
    public void TestScenarioBuilder_MissingPropertyScenario_BuildsCorrectly()
    {
        // Arrange & Act
        string source = TestScenarioBuilder.CreateMissingPropertyScenario().Build();

        // Assert
        Assert.Contains("public string ImportantData { get; set; }", source);
        // Destination should not have ImportantData
        string[] lines = source.Split('\n');
        string[] destinationLines = lines
            .SkipWhile(l => !l.Contains("public class Destination"))
            .TakeWhile(l => !l.Contains("public class") || l.Contains("public class Destination"))
            .ToArray();

        Assert.DoesNotContain(destinationLines, l => l.Contains("ImportantData"));
    }

    [Fact]
    public void DiagnosticAssertions_CanCreateDiagnostic()
    {
        // Arrange & Act
        DiagnosticResult diagnostic = DiagnosticAssertions
            .Diagnostic(AutoMapperDiagnostics.PropertyTypeMismatch)
            .AtLocation(5, 10)
            .WithArguments("Age", "string", "int")
            .Build();

        // Assert
        // The DiagnosticResult is mainly used for test comparisons
        // We can verify our descriptor directly
        Assert.Equal("AM001", AutoMapperDiagnostics.PropertyTypeMismatch.Id);
        Assert.Equal(DiagnosticSeverity.Error, AutoMapperDiagnostics.PropertyTypeMismatch.DefaultSeverity);
    }

    [Fact]
    public void AutoMapperDiagnostics_HaveCorrectProperties()
    {
        // Test AM001
        Assert.Equal("AM001", AutoMapperDiagnostics.PropertyTypeMismatch.Id);
        Assert.Equal(DiagnosticSeverity.Error, AutoMapperDiagnostics.PropertyTypeMismatch.DefaultSeverity);
        Assert.Equal("AutoMapper.TypeSafety", AutoMapperDiagnostics.PropertyTypeMismatch.Category);

        // Test AM002
        Assert.Equal("AM002", AutoMapperDiagnostics.NullableToNonNullable.Id);
        Assert.Equal(DiagnosticSeverity.Warning, AutoMapperDiagnostics.NullableToNonNullable.DefaultSeverity);

        // Test AM010
        Assert.Equal("AM010", AutoMapperDiagnostics.MissingDestinationProperty.Id);
        Assert.Equal(DiagnosticSeverity.Warning, AutoMapperDiagnostics.MissingDestinationProperty.DefaultSeverity);

        // Test AM011
        Assert.Equal("AM011", AutoMapperDiagnostics.UnmappedRequiredProperty.Id);
        Assert.Equal(DiagnosticSeverity.Error, AutoMapperDiagnostics.UnmappedRequiredProperty.DefaultSeverity);
    }
}

/// <summary>
///     Dummy analyzer for testing the test infrastructure
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DummyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [AutoMapperDiagnostics.PropertyTypeMismatch];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        // No actual analysis for this dummy analyzer
    }
}

/// <summary>
///     Test that our base analyzer test class works
/// </summary>
public class DummyAnalyzerTests : AnalyzerTestBase<DummyAnalyzer>
{
    [Fact]
    public async Task DummyAnalyzer_DoesNotReportDiagnosticsForValidCode()
    {
        string source = """

                        using System;

                        public class TestClass
                        {
                            public string Name { get; set; }
                        }
                        """;

        await RunAnalyzerTestAsync(source);
    }
}

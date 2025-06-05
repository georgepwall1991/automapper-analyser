using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using System.Collections.Immutable;
using AutoMapperAnalyzer.Tests.Helpers;

namespace AutoMapperAnalyzer.Tests.Framework;

/// <summary>
/// Comprehensive framework for testing diagnostic analyzers with AutoMapper scenarios
/// </summary>
public static class DiagnosticTestFramework
{
    /// <summary>
    /// Creates a test runner for a specific analyzer
    /// </summary>
    public static AnalyzerTestRunner<TAnalyzer> ForAnalyzer<TAnalyzer>()
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        return new AnalyzerTestRunner<TAnalyzer>();
    }

    /// <summary>
    /// Creates a test runner with multiple analyzers
    /// </summary>
    public static MultiAnalyzerTestRunner ForAnalyzers(params DiagnosticAnalyzer[] analyzers)
    {
        return new MultiAnalyzerTestRunner(analyzers);
    }
}

/// <summary>
/// Test runner for a single analyzer type
/// </summary>
/// <typeparam name="TAnalyzer">The analyzer type to test</typeparam>
public class AnalyzerTestRunner<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    private readonly List<(string FileName, string Source)> _sources = new();
    private readonly List<DiagnosticResult> _expectedDiagnostics = new();
    private readonly List<MetadataReference> _additionalReferences = new();
    private ReferenceAssemblies _referenceAssemblies = ReferenceAssemblies.Net.Net80;

    /// <summary>
    /// Adds source code to test
    /// </summary>
    public AnalyzerTestRunner<TAnalyzer> WithSource(string source, string fileName = "Test.cs")
    {
        _sources.Add((fileName, source));
        return this;
    }

    /// <summary>
    /// Adds source code using the TestScenarioBuilder
    /// </summary>
    public AnalyzerTestRunner<TAnalyzer> WithScenario(TestScenarioBuilder scenario, string fileName = "Test.cs")
    {
        return WithSource(scenario.Build(), fileName);
    }

    /// <summary>
    /// Expects a specific diagnostic
    /// </summary>
    public AnalyzerTestRunner<TAnalyzer> ExpectDiagnostic(DiagnosticDescriptor descriptor, int line, int column, params object[] messageArgs)
    {
        var diagnostic = new DiagnosticResult(descriptor)
            .WithLocation(line, column)
            .WithArguments(messageArgs);
        _expectedDiagnostics.Add(diagnostic);
        return this;
    }

    /// <summary>
    /// Expects a diagnostic with span information
    /// </summary>
    public AnalyzerTestRunner<TAnalyzer> ExpectDiagnostic(DiagnosticDescriptor descriptor, int startLine, int startColumn, int endLine, int endColumn, params object[] messageArgs)
    {
        var diagnostic = new DiagnosticResult(descriptor)
            .WithSpan(startLine, startColumn, endLine, endColumn)
            .WithArguments(messageArgs);
        _expectedDiagnostics.Add(diagnostic);
        return this;
    }

    /// <summary>
    /// Expects no diagnostics to be reported
    /// </summary>
    public AnalyzerTestRunner<TAnalyzer> ExpectNoDiagnostics()
    {
        _expectedDiagnostics.Clear();
        return this;
    }

    /// <summary>
    /// Adds additional assembly references
    /// </summary>
    public AnalyzerTestRunner<TAnalyzer> WithReferences(params MetadataReference[] references)
    {
        _additionalReferences.AddRange(references);
        return this;
    }

    /// <summary>
    /// Sets the reference assemblies to use
    /// </summary>
    public AnalyzerTestRunner<TAnalyzer> WithReferenceAssemblies(ReferenceAssemblies referenceAssemblies)
    {
        _referenceAssemblies = referenceAssemblies;
        return this;
    }

    /// <summary>
    /// Runs the test asynchronously
    /// </summary>
    public async Task RunAsync()
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>();

        // Configure test state
        test.TestState.ReferenceAssemblies = _referenceAssemblies;
        
        // Add AutoMapper references
        test.TestState.AdditionalReferences.Add(
            MetadataReference.CreateFromFile(typeof(AutoMapper.IMapper).Assembly.Location));
        test.TestState.AdditionalReferences.Add(
            MetadataReference.CreateFromFile(typeof(AutoMapper.Profile).Assembly.Location));

        // Add additional references
        foreach (var reference in _additionalReferences)
        {
            test.TestState.AdditionalReferences.Add(reference);
        }

        // Add sources
        if (_sources.Count == 0)
        {
            throw new InvalidOperationException("At least one source file must be provided");
        }

        var primarySource = _sources[0];
        test.TestCode = primarySource.Source;

        for (int i = 1; i < _sources.Count; i++)
        {
            test.TestState.Sources.Add((_sources[i].FileName, _sources[i].Source));
        }

        // Add expected diagnostics
        test.ExpectedDiagnostics.AddRange(_expectedDiagnostics);

        // Run the test
        await test.RunAsync();
    }

    /// <summary>
    /// Runs the test and verifies no diagnostics are reported
    /// </summary>
    public async Task RunWithNoDiagnosticsAsync()
    {
        ExpectNoDiagnostics();
        await RunAsync();
    }
}

/// <summary>
/// Test runner for multiple analyzers (simplified implementation)
/// </summary>
public class MultiAnalyzerTestRunner
{
    private readonly DiagnosticAnalyzer[] _analyzers;
    private readonly List<(string FileName, string Source)> _sources = new();
    private readonly List<DiagnosticResult> _expectedDiagnostics = new();

    internal MultiAnalyzerTestRunner(DiagnosticAnalyzer[] analyzers)
    {
        _analyzers = analyzers ?? throw new ArgumentNullException(nameof(analyzers));
    }

    /// <summary>
    /// Adds source code to test
    /// </summary>
    public MultiAnalyzerTestRunner WithSource(string source, string fileName = "Test.cs")
    {
        _sources.Add((fileName, source));
        return this;
    }

    /// <summary>
    /// Expects a specific diagnostic
    /// </summary>
    public MultiAnalyzerTestRunner ExpectDiagnostic(DiagnosticDescriptor descriptor, int line, int column, params object[] messageArgs)
    {
        var diagnostic = new DiagnosticResult(descriptor)
            .WithLocation(line, column)
            .WithArguments(messageArgs);
        _expectedDiagnostics.Add(diagnostic);
        return this;
    }

    /// <summary>
    /// Runs the test with all configured analyzers (placeholder implementation)
    /// </summary>
    public async Task RunAsync()
    {
        // For now, we'll focus on single analyzer testing
        // Multi-analyzer testing can be implemented later when needed
        throw new NotImplementedException("Multi-analyzer testing will be implemented in a future iteration");
    }
}

/// <summary>
/// Empty analyzer used as a placeholder for multi-analyzer tests
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal class EmptyDiagnosticAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
        ImmutableArray<DiagnosticDescriptor>.Empty;

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        // No analysis
    }
}

/// <summary>
/// Fluent extensions for common diagnostic testing patterns
/// </summary>
public static class DiagnosticTestExtensions
{
    /// <summary>
    /// Tests type mismatch scenarios
    /// </summary>
    public static AnalyzerTestRunner<TAnalyzer> WithTypeMismatchScenario<TAnalyzer>(
        this AnalyzerTestRunner<TAnalyzer> runner,
        string sourceType, string destType, string propertyName = "Value")
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var scenario = new TestScenarioBuilder()
            .AddClass("Source", (sourceType, propertyName))
            .AddClass("Destination", (destType, propertyName))
            .AddMapping("Source", "Destination");

        return runner.WithScenario(scenario);
    }

    /// <summary>
    /// Tests nullable scenarios
    /// </summary>
    public static AnalyzerTestRunner<TAnalyzer> WithNullableScenario<TAnalyzer>(
        this AnalyzerTestRunner<TAnalyzer> runner,
        string propertyName = "Name")
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var scenario = new TestScenarioBuilder()
            .AddClass("Source", ("string?", propertyName))
            .AddClass("Destination", ("string", propertyName))
            .AddMapping("Source", "Destination");

        return runner.WithScenario(scenario);
    }

    /// <summary>
    /// Tests missing property scenarios
    /// </summary>
    public static AnalyzerTestRunner<TAnalyzer> WithMissingPropertyScenario<TAnalyzer>(
        this AnalyzerTestRunner<TAnalyzer> runner,
        string missingProperty = "ImportantData")
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var scenario = new TestScenarioBuilder()
            .AddClass("Source", ("string", "Name"), ("string", missingProperty))
            .AddClass("Destination", ("string", "Name"))
            .AddMapping("Source", "Destination");

        return runner.WithScenario(scenario);
    }
} 
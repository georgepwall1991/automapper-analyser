using System.Threading.Tasks;
using AutoMapper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.Infrastructure;

internal static class CodeFixVerifier<TAnalyzer, TCodeFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    public static Task VerifyFixAsync(string source, DiagnosticResult expectedDiagnostic, string fixedSource, DiagnosticResult[]? remainingDiagnostics = null)
        => VerifyFixAsync(source, new[] { expectedDiagnostic }, fixedSource, remainingDiagnostics);

    public static async Task VerifyFixAsync(string source, DiagnosticResult[] expectedDiagnostics, string fixedSource, DiagnosticResult[]? remainingDiagnostics = null)
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        AddAutoMapperReferences(test.TestState);
        AddAutoMapperReferences(test.FixedState);

        var remainingCount = remainingDiagnostics?.Length ?? 0;
        var expectedCount = expectedDiagnostics?.Length ?? 1;
        var iterations = Math.Max(1, Math.Max(remainingCount + 1, expectedCount));
        test.NumberOfFixAllIterations = iterations;
        test.NumberOfIncrementalIterations = iterations;
        test.ExpectedDiagnostics.AddRange(expectedDiagnostics);

        if (remainingDiagnostics != null)
        {
            test.FixedState.ExpectedDiagnostics.AddRange(remainingDiagnostics);
        }

        await test.RunAsync();
    }

    public static async Task VerifyFixWithIterationsAsync(string source, DiagnosticResult[] expectedDiagnostics, string fixedSource, int iterations)
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfFixAllIterations = iterations,
            NumberOfIncrementalIterations = iterations
        };

        AddAutoMapperReferences(test.TestState);
        AddAutoMapperReferences(test.FixedState);
        test.ExpectedDiagnostics.AddRange(expectedDiagnostics);
        await test.RunAsync();
    }

    public static async Task VerifyFixAsync((string filename, string source)[] sources, DiagnosticResult expectedDiagnostic, string fixedSource, DiagnosticResult[]? remainingDiagnostics = null)
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        if (sources.Length > 0)
        {
            test.TestCode = sources[0].source;
            test.FixedCode = fixedSource;

            for (int i = 1; i < sources.Length; i++)
            {
                test.TestState.Sources.Add((sources[i].filename, sources[i].source));
                test.FixedState.Sources.Add((sources[i].filename, sources[i].source));
            }
        }

        AddAutoMapperReferences(test.TestState);
        AddAutoMapperReferences(test.FixedState);

        var iterations2 = Math.Max(1, (remainingDiagnostics?.Length ?? 0) + 1);
        test.NumberOfFixAllIterations = iterations2;
        test.NumberOfIncrementalIterations = iterations2;
        test.ExpectedDiagnostics.Add(expectedDiagnostic);
        if (remainingDiagnostics != null)
        {
            test.FixedState.ExpectedDiagnostics.AddRange(remainingDiagnostics);
        }

        await test.RunAsync();
    }

    public static async Task VerifyFixWithIterationsAsync((string filename, string source)[] sources, DiagnosticResult expectedDiagnostic, string fixedSource, int iterations)
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfFixAllIterations = iterations,
            NumberOfIncrementalIterations = iterations
        };

        if (sources.Length > 0)
        {
            test.TestCode = sources[0].source;
            test.FixedCode = fixedSource;
            for (int i = 1; i < sources.Length; i++)
            {
                test.TestState.Sources.Add((sources[i].filename, sources[i].source));
                test.FixedState.Sources.Add((sources[i].filename, sources[i].source));
            }
        }

        AddAutoMapperReferences(test.TestState);
        AddAutoMapperReferences(test.FixedState);
        test.ExpectedDiagnostics.Add(expectedDiagnostic);
        await test.RunAsync();
    }

    private static void AddAutoMapperReferences(SolutionState state)
    {
        state.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Profile).Assembly.Location));
    }
}

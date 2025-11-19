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
    public static Task VerifyFixAsync(string source, DiagnosticResult expectedDiagnostic, string fixedSource,
        DiagnosticResult[]? remainingDiagnostics = null)
    {
        return VerifyFixAsync(source, new[] { expectedDiagnostic }, fixedSource, null, remainingDiagnostics);
    }

    public static Task VerifyFixAsync(string source, DiagnosticResult expectedDiagnostic, string fixedSource,
        int? codeActionIndex, DiagnosticResult[]? remainingDiagnostics = null)
    {
        return VerifyFixAsync(source, new[] { expectedDiagnostic }, fixedSource, codeActionIndex, remainingDiagnostics);
    }

    public static async Task VerifyFixAsync(string source, DiagnosticResult[] expectedDiagnostics, string fixedSource,
        DiagnosticResult[]? remainingDiagnostics = null)
    {
        await VerifyFixAsync(source, expectedDiagnostics, fixedSource, null, remainingDiagnostics);
    }

    public static async Task VerifyFixAsync(string source, DiagnosticResult[] expectedDiagnostics, string fixedSource,
        int? codeActionIndex, DiagnosticResult[]? remainingDiagnostics = null)
    {
        await VerifyFixAsync(source, expectedDiagnostics, fixedSource, codeActionIndex, remainingDiagnostics, null);
    }
    
    public static async Task VerifyFixAsync(string source, DiagnosticResult[] expectedDiagnostics, string fixedSource,
        int? codeActionIndex, int? iterations, DiagnosticResult[]? remainingDiagnostics = null)
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CodeActionIndex = codeActionIndex
        };

        AddAutoMapperReferences(test.TestState);
        AddAutoMapperReferences(test.FixedState);

        int remainingCount = remainingDiagnostics?.Length ?? 0;
        int expectedCount = expectedDiagnostics?.Length ?? 1;
        int defaultIterations = Math.Max(1, Math.Max(remainingCount + 1, expectedCount));
        
        int finalIterations = iterations ?? defaultIterations;
        
        test.NumberOfFixAllIterations = finalIterations;
        test.NumberOfIncrementalIterations = finalIterations;
        test.ExpectedDiagnostics.AddRange(expectedDiagnostics);

        if (remainingDiagnostics != null)
        {
            test.FixedState.ExpectedDiagnostics.AddRange(remainingDiagnostics);
        }

        await test.RunAsync();
    }

    public static async Task VerifyFixAsync(string source, DiagnosticResult[] expectedDiagnostics, string fixedSource,
        int? codeActionIndex, DiagnosticResult[]? remainingDiagnostics = null, int? iterations = null)
    {
         var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CodeActionIndex = codeActionIndex
        };

        AddAutoMapperReferences(test.TestState);
        AddAutoMapperReferences(test.FixedState);

        int remainingCount = remainingDiagnostics?.Length ?? 0;
        int expectedCount = expectedDiagnostics?.Length ?? 1;
        int defaultIterations = Math.Max(1, Math.Max(remainingCount + 1, expectedCount));
        
        int finalIterations = iterations ?? defaultIterations;
        
        test.NumberOfFixAllIterations = finalIterations;
        test.NumberOfIncrementalIterations = finalIterations;
        test.ExpectedDiagnostics.AddRange(expectedDiagnostics);

        if (remainingDiagnostics != null)
        {
            test.FixedState.ExpectedDiagnostics.AddRange(remainingDiagnostics);
        }

        await test.RunAsync();
    }

    public static async Task VerifyFixWithIterationsAsync(string source, DiagnosticResult[] expectedDiagnostics,
        string fixedSource, int iterations)
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

    public static async Task VerifyFixAsync((string filename, string source)[] sources,
        DiagnosticResult expectedDiagnostic, string fixedSource, DiagnosticResult[]? remainingDiagnostics = null)
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

        int iterations2 = Math.Max(1, (remainingDiagnostics?.Length ?? 0) + 1);
        test.NumberOfFixAllIterations = iterations2;
        test.NumberOfIncrementalIterations = iterations2;
        test.ExpectedDiagnostics.Add(expectedDiagnostic);
        if (remainingDiagnostics != null)
        {
            test.FixedState.ExpectedDiagnostics.AddRange(remainingDiagnostics);
        }

        await test.RunAsync();
    }

    public static async Task VerifyFixWithIterationsAsync((string filename, string source)[] sources,
        DiagnosticResult expectedDiagnostic, string fixedSource, int iterations)
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

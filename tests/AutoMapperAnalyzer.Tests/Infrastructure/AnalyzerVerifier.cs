using System.Threading.Tasks;
using AutoMapper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.Infrastructure;

internal static class AnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    public static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        AddAutoMapperReferences(test.TestState);

        foreach (var diagnostic in expected)
        {
            test.ExpectedDiagnostics.Add(diagnostic);
        }

        await test.RunAsync();
    }

    public static async Task VerifyAnalyzerAsync((string filename, string source)[] sources, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        AddAutoMapperReferences(test.TestState);

        if (sources.Length > 0)
        {
            test.TestCode = sources[0].source;
            for (int i = 1; i < sources.Length; i++)
            {
                test.TestState.Sources.Add((sources[i].filename, sources[i].source));
            }
        }

        foreach (var diagnostic in expected)
        {
            test.ExpectedDiagnostics.Add(diagnostic);
        }

        await test.RunAsync();
    }

    private static void AddAutoMapperReferences(SolutionState state)
    {
        state.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Profile).Assembly.Location));
    }
}

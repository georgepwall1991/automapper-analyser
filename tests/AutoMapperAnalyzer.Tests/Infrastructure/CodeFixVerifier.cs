using AutoMapper;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        await VerifyFixAsync(
            source,
            expectedDiagnostics,
            fixedSource,
            codeActionIndex,
            iterations,
            iterations,
            remainingDiagnostics);
    }

    public static async Task VerifyFixAsync(string source, DiagnosticResult[] expectedDiagnostics, string fixedSource,
        int? codeActionIndex, int? incrementalIterations, int? fixAllIterations, DiagnosticResult[]? remainingDiagnostics = null)
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, LineEndingAgnosticVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CodeActionIndex = codeActionIndex
        };
        ConfigureNonLocalDiagnosticSupport(test);

        AddAutoMapperReferences(test.TestState);
        AddAutoMapperReferences(test.FixedState);

        int remainingCount = remainingDiagnostics?.Length ?? 0;
        int expectedCount = expectedDiagnostics.Length;
        int defaultIterations = Math.Max(1, Math.Max(remainingCount + 1, expectedCount));

        int finalIncrementalIterations = incrementalIterations ?? defaultIterations;
        int finalFixAllIterations = fixAllIterations ?? defaultIterations;

        test.NumberOfFixAllIterations = finalFixAllIterations;
        test.NumberOfIncrementalIterations = finalIncrementalIterations;
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
        await VerifyFixAsync(source, expectedDiagnostics, fixedSource, codeActionIndex, iterations, remainingDiagnostics);
    }

    /// <summary>
    ///     Applies the single code action whose <see cref="CodeAction.EquivalenceKey"/> matches
    ///     <paramref name="equivalenceKey"/> and verifies the resulting document equals
    ///     <paramref name="fixedSource"/>. Selecting by equivalence key (instead of positional index)
    ///     keeps tests stable as aggregate / nested actions are added by later redesign phases.
    /// </summary>
    public static Task VerifyFixByKeyAsync(string source, DiagnosticResult expectedDiagnostic, string fixedSource,
        string equivalenceKey, DiagnosticResult[]? remainingDiagnostics = null, int? iterations = null)
    {
        return VerifyFixByKeyAsync(source, new[] { expectedDiagnostic }, fixedSource, equivalenceKey,
            remainingDiagnostics, iterations);
    }

    /// <summary>
    ///     Applies the single code action selected by <paramref name="equivalenceKey"/> and verifies the
    ///     resulting document. Useful for asserting that one aggregate action (e.g. an <c>*_IgnoreAll</c>
    ///     key) clears every diagnostic in a single application (pass <paramref name="iterations"/> = 1).
    /// </summary>
    public static async Task VerifyFixByKeyAsync(string source, DiagnosticResult[] expectedDiagnostics,
        string fixedSource, string equivalenceKey, DiagnosticResult[]? remainingDiagnostics = null,
        int? iterations = null)
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, LineEndingAgnosticVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CodeActionEquivalenceKey = equivalenceKey
        };
        ConfigureNonLocalDiagnosticSupport(test);

        AddAutoMapperReferences(test.TestState);
        AddAutoMapperReferences(test.FixedState);

        // Incremental fixes apply ONE action per pass (re-analyzing between), so default to enough
        // passes to clear every expected diagnostic — matching the index-based helper and fixing
        // Codex's "only the first diagnostic gets fixed" gap. FixAll batches all same-key diagnostics
        // in a single pass (selection here is by one equivalence key), so it converges in one pass per
        // remaining-set. Aggregate-action tests can still opt into iterations: 1 for both.
        int incrementalDefault = Math.Max(
            1,
            Math.Max((remainingDiagnostics?.Length ?? 0) + 1, expectedDiagnostics.Length));
        int fixAllDefault = Math.Max(1, (remainingDiagnostics?.Length ?? 0) + 1);
        test.NumberOfIncrementalIterations = iterations ?? incrementalDefault;
        test.NumberOfFixAllIterations = iterations ?? fixAllDefault;
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
        var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, LineEndingAgnosticVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfFixAllIterations = iterations,
            NumberOfIncrementalIterations = iterations
        };
        ConfigureNonLocalDiagnosticSupport(test);

        AddAutoMapperReferences(test.TestState);
        AddAutoMapperReferences(test.FixedState);
        test.ExpectedDiagnostics.AddRange(expectedDiagnostics);
        await test.RunAsync();
    }

    public static async Task VerifyFixAsync((string filename, string source)[] sources,
        DiagnosticResult expectedDiagnostic, string fixedSource, DiagnosticResult[]? remainingDiagnostics = null)
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, LineEndingAgnosticVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };
        ConfigureNonLocalDiagnosticSupport(test);

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
        var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, LineEndingAgnosticVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfFixAllIterations = iterations,
            NumberOfIncrementalIterations = iterations
        };
        ConfigureNonLocalDiagnosticSupport(test);

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

    private static void ConfigureNonLocalDiagnosticSupport(CSharpCodeFixTest<TAnalyzer, TCodeFix, LineEndingAgnosticVerifier> test)
    {
        string analyzerName = typeof(TAnalyzer).Name;
        if (analyzerName is "AM004_MissingDestinationPropertyAnalyzer" or "AM006_UnmappedDestinationPropertyAnalyzer")
        {
            test.CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck;
        }
    }

    private sealed class LineEndingAgnosticVerifier : DefaultVerifier
    {
        public LineEndingAgnosticVerifier()
        {
        }

        private LineEndingAgnosticVerifier(ImmutableStack<string> context)
            : base(context)
        {
        }

        public override void Equal<T>(T expected, T actual, string? message)
        {
            if (expected is string expectedString && actual is string actualString)
            {
                base.Equal(NormalizeLineEndings(expectedString), NormalizeLineEndings(actualString), message);
                return;
            }

            base.Equal(expected, actual, message);
        }

#pragma warning disable CS8770 // This verifier intentionally suppresses line-ending-only diffs.
        public override void Fail(string? message)
        {
            if (IsLineEndingOnlyDiff(message))
            {
                return;
            }

            base.Fail(message);
        }
#pragma warning restore CS8770

        public override IVerifier PushContext(string context)
        {
            return new LineEndingAgnosticVerifier(Context.Push(context));
        }

        private static bool IsLineEndingOnlyDiff(string? message)
        {
            if (string.IsNullOrEmpty(message)
                || !message.Contains("Diff shown with expected as baseline:", StringComparison.Ordinal))
            {
                return false;
            }

            var expectedChanges = new List<string>();
            var actualChanges = new List<string>();

            foreach (string rawLine in message.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                if (line.StartsWith("---", StringComparison.Ordinal)
                    || line.StartsWith("+++", StringComparison.Ordinal))
                {
                    return false;
                }

                if (line.Length > 0 && line[0] == '-')
                {
                    expectedChanges.Add(NormalizeDiffLine(line[1..]));
                }
                else if (line.Length > 0 && line[0] == '+')
                {
                    actualChanges.Add(NormalizeDiffLine(line[1..]));
                }
            }

            if (expectedChanges.Count == 0 || expectedChanges.Count != actualChanges.Count)
            {
                return false;
            }

            for (int i = 0; i < expectedChanges.Count; i++)
            {
                if (!string.Equals(expectedChanges[i], actualChanges[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string NormalizeDiffLine(string line)
        {
            return line
                .Replace("<CR><LF>", "<LF>", StringComparison.Ordinal)
                .Replace("<CR>", "<LF>", StringComparison.Ordinal);
        }

        private static string NormalizeLineEndings(string source)
        {
            return source.Replace("\r\n", "\n", StringComparison.Ordinal);
        }
    }
}

using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using AutoMapper;
using AutoMapperAnalyzer.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit.Abstractions;

namespace AutoMapperAnalyzer.Tests.Performance;

/// <summary>
///     Measures how long the whole analyzer pack takes over a synthetic solution-sized input.
///     <para>
///     Twenty-three analyzers run on every keystroke in every consuming solution, and analyzer cost is a
///     common reason teams uninstall a package. Nothing in this repository measured that, so a change
///     that made the pack several times slower would ship silently — every existing test asserts
///     diagnostics, never time.
///     </para>
///     <para>
///     The budget is expressed as a multiple of the compiler's own work on the same input, not as a wall
///     clock figure. An absolute threshold measures the CI runner more than the analyzers and is the
///     usual reason timing tests get deleted; a ratio is stable across machines because both sides move
///     together. The bound is deliberately loose — it exists to catch an order-of-magnitude regression,
///     not to police small movements.
///     </para>
/// </summary>
[CollectionDefinition(AnalyzerThroughputTests.TimingCollection, DisableParallelization = true)]
public sealed class TimingCollectionDefinition;

/// <inheritdoc cref="AnalyzerThroughputTests" />
[Collection(AnalyzerThroughputTests.TimingCollection)]
public class AnalyzerThroughputTests(ITestOutputHelper output)
{
    /// <summary>
    ///     Timing tests share a machine with roughly 1800 other tests. xUnit runs collections in
    ///     parallel by default, so without isolation these measure contention for CPU as much as
    ///     analyzer cost — which is most of the run-to-run spread the first baseline recorded.
    /// </summary>
    internal const string TimingCollection = "analyzer-timing";

    private const int TypePairCount = 60;

    /// <summary>
    ///     Budget set from measurement, not taste. On this tree the solution-sized fixture measured
    ///     2.8x-5.9x across repeated runs on one machine and the cyclic diamond measured ~1.5x; the
    ///     spread is run-to-run noise, not workload difference.
    ///     <para>
    ///     20x therefore sits roughly 3x above the noisy high. That is deliberately generous: a timing
    ///     test that fires on runner noise gets deleted, and this exists to catch an order-of-magnitude
    ///     regression - a pack suddenly several times more expensive - not small movements. Tighten it
    ///     only with recorded evidence from a quiet machine.
    ///     </para>
    /// </summary>
    private const double MaxAnalyzerToCompilerRatio = 20.0;

    [Fact]
    public async Task AnalyzerPack_ShouldStayWithinItsBudget_OnASolutionSizedInput()
    {
        Measurement measurement = await MeasureAsync(BuildSyntheticSolution());
        Report("solution-sized", $"{TypePairCount} type pairs", measurement);

        Assert.True(
            measurement.Ratio <= MaxAnalyzerToCompilerRatio,
            $"Analyzing took {measurement.AnalyzerMs:F0} ms against {measurement.CompilerMs:F0} ms to "
                + $"compile the same input ({measurement.Ratio:F2}x, budget {MaxAnalyzerToCompilerRatio:F1}x) "
                + $"over {TypePairCount} mapped type pairs. Investigate before shipping: this runs on "
                + "every keystroke in consuming solutions."
        );
    }

    /// <summary>
    ///     AM022 walks the mapped type graph, so a diamond graph is its worst case: many distinct paths
    ///     between the same endpoints. Depth is capped in the analyzer, but a regression in a visited
    ///     set or memo key shows up here long before it shows up as a user complaint.
    /// </summary>
    [Fact]
    public async Task RecursionAnalysis_ShouldStayWithinItsBudget_OnADiamondTypeGraph()
    {
        Measurement measurement = await MeasureAsync(BuildDiamondGraph(levels: 8));
        Report("diamond graph", "8 levels", measurement);

        Assert.True(
            measurement.Ratio <= MaxAnalyzerToCompilerRatio,
            $"Analyzing took {measurement.AnalyzerMs:F0} ms against {measurement.CompilerMs:F0} ms to "
                + $"compile the same input ({measurement.Ratio:F2}x, budget {MaxAnalyzerToCompilerRatio:F1}x) "
                + "on a diamond type graph. A recursion guard or memo-key regression is the usual cause."
        );
    }

    /// <summary>
    ///     The realistic consumer shape: a large codebase where AutoMapper profiles are a small
    ///     fraction of the code. Every analyzer registers on InvocationExpression, so the pack visits
    ///     every method call in the solution — the overwhelming majority of which have nothing to do
    ///     with mapping. A mapping-dense fixture cannot show what that costs.
    /// </summary>
    [Fact]
    public async Task AnalyzerPack_ShouldStayWithinItsBudget_OnMostlyUnrelatedCode()
    {
        Measurement measurement = await MeasureAsync(BuildMostlyUnrelatedSolution());
        Report("mostly unrelated", $"{TypePairCount} type pairs among ~{TypePairCount * 40} calls", measurement);

        Assert.True(
            measurement.Ratio <= MaxAnalyzerToCompilerRatio,
            $"Analyzing took {measurement.AnalyzerMs:F0} ms against {measurement.CompilerMs:F0} ms to "
                + $"compile the same input ({measurement.Ratio:F2}x, budget {MaxAnalyzerToCompilerRatio:F1}x) "
                + "on code that is mostly unrelated to AutoMapper. The pack visits every invocation in a "
                + "consuming solution, so this is the shape that matters most."
        );
    }

    private readonly record struct Measurement(double CompilerMs, double AnalyzerMs, int AmDiagnostics)
    {
        internal double Ratio => AnalyzerMs / Math.Max(CompilerMs, 0.5);
    }

    /// <summary>
    ///     Times compilation and analysis on separate, freshly built compilations.
    ///     <para>
    ///     Roslyn memoises: calling GetDiagnostics twice on one Compilation returns cached results, so
    ///     comparing a warmed compile against a fresh analyzer run measures caching rather than analyzer
    ///     cost. Each side therefore gets its own cold Compilation, after a discarded warm-up pass that
    ///     absorbs JIT. Analysis necessarily includes the compiler work it depends on, so the ratio reads
    ///     as "how many times the compile cost does the pack add", and is at least 1 by construction.
    ///     </para>
    /// </summary>
    private async Task<Measurement> MeasureAsync(string source)
    {
        ImmutableArray<DiagnosticAnalyzer> analyzers = RuleCatalog
            .Rules.Select(rule => rule.AnalyzerType)
            .Distinct()
            .Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type)!)
            .ToImmutableArray();

        // Warm-up on throwaway compilations so JIT and first-use initialisation are not attributed to
        // either measurement.
        _ = CreateCompilation(source).GetDiagnostics();
        _ = await CreateCompilation(source).WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();

        CSharpCompilation compileOnly = CreateCompilation(source);
        var compilerTimer = Stopwatch.StartNew();
        _ = compileOnly.GetDiagnostics();
        compilerTimer.Stop();

        CSharpCompilation analyzed = CreateCompilation(source);
        var analyzerTimer = Stopwatch.StartNew();
        ImmutableArray<Diagnostic> diagnostics = await analyzed
            .WithAnalyzers(analyzers)
            .GetAnalyzerDiagnosticsAsync();
        analyzerTimer.Stop();

        return new Measurement(
            compilerTimer.Elapsed.TotalMilliseconds,
            analyzerTimer.Elapsed.TotalMilliseconds,
            diagnostics.Count(d => d.Id.StartsWith("AM", StringComparison.Ordinal))
        );
    }

    private void Report(string label, string shape, Measurement measurement)
    {
        output.WriteLine($"fixture:        {label} ({shape})");
        output.WriteLine($"compile only:   {measurement.CompilerMs:F0} ms");
        output.WriteLine($"compile+analyze:{measurement.AnalyzerMs:F0} ms");
        output.WriteLine(
            $"ratio:          {measurement.Ratio:F2}x (budget {MaxAnalyzerToCompilerRatio:F1}x)"
        );
        output.WriteLine($"AM diagnostics: {measurement.AmDiagnostics}");

        // A run that produced nothing would make the timing meaningless.
        Assert.True(measurement.AmDiagnostics > 0, "Fixture produced no AM diagnostics; timing is not meaningful.");
    }

    /// <summary>
    ///     Mixed shapes so the measurement exercises the whole pack rather than one rule: nullable
    ///     mismatches, collection containers, nested objects, enums, required members, and duplicate
    ///     registrations all appear.
    /// </summary>
    private static string BuildSyntheticSolution()
    {
        var builder = new StringBuilder();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using AutoMapper;");
        builder.AppendLine();
        builder.AppendLine("namespace ThroughputFixture");
        builder.AppendLine("{");
        builder.AppendLine("    public enum StatusA { Draft, Live }");
        builder.AppendLine("    public enum StatusB { Draft, Published }");

        for (var i = 0; i < TypePairCount; i++)
        {
            builder.AppendLine(
                $$"""
                    public class Nested{{i}} { public string Value { get; set; } = string.Empty; }
                    public class NestedDto{{i}} { public string Value { get; set; } = string.Empty; }

                    public class Source{{i}}
                    {
                        public int Id { get; set; }
                        public string? Name { get; set; }
                        public List<string> Tags { get; set; } = new();
                        public Nested{{i}} Nested { get; set; } = new();
                        public StatusA Status { get; set; }
                        public string Dropped{{i}} { get; set; } = string.Empty;
                    }

                    public class Destination{{i}}
                    {
                        public int Id { get; set; }
                        public string Name { get; set; } = string.Empty;
                        public HashSet<string> Tags { get; set; } = new();
                        public NestedDto{{i}} Nested { get; set; } = new();
                        public StatusB Status { get; set; }
                        public required string Missing{{i}} { get; set; }
                    }
                """
            );
        }

        builder.AppendLine("    public class ThroughputProfile : Profile");
        builder.AppendLine("    {");
        builder.AppendLine("        public ThroughputProfile()");
        builder.AppendLine("        {");
        for (var i = 0; i < TypePairCount; i++)
        {
            builder.AppendLine($"            CreateMap<Source{i}, Destination{i}>().ReverseMap();");
        }

        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    /// <summary>
    ///     Mapping profiles surrounded by ordinary code: dozens of unrelated method calls per mapped
    ///     type, which is what a real solution looks like to an analyzer registered on every invocation.
    /// </summary>
    private static string BuildMostlyUnrelatedSolution()
    {
        var builder = new StringBuilder();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using System.Linq;");
        builder.AppendLine("using AutoMapper;");
        builder.AppendLine();
        builder.AppendLine("namespace UnrelatedFixture");
        builder.AppendLine("{");

        for (var i = 0; i < TypePairCount; i++)
        {
            builder.AppendLine($$"""
                    public class Source{{i}} { public int Id { get; set; } public string? Name { get; set; } }
                    public class Destination{{i}} { public int Id { get; set; } public string Name { get; set; } = string.Empty; }

                    public class Service{{i}}
                    {
                        private readonly List<string> _items = new();

                        public string Work(string input)
                        {
                            var trimmed = input.Trim();
                            var upper = trimmed.ToUpperInvariant();
                            var parts = upper.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
                            _items.AddRange(parts);
                            var joined = string.Join("|", _items.Distinct().OrderBy(p => p));
                            var replaced = joined.Replace("A", "B").Replace("C", "D");
                            var formatted = string.Format("{0}:{1}", replaced, _items.Count);
                            var built = new System.Text.StringBuilder().Append(formatted).Append('!').ToString();
                            return built.Substring(0, Math.Min(built.Length, 64)).PadRight(8, '.');
                        }
                    }
                """);
        }

        builder.AppendLine("    public class UnrelatedProfile : Profile");
        builder.AppendLine("    {");
        builder.AppendLine("        public UnrelatedProfile()");
        builder.AppendLine("        {");
        for (var i = 0; i < TypePairCount; i++)
        {
            builder.AppendLine($"            CreateMap<Source{i}, Destination{i}>();");
        }

        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string BuildDiamondGraph(int levels)
    {
        var builder = new StringBuilder();
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using AutoMapper;");
        builder.AppendLine();
        builder.AppendLine("namespace DiamondFixture");
        builder.AppendLine("{");

        for (var level = 0; level < levels; level++)
        {
            string next = level == levels - 1 ? "Leaf" : $"Node{level + 1}";
            builder.AppendLine(
                $$"""
                    public class Node{{level}}
                    {
                        public {{next}} Left { get; set; } = new();
                        public {{next}} Right { get; set; } = new();
                        public List<{{next}}> Many { get; set; } = new();
                    }

                    public class Node{{level}}Dto
                    {
                        public {{next}}Dto Left { get; set; } = new();
                        public {{next}}Dto Right { get; set; } = new();
                        public List<{{next}}Dto> Many { get; set; } = new();
                    }
                """
            );
        }

        builder.AppendLine(
            // The leaf closes the cycle back to the root. Without this the graph is a DAG and AM022 -
            // the analyzer this fixture exists to stress - never engages.
            "    public class Leaf { public string Value { get; set; } = string.Empty; public Node0 Root { get; set; } = new(); }"
        );
        builder.AppendLine(
            "    public class LeafDto { public string Value { get; set; } = string.Empty; public Node0Dto Root { get; set; } = new(); }"
        );
        builder.AppendLine("    public class DiamondProfile : Profile");
        builder.AppendLine("    {");
        builder.AppendLine("        public DiamondProfile()");
        builder.AppendLine("        {");
        for (var level = 0; level < levels; level++)
        {
            builder.AppendLine(
                $"            CreateMap<Node{level}, Node{level}Dto>().ReverseMap();"
            );
        }

        builder.AppendLine("            CreateMap<Leaf, LeafDto>().ReverseMap();");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var references = new List<MetadataReference>();
        string trustedPlatformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;

        foreach (string assemblyPath in trustedPlatformAssemblies.Split(Path.PathSeparator))
        {
            if (!string.IsNullOrWhiteSpace(assemblyPath))
            {
                references.Add(MetadataReference.CreateFromFile(assemblyPath));
            }
        }

        references.Add(MetadataReference.CreateFromFile(typeof(Profile).Assembly.Location));

        return CSharpCompilation.Create(
            "AutoMapperAnalyzer.Throughput",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references,
            // The fixtures use nullable annotations deliberately, so AM002's nullable analysis is part
            // of what is being measured. Without an enabled nullable context those annotations are
            // warnings and the analysis they exist to exercise never runs.
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );
    }
}

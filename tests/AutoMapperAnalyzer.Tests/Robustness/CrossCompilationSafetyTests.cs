using System.Collections.Immutable;
using AutoMapper;
using AutoMapperAnalyzer.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Tests.Robustness;

/// <summary>
///     Drives the analyzer catalog over a profile whose mapped types live in a referenced
///     <b>compilation</b> — the shape MSBuildWorkspace produces for project references.
///     <para>
///     Symbols reached through a <see cref="CompilationReference" /> keep
///     <see cref="ISymbol.DeclaringSyntaxReferences" />, but those references resolve to trees that are
///     not part of the analyzed compilation. Anchoring a reported diagnostic on one throws
///     <see cref="ArgumentException" /> and surfaces to the user as AD0001 — the failure corpus
///     scanning found on CleanArchitecture, where AM004 crashed for every DTO declared in the Domain
///     project and consumed by the Application project's profiles.
///     </para>
/// </summary>
public class CrossCompilationSafetyTests
{
    private const string ModelsSource = """
        namespace Models
        {
            public class Source
            {
                public string Name { get; set; } = string.Empty;
                public string Extra { get; set; } = string.Empty;
                public string Value { get; set; } = string.Empty;
                public string UserName { get; set; } = string.Empty;
            }

            public class Destination
            {
                public string Name { get; set; } = string.Empty;
                public int Value { get; set; }
                public string Username { get; set; } = string.Empty;
                public string Missing { get; set; } = string.Empty;
                public required string Req { get; set; }
            }
        }
        """;

    private const string ProfileSource = """
        using AutoMapper;
        using Models;

        namespace Profiles
        {
            public class TestProfile : Profile
            {
                public TestProfile()
                {
                    CreateMap<Source, Destination>();
                }
            }
        }
        """;

    /// <summary>
    ///     Every rule that anchors on a member declaration must still report when that member is
    ///     declared in another compilation — the diagnostic falls back to the CreateMap invocation —
    ///     and none may throw while doing so.
    /// </summary>
    [Theory]
    [InlineData("AM001")]
    [InlineData("AM004")]
    [InlineData("AM005")]
    [InlineData("AM006")]
    [InlineData("AM011")]
    public async Task MemberAnchoredRules_ShouldReportInsideTheAnalyzedCompilation_WhenModelsAreProjectReferenced(
        string ruleId
    )
    {
        (Compilation compilation, SyntaxTree profileTree) = BuildCrossCompilationPair();
        DiagnosticAnalyzer analyzer = CreateAnalyzer(ruleId);

        (ImmutableArray<Diagnostic> diagnostics, IReadOnlyList<string> failures) =
            await RunAnalyzersAsync(compilation, [analyzer]);

        Assert.True(
            failures.Count == 0,
            $"{ruleId} threw while analyzing project-referenced models:{Environment.NewLine}"
                + string.Join(Environment.NewLine, failures)
        );

        List<Diagnostic> reported = diagnostics.Where(d => d.Id == ruleId).ToList();
        Assert.True(
            reported.Count > 0,
            $"{ruleId} reported nothing for project-referenced models; the fix may only move WHERE the "
                + "diagnostic lands, never WHETHER it fires."
        );

        Assert.All(
            reported,
            diagnostic =>
                Assert.True(
                    compilation.ContainsSyntaxTree(diagnostic.Location.SourceTree!)
                        && ReferenceEquals(diagnostic.Location.SourceTree, profileTree),
                    $"{ruleId} reported a location outside the analyzed compilation: "
                        + diagnostic.Location.GetLineSpan()
                )
        );
    }

    /// <summary>
    ///     The whole catalog over the same project-reference shape. A rule that reads a foreign
    ///     declaration for anything but a reported location — feeding it to a semantic model, for
    ///     instance — throws just the same, so the sweep stays wider than the rules pinned above.
    /// </summary>
    [Fact]
    public async Task EveryAnalyzer_ShouldNotThrow_WhenMappedTypesComeFromAProjectReference()
    {
        (Compilation compilation, _) = BuildCrossCompilationPair();

        ImmutableArray<DiagnosticAnalyzer> analyzers = RuleCatalog
            .Rules.Select(rule => rule.AnalyzerType)
            .Distinct()
            .Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type)!)
            .ToImmutableArray();

        (_, IReadOnlyList<string> failures) = await RunAnalyzersAsync(compilation, analyzers);

        Assert.True(
            failures.Count == 0,
            "Analyzers threw on project-referenced models:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, failures)
        );
    }

    private static DiagnosticAnalyzer CreateAnalyzer(string ruleId)
    {
        Type analyzerType = RuleCatalog
            .Rules.Where(rule => rule.RuleId == ruleId)
            .Select(rule => rule.AnalyzerType)
            .Distinct()
            .Single();

        return (DiagnosticAnalyzer)Activator.CreateInstance(analyzerType)!;
    }

    private static async Task<(
        ImmutableArray<Diagnostic> Diagnostics,
        IReadOnlyList<string> Failures
    )> RunAnalyzersAsync(Compilation compilation, IReadOnlyList<DiagnosticAnalyzer> analyzers)
    {
        var failures = new List<string>();
        var failureLock = new object();

        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: (exception, analyzer, _) =>
            {
                lock (failureLock)
                {
                    failures.Add(
                        $"{analyzer.GetType().Name}: {exception.GetType().Name}: {exception.Message}"
                    );
                }
            },
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false
        );

        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers(analyzers.ToImmutableArray(), options)
            .GetAnalyzerDiagnosticsAsync();

        foreach (
            Diagnostic diagnostic in diagnostics.Where(diagnostic =>
                string.Equals(diagnostic.Id, "AD0001", StringComparison.Ordinal)
            )
        )
        {
            lock (failureLock)
            {
                failures.Add("AD0001: " + diagnostic.GetMessage());
            }
        }

        return (diagnostics, failures);
    }

    /// <summary>
    ///     Builds a profile compilation that sees its model types through a
    ///     <see cref="CompilationReference" /> — <see cref="Compilation.ToMetadataReference" /> does not
    ///     emit, so the model symbols keep syntax references into the models compilation's trees. That
    ///     is exactly what MSBuildWorkspace produces for project references, and what a metadata
    ///     (PE) reference cannot reproduce.
    /// </summary>
    private static (Compilation ProfileCompilation, SyntaxTree ProfileTree) BuildCrossCompilationPair()
    {
        List<MetadataReference> references = BuildReferences();

        var modelsCompilation = CSharpCompilation.Create(
            "CrossCompilation.Models",
            [
                CSharpSyntaxTree.ParseText(
                    ModelsSource,
                    new CSharpParseOptions(LanguageVersion.Preview),
                    path: "Models.cs"
                ),
            ],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        SyntaxTree profileTree = CSharpSyntaxTree.ParseText(
            ProfileSource,
            new CSharpParseOptions(LanguageVersion.Preview),
            path: "Profile.cs"
        );

        var profileCompilation = CSharpCompilation.Create(
            "CrossCompilation.Profile",
            [profileTree],
            [.. references, modelsCompilation.ToMetadataReference()],
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        return (profileCompilation, profileTree);
    }

    private static List<MetadataReference> BuildReferences()
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
        return references;
    }
}

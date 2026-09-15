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

    private const string CtorModelsSource = """
        namespace Models
        {
            public class CtorSource
            {
                public string Name { get; set; } = string.Empty;
            }

            public record DestRecord(string Renamed);

            public class DestClass
            {
                public DestClass(string renamed)
                {
                    Differently = renamed;
                }

                public string Differently { get; set; } = string.Empty;
            }
        }
        """;

    private const string CtorProfileSource = """
        using AutoMapper;
        using Models;

        namespace Profiles
        {
            public class TestProfile : Profile
            {
                public TestProfile()
                {
                    CreateMap<CtorSource, DestRecord>()
                        .ForCtorParam("renamed", o => o.MapFrom(s => s.Name));
                    CreateMap<CtorSource, DestClass>()
                        .ForCtorParam("renamed", o => o.MapFrom(s => s.Name));
                }
            }
        }
        """;

    private const string ComparerHelpersSource = """
        namespace Helpers
        {
            public static class Comparers
            {
                public static readonly System.StringComparer Cmp = System.StringComparer.Ordinal;
                public static System.StringComparer CmpProp => System.StringComparer.OrdinalIgnoreCase;
            }
        }
        """;

    private const string ComparerProfileSource = """
        using AutoMapper;
        using Helpers;
        using Models;

        namespace Profiles
        {
            public class TestProfile : Profile
            {
                public TestProfile()
                {
                    CreateMap<Source, Destination>()
                        .ForMember(d => d.Missing, o => o.MapFrom(s => Comparers.Cmp.GetHashCode(s.Name)));
                    CreateMap<Source, FlatDestination>()
                        .ForMember(d => d.Missing, o => o.MapFrom(s => Comparers.CmpProp.GetHashCode(s.Name)));
                }
            }
        }
        """;

    private const string ConverterHelpersSource = """
        using AutoMapper;
        using Models;

        namespace Helpers
        {
            public class FooConverter : ITypeConverter<Source, Destination>
            {
                public Destination Convert(Source source, Destination destination, ResolutionContext context)
                {
                    return destination;
                }
            }

            public static class Holder
            {
                public static readonly System.Type Conv = typeof(FooConverter);
                public static System.Type ConvProp => typeof(FooConverter);
            }
        }
        """;

    private const string ConverterProfileSource = """
        using AutoMapper;
        using Helpers;
        using Models;

        namespace Profiles
        {
            public class TestProfile : Profile
            {
                public TestProfile()
                {
                    CreateMap<Source, Destination>().ConvertUsing(Holder.Conv);
                    CreateMap<Source, FlatDestination>().ConvertUsing(Holder.ConvProp);
                }
            }
        }
        """;

    private const string ExtraModelsSource = """

        namespace Models
        {
            public class FlatDestination
            {
                public string Name { get; set; } = string.Empty;
                public string Missing { get; set; } = string.Empty;
            }
        }
        """;
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

    /// <summary>
    ///     ForCtorParam analysis resolves the destination constructor's <i>syntax</i> — a body
    ///     assignment (<c>DestClass</c>) or a positional-record parameter (<c>DestRecord</c>) — and
    ///     binds it with a semantic model. When that constructor lives in a referenced compilation,
    ///     binding its tree throws. Both helpers must fail closed instead.
    /// </summary>
    [Theory]
    [InlineData("AM002")]
    [InlineData("AM022")]
    public async Task ConstructorParameterAnalysis_ShouldNotThrow_WhenDestinationCtorIsProjectReferenced(
        string ruleId
    )
    {
        Compilation compilation = BuildCrossCompilationPair(CtorModelsSource, CtorProfileSource)
            .ProfileCompilation;

        (_, IReadOnlyList<string> failures) = await RunAnalyzersAsync(compilation, [CreateAnalyzer(ruleId)]);

        Assert.True(
            failures.Count == 0,
            $"{ruleId} threw while resolving a project-referenced constructor:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, failures)
        );
    }

    /// <summary>
    ///     A member initializer reached through <see cref="ISymbol.DeclaringSyntaxReferences" /> can
    ///     sit in a different tree of the <i>same</i> compilation — a comparer field in a helpers
    ///     file, for instance. A semantic model only binds its own tree, so the analyzer must rebind
    ///     before resolving the initializer. Rebinding preserves the designed detection: a
    ///     <c>static readonly StringComparer</c> stays a known-cheap receiver and reports nothing.
    /// </summary>
    [Fact]
    public async Task ComparerReceiversInAnotherFile_ShouldStayRecognized_WithoutThrowing()
    {
        Compilation compilation = BuildSingleCompilation(
            ("Models.cs", ModelsSource),
            ("ExtraModels.cs", ExtraModelsSource),
            ("Helpers.cs", ComparerHelpersSource),
            ("Profile.cs", ComparerProfileSource)
        );

        (ImmutableArray<Diagnostic> diagnostics, IReadOnlyList<string> failures) =
            await RunAnalyzersAsync(compilation, [CreateAnalyzer("AM031")]);

        Assert.True(
            failures.Count == 0,
            "AM031 threw while following a cross-file comparer receiver:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, failures)
        );

        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Id == "AM031" &&
                          diagnostic.GetMessage().Contains("method call")
        );
    }

    /// <summary>
    ///     Same boundary, different analysis: AM033 marks a converter used by following a
    ///     <c>ConvertUsing</c> argument to the <c>System.Type</c> field or property that holds it.
    ///     When that member is declared in another file, rebinding its tree keeps the converter
    ///     recognized as used — skipping it would crash or produce an unused-converter false
    ///     positive.
    /// </summary>
    [Fact]
    public async Task ConverterTypeMembersInAnotherFile_ShouldStayRecognizedAsUsed_WithoutThrowing()
    {
        Compilation compilation = BuildSingleCompilation(
            ("Models.cs", ModelsSource),
            ("ExtraModels.cs", ExtraModelsSource),
            ("Converters.cs", ConverterHelpersSource),
            ("Profile.cs", ConverterProfileSource)
        );

        (ImmutableArray<Diagnostic> diagnostics, IReadOnlyList<string> failures) =
            await RunAnalyzersAsync(compilation, [CreateAnalyzer("AM033")]);

        Assert.True(
            failures.Count == 0,
            "AM033 threw while following a cross-file converter type reference:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, failures)
        );

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "AM033");
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
    private static (Compilation ProfileCompilation, SyntaxTree ProfileTree) BuildCrossCompilationPair() =>
        BuildCrossCompilationPair(ModelsSource, ProfileSource);

    private static (Compilation ProfileCompilation, SyntaxTree ProfileTree) BuildCrossCompilationPair(
        string modelsSource,
        string profileSource)
    {
        List<MetadataReference> references = BuildReferences();

        var modelsCompilation = CSharpCompilation.Create(
            "CrossCompilation.Models",
            [
                CSharpSyntaxTree.ParseText(
                    modelsSource,
                    new CSharpParseOptions(LanguageVersion.Preview),
                    path: "Models.cs"
                ),
            ],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        SyntaxTree profileTree = CSharpSyntaxTree.ParseText(
            profileSource,
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

    /// <summary>
    ///     Builds one compilation from several named sources — the same-compilation/different-tree
    ///     boundary. A <see cref="SemanticModel" /> bound to one tree cannot answer questions about
    ///     nodes from another, which is exactly what cross-file member initializers exercise.
    /// </summary>
    private static Compilation BuildSingleCompilation(params (string Path, string Source)[] sources)
    {
        SyntaxTree[] trees = sources
            .Select(source =>
                CSharpSyntaxTree.ParseText(
                    source.Source,
                    new CSharpParseOptions(LanguageVersion.Preview),
                    path: source.Path
                )
            )
            .ToArray();

        return CSharpCompilation.Create(
            "CrossFile.Profile",
            trees,
            BuildReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );
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

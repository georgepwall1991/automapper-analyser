using System.Collections.Immutable;
using AutoMapper;
using AutoMapperAnalyzer.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Tests.Robustness;

/// <summary>
///     Drives every shipped analyzer over code that does not compile, is half-typed, or is otherwise
///     hostile, and fails if any analyzer throws.
///     <para>
///     Per-rule tests feed analyzers well-formed code because they are testing diagnostics. But an
///     analyzer runs on every keystroke, so it spends much of its life reading incomplete syntax and
///     error types. An exception there surfaces as <c>AD0001</c> in the user's Error List, and one
///     AD0001 discredits all 23 rules at once — the user cannot tell which analyzer misbehaved.
///     </para>
///     <para>
///     There is deliberately no try/catch wrapper in the analyzers. Roslyn already contains analyzer
///     exceptions; swallowing them locally would hide real defects from this suite while making the IDE
///     quieter. These tests exist to find crashes, not to mask them.
///     </para>
/// </summary>
public class AnalyzerCrashSafetyTests
{
    /// <summary>
    ///     Shapes chosen to hit the paths the analyzers actually walk: type-argument resolution, fluent
    ///     chain traversal, member selectors, recursion guards, and nullable/generic unwrapping — each
    ///     under syntax or symbols that do not resolve.
    /// </summary>
    public static TheoryData<string, string> HostileSources()
    {
        return new TheoryData<string, string>
        {
            { "incomplete generic argument list", "CreateMap<Source, >();" },
            { "unterminated invocation", "CreateMap<Source, Destination>(" },
            { "missing type arguments", "CreateMap<>();" },
            { "no type arguments at all", "CreateMap();" },
            { "undefined types", "CreateMap<Missing, AlsoMissing>();" },
            { "half-typed member selector", "CreateMap<Source, Destination>().ForMember(d => d." },
            {
                "member selector on nothing",
                "CreateMap<Source, Destination>().ForMember(d => , o => o.Ignore());"
            },
            {
                "unresolved options call",
                "CreateMap<Source, Destination>().ForMember(d => d.Name, o => o.NotAThing());"
            },
            { "dangling fluent chain", "CreateMap<Source, Destination>()." },
            { "ReverseMap on nothing", "CreateMap<Source, Destination>().ReverseMap()." },
            {
                "ForCtorParam without arguments",
                "CreateMap<Source, Destination>().ForCtorParam();"
            },
            {
                "ForPath with empty path",
                "CreateMap<Source, Destination>().ForPath(d => , o => o.Ignore());"
            },
            {
                "IncludeMembers with no selector",
                "CreateMap<Source, Destination>().IncludeMembers();"
            },
            {
                "IncludeMembers with unresolved selector",
                "CreateMap<Source, Destination>().IncludeMembers(s => s.Nope);"
            },
            {
                "ConvertUsing unresolved converter",
                "CreateMap<Source, Destination>().ConvertUsing<NoSuchConverter>();"
            },
            { "typeof with missing type", "CreateMap(typeof(Missing), typeof(Destination));" },
            {
                "open generic typeof",
                "CreateMap(typeof(System.Collections.Generic.List<>), typeof(Destination));"
            },
            { "self-referential map", "CreateMap<Source, Source>();" },
            {
                "nested unresolved generics",
                "CreateMap<System.Collections.Generic.List<Missing>, Destination>();"
            },
            {
                "duplicated dangling chain",
                "CreateMap<Source, Destination>().ForMember().ForMember().ReverseMap();"
            },
        };
    }

    [Theory]
    [MemberData(nameof(HostileSources))]
    public async Task EveryAnalyzer_ShouldNotThrow_OnHostileInput(
        string description,
        string mappingStatement
    )
    {
        string source = $$"""
            using AutoMapper;

            namespace TestNamespace
            {
                public class Inner { public string Name { get; set; } }
                public class Source { public string Name { get; set; } public Inner Inner { get; set; } }
                public class Destination { public string Name { get; set; } public required string Req { get; set; } }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        {{mappingStatement}}
                    }
                }
            }
            """;

        IReadOnlyList<string> failures = await RunEveryAnalyzerAsync(source);

        Assert.True(
            failures.Count == 0,
            $"Analyzers threw on hostile input ({description}):{Environment.NewLine}"
                + string.Join(Environment.NewLine, failures)
        );
    }

    /// <summary>
    ///     A type graph that is cyclic through several shapes at once. Recursion guards are per-analyzer
    ///     and easy to get subtly wrong; a stack overflow cannot be caught, so this fails the run rather
    ///     than reporting an exception.
    /// </summary>
    [Fact]
    public async Task EveryAnalyzer_ShouldNotThrow_OnCyclicTypeGraph()
    {
        const string source = """
            using System.Collections.Generic;
            using AutoMapper;

            namespace TestNamespace
            {
                public class A { public B B { get; set; } public List<A> Selves { get; set; } }
                public class B { public C C { get; set; } public A BackToA { get; set; } }
                public class C { public A A { get; set; } public Dictionary<string, B> Map { get; set; } }

                public class ADto { public BDto B { get; set; } public List<ADto> Selves { get; set; } }
                public class BDto { public CDto C { get; set; } public ADto BackToA { get; set; } }
                public class CDto { public ADto A { get; set; } public Dictionary<string, BDto> Map { get; set; } }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<A, ADto>().ReverseMap();
                        CreateMap<B, BDto>().ReverseMap();
                        CreateMap<C, CDto>().ReverseMap();
                    }
                }
            }
            """;

        IReadOnlyList<string> failures = await RunEveryAnalyzerAsync(source);

        Assert.True(
            failures.Count == 0,
            "Analyzers threw on a cyclic type graph:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, failures)
        );
    }

    [Fact]
    public async Task EveryAnalyzer_ShouldNotThrow_OnEmptyAndTrivialCompilations()
    {
        foreach (string source in new[] { string.Empty, "using AutoMapper;", "class C { }" })
        {
            IReadOnlyList<string> failures = await RunEveryAnalyzerAsync(source);
            Assert.True(
                failures.Count == 0,
                $"Analyzers threw on a trivial compilation ('{source}'):"
                    + Environment.NewLine
                    + string.Join(Environment.NewLine, failures)
            );
        }
    }

    /// <summary>
    ///     Runs every analyzer in the catalog and returns a failure line per thrown exception. Analyzer
    ///     exceptions are captured directly rather than read from AD0001, so the result does not depend
    ///     on the compilation's diagnostic options.
    /// </summary>
    private static async Task<IReadOnlyList<string>> RunEveryAnalyzerAsync(string source)
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

        CSharpCompilation compilation = CSharpCompilation.Create(
            "AutoMapperAnalyzer.CrashSafety",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        ImmutableArray<DiagnosticAnalyzer> analyzers = RuleCatalog
            .Rules.Select(rule => rule.AnalyzerType)
            .Distinct()
            .Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type)!)
            .ToImmutableArray();

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
            .WithAnalyzers(analyzers, options)
            .GetAnalyzerDiagnosticsAsync();

        // Belt and braces: an AD0001 that arrives without the callback firing is still a crash.
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

        return failures;
    }
}

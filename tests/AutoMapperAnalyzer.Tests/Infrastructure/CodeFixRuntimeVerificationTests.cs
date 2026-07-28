using System.Collections;
using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using AutoMapperAnalyzer.Tests.Infrastructure;

namespace AutoMapperAnalyzer.Tests.Infrastructure;

/// <summary>
///     Executes representative code-fix output against real AutoMapper instead of comparing it to
///     expected text. Fixer tests elsewhere assert string equality, which cannot distinguish a fix that
///     AutoMapper accepts from one that merely looks right.
/// </summary>
public class CodeFixRuntimeVerificationTests
{
    [Fact]
    public void RuntimeVerifier_ShouldAcceptExplicitForMemberFix()
    {
        // Shape produced by the AM004/AM011 "map from similar source property" action.
        const string fixedCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source
                {
                    public string Email { get; set; } = string.Empty;
                }

                public class Destination
                {
                    public required string ContactEmail { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>()
                            .ForMember(dest => dest.ContactEmail, opt => opt.MapFrom(src => src.Email));
                    }
                }
            }
            """;

        CodeFixRuntimeVerifier.AssertConfiguresValidMapper(
            fixedCode,
            "AM004/AM011 explicit ForMember fix"
        );
    }

    [Fact]
    public void RuntimeVerifier_ShouldAcceptIgnoreFix()
    {
        // Shape produced by the AM006/AM011 explicit Ignore action.
        const string fixedCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source
                {
                    public string Name { get; set; } = string.Empty;
                }

                public class Destination
                {
                    public string Name { get; set; } = string.Empty;
                    public string Unmapped { get; set; } = string.Empty;
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>()
                            .ForMember(dest => dest.Unmapped, opt => opt.Ignore());
                    }
                }
            }
            """;

        CodeFixRuntimeVerifier.AssertConfiguresValidMapper(fixedCode, "AM006/AM011 Ignore fix");
    }

    [Fact]
    public void RuntimeVerifier_ShouldRejectConfigurationAutoMapperDoesNotAccept()
    {
        // Compiles cleanly and would pass any string comparison, but leaves a required destination
        // member unmapped. Proves the verifier fails for semantic reasons, not just compiler errors.
        const string invalidFix = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source
                {
                    public string Other { get; set; } = string.Empty;
                }

                public class Destination
                {
                    public string Missing { get; set; } = string.Empty;
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                    }
                }
            }
            """;

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            CodeFixRuntimeVerifier.AssertConfiguresValidMapper(invalidFix)
        );

        Assert.Contains(
            "AutoMapper rejected the configuration",
            failure.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void RuntimeVerifier_ShouldSurfaceCodeFixOutputThatDoesNotCompile()
    {
        const string uncompilableFix = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Missing, AlsoMissing>();
                    }
                }
            }
            """;

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            CodeFixRuntimeVerifier.AssertConfiguresValidMapper(uncompilableFix)
        );

        Assert.Contains("did not compile", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeVerifier_ShouldObserveStackConversionOrder()
    {
        // The 2.30.83 defect class: AM021's Stack<T> element-conversion fix was compile-clean and
        // matched its expected text while silently reversing LIFO order. Only executing the mapping
        // distinguishes correct ordering from plausible-looking output.
        const string fixedCode = """
            using System.Collections.Generic;
            using System.Linq;
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source
                {
                    public Stack<int> Values { get; set; } = new Stack<int>();
                }

                public class Destination
                {
                    public Stack<string> Values { get; set; } = new Stack<string>();
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>()
                            .ForMember(
                                dest => dest.Values,
                                opt => opt.MapFrom(src =>
                                    new Stack<string>(src.Values.Select(value => value.ToString()).Reverse())));
                    }
                }
            }
            """;

        object mapped = CodeFixRuntimeVerifier.MapThroughFixedCode(
            fixedCode,
            "Source",
            "Destination",
            sourceType =>
            {
                object instance = Activator.CreateInstance(sourceType)!;
                var stack = new Stack<int>();
                stack.Push(1);
                stack.Push(2);
                stack.Push(3);
                sourceType.GetProperty("Values")!.SetValue(instance, stack);
                return instance;
            },
            "AM021 Stack<T> element conversion"
        );

        var values = (IEnumerable)mapped.GetType().GetProperty("Values")!.GetValue(mapped)!;
        List<string> ordered = values.Cast<object>().Select(value => value.ToString()!).ToList();

        // Source pops 3,2,1 - the converted destination must pop in the same order.
        Assert.Equal(["3", "2", "1"], ordered);
    }

    [Fact]
    public async Task RealFixerOutput_ShouldProduceAMapperAutoMapperAccepts()
    {
        // End-to-end: run the real AM011 fixer over source, then execute what it produced. Every other
        // fixer test stops at comparing the fixed text to an expected document.
        const string source = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source
                {
                    public string Email { get; set; } = string.Empty;
                }

                public class Destination
                {
                    public required string Email { get; set; }
                    public required string ContactName { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                    }
                }
            }
            """;

        Document document = AggregateFixTestHarness.CreateDocument(source, "RuntimeFixerProof");
        ImmutableArray<Diagnostic> diagnostics =
            await AggregateFixTestHarness.GetDiagnosticsAsync<AM011_UnmappedRequiredPropertyAnalyzer>(document);

        Assert.NotEmpty(diagnostics);

        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions =
            CodeFixActionInspector.Flatten(await GetTopLevelActionsAsync(document, diagnostics));
        string ignoreKey = actions
            .Select(action => action.EquivalenceKey)
            .First(key => key != null && key.Contains("Ignore", StringComparison.OrdinalIgnoreCase))!;

        Document fixedDocument = await CodeFixActionInspector.ApplyActionByKeyAsync(
            document,
            new AM011_UnmappedRequiredPropertyCodeFixProvider(),
            diagnostics,
            ignoreKey);

        string fixedText = (await fixedDocument.GetTextAsync()).ToString();

        // The fixer's own output must configure a mapper AutoMapper accepts.
        CodeFixRuntimeVerifier.AssertConfiguresValidMapper(
            fixedText,
            $"AM011 fixer output for equivalence key '{ignoreKey}'");
    }

    private static async Task<List<CodeAction>> GetTopLevelActionsAsync(
        Document document,
        ImmutableArray<Diagnostic> diagnostics)
    {
        var topLevel = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostics[0].Location.SourceSpan,
            diagnostics,
            (action, _) => topLevel.Add(action),
            CancellationToken.None);

        await new AM011_UnmappedRequiredPropertyCodeFixProvider().RegisterCodeFixesAsync(context);
        return topLevel;
    }
}

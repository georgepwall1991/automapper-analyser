using AutoMapper;
using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Analyzers.Helpers;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using System.Reflection;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

public class AM011_CodeFixTests
{
    [Fact]
    public async Task AM011_ShouldRegister_Clean_BulkActionTitles()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public required string RequiredField { get; set; }
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

        Document document = CreateDocument(testCode);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        Diagnostic diagnostic = (await compilation.WithAnalyzers(
                [new AM011_UnmappedRequiredPropertyAnalyzer()])
            .GetAnalyzerDiagnosticsAsync())
            .Single();

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);
        string[] titles = actions.Select(action => action.Title).ToArray();

        Assert.Contains("Configure bulk fix for required properties...", titles);
        Assert.Contains("Map all unmapped required properties to default values", titles);
        Assert.Contains("Map all unmapped required properties to sample constants", titles);
        Assert.Contains("Ignore all unmapped required properties", titles);
        Assert.Contains("Create all missing source properties", titles);
        Assert.DoesNotContain(
            titles,
            title => title.EnumerateRunes().Any(rune => rune.Value is 0x2705 or 0x1F4DD or 0x26A1));
    }

    [Fact]
    public async Task AM011_ShouldAddDefaultValueMapping()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public required string RequiredField { get; set; }
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

        const string expectedFixedCode = """

                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                                 public string Email { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public string Email { get; set; }
                                                 public required string RequiredField { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredField, opt => opt.MapFrom(src => string.Empty));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(23, 13)
                    .WithArguments("RequiredField"),
                expectedFixedCode,
                1); // Bulk fix: "Map all to default value" (was 0 before interactive wizard)
    }

    [Fact]
    public async Task AM011_ShouldAddConstantValueMapping()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public required int RequiredNumber { get; set; }
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

        const string expectedFixedCode = """

                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public required int RequiredNumber { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredNumber, opt => opt.MapFrom(src => 0));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(21, 13)
                    .WithArguments("RequiredNumber"),
                expectedFixedCode,
                1); // Bulk fix: "Map all to default value"
    }

    [Fact]
    public async Task AM011_ShouldAddCustomLogicMapping()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public required string RequiredField { get; set; }
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

        const string expectedBulkFixedCode = """

                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public required string RequiredField { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredField, opt => opt.MapFrom(src => string.Empty));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(21, 13)
                    .WithArguments("RequiredField"),
                expectedBulkFixedCode,
                1); // Selects "Map all unmapped properties to default value"
    }

    [Fact]
    public async Task AM011_ShouldAddSourcePropertySuggestionComment()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public required string RequiredDescription { get; set; }
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

        const string expectedFixedCode = """

                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public required string RequiredDescription { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredDescription, opt => opt.MapFrom(src => string.Empty));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(21, 13)
                    .WithArguments("RequiredDescription"),
                expectedFixedCode,
                1); // Bulk fix: "Map all to default value"
    }

    [Fact]
    public async Task AM011_ShouldHandleMultipleRequiredProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public required string RequiredField1 { get; set; }
                                        public required string RequiredField2 { get; set; }
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

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public required string RequiredField1 { get; set; }
                                                 public required string RequiredField2 { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredField1, opt => opt.MapFrom(src => string.Empty)).ForMember(dest => dest.RequiredField2, opt => opt.MapFrom(src => string.Empty));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[]
                {
                    new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                        .WithLocation(21, 13)
                        .WithArguments("RequiredField1"),
                    new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                        .WithLocation(21, 13)
                        .WithArguments("RequiredField2")
                },
                expectedFixedCode,
                1, 1); // Selects Bulk Fix, expectation 1 iteration
    }

    [Fact]
    public async Task AM011_ShouldHandleRequiredBoolProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public required bool RequiredFlag { get; set; }
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

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public required bool RequiredFlag { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredFlag, opt => opt.MapFrom(src => false));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(20, 13)
                    .WithArguments("RequiredFlag"),
                expectedFixedCode,
                1); // Bulk fix: "Map all to default value"
    }

    [Fact]
    public async Task AM011_ShouldHandleRequiredDecimalProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public required decimal RequiredPrice { get; set; }
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

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public required decimal RequiredPrice { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredPrice, opt => opt.MapFrom(src => 0m));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(20, 13)
                    .WithArguments("RequiredPrice"),
                expectedFixedCode,
                1); // Bulk fix: "Map all to default value"
    }

    [Fact]
    public async Task AM011_BulkFix_ShouldNotAddForMember_WhenPropertyAlreadyConfiguredWithForCtorParam()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public required string RequiredByCtor { get; set; }
                                        public required string RequiredMissing { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForCtorParam(nameof(Destination.RequiredByCtor), opt => opt.MapFrom(src => src.Name));
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
        using AutoMapper;

        namespace TestNamespace
        {
            public class Source
            {
                public string Name { get; set; }
            }

            public class Destination
            {
                public required string RequiredByCtor { get; set; }
                public required string RequiredMissing { get; set; }
            }

            public class TestProfile : Profile
            {
                public TestProfile()
                {
                    CreateMap<Source, Destination>()
        .ForMember(dest => dest.RequiredMissing, opt => opt.MapFrom(src => string.Empty)).ForCtorParam(nameof(Destination.RequiredByCtor), opt => opt.MapFrom(src => src.Name));
                }
            }
        }
        """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(20, 13)
                    .WithArguments("RequiredMissing"),
                expectedFixedCode,
                1); // Bulk fix: "Map all unmapped properties to default value"
    }

    [Fact]
    public async Task AM011_BulkFix_ShouldProduceValidForMemberCalls_NotPlaceholder()
    {
        // This test verifies the fix for Bug 1: CreateForMemberCallExpression
        // previously returned placeholder() code in the chunked bulk fix path.
        // Even in the non-chunked path, we verify proper ForMember generation.
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public required string RequiredA { get; set; }
                                        public required int RequiredB { get; set; }
                                        public required bool RequiredC { get; set; }
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

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public required string RequiredA { get; set; }
                                                 public required int RequiredB { get; set; }
                                                 public required bool RequiredC { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredA, opt => opt.MapFrom(src => string.Empty)).ForMember(dest => dest.RequiredB, opt => opt.MapFrom(src => 0)).ForMember(dest => dest.RequiredC, opt => opt.MapFrom(src => false));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[]
                {
                    new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                        .WithLocation(22, 13)
                        .WithArguments("RequiredA"),
                    new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                        .WithLocation(22, 13)
                        .WithArguments("RequiredB"),
                    new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                        .WithLocation(22, 13)
                        .WithArguments("RequiredC")
                },
                expectedFixedCode,
                1, 1); // Selects Bulk Fix, 1 iteration
    }

    [Fact]
    public async Task AM011_BulkFix_ShouldIgnoreAllUnmappedRequiredProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public required string RequiredA { get; set; }
                                        public required int RequiredB { get; set; }
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

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public required string RequiredA { get; set; }
                                                 public required int RequiredB { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredA, opt => opt.Ignore()).ForMember(dest => dest.RequiredB, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[]
                {
                    new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                        .WithLocation(21, 13)
                        .WithArguments("RequiredA"),
                    new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                        .WithLocation(21, 13)
                        .WithArguments("RequiredB")
                },
                expectedFixedCode,
                3,
                1);
    }

    [Fact]
    public async Task AM011_BulkFix_ShouldCreateAllMissingSourceProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public required string RequiredTitle { get; set; }
                                        public required bool IsActive { get; set; }
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

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                                 public string RequiredTitle { get; set; }
                                                 public bool IsActive { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public required string RequiredTitle { get; set; }
                                                 public required bool IsActive { get; set; }
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

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[]
                {
                    new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                        .WithLocation(21, 13)
                        .WithArguments("IsActive"),
                    new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                        .WithLocation(21, 13)
                        .WithArguments("RequiredTitle")
                },
                expectedFixedCode,
                4,
                1);
    }

    [Theory]
    [InlineData(BulkFixAction.Todo)]
    [InlineData(BulkFixAction.Custom)]
    [InlineData(BulkFixAction.Nullable)]
    public void AM011_ApplyPropertyAction_ShouldFallbackToDefault_ForLegacyActions(BulkFixAction legacyAction)
    {
        var provider = new AM011_UnmappedRequiredPropertyCodeFixProvider();
        InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)SyntaxFactory
            .ParseExpression("CreateMap<Source, Destination>()");
        var propertyAction = new PropertyFixAction("RequiredField", "string", legacyAction);

        MethodInfo applyPropertyActionMethod =
            typeof(AM011_UnmappedRequiredPropertyCodeFixProvider).GetMethod(
                "ApplyPropertyAction",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

        var result = (InvocationExpressionSyntax)applyPropertyActionMethod.Invoke(
            provider,
            [invocation, propertyAction, Enumerable.Empty<IPropertySymbol>()])!;

        Assert.Contains("MapFrom(", result.ToString(), StringComparison.Ordinal);
    }

    private static Document CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);

        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, "AM011Tests", "AM011Tests", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview));

        string trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        foreach (string assemblyPath in trustedPlatformAssemblies.Split(Path.PathSeparator))
        {
            if (!string.IsNullOrWhiteSpace(assemblyPath))
            {
                solution = solution.AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyPath));
            }
        }

        solution = solution
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Profile).Assembly.Location))
            .AddDocument(documentId, "Test0.cs", SourceText.From(source));

        return solution.GetDocument(documentId)!;
    }

    private static async Task<List<CodeAction>> RegisterActionsAsync(Document document, Diagnostic diagnostic)
    {
        var actions = new List<CodeAction>();
        var provider = new AM011_UnmappedRequiredPropertyCodeFixProvider();

        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);
        return actions;
    }
}

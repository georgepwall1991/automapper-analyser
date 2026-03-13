using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AutoMapper;
using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

public class AM005_CodeFixIntegrationTests
{
    [Fact]
    public async Task AM005_ShouldApplyExplicitMappingCodeFix()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string firstName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string FirstName { get; set; }
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
                                                 public string firstName { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string FirstName { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.firstName));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM005_CaseSensitivityMismatchAnalyzer, AM005_CaseSensitivityMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule)
                    .WithLocation(19, 13)
                    .WithArguments("firstName", "FirstName"),
                expectedFixedCode,
                0);
    }

    [Fact]
    public async Task AM005_ShouldApplyExplicitMappingCodeFix_ForReverseMapDiagnostic()
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
                                        public string name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.name, opt => opt.MapFrom(src => src.Name))
                                                .ReverseMap();
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
                                                 public string name { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.name, opt => opt.MapFrom(src => src.Name))
                                                         .ReverseMap().ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.name));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM005_CaseSensitivityMismatchAnalyzer, AM005_CaseSensitivityMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule)
                    .WithLocation(19, 13)
                    .WithArguments("name", "Name"),
                expectedFixedCode,
                0);
    }

    [Fact]
    public async Task AM005_ShouldOfferOnlyExplicitMappingCodeFix_WhenSourcePropertyIsEditable()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string emailAddress { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string EmailAddress { get; set; }
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
                ImmutableArray.Create<DiagnosticAnalyzer>(new AM005_CaseSensitivityMismatchAnalyzer()))
            .GetAnalyzerDiagnosticsAsync())
            .Single();

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);
        CodeAction action = Assert.Single(actions);
        Assert.Equal("Map 'emailAddress' to 'EmailAddress' explicitly", action.Title);
    }

    private static Document CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);

        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, "AM005Tests", "AM005Tests", LanguageNames.CSharp)
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
        var provider = new AM005_CaseSensitivityMismatchCodeFixProvider();

        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);
        return actions;
    }
}

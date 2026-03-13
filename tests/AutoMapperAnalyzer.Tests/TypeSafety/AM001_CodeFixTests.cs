using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using AutoMapper;
using AutoMapperAnalyzer.Analyzers.TypeSafety;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.TypeSafety;

public class AM001_CodeFixTests
{
    private static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor, int line, int column,
        params object[] messageArgs)
    {
        return new DiagnosticResult(descriptor).WithLocation(line, column).WithArguments(messageArgs);
    }

    [Fact]
    public async Task AM001_ShouldFixPropertyTypeMismatchWithToString()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Age { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Age { get; set; }
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
                                         #nullable enable
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public int Age { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Age { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age.ToString()));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 20, 13,
                    "Age", "Source", "int", "Destination", "string"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM001_ShouldFixNumericConversionWithCast()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public double Score { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public float Score { get; set; }
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
                                                 public double Score { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public float Score { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Score, opt => opt.MapFrom(src => (float)src.Score));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 19, 13,
                    "Score", "Source", "double", "Destination", "float"),
                expectedFixedCode);
    }

    [Fact(Skip = "Analyzer limitation: expression tree patterns - see docs/TEST_LIMITATIONS.md #3")]
    public async Task AM001_ShouldFixStringToIntConversionWithParse()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Value { get; set; }
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
                                                 public string Value { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public int Value { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.Value != null ? int.Parse(src.Value) : 0));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 19, 13,
                    "Value", "Source", "string", "Destination", "int"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM001_ShouldFixMultiplePropertyTypeMismatches()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Age { get; set; }
                                        public double Score { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Age { get; set; }
                                        public string Score { get; set; }
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
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(document);
        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostics);

        Assert.Collection(
            actions.Select(action => action.Title),
            title => Assert.Equal("Map 'Age' with conversion", title),
            title => Assert.Equal("Ignore property 'Age'", title),
            title => Assert.Equal("Map 'Score' with conversion", title),
            title => Assert.Equal("Ignore property 'Score'", title));

        string updatedCode = await ApplyActionAsync(actions[0], document);
        Assert.Contains(".ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age.ToString()))", updatedCode,
            StringComparison.Ordinal);
        Assert.DoesNotContain("dest => dest.Score", updatedCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AM001_ShouldFixSecondPropertyTypeMismatch_WhenMultipleDiagnosticsExist()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Age { get; set; }
                                        public double Score { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Age { get; set; }
                                        public string Score { get; set; }
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
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(document);
        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostics);

        CodeAction scoreAction = Assert.Single(
            actions,
            action => action.Title == "Map 'Score' with conversion");
        string updatedCode = await ApplyActionAsync(scoreAction, document);

        Assert.Contains(".ForMember(dest => dest.Score, opt => opt.MapFrom(src => src.Score.ToString()))", updatedCode,
            StringComparison.Ordinal);
        Assert.DoesNotContain("dest => dest.Age", updatedCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AM001_ShouldApplyIterativeFixes_ForMultiplePropertyTypeMismatches()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Age { get; set; }
                                        public double Score { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Age { get; set; }
                                        public string Score { get; set; }
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
                                                 public int Age { get; set; }
                                                 public double Score { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Age { get; set; }
                                                 public string Score { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Score, opt => opt.MapFrom(src => src.Score.ToString())).ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age.ToString()));
                                                 }
                                             }
                                         }
                                         """;

        DiagnosticResult[] diagnostics =
        [
            Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 21, 13,
                "Age", "Source", "int", "Destination", "string"),
            Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 21, 13,
                "Score", "Source", "double", "Destination", "string")
        ];

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixWithIterationsAsync(testCode, diagnostics, expectedFixedCode, iterations: 2);
    }

    [Fact]
    public async Task AM001_ShouldFixNullableStringToIntWithParsePattern()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string? Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Value { get; set; }
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
                                         #nullable enable
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string? Value { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public int Value { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.Value != null ? int.Parse(src.Value) : 0));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 20, 13,
                    "Value", "Source", "string?", "Destination", "int"),
                expectedFixedCode);
    }

    private static Document CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);

        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, "AM001Tests", "AM001Tests", LanguageNames.CSharp)
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

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(Document document)
    {
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        return (await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new AM001_PropertyTypeMismatchAnalyzer()))
            .GetAnalyzerDiagnosticsAsync())
            .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .ThenBy(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static async Task<List<CodeAction>> RegisterActionsAsync(Document document, ImmutableArray<Diagnostic> diagnostics)
    {
        var actions = new List<CodeAction>();
        var provider = new AM001_PropertyTypeMismatchCodeFixProvider();

        var context = new CodeFixContext(
            document,
            diagnostics[0].Location.SourceSpan,
            diagnostics,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);
        return actions;
    }

    private static async Task<string> ApplyActionAsync(CodeAction action, Document originalDocument)
    {
        ImmutableArray<CodeActionOperation> operations = await action.GetOperationsAsync(CancellationToken.None);
        ApplyChangesOperation applyChanges = Assert.IsType<ApplyChangesOperation>(operations.Single());

        Document updatedDocument = applyChanges.ChangedSolution.GetDocument(originalDocument.Id)!;
        SourceText updatedText = await updatedDocument.GetTextAsync();
        return updatedText.ToString();
    }
}

using System.Collections.Immutable;
using System.IO;
using AutoMapper;
using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

public class DataIntegrityCodeActionUxTests
{
    [Fact]
    public async Task AM004_ShouldRegisterManualReviewDoNotValidateTitleAndEquivalenceKey()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string UnusedProperty { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
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
        Diagnostic diagnostic = await GetSingleDiagnosticAsync<AM004_MissingDestinationPropertyAnalyzer>(document);

        List<CodeAction> actions = await RegisterActionsAsync(
            document,
            new AM004_MissingDestinationPropertyCodeFixProvider(),
            diagnostic);

        CodeAction action = Assert.Single(actions);
        Assert.Equal(
            "Suppress source validation for 'UnusedProperty' with DoNotValidate() (manual review)",
            action.Title);
        Assert.Equal("AM004_DoNotValidate_UnusedProperty", action.EquivalenceKey);
    }

    [Fact]
    public async Task AM006_ShouldRegisterManualReviewIgnoreTitleAndEquivalenceKey()
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
                                        public string ExtraInfo { get; set; }
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
        Diagnostic diagnostic = await GetSingleDiagnosticAsync<AM006_UnmappedDestinationPropertyAnalyzer>(document);

        List<CodeAction> actions = await RegisterActionsAsync(
            document,
            new AM006_UnmappedDestinationPropertyCodeFixProvider(),
            diagnostic);

        CodeAction action = Assert.Single(actions);
        Assert.Equal("Ignore destination property 'ExtraInfo' (manual review)", action.Title);
        Assert.Equal("AM006_Ignore_ExtraInfo", action.EquivalenceKey);
    }

    private static Document CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);

        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, "DataIntegrityCodeActionUxTests", "DataIntegrityCodeActionUxTests", LanguageNames.CSharp)
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

    private static async Task<Diagnostic> GetSingleDiagnosticAsync<TAnalyzer>(Document document)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        ImmutableArray<Diagnostic> diagnostics = await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new TAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        return Assert.Single(diagnostics);
    }

    private static async Task<List<CodeAction>> RegisterActionsAsync(
        Document document,
        CodeFixProvider provider,
        Diagnostic diagnostic)
    {
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);
        return actions;
    }
}

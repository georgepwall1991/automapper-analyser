using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

/// <summary>
///     Same-document property-token AM004 diagnostics only put one diagnostic in the IDE
///     CodeFixContext. Aggregates must recompute siblings (like AM011), not require multi-diag pile-up.
/// </summary>
public class AM004_SameDocumentSiblingAggregateTests
{
    private const string TwoUnmappedSource = """
                                             using AutoMapper;

                                             namespace TestNamespace
                                             {
                                                 public class Source
                                                 {
                                                     public string Name { get; set; }
                                                     public string Unused1 { get; set; }
                                                     public string Unused2 { get; set; }
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

    [Fact]
    public async Task AM004_SinglePropertyTokenCaret_OffersDoNotValidateAll_ForSameDocumentSiblings()
    {
        Document document = CreateDocument(TwoUnmappedSource);
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(document);
        Assert.Equal(2, diagnostics.Length);

        // One property-token caret only — siblings must be recomputed.
        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions =
            await CodeFixActionInspector.GetActionsAsync(
                document,
                new AM004_MissingDestinationPropertyCodeFixProvider(),
                diagnostics[0]);

        Assert.Contains(actions, a => a.EquivalenceKey == "AM004_DoNotValidateAll" && a.Depth == 0);
        Assert.Contains(
            actions,
            a => a.Depth == 0 && a.HasChildren &&
                 a.Title.Contains("individual", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, a => a.IsNested && a.EquivalenceKey != null &&
                                      a.EquivalenceKey.StartsWith("AM004_DoNotValidate_", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AM004_DoNotValidateAll_FromSingleCaret_SuppressesEverySibling()
    {
        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                                 public string Unused1 { get; set; }
                                                 public string Unused2 { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForSourceMember(src => src.Unused1, opt => opt.DoNotValidate()).ForSourceMember(src => src.Unused2, opt => opt.DoNotValidate());
                                                 }
                                             }
                                         }
                                         """;

        Document document = CreateDocument(TwoUnmappedSource);
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(document);
        Assert.Equal(2, diagnostics.Length);

        Document fixedDocument = await CodeFixActionInspector.ApplyActionByKeyAsync(
            document,
            new AM004_MissingDestinationPropertyCodeFixProvider(),
            ImmutableArray.Create(diagnostics[0]),
            "AM004_DoNotValidateAll");

        string updated = (await fixedDocument.GetTextAsync()).ToString();
        Assert.Equal(
            Normalize(expectedFixedCode),
            Normalize(updated));
        Assert.Empty(await GetDiagnosticsAsync(fixedDocument));
    }

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

    private static Document CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);
        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, "AM004Sibling", "AM004Sibling", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview));

        string trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        foreach (string path in trusted.Split(Path.PathSeparator))
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                solution = solution.AddMetadataReference(projectId, MetadataReference.CreateFromFile(path));
            }
        }

        solution = solution
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(AutoMapper.Profile).Assembly.Location))
            .AddDocument(documentId, "Test0.cs", SourceText.From(source));
        return solution.GetDocument(documentId)!;
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(Document document)
    {
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        return (await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new AM004_MissingDestinationPropertyAnalyzer()))
            .GetAnalyzerDiagnosticsAsync())
            .OrderBy(d => d.Location.SourceSpan.Start)
            .ToImmutableArray();
    }
}

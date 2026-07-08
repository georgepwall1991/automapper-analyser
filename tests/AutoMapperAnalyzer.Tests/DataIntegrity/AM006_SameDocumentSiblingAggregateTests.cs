using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

/// <summary>
///     Same-document property-token AM006 diagnostics only put one diagnostic in the IDE
///     CodeFixContext. Aggregates must recompute siblings (like AM011), not require multi-diag pile-up.
/// </summary>
public class AM006_SameDocumentSiblingAggregateTests
{
    private const string TwoUnmappedDestination = """
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
                                                          public string Extra1 { get; set; }
                                                          public string Extra2 { get; set; }
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
    public async Task AM006_SinglePropertyTokenCaret_OffersIgnoreAll_ForSameDocumentSiblings()
    {
        Document document = CreateDocument(TwoUnmappedDestination);
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(document);
        Assert.Equal(2, diagnostics.Length);

        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions =
            await CodeFixActionInspector.GetActionsAsync(
                document,
                new AM006_UnmappedDestinationPropertyCodeFixProvider(),
                diagnostics[0]);

        Assert.Contains(actions, a => a.EquivalenceKey == "AM006_IgnoreAll" && a.Depth == 0);
        Assert.Contains(
            actions,
            a => a.Depth == 0 && a.HasChildren &&
                 a.Title.Contains("individual", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, a => a.IsNested && a.EquivalenceKey != null &&
                                      a.EquivalenceKey.StartsWith("AM006_Ignore_", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AM006_IgnoreAll_FromSingleCaret_IgnoresEverySibling()
    {
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
                                                 public string Extra1 { get; set; }
                                                 public string Extra2 { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Extra1, opt => opt.Ignore()).ForMember(dest => dest.Extra2, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        Document document = CreateDocument(TwoUnmappedDestination);
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(document);
        Assert.Equal(2, diagnostics.Length);

        Document fixedDocument = await CodeFixActionInspector.ApplyActionByKeyAsync(
            document,
            new AM006_UnmappedDestinationPropertyCodeFixProvider(),
            ImmutableArray.Create(diagnostics[0]),
            "AM006_IgnoreAll");

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
            .AddProject(projectId, "AM006Sibling", "AM006Sibling", LanguageNames.CSharp)
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
                ImmutableArray.Create<DiagnosticAnalyzer>(new AM006_UnmappedDestinationPropertyAnalyzer()))
            .GetAnalyzerDiagnosticsAsync())
            .OrderBy(d => d.Location.SourceSpan.Start)
            .ToImmutableArray();
    }
}

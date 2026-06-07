using System.Collections.Immutable;
using System.IO;
using AutoMapper;
using AutoMapperAnalyzer.Analyzers.ComplexMappings;
using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Tests.Infrastructure;

/// <summary>
///     Meta-tests for the code-fix test harness helpers added in the fixer item-picker redesign
///     (Phase 0). They exercise the new <c>VerifyFixByKeyAsync</c> equivalence-key selection path and
///     the <see cref="CodeFixActionInspector"/> flattening/top-level-count helpers against existing,
///     fully-verified AM005 behaviour so the helpers themselves are proven before later phases rely on
///     them to select aggregate / nested actions by key.
/// </summary>
public class CodeFixHarnessTests
{
    private const string FirstNameSource = """
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

    private const string FirstNameFixed = """
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

    private const string EmailSource = """
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

    [Fact]
    public async Task VerifyFixByKeyAsync_AppliesActionSelectedByEquivalenceKey()
    {
        await CodeFixVerifier<AM005_CaseSensitivityMismatchAnalyzer, AM005_CaseSensitivityMismatchCodeFixProvider>
            .VerifyFixByKeyAsync(
                FirstNameSource,
                new DiagnosticResult(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule)
                    .WithLocation(19, 13)
                    .WithArguments("firstName", "FirstName"),
                FirstNameFixed,
                "AM005_ExplicitMapping_firstName_FirstName");
    }

    [Fact]
    public async Task VerifyFixByKeyAsync_FixesAllSameKeyDiagnostics_WithoutExplicitIterations()
    {
        // Two self-referencing maps => two AM022 diagnostics, both offering the category-level
        // "AM022_AddMaxDepth" action. Selecting by that key with the DEFAULT iteration count must fix
        // BOTH maps; if the default ignored expectedDiagnostics.Length it would fix only the first.
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class NodeA
                                    {
                                        public string Name { get; set; }
                                        public NodeA Parent { get; set; }
                                    }

                                    public class NodeADto
                                    {
                                        public string Name { get; set; }
                                        public NodeADto Parent { get; set; }
                                    }

                                    public class NodeB
                                    {
                                        public string Name { get; set; }
                                        public NodeB Parent { get; set; }
                                    }

                                    public class NodeBDto
                                    {
                                        public string Name { get; set; }
                                        public NodeBDto Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<NodeA, NodeADto>();
                                            CreateMap<NodeB, NodeBDto>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class NodeA
                                             {
                                                 public string Name { get; set; }
                                                 public NodeA Parent { get; set; }
                                             }

                                             public class NodeADto
                                             {
                                                 public string Name { get; set; }
                                                 public NodeADto Parent { get; set; }
                                             }

                                             public class NodeB
                                             {
                                                 public string Name { get; set; }
                                                 public NodeB Parent { get; set; }
                                             }

                                             public class NodeBDto
                                             {
                                                 public string Name { get; set; }
                                                 public NodeBDto Parent { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<NodeA, NodeADto>().MaxDepth(2);
                                                     CreateMap<NodeB, NodeBDto>().MaxDepth(2);
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixByKeyAsync(
                testCode,
                new[]
                {
                    new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                        .WithLocation(33, 13)
                        .WithArguments("NodeA", "NodeADto"),
                    new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                        .WithLocation(34, 13)
                        .WithArguments("NodeB", "NodeBDto")
                },
                expectedFixedCode,
                "AM022_AddMaxDepth");
    }

    [Fact]
    public async Task Inspector_FlattensActions_AndCountsTopLevel()
    {
        Document document = CreateDocument(EmailSource);
        Diagnostic diagnostic = await GetSingleDiagnosticAsync<AM005_CaseSensitivityMismatchAnalyzer>(document);

        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions =
            await CodeFixActionInspector.GetActionsAsync(
                document,
                new AM005_CaseSensitivityMismatchCodeFixProvider(),
                diagnostic);

        Assert.Equal(1, CodeFixActionInspector.TopLevelCount(actions));
        Assert.Contains(
            actions,
            a => a.EquivalenceKey == "AM005_ExplicitMapping_emailAddress_EmailAddress"
                 && !a.IsNested
                 && !a.HasChildren
                 && a.Depth == 0);
    }

    [Fact]
    public async Task Inspector_FlattensNestedActions_WithDepthAndTopLevelCount()
    {
        Document document = CreateDocument(EmailSource);

        // A grouped per-property action (the shape later phases will register): one top-level parent
        // with two nested children. The inspector must report ONE top-level entry and surface both
        // children at depth 1 with IsNested = true so menu-length / key assertions work on sub-menus.
        CodeAction child1 = CodeAction.Create(
            "Map 'emailAddress' to 'EmailAddress' explicitly",
            _ => Task.FromResult(document),
            "AM005_ExplicitMapping_emailAddress_EmailAddress");
        CodeAction child2 = CodeAction.Create(
            "Ignore destination property 'EmailAddress'",
            _ => Task.FromResult(document),
            "AM005_Ignore_EmailAddress");
        CodeAction parent = CodeAction.Create(
            "Fix property 'EmailAddress'…",
            ImmutableArray.Create(child1, child2),
            isInlinable: false);

        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions =
            CodeFixActionInspector.Flatten(new[] { parent });

        Assert.Equal(1, CodeFixActionInspector.TopLevelCount(actions));
        Assert.Contains(
            actions,
            a => a.Title == "Fix property 'EmailAddress'…" && a.Depth == 0 && !a.IsNested && a.HasChildren);
        Assert.Contains(
            actions,
            a => a.EquivalenceKey == "AM005_ExplicitMapping_emailAddress_EmailAddress"
                 && a.Depth == 1 && a.IsNested && !a.HasChildren);
        Assert.Contains(
            actions,
            a => a.EquivalenceKey == "AM005_Ignore_EmailAddress"
                 && a.Depth == 1 && a.IsNested && !a.HasChildren);
    }

    private static Document CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);

        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, "CodeFixHarnessTests", "CodeFixHarnessTests", LanguageNames.CSharp)
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
        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new TAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        return Assert.Single(diagnostics);
    }
}

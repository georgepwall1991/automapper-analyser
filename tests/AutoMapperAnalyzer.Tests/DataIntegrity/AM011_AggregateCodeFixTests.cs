using System.Collections.Immutable;
using System.IO;
using AutoMapper;
using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

/// <summary>
///     Phase 1 of the fixer item-picker redesign: aggregate "Map all / Ignore all" actions for AM011.
///     A single aggregate action fixes every unmapped required property of one CreateMap in one edit,
///     replacing the 2-clicks-per-property workflow. Offered only when 2+ properties are unmapped (the
///     single-property case stays a flat per-property choice). Aggregate actions are multi-diagnostic, so
///     they are exercised via <see cref="CodeFixActionInspector.ApplyActionByKeyAsync"/> (which registers
///     the full diagnostic set the way the IDE does) rather than the per-diagnostic code-fix verifier.
/// </summary>
public class AM011_AggregateCodeFixTests
{
    private const string ThreeRequiredSource = """
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
                                                       public required string Alpha { get; set; }
                                                       public required string Beta { get; set; }
                                                       public required string Gamma { get; set; }
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
    public async Task AM011_IgnoreAll_IgnoresEveryRequiredProperty_InOneEdit()
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
                                                 public required string Alpha { get; set; }
                                                 public required string Beta { get; set; }
                                                 public required string Gamma { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Alpha, opt => opt.Ignore()).ForMember(dest => dest.Beta, opt => opt.Ignore()).ForMember(dest => dest.Gamma, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        await AssertAggregateClearsAllAsync(ThreeRequiredSource, "AM011_IgnoreAll", expectedFixedCode);
    }

    [Fact]
    public async Task AM011_ScaffoldAll_ScaffoldsEveryRequiredProperty_InOneEdit_WhenNoFuzzyMatches()
    {
        // No unique fuzzy source matches → honest Scaffold-all (not "Map all") injects defaults.
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
                                                 public required string Alpha { get; set; }
                                                 public required string Beta { get; set; }
                                                 public required string Gamma { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Alpha, opt => opt.MapFrom(src => string.Empty)).ForMember(dest => dest.Beta, opt => opt.MapFrom(src => string.Empty)).ForMember(dest => dest.Gamma, opt => opt.MapFrom(src => string.Empty));
                                                 }
                                             }
                                         }
                                         """;

        await AssertAggregateClearsAllAsync(ThreeRequiredSource, "AM011_ScaffoldAll", expectedFixedCode);
    }

    [Fact]
    public async Task AM011_IgnoreAll_HandlesReverseMapDirection_InOneEdit()
    {
        // Forward A -> B has no required dest members; the reverse map B -> A leaves A's two required
        // members unmapped, so both AM011 diagnostics anchor to the ReverseMap() node. The aggregate
        // must resolve the forward CreateMap's (swapped) types and fold ForMember onto ReverseMap().
        const string reverseSource = """
                                     using AutoMapper;

                                     namespace TestNamespace
                                     {
                                         public class A
                                         {
                                             public string Name { get; set; }
                                             public required string RevAlpha { get; set; }
                                             public required string RevBeta { get; set; }
                                         }

                                         public class B
                                         {
                                             public string Name { get; set; }
                                         }

                                         public class TestProfile : Profile
                                         {
                                             public TestProfile()
                                             {
                                                 CreateMap<A, B>().ReverseMap();
                                             }
                                         }
                                     }
                                     """;

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class A
                                             {
                                                 public string Name { get; set; }
                                                 public required string RevAlpha { get; set; }
                                                 public required string RevBeta { get; set; }
                                             }

                                             public class B
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<A, B>().ReverseMap().ForMember(dest => dest.RevAlpha, opt => opt.Ignore()).ForMember(dest => dest.RevBeta, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        await AssertAggregateClearsAllAsync(reverseSource, "AM011_IgnoreAll", expectedFixedCode);
    }

    [Fact]
    public async Task AM011_ScaffoldAll_EscapesKeywordSourceIdentifiers_WhenMixedFuzzyAndDefaults()
    {
        // Mixed fuzzy + default → Scaffold-all (not dishonest "Map all"). 'Events' → src.@event; Status → default.
        const string keywordSource = """
                                     using AutoMapper;

                                     namespace TestNamespace
                                     {
                                         public class Source
                                         {
                                             public string @event { get; set; }
                                         }

                                         public class Destination
                                         {
                                             public required string Events { get; set; }
                                             public required string Status { get; set; }
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
                                                 public string @event { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public required string Events { get; set; }
                                                 public required string Status { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Events, opt => opt.MapFrom(src => src.@event)).ForMember(dest => dest.Status, opt => opt.MapFrom(src => string.Empty));
                                                 }
                                             }
                                         }
                                         """;

        await AssertAggregateClearsAllAsync(keywordSource, "AM011_ScaffoldAll", expectedFixedCode);
    }

    [Fact]
    public async Task AM011_MapAll_OnlyWhenEveryRequiredPropertyHasUniqueFuzzyMatch()
    {
        const string allFuzzySource = """
                                      using AutoMapper;

                                      namespace TestNamespace
                                      {
                                          public class Source
                                          {
                                              public string FirstNam { get; set; }
                                              public string LastNam { get; set; }
                                          }

                                          public class Destination
                                          {
                                              public required string FirstName { get; set; }
                                              public required string LastName { get; set; }
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
                                                 public string FirstNam { get; set; }
                                                 public string LastNam { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public required string FirstName { get; set; }
                                                 public required string LastName { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstNam)).ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.LastNam));
                                                 }
                                             }
                                         }
                                         """;

        Document document = CreateDocument(allFuzzySource);
        ImmutableArray<Diagnostic> diagnostics =
            await GetDiagnosticsAsync<AM011_UnmappedRequiredPropertyAnalyzer>(document);
        Assert.Equal(2, diagnostics.Length);

        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions =
            await CodeFixActionInspector.GetActionsAsync(
                document,
                new AM011_UnmappedRequiredPropertyCodeFixProvider(),
                diagnostics[0]);

        Assert.Contains(actions, a => a.EquivalenceKey == "AM011_MapAll" && a.Depth == 0);
        Assert.DoesNotContain(actions, a => a.EquivalenceKey == "AM011_ScaffoldAll");
        Assert.Contains(actions, a => a.EquivalenceKey == "AM011_IgnoreAll" && a.Depth == 0);

        await AssertAggregateClearsAllAsync(allFuzzySource, "AM011_MapAll", expectedFixedCode);
    }

    [Fact]
    public async Task AM011_MultipleRequiredProperties_OffersAggregateActionsAtTopLevel()
    {
        Document document = CreateDocument(ThreeRequiredSource);
        ImmutableArray<Diagnostic> diagnostics =
            await GetDiagnosticsAsync<AM011_UnmappedRequiredPropertyAnalyzer>(document);
        Assert.Equal(3, diagnostics.Length);

        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions =
            await CodeFixActionInspector.GetActionsAsync(
                document,
                new AM011_UnmappedRequiredPropertyCodeFixProvider(),
                diagnostics[0]);

        // No fuzzy matches → Scaffold-all (honest), not Map-all.
        Assert.Contains(actions, a => a.EquivalenceKey == "AM011_ScaffoldAll" && a.Depth == 0);
        Assert.DoesNotContain(actions, a => a.EquivalenceKey == "AM011_MapAll");
        Assert.Contains(actions, a => a.EquivalenceKey == "AM011_IgnoreAll" && a.Depth == 0);
        Assert.Contains(
            actions,
            a => a.EquivalenceKey == "AM011_IgnoreAll" &&
                 a.Title.Contains("manual review", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            actions,
            a => a.EquivalenceKey == "AM011_ScaffoldAll" &&
                 a.Title.Contains("manual review", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AM011_MultipleRequiredProperties_NestPerPropertyFixes_KeepingThreeTopLevelItems()
    {
        Document document = CreateDocument(ThreeRequiredSource);
        ImmutableArray<Diagnostic> diagnostics =
            await GetDiagnosticsAsync<AM011_UnmappedRequiredPropertyAnalyzer>(document);
        Assert.Equal(3, diagnostics.Length);

        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions =
            await CodeFixActionInspector.GetActionsAsync(
                document,
                new AM011_UnmappedRequiredPropertyCodeFixProvider(),
                diagnostics[0]);

        // Scaffold all + Ignore all + a single "Fix individual…" parent = 3 top-level entries (not 2 + 6 flat).
        Assert.Equal(3, CodeFixActionInspector.TopLevelCount(actions));
        Assert.Contains(actions, a => a.Depth == 0 && a.EquivalenceKey == "AM011_ScaffoldAll");
        Assert.Contains(actions, a => a.Depth == 0 && a.EquivalenceKey == "AM011_IgnoreAll");
        Assert.DoesNotContain(actions, a => a.EquivalenceKey == "AM011_MapAll");
        Assert.Contains(
            actions,
            a => a.Depth == 0 && a.HasChildren && a.Title.Contains("individual", StringComparison.OrdinalIgnoreCase));

        // Per-property fixes are nested under the parent, never duplicated at the top level.
        Assert.DoesNotContain(
            actions,
            a => a.Depth == 0 &&
                 !a.HasChildren &&
                 a.EquivalenceKey != null &&
                 a.EquivalenceKey.StartsWith("AM011_Ignore_", StringComparison.Ordinal));
        Assert.Contains(actions, a => a.IsNested && a.EquivalenceKey == "AM011_Ignore_Alpha");
        Assert.Contains(actions, a => a.IsNested && a.EquivalenceKey == "AM011_Ignore_Beta");
        Assert.Contains(actions, a => a.IsNested && a.EquivalenceKey == "AM011_Ignore_Gamma");
    }

    [Fact]
    public async Task AM011_SingleRequiredProperty_DoesNotOfferAggregateActions()
    {
        const string singleRequiredSource = """
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
                                                    public required string Alpha { get; set; }
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

        Document document = CreateDocument(singleRequiredSource);
        ImmutableArray<Diagnostic> diagnostics =
            await GetDiagnosticsAsync<AM011_UnmappedRequiredPropertyAnalyzer>(document);
        Diagnostic single = Assert.Single(diagnostics);

        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions =
            await CodeFixActionInspector.GetActionsAsync(
                document,
                new AM011_UnmappedRequiredPropertyCodeFixProvider(),
                single);

        Assert.DoesNotContain(actions, a => a.EquivalenceKey is "AM011_MapAll" or "AM011_ScaffoldAll" or "AM011_IgnoreAll");
        CodeFixActionInspector.ActionInfo action = Assert.Single(actions);
        Assert.Equal("AM011_Ignore_Alpha", action.EquivalenceKey);
        Assert.Contains("manual review", action.Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(actions, item =>
            item.EquivalenceKey != null &&
            item.EquivalenceKey.StartsWith("AM011_DefaultValue_", StringComparison.Ordinal));
    }

    private static async Task AssertAggregateClearsAllAsync(
        string source, string equivalenceKey, string expectedFixedCode)
    {
        Document document = CreateDocument(source);
        ImmutableArray<Diagnostic> diagnostics =
            await GetDiagnosticsAsync<AM011_UnmappedRequiredPropertyAnalyzer>(document);
        Assert.True(diagnostics.Length >= 2, $"Expected 2+ diagnostics, got {diagnostics.Length}.");

        Document fixedDocument = await CodeFixActionInspector.ApplyActionByKeyAsync(
            document,
            new AM011_UnmappedRequiredPropertyCodeFixProvider(),
            ImmutableArray.Create(diagnostics[0]),
            equivalenceKey);

        string actualFixedCode = (await fixedDocument.GetTextAsync()).ToString();
        Assert.Equal(Normalize(expectedFixedCode), Normalize(actualFixedCode));

        // One aggregate edit clears every diagnostic.
        Assert.Empty(await GetDiagnosticsAsync<AM011_UnmappedRequiredPropertyAnalyzer>(fixedDocument));
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n");

    private static Document CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);

        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, "AM011AggregateTests", "AM011AggregateTests", LanguageNames.CSharp)
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

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync<TAnalyzer>(Document document)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        return await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new TAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
    }
}

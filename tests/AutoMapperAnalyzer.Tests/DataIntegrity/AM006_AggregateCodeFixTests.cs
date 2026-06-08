using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

/// <summary>
///     Aggregate "Map all / Ignore all" + nested "Fix individual destination property…" coverage for AM006.
///     AM006 is property-anchored, so the pile-up (and therefore the aggregate) only occurs for metadata
///     (compiled) model types, where every diagnostic falls back to the CreateMap invocation. These tests
///     reference the models from a compiled assembly to reproduce that. "Map all" is only offered when every
///     flagged property has a unique fuzzy source match; "Ignore all" is always available for a 2+ map.
/// </summary>
public class AM006_AggregateCodeFixTests
{
    private const string Project = "AM006AggregateTests";

    private const string NoMatchModels = """
                                         namespace Models
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
                                         }
                                         """;

    private const string FuzzyModels = """
                                       namespace Models
                                       {
                                           public class Source
                                           {
                                               public string FirstName { get; set; }
                                               public string LastName { get; set; }
                                           }

                                           public class Destination
                                           {
                                               public string FirstNam { get; set; }
                                               public string LastNam { get; set; }
                                           }
                                       }
                                       """;

    private const string Profile = """
                                   using AutoMapper;
                                   using Models;

                                   namespace TestNamespace
                                   {
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
    public async Task AM006_IgnoreAll_IgnoresEveryUnmappedDestination_InOneEdit()
    {
        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using Models;

                                         namespace TestNamespace
                                         {
                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Extra1, opt => opt.Ignore()).ForMember(dest => dest.Extra2, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        Document document = AggregateFixTestHarness.CreateDocumentWithMetadataModels(Profile, NoMatchModels, Project);
        await AggregateFixTestHarness
            .AssertAggregateClearsAllAsync<AM006_UnmappedDestinationPropertyAnalyzer, AM006_UnmappedDestinationPropertyCodeFixProvider>(
                document, "AM006_IgnoreAll", expectedFixedCode);
    }

    [Fact]
    public async Task AM006_MapAll_MapsEveryFuzzyMatch_InOneEdit()
    {
        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using Models;

                                         namespace TestNamespace
                                         {
                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.FirstNam, opt => opt.MapFrom(src => src.FirstName)).ForMember(dest => dest.LastNam, opt => opt.MapFrom(src => src.LastName));
                                                 }
                                             }
                                         }
                                         """;

        Document document = AggregateFixTestHarness.CreateDocumentWithMetadataModels(Profile, FuzzyModels, Project);
        await AggregateFixTestHarness
            .AssertAggregateClearsAllAsync<AM006_UnmappedDestinationPropertyAnalyzer, AM006_UnmappedDestinationPropertyCodeFixProvider>(
                document, "AM006_MapAll", expectedFixedCode);
    }

    [Fact]
    public async Task AM006_IgnoreAll_HandlesReverseMapDirection_InOneEdit()
    {
        // Reverse map B -> A leaves two of A's destination members unmapped; both diagnostics anchor to the
        // ReverseMap() node. The aggregate must resolve the forward CreateMap's swapped types and fold onto
        // ReverseMap() rather than returning an empty action set.
        const string reverseModels = """
                                     namespace Models
                                     {
                                         public class A
                                         {
                                             public string X { get; set; }
                                             public string RevExtra1 { get; set; }
                                             public string RevExtra2 { get; set; }
                                         }

                                         public class B
                                         {
                                             public string X { get; set; }
                                         }
                                     }
                                     """;

        const string reverseProfile = """
                                      using AutoMapper;
                                      using Models;

                                      namespace TestNamespace
                                      {
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
                                         using Models;

                                         namespace TestNamespace
                                         {
                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<A, B>().ReverseMap().ForMember(dest => dest.RevExtra1, opt => opt.Ignore()).ForMember(dest => dest.RevExtra2, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        Document document = AggregateFixTestHarness.CreateDocumentWithMetadataModels(reverseProfile, reverseModels, Project);
        await AggregateFixTestHarness
            .AssertAggregateClearsAllAsync<AM006_UnmappedDestinationPropertyAnalyzer, AM006_UnmappedDestinationPropertyCodeFixProvider>(
                document, "AM006_IgnoreAll", expectedFixedCode);
    }

    [Fact]
    public async Task AM006_TwoUnmapped_NoFuzzy_OffersIgnoreAllAndNestedSubmenu_NoMapAll()
    {
        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions = await RegisterActionsAsync(NoMatchModels);

        Assert.Equal(2, CodeFixActionInspector.TopLevelCount(actions));
        Assert.Contains(actions, a => a.Depth == 0 && a.EquivalenceKey == "AM006_IgnoreAll");
        Assert.DoesNotContain(actions, a => a.EquivalenceKey == "AM006_MapAll");
        Assert.Contains(
            actions,
            a => a.Depth == 0 && a.HasChildren && a.Title.Contains("individual", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, a => a.IsNested && a.EquivalenceKey == "AM006_Ignore_Extra1");
    }

    [Fact]
    public async Task AM006_TwoUnmapped_Fuzzy_OffersMapAllIgnoreAllAndNestedSubmenu()
    {
        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions = await RegisterActionsAsync(FuzzyModels);

        Assert.Equal(3, CodeFixActionInspector.TopLevelCount(actions));
        Assert.Contains(actions, a => a.Depth == 0 && a.EquivalenceKey == "AM006_MapAll");
        Assert.Contains(actions, a => a.Depth == 0 && a.EquivalenceKey == "AM006_IgnoreAll");
        Assert.Contains(
            actions,
            a => a.Depth == 0 && a.HasChildren && a.Title.Contains("individual", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            actions,
            a => a.Depth == 0 && a.EquivalenceKey != null && a.EquivalenceKey.StartsWith("AM006_Ignore_", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AM006_SingleUnmapped_StaysFlat_NoAggregate()
    {
        const string singleModels = """
                                    namespace Models
                                    {
                                        public class Source
                                        {
                                            public string Name { get; set; }
                                        }

                                        public class Destination
                                        {
                                            public string Name { get; set; }
                                            public string Extra1 { get; set; }
                                        }
                                    }
                                    """;

        Document document = AggregateFixTestHarness.CreateDocumentWithMetadataModels(Profile, singleModels, Project);
        ImmutableArray<Diagnostic> diagnostics =
            await AggregateFixTestHarness.GetDiagnosticsAsync<AM006_UnmappedDestinationPropertyAnalyzer>(document);
        Diagnostic single = Assert.Single(diagnostics);

        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions = await CodeFixActionInspector.GetActionsAsync(
            document, new AM006_UnmappedDestinationPropertyCodeFixProvider(), single);

        Assert.DoesNotContain(actions, a => a.EquivalenceKey is "AM006_MapAll" or "AM006_IgnoreAll");
        Assert.Equal(1, CodeFixActionInspector.TopLevelCount(actions));
    }

    private static async Task<IReadOnlyList<CodeFixActionInspector.ActionInfo>> RegisterActionsAsync(string modelsSource)
    {
        Document document = AggregateFixTestHarness.CreateDocumentWithMetadataModels(Profile, modelsSource, Project);
        ImmutableArray<Diagnostic> diagnostics =
            await AggregateFixTestHarness.GetDiagnosticsAsync<AM006_UnmappedDestinationPropertyAnalyzer>(document);
        Assert.Equal(2, diagnostics.Length);

        return await CodeFixActionInspector.GetActionsAsync(
            document, new AM006_UnmappedDestinationPropertyCodeFixProvider(), diagnostics);
    }
}

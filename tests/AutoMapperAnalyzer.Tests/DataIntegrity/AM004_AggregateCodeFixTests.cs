using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

/// <summary>
///     Aggregate "Map all / DoNotValidate all" + nested "Fix individual source property…" coverage for AM004
///     (source properties with no destination). Like AM006, the pile-up only occurs for metadata (compiled)
///     model types, where every diagnostic falls back to the CreateMap invocation, so these tests reference
///     the models from a compiled assembly. "Map all" is only offered when every flagged source property has
///     a fuzzy destination match; "DoNotValidate all" is always available for a 2+ map.
/// </summary>
public class AM004_AggregateCodeFixTests
{
    private const string Project = "AM004AggregateTests";

    private const string NoMatchModels = """
                                         namespace Models
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
    public async Task AM004_DoNotValidateAll_SuppressesEverySourceProperty_InOneEdit()
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
                                                     CreateMap<Source, Destination>().ForSourceMember(src => src.Unused1, opt => opt.DoNotValidate()).ForSourceMember(src => src.Unused2, opt => opt.DoNotValidate());
                                                 }
                                             }
                                         }
                                         """;

        Document document = AggregateFixTestHarness.CreateDocumentWithMetadataModels(Profile, NoMatchModels, Project);
        await AggregateFixTestHarness
            .AssertAggregateClearsAllAsync<AM004_MissingDestinationPropertyAnalyzer, AM004_MissingDestinationPropertyCodeFixProvider>(
                document, "AM004_DoNotValidateAll", expectedFixedCode);
    }

    [Fact]
    public async Task AM004_MapAll_MapsEveryFuzzyMatch_InOneEdit()
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
            .AssertAggregateClearsAllAsync<AM004_MissingDestinationPropertyAnalyzer, AM004_MissingDestinationPropertyCodeFixProvider>(
                document, "AM004_MapAll", expectedFixedCode);
    }

    [Fact]
    public async Task AM004_TwoUnmapped_NoFuzzy_OffersDoNotValidateAllAndNestedSubmenu_NoMapAll()
    {
        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions = await RegisterActionsAsync(NoMatchModels);

        Assert.Equal(2, CodeFixActionInspector.TopLevelCount(actions));
        Assert.Contains(actions, a => a.Depth == 0 && a.EquivalenceKey == "AM004_DoNotValidateAll");
        Assert.DoesNotContain(actions, a => a.EquivalenceKey == "AM004_MapAll");
        Assert.Contains(
            actions,
            a => a.Depth == 0 && a.HasChildren && a.Title.Contains("individual", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, a => a.IsNested && a.EquivalenceKey == "AM004_DoNotValidate_Unused1");
    }

    [Fact]
    public async Task AM004_MapAll_Withheld_WhenAnySourcePropertyHasAmbiguousFuzzyDestination()
    {
        // FirstName → FirstNam is unique; Email → Eamil/Emial ties. "Map all" must not appear
        // because it would invent an arbitrary destination for Email.
        const string ambiguousModels = """
                                       namespace Models
                                       {
                                           public class Source
                                           {
                                               public string FirstName { get; set; }
                                               public string Email { get; set; }
                                           }

                                           public class Destination
                                           {
                                               public string FirstNam { get; set; }
                                               public string Eamil { get; set; }
                                               public string Emial { get; set; }
                                           }
                                       }
                                       """;

        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions = await RegisterActionsAsync(ambiguousModels);

        Assert.Contains(actions, a => a.Depth == 0 && a.EquivalenceKey == "AM004_DoNotValidateAll");
        Assert.DoesNotContain(actions, a => a.EquivalenceKey == "AM004_MapAll");
        Assert.DoesNotContain(
            actions,
            a => a.EquivalenceKey != null && a.EquivalenceKey.StartsWith("AM004_FuzzyMatch_Email_", StringComparison.Ordinal));
        Assert.Contains(
            actions,
            a => a.EquivalenceKey != null && a.EquivalenceKey.StartsWith("AM004_FuzzyMatch_FirstName_", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AM004_TwoUnmapped_Fuzzy_OffersMapAllDoNotValidateAllAndNestedSubmenu()
    {
        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions = await RegisterActionsAsync(FuzzyModels);

        Assert.Equal(3, CodeFixActionInspector.TopLevelCount(actions));
        Assert.Contains(actions, a => a.Depth == 0 && a.EquivalenceKey == "AM004_MapAll");
        Assert.Contains(actions, a => a.Depth == 0 && a.EquivalenceKey == "AM004_DoNotValidateAll");
        Assert.Contains(
            actions,
            a => a.Depth == 0 && a.HasChildren && a.Title.Contains("individual", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            actions,
            a => a.Depth == 0 && a.EquivalenceKey != null && a.EquivalenceKey.StartsWith("AM004_DoNotValidate_", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AM004_SingleUnmapped_StaysFlat_NoAggregate()
    {
        const string singleModels = """
                                    namespace Models
                                    {
                                        public class Source
                                        {
                                            public string Name { get; set; }
                                            public string Unused1 { get; set; }
                                        }

                                        public class Destination
                                        {
                                            public string Name { get; set; }
                                        }
                                    }
                                    """;

        Document document = AggregateFixTestHarness.CreateDocumentWithMetadataModels(Profile, singleModels, Project);
        ImmutableArray<Diagnostic> diagnostics =
            await AggregateFixTestHarness.GetDiagnosticsAsync<AM004_MissingDestinationPropertyAnalyzer>(document);
        Diagnostic single = Assert.Single(diagnostics);

        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions = await CodeFixActionInspector.GetActionsAsync(
            document, new AM004_MissingDestinationPropertyCodeFixProvider(), single);

        Assert.DoesNotContain(actions, a => a.EquivalenceKey is "AM004_MapAll" or "AM004_DoNotValidateAll");
        Assert.Equal(1, CodeFixActionInspector.TopLevelCount(actions));
    }

    private static async Task<IReadOnlyList<CodeFixActionInspector.ActionInfo>> RegisterActionsAsync(string modelsSource)
    {
        Document document = AggregateFixTestHarness.CreateDocumentWithMetadataModels(Profile, modelsSource, Project);
        ImmutableArray<Diagnostic> diagnostics =
            await AggregateFixTestHarness.GetDiagnosticsAsync<AM004_MissingDestinationPropertyAnalyzer>(document);
        Assert.Equal(2, diagnostics.Length);

        return await CodeFixActionInspector.GetActionsAsync(
            document, new AM004_MissingDestinationPropertyCodeFixProvider(), diagnostics);
    }
}

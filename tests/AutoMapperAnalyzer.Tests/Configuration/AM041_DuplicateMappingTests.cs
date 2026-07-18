using AutoMapperAnalyzer.Analyzers.Configuration;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.Configuration;

public class AM041_DuplicateMappingTests
{
    [Fact]
    public async Task Should_ReportDiagnostic_When_DuplicateMappingInSameProfile()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>();
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(11, 9)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_DuplicateMappingInDifferentProfiles()
    {
        const string profile1 = """
                                using AutoMapper;
                                public class Source {}
                                public class Destination {}

                                public class Profile1 : Profile
                                {
                                    public Profile1()
                                    {
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        const string profile2 = """
                                using AutoMapper;

                                public class Profile2 : Profile
                                {
                                    public Profile2()
                                    {
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation("Profile2.cs", 7, 9) // Line 7 in profile2
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(
            new[] { ("Profile1.cs", profile1), ("Profile2.cs", profile2) },
            expected);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_MappingsAreUnique()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}
                                public class Other {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>();
                                        CreateMap<Source, Other>();
                                        CreateMap<Other, Destination>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_ForNonAutoMapperCreateMap()
    {
        const string testCode = """
                                namespace CustomMapping;

                                public class Source {}
                                public class Destination {}

                                public class Profile
                                {
                                    protected void CreateMap<TSource, TDestination>() {}
                                }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>();
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode);
    }
    [Fact]
    public async Task Should_ReportDiagnostic_When_ParenthesizedCreateMapReverseMapDuplicates()
    {
        // (CreateMap<S,D>()).ReverseMap() must register the reverse D→S so a later
        // CreateMap<D,S>() is detected as a duplicate.
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        (CreateMap<Source, Destination>()).ReverseMap();
                                        CreateMap<Destination, Source>();
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(11, 9)
            .WithArguments("Destination", "Source");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_DeferredReverseMapPrecedesExplicitReverseDirection()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        var mapping = CreateMap<Source, Destination>();
                                        mapping.ReverseMap();
                                        CreateMap<Destination, Source>();
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(12, 9)
            .WithArguments("Destination", "Source");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_OnDeferredReverseMap_WhenExplicitDirectionPrecedesIt()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Destination, Source>();
                                        var mapping = CreateMap<Source, Destination>();
                                        mapping.ReverseMap();
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(12, 17)
            .WithArguments("Destination", "Source");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }


    [Fact]
    public async Task Should_ReportDiagnostic_When_ConfiguredLocalDefersReverseMap()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string Name { get; set; } = ""; }
                                public class Destination { public string Name { get; set; } = ""; }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        var mapping = CreateMap<Source, Destination>()
                                            .ForMember(destination => destination.Name, options => options.MapFrom(source => source.Name));
                                        mapping.ReverseMap();
                                        CreateMap<Destination, Source>();
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(13, 9)
            .WithArguments("Destination", "Source");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_WhenLocalCreateMapHasNoDeferredReverseMap()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        var mapping = CreateMap<Source, Destination>();
                                        CreateMap<Destination, Source>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_WhenDeferredReverseMapUsesAlias()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        var mapping = CreateMap<Source, Destination>();
                                        var alias = mapping;
                                        alias.ReverseMap();
                                        CreateMap<Destination, Source>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_WhenDeferredReverseMapIsConditional()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile(bool reverse)
                                    {
                                        var mapping = CreateMap<Source, Destination>();
                                        if (reverse)
                                        {
                                            mapping.ReverseMap();
                                        }
                                        CreateMap<Destination, Source>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_WhenCreateMapIsNestedInInitializerArgument()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        var mapping = Wrap(CreateMap<Source, Destination>());
                                        mapping.ReverseMap();
                                        CreateMap<Destination, Source>();
                                    }

                                    private static IMappingExpression<TSource, TDestination> Wrap<TSource, TDestination>(
                                        IMappingExpression<TSource, TDestination> mapping) => mapping;
                                }
                                """;

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_WhenRegistrationsAreInOppositeIfElseBranches()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile(bool useAlternative)
                                    {
                                        if (useAlternative)
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                        else
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_WhenRegistrationsAreInIndependentIfStatements()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile(bool first, bool second)
                                    {
                                        if (first)
                                        {
                                            CreateMap<Source, Destination>();
                                        }

                                        if (second)
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(17, 13)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_WhenRegistrationsAreInOneIfElseIfChain()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile(bool first, bool second)
                                    {
                                        if (first)
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                        else if (second)
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                        else
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_WhenUnconditionalRegistrationFollowsIfElseRegistrations()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile(bool useAlternative)
                                    {
                                        if (useAlternative)
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                        else
                                        {
                                            CreateMap<Source, Destination>();
                                        }

                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(19, 9)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

}

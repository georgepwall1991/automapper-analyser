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
    public async Task Should_NotReportDiagnostic_WhenMapperConfigurationRegistrationsAreInOppositeIfElseBranches()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class ConfigurationOwner
                                {
                                    public ConfigurationOwner(bool useAlternative)
                                    {
                                        _ = new MapperConfiguration(cfg =>
                                        {
                                            if (useAlternative)
                                            {
                                                cfg.CreateMap<Source, Destination>();
                                            }
                                            else
                                            {
                                                cfg.CreateMap<Source, Destination>();
                                            }
                                        });
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_WhenLookalikeMapperConfigurationExecutesCallbackTwice()
    {
        const string testCode = """
                                #pragma warning disable CS0436
                                using AutoMapper;
                                using System;

                                public class Source {}
                                public class Destination {}

                                namespace AutoMapper
                                {
                                    public class MapperConfiguration
                                    {
                                        public MapperConfiguration(Action<IMapperConfigurationExpression> configure)
                                        {
                                            IMapperConfigurationExpression expression = default!;
                                            configure(expression);
                                            configure(expression);
                                        }
                                    }
                                }

                                public class ConfigurationOwner
                                {
                                    public ConfigurationOwner(bool useAlternative)
                                    {
                                        _ = new MapperConfiguration(cfg =>
                                        {
                                            if (useAlternative)
                                            {
                                                cfg.CreateMap<Source, Destination>();
                                            }
                                            else
                                            {
                                                cfg.CreateMap<Source, Destination>();
                                            }
                                        });
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(33, 17)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_WhenIfElseCanExecuteBothBranchesAcrossLoopIterations()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class ConfigurationOwner
                                {
                                    public ConfigurationOwner()
                                    {
                                        _ = new MapperConfiguration(cfg =>
                                        {
                                            foreach (bool useAlternative in new[] { true, false })
                                            {
                                                if (useAlternative)
                                                {
                                                    cfg.CreateMap<Source, Destination>();
                                                }
                                                else
                                                {
                                                    cfg.CreateMap<Source, Destination>();
                                                }
                                            }
                                        });
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(20, 21)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_WhenRepeatableHelperExecutesOppositeIfElseBranches()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        Configure(useAlternative: true);
                                        Configure(useAlternative: false);
                                    }

                                    private void Configure(bool useAlternative)
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

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(22, 13)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
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
    public async Task Should_NotReportDiagnostic_WhenMapperConfigurationRegistrationsAreInOneIfElseIfChain()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class ConfigurationOwner
                                {
                                    public ConfigurationOwner(bool first, bool second)
                                    {
                                        _ = new MapperConfiguration(cfg =>
                                        {
                                            if (first)
                                            {
                                                cfg.CreateMap<Source, Destination>();
                                            }
                                            else if (second)
                                            {
                                                cfg.CreateMap<Source, Destination>();
                                            }
                                            else
                                            {
                                                cfg.CreateMap<Source, Destination>();
                                            }
                                        });
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

                                public class ConfigurationOwner
                                {
                                    public ConfigurationOwner(bool useAlternative)
                                    {
                                        _ = new MapperConfiguration(cfg =>
                                        {
                                            if (useAlternative)
                                            {
                                                cfg.CreateMap<Source, Destination>();
                                            }
                                            else
                                            {
                                                cfg.CreateMap<Source, Destination>();
                                            }

                                            cfg.CreateMap<Source, Destination>();
                                        });
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(21, 13)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_WhenProfileInstancesCanSelectDifferentSwitchSections()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile(int mode)
                                    {
                                        switch (mode)
                                        {
                                            case 0:
                                                CreateMap<Source, Destination>();
                                                break;
                                            default:
                                                CreateMap<Source, Destination>();
                                                break;
                                        }
                                    }
                                }

                                public class ConfigurationOwner
                                {
                                    public ConfigurationOwner()
                                    {
                                        _ = new MapperConfiguration(cfg =>
                                        {
                                            cfg.AddProfile(new MyProfile(0));
                                            cfg.AddProfile(new MyProfile(1));
                                        });
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(16, 17)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_WhenSwitchCanExecuteDifferentSectionsAcrossLoopIterations()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        foreach (int mode in new[] { 0, 1 })
                                        {
                                            switch (mode)
                                            {
                                                case 0:
                                                    CreateMap<Source, Destination>();
                                                    break;
                                                default:
                                                    CreateMap<Source, Destination>();
                                                    break;
                                            }
                                        }
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(18, 21)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_WhenRegistrationInsideSwitchSectionCanRepeatInLoop()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class ConfigurationOwner
                                {
                                    public ConfigurationOwner(int mode)
                                    {
                                        _ = new MapperConfiguration(cfg =>
                                        {
                                            switch (mode)
                                            {
                                                case 0:
                                                    for (int index = 0; index < 2; index++)
                                                    {
                                                        cfg.CreateMap<Source, Destination>();
                                                    }
                                                    break;
                                                default:
                                                    cfg.CreateMap<Source, Destination>();
                                                    break;
                                            }
                                        });
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(21, 21)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_WhenRegistrationsShareSwitchSection()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile(int mode)
                                    {
                                        switch (mode)
                                        {
                                            case 0:
                                                CreateMap<Source, Destination>();
                                                CreateMap<Source, Destination>();
                                                break;
                                        }
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(14, 17)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_WhenSwitchCanGotoAnotherRegistrationSection()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile(int mode)
                                    {
                                        switch (mode)
                                        {
                                            case 0:
                                                CreateMap<Source, Destination>();
                                                goto default;
                                            default:
                                                CreateMap<Source, Destination>();
                                                break;
                                        }
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(16, 17)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_WhenGotoOutsideSwitchCanRepeatDifferentSections()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        int mode = 0;
                                    retry:
                                        switch (mode)
                                        {
                                            case 0:
                                                CreateMap<Source, Destination>();
                                                mode = 1;
                                                break;
                                            default:
                                                CreateMap<Source, Destination>();
                                                return;
                                        }
                                        goto retry;
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(19, 17)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_WhenLocalFunctionCanRunSwitchWithDifferentModes()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        Configure(0);
                                        Configure(1);

                                        void Configure(int mode)
                                        {
                                            switch (mode)
                                            {
                                                case 0:
                                                    CreateMap<Source, Destination>();
                                                    break;
                                                default:
                                                    CreateMap<Source, Destination>();
                                                    break;
                                            }
                                        }
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(21, 21)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_WhenHelperMethodCanRunSwitchWithDifferentModes()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        Configure(0);
                                        Configure(1);
                                    }

                                    private void Configure(int mode)
                                    {
                                        switch (mode)
                                        {
                                            case 0:
                                                CreateMap<Source, Destination>();
                                                break;
                                            default:
                                                CreateMap<Source, Destination>();
                                                break;
                                        }
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(22, 17)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_WhenLambdaCanRunSwitchWithDifferentModes()
    {
        const string testCode = """
                                using System;
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        Action<int> configure = mode =>
                                        {
                                            switch (mode)
                                            {
                                                case 0:
                                                    CreateMap<Source, Destination>();
                                                    break;
                                                default:
                                                    CreateMap<Source, Destination>();
                                                    break;
                                            }
                                        };

                                        configure(0);
                                        configure(1);
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(19, 21)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_WhenRegistrationsAreInDirectMapperConfigurationLambdaSwitch()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class ConfigurationOwner
                                {
                                    public ConfigurationOwner(int mode)
                                    {
                                        _ = new MapperConfiguration(cfg =>
                                        {
                                            switch (mode)
                                            {
                                                case 0:
                                                    cfg.CreateMap<Source, Destination>();
                                                    break;
                                                default:
                                                    cfg.CreateMap<Source, Destination>();
                                                    break;
                                            }
                                        });
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

}

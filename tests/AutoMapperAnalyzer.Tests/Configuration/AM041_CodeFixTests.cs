using AutoMapperAnalyzer.Analyzers.Configuration;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.Configuration;

public class AM041_CodeFixTests
{
    [Fact]
    public async Task Should_Remove_DuplicateCreateMap_Statement()
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

        const string fixedCode = """
                                 using AutoMapper;

                                 public class Source {}
                                 public class Destination {}

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Destination>();
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(11, 9)
            .WithArguments("Source", "Destination");

        await CodeFixVerifier<AM041_DuplicateMappingAnalyzer, AM041_DuplicateMappingCodeFixProvider>
            .VerifyFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task Should_Remove_DuplicateReverseMap_Call()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>().ReverseMap();
                                        CreateMap<Destination, Source>();
                                    }
                                }
                                """;

        // Here, CreateMap<Destination, Source>() is the duplicate (Line 11)
        // Wait, my analyzer logic flags the second one found.
        // CreateMap<S, D>().ReverseMap() registers S->D and D->S.
        // CreateMap<D, S>() registers D->S.
        // Duplicate is D->S.
        // Location of duplicate depends on order.
        // If CreateMap<D, S> is processed second, it's flagged.
        // And the fix should remove it.

        // Wait, removing CreateMap<Destination, Source>() statement is correct here.
        // But what if ReverseMap() is the one flagged?
        // e.g. CreateMap<Destination, Source>(); CreateMap<Source, Destination>().ReverseMap();

        const string fixedCode = """
                                 using AutoMapper;

                                 public class Source {}
                                 public class Destination {}

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Destination>().ReverseMap();
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(11, 9)
            .WithArguments("Destination", "Source");

        await CodeFixVerifier<AM041_DuplicateMappingAnalyzer, AM041_DuplicateMappingCodeFixProvider>
            .VerifyFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task Should_Remove_ReverseMap_WhenItIsTheDuplicate()
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
                                        CreateMap<Source, Destination>().ReverseMap();
                                    }
                                }
                                """;

        // Here:
        // 1. CreateMap<Destination, Source>() -> D->S
        // 2. CreateMap<Source, Destination>().ReverseMap() -> S->D and D->S
        // Duplicate D->S is found at (2).
        // Location should be ReverseMap() call (line 11).

        // Fix should remove ReverseMap().

        const string fixedCode = """
                                 using AutoMapper;

                                 public class Source {}
                                 public class Destination {}

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Destination, Source>();
                                         CreateMap<Source, Destination>();
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(11, 42) // Location of ReverseMap
            .WithArguments("Destination", "Source");

        await CodeFixVerifier<AM041_DuplicateMappingAnalyzer, AM041_DuplicateMappingCodeFixProvider>
            .VerifyFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task Should_KeepReverseDirection_WhenDuplicateIsCreateMapInsideReverseMap()
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
                                        CreateMap<Source, Destination>().ReverseMap();
                                    }
                                }
                                """;

        const string fixedCode = """
                                 using AutoMapper;

                                 public class Source {}
                                 public class Destination {}

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Destination>();
                                         CreateMap<Destination, Source>();
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(11, 9)
            .WithArguments("Source", "Destination");

        await CodeFixVerifier<AM041_DuplicateMappingAnalyzer, AM041_DuplicateMappingCodeFixProvider>
            .VerifyFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task Should_KeepReverseDirection_ForMemberAccessCreateMapPattern()
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
                                        this.CreateMap<Source, Destination>().ReverseMap();
                                    }
                                }
                                """;

        const string fixedCode = """
                                 using AutoMapper;

                                 public class Source {}
                                 public class Destination {}

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Destination>();
                                         this.CreateMap<Destination, Source>();
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(11, 9)
            .WithArguments("Source", "Destination");

        await CodeFixVerifier<AM041_DuplicateMappingAnalyzer, AM041_DuplicateMappingCodeFixProvider>
            .VerifyFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task Should_Remove_ThreeDuplicates_BulkRemoval()
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
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        const string fixedCode = """
                                 using AutoMapper;

                                 public class Source {}
                                 public class Destination {}

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Destination>();
                                     }
                                 }
                                 """;

        DiagnosticResult diag1 = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(11, 9)
            .WithArguments("Source", "Destination");

        DiagnosticResult diag2 = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(12, 9)
            .WithArguments("Source", "Destination");

        await CodeFixVerifier<AM041_DuplicateMappingAnalyzer, AM041_DuplicateMappingCodeFixProvider>
            .VerifyFixAsync(testCode, new[] { diag1, diag2 }, fixedCode, null, null, 2);
    }

    [Fact]
    public async Task Should_Detect_ConflictingConfigurations()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string Name { get; set; } }
                                public class Destination { public string FullName { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.FullName, opt => opt.MapFrom(s => s.Name));
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.FullName, opt => opt.Ignore());
                                    }
                                }
                                """;

        const string fixedCode = """
                                 using AutoMapper;

                                 public class Source { public string Name { get; set; } }
                                 public class Destination { public string FullName { get; set; } }

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                             .ForMember(d => d.FullName, opt => opt.MapFrom(s => s.Name));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(12, 9)
            .WithArguments("Source", "Destination");

        await CodeFixVerifier<AM041_DuplicateMappingAnalyzer, AM041_DuplicateMappingCodeFixProvider>
            .VerifyFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task Should_Detect_DuplicateAcrossProfiles()
    {
        const string testCode = """
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

                                public class Profile2 : Profile
                                {
                                    public Profile2()
                                    {
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(18, 9)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_Detect_ReverseMapCreatesDuplicates()
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
                                        CreateMap<Destination, Source>();
                                        CreateMap<Source, Destination>().ReverseMap();
                                    }
                                }
                                """;

        const string fixedCode = """
                                 using AutoMapper;

                                 public class Source {}
                                 public class Destination {}

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Destination>();
                                         CreateMap<Destination, Source>();
                                     }
                                 }
                                 """;

        DiagnosticResult diag1 = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(12, 9)
            .WithArguments("Source", "Destination");

        DiagnosticResult diag2 = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(12, 42)
            .WithArguments("Destination", "Source");

        await CodeFixVerifier<AM041_DuplicateMappingAnalyzer, AM041_DuplicateMappingCodeFixProvider>
            .VerifyFixAsync(testCode, new[] { diag1, diag2 }, fixedCode, null, null, 2);
    }

    [Fact]
    public async Task Should_Handle_GenericTypeDuplicates()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<List<Source>, List<Destination>>();
                                        CreateMap<List<Source>, List<Destination>>();
                                    }
                                }
                                """;

        const string fixedCode = """
                                 using AutoMapper;
                                 using System.Collections.Generic;

                                 public class Source {}
                                 public class Destination {}

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<List<Source>, List<Destination>>();
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(12, 9)
            .WithArguments("List", "List");

        await CodeFixVerifier<AM041_DuplicateMappingAnalyzer, AM041_DuplicateMappingCodeFixProvider>
            .VerifyFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task Should_VerifyCodeActionTitle()
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

        const string fixedCode = """
                                 using AutoMapper;

                                 public class Source {}
                                 public class Destination {}

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Destination>();
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(11, 9)
            .WithArguments("Source", "Destination");

        await CodeFixVerifier<AM041_DuplicateMappingAnalyzer, AM041_DuplicateMappingCodeFixProvider>
            .VerifyFixAsync(testCode, expected, fixedCode, 0);
    }

    [Fact]
    public async Task Should_NotDetectDuplicate_WhenUsingIncludeBase()
    {
        const string testCode = """
                                using AutoMapper;

                                public class BaseSource {}
                                public class DerivedSource : BaseSource {}
                                public class BaseDestination {}
                                public class DerivedDestination : BaseDestination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<BaseSource, BaseDestination>();
                                        CreateMap<DerivedSource, DerivedDestination>()
                                            .IncludeBase<BaseSource, BaseDestination>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode);
    }
}

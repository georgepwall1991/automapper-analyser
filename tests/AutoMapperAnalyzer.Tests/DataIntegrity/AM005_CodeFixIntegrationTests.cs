using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

public class AM005_CodeFixIntegrationTests
{
    [Fact]
    public async Task AM005_ShouldApplyExplicitMappingCodeFix()
    {
        const string testCode = """
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

        const string expectedFixedCode = """
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

        await CodeFixVerifier<AM005_CaseSensitivityMismatchAnalyzer, AM005_CaseSensitivityMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule)
                    .WithLocation(19, 13)
                    .WithArguments("firstName", "FirstName"),
                expectedFixedCode,
                0);
    }

    [Fact]
    public async Task AM005_ShouldApplyExplicitMappingCodeFix_ForReverseMapDiagnostic()
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
                                        public string name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.name, opt => opt.MapFrom(src => src.Name))
                                                .ReverseMap();
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
                                                 public string Name { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string name { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.name, opt => opt.MapFrom(src => src.Name))
                                                         .ReverseMap().ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.name));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM005_CaseSensitivityMismatchAnalyzer, AM005_CaseSensitivityMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule)
                    .WithLocation(19, 13)
                    .WithArguments("name", "Name"),
                expectedFixedCode,
                0);
    }

    [Fact]
    public async Task AM005_ShouldApplyRenameCodeFix_WhenSourcePropertyIsEditable()
    {
        const string testCode = """
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

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string EmailAddress { get; set; }
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

        await CodeFixVerifier<AM005_CaseSensitivityMismatchAnalyzer, AM005_CaseSensitivityMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule)
                    .WithLocation(19, 13)
                    .WithArguments("emailAddress", "EmailAddress"),
                expectedFixedCode,
                1);
    }
}

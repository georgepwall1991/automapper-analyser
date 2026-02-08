using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

public class AM005_CodeFixTests
{
    [Fact]
    public async Task AM005_ShouldAddExplicitPropertyMapping()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string firstName { get; set; }
                                        public string lastName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string FirstName { get; set; }
                                        public string LastName { get; set; }
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
                                                 public string lastName { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string FirstName { get; set; }
                                                 public string LastName { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.firstName));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM005_CaseSensitivityMismatchAnalyzer>()
            .WithCodeFix<AM005_CaseSensitivityMismatchCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 23, 29)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM005_ShouldOfferExecutableFix_ForSimpleCaseMismatch()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string userName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string UserName { get; set; }
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
                                                 public string userName { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string UserName { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.userName));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM005_CaseSensitivityMismatchAnalyzer>()
            .WithCodeFix<AM005_CaseSensitivityMismatchCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 21, 29)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM005_ShouldOfferExecutableFix_ForLowerCamelSourceProperty()
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
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.EmailAddress, opt => opt.MapFrom(src => src.emailAddress));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM005_CaseSensitivityMismatchAnalyzer>()
            .WithCodeFix<AM005_CaseSensitivityMismatchCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 21, 29)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM005_ShouldHandleAcronymCaseMismatch()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Url { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string URL { get; set; }
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
                                                 public string Url { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string URL { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.URL, opt => opt.MapFrom(src => src.Url));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM005_CaseSensitivityMismatchAnalyzer>()
            .WithCodeFix<AM005_CaseSensitivityMismatchCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 14, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM005_ShouldHandleUnderscoreCaseMismatch()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string user_name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string UserName { get; set; }
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
                                                 public string user_name { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string UserName { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.user_name));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM005_CaseSensitivityMismatchAnalyzer>()
            .WithCodeFix<AM005_CaseSensitivityMismatchCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 14, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM005_ShouldHandleNumericSuffixCaseMismatch()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string address1 { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Address1 { get; set; }
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
                                                 public string address1 { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Address1 { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Address1, opt => opt.MapFrom(src => src.address1));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM005_CaseSensitivityMismatchAnalyzer>()
            .WithCodeFix<AM005_CaseSensitivityMismatchCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 14, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }
}

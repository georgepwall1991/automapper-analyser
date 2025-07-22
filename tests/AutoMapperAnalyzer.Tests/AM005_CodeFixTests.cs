using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.CodeFixes;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests;

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
    public async Task AM005_ShouldAddCaseInsensitiveConfigComment()
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
                                                     // TODO: Consider configuring case-insensitive property matching in MapperConfiguration
                                                     // Alternative: cfg.DestinationMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
                                                     // or cfg.SourceMemberNamingConvention = PascalCaseNamingConvention.Instance;
                                                     CreateMap<Source, Destination>();
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
    public async Task AM005_ShouldAddCasingCorrectionComment()
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
                                                     // TODO: Standardize property casing - consider renaming 'emailAddress' to 'EmailAddress' in source class
                                                     // This will eliminate case sensitivity issues and improve code consistency
                                                     CreateMap<Source, Destination>();
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
}
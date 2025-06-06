using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests;

public class AM005_CaseSensitivityMismatchTests
{
    [Fact]
    public async Task AM005_ShouldReportDiagnostic_WhenPropertiesDifferInCasingOnly()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string firstName { get; set; }
                                        public string lastName { get; set; }
                                        public string userName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string FirstName { get; set; }
                                        public string LastName { get; set; }
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM005_CaseSensitivityMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 23, 13, "firstName", "FirstName")
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 23, 13, "lastName", "LastName")
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 23, 13, "userName", "UserName")
            .RunAsync();
    }

    [Fact]
    public async Task AM005_ShouldNotReportDiagnostic_WhenPropertyNamesMatchExactly()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string FirstName { get; set; }
                                        public string LastName { get; set; }
                                        public string Email { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string FirstName { get; set; }
                                        public string LastName { get; set; }
                                        public string Email { get; set; }
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM005_CaseSensitivityMismatchAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM005_ShouldNotReportDiagnostic_WhenPropertyIsCompletelyMissing()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public string CompletelyDifferentProperty { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
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

        // This should trigger AM004 (missing destination), not AM005 (case mismatch)
        await DiagnosticTestFramework
            .ForAnalyzer<AM005_CaseSensitivityMismatchAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM005_ShouldHandleExplicitPropertyMapping()
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
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.firstName));
                                        }
                                    }
                                }
                                """;

        // Only lastName should trigger diagnostic since firstName is explicitly mapped
        await DiagnosticTestFramework
            .ForAnalyzer<AM005_CaseSensitivityMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 21, 13, "lastName", "LastName")
            .RunAsync();
    }

    [Fact]
    public async Task AM005_ShouldHandleMixedCasingScenarios()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string userID { get; set; }
                                        public string eMail { get; set; }
                                        public string phoneNumber { get; set; }
                                        public string HTTPStatus { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string UserID { get; set; }
                                        public string Email { get; set; }
                                        public string PhoneNumber { get; set; }
                                        public string HttpStatus { get; set; }
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM005_CaseSensitivityMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 25, 13, "userID", "UserID")
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 25, 13, "eMail", "Email")
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 25, 13, "phoneNumber", "PhoneNumber")
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 25, 13, "HTTPStatus", "HttpStatus")
            .RunAsync();
    }

    [Fact]
    public async Task AM005_ShouldHandleInheritedProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class BaseSource
                                    {
                                        public string baseName { get; set; }
                                    }

                                    public class Source : BaseSource
                                    {
                                        public string firstName { get; set; }
                                    }

                                    public class BaseDestination
                                    {
                                        public string BaseName { get; set; }
                                    }

                                    public class Destination : BaseDestination
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM005_CaseSensitivityMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 29, 13, "baseName", "BaseName")
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 29, 13, "firstName", "FirstName")
            .RunAsync();
    }

    [Fact]
    public async Task AM005_ShouldIgnoreStaticProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string firstName { get; set; }
                                        public static string staticProperty { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string FirstName { get; set; }
                                        public static string StaticProperty { get; set; }
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

        // Only instance properties should be analyzed, static properties should be ignored
        await DiagnosticTestFramework
            .ForAnalyzer<AM005_CaseSensitivityMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 21, 13, "firstName", "FirstName")
            .RunAsync();
    }

    [Fact]
    public async Task AM005_ShouldIgnoreReadOnlyProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string firstName { get; set; }
                                        public string readOnlyProperty { get; }
                                    }

                                    public class Destination
                                    {
                                        public string FirstName { get; set; }
                                        public string ReadOnlyProperty { get; }
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

        // Only read-write properties should be analyzed
        await DiagnosticTestFramework
            .ForAnalyzer<AM005_CaseSensitivityMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 21, 13, "firstName", "FirstName")
            .RunAsync();
    }

    [Fact]
    public async Task AM005_ShouldHandleComplexCasingEdgeCases()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string XMLHttpRequest { get; set; }
                                        public string URLPath { get; set; }
                                        public string iDCard { get; set; }
                                        public string aCRONYM { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string XmlHttpRequest { get; set; }
                                        public string UrlPath { get; set; }
                                        public string IdCard { get; set; }
                                        public string Acronym { get; set; }
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM005_CaseSensitivityMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 25, 13, "XMLHttpRequest", "XmlHttpRequest")
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 25, 13, "URLPath", "UrlPath")
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 25, 13, "iDCard", "IdCard")
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 25, 13, "aCRONYM", "Acronym")
            .RunAsync();
    }

    [Fact]
    public async Task AM005_ShouldNotReportWhenPropertiesHaveIncompatibleTypes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string firstName { get; set; }
                                        public string ageValue { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string FirstName { get; set; }
                                        public int AgeValue { get; set; }
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

        // firstName should trigger AM005 (case mismatch)
        // ageValue should NOT trigger AM005 because it has type mismatch (would be handled by AM001)
        await DiagnosticTestFramework
            .ForAnalyzer<AM005_CaseSensitivityMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule, 21, 13, "firstName", "FirstName")
            .RunAsync();
    }
} 
using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests;

public class AM004_MissingDestinationPropertyTests
{
    [Fact]
    public async Task AM004_ShouldReportDiagnostic_WhenSourcePropertyMissingInDestination()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public string ImportantData { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        // Missing ImportantData property - data loss!
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
            .ForAnalyzer<AM004_MissingDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 23, 13,
                "ImportantData")
            .RunAsync();
    }

    [Fact]
    public async Task AM004_ShouldNotReportDiagnostic_WhenAllPropertiesAreMapped()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public string Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public string Data { get; set; }
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
            .ForAnalyzer<AM004_MissingDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM004_ShouldNotReportDiagnostic_WhenExplicitIgnoreConfigured()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public string TempData { get; set; }
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
                                            CreateMap<Source, Destination>()
                                                .ForSourceMember(src => src.TempData, opt => opt.DoNotValidate());
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM004_MissingDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM004_ShouldNotReportDiagnostic_WhenCustomMappingHandlesProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string FirstName { get; set; }
                                        public string LastName { get; set; }
                                        public string MiddleName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string FullName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => 
                                                    $"{src.FirstName} {src.MiddleName} {src.LastName}"));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM004_MissingDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM004_ShouldReportMultipleMissingProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string MissingProperty1 { get; set; }
                                        public string MissingProperty2 { get; set; }
                                        public int MissingNumber { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
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
            .ForAnalyzer<AM004_MissingDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 22, 13,
                "MissingProperty1")
            .ExpectDiagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 22, 13,
                "MissingProperty2")
            .ExpectDiagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 22, 13,
                "MissingNumber")
            .RunAsync();
    }

    [Fact]
    public async Task AM004_ShouldHandleCaseSensitivePropertyMatching()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string userName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
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

        // AutoMapper uses case-insensitive matching by default, so userName should map to UserName
        await DiagnosticTestFramework
            .ForAnalyzer<AM004_MissingDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM004_ShouldIgnoreStaticProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public static string StaticProperty { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
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

        // Static properties should not be considered for mapping
        await DiagnosticTestFramework
            .ForAnalyzer<AM004_MissingDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM004_ShouldIgnoreReadOnlyProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string ReadOnlyProp { get; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
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

        // Read-only properties (no setter) should not be considered for mapping
        await DiagnosticTestFramework
            .ForAnalyzer<AM004_MissingDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM004_ShouldHandleInheritedProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class BaseSource
                                    {
                                        public string BaseProperty { get; set; }
                                        public string MissingInDest { get; set; }
                                    }

                                    public class Source : BaseSource
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string BaseProperty { get; set; }
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
            .ForAnalyzer<AM004_MissingDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 26, 13,
                "MissingInDest")
            .RunAsync();
    }

    [Fact]
    public async Task AM004_ShouldHandleComplexTypes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Address
                                    {
                                        public string Street { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public Address HomeAddress { get; set; }
                                        public Address WorkAddress { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public Address HomeAddress { get; set; }
                                        // Missing WorkAddress - data loss!
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
            .ForAnalyzer<AM004_MissingDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 28, 13,
                "WorkAddress")
            .RunAsync();
    }
}

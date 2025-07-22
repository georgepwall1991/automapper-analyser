using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests;

public class AM011_UnmappedRequiredPropertyTests
{
    [Fact]
    public async Task AM011_ShouldReportDiagnostic_WhenRequiredPropertyNotMappedFromSource()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public required string RequiredField { get; set; }
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
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule, 22, 13,
                "RequiredField")
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldNotReportDiagnostic_WhenRequiredPropertyMappedFromSource()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public string RequiredField { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public required string RequiredField { get; set; }
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
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM011_ShouldNotReportDiagnostic_WhenRequiredPropertyExplicitlyMapped()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public string SourceValue { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public required string RequiredField { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.RequiredField, opt => opt.MapFrom(src => src.SourceValue));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM011_ShouldNotReportDiagnostic_WhenRequiredPropertyHasConstantValue()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public required string RequiredField { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.RequiredField, opt => opt.MapFrom(src => "DefaultValue"));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM011_ShouldReportMultipleRequiredProperties()
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
                                        public string Name { get; set; }
                                        public required string RequiredField1 { get; set; }
                                        public required string RequiredField2 { get; set; }
                                        public required int RequiredNumber { get; set; }
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
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule, 22, 13,
                "RequiredField1")
            .ExpectDiagnostic(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule, 22, 13,
                "RequiredField2")
            .ExpectDiagnostic(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule, 22, 13,
                "RequiredNumber")
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldHandleInheritedRequiredProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class BaseSource
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Source : BaseSource
                                    {
                                        public string Email { get; set; }
                                    }

                                    public class BaseDestination
                                    {
                                        public string Name { get; set; }
                                        public required string BaseRequiredField { get; set; }
                                    }

                                    public class Destination : BaseDestination
                                    {
                                        public string Email { get; set; }
                                        public required string DerivedRequiredField { get; set; }
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
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule, 31, 13,
                "BaseRequiredField")
            .ExpectDiagnostic(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule, 31, 13,
                "DerivedRequiredField")
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldIgnoreNonRequiredProperties()
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
                                        public string Name { get; set; }
                                        public string OptionalField { get; set; }
                                        public required string RequiredField { get; set; }
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

        // Only RequiredField should trigger diagnostic, not OptionalField
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule, 21, 13,
                "RequiredField")
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldIgnoreStaticProperties()
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
                                        public string Name { get; set; }
                                        public static string StaticRequiredField { get; set; }
                                        public required string InstanceRequiredField { get; set; }
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

        // Only instance required properties should be analyzed
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule, 21, 13,
                "InstanceRequiredField")
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldHandlePartialMapping()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string MappedValue { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public required string MappedRequired { get; set; }
                                        public required string UnmappedRequired { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.MappedRequired, opt => opt.MapFrom(src => src.MappedValue));
                                        }
                                    }
                                }
                                """;

        // Only the unmapped required property should trigger diagnostic
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule, 22, 13,
                "UnmappedRequired")
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldHandleCaseSensitivePropertyMatching()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string requiredfield { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public required string RequiredField { get; set; }
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

        // AutoMapper uses case-insensitive matching by default, so requiredfield should map to RequiredField
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }
}

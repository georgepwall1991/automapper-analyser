using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

public class AM006_UnmappedDestinationPropertyTests
{
    [Fact]
    public async Task AM006_ShouldReportDiagnostic_ForNonRequiredUnmappedDestinationProperty()
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
                                        public string ExtraInfo { get; set; }
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
            .ForAnalyzer<AM006_UnmappedDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule, 20, 13,
                "ExtraInfo", "Source")
            .RunAsync();
    }

    [Fact]
    public async Task AM006_ShouldNotReportDiagnostic_ForRequiredUnmappedDestinationProperty()
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
            .ForAnalyzer<AM006_UnmappedDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM006_ShouldNotReportDiagnostic_WhenDestinationPropertyConfiguredWithForMember()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Info { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string ExtraInfo { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.ExtraInfo, opt => opt.MapFrom(src => src.Info));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM006_UnmappedDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }
}

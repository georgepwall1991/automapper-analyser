using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

public class AM011_EdgeCaseTests
{
    [Fact]
    public async Task Should_ReportDiagnostic_When_MappingPropertyThatContainsRequiredPropertyName()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source
                                {
                                    public string Value { get; set; }
                                }

                                public class Destination
                                {
                                    public required string Name { get; set; }
                                    public string NameSuffix { get; set; }
                                }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.NameSuffix, o => o.MapFrom(s => s.Value));
                                        // 'Name' is required but not mapped.
                                        // However, 'NameSuffix' contains "Name".
                                        // If the analyzer uses string.Contains("Name"), it might falsely think Name is mapped.
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule, 18, 9, "Name")
            .RunAsync();
    }
}

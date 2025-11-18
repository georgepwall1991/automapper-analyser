using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests;

public class AM020_ReverseMapTests
{
    [Fact]
    public async Task AM020_ShouldNotReportDiagnostic_WhenReverseMapProvidesMapping()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceInner { public string P { get; set; } }
                                    public class DestInner { public string P { get; set; } }

                                    public class Source
                                    {
                                        public SourceInner Inner { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public DestInner Inner { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                            
                                            // This defines DestInner -> SourceInner AND SourceInner -> DestInner (via ReverseMap)
                                            // So AM020 should be satisfied for Source -> Destination mapping of 'Inner'.
                                            CreateMap<DestInner, SourceInner>().ReverseMap();
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM020_NestedObjectMappingAnalyzer>.VerifyAnalyzerAsync(testCode);
    }
}


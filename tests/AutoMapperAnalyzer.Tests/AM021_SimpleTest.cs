using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests;

public class AM021_SimpleTest
{
    [Fact]
    public async Task AM021_Simple_ShouldReportDiagnostic()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Numbers { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<int> Numbers { get; set; }
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
            .ForAnalyzer<AM021_CollectionElementMismatchAnalyzer_Simple>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM021_CollectionElementMismatchAnalyzer_Simple.CollectionElementMismatchRule, 20, 29, 
                "List<System.String>", "List<System.Int32>")
            .RunAsync();
    }
}
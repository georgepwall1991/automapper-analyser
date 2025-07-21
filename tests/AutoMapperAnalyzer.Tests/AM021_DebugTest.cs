using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests;

public class AM021_DebugTest
{
    [Fact]
    public async Task AM021_Debug_SimpleCollectionMismatch()
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

        // Just try to run without expecting any diagnostics to see what happens
        await DiagnosticTestFramework
            .ForAnalyzer<AM021_CollectionElementMismatchAnalyzer>()
            .WithSource(testCode)
            .RunAsync();
    }
}
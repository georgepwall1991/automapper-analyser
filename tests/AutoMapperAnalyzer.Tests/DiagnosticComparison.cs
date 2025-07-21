using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests;

public class DiagnosticComparison
{
    [Fact]
    public async Task AM003_Works_HashSetToList()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public HashSet<string> Tags { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<string> Tags { get; set; }
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
            .ForAnalyzer<AM003_CollectionTypeIncompatibilityAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule, 21, 13,
                "Tags", "Source", "System.Collections.Generic.HashSet<string>", "Destination",
                "System.Collections.Generic.List<string>")
            .RunAsync();
    }

    [Fact]
    public async Task AM021_Should_Work_ListElementMismatch()
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

        // Let's first test without expectations to see what's happening
        await DiagnosticTestFramework
            .ForAnalyzer<AM021_CollectionElementMismatchAnalyzer>()
            .WithSource(testCode)
            .RunAsync();
    }
}
using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests;

public class AM021_CollectionElementMismatchTests
{
    [Fact]
    public async Task AM021_ShouldReportDiagnostic_WhenCollectionElementTypesIncompatible()
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
            .ForAnalyzer<AM021_CollectionElementMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM021_CollectionElementMismatchAnalyzer.CollectionElementMismatchRule, 20, 29, "List<String>", "List<Int32>")
            .RunAsync();
    }

    [Fact]
    public async Task AM021_ShouldReportDiagnostic_WhenComplexElementTypesMissingMapping()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public int Age { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string FullName { get; set; }
                                        public int Age { get; set; }
                                    }

                                    public class Source
                                    {
                                        public List<SourcePerson> People { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<DestPerson> People { get; set; }
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
            .ForAnalyzer<AM021_CollectionElementMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM021_CollectionElementMismatchAnalyzer.CollectionElementMappingMissingRule, 36, 29, "SourcePerson", "DestPerson")
            .RunAsync();
    }

    [Fact]
    public async Task AM021_ShouldReportDiagnostic_WhenArrayToCollectionElementMismatch()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string[] Tags { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public HashSet<int> Tags { get; set; }
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
            .ForAnalyzer<AM021_CollectionElementMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM021_CollectionElementMismatchAnalyzer.CollectionElementMismatchRule, 20, 29, "String[]", "HashSet<Int32>")
            .RunAsync();
    }

    [Fact]
    public async Task AM021_ShouldReportMultipleDiagnostics_WhenMultipleCollectionIssues()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> StringNumbers { get; set; }
                                        public HashSet<double> DecimalValues { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<int> StringNumbers { get; set; }
                                        public List<string> DecimalValues { get; set; }
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
            .ForAnalyzer<AM021_CollectionElementMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM021_CollectionElementMismatchAnalyzer.CollectionElementMismatchRule, 21, 29)
            .ExpectDiagnostic(AM021_CollectionElementMismatchAnalyzer.CollectionElementMismatchRule, 22, 29)
            .RunAsync();
    }

    [Fact]
    public async Task AM021_ShouldNotReportDiagnostic_WhenElementTypesCompatible()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Names { get; set; }
                                        public HashSet<int> Numbers { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public IEnumerable<string> Names { get; set; }
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
            .ForAnalyzer<AM021_CollectionElementMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM021_ShouldNotReportDiagnostic_WhenExplicitElementMappingProvided()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string FullName { get; set; }
                                    }

                                    public class Source
                                    {
                                        public List<SourcePerson> People { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<DestPerson> People { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                            CreateMap<SourcePerson, DestPerson>()
                                                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.Name));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM021_CollectionElementMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM021_ShouldNotReportDiagnostic_WhenExplicitConversionProvided()
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
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Numbers, opt => opt.MapFrom(src =>
                                                    src.Numbers.Select(x => int.Parse(x)).ToList()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM021_CollectionElementMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM021_ShouldHandleNestedCollections()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<List<string>> Matrix { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<List<int>> Matrix { get; set; }
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
            .ForAnalyzer<AM021_CollectionElementMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM021_CollectionElementMismatchAnalyzer.CollectionElementMismatchRule, 18, 29)
            .RunAsync();
    }

    [Fact]
    public async Task AM021_ShouldIgnoreNonCollectionProperties()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public int Age { get; set; }
                                        public List<string> Tags { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public int Age { get; set; }
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
            .ForAnalyzer<AM021_CollectionElementMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM021_ShouldHandleInheritedCollectionProperties()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class BaseSource
                                    {
                                        public List<string> BaseCollection { get; set; }
                                    }

                                    public class Source : BaseSource
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class BaseDest
                                    {
                                        public List<int> BaseCollection { get; set; }
                                    }

                                    public class Destination : BaseDest
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
            .ForAnalyzer<AM021_CollectionElementMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM021_CollectionElementMismatchAnalyzer.CollectionElementMismatchRule, 28, 29)
            .RunAsync();
    }
}
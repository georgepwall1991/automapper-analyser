using AutoMapperAnalyzer.Analyzers.ComplexMappings;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests.ComplexMappings;

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
            .ExpectDiagnostic(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule, 20, 13,
                "Numbers", "Source", "string", "Destination", "int")
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
            .ExpectDiagnostic(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule, 32, 13,
                "People", "Source", "TestNamespace.SourcePerson", "Destination", "TestNamespace.DestPerson")
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
            .ExpectDiagnostic(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule, 20, 13,
                "Tags", "Source", "string", "Destination", "int")
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

        // NOTE: Analyzer reports both diagnostics, but they point to the same CreateMap invocation line
        // The location is the CreateMap call, not individual property locations
        await DiagnosticTestFramework
            .ForAnalyzer<AM021_CollectionElementMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule, 22, 13,
                "StringNumbers", "Source", "string", "Destination", "int")
            .ExpectDiagnostic(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule, 22, 13,
                "DecimalValues", "Source", "double", "Destination", "string")
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
                                using System.Linq;

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
            .ExpectDiagnostic(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule, 20, 13,
                "Matrix", "Source", "System.Collections.Generic.List<string>", "Destination",
                "System.Collections.Generic.List<int>")
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

        // The analyzer should now correctly detect incompatible element types in inherited properties
        // BaseCollection has List<string> in source but List<int> in destination
        await DiagnosticTestFramework
            .ForAnalyzer<AM021_CollectionElementMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule, 30, 13,
                "BaseCollection", "Source", "string", "Destination", "int")
            .RunAsync();
    }

    [Fact]
    public async Task AM021_ShouldReportDiagnostic_WhenIEnumerableToHashSetWithElementMismatch()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public IEnumerable<string> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public HashSet<int> Values { get; set; }
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
            .ExpectDiagnostic(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule, 20, 13,
                "Values", "Source", "string", "Destination", "int")
            .RunAsync();
    }

    [Fact]
    public async Task AM021_ShouldReportDiagnostic_WhenQueueToStackWithElementMismatch()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Queue<double> Measurements { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Stack<int> Measurements { get; set; }
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
            .ExpectDiagnostic(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule, 20, 13,
                "Measurements", "Source", "double", "Destination", "int")
            .RunAsync();
    }

    [Fact]
    public async Task AM021_ShouldReportDiagnostic_WhenDictionaryValueTypesMismatch()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Dictionary<string, int> Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Dictionary<string, string> Data { get; set; }
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
            .ExpectDiagnostic(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule, 20, 13,
                "Data", "Source", "System.Collections.Generic.KeyValuePair<string, int>", "Destination",
                "System.Collections.Generic.KeyValuePair<string, string>")
            .RunAsync();
    }

    [Fact]
    public async Task AM021_ShouldNotReportDiagnostic_WhenCustomCollectionWithElementMapping()
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

                                    public class PersonCollection : List<SourcePerson> { }

                                    public class Source
                                    {
                                        public PersonCollection People { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<DestPerson> People { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.Name));
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
}

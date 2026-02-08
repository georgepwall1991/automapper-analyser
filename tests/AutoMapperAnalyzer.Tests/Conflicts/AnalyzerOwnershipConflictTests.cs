using AutoMapperAnalyzer.Analyzers.ComplexMappings;
using AutoMapperAnalyzer.Analyzers.TypeSafety;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests.Conflicts;

public class AnalyzerOwnershipConflictTests
{
    [Fact]
    public async Task StringToIntMismatch_ReportsOnlyAM001()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Age { get; set; } = string.Empty;
                                    }

                                    public class Destination
                                    {
                                        public int Age { get; set; }
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
            .ForAnalyzers(
                new AM001_PropertyTypeMismatchAnalyzer(),
                new AM030_CustomTypeConverterAnalyzer())
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule,
                19,
                13,
                "Age",
                "Source",
                "string",
                "Destination",
                "int")
            .RunAsync();
    }

    [Fact]
    public async Task NestedObjectMismatch_ReportsOnlyAM020()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceAddress
                                    {
                                        public string Street { get; set; } = string.Empty;
                                    }

                                    public class DestinationAddress
                                    {
                                        public string Street { get; set; } = string.Empty;
                                    }

                                    public class Source
                                    {
                                        public SourceAddress Address { get; set; } = new();
                                    }

                                    public class Destination
                                    {
                                        public DestinationAddress Address { get; set; } = new();
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
            .ForAnalyzers(
                new AM020_NestedObjectMappingAnalyzer(),
                new AM030_CustomTypeConverterAnalyzer())
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule,
                29,
                13,
                "Address",
                "SourceAddress",
                "DestinationAddress")
            .RunAsync();
    }

    [Fact]
    public async Task CollectionElementMismatch_ReportsOnlyAM021()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class SourceItem
                                    {
                                        public string Name { get; set; } = string.Empty;
                                    }

                                    public class DestinationItem
                                    {
                                        public string Title { get; set; } = string.Empty;
                                    }

                                    public class Source
                                    {
                                        public List<SourceItem> Items { get; set; } = new();
                                    }

                                    public class Destination
                                    {
                                        public List<DestinationItem> Items { get; set; } = new();
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
            .ForAnalyzers(
                new AM003_CollectionTypeIncompatibilityAnalyzer(),
                new AM021_CollectionElementMismatchAnalyzer())
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule,
                30,
                13,
                "Items",
                "Source",
                "TestNamespace.SourceItem",
                "Destination",
                "TestNamespace.DestinationItem")
            .RunAsync();
    }

    [Fact]
    public async Task CollectionContainerMismatch_ReportsOnlyAM003()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public HashSet<string> Tags { get; set; } = new();
                                    }

                                    public class Destination
                                    {
                                        public List<string> Tags { get; set; } = new();
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
            .ForAnalyzers(
                new AM003_CollectionTypeIncompatibilityAnalyzer(),
                new AM021_CollectionElementMismatchAnalyzer())
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
                20,
                13,
                "Tags",
                "Source",
                "System.Collections.Generic.HashSet<string>",
                "Destination",
                "System.Collections.Generic.List<string>")
            .RunAsync();
    }
}

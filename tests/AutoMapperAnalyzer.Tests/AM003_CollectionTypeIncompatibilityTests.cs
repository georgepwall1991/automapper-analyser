using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests;

public class AM003_CollectionTypeIncompatibilityTests
{
    private static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor, int line, int column, params object[] messageArgs)
        => new DiagnosticResult(descriptor).WithLocation(line, column).WithArguments(messageArgs);
    [Fact]
    public async Task AM003_ShouldReportDiagnostic_WhenHashSetToList()
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

        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule, 21, 13,
                "Tags", "Source", "System.Collections.Generic.HashSet<string>", "Destination", "System.Collections.Generic.List<string>"));
    }

    [Fact]
    public async Task AM003_ShouldReportDiagnostic_WhenCollectionElementTypesIncompatible()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Items { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<int> Items { get; set; }
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

        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionElementIncompatibilityRule, 21, 13,
                "Items", "Source", "string", "Destination", "int"));
    }

    [Fact]
    public async Task AM003_ShouldNotReportDiagnostic_WhenCollectionTypesCompatible()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Items { get; set; }
                                        public string[] Tags { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<string> Items { get; set; }
                                        public string[] Tags { get; set; }
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

        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM003_ShouldNotReportDiagnostic_WhenExplicitMappingProvided()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Linq;

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
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags.ToList()));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM003_ShouldReportDiagnostic_WhenQueueToList()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Queue<string> Messages { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<string> Messages { get; set; }
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

        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule, 21, 13,
                "Messages", "Source", "System.Collections.Generic.Queue<string>", "Destination", "System.Collections.Generic.List<string>"));
    }

    [Fact]
    public async Task AM003_ShouldHandleArrayToListCompatibility()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string[] Items { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string[] Items { get; set; }
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

        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM003_ShouldReportDiagnostic_WhenNumericElementTypesIncompatible()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<int> Numbers { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<string> Numbers { get; set; }
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

        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionElementIncompatibilityRule, 21, 13,
                "Numbers", "Source", "int", "Destination", "string"));
    }

    [Fact]
    public async Task AM003_ShouldNotReportDiagnostic_WhenNumericTypesCompatible()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<int> Numbers { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<double> Numbers { get; set; }
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

        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM003_ShouldHandleObservableCollectionCompatibility()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.ObjectModel;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public ObservableCollection<string> Items { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public ObservableCollection<string> Items { get; set; }
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

        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM003_ShouldReportDiagnostic_WhenMultipleCollectionIssues()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public HashSet<string> Tags { get; set; }
                                        public Queue<int> Numbers { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<string> Tags { get; set; }
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

        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule, 23, 13,
                "Tags", "Source", "System.Collections.Generic.HashSet<string>", "Destination", "System.Collections.Generic.List<string>"),
            Diagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule, 23, 13,
                "Numbers", "Source", "System.Collections.Generic.Queue<int>", "Destination", "System.Collections.Generic.List<int>"));
    }
}

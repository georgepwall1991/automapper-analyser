using AutoMapperAnalyzer.Analyzers.TypeSafety;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.TypeSafety;

public class AM003_CollectionTypeIncompatibilityTests
{
    private static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor, int line, int column,
        params object[] messageArgs)
    {
        return new DiagnosticResult(descriptor).WithLocation(line, column).WithArguments(messageArgs);
    }

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
                "Tags", "Source", "System.Collections.Generic.HashSet<string>", "Destination",
                "System.Collections.Generic.List<string>"));
    }

    [Fact]
    public async Task AM003_ShouldNotReportDiagnostic_WhenCollectionElementTypesIncompatible()
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

        // AM021 owns collection element mismatch diagnostics.
        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
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
    public async Task AM003_ShouldNotReportDiagnostic_WhenExplicitMappingProvidedWithStringDestinationMember()
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
                                                .ForMember("Tags", opt => opt.MapFrom(src => src.Tags.ToList()));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM003_ShouldNotReportDiagnostic_WhenExplicitMappingProvidedWithNameofDestinationMember()
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
                                                .ForMember(nameof(Destination.Tags), opt => opt.MapFrom(src => src.Tags.ToList()));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM003_ShouldNotReportDiagnostic_WhenExplicitMappingProvidedWithConstantDestinationMember()
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
                                        private const string TagsMember = nameof(Destination.Tags);

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(TagsMember, opt => opt.MapFrom(src => src.Tags.ToList()));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM003_ShouldNotReportDiagnostic_WhenConstructUsingHandlesCollectionConversion()
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
                                                .ConstructUsing(src => new Destination { Tags = src.Tags.ToList() });
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
                "Messages", "Source", "System.Collections.Generic.Queue<string>", "Destination",
                "System.Collections.Generic.List<string>"));
    }

    [Fact]
    public async Task AM003_ShouldReportDiagnostic_WhenListToImmutableList()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Collections.Immutable;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Tags { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public ImmutableList<string> Tags { get; set; }
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
            Diagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule, 22, 13,
                "Tags", "Source", "System.Collections.Generic.List<string>", "Destination",
                "System.Collections.Immutable.ImmutableList<string>"));
    }

    [Fact]
    public async Task AM003_ShouldReportDiagnostic_WhenListToImmutableArray()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Collections.Immutable;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Tags { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public ImmutableArray<string> Tags { get; set; }
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
            Diagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule, 22, 13,
                "Tags", "Source", "System.Collections.Generic.List<string>", "Destination",
                "System.Collections.Immutable.ImmutableArray<string>"));
    }

    [Fact]
    public async Task AM003_ShouldReportDiagnostic_WhenImmutableArrayToList()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Collections.Immutable;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public ImmutableArray<string> Tags { get; set; }
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
            Diagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule, 22, 13,
                "Tags", "Source", "System.Collections.Immutable.ImmutableArray<string>", "Destination",
                "System.Collections.Generic.List<string>"));
    }

    [Fact]
    public async Task AM003_ShouldReportDiagnostic_WhenQueueToFrozenSet()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Frozen;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Queue<int> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public FrozenSet<int> Values { get; set; }
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
            Diagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule, 22, 13,
                "Values", "Source", "System.Collections.Generic.Queue<int>", "Destination",
                "System.Collections.Frozen.FrozenSet<int>"));
    }

    [Fact]
    public async Task AM003_ShouldReportDiagnostic_WhenListToSortedSetOrLinkedList()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<int> SortedValues { get; set; }
                                        public List<string> LinkedValues { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public SortedSet<int> SortedValues { get; set; }
                                        public LinkedList<string> LinkedValues { get; set; }
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
                "SortedValues", "Source", "System.Collections.Generic.List<int>", "Destination",
                "System.Collections.Generic.SortedSet<int>"),
            Diagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule, 23, 13,
                "LinkedValues", "Source", "System.Collections.Generic.List<string>", "Destination",
                "System.Collections.Generic.LinkedList<string>"));
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
    public async Task AM003_ShouldNotReportDiagnostic_WhenSourceCollectionAssignableToDestinationInterface()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string[] Tags { get; set; }
                                        public HashSet<int> Numbers { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public IEnumerable<string> Tags { get; set; }
                                        public IReadOnlyCollection<int> Numbers { get; set; }
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
    public async Task AM003_ShouldReportReverseMapDiagnostic_WhenReverseCollectionNotAssignable()
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
                                        public IEnumerable<string> Tags { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ReverseMap();
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule, 21, 13,
                "Tags", "Destination", "System.Collections.Generic.IEnumerable<string>", "Source",
                "System.Collections.Generic.HashSet<string>"));
    }

    [Fact]
    public async Task AM003_ShouldNotReportDiagnostic_WhenNumericElementTypesIncompatible()
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

        // AM021 owns collection element mismatch diagnostics.
        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
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
                "Tags", "Source", "System.Collections.Generic.HashSet<string>", "Destination",
                "System.Collections.Generic.List<string>"),
            Diagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule, 23, 13,
                "Numbers", "Source", "System.Collections.Generic.Queue<int>", "Destination",
                "System.Collections.Generic.List<int>"));
    }

    [Fact]
    public async Task AM003_ShouldNotReportDiagnostic_ForCreateMapLikeApiOutsideAutoMapper()
    {
        const string testCode = """

                                using System;
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

                                    public class FakeMapOptions<TSource, TDestMember>
                                    {
                                        public void MapFrom(Func<TSource, TDestMember> resolver)
                                        {
                                        }
                                    }

                                    public class FakeMapExpression<TSource, TDestination>
                                    {
                                        public FakeMapExpression<TSource, TDestination> ForMember<TDestMember>(
                                            Func<TDestination, TDestMember> destinationMember,
                                            Action<FakeMapOptions<TSource, TDestMember>> optionsAction)
                                        {
                                            return this;
                                        }
                                    }

                                    public class Profile
                                    {
                                        public FakeMapExpression<TSource, TDestination> CreateMap<TSource, TDestination>() => new();
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
    public async Task AM003_ShouldNotReportDiagnostic_WhenExplicitMappingProvidedWithParenthesizedLambda()
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
                                                .ForMember((dest) => dest.Tags, (opt) => opt.MapFrom((src) => src.Tags.ToList()));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM003_ShouldNotReportDiagnostic_WhenExplicitMappingProvidedWithTypedLambdaParameters()
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
                                                .ForMember((Destination dest) => dest.Tags,
                                                    (opt) => opt.MapFrom((Source src) => src.Tags.ToList()));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM003_ShouldNotReportDiagnostic_WhenUserDefinedImplicitContainerConversionExists()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class DestinationTags : List<string>
                                    {
                                        public static implicit operator DestinationTags(HashSet<string> tags)
                                        {
                                            var destination = new DestinationTags();
                                            destination.AddRange(tags);
                                            return destination;
                                        }
                                    }

                                    public class Source
                                    {
                                        public HashSet<string> Tags { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public DestinationTags Tags { get; set; }
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
    public async Task AM003_ShouldReportDiagnostic_WhenOnlyUserDefinedExplicitContainerConversionExists()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class DestinationTags : List<string>
                                    {
                                        public static explicit operator DestinationTags(HashSet<string> tags)
                                        {
                                            var destination = new DestinationTags();
                                            destination.AddRange(tags);
                                            return destination;
                                        }
                                    }

                                    public class Source
                                    {
                                        public HashSet<string> Tags { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public DestinationTags Tags { get; set; }
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
            Diagnostic(
                AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
                31,
                13,
                "Tags",
                "Source",
                "System.Collections.Generic.HashSet<string>",
                "Destination",
                "TestNamespace.DestinationTags"));
    }

    [Fact]
    public async Task AM003_ShouldNotReportDiagnostic_ForCustomCollectionTypeNameContainingStack()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class StackCollection<T> : List<T>
                                    {
                                    }

                                    public class Source
                                    {
                                        public StackCollection<int> Items { get; set; }
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

        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }
}

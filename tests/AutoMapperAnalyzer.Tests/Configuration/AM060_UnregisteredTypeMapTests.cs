using AutoMapperAnalyzer.Analyzers.Configuration;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.Configuration;

public class AM060_UnregisteredTypeMapTests
{
    [Fact]
    public async Task Should_ReportDiagnostic_When_SingleGenericMapHasNoRegistration()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}
                                public class Other {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Other>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, Source source)
                                    {
                                        var destination = mapper.Map<Destination>(source);
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM060_UnregisteredTypeMapAnalyzer.UnregisteredTypeMapRule)
            .WithLocation(19, 34)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_TwoGenericMapHasNoRegistration()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}
                                public class Other {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Other>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, Source source)
                                    {
                                        var destination = mapper.Map<Source, Destination>(source);
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM060_UnregisteredTypeMapAnalyzer.UnregisteredTypeMapRule)
            .WithLocation(19, 34)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_NonGenericMapWithTypesHasNoRegistration()
    {
        const string testCode = """
                                using System;
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}
                                public class Other {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Other>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, object source)
                                    {
                                        object destination = mapper.Map(source, typeof(Source), typeof(Destination));
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM060_UnregisteredTypeMapAnalyzer.UnregisteredTypeMapRule)
            .WithLocation(20, 37)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_ProjectToHasNoRegistration()
    {
        const string testCode = """
                                using System.Linq;
                                using AutoMapper;
                                using AutoMapper.QueryableExtensions;

                                public class Source {}
                                public class Destination {}
                                public class Other {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Other>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IQueryable<Source> query, IConfigurationProvider configuration)
                                    {
                                        var destination = query.ProjectTo<Destination>(configuration).ToList();
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM060_UnregisteredTypeMapAnalyzer.UnregisteredTypeMapRule)
            .WithLocation(21, 33)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_CollectionElementMapIsMissing()
    {
        const string testCode = """
                                using System.Collections.Generic;
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Source>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, List<Source> sources)
                                    {
                                        var destination = mapper.Map<List<Destination>>(sources);
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM060_UnregisteredTypeMapAnalyzer.UnregisteredTypeMapRule)
            .WithLocation(19, 34)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_MappingIsRegistered()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, Source source)
                                    {
                                        var destination = mapper.Map<Destination>(source);
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_ReverseMapCoversDirection()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Destination, Source>().ReverseMap();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, Source source)
                                    {
                                        var destination = mapper.Map<Destination>(source);
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_BaseTypeRegistrationCoversCall()
    {
        const string testCode = """
                                using AutoMapper;

                                public class SourceBase {}
                                public class SourceDerived : SourceBase {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<SourceBase, Destination>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, SourceDerived source)
                                    {
                                        var destination = mapper.Map<Destination>(source);
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_InterfaceRegistrationCoversCall()
    {
        const string testCode = """
                                using AutoMapper;

                                public interface ISource {}
                                public class Source : ISource {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<ISource, Destination>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, Source source)
                                    {
                                        var destination = mapper.Map<Destination>(source);
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_CollectionElementMapIsRegistered()
    {
        const string testCode = """
                                using System.Collections.Generic;
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, List<Source> sources)
                                    {
                                        var destination = mapper.Map<List<Destination>>(sources);
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_ForSimpleTypeMappings()
    {
        const string testCode = """
                                using System;
                                using AutoMapper;

                                public class Source {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Source>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, int value, string text)
                                    {
                                        string asString = mapper.Map<string>(value);
                                        DateTime asDate = mapper.Map<DateTime>(text);
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_ForEnumMappings()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1 }
                                public enum DestinationStatus { Active = 1 }
                                public class Source {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Source>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, SourceStatus status)
                                    {
                                        var mapped = mapper.Map<DestinationStatus>(status);
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_ForDictionaryMappings()
    {
        const string testCode = """
                                using System.Collections.Generic;
                                using AutoMapper;

                                public class Source {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Source>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, Dictionary<string, int> values)
                                    {
                                        var mapped = mapper.Map<Dictionary<string, long>>(values);
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_ForIdentityMapping()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Source>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, Source source)
                                    {
                                        var clone = mapper.Map<Source>(source);
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_CompilationHasNoRegistrations()
    {
        // Projects that contain no CreateMap at all normally consume maps configured in another
        // assembly; absence cannot be proven, so the rule stays silent.
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class Service
                                {
                                    public void Run(IMapper mapper, Source source)
                                    {
                                        var destination = mapper.Map<Destination>(source);
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_ForLookalikeMapMethod()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}
                                public class Other {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Other>();
                                    }
                                }

                                public static class CustomMapper
                                {
                                    public static TDestination Map<TDestination>(object source) => default!;
                                }

                                public class Service
                                {
                                    public void Run(Source source)
                                    {
                                        var destination = CustomMapper.Map<Destination>(source);
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_NonGenericMapUsesReorderedNamedArguments()
    {
        const string testCode = """
                                using System;
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}
                                public class Other {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Other>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, object source)
                                    {
                                        object destination = mapper.Map(
                                            source,
                                            destinationType: typeof(Destination),
                                            sourceType: typeof(Source));
                                    }
                                }
                                """;

        // Named-argument reordering must not swap the resolved pair.
        DiagnosticResult expected = new DiagnosticResult(AM060_UnregisteredTypeMapAnalyzer.UnregisteredTypeMapRule)
            .WithLocation(20, 37)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_InstanceProjectToHasNoRegistration()
    {
        const string testCode = """
                                using System.Linq;
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}
                                public class Other {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Other>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, IQueryable<Source> query)
                                    {
                                        var destination = mapper.ProjectTo<Destination>(query).ToList();
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM060_UnregisteredTypeMapAnalyzer.UnregisteredTypeMapRule)
            .WithLocation(20, 34)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_ComplexSourceMapsToEnumWithoutRegistration()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum DestinationStatus { Active = 1 }
                                public class Source {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Source>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, Source source)
                                    {
                                        var status = mapper.Map<DestinationStatus>(source);
                                    }
                                }
                                """;

        // One-sided simple pairs (complex -> enum) still require an explicit map.
        DiagnosticResult expected = new DiagnosticResult(AM060_UnregisteredTypeMapAnalyzer.UnregisteredTypeMapRule)
            .WithLocation(18, 29)
            .WithArguments("Source", "DestinationStatus");

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_DestinationIsAssignableFromSource()
    {
        const string testCode = """
                                using AutoMapper;

                                public class SourceBase {}
                                public class SourceDerived : SourceBase {}
                                public class Other {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Other, Other>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, SourceDerived source)
                                    {
                                        var upcast = mapper.Map<SourceBase>(source);
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_CollectionElementsAreAssignable()
    {
        const string testCode = """
                                using System.Collections.Generic;
                                using AutoMapper;

                                public class SourceBase {}
                                public class SourceDerived : SourceBase {}
                                public class Other {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Other, Other>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, List<SourceDerived> sources)
                                    {
                                        var upcast = mapper.Map<List<SourceBase>>(sources);
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_ClosedTypeOfRegistrationExists()
    {
        const string testCode = """
                                using System;
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap(typeof(Source), typeof(Destination));
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, Source source)
                                    {
                                        var destination = mapper.Map<Destination>(source);
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_GenericRegistrationHelperExists()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        AddMap<Source, Destination>();
                                    }

                                    private void AddMap<TSource, TDestination>()
                                        where TSource : class
                                        where TDestination : class
                                    {
                                        CreateMap<TSource, TDestination>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, Source source)
                                    {
                                        var destination = mapper.Map<Destination>(source);
                                    }
                                }
                                """;

        // Generic helper registrations cannot be expanded to concrete pairs statically, so the
        // rule fails closed for the whole compilation.
        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_OpenGenericRegistrationExists()
    {
        const string testCode = """
                                using System;
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}
                                public class Wrapper<T> {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap(typeof(Wrapper<>), typeof(Destination));
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, Source source)
                                    {
                                        var destination = mapper.Map<Destination>(source);
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM060_UnregisteredTypeMapAnalyzer>.VerifyAnalyzerAsync(testCode);
    }
}

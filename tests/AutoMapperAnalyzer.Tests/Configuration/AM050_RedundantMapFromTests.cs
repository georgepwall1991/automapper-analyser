using AutoMapperAnalyzer.Analyzers.Configuration;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.Configuration;

public class AM050_RedundantMapFromTests
{
    [Fact]
    public async Task Should_ReportDiagnostic_When_MappingSamePropertyName()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string Name { get; set; } }
                                public class Destination { public string Name { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name));
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
            .WithLocation(11, 42) // Points to MapFrom call
            .WithArguments("Name");

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_TopLevelForPathMapsSamePropertyName()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string Name { get; set; } }
                                public class Destination { public string Name { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForPath(d => d.Name, o => o.MapFrom(s => s.Name));
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
            .WithLocation(11, 40)
            .WithArguments("Name");

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_ForPathMapsNestedDestinationPath()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string Name { get; set; } }
                                public class Destination { public DestinationChild Child { get; set; } }
                                public class DestinationChild { public string Name { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForPath(d => d.Child.Name, o => o.MapFrom(s => s.Name));
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_LambdaBodiesParenthesizeMatchingMembers()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string Name { get; set; } }
                                public class Destination { public string Name { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => (d.Name), o => o.MapFrom(s => (s.Name)));
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
            .WithLocation(11, 44)
            .WithArguments("Name");

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_MapFromUsesParenthesizedLambda()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string Name { get; set; } }
                                public class Destination { public string Name { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.Name, o => o.MapFrom((s) => s.Name));
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
            .WithLocation(11, 42)
            .WithArguments("Name");

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_MapFromUsesTypedParenthesizedLambda()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string Name { get; set; } }
                                public class Destination { public string Name { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.Name, o => o.MapFrom((Source s) => s.Name));
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
            .WithLocation(11, 42)
            .WithArguments("Name");

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_ParenthesizedLambdaMapsDifferentProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string OtherName { get; set; } }
                                public class Destination { public string Name { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.Name, o => o.MapFrom((Source s) => s.OtherName));
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_MappingDifferentProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string OtherName { get; set; } }
                                public class Destination { public string Name { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.Name, o => o.MapFrom(s => s.OtherName));
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_StringDestinationMemberTypeMatches()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string Name { get; set; } }
                                public class Destination { public string Name { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember("Name", o => o.MapFrom(s => s.Name));
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
            .WithLocation(11, 37)
            .WithArguments("Name");

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_StringDestinationMemberUsesNameof()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string Name { get; set; } }
                                public class Destination { public string Name { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(nameof(Destination.Name), o => o.MapFrom(s => s.Name));
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
            .WithLocation(11, 55)
            .WithArguments("Name");

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_StringDestinationMemberUsesConstant()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string Name { get; set; } }
                                public class Destination { public string Name { get; set; } }

                                public class MyProfile : Profile
                                {
                                    private const string NameMember = nameof(Destination.Name);

                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(NameMember, o => o.MapFrom(s => s.Name));
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
            .WithLocation(13, 41)
            .WithArguments("Name");

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_StringDestinationMemberTypeDiffers()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string Name { get; set; } }
                                public class Destination { public int Name { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember("Name", o => o.MapFrom(s => s.Name));
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_ReverseMapStringDestinationMemberTypeDiffers()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public int Name { get; set; } }
                                public class Destination { public string Name { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ReverseMap()
                                            .ForMember("Name", o => o.MapFrom(s => s.Name));
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_ReverseMapNameofDestinationMemberIsRedundant()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string Name { get; set; } }
                                public class Destination { public string Name { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ReverseMap()
                                            .ForMember(nameof(Source.Name), o => o.MapFrom(d => d.Name));
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
            .WithLocation(12, 50)
            .WithArguments("Name");

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_ReverseMapConstDestinationMemberIsRedundant()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string Name { get; set; } }
                                public class Destination { public string Name { get; set; } }

                                public class MyProfile : Profile
                                {
                                    private const string NameMember = nameof(Source.Name);

                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ReverseMap()
                                            .ForMember(NameMember, o => o.MapFrom(d => d.Name));
                                    }
                                }
                                """;

        DiagnosticResult expected = new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
            .WithLocation(14, 41)
            .WithArguments("Name");

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_NullableReferenceSourceMapsToNonNullableDestination()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public class Source { public string? Name { get; set; } }
                                public class Destination { public string Name { get; set; } = ""; }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name));
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_MappingExpression()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string Name { get; set; } }
                                public class Destination { public string Name { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name.ToUpper()));
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_MappingFromCapturedVariable()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string Name { get; set; } }
                                public class Destination { public string Name { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        var other = new Source { Name = "captured" };
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.Name, o => o.MapFrom(s => other.Name));
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_ForNonAutoMapperForMemberAndMapFrom()
    {
        const string testCode = """
                                using System;

                                public class Source { public string Name { get; set; } }
                                public class Destination { public string Name { get; set; } }

                                public class FakeOptions<TSource, TDestination>
                                {
                                    public FakeOptions<TSource, TDestination> MapFrom(Func<TSource, object> map)
                                    {
                                        return this;
                                    }
                                }

                                public class FakeBuilder<TSource, TDestination>
                                {
                                    public FakeBuilder<TSource, TDestination> ForMember(
                                        Func<TDestination, object> destination,
                                        Func<FakeOptions<TSource, TDestination>, FakeOptions<TSource, TDestination>> config)
                                    {
                                        return this;
                                    }
                                }

                                public class FakeProfile
                                {
                                    public FakeBuilder<TSource, TDestination> CreateMap<TSource, TDestination>()
                                    {
                                        return new FakeBuilder<TSource, TDestination>();
                                    }
                                }

                                public class Consumer
                                {
                                    public void Configure()
                                    {
                                        new FakeProfile()
                                            .CreateMap<Source, Destination>()
                                            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name));
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>.VerifyAnalyzerAsync(testCode);
    }
}

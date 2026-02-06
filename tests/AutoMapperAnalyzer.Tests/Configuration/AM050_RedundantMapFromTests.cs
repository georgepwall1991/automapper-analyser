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

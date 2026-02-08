using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

public class AM006_UnmappedDestinationPropertyTests
{
    [Fact]
    public async Task AM006_ShouldReportDiagnostic_ForNonRequiredUnmappedDestinationProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string ExtraInfo { get; set; }
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
            .ForAnalyzer<AM006_UnmappedDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule, 20, 13,
                "ExtraInfo", "Source")
            .RunAsync();
    }

    [Fact]
    public async Task AM006_ShouldNotReportDiagnostic_ForRequiredUnmappedDestinationProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public required string RequiredField { get; set; }
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
            .ForAnalyzer<AM006_UnmappedDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM006_ShouldNotReportDiagnostic_WhenDestinationPropertyConfiguredWithForMember()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Info { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string ExtraInfo { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.ExtraInfo, opt => opt.MapFrom(src => src.Info));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM006_UnmappedDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM006_ShouldNotReportDiagnostic_ForCreateMapLikeApiOutsideAutoMapper()
    {
        const string testCode = """
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string ExtraInfo { get; set; }
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM006_UnmappedDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM006_ShouldNotReportDiagnostic_WhenDestinationPropertyConfiguredWithParenthesizedForMember()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Info { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string ExtraInfo { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember((dest) => dest.ExtraInfo, (opt) => opt.MapFrom((src) => src.Info));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM006_UnmappedDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM006_ShouldReportDiagnostic_WhenFlatteningPrefixMatchesButNestedMemberDoesNotExist()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Customer
                                    {
                                        public string FirstName { get; set; }
                                    }

                                    public class Source
                                    {
                                        public Customer Customer { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string CustomerAge { get; set; }
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
            .ForAnalyzer<AM006_UnmappedDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule, 24, 13,
                "CustomerAge", "Source")
            .RunAsync();
    }

    [Fact]
    public async Task AM006_ShouldReportDiagnostic_WhenOnlyReverseMapConfiguresProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        // ProtectedOrInternal is intentionally excluded by GetMappableProperties.
                                        protected internal string ReverseOnly { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string ReverseOnly { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ReverseMap()
                                                .ForMember(src => src.ReverseOnly, opt => opt.MapFrom(dest => dest.ReverseOnly));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM006_UnmappedDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule, 22, 13,
                "ReverseOnly", "Source")
            .RunAsync();
    }
}

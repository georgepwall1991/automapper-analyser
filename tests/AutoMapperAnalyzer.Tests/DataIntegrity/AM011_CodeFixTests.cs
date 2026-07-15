using AutoMapper;
using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

public class AM011_CodeFixTests
{
    [Fact]
    public async Task AM011_ShouldOfferIgnore_WhenNoFuzzyStringMatch()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
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

        const string expectedFixedCode = """

                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                                 public string Email { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public string Email { get; set; }
                                                 public required string RequiredField { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredField, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(16, 32)
                    .WithArguments("RequiredField"),
                expectedFixedCode,
                0); // No proven source match: require an explicit manual-review Ignore.
    }

    [Fact]
    public async Task AM011_ShouldOfferIgnore_WhenNoFuzzyNumericMatch()
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
                                        public required int RequiredNumber { get; set; }
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

        const string expectedFixedCode = """

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
                                                 public required int RequiredNumber { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredNumber, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(14, 29)
                    .WithArguments("RequiredNumber"),
                expectedFixedCode,
                0); // No proven source match: do not manufacture a numeric default.
    }

    [Fact]
    public async Task AM011_ShouldOfferIgnore_WhenNoFuzzySourceExists()
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

        const string expectedBulkFixedCode = """

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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredField, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(14, 32)
                    .WithArguments("RequiredField"),
                expectedBulkFixedCode,
                0); // No proven source match: require an explicit manual-review Ignore.
    }

    [Fact]
    public async Task AM011_ShouldOfferIgnore_WhenSourceNameIsNotFuzzy()
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
                                        public required string RequiredDescription { get; set; }
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

        const string expectedFixedCode = """

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
                                                 public required string RequiredDescription { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredDescription, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(14, 32)
                    .WithArguments("RequiredDescription"),
                expectedFixedCode,
                0); // Distant names are not enough evidence to invent a mapping.
    }

    [Fact]
    public async Task AM011_ShouldHandleMultipleRequiredProperties()
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
                                        public required string RequiredField1 { get; set; }
                                        public required string RequiredField2 { get; set; }
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

        const string expectedFixedCode = """
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
                                                 public required string RequiredField1 { get; set; }
                                                 public required string RequiredField2 { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredField1, opt => opt.MapFrom(src => string.Empty)).ForMember(dest => dest.RequiredField2, opt => opt.MapFrom(src => string.Empty));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[]
                {
                    new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                        .WithLocation(13, 32)
                        .WithArguments("RequiredField1"),
                    new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                        .WithLocation(14, 32)
                        .WithArguments("RequiredField2")
                },
                expectedFixedCode,
                0, 1); // Aggregate map-all action keeps destination declaration order.
    }

    [Fact]
    public async Task AM011_ShouldIgnoreRequiredBool_WhenNoFuzzyMatch()
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
                                        public required bool RequiredFlag { get; set; }
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

        const string expectedFixedCode = """
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
                                                 public required bool RequiredFlag { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredFlag, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(13, 30)
                    .WithArguments("RequiredFlag"),
                expectedFixedCode,
                0); // No proven source match: do not manufacture false.
    }

    [Fact]
    public async Task AM011_ShouldIgnoreRequiredDecimal_WhenNoFuzzyMatch()
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
                                        public required decimal RequiredPrice { get; set; }
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

        const string expectedFixedCode = """
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
                                                 public required decimal RequiredPrice { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredPrice, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(13, 33)
                    .WithArguments("RequiredPrice"),
                expectedFixedCode,
                0); // No proven source match: do not manufacture a decimal default.
    }

    [Fact]
    public async Task AM011_ShouldWithholdDefaultScaffold_WhenBestCandidateIsAmbiguous()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Eamil { get; set; }
                                        public string Emial { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public required string Email { get; set; }
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

        const string expectedFixedCode = """

                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Eamil { get; set; }
                                                 public string Emial { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public required string Email { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Email, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(14, 32)
                    .WithArguments("Email"),
                expectedFixedCode,
                0); // No unique fuzzy match: require an explicit manual-review Ignore.
    }

    [Fact]
    public async Task AM011_ShouldFuzzyMap_WhenReverseMapRequiredPropertyHasSimilarSource()
    {
        // ReverseMap anchors diagnostics on ReverseMap(); per-property fuzzy must resolve
        // swapped source/destination types (not GetCreateMapTypeArguments on ReverseMap alone).
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class A
                                    {
                                        public required string FullNam { get; set; }
                                    }

                                    public class B
                                    {
                                        public string FullName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<A, B>().ReverseMap();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class A
                                             {
                                                 public required string FullNam { get; set; }
                                             }

                                             public class B
                                             {
                                                 public string FullName { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<A, B>().ReverseMap().ForMember(dest => dest.FullNam, opt => opt.MapFrom(src => src.FullName));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(7, 32)
                    .WithArguments("FullNam"),
                expectedFixedCode,
                0);
    }

}

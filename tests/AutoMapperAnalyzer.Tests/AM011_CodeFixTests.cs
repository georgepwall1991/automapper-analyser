using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests;

public class AM011_CodeFixTests
{
    [Fact]
    public async Task AM011_ShouldAddDefaultValueMapping()
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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredField, opt => opt.MapFrom(src => string.Empty));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(23, 13)
                    .WithArguments("RequiredField"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM011_ShouldAddConstantValueMapping()
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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredNumber, opt => opt.MapFrom(src => 0));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(21, 13)
                    .WithArguments("RequiredNumber"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM011_ShouldAddSourcePropertySuggestionComment()
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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredDescription, opt => opt.MapFrom(src => string.Empty));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(21, 13)
                    .WithArguments("RequiredDescription"),
                expectedFixedCode);
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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredField2, opt => opt.MapFrom(src => string.Empty)).ForMember(dest => dest.RequiredField1, opt => opt.MapFrom(src => string.Empty));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new [] {
                    new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                        .WithLocation(21, 13)
                        .WithArguments("RequiredField1"),
                    new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                        .WithLocation(21, 13)
                        .WithArguments("RequiredField2")
                },
                expectedFixedCode);
    }

    [Fact]
    public async Task AM011_ShouldHandleRequiredBoolProperty()
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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredFlag, opt => opt.MapFrom(src => false));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(20, 13)
                    .WithArguments("RequiredFlag"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM011_ShouldHandleRequiredDecimalProperty()
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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.RequiredPrice, opt => opt.MapFrom(src => 0m));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM011_UnmappedRequiredPropertyAnalyzer, AM011_UnmappedRequiredPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule)
                    .WithLocation(20, 13)
                    .WithArguments("RequiredPrice"),
                expectedFixedCode);
    }
}

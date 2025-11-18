using AutoMapperAnalyzer.Analyzers.ComplexMappings;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.ComplexMappings;

public class AM030_CodeFixTests
{
    [Fact]
    public async Task AM030_ShouldFixMissingConvertUsingWithLambda()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string CreatedDate { get; set; } = "2023-01-01";
                                    }

                                    public class Destination
                                    {
                                        public DateTime CreatedDate { get; set; }
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
                                         using System;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string CreatedDate { get; set; } = "2023-01-01";
                                             }

                                             public class Destination
                                             {
                                                 public DateTime CreatedDate { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(src => DateTime.Parse(src.CreatedDate)));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM030_CustomTypeConverterAnalyzer, AM030_CustomTypeConverterCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule)
                    .WithLocation(20, 13)
                    .WithArguments("CreatedDate", "ITypeConverter<String, DateTime>"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM030_ShouldFixMissingConvertUsingWithConverter()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Price { get; set; } = "19.99";
                                    }

                                    public class Destination
                                    {
                                        public decimal Price { get; set; }
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
                                         using System;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Price { get; set; } = "19.99";
                                             }

                                             public class Destination
                                             {
                                                 public decimal Price { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Price, opt => opt.MapFrom(src => decimal.Parse(src.Price)));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM030_CustomTypeConverterAnalyzer, AM030_CustomTypeConverterCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule)
                    .WithLocation(20, 13)
                    .WithArguments("Price", "ITypeConverter<String, Decimal>"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM030_ShouldFixMissingConvertUsingWithForMember()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string UpdatedDate { get; set; } = "2023-01-02";
                                    }

                                    public class Destination
                                    {
                                        public DateTime UpdatedDate { get; set; }
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
                                         using System;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string UpdatedDate { get; set; } = "2023-01-02";
                                             }

                                             public class Destination
                                             {
                                                 public DateTime UpdatedDate { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.UpdatedDate, opt => opt.MapFrom(src => DateTime.Parse(src.UpdatedDate)));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM030_CustomTypeConverterAnalyzer, AM030_CustomTypeConverterCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule)
                    .WithLocation(20, 13)
                    .WithArguments("UpdatedDate", "ITypeConverter<String, DateTime>"),
                expectedFixedCode);
}
}

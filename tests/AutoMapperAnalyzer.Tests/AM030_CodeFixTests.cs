using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.CodeFixes;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests;

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
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.CreatedDate, opt => opt.ConvertUsing(src => string.IsNullOrEmpty(src.CreatedDate) ? DateTime.MinValue : DateTime.Parse(src.CreatedDate)));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithCodeFix<AM030_CustomTypeConverterCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule, 20, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
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
                                                     CreateMap<Source, Destination>()
                                                         .ConvertUsing<StringToDecimalConverter>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithCodeFix<AM030_CustomTypeConverterCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule, 20, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
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
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.UpdatedDate, opt => opt.ConvertUsing<StringToDateTimeConverter>());
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithCodeFix<AM030_CustomTypeConverterCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule, 20, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }
}
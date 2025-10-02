using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests;

public class AM002_CodeFixTests
{
    [Fact]
    public async Task AM002_ShouldFixNullableToNonNullableWithNullCoalescing()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string? Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
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
                                                 public string? Name { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? default));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM002_NullableCompatibilityAnalyzer>()
            .WithCodeFix<AM002_NullableCompatibilityCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 14, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM002_ShouldFixNullableIntToNonNullableWithDefault()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int? Age { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Age { get; set; }
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
                                                 public int? Age { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public int Age { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age ?? default));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM002_NullableCompatibilityAnalyzer>()
            .WithCodeFix<AM002_NullableCompatibilityCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 14, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM002_ShouldFixNullableBoolToNonNullable()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public bool? IsActive { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool IsActive { get; set; }
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
                                                 public bool? IsActive { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public bool IsActive { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive ?? default));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM002_NullableCompatibilityAnalyzer>()
            .WithCodeFix<AM002_NullableCompatibilityCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 14, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM002_ShouldFixMultipleNullableProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string? Name { get; set; }
                                        public int? Age { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public int Age { get; set; }
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

        // Fix the first property (Name)
        const string expectedFixedCodeAfterFirstFix = """
                                                       using AutoMapper;

                                                       namespace TestNamespace
                                                       {
                                                           public class Source
                                                           {
                                                               public string? Name { get; set; }
                                                               public int? Age { get; set; }
                                                           }

                                                           public class Destination
                                                           {
                                                               public string Name { get; set; }
                                                               public int Age { get; set; }
                                                           }

                                                           public class TestProfile : Profile
                                                           {
                                                               public TestProfile()
                                                               {
                                                                   CreateMap<Source, Destination>()
                                                                       .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? default));
                                                               }
                                                           }
                                                       }
                                                       """;

        await CodeFixTestFramework
            .ForAnalyzer<AM002_NullableCompatibilityAnalyzer>()
            .WithCodeFix<AM002_NullableCompatibilityCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 15, 13)
            .ExpectFixedCode(expectedFixedCodeAfterFirstFix)
            .RunAsync();
    }

    [Fact]
    public async Task AM002_ShouldFixNullableIntWithDefaultCoalescing()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int? Count { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Count { get; set; }
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
                                                 public int? Count { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public int Count { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Count, opt => opt.MapFrom(src => src.Count ?? default));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM002_NullableCompatibilityAnalyzer>()
            .WithCodeFix<AM002_NullableCompatibilityCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 14, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM002_ShouldFixNullableDecimalWithDefaultCoalescing()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public decimal? Amount { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public decimal Amount { get; set; }
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
                                                 public decimal? Amount { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public decimal Amount { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount ?? default));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM002_NullableCompatibilityAnalyzer>()
            .WithCodeFix<AM002_NullableCompatibilityCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 14, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }
}

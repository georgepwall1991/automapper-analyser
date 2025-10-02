using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests;

public class AM001_CodeFixTests
{
    [Fact]
    public async Task AM001_ShouldFixPropertyTypeMismatchWithToString()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Age { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Age { get; set; }
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
                                                 public int Age { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Age { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age.ToString()));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM001_PropertyTypeMismatchAnalyzer>()
            .WithCodeFix<AM001_PropertyTypeMismatchCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 14, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM001_ShouldFixNullableCompatibilityWithNullCoalescing()
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
            .ForAnalyzer<AM001_PropertyTypeMismatchAnalyzer>()
            .WithCodeFix<AM001_PropertyTypeMismatchCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM001_PropertyTypeMismatchAnalyzer.NullableCompatibilityRule, 14, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM001_ShouldFixGenericTypeMismatchWithConversion()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<int> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<string> Values { get; set; }
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
                                         using System.Collections.Generic;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public List<int> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public List<string> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Values, opt => opt.MapFrom(src => src.Values.ToString()));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM001_PropertyTypeMismatchAnalyzer>()
            .WithCodeFix<AM001_PropertyTypeMismatchCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM001_PropertyTypeMismatchAnalyzer.GenericTypeMismatchRule, 15, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM001_ShouldFixMultiplePropertyTypeMismatches()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Age { get; set; }
                                        public double Score { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Age { get; set; }
                                        public string Score { get; set; }
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

        // Fix the first property (Age)
        const string expectedFixedCodeAfterFirstFix = """
                                                       using AutoMapper;

                                                       namespace TestNamespace
                                                       {
                                                           public class Source
                                                           {
                                                               public int Age { get; set; }
                                                               public double Score { get; set; }
                                                           }

                                                           public class Destination
                                                           {
                                                               public string Age { get; set; }
                                                               public string Score { get; set; }
                                                           }

                                                           public class TestProfile : Profile
                                                           {
                                                               public TestProfile()
                                                               {
                                                                   CreateMap<Source, Destination>()
                                                                       .ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age.ToString()));
                                                               }
                                                           }
                                                       }
                                                       """;

        await CodeFixTestFramework
            .ForAnalyzer<AM001_PropertyTypeMismatchAnalyzer>()
            .WithCodeFix<AM001_PropertyTypeMismatchCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 15, 13)
            .ExpectFixedCode(expectedFixedCodeAfterFirstFix)
            .RunAsync();
    }
}

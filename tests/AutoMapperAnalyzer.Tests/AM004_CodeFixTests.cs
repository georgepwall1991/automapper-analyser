using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.CodeFixes;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests;

public class AM004_CodeFixTests
{
    [Fact]
    public async Task AM004_ShouldAddIgnoreForSourceProperty()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string UnusedProperty { get; set; }
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
                                                 public string Name { get; set; }
                                                 public string UnusedProperty { get; set; }
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
                                                         .ForSourceMember(src => src.UnusedProperty, opt => opt.DoNotValidate());
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM004_MissingDestinationPropertyAnalyzer>()
            .WithCodeFix<AM004_MissingDestinationPropertyCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 23, 29)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM004_ShouldAddCustomMappingWithComment()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string ImportantData { get; set; }
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
                                                 public string Name { get; set; }
                                                 public string ImportantData { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     // TODO: Add 'ImportantData' property of type 'string' to destination class
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.ImportantData, opt => opt.MapFrom(src => src.ImportantData));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM004_MissingDestinationPropertyAnalyzer>()
            .WithCodeFix<AM004_MissingDestinationPropertyCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 23, 29)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM004_ShouldAddCombinedPropertyMapping()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Description { get; set; }
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
                                                 public string Name { get; set; }
                                                 public string Description { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     // TODO: Replace 'CombinedProperty' with actual destination property name
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.CombinedProperty, opt => opt.MapFrom(src => $"{src.Description}"));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM004_MissingDestinationPropertyAnalyzer>()
            .WithCodeFix<AM004_MissingDestinationPropertyCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 23, 29)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }
}
using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests;

public class AM004_CodeFixTests
{
    private static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor, int line, int column, params object[] messageArgs)
    {
        var result = new DiagnosticResult(descriptor).WithLocation(line, column);
        if (messageArgs.Length > 0)
        {
            result = result.WithArguments(messageArgs);
        }

        return result;
    }

    private static Task VerifyFixAsync(string source, DiagnosticDescriptor descriptor, int line, int column, string fixedCode, params object[] messageArgs)
        => CodeFixVerifier<AM004_MissingDestinationPropertyAnalyzer, AM004_MissingDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(source, Diagnostic(descriptor, line, column, messageArgs), fixedCode);

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
                                                     CreateMap<Source, Destination>().ForSourceMember(src => src.UnusedProperty, opt => opt.DoNotValidate());
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
            21,
            13,
            expectedFixedCode,
            "UnusedProperty");
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
                                                     CreateMap<Source, Destination>().ForSourceMember(src => src.ImportantData, opt => opt.DoNotValidate());
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
            21,
            13,
            expectedFixedCode,
            "ImportantData");
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
                                                     CreateMap<Source, Destination>().ForSourceMember(src => src.Description, opt => opt.DoNotValidate());
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
            21,
            13,
            expectedFixedCode,
            "Description");
    }

    [Fact]
    public async Task AM004_ShouldHandleMultipleUnmappedSourceProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Extra1 { get; set; }
                                        public string Extra2 { get; set; }
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
                                                 public string Extra1 { get; set; }
                                                 public string Extra2 { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForSourceMember(src => src.Extra2, opt => opt.DoNotValidate()).ForSourceMember(src => src.Extra1, opt => opt.DoNotValidate());
                                                 }
                                             }
                                         }
                                         """;

        var extra1Diagnostic = Diagnostic(
            AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
            21,
            13,
            "Extra1");

        var extra2Diagnostic = Diagnostic(
            AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
            21,
            13,
            "Extra2");

        await CodeFixVerifier<AM004_MissingDestinationPropertyAnalyzer, AM004_MissingDestinationPropertyCodeFixProvider>.VerifyFixAsync(
            testCode,
            new[] { extra1Diagnostic, extra2Diagnostic },
            expectedFixedCode);
    }

    [Fact]
    public async Task AM004_ShouldHandleNumericSourceProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public int UnusedId { get; set; }
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
                                                 public int UnusedId { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForSourceMember(src => src.UnusedId, opt => opt.DoNotValidate());
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
            20,
            13,
            expectedFixedCode,
            "UnusedId");
    }

    [Fact]
    public async Task AM004_ShouldHandleComplexTypeSourceProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Address
                                    {
                                        public string Street { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public Address UnusedAddress { get; set; }
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
                                             public class Address
                                             {
                                                 public string Street { get; set; }
                                             }

                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                                 public Address UnusedAddress { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForSourceMember(src => src.UnusedAddress, opt => opt.DoNotValidate());
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
            25,
            13,
            expectedFixedCode,
            "UnusedAddress");
    }
}

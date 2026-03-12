using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

public class AM004_CodeFixTests
{
    private static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor, int line, int column,
        params object[] messageArgs)
    {
        DiagnosticResult result = new DiagnosticResult(descriptor).WithLocation(line, column);
        if (messageArgs.Length > 0)
        {
            result = result.WithArguments(messageArgs);
        }

        return result;
    }

    private static Task VerifyFixAsync(string source, DiagnosticDescriptor descriptor, int line, int column,
        string fixedCode, params object[] messageArgs)
    {
        return CodeFixVerifier<AM004_MissingDestinationPropertyAnalyzer,
                AM004_MissingDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(source, Diagnostic(descriptor, line, column, messageArgs), fixedCode);
    }

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
                                                     CreateMap<Source, Destination>().ForSourceMember(src => src.Extra1, opt => opt.DoNotValidate()).ForSourceMember(src => src.Extra2, opt => opt.DoNotValidate());
                                                 }
                                             }
                                         }
                                         """;

        DiagnosticResult extra1Diagnostic = Diagnostic(
            AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
            21,
            13,
            "Extra1");

        DiagnosticResult extra2Diagnostic = Diagnostic(
            AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
            21,
            13,
            "Extra2");

        await CodeFixVerifier<AM004_MissingDestinationPropertyAnalyzer, AM004_MissingDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[] { extra1Diagnostic, extra2Diagnostic },
                expectedFixedCode,
                null, 
                null, 
                1); // Index 0 = "Ignore N unmapped source properties"
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






    [Fact]
    public async Task AM004_FuzzyMatch_ShouldSuggestSimilarProperty_WhenLevenshteinDistance1()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Emal { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
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
                                                 public string Emal { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public string Email { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Emal));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM004_MissingDestinationPropertyAnalyzer, AM004_MissingDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[]
                {
                    new DiagnosticResult(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule)
                        .WithLocation(21, 13)
                        .WithArguments("Emal")
                },
                expectedFixedCode,
                0, // Index 0 = fuzzy match "Map 'Emal' to similar property 'Email'"
                null,
                1);
    }

    [Fact]
    public async Task AM004_FuzzyMatch_ShouldSuggestSimilarProperty_WhenLevenshteinDistance2()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Usr { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string User { get; set; }
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
                                                 public string Usr { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public string User { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.User, opt => opt.MapFrom(src => src.Usr));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM004_MissingDestinationPropertyAnalyzer, AM004_MissingDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[]
                {
                    new DiagnosticResult(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule)
                        .WithLocation(21, 13)
                        .WithArguments("Usr")
                },
                expectedFixedCode,
                0, // Index 0 = fuzzy match "Map 'Usr' to similar property 'User'"
                null,
                1);
    }

    [Fact]
    public async Task AM004_FuzzyMatch_ShouldNotSuggest_WhenDistanceTooLarge()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string InternalTrackingCode { get; set; }
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

        // When no fuzzy match exists, first per-property fix is "Create property in destination type"
        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                                 public string InternalTrackingCode { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public string InternalTrackingCode { get; set; }
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

        await CodeFixVerifier<AM004_MissingDestinationPropertyAnalyzer, AM004_MissingDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[]
                {
                    new DiagnosticResult(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule)
                        .WithLocation(20, 13)
                        .WithArguments("InternalTrackingCode")
                },
                expectedFixedCode,
                2, // Index 2 = first per-property fix "'InternalTrackingCode' (string) — missing from 'Destination'" -> create
                null,
                1);
    }




    [Fact]
    public async Task AM004_PerPropertyIgnore_ShouldWorkWithReverseMap()
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
                                        public string DestExtra { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.DestExtra, opt => opt.Ignore())
                                                .ReverseMap();
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
                                                 public string DestExtra { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.DestExtra, opt => opt.Ignore())
                                                         .ReverseMap().ForSourceMember(src => src.DestExtra, opt => opt.DoNotValidate());
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM004_MissingDestinationPropertyAnalyzer, AM004_MissingDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[]
                {
                    new DiagnosticResult(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule)
                        .WithLocation(20, 13)
                        .WithArguments("DestExtra")
                },
                expectedFixedCode,
                0, // Index 0 = "Ignore N unmapped source properties"
                null,
                1);
    }




    [Fact]
    public async Task AM004_ShouldHandleInternalProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        internal string InternalData { get; set; }
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
                                                 internal string InternalData { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForSourceMember(src => src.InternalData, opt => opt.DoNotValidate());
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
            "InternalData");
    }

    [Fact]
    public async Task AM004_ShouldHandlePrivateSetAccessors()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string ReadOnlyData { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string ReadOnlyData { get; private set; }
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

        // Private set should still be mappable (AutoMapper can set it)
        // No diagnostic should be raised
        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldHandleProtectedPropertiesInInheritance()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class BaseSource
                                    {
                                        protected string ProtectedField { get; set; }
                                    }

                                    public class Source : BaseSource
                                    {
                                        public string Name { get; set; }
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

        // Protected properties are not publicly accessible and should not be flagged
        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldHandleGenericTypesWithConstraints()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source<T> where T : class
                                    {
                                        public string Name { get; set; }
                                        public T Value { get; set; }
                                        public string Extra { get; set; }
                                    }

                                    public class Destination<T> where T : class
                                    {
                                        public string Name { get; set; }
                                        public T Value { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source<string>, Destination<string>>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source<T> where T : class
                                             {
                                                 public string Name { get; set; }
                                                 public T Value { get; set; }
                                                 public string Extra { get; set; }
                                             }

                                             public class Destination<T> where T : class
                                             {
                                                 public string Name { get; set; }
                                                 public T Value { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source<string>, Destination<string>>().ForSourceMember(src => src.Extra, opt => opt.DoNotValidate());
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
            22,
            13,
            expectedFixedCode,
            "Extra");
    }

    [Fact]
    public async Task AM004_ShouldHandleRecordWithInheritance()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record BaseSource
                                    {
                                        public string Id { get; init; }
                                    }

                                    public record Source : BaseSource
                                    {
                                        public string Name { get; init; }
                                        public string Extra { get; init; }
                                    }

                                    public record BaseDestination
                                    {
                                        public string Id { get; init; }
                                    }

                                    public record Destination : BaseDestination
                                    {
                                        public string Name { get; init; }
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
                                             public record BaseSource
                                             {
                                                 public string Id { get; init; }
                                             }

                                             public record Source : BaseSource
                                             {
                                                 public string Name { get; init; }
                                                 public string Extra { get; init; }
                                             }

                                             public record BaseDestination
                                             {
                                                 public string Id { get; init; }
                                             }

                                             public record Destination : BaseDestination
                                             {
                                                 public string Name { get; init; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForSourceMember(src => src.Extra, opt => opt.DoNotValidate());
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
            30,
            13,
            expectedFixedCode,
            "Extra");
    }
}

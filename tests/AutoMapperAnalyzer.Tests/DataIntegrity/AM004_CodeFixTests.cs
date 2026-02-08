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
    public async Task AM004_ShouldBulkCreateMissingProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Missing1 { get; set; }
                                        public int Missing2 { get; set; }
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
                                                 public string Missing1 { get; set; }
                                                 public int Missing2 { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public string Missing1 { get; set; }
                                                 public int Missing2 { get; set; }
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

        DiagnosticResult diag1 = Diagnostic(
            AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
            21,
            13,
            "Missing1");

        DiagnosticResult diag2 = Diagnostic(
            AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
            21,
            13,
            "Missing2");

        await CodeFixVerifier<AM004_MissingDestinationPropertyAnalyzer, AM004_MissingDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[] { diag1, diag2 },
                expectedFixedCode,
                1, // Index 1 = "Add N missing properties to 'DestType'"
                null, // remainingDiagnostics
                1); // Iterations
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
    public async Task AM004_BulkIgnore_ShouldIgnoreOnlyUnmappedProperties_WhenCustomMappingUsesSourceMembers()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string FirstName { get; set; }
                                        public string LastName { get; set; }
                                        public string MiddleName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string FullName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FirstName + " " + src.LastName));
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
                                                 public string FirstName { get; set; }
                                                 public string LastName { get; set; }
                                                 public string MiddleName { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string FullName { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                         .ForSourceMember(src => src.MiddleName, opt => opt.DoNotValidate()).ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FirstName + " " + src.LastName));
                                                 }
                                             }
                                         }
                                         """;

        DiagnosticResult middleNameDiagnostic = Diagnostic(
            AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
            21,
            13,
            "MiddleName");

        await CodeFixVerifier<AM004_MissingDestinationPropertyAnalyzer, AM004_MissingDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[] { middleNameDiagnostic },
                expectedFixedCode,
                0, // Index 0 = "Ignore N unmapped source properties"
                null,
                1);
    }

    [Fact]
    public async Task AM004_BulkIgnore_ShouldHandleNonAutoMapperReverseMapInChain()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public static class MappingExtensions
                                    {
                                        public static IMappingExpression<TSource, TDestination> ReverseMap<TSource, TDestination>(
                                            this IMappingExpression<TSource, TDestination> expression,
                                            int marker) => expression;
                                    }

                                    public class Source
                                    {
                                        public string FirstName { get; set; }
                                        public string LastName { get; set; }
                                        public string MiddleName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string FullName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ReverseMap(1)
                                                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FirstName + " " + src.LastName));
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public static class MappingExtensions
                                             {
                                                 public static IMappingExpression<TSource, TDestination> ReverseMap<TSource, TDestination>(
                                                     this IMappingExpression<TSource, TDestination> expression,
                                                     int marker) => expression;
                                             }

                                             public class Source
                                             {
                                                 public string FirstName { get; set; }
                                                 public string LastName { get; set; }
                                                 public string MiddleName { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string FullName { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                         .ForSourceMember(src => src.MiddleName, opt => opt.DoNotValidate()).ReverseMap(1)
                                                         .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FirstName + " " + src.LastName));
                                                 }
                                             }
                                         }
                                         """;

        DiagnosticResult middleNameDiagnostic = Diagnostic(
            AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
            28,
            13,
            "MiddleName");

        await CodeFixVerifier<AM004_MissingDestinationPropertyAnalyzer, AM004_MissingDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[] { middleNameDiagnostic },
                expectedFixedCode,
                0, // Index 0 = "Ignore N unmapped source properties"
                null,
                1);
    }

    [Fact]
    public async Task AM004_BulkCreate_ShouldCreateOnlyUnmappedProperties_WhenCustomMappingUsesSourceMembers()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string FirstName { get; set; }
                                        public string LastName { get; set; }
                                        public string MiddleName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string FullName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FirstName + " " + src.LastName));
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
                                                 public string FirstName { get; set; }
                                                 public string LastName { get; set; }
                                                 public string MiddleName { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string FullName { get; set; }
                                                 public string MiddleName { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FirstName + " " + src.LastName));
                                                 }
                                             }
                                         }
                                         """;

        DiagnosticResult middleNameDiagnostic = Diagnostic(
            AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
            21,
            13,
            "MiddleName");

        await CodeFixVerifier<AM004_MissingDestinationPropertyAnalyzer, AM004_MissingDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[] { middleNameDiagnostic },
                expectedFixedCode,
                1, // Index 1 = "Add N missing properties to 'DestType'"
                null,
                1);
    }

    [Fact]
    public async Task AM004_BulkIgnore_ShouldHandleReverseMapMissingSourceProperty()
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
                                        public string Description { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>().ReverseMap();
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
                                                 public string Description { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ReverseMap().ForSourceMember(src => src.Description, opt => opt.DoNotValidate());
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
                        .WithArguments("Description")
                },
                expectedFixedCode,
                0, // Index 0 = "Ignore N unmapped source properties"
                null,
                1);
    }

    [Fact]
    public async Task AM004_BulkIgnore_ShouldNotIgnoreForCtorParamMappedSourceProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string RawName { get; set; }
                                        public string ExtraData { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; }

                                        public Destination(string name)
                                        {
                                            Name = name;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForCtorParam("name", opt => opt.MapFrom(src => src.RawName));
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
                                                 public string RawName { get; set; }
                                                 public string ExtraData { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; }

                                                 public Destination(string name)
                                                 {
                                                     Name = name;
                                                 }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                         .ForSourceMember(src => src.ExtraData, opt => opt.DoNotValidate()).ForCtorParam("name", opt => opt.MapFrom(src => src.RawName));
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
                        .WithLocation(25, 13)
                        .WithArguments("ExtraData")
                },
                expectedFixedCode,
                0, // Index 0 = "Ignore N unmapped source properties"
                null,
                1);
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
                2, // Index 2 = first per-property fix "'Emal' (string) — missing from 'Destination'" -> fuzzy match
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
                2, // Index 2 = first per-property fix "'Usr' (string) — missing from 'Destination'" -> fuzzy match
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
    public async Task AM004_BulkCreate_ShouldGenerateInitOnly_WhenDestinationUsesInitAccessor()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Extra { get; set; }
                                    }

                                    public class Destination
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
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                                 public string Extra { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; init; }
                                                 public string Extra { get; init; }
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
                        .WithArguments("Extra")
                },
                expectedFixedCode,
                1, // Index 1 = "Add N missing properties to 'DestType'"
                null,
                1);
    }

    [Fact]
    public async Task AM004_BulkCreate_ShouldAddPositionalParameter_WhenDestinationIsPositionalRecord()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Extra { get; set; }
                                    }

                                    public record Destination(string Name);

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
                                                 public string Extra { get; set; }
                                             }

                                             public record Destination(string Name, string Extra);

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
                        .WithLocation(17, 13)
                        .WithArguments("Extra")
                },
                expectedFixedCode,
                1, // Index 1 = "Add N missing properties to 'DestType'"
                null,
                1);
    }

    [Fact]
    public async Task AM004_BulkCreate_ShouldAddInitProperty_WhenDestinationIsRecordWithBody()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Extra { get; set; }
                                    }

                                    public record Destination
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
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                                 public string Extra { get; set; }
                                             }

                                             public record Destination
                                             {
                                                 public string Name { get; init; }
                                                 public string Extra { get; init; }
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
                        .WithArguments("Extra")
                },
                expectedFixedCode,
                1, // Index 1 = "Add N missing properties to 'DestType'"
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
    public async Task AM004_BulkIgnore_ShouldHandleMultiplePropertiesWithReverseMap()
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
                                        public string ExtraA { get; set; }
                                        public string ExtraB { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.ExtraA, opt => opt.Ignore())
                                                .ForMember(dest => dest.ExtraB, opt => opt.Ignore())
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
                                                 public string ExtraA { get; set; }
                                                 public string ExtraB { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.ExtraA, opt => opt.Ignore())
                                                         .ForMember(dest => dest.ExtraB, opt => opt.Ignore())
                                                         .ReverseMap().ForSourceMember(src => src.ExtraA, opt => opt.DoNotValidate()).ForSourceMember(src => src.ExtraB, opt => opt.DoNotValidate());
                                                 }
                                             }
                                         }
                                         """;

        var diagA = Diagnostic(
            AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
            21, 13,
            "ExtraA");
        var diagB = Diagnostic(
            AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
            21, 13,
            "ExtraB");

        await CodeFixVerifier<AM004_MissingDestinationPropertyAnalyzer, AM004_MissingDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[] { diagA, diagB },
                expectedFixedCode,
                0, // Index 0 = "Ignore N unmapped source properties"
                null,
                1);
    }

    [Fact]
    public async Task AM004_BulkIgnore_ShouldWorkWhenPerPropertyFixesAreNested()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string A { get; set; }
                                        public string B { get; set; }
                                        public string C { get; set; }
                                        public string D { get; set; }
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
                                                 public string A { get; set; }
                                                 public string B { get; set; }
                                                 public string C { get; set; }
                                                 public string D { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForSourceMember(src => src.A, opt => opt.DoNotValidate()).ForSourceMember(src => src.B, opt => opt.DoNotValidate()).ForSourceMember(src => src.C, opt => opt.DoNotValidate()).ForSourceMember(src => src.D, opt => opt.DoNotValidate());
                                                 }
                                             }
                                         }
                                         """;

        var diagA = Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 23, 13, "A");
        var diagB = Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 23, 13, "B");
        var diagC = Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 23, 13, "C");
        var diagD = Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 23, 13, "D");

        // 4 diagnostics triggers action-oriented grouping: actions grouped by type (ignore/create)
        // Bulk fixes remain at top level: Index 0 = bulk ignore, Index 1 = bulk create
        await CodeFixVerifier<AM004_MissingDestinationPropertyAnalyzer, AM004_MissingDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[] { diagA, diagB, diagC, diagD },
                expectedFixedCode,
                0, // Index 0 = "Ignore 4 unmapped 'Source' properties"
                null,
                1);
    }

    [Fact]
    public async Task AM004_BulkCreate_ShouldWorkWhenPerPropertyFixesAreNested()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string A { get; set; }
                                        public int B { get; set; }
                                        public string C { get; set; }
                                        public int D { get; set; }
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
                                                 public string A { get; set; }
                                                 public int B { get; set; }
                                                 public string C { get; set; }
                                                 public int D { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public string A { get; set; }
                                                 public int B { get; set; }
                                                 public string C { get; set; }
                                                 public int D { get; set; }
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

        var diagA = Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 23, 13, "A");
        var diagB = Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 23, 13, "B");
        var diagC = Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 23, 13, "C");
        var diagD = Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 23, 13, "D");

        // 4 diagnostics triggers action-oriented grouping: actions grouped by type (ignore/create)
        // Bulk fixes remain at top level: Index 0 = bulk ignore, Index 1 = bulk create
        await CodeFixVerifier<AM004_MissingDestinationPropertyAnalyzer, AM004_MissingDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[] { diagA, diagB, diagC, diagD },
                expectedFixedCode,
                1, // Index 1 = "Add 4 missing properties to 'Destination'"
                null,
                1);
    }
}

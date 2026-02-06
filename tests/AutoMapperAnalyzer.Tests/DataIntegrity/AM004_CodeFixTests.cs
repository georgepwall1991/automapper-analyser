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
                1); // Index 0 = "Ignore all unmapped source properties"
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
                1, // Index 1 = "Create all missing properties in destination type"
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
                0, // Index 0 = "Ignore all unmapped source properties"
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
                1, // Index 1 = "Create all missing properties in destination type"
                null,
                1);
    }
}

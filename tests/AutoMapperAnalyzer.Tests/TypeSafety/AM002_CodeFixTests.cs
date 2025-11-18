using AutoMapperAnalyzer.Analyzers.TypeSafety;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.TypeSafety;

public class AM002_CodeFixTests
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
        return VerifyFixAsync(source, descriptor, line, column, fixedCode, null, messageArgs);
    }

    private static Task VerifyFixAsync(string source, DiagnosticDescriptor descriptor, int line, int column,
        string fixedCode, DiagnosticResult[]? remainingDiagnostics, params object[] messageArgs)
    {
        return CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixAsync(source, Diagnostic(descriptor, line, column, messageArgs), fixedCode, remainingDiagnostics);
    }

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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? string.Empty));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            19,
            13,
            expectedFixedCode,
            "Name",
            "Source",
            "string?",
            "Destination",
            "string");
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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age ?? 0));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            19,
            13,
            expectedFixedCode,
            "Age",
            "Source",
            "int?",
            "Destination",
            "int");
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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive ?? false));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            19,
            13,
            expectedFixedCode,
            "IsActive",
            "Source",
            "bool?",
            "Destination",
            "bool");
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
                                                                  CreateMap<Source, Destination>().ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? string.Empty)).ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age ?? 0));
                                                              }
                                                          }
                                                      }
                                                      """;

        DiagnosticResult ageDiagnostic = Diagnostic(
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            21,
            13,
            "Age",
            "Source",
            "int?",
            "Destination",
            "int");

        DiagnosticResult nameDiagnostic = Diagnostic(
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            21,
            13,
            "Name",
            "Source",
            "string?",
            "Destination",
            "string");

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[] { ageDiagnostic, nameDiagnostic },
                expectedFixedCodeAfterFirstFix);
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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Count, opt => opt.MapFrom(src => src.Count ?? 0));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            19,
            13,
            expectedFixedCode,
            "Count",
            "Source",
            "int?",
            "Destination",
            "int");
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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount ?? 0m));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            19,
            13,
            expectedFixedCode,
            "Amount",
            "Source",
            "decimal?",
            "Destination",
            "decimal");
    }
}

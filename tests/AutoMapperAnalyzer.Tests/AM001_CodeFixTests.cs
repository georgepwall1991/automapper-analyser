using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests;

public class AM001_CodeFixTests
{
    private static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor, int line, int column, params object[] messageArgs)
        => new DiagnosticResult(descriptor).WithLocation(line, column).WithArguments(messageArgs);

    [Fact]
    public async Task AM001_ShouldFixPropertyTypeMismatchWithToString()
    {
        const string testCode = """
                                #nullable enable
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
#nullable enable
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
            CreateMap<Source, Destination>().ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age.ToString()));
        }
    }
}
""";

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 20, 13,
                    "Age", "Source", "int", "Destination", "string"),
                expectedFixedCode);
    }

    [Fact(Skip = "AM001 analyzer does not currently emit nullable diagnostics without additional context")]
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
#nullable enable
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

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.NullableCompatibilityRule, 20, 13,
                    "Name", "Source", "string?", "Destination", "string"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM001_ShouldFixNumericConversionWithCast()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public double Score { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public float Score { get; set; }
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
        public double Score { get; set; }
    }

    public class Destination
    {
        public float Score { get; set; }
    }

    public class TestProfile : Profile
    {
        public TestProfile()
        {
            CreateMap<Source, Destination>().ForMember(dest => dest.Score, opt => opt.MapFrom(src => (float)src.Score));
        }
    }
}
""";

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 19, 13,
                    "Score", "Source", "double", "Destination", "float"),
                expectedFixedCode);
    }

    [Fact(Skip = "Pending fix: generated code uses expression patterns unsupported in expression trees")]
    public async Task AM001_ShouldFixStringToIntConversionWithParse()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Value { get; set; }
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
        public string Value { get; set; }
    }

    public class Destination
    {
        public int Value { get; set; }
    }

    public class TestProfile : Profile
    {
        public TestProfile()
        {
            CreateMap<Source, Destination>().ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.Value is not null ? int.Parse(src.Value) : 0));
        }
    }
}
""";

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 19, 13,
                    "Value", "Source", "string", "Destination", "int"),
                expectedFixedCode);
    }

    [Fact(Skip = "Pending fix: need multi-diagnostic support for incremental code fix application")]
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
            CreateMap<Source, Destination>().ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age.ToString()));
        }
    }
}
""";

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 21, 13,
                    "Age", "Source", "int", "Destination", "string"),
                expectedFixedCodeAfterFirstFix,
                new[]
                {
                    Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 21, 13,
                        "Score", "Source", "double", "Destination", "string")
                });
    }
}

using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using AutoMapper;
using AutoMapperAnalyzer.Analyzers.TypeSafety;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Tests.TypeSafety;

public class AM001_CodeFixTests
{
    private static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor, int line, int column,
        params object[] messageArgs)
    {
        return new DiagnosticResult(descriptor).WithLocation(line, column).WithArguments(messageArgs);
    }

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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age.ToString(global::System.Globalization.CultureInfo.InvariantCulture)));
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

    [Fact]
    public async Task AM001_ShouldFixReverseMapNumericConversionWithCast()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Score { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public long Score { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
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
                                                 public int Score { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public long Score { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ReverseMap().ForMember(dest => dest.Score, opt => opt.MapFrom(src => (int)src.Score));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 19, 13,
                    "Score", "Destination", "long", "Source", "int"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM001_ShouldFixDoubleToDecimalConversionWithCast()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public double Amount { get; set; }
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
                                                 public double Amount { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public decimal Amount { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Amount, opt => opt.MapFrom(src => (decimal)src.Amount));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 19, 13,
                    "Amount", "Source", "double", "Destination", "decimal"),
                expectedFixedCode);
    }

    [Fact]
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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.Value != null ? int.Parse(src.Value, global::System.Globalization.CultureInfo.InvariantCulture) : 0));
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

        Document document = CreateDocument(testCode);
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(document);
        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostics);

        Assert.Collection(
            actions.Select(action => action.Title),
            title => Assert.Equal("Map 'Age' with conversion", title),
            title => Assert.Equal("Ignore property 'Age' (manual review)", title),
            title => Assert.Equal("Map 'Score' with conversion", title),
            title => Assert.Equal("Ignore property 'Score' (manual review)", title));

        string updatedCode = await ApplyActionAsync(actions[0], document);
        Assert.Contains(".ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age.ToString(global::System.Globalization.CultureInfo.InvariantCulture)))", updatedCode,
            StringComparison.Ordinal);
        Assert.DoesNotContain("dest => dest.Score", updatedCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AM001_ShouldFixSecondPropertyTypeMismatch_WhenMultipleDiagnosticsExist()
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

        Document document = CreateDocument(testCode);
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(document);
        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostics);

        CodeAction scoreAction = Assert.Single(
            actions,
            action => action.Title == "Map 'Score' with conversion");
        string updatedCode = await ApplyActionAsync(scoreAction, document);

        Assert.Contains(".ForMember(dest => dest.Score, opt => opt.MapFrom(src => src.Score.ToString(global::System.Globalization.CultureInfo.InvariantCulture)))", updatedCode,
            StringComparison.Ordinal);
        Assert.DoesNotContain("dest => dest.Age", updatedCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AM001_ShouldApplyIterativeFixes_ForMultiplePropertyTypeMismatches()
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

        const string expectedFixedCode = """
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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Score, opt => opt.MapFrom(src => src.Score.ToString(global::System.Globalization.CultureInfo.InvariantCulture))).ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age.ToString(global::System.Globalization.CultureInfo.InvariantCulture)));
                                                 }
                                             }
                                         }
                                         """;

        DiagnosticResult[] diagnostics =
        [
            Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 21, 13,
                "Age", "Source", "int", "Destination", "string"),
            Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 21, 13,
                "Score", "Source", "double", "Destination", "string")
        ];

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixWithIterationsAsync(testCode, diagnostics, expectedFixedCode, iterations: 2);
    }

    [Fact]
    public async Task AM001_ShouldApplyFixesToCorrectInvocation_ForSamePropertyNameAcrossCreateMaps()
    {
        // Two separate CreateMap calls each have an "Age" mismatch, so both code actions share the
        // equivalence key AM001_MapWithConversion_Age. Each action closes over its own invocation, so
        // applying both must fix each CreateMap independently rather than touching the wrong one.
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source1 { public int Age { get; set; } }
                                    public class Dest1 { public string Age { get; set; } }
                                    public class Source2 { public int Age { get; set; } }
                                    public class Dest2 { public string Age { get; set; } }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source1, Dest1>();
                                            CreateMap<Source2, Dest2>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source1 { public int Age { get; set; } }
                                             public class Dest1 { public string Age { get; set; } }
                                             public class Source2 { public int Age { get; set; } }
                                             public class Dest2 { public string Age { get; set; } }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source1, Dest1>().ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age.ToString(global::System.Globalization.CultureInfo.InvariantCulture)));
                                                     CreateMap<Source2, Dest2>().ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age.ToString(global::System.Globalization.CultureInfo.InvariantCulture)));
                                                 }
                                             }
                                         }
                                         """;

        DiagnosticResult[] diagnostics =
        [
            Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 14, 13,
                "Age", "Source1", "int", "Dest1", "string"),
            Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 15, 13,
                "Age", "Source2", "int", "Dest2", "string")
        ];

        // Incremental application takes 2 passes (one per diagnostic); Fix-all-in-document batches both
        // same-key actions into a single pass. Both must yield the same correctly-fixed result.
        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(testCode, diagnostics, expectedFixedCode, null, 2, 1, null);
    }

    [Fact]
    public async Task AM001_ShouldFixNullableStringToIntWithParsePattern()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string? Value { get; set; }
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
                                         #nullable enable
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string? Value { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public int Value { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.Value != null ? int.Parse(src.Value, global::System.Globalization.CultureInfo.InvariantCulture) : 0));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 20, 13,
                    "Value", "Source", "string?", "Destination", "int"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM001_ShouldFixEnumToStringWithToString()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public enum OrderStatus
                                    {
                                        Draft,
                                        Submitted
                                    }

                                    public class Source
                                    {
                                        public OrderStatus Status { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Status { get; set; }
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
                                             public enum OrderStatus
                                             {
                                                 Draft,
                                                 Submitted
                                             }

                                             public class Source
                                             {
                                                 public OrderStatus Status { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Status { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 25, 13,
                    "Status", "Source", "TestNamespace.OrderStatus", "Destination", "string"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM001_ShouldFixStringToEnumWithParse()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public enum OrderStatus
                                    {
                                        Draft,
                                        Submitted
                                    }

                                    public class Source
                                    {
                                        public string Status { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public OrderStatus Status { get; set; }
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
                                             public enum OrderStatus
                                             {
                                                 Draft,
                                                 Submitted
                                             }

                                             public class Source
                                             {
                                                 public string Status { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public OrderStatus Status { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status != null ? global::System.Enum.Parse<global::TestNamespace.OrderStatus>(src.Status) : default));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 25, 13,
                    "Status", "Source", "string", "Destination", "TestNamespace.OrderStatus"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM001_ShouldFixNullableStringToEnumWithParseFallback()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public enum OrderStatus
                                    {
                                        Draft,
                                        Submitted
                                    }

                                    public class Source
                                    {
                                        public string? Status { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public OrderStatus Status { get; set; }
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
                                             public enum OrderStatus
                                             {
                                                 Draft,
                                                 Submitted
                                             }

                                             public class Source
                                             {
                                                 public string? Status { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public OrderStatus Status { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status != null ? global::System.Enum.Parse<global::TestNamespace.OrderStatus>(src.Status) : default));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 26, 13,
                    "Status", "Source", "string?", "Destination", "TestNamespace.OrderStatus"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM001_ShouldOfferOnlyIgnore_WhenEnumMapsToNumericType()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public enum OrderStatus
                                    {
                                        Draft,
                                        Submitted
                                    }

                                    public class Source
                                    {
                                        public OrderStatus Status { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Status { get; set; }
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

        Document document = CreateDocument(testCode);
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(document);
        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostics);

        CodeAction action = Assert.Single(actions);
        Assert.Equal("Ignore property 'Status' (manual review)", action.Title);
    }

    [Fact]
    public async Task AM001_ShouldFixUriToStringWithToString()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Uri Website { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Website { get; set; }
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
                                                 public Uri Website { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Website { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Website, opt => opt.MapFrom(src => src.Website.ToString()));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 20, 13,
                    "Website", "Source", "System.Uri", "Destination", "string"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM001_ShouldFixNullableIntToStringWithInvariantToString()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int? Age { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Age { get; set; } = string.Empty;
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
                                                 public int? Age { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Age { get; set; } = string.Empty;
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age.HasValue ? src.Age.Value.ToString(global::System.Globalization.CultureInfo.InvariantCulture) : string.Empty));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 20, 13,
                    "Age", "Source", "int?", "Destination", "string"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM001_ShouldFixDateTimeToStringWithInvariantToString()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public DateTime CreatedDate { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string CreatedDate { get; set; }
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
                                                 public DateTime CreatedDate { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string CreatedDate { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(src => src.CreatedDate.ToString(global::System.Globalization.CultureInfo.InvariantCulture)));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 20, 13,
                    "CreatedDate", "Source", "System.DateTime", "Destination", "string"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM001_ShouldFixKeywordPropertyNameWithEscapedIdentifier()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int @class { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string @class { get; set; }
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
                                                 public int @class { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string @class { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.@class, opt => opt.MapFrom(src => src.@class.ToString(global::System.Globalization.CultureInfo.InvariantCulture)));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 19, 13,
                    "class", "Source", "int", "Destination", "string"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM001_ShouldOfferOnlyIgnore_WhenReferenceConversionIsNotExecutable()
    {
        // Domain object without a known scalar conversion recipe remains ignore-only.
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class CustomId
                                    {
                                        public int Value { get; set; }
                                    }

                                    public class Source
                                    {
                                        public CustomId Id { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Id { get; set; }
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

        Document document = CreateDocument(testCode);
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(document);
        // CustomId is a complex type → AM020 ownership; AM001 should not report.
        // If it did report without a conversion recipe, only Ignore would be offered.
        // Keep this as a guard that non-executable domain conversions never invent MapFrom.
        if (diagnostics.IsDefaultOrEmpty || diagnostics.Length == 0)
        {
            return;
        }

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostics);
        Assert.All(actions, action => Assert.Contains("manual review", action.Title, StringComparison.Ordinal));
        Assert.DoesNotContain(actions, action => action.Title.Contains("with conversion", StringComparison.Ordinal));
    }


    [Fact]
    public async Task AM001_ShouldFixTimeSpanToStringWithoutCultureArg()
    {
        // TimeSpan has no ToString(IFormatProvider); parameterless ToString is culture-invariant for the default form.
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public TimeSpan Duration { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Duration { get; set; }
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
                                                 public TimeSpan Duration { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Duration { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Duration, opt => opt.MapFrom(src => src.Duration.ToString()));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 20, 13,
                    "Duration", "Source", "System.TimeSpan", "Destination", "string"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM001_ShouldFixStringToNullableIntWithNullFallback()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string? Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int? Value { get; set; }
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
                                                 public string? Value { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public int? Value { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.Value != null ? int.Parse(src.Value, global::System.Globalization.CultureInfo.InvariantCulture) : default(int?)));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM001_PropertyTypeMismatchAnalyzer, AM001_PropertyTypeMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 20, 13,
                    "Value", "Source", "string?", "Destination", "int?"),
                expectedFixedCode);
    }

    private static Document CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);

        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, "AM001Tests", "AM001Tests", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview));

        string trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        foreach (string assemblyPath in trustedPlatformAssemblies.Split(Path.PathSeparator))
        {
            if (!string.IsNullOrWhiteSpace(assemblyPath))
            {
                solution = solution.AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyPath));
            }
        }

        solution = solution
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Profile).Assembly.Location))
            .AddDocument(documentId, "Test0.cs", SourceText.From(source));

        return solution.GetDocument(documentId)!;
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(Document document)
    {
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        return (await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new AM001_PropertyTypeMismatchAnalyzer()))
            .GetAnalyzerDiagnosticsAsync())
            .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .ThenBy(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static async Task<List<CodeAction>> RegisterActionsAsync(Document document, ImmutableArray<Diagnostic> diagnostics)
    {
        var actions = new List<CodeAction>();
        var provider = new AM001_PropertyTypeMismatchCodeFixProvider();

        var context = new CodeFixContext(
            document,
            diagnostics[0].Location.SourceSpan,
            diagnostics,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);
        return actions;
    }

    private static async Task<string> ApplyActionAsync(CodeAction action, Document originalDocument)
    {
        ImmutableArray<CodeActionOperation> operations = await action.GetOperationsAsync(CancellationToken.None);
        ApplyChangesOperation applyChanges = Assert.IsType<ApplyChangesOperation>(operations.Single());

        Document updatedDocument = applyChanges.ChangedSolution.GetDocument(originalDocument.Id)!;
        SourceText updatedText = await updatedDocument.GetTextAsync();
        return updatedText.ToString();
    }
}

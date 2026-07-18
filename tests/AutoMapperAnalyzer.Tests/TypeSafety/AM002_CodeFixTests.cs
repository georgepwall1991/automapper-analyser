using System.Collections.Immutable;
using System.IO;
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

public class AM002_CodeFixTests
{
    private static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor, int line, int column,
        params object[] messageArgs)
    {
        if (descriptor == AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule &&
            messageArgs is [object destinationPropertyName, object sourceTypeName, object sourcePropertyType, object destinationTypeName, object destinationPropertyType])
        {
            messageArgs =
            [
                destinationPropertyName,
                sourceTypeName,
                destinationPropertyName,
                sourcePropertyType,
                destinationTypeName,
                destinationPropertyType
            ];
        }

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
    public async Task AM002_ShouldFixReverseMapNullableToNonNullableWithNullCoalescing()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string? Name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.NullSubstitute("fallback"))
                                                .ReverseMap();
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
                                                 public string Name { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string? Name { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Name, opt => opt.NullSubstitute("fallback"))
                                                         .ReverseMap().ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? string.Empty));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            20,
            13,
            expectedFixedCode,
            "Name",
            "Destination",
            "string?",
            "Source",
            "string");
    }

    [Fact]
    public async Task AM002_ShouldEscapeKeywordPropertyName_WhenScaffoldingDefaultMapping()
    {
        // A property whose name is a C# keyword must be emitted as a verbatim identifier in both the
        // destination selector and the source access, otherwise the generated fix fails to compile.
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string? @class { get; set; }
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
                                                 public string? @class { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string @class { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.@class, opt => opt.MapFrom(src => src.@class ?? string.Empty));
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
            "class",
            "Source",
            "string?",
            "Destination",
            "string");
    }

    [Fact]
    public async Task AM002_ShouldReplaceUnsafePassThroughMapFromWithNullCoalescing()
    {
        const string testCode = """
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
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name));
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
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? string.Empty));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 20, 13,
                "Name",
                "Source",
                "string?",
                "Destination",
                "string"),
            expectedFixedCode,
            codeActionIndex: 0);
    }

    [Fact]
    public async Task AM002_ShouldCoalesceEffectiveLaterMapFromExpression()
    {
        const string testCode = """
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
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? string.Empty))
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name));
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
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? string.Empty))
                                                         .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? string.Empty));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 20, 13,
                "Name",
                "Source",
                "string?",
                "Destination",
                "string"),
            expectedFixedCode,
            codeActionIndex: 0);
    }

    [Fact]
    public async Task AM002_ShouldCoalesceExistingMapFromExpression()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string? Name { get; set; }
                                        public string? OtherName { get; set; }
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
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom(s => s.OtherName));
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
                                                 public string? OtherName { get; set; }
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
                                                         .ForMember(dest => dest.Name, opt => opt.MapFrom(s => s.OtherName ?? string.Empty));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            21,
            13,
            expectedFixedCode,
            "Name",
            "Source",
            "OtherName",
            "string?",
            "Destination",
            "string");
    }

    [Fact]
    public async Task AM002_ShouldCoalesceExistingMapFromExpressionWithQualifiedDateTimeDefault()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public System.DateTime? OtherDate { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public System.DateTime CreatedAt { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(s => s.OtherDate));
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
                                                 public System.DateTime? OtherDate { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public System.DateTime CreatedAt { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(s => s.OtherDate ?? global::System.DateTime.MinValue));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 20, 13,
                "CreatedAt",
                "Source",
                "OtherDate",
                "System.DateTime?",
                "Destination",
                "System.DateTime"),
            expectedFixedCode,
            codeActionIndex: 0);
    }

    [Fact]
    public async Task AM002_ShouldFixGenericNullableTypeParameterWithNullForgivingDefault()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source<T>
                                        where T : class
                                    {
                                        public T? Value { get; set; }
                                    }

                                    public class Destination<T>
                                        where T : class
                                    {
                                        public T Value { get; set; }
                                    }

                                    public class TestProfile<T> : Profile
                                        where T : class
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source<T>, Destination<T>>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         #nullable enable
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source<T>
                                                 where T : class
                                             {
                                                 public T? Value { get; set; }
                                             }

                                             public class Destination<T>
                                                 where T : class
                                             {
                                                 public T Value { get; set; }
                                             }

                                             public class TestProfile<T> : Profile
                                                 where T : class
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source<T>, Destination<T>>().ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.Value ?? default!));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            23,
            13,
            expectedFixedCode,
            "Value",
            "Source<T>",
            "T?",
            "Destination<T>",
            "T");
    }

    [Fact]
    public async Task AM002_ShouldPreserveExistingNonVetoingMemberOptionsWhenAddingNullCoalescingMapFrom()
    {
        const string testCode = """
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
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.AddTransform(value => value.Trim()));
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
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Name, opt => { opt.AddTransform(value => value.Trim()); opt.MapFrom(src => src.Name ?? string.Empty); });
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            20,
            13,
            expectedFixedCode,
            "Name",
            "Source",
            "string?",
            "Destination",
            "string");
    }

    [Fact]
    public async Task AM002_ShouldPreserveGetMethodConventionWhenAppendingMapFrom()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public sealed class Source
                                    {
                                        public string? GetName() => null;
                                    }

                                    public sealed class Destination
                                    {
                                        public string Name { get; set; } = string.Empty;
                                    }

                                    public sealed class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(destination => destination.Name, options => options.DoNotAllowNull());
                                        }
                                    }
                                }
                                """;

        const string fixedCode = """
                                 #nullable enable
                                 using AutoMapper;

                                 namespace TestNamespace
                                 {
                                     public sealed class Source
                                     {
                                         public string? GetName() => null;
                                     }

                                     public sealed class Destination
                                     {
                                         public string Name { get; set; } = string.Empty;
                                     }

                                     public sealed class TestProfile : Profile
                                     {
                                         public TestProfile()
                                         {
                                             CreateMap<Source, Destination>()
                                                 .ForMember(destination => destination.Name, options => { options.DoNotAllowNull(); options.MapFrom(src => src.GetName() ?? string.Empty); });
                                         }
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(20, 13)
            .WithArguments("Name", "Source", "GetName", "string?", "Destination", "string");

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixByKeyAsync(testCode, expected, fixedCode, "AM002_DefaultValue_Name");
    }

    [Fact]
    public async Task AM002_ShouldAddTopLevelForMember_WhenChildForPathExists()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public struct Address
                                    {
                                        public string? Line1 { get; set; }
                                    }

                                    public class Source
                                    {
                                        public Address? Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Address Address { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForPath(dest => dest.Address.Line1, opt => opt.MapFrom(src => src.Address.Value.Line1));
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         #nullable enable
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public struct Address
                                             {
                                                 public string? Line1 { get; set; }
                                             }

                                             public class Source
                                             {
                                                 public Address? Address { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public Address Address { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                         .ForMember(dest => dest.Address, opt => opt.MapFrom(src => src.Address ?? default)).ForPath(dest => dest.Address.Line1, opt => opt.MapFrom(src => src.Address.Value.Line1));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            25,
            13,
            expectedFixedCode,
            "Address",
            "Source",
            "TestNamespace.Address?",
            "Destination",
            "TestNamespace.Address");
    }

    [Fact]
    public async Task AM002_ShouldAvoidSourceParameterCollision_WhenAppendingMapFrom()
    {
        const string testCode = """
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
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, src => src.AddTransform(source => source.Trim()));
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
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Name, src => { src.AddTransform(source => source.Trim()); src.MapFrom(source => source.Name ?? string.Empty); });
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            20,
            13,
            expectedFixedCode,
            "Name",
            "Source",
            "string?",
            "Destination",
            "string");
    }

    [Fact]
    public async Task AM002_ShouldNotOfferDefaultValueFix_WhenExistingMemberConditionCanVetoAssignment()
    {
        const string testCode = """
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
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.Condition(src => src.Name != null));
                                        }
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        CodeAction action = Assert.Single(actions);
        Assert.Equal("AM002_Ignore_Name", action.EquivalenceKey);
    }

    [Fact]
    public async Task AM002_ShouldNotOfferDefaultValueFix_WhenMapFromDereferencesNullableReceiver()
    {
        const string testCode = """
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
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()));
                                        }
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        CodeAction action = Assert.Single(actions);
        Assert.Equal("AM002_Ignore_Name", action.EquivalenceKey);
    }

    [Fact]
    public async Task AM002_ShouldCoalesceUnsafeTopLevelForPathMapFrom()
    {
        const string testCode = """
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
                                            CreateMap<Source, Destination>()
                                                .ForPath(dest => dest.Name, opt => opt.MapFrom(src => src.Name));
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
                                                     CreateMap<Source, Destination>()
                                                         .ForPath(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? string.Empty));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            20,
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

        // Both properties get fixed via per-property fixes
        const string expectedFixedCode = """
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
            .VerifyFixWithIterationsAsync(
                testCode,
                new[] { ageDiagnostic, nameDiagnostic },
                expectedFixedCode,
                2);
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

    [Fact]
    public async Task AM002_ShouldIgnoreSingleProperty()
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

        // Expect property to be ignored
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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Name, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 19, 13,
                    "Name", "Source", "string?", "Destination", "string"),
                expectedFixedCode,
                codeActionIndex: 1); // Use the second fix (Ignore property)
    }

    [Fact]
    public async Task AM002_ShouldNotRegisterCodeActions_ForNonNullableToNullableInfoDiagnostic()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; } = string.Empty;
                                    }

                                    public class Destination
                                    {
                                        public string? Name { get; set; }
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
        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Descriptor == AM002_NullableCompatibilityAnalyzer.NonNullableToNullableRule);

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task AM002_ShouldOfferOnlyIgnore_ForNullableElementCollection()
    {
        // A "src.Tags ?? default" coalesce scaffold cannot fix element-level nullability (the elements are
        // still nullable), so the element-nullability diagnostic must offer only the manual-review ignore
        // action — the user has to choose filter-vs-default semantics deliberately.
        const string testCode = """
                                #nullable enable
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string?> Tags { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<string> Tags { get; set; }
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
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        CodeAction action = Assert.Single(actions);
        Assert.Equal("AM002_Ignore_Tags", action.EquivalenceKey);
    }

    [Fact]
    public async Task AM002_ShouldNotRegisterCodeActions_ForCtorParamNullableElementCollection()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public sealed record Source(List<string?> Values);
                                    public sealed record Destination(List<string> Values);

                                    public sealed class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForCtorParam(nameof(Destination.Values), options =>
                                                    options.MapFrom(source => source.Values));
                                        }
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        Assert.Empty(actions);
    }

    private static Document CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);

        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, "AM002Tests", "AM002Tests", LanguageNames.CSharp)
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
                ImmutableArray.Create<DiagnosticAnalyzer>(new AM002_NullableCompatibilityAnalyzer()))
            .GetAnalyzerDiagnosticsAsync())
            .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .ThenBy(diagnostic => diagnostic.GetMessage(), StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static async Task<List<CodeAction>> RegisterActionsAsync(Document document, params Diagnostic[] diagnostics)
    {
        var actions = new List<CodeAction>();
        var provider = new AM002_NullableCompatibilityCodeFixProvider();

        var context = new CodeFixContext(
            document,
            diagnostics[0].Location.SourceSpan,
            diagnostics.ToImmutableArray(),
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);
        return actions;
    }

    [Fact]
    public async Task AM002_ShouldRewriteUnsafeForCtorParamMapFromWithNullCoalescing()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record Source(string? Value);
                                public sealed record Destination(string Value);

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam(nameof(Destination.Value), options =>
                                                options.MapFrom(source => source.Value));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 #nullable enable
                                 using AutoMapper;

                                 public sealed record Source(string? Value);
                                 public sealed record Destination(string Value);

                                 public sealed class TestProfile : Profile
                                 {
                                     public TestProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                             .ForCtorParam(nameof(Destination.Value), options =>
                                                 options.MapFrom(source => source.Value ?? string.Empty));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(11, 9)
            .WithArguments("Value", "Source", "Value", "string?", "Destination", "string");

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixByKeyAsync(testCode, expected, fixedCode, "AM002_DefaultValue_Value");
    }

    [Fact]
    public async Task AM002_ShouldRewriteConstructorParameterWithoutDestinationProperty()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record Source(string? token);

                                public sealed class Destination
                                {
                                    private readonly string _token;

                                    public Destination(string token)
                                    {
                                        _token = token;
                                    }
                                }

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam("token", options =>
                                                options.MapFrom(source => source.token));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 #nullable enable
                                 using AutoMapper;

                                 public sealed record Source(string? token);

                                 public sealed class Destination
                                 {
                                     private readonly string _token;

                                     public Destination(string token)
                                     {
                                         _token = token;
                                     }
                                 }

                                 public sealed class TestProfile : Profile
                                 {
                                     public TestProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                             .ForCtorParam("token", options =>
                                                 options.MapFrom(source => source.token ?? string.Empty));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(20, 9)
            .WithArguments("token", "Source", "token", "string?", "Destination", "string");

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixByKeyAsync(testCode, expected, fixedCode, "AM002_DefaultValue_token");
    }


    [Fact]
    public async Task AM002_ShouldRewriteConstructorParameterWhenRequiredSiblingUsesFlattening()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record Customer(string Name);
                                public sealed record Source(string? token, Customer Customer);

                                public sealed class Destination
                                {
                                    private readonly string _token;

                                    public Destination(string token, string customerName)
                                    {
                                        _token = token;
                                        CustomerName = customerName;
                                    }

                                    public string CustomerName { get; }
                                }

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam("token", options =>
                                                options.MapFrom(source => source.token));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 #nullable enable
                                 using AutoMapper;

                                 public sealed record Customer(string Name);
                                 public sealed record Source(string? token, Customer Customer);

                                 public sealed class Destination
                                 {
                                     private readonly string _token;

                                     public Destination(string token, string customerName)
                                     {
                                         _token = token;
                                         CustomerName = customerName;
                                     }

                                     public string CustomerName { get; }
                                 }

                                 public sealed class TestProfile : Profile
                                 {
                                     public TestProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                             .ForCtorParam("token", options =>
                                                 options.MapFrom(source => source.token ?? string.Empty));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(24, 9)
            .WithArguments("token", "Source", "token", "string?", "Destination", "string");

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixByKeyAsync(testCode, expected, fixedCode, "AM002_DefaultValue_token");
    }

    [Fact]
    public async Task AM002_ShouldRewriteNullForgivingForCtorParamMapFromWithNullCoalescing()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record Source(string? Value);
                                public sealed record Destination(string Value);

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam(nameof(Destination.Value), options =>
                                                options.MapFrom(source => source.Value!));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 #nullable enable
                                 using AutoMapper;

                                 public sealed record Source(string? Value);
                                 public sealed record Destination(string Value);

                                 public sealed class TestProfile : Profile
                                 {
                                     public TestProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                             .ForCtorParam(nameof(Destination.Value), options =>
                                                 options.MapFrom(source => source.Value ?? string.Empty));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(11, 9)
            .WithArguments("Value", "Source", "Value", "string?", "Destination", "string");

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixByKeyAsync(testCode, expected, fixedCode, "AM002_DefaultValue_Value");
    }

    [Fact]
    public async Task AM002_ShouldOfferForCtorParamFixForNullForgivenNullableField()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed class Source;
                                public sealed record Destination(string Value);

                                public sealed class TestProfile : Profile
                                {
                                    private readonly string? _value;

                                    public TestProfile(string? value)
                                    {
                                        _value = value;
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam(nameof(Destination.Value), options =>
                                                options.MapFrom(_ => _value!));
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));

        CodeAction action = Assert.Single(await RegisterActionsAsync(document, diagnostic));
        Assert.Equal("AM002_DefaultValue_Value", action.EquivalenceKey);
    }

    [Fact]
    public async Task AM002_ShouldOfferForCtorParamFixForNullForgivenNullableLocal()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed class Source;
                                public sealed record Destination(string Value);

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        string? value = null;
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam(nameof(Destination.Value), options =>
                                                options.MapFrom(_ => value!));
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));

        CodeAction action = Assert.Single(await RegisterActionsAsync(document, diagnostic));
        Assert.Equal("AM002_DefaultValue_Value", action.EquivalenceKey);
    }

    [Fact]
    public async Task AM002_ShouldOfferForCtorParamFixForNullForgivenNullableParameter()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed class Source;
                                public sealed record Destination(string Value);

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile(string? value)
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam(nameof(Destination.Value), options =>
                                                options.MapFrom(_ => value!));
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));

        CodeAction action = Assert.Single(await RegisterActionsAsync(document, diagnostic));
        Assert.Equal("AM002_DefaultValue_Value", action.EquivalenceKey);
    }


    [Fact]
    public async Task AM002_ShouldRewriteParenthesizedNullForgivingForCtorParamMapFromWithNullCoalescing()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record Source(string? Value);
                                public sealed record Destination(string Value);

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam(nameof(Destination.Value), options =>
                                                options.MapFrom(source => (source.Value!)));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 #nullable enable
                                 using AutoMapper;

                                 public sealed record Source(string? Value);
                                 public sealed record Destination(string Value);

                                 public sealed class TestProfile : Profile
                                 {
                                     public TestProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                             .ForCtorParam(nameof(Destination.Value), options =>
                                                 options.MapFrom(source => source.Value ?? string.Empty));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(11, 9)
            .WithArguments("Value", "Source", "Value", "string?", "Destination", "string");

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixByKeyAsync(testCode, expected, fixedCode, "AM002_DefaultValue_Value");
    }

    [Fact]
    public async Task AM002_ShouldPreserveParenthesizedSwitchExpressionInForCtorParamFix()
    {
        const string testCode = """
                                #nullable enable
                                using System;
                                using AutoMapper;

                                public sealed record Source(bool Flag, string? Value);
                                public sealed record Destination(string Value);

                                namespace AutoMapper
                                {
                                    public abstract class TestProfileBase
                                    {
                                        protected MappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>() => new();
                                    }

                                    public sealed class MappingExpression<TSource, TDestination>
                                    {
                                        public MappingExpression<TSource, TDestination> ForCtorParam(
                                            string name,
                                            Action<CtorOptions<TSource>> configure) => this;
                                    }

                                    public sealed class CtorOptions<TSource>
                                    {
                                        public void MapFrom<TResult>(Func<TSource, TResult> selector) { }
                                    }
                                }

                                public sealed class TestProfile : TestProfileBase
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam(nameof(Destination.Value), options =>
                                                options.MapFrom(source =>
                                                    (source.Flag switch { true => source.Value, false => null })));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 #nullable enable
                                 using System;
                                 using AutoMapper;

                                 public sealed record Source(bool Flag, string? Value);
                                 public sealed record Destination(string Value);

                                 namespace AutoMapper
                                 {
                                     public abstract class TestProfileBase
                                     {
                                         protected MappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>() => new();
                                     }

                                     public sealed class MappingExpression<TSource, TDestination>
                                     {
                                         public MappingExpression<TSource, TDestination> ForCtorParam(
                                             string name,
                                             Action<CtorOptions<TSource>> configure) => this;
                                     }

                                     public sealed class CtorOptions<TSource>
                                     {
                                         public void MapFrom<TResult>(Func<TSource, TResult> selector) { }
                                     }
                                 }

                                 public sealed class TestProfile : TestProfileBase
                                 {
                                     public TestProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                             .ForCtorParam(nameof(Destination.Value), options =>
                                                 options.MapFrom(source =>
                                                     (source.Flag switch { true => source.Value, false => null }) ?? string.Empty));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(32, 9)
            .WithArguments("Value", "Source", "Flag", "string?", "Destination", "string");

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixByKeyAsync(testCode, expected, fixedCode, "AM002_DefaultValue_Value");
    }


    [Fact]
    public async Task AM002_ShouldWithholdFixForNullForgivenNullableReceiverInForCtorParam()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record NestedSource(string? Value);
                                public sealed record Source(NestedSource? Nested);
                                public sealed record Destination(string Value);

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam(nameof(Destination.Value), options =>
                                                options.MapFrom(source => source.Nested!.Value));
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));

        Assert.Empty(await RegisterActionsAsync(document, diagnostic));
    }

    [Fact]
    public async Task AM002_ShouldWithholdNullDefaultForConstructorOnlyReferenceParameter()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed class Widget;
                                public sealed record Source(Widget? widget);

                                public sealed class Destination
                                {
                                    private readonly Widget _widget;

                                    public Destination(Widget widget)
                                    {
                                        _widget = widget;
                                    }
                                }

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam("widget", options =>
                                                options.MapFrom(source => source.widget));
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));

        Assert.Empty(await RegisterActionsAsync(document, diagnostic));
    }

    [Fact]
    public async Task AM002_ShouldWithholdPropertyIgnoreForForCtorParam()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record Source(string? Value);
                                public sealed record Destination(string Value);

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam(nameof(Destination.Value), options =>
                                                options.MapFrom(source => source.Value));
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));

        CodeAction action = Assert.Single(await RegisterActionsAsync(document, diagnostic));
        Assert.Equal("AM002_DefaultValue_Value", action.EquivalenceKey);
    }

    [Fact]
    public async Task AM002_ShouldAddForMemberForInvalidSameNameForCtorParam()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record Source(string? Value);

                                public sealed class Destination
                                {
                                    public Destination(string other)
                                    {
                                    }

                                    public string Value { get; set; } = string.Empty;
                                }

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam("Value", options =>
                                                options.MapFrom(source => source.Value));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 #nullable enable
                                 using AutoMapper;

                                 public sealed record Source(string? Value);

                                 public sealed class Destination
                                 {
                                     public Destination(string other)
                                     {
                                     }

                                     public string Value { get; set; } = string.Empty;
                                 }

                                 public sealed class TestProfile : Profile
                                 {
                                     public TestProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                 .ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.Value ?? string.Empty)).ForCtorParam("Value", options =>
                                                 options.MapFrom(source => source.Value));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(19, 9)
            .WithArguments("Value", "Source", "Value", "string?", "Destination", "string");

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));
        CodeAction[] actions = (await RegisterActionsAsync(document, diagnostic)).ToArray();
        Assert.Equal(
            ["AM002_DefaultValue_Value", "AM002_Ignore_Value"],
            actions.Select(action => action.EquivalenceKey));

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixByKeyAsync(testCode, expected, fixedCode, "AM002_DefaultValue_Value");
    }

    [Fact]
    public async Task AM002_ShouldAddForMemberForWrongCaseForCtorParam()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record Source(string? Value, string Safe);

                                public sealed class Destination
                                {
                                    public Destination(string value)
                                    {
                                        Value = value;
                                    }

                                    public string Value { get; set; } = string.Empty;
                                }

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam("Value", options =>
                                                options.MapFrom(source => source.Safe));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 #nullable enable
                                 using AutoMapper;

                                 public sealed record Source(string? Value, string Safe);

                                 public sealed class Destination
                                 {
                                     public Destination(string value)
                                     {
                                         Value = value;
                                     }

                                     public string Value { get; set; } = string.Empty;
                                 }

                                 public sealed class TestProfile : Profile
                                 {
                                     public TestProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                 .ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.Value ?? string.Empty)).ForCtorParam("Value", options =>
                                                 options.MapFrom(source => source.Safe));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(20, 9)
            .WithArguments("Value", "Source", "Value", "string?", "Destination", "string");

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixByKeyAsync(testCode, expected, fixedCode, "AM002_DefaultValue_Value");
    }

    [Fact]
    public async Task AM002_ShouldScaffoldGetMethodConventionForWritableAlias()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed class Source
                                {
                                    public string Input { get; set; } = string.Empty;
                                    public string? GetValue() => null;
                                }

                                public sealed class Destination
                                {
                                    public Destination(string valueParam)
                                    {
                                        Value = valueParam;
                                    }

                                    public string Value { get; set; } = string.Empty;
                                }

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam("valueParam", options =>
                                                options.MapFrom(source => source.Input));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 #nullable enable
                                 using AutoMapper;

                                 public sealed class Source
                                 {
                                     public string Input { get; set; } = string.Empty;
                                     public string? GetValue() => null;
                                 }

                                 public sealed class Destination
                                 {
                                     public Destination(string valueParam)
                                     {
                                         Value = valueParam;
                                     }

                                     public string Value { get; set; } = string.Empty;
                                 }

                                 public sealed class TestProfile : Profile
                                 {
                                     public TestProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                 .ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.GetValue() ?? string.Empty)).ForCtorParam("valueParam", options =>
                                                 options.MapFrom(source => source.Input));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(24, 9)
            .WithArguments("Value", "Source", "GetValue", "string?", "Destination", "string");

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixByKeyAsync(testCode, expected, fixedCode, "AM002_DefaultValue_Value");
    }

    [Fact]
    public async Task AM002_ShouldRewriteUnsafeForCtorParamInsteadOfLaterSafeForMember()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record Source(string? Value);
                                public sealed record Destination(string Value);

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam(nameof(Destination.Value), options =>
                                                options.MapFrom(source => source.Value))
                                            .ForMember(destination => destination.Value, options =>
                                                options.MapFrom(source => source.Value ?? string.Empty));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 #nullable enable
                                 using AutoMapper;

                                 public sealed record Source(string? Value);
                                 public sealed record Destination(string Value);

                                 public sealed class TestProfile : Profile
                                 {
                                     public TestProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                             .ForCtorParam(nameof(Destination.Value), options =>
                                                 options.MapFrom(source => source.Value ?? string.Empty))
                                             .ForMember(destination => destination.Value, options =>
                                                 options.MapFrom(source => source.Value ?? string.Empty));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(11, 9)
            .WithArguments("Value", "Source", "Value", "string?", "Destination", "string");

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixByKeyAsync(testCode, expected, fixedCode, "AM002_DefaultValue_Value");
    }

    [Fact]
    public async Task AM002_ShouldRewriteWritableConstructorAliasUsedDuringConstruction()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record Source(string? Value);

                                public sealed class Destination
                                {
                                    public Destination(string valueParam)
                                    {
                                        Value = valueParam;
                                        Length = valueParam.Length;
                                    }

                                    public string Value { get; set; } = string.Empty;
                                    public int Length { get; }
                                }

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam("valueParam", options =>
                                                options.MapFrom(source => source.Value))
                                            .ForMember(destination => destination.Value, options =>
                                                options.MapFrom(source => source.Value ?? string.Empty));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 #nullable enable
                                 using AutoMapper;

                                 public sealed record Source(string? Value);

                                 public sealed class Destination
                                 {
                                     public Destination(string valueParam)
                                     {
                                         Value = valueParam;
                                         Length = valueParam.Length;
                                     }

                                     public string Value { get; set; } = string.Empty;
                                     public int Length { get; }
                                 }

                                 public sealed class TestProfile : Profile
                                 {
                                     public TestProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                             .ForCtorParam("valueParam", options =>
                                                 options.MapFrom(source => source.Value ?? string.Empty))
                                             .ForMember(destination => destination.Value, options =>
                                                 options.MapFrom(source => source.Value ?? string.Empty));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(22, 9)
            .WithArguments("valueParam", "Source", "Value", "string?", "Destination", "string");

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixByKeyAsync(testCode, expected, fixedCode, "AM002_DefaultValue_valueParam");
    }

    [Fact]
    public async Task AM002_ShouldAddForMemberForUnsafeAliasedCtorParam()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record Source(string? Value);

                                public sealed class Destination
                                {
                                    public Destination(string valueParam)
                                    {
                                        Value = valueParam;
                                    }

                                    public string Value { get; set; }
                                }

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam("valueParam", options =>
                                                options.MapFrom(source => source.Value));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 #nullable enable
                                 using AutoMapper;

                                 public sealed record Source(string? Value);

                                 public sealed class Destination
                                 {
                                     public Destination(string valueParam)
                                     {
                                         Value = valueParam;
                                     }

                                     public string Value { get; set; }
                                 }

                                 public sealed class TestProfile : Profile
                                 {
                                     public TestProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                 .ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.Value ?? string.Empty)).ForCtorParam("valueParam", options =>
                                                 options.MapFrom(source => source.Value));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(20, 9)
            .WithArguments("Value", "Source", "Value", "string?", "Destination", "string");


        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));
        CodeAction action = Assert.Single(await RegisterActionsAsync(document, diagnostic));
        Assert.Equal("AM002_DefaultValue_Value", action.EquivalenceKey);

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixByKeyAsync(testCode, expected, fixedCode, "AM002_DefaultValue_Value");
    }

    [Fact]
    public async Task AM002_ShouldKeepPropertyIgnoreWhenConstructorAliasIsNonNullable()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record Source(string Input, string? Value);

                                public sealed class Destination
                                {
                                    public Destination(string valueParam)
                                    {
                                        Value = valueParam;
                                    }

                                    public string Value { get; set; } = string.Empty;
                                }

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam("valueParam", options =>
                                                options.MapFrom(source => source.Input));
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));
        CodeAction[] actions = (await RegisterActionsAsync(document, diagnostic)).ToArray();

        Assert.Equal(
            ["AM002_DefaultValue_Value", "AM002_Ignore_Value"],
            actions.Select(action => action.EquivalenceKey));
    }


    [Fact]
    public async Task AM002_ShouldRewriteUnsafeCtorAliasAssignedToReadOnlyDestinationProperty()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record Source(string? Value);

                                public sealed class Destination
                                {
                                    public Destination(string valueParam)
                                    {
                                        Value = valueParam;
                                    }

                                    public string Value { get; }
                                }

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam("valueParam", options =>
                                                options.MapFrom(source => source.Value));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 #nullable enable
                                 using AutoMapper;

                                 public sealed record Source(string? Value);

                                 public sealed class Destination
                                 {
                                     public Destination(string valueParam)
                                     {
                                         Value = valueParam;
                                     }

                                     public string Value { get; }
                                 }

                                 public sealed class TestProfile : Profile
                                 {
                                     public TestProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                             .ForCtorParam("valueParam", options =>
                                                 options.MapFrom(source => source.Value ?? string.Empty));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(20, 9)
            .WithArguments("Value", "Source", "Value", "string?", "Destination", "string");

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixByKeyAsync(testCode, expected, fixedCode, "AM002_DefaultValue_Value");
    }

    [Fact]
    public async Task AM002_ShouldAddForMemberForExpressionBodiedAliasedCtorParam()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record Source(string? Value);

                                public sealed class Destination
                                {
                                    public Destination(string valueParam) => Value = valueParam;

                                    public string Value { get; set; }
                                }

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam("valueParam", options =>
                                                options.MapFrom(source => source.Value ?? string.Empty));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 #nullable enable
                                 using AutoMapper;

                                 public sealed record Source(string? Value);

                                 public sealed class Destination
                                 {
                                     public Destination(string valueParam) => Value = valueParam;

                                     public string Value { get; set; }
                                 }

                                 public sealed class TestProfile : Profile
                                 {
                                     public TestProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                 .ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.Value ?? string.Empty)).ForCtorParam("valueParam", options =>
                                                 options.MapFrom(source => source.Value ?? string.Empty));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(17, 9)
            .WithArguments("Value", "Source", "Value", "string?", "Destination", "string");

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixByKeyAsync(testCode, expected, fixedCode, "AM002_DefaultValue_Value");
    }

    [Fact]
    public async Task AM002_ShouldAddForMemberWhenAliasedCtorPropertyRemainsWritable()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record Source(string? Value);

                                public sealed class Destination
                                {
                                    public Destination(string valueParam)
                                    {
                                        Value = valueParam;
                                    }

                                    public string Value { get; set; } = string.Empty;
                                }

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam("valueParam", options =>
                                                options.MapFrom(source => source.Value ?? string.Empty));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 #nullable enable
                                 using AutoMapper;

                                 public sealed record Source(string? Value);

                                 public sealed class Destination
                                 {
                                     public Destination(string valueParam)
                                     {
                                         Value = valueParam;
                                     }

                                     public string Value { get; set; } = string.Empty;
                                 }

                                 public sealed class TestProfile : Profile
                                 {
                                     public TestProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                 .ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.Value ?? string.Empty)).ForCtorParam("valueParam", options =>
                                                 options.MapFrom(source => source.Value ?? string.Empty));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(20, 9)
            .WithArguments("Value", "Source", "Value", "string?", "Destination", "string");

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixByKeyAsync(testCode, expected, fixedCode, "AM002_DefaultValue_Value");
    }

    [Fact]
    public async Task AM002_ShouldRewriteCtorParamSelectedByMemberAccessConstant()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record Source(string? Value);
                                public sealed record Destination(string Value);

                                public static class ParameterNames
                                {
                                    public const string ValueConstructorParameter = nameof(Destination.Value);
                                }

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam(ParameterNames.ValueConstructorParameter, options =>
                                                options.MapFrom(source => source.Value));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 #nullable enable
                                 using AutoMapper;

                                 public sealed record Source(string? Value);
                                 public sealed record Destination(string Value);

                                 public static class ParameterNames
                                 {
                                     public const string ValueConstructorParameter = nameof(Destination.Value);
                                 }

                                 public sealed class TestProfile : Profile
                                 {
                                     public TestProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                             .ForCtorParam(ParameterNames.ValueConstructorParameter, options =>
                                                 options.MapFrom(source => source.Value ?? string.Empty));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(16, 9)
            .WithArguments("Value", "Source", "Value", "string?", "Destination", "string");

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixByKeyAsync(testCode, expected, fixedCode, "AM002_DefaultValue_Value");
    }
    [Fact]
    public async Task AM002_ShouldPreserveAliasedConstructorMapFromTransformation()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record Source(string? Value);
                                public sealed record Destination(string Value);

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam(nameof(Destination.Value), options =>
                                            {
                                                var alias = options;
                                                alias.MapFrom(source => Normalize(source.Value));
                                            });
                                    }

                                    private static string? Normalize(string? value) => value;
                                }
                                """;

        const string fixedCode = """
                                 #nullable enable
                                 using AutoMapper;

                                 public sealed record Source(string? Value);
                                 public sealed record Destination(string Value);

                                 public sealed class TestProfile : Profile
                                 {
                                     public TestProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                             .ForCtorParam(nameof(Destination.Value), options =>
                                             {
                                                 var alias = options;
                                                 alias.MapFrom(source => Normalize(source.Value) ?? string.Empty);
                                             });
                                     }

                                     private static string? Normalize(string? value) => value;
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(11, 9)
            .WithArguments("Value", "Source", "Value", "string?", "Destination", "string");

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixByKeyAsync(testCode, expected, fixedCode, "AM002_DefaultValue_Value");
    }
    [Fact]
    public async Task AM002_ShouldWithholdDefaultFixWhenInvokedHelperAliasesCondition()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                public sealed record Source(string? Name);
                                public sealed record Destination(string Name);

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(destination => destination.Name, options =>
                                            {
                                                var current = options;

                                                void Apply()
                                                {
                                                    current.Condition((Source source) => source.Name != null);
                                                    current.MapFrom(source => source.Name);
                                                }

                                                Apply();
                                            });
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));

        CodeAction action = Assert.Single(await RegisterActionsAsync(document, diagnostic));

        Assert.Equal("AM002_Ignore_Name", action.EquivalenceKey);
    }

}

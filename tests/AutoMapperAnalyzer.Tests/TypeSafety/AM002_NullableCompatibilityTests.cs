using AutoMapperAnalyzer.Analyzers.TypeSafety;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.TypeSafety;

public class AM002_NullableCompatibilityTests
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

        return new DiagnosticResult(descriptor).WithLocation(line, column).WithArguments(messageArgs);
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenNullableStringToNonNullableString()
    {
        string testCode = """

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
                                      CreateMap<Source, Destination>();
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_ForReadOnlyNullableSourceProperty()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public Source(string? name)
                                  {
                                      Name = name;
                                  }

                                  public string? Name { get; }
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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 26, 13, "Name", "Source",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportInfo_WhenNonNullableStringToNullableString()
    {
        string testCode = """

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
                                      CreateMap<Source, Destination>();
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NonNullableToNullableRule, 21, 13, "Name", "Source",
                "string", "Destination", "string?"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenNullableIntToNonNullableInt()
    {
        string testCode = """

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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Age", "Source", "int?",
                "Destination", "int"));
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenTypesAreCompatible()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public string Name { get; set; }
                                  public int Age { get; set; }
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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenExplicitMappingProvided()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? "default"));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromPassesNullableSourceToNonNullableDestination()
    {
        string testCode = """

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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableSource()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesParenthesizedNullableSource()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (src.Name!)));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesCastedNullableSource()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => ((string?)src.Name)!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromCastsSuppressedNullableSource()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (string)src.Name!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromCastsSuppressedNullableSourceToObject()
    {
        string testCode = """

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
                                  public object Value { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Value, opt => opt.MapFrom(src => (object)src.Name!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Value", "Source",
                "Name",
                "string?", "Destination", "object"));
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenExplicitMapFromSuppressesNonNullableSource()
    {
        string testCode = """

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
                                  public string Name { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableElementAccess()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public string?[] Values { get; set; }
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Values[0]!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name",
                "MapFrom expression", "src.Values[0]", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableIndexerAccess()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System.Collections.Generic;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public IReadOnlyList<string?> Values { get; set; }
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Values[0]!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name",
                "MapFrom expression", "src.Values[0]", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenExplicitMapFromSuppressesNonNullableElementAccess()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public string[] Values { get; set; }
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Values[0]!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenMapFromSuppressesNullableArrayReceiverBeforeElementAccess()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public string[]? Values { get; set; }
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Values![0]));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "Values",
                "string[]?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableCoalesceResult()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (src.Name ?? src.OtherName)!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name", "Source",
                "Name",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromUsesNullForgivenNullableCoalesceFallback()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? src.OtherName!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name", "Source",
                "OtherName",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromUsesConcreteNullForgivenDefaultFallback()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? default!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "Name",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromUsesParenthesizedConcreteNullForgivenDefaultFallback()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? (default!)));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "Name",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromUsesLocalNullableNullForgivenDefaultFallback()
    {
        string testCode = """

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
                                  public string Name { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      string? fallback = null;
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => fallback ?? default!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name",
                "MapFrom expression", "fallback", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenExplicitMapFromUsesNonNullableSourceWithNullForgivenDefaultFallback()
    {
        string testCode = """

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
                                  public string Name { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? default!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenOuterCoalesceHandlesNullForgivenDefaultFallback()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (src.Name ?? default!) ?? string.Empty));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenOuterSuppressionWrapsNullForgivenDefaultFallback()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (src.Name ?? default!)!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "Name",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenOuterSuppressionWrapsNullForgivenNullFallback()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (src.Name ?? null!)!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "Name",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenOuterSuppressionWrapsNullForgivenNullableCoalesceFallback()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (src.Name ?? src.OtherName!)!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name", "Source",
                "Name",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenOuterSuppressionWrapsNullForgivenConditionalBranch()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public bool UseOther { get; set; }
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (src.UseOther ? src.OtherName! : string.Empty)!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name",
                "MapFrom expression", "src.UseOther ? src.OtherName! : string.Empty", "string?", "Destination",
                "string"));
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenNullForgivenCoalesceLeftHasNonNullFallback()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name! ?? string.Empty));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenNullForgivenCoalesceLeftHasNullForgivenNullableFallback()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name! ?? src.OtherName!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name", "Source",
                "OtherName",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenNullForgivenCoalesceLeftHasNullableFallback()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name! ?? src.OtherName));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name", "Source",
                "Name",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenNonNullableCoalesceLeftHasSuppressedNullableFallback()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public string Name { get; set; }
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? src.OtherName!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableCoalesceNullLiteral()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (src.Name ?? null)!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "Name",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableCoalesceDefaultLiteral()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (src.Name ?? default)!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "Name",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableCoalesceTypedDefault()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (src.Name ?? default(string))!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "Name",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableDefaultExpression()
    {
        string testCode = """

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
                                  public string Name { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => default(string)!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name",
                "MapFrom expression", "default(string)", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullLiteral()
    {
        string testCode = """

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
                                  public string Name { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (string?)null!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name",
                "MapFrom expression", "null", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableValueNullLiteral()
    {
        string testCode = """

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
                                  public int Count { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Count, opt => opt.MapFrom(src => (int?)null!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Count",
                "MapFrom expression", "null", "int?", "Destination", "int"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableAsExpression()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public object? Value { get; set; }
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (src.Value as string)!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "Value",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableConditionalAccessResult()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom((src, dest) => src.Name?.Trim()!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "Name",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableConditionalResult()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (src.Name != null ? src.Name : null)!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name",
                "MapFrom expression", "src.Name != null ? src.Name : null", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableValueConditionalResult()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public int Count { get; set; }
                              }

                              public class Destination
                              {
                                  public int Count { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      int? fallback = null;
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Count, opt => opt.MapFrom(src => (true ? fallback : default(int?))!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Count",
                "MapFrom expression", "true ? fallback : default(int?)", "int?", "Destination", "int"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesCompositeNullableConditionalResult()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public bool UseOther { get; set; }
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (src.UseOther ? src.OtherName : string.Empty)!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name",
                "MapFrom expression", "src.UseOther ? src.OtherName : string.Empty", "string?", "Destination",
                "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesCompositeNullableConditionalFalseBranch()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public bool UseFallback { get; set; }
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (src.UseFallback ? string.Empty : src.OtherName)!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name",
                "MapFrom expression", "src.UseFallback ? string.Empty : src.OtherName", "string?", "Destination",
                "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableSwitchResult()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public bool UseName { get; set; }
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom((src, dest) => (src.UseName switch { true => src.Name, _ => string.Empty })!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name",
                "MapFrom expression", "src.UseName switch { true => src.Name, _ => string.Empty }", "string?",
                "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenSwitchArmSuppressesNullableSource()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public bool UseOther { get; set; }
                                  public string Name { get; set; }
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom((src, dest) => src.UseOther switch { true => src.OtherName!, _ => string.Empty }));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 23, 13, "Name", "Source",
                "OtherName",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenSwitchArmWhenClauseGuardsNullableSource()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public bool UseName { get; set; }
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom((src, dest) => (src.UseName switch { true when src.Name != null => src.Name, _ => string.Empty })!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenSwitchArmPatternGuardsNullForgivenSource()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom((src, dest) => src.Name switch { { } => src.Name!, _ => string.Empty }));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenEarlierSwitchArmHandlesNullBeforeDiscardArm()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom((src, dest) => src.Name switch { null => string.Empty, _ => src.Name! }));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenExplicitMapFromSuppressesGuardedCompositeConditionalResult()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (src.Name != null ? src.Name : string.Empty)!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenNullForgivenDereferenceIsGuardedByNegatedNullCheck()
    {
        string testCode = """

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
                                  public int NameLength { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.NameLength, opt => opt.MapFrom((src, dest) => !(src.Name == null) ? src.Name!.Length : 0));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenNullForgivenDereferenceIsGuardedByFalseBranchOfNegatedNullCheck()
    {
        string testCode = """

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
                                  public int NameLength { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.NameLength, opt => opt.MapFrom((src, dest) => !(src.Name != null) ? 0 : src.Name!.Length));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenNullForgivenDereferenceIsGuardedByEqualityToNonNullValue()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom((src, dest) => src.Name == "special" ? src.Name! : string.Empty));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenNullForgivenDereferenceIsGuardedByFalseBranchOfInequalityToNonNullValue()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom((src, dest) => src.Name != "special" ? string.Empty : src.Name!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenNullForgivenDereferenceIsGuardedByDefaultLiteralNullCheck()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom((src, dest) => src.Name == default ? string.Empty : src.Name!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenNullableValueDereferenceIsGuardedByEqualityToTypedDefault()
    {
        string testCode = """

                          #nullable enable
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
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Count, opt => opt.MapFrom((src, dest) => src.Count == default(int) ? src.Count!.Value : 0));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenNullableValueDereferenceFollowsFalseBranchOfTypedDefaultEquality()
    {
        string testCode = """

                          #nullable enable
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
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Count, opt => opt.MapFrom((src, dest) => src.Count == default(int) ? 0 : src.Count!.Value));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Count",
                "Source", "Count", "int?", "Destination", "int"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenNullableValueDereferenceFollowsTrueBranchOfTypedDefaultInequality()
    {
        string testCode = """

                          #nullable enable
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
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Count, opt => opt.MapFrom((src, dest) => src.Count != default(int) ? src.Count!.Value : 0));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Count",
                "Source", "Count", "int?", "Destination", "int"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromUsesNullForgivenNullableConditionalFallback()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name == null ? src.OtherName! : src.Name!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name", "Source",
                "OtherName",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableHelperResult()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => GetName(src)!));
                                  }

                                  private static string? GetName(Source source) => source.Name;
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name",
                "TestProfile", "GetName(src)", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenNullCheckGuardsDifferentNullableMethodInvocation()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public string? Name { get; set; }
                                  public string? GetName() => Name;
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.GetName() == null ? string.Empty : src.GetName()!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name",
                "Source", "GetName", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromUsesNullableSourceMethodResult()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public string? Name { get; set; }
                                  public string? GetName() => Name;
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.GetName()));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name",
                "Source", "GetName", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromUsesNullableHelperResult()
    {
        string testCode = """

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
                                  public string Name { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => GetName(src)));
                                  }

                                  private static string? GetName(Source source) => source.Name;
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name",
                "TestProfile", "GetName(src)", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableField()
    {
        string testCode = """

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
                                  public string Name { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  private readonly string? _fallback;

                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => _fallback!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 23, 13, "Name",
                "TestProfile", "_fallback", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableProfileProperty()
    {
        string testCode = """

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
                                  public string Name { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  private string? Fallback { get; set; }

                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => Fallback!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 23, 13, "Name",
                "TestProfile", "Fallback", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableLocal()
    {
        string testCode = """

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
                                  public string Name { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      string? fallback = null;
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => fallback!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name",
                "MapFrom expression", "fallback", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableNonSourceExpression()
    {
        string testCode = """

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
                                  public string Name { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      string? fallback = null;
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (GetName(src) ?? fallback)!));
                                  }

                                  private static string? GetName(Source source) => source.Name;
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name",
                "MapFrom expression", "GetName(src) ?? fallback", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromSuppressesNullableConstructorParameter()
    {
        string testCode = """

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
                                  public string Name { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile(string? fallback)
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => fallback!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name",
                "MapFrom expression", "fallback", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenLaterMemberConfigurationOverridesSafeNullHandling()
    {
        string testCode = """

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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromUsesDifferentNullableSource()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.OtherName));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name", "Source",
                "OtherName",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromUsesNullableSourceWithoutConventionSource()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.OtherName));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "OtherName",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenExplicitMapFromUsesNullableSourceAndConventionSourceIsNonNullable()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public string Name { get; set; }
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.OtherName));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name", "Source",
                "OtherName",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenMapFromCallsHelperNamedIgnore()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public static class StringExtensions
                              {
                                  public static string? Ignore(this string? value) => value;
                              }

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Ignore()));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 26, 13, "Name", "Source",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenHelperMapFromAppearsInsideMemberOptions()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System;

                          namespace TestNamespace
                          {
                              public static class Helper
                              {
                                  public static void MapFrom<T>(Func<Source, T> resolver)
                                  {
                                  }
                              }

                              public class Source
                              {
                                  public string Name { get; set; }
                                  public string? OtherNullableName { get; set; }
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
                                          .ForMember(dest => dest.Name, opt => Helper.MapFrom(src => src.OtherNullableName));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenCustomResolverMapFromHandlesDestinationProperty()
    {
        string testCode = """

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

                              public class SafeNameResolver : IValueResolver<Source, Destination, string>
                              {
                                  public string Resolve(Source source, Destination destination, string destMember, ResolutionContext context)
                                  {
                                      return source.Name ?? string.Empty;
                                  }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom<SafeNameResolver>());
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenCustomMemberResolverMapFromHandlesDestinationProperty()
    {
        string testCode = """

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

                              public class NullSafeNameResolver : IMemberValueResolver<Source, Destination, string?, string>
                              {
                                  public string Resolve(Source source, Destination destination, string? sourceMember, string destMember, ResolutionContext context)
                                  {
                                      return sourceMember ?? string.Empty;
                                  }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom<NullSafeNameResolver, string?>(src => src.Name));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenMemberValueConverterHandlesDestinationProperty()
    {
        string testCode = """

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

                              public class NullSafeStringConverter : IValueConverter<string?, string>
                              {
                                  public string Convert(string? sourceMember, ResolutionContext context)
                                  {
                                      return sourceMember ?? string.Empty;
                                  }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Name, opt => opt.ConvertUsing<NullSafeStringConverter, string?>(src => src.Name));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenDestinationPropertyIgnored()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.Ignore());
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenTopLevelForPathHandlesNullability()
    {
        string testCode = """

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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenGenericForPathMapFromPassesNullableSourceToNonNullableDestination()
    {
        string testCode = """

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
                                          .ForPath(dest => dest.Name, opt => opt.MapFrom<string?>(src => src.Name));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenMapFromDereferencesNullableReceiver()
    {
        string testCode = """

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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenNullSubstituteIsPairedWithUnsafeMapFromDereference()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt =>
                                          {
                                              opt.NullSubstitute("fallback");
                                              opt.MapFrom(src => src.Name.Trim());
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenMapFromUsesNullNormalizingExtensionMethod()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public static class StringExtensions
                              {
                                  public static string OrEmpty(this string? value) => value ?? string.Empty;
                              }

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.OrEmpty()));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenLaterMapFromInSameOptionsLambdaHandlesNullability()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt =>
                                          {
                                              opt.MapFrom(src => src.Name);
                                              opt.MapFrom(src => src.Name ?? string.Empty);
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenLaterMapFromInSameOptionsLambdaOverridesSafeMapping()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt =>
                                          {
                                              opt.MapFrom(src => src.Name ?? string.Empty);
                                              opt.MapFrom(src => src.Name);
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenDifferentSourceMapFromDereferencesNullableReceiver()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public string Name { get; set; }
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.OtherName.Trim()));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name", "Source",
                "OtherName",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenMapFromDereferencesNullableReceiverForDifferentReturnType()
    {
        string testCode = """

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
                                  public int NameLength { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.NameLength, opt => opt.MapFrom(src => src.Name.Length));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "NameLength", "Source",
                "Name",
                "string?", "Destination", "int"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenMapFromSuppressesNullableReceiverBeforeDereference()
    {
        string testCode = """

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
                                  public int NameLength { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.NameLength, opt => opt.MapFrom(src => src.Name!.Length));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "NameLength", "Source",
                "Name",
                "string?", "Destination", "int"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenMapFromSuppressesParenthesizedNullableReceiverBeforeDereference()
    {
        string testCode = """

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
                                  public int NameLength { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.NameLength, opt => opt.MapFrom(src => (src.Name!).Length));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "NameLength", "Source",
                "Name",
                "string?", "Destination", "int"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenMapFromCastsSuppressedNullableReceiverBeforeDereference()
    {
        string testCode = """

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
                                  public int NameLength { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.NameLength, opt => opt.MapFrom(src => ((string)src.Name!).Length));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "NameLength", "Source",
                "Name",
                "string?", "Destination", "int"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenMapFromInvokesNullForgivenNullableDelegate()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public Func<string>? Factory { get; set; }
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Factory!()));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Name", "Source",
                "Factory",
                "System.Func<string>?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WithNestedSuppressedNullableMemberPath()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Child
                              {
                                  public string Name { get; set; }
                              }

                              public class Parent
                              {
                                  public Child? Child { get; set; }
                              }

                              public class Source
                              {
                                  public Parent? Parent { get; set; }
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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom((src, dest) => src.Parent!.Child!.Name));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 31, 13, "Name", "Source",
                "Parent.Child",
                "TestNamespace.Child?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenMapFromDereferencesNullForgivenConditionalBranch()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public bool UseName { get; set; }
                                  public string? Name { get; set; }
                              }

                              public class Destination
                              {
                                  public int NameLength { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.NameLength, opt => opt.MapFrom(src => (src.UseName ? src.Name! : string.Empty).Length));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "NameLength", "Source",
                "Name",
                "string?", "Destination", "int"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenMapFromDereferencesNullForgivenCoalesceFallback()
    {
        string testCode = """

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
                                  public int NameLength { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.NameLength, opt => opt.MapFrom(src => (src.Name ?? src.OtherName!).Length));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "NameLength", "Source",
                "OtherName",
                "string?", "Destination", "int"));
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenMapFromDereferencesNonNullableCoalesceLeftBeforeNullForgivenFallback()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public SourceChild Child { get; set; }
                                  public SourceChild? FallbackChild { get; set; }
                              }

                              public class SourceChild
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
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (src.Child ?? src.FallbackChild!).Name));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenMapFromDereferencesGuardedCoalesceLeftBeforeNullForgivenFallback()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public SourceChild? Child { get; set; }
                                  public SourceChild? FallbackChild { get; set; }
                              }

                              public class SourceChild
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
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Child != null ? (src.Child ?? src.FallbackChild!).Name : string.Empty));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenNullForgivenDefaultFallbackHasFinalCoalesceFallback()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => (src.Name ?? default!) ?? string.Empty));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenMapFromShortCircuitGuardsSuppressedNullableReceiverInCondition()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name != null && src.Name!.Length > 0 ? src.Name : string.Empty));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenMapFromOrShortCircuitGuardsSuppressedNullableReceiverInCondition()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name == null || src.Name!.Length == 0 ? string.Empty : src.Name));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenMapFromNullForgivenConditionGuardsSuppression()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name! != null ? src.Name! : string.Empty));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenNullSubstituteIsPairedWithSuppressedNullableDereference()
    {
        string testCode = """

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
                                  public int NameLength { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.NameLength, opt =>
                                          {
                                              opt.MapFrom(src => src.Name!.Length);
                                              opt.NullSubstitute(0);
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "NameLength", "Source",
                "Name",
                "string?", "Destination", "int"));
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenMapFromGuardsSuppressedNullableReceiverBeforeDereference()
    {
        string testCode = """

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
                                  public int NameLength { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.NameLength, opt => opt.MapFrom(src => src.Name == null ? 0 : src.Name!.Length));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenMapFromPatternGuardsSuppressedNullableReceiverBeforeDereference()
    {
        string testCode = """

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
                                  public int NameLength { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.NameLength, opt => opt.MapFrom((src, dest) => src.Name is null ? 0 : src.Name!.Length));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenMapFromPositivePatternGuardsSuppressedNullableReceiverBeforeDereference()
    {
        string testCode = """

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
                                  public int NameLength { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.NameLength, opt => opt.MapFrom((src, dest) => src.Name is not null ? src.Name!.Length : 0));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Theory]
    [InlineData("src.Name is { } ? src.Name!.Length : 0")]
    [InlineData("src.Name is ({ }) ? src.Name!.Length : 0")]
    [InlineData("src.Name is string _ ? src.Name!.Length : 0")]
    [InlineData("src.Name is not { } ? 0 : src.Name!.Length")]
    [InlineData("src.Name is (null) ? 0 : src.Name!.Length")]
    [InlineData("src.Name is not null && true ? src.Name!.Length : 0")]
    [InlineData("src.Name is null || false ? 0 : src.Name!.Length")]
    [InlineData("src.Name is not null and { Length: > 0 } ? src.Name!.Length : 0")]
    [InlineData("src.Name is null or { Length: 0 } ? 0 : src.Name!.Length")]
    [InlineData("src.Name is null and null ? 0 : src.Name!.Length")]
    [InlineData("src.Name is { Length: > 0 } or \"ready\" ? src.Name!.Length : 0")]
    public async Task AM002_ShouldNotReportDiagnostic_WhenMapFromPatternOrCompoundGuardProtectsSuppressedNullableReceiver(
        string expression)
    {
        string testCode = $$"""

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
                                   public int NameLength { get; set; }
                               }

                               public class TestProfile : Profile
                               {
                                   public TestProfile()
                                   {
                                       CreateMap<Source, Destination>()
                                           .ForMember(dest => dest.NameLength, opt => opt.MapFrom((src, dest) => {{expression}}));
                                   }
                               }
                           }
                           """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Theory]
    [InlineData("src.Name is { } ? 0 : src.Name!.Length")]
    [InlineData("src.Name is var _ ? src.Name!.Length : 0")]
    public async Task AM002_ShouldReportDiagnostic_WhenMapFromPatternDoesNotGuardSuppressedNullableReceiver(
        string expression)
    {
        string testCode = $$"""

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
                                   public int NameLength { get; set; }
                               }

                               public class TestProfile : Profile
                               {
                                   public TestProfile()
                                   {
                                       CreateMap<Source, Destination>()
                                           .ForMember(dest => dest.NameLength, opt => opt.MapFrom((src, dest) => {{expression}}));
                                   }
                               }
                           }
                           """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "NameLength", "Source",
                "Name",
                "string?", "Destination", "int"));
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenSuppressedNullableReceiverDereferenceMapsToNullableDestination()
    {
        string testCode = """

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
                                  public int? NameLength { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.NameLength, opt => opt.MapFrom(src => src.Name!.Length));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenMapFromGuardsNullableReceiverBeforeDereference()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name == null ? string.Empty : src.Name.Trim()));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenMapFromUsesDefaultNullGuardBeforeDereference()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name == default ? string.Empty : src.Name.Trim()));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenMapFromUsesNullableValueGetValueOrDefault()
    {
        string testCode = """

                          #nullable enable
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
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Count, opt => opt.MapFrom(src => src.Count.GetValueOrDefault()));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenOnlyChildForPathIsIgnored()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Address
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
                                          .ForPath(dest => dest.Address.Line1, opt => opt.Ignore());
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 26, 13, "Address", "Source",
                "TestNamespace.Address?", "Destination", "TestNamespace.Address"));
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenNullSubstituteHandlesDestinationProperty()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.NullSubstitute("fallback"));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenMapFromNullableSourceHasSafeNullSubstitute()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt =>
                                          {
                                              opt.MapFrom(src => src.Name);
                                              opt.NullSubstitute("fallback");
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenNullSubstituteValueIsAssignableToDestinationProperty()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public object? Value { get; set; }
                              }

                              public class Destination
                              {
                                  public object Value { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Value, opt => opt.NullSubstitute("fallback"));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenNullSubstituteUsesTypedValueTypeDefault()
    {
        string testCode = """

                          #nullable enable
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
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Count, opt => opt.NullSubstitute(default(int)));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenNullSubstituteUsesNullForNonNullableDestination()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.NullSubstitute(null));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenNullSubstituteUsesDefaultForNonNullableDestination()
    {
        string testCode = """

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
                                          .ForMember(dest => dest.Name, opt => opt.NullSubstitute(default));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source",
                "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenConstructUsingHandlesNullability()
    {
        string testCode = """

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
                                          .ConstructUsing(src => new Destination { Name = src.Name ?? string.Empty });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenNullableDateTimeToNonNullable()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public DateTime? CreatedDate { get; set; }
                              }

                              public class Destination
                              {
                                  public DateTime CreatedDate { get; set; }
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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "CreatedDate", "Source",
                "System.DateTime?", "Destination", "System.DateTime"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenMultipleNullableProperties()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public string? Name { get; set; }
                                  public int? Age { get; set; }
                                  public string Email { get; set; }
                              }

                              public class Destination
                              {
                                  public string Name { get; set; }
                                  public int Age { get; set; }
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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 25, 13, "Name", "Source",
                "string?", "Destination", "string"),
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 25, 13, "Age", "Source", "int?",
                "Destination", "int"));
    }

    [Fact]
    public async Task AM002_ShouldHandleComplexNullableScenarios()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class SourceAddress
                              {
                                  public string? Street { get; set; }
                                  public string? City { get; set; }
                              }

                              public class DestinationAddress
                              {
                                  public string Street { get; set; }
                                  public string? City { get; set; }
                              }

                              public class Source
                              {
                                  public SourceAddress? Address { get; set; }
                              }

                              public class Destination
                              {
                                  public DestinationAddress? Address { get; set; }
                              }

                              public class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>();
                                      CreateMap<SourceAddress, DestinationAddress>();
                                  }
                              }
                          }
                          """;

        // Should detect nullable -> non-nullable issue in SourceAddress.Street -> DestinationAddress.Street
        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 34, 13, "Street", "SourceAddress",
                "string?", "DestinationAddress", "string"));
    }

    [Fact]
    public async Task AM002_ShouldHandleGenericNullableTypes()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System.Collections.Generic;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public List<string>? Items { get; set; }
                              }

                              public class Destination
                              {
                                  public List<string> Items { get; set; }
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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Items", "Source",
                "System.Collections.Generic.List<string>?", "Destination", "System.Collections.Generic.List<string>"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_ForGenericNullableTypeParameterToNonNullable()
    {
        string testCode = """

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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 24, 13, "Value", "Source<T>",
                "T?", "Destination<T>", "T"));
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenGenericNullableTypeParameterMapFromUsesNullForgivingFallback()
    {
        string testCode = """

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
                                      CreateMap<Source<T>, Destination<T>>()
                                          .ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.Value ?? default!));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenGenericNullableTypeParameterMapFromUsesParenthesizedNullForgivingFallback()
    {
        string testCode = """

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
                                      CreateMap<Source<T>, Destination<T>>()
                                          .ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.Value ?? (default!)));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_ForCreateMapLikeApiOutsideAutoMapper()
    {
        string testCode = """

                          #nullable enable
                          using System;

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

                              public class FakeMapOptions<TSource, TDestMember>
                              {
                                  public void MapFrom(Func<TSource, TDestMember> resolver)
                                  {
                                  }
                              }

                              public class FakeMapExpression<TSource, TDestination>
                              {
                                  public FakeMapExpression<TSource, TDestination> ForMember<TDestMember>(
                                      Func<TDestination, TDestMember> destinationMember,
                                      Action<FakeMapOptions<TSource, TDestMember>> optionsAction)
                                  {
                                      return this;
                                  }
                              }

                              public class Profile
                              {
                                  public FakeMapExpression<TSource, TDestination> CreateMap<TSource, TDestination>() => new();
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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenExplicitMappingProvidedWithParenthesizedLambda()
    {
        string testCode = """

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
                                          .ForMember((dest) => dest.Name, (opt) => opt.MapFrom((src) => src.Name ?? "fallback"));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenNullableAndTypeIncompatible()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public string? Age { get; set; }
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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_ForObliviousReferenceTypes()
    {
        string testCode = """

                          #nullable disable
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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_ForNullableValueTypeInObliviousContext()
    {
        string testCode = """

                          #nullable disable
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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Age", "Source", "int?",
                "Destination", "int"));
    }

    // --- Collection element nullability -------------------------------------------------------------
    // AM002 catches a null collection (List<string>?) but historically missed a non-null collection whose
    // ELEMENTS are nullable (List<string?>). A null element flows straight into a non-nullable element slot,
    // the same NRE class one level down. Scoped to reference-type elements with the same underlying type so
    // genuine element-type mismatches (and value-type nullables) stay AM021/AM001's responsibility.

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenNullableElementListToNonNullableElementList()
    {
        string testCode = """

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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Tags", "Source",
                "System.Collections.Generic.List<string?>", "Destination", "System.Collections.Generic.List<string>"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenNullableElementArrayToNonNullableElementArray()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public string?[] Tags { get; set; }
                              }

                              public class Destination
                              {
                                  public string[] Tags { get; set; }
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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Tags", "Source",
                "string?[]", "Destination", "string[]"));
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenNullableElementToNullableElement()
    {
        string testCode = """

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
                                  public List<string?> Tags { get; set; }
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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenNonNullableElementToNullableElement()
    {
        // Non-null element -> nullable element widens nullability safely; the Error rule must stay silent.
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System.Collections.Generic;

                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public List<string> Tags { get; set; }
                              }

                              public class Destination
                              {
                                  public List<string?> Tags { get; set; }
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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenElementNullabilityHandledByIgnore()
    {
        string testCode = """

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
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Tags, opt => opt.Ignore());
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenElementTypesDifferentReferenceTypes()
    {
        // Different reference element types are a type mismatch (AM021's lane), not nullability — AM002 must
        // not fire just because the source element is nullable.
        string testCode = """

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
                                  public List<object> Tags { get; set; }
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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

}

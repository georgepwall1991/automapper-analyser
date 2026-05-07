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
}

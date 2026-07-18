using AutoMapper;
using AutoMapperAnalyzer.Analyzers.TypeSafety;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.TypeSafety;

public class AM002_NullableCompatibilityTests
{
    private sealed record RuntimeSource(string? Value);
    private sealed record PostMappedRuntimeSource(string Safe, string? NullableValue);

    private sealed class SameNamedRuntimeDestination
    {
        public SameNamedRuntimeDestination(string Value)
        {
            Other = Value;
        }

        public string Value { get; set; } = "initial";

        public string Other { get; }
    }

    private sealed class PostMappedRuntimeDestination
    {
        public PostMappedRuntimeDestination(string Value)
        {
            this.Value = Value;
        }

        public string Value { get; set; }
    }

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
    public void AM002_Runtime_ShouldConsumeSameNamedConstructorParameterBeforeWritableMemberMapping()
    {
        var configuration = new MapperConfiguration(config => config
            .CreateMap<RuntimeSource, SameNamedRuntimeDestination>()
            .ForCtorParam(
                nameof(SameNamedRuntimeDestination.Value),
                options => options.MapFrom(source => source.Value ?? string.Empty)));

        configuration.AssertConfigurationIsValid();

        SameNamedRuntimeDestination result = configuration.CreateMapper()
            .Map<SameNamedRuntimeDestination>(new RuntimeSource(null));

        Assert.Equal("initial", result.Value);
        Assert.Equal(string.Empty, result.Other);
    }

    [Fact]
    public void AM002_Runtime_ShouldConsumeExplicitSameNamedMemberMappingWithConstructorParameter()
    {
        var configuration = new MapperConfiguration(config => config
            .CreateMap<PostMappedRuntimeSource, PostMappedRuntimeDestination>()
            .ForCtorParam(
                nameof(PostMappedRuntimeDestination.Value),
                options => options.MapFrom(source => source.Safe))
            .ForMember(
                destination => destination.Value,
                options => options.MapFrom(source => source.NullableValue)));

        configuration.AssertConfigurationIsValid();

        PostMappedRuntimeDestination result = configuration.CreateMapper()
            .Map<PostMappedRuntimeDestination>(new PostMappedRuntimeSource("safe", null));

        Assert.Equal("safe", result.Value);
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
    public async Task AM002_ShouldReportReverseMapDiagnostic_WhenOnlyReverseLosesNullability()
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
                                      CreateMap<Source, Destination>()
                                          .ForMember(dest => dest.Name, opt => opt.NullSubstitute("fallback"))
                                          .ReverseMap();
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Destination",
                "string?", "Source", "string"));
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
    public async Task AM002_ShouldNotReportDiagnostic_WhenExplicitMappingUsesNameofDestinationMember()
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
                                          .ForMember(nameof(Destination.Name), opt => opt.MapFrom(src => src.Name ?? "default"));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenExplicitMappingUsesStringDestinationMember()
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
                                          .ForMember("Name", opt => opt.MapFrom(src => src.Name ?? "default"));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenExplicitMappingUsesConstantDestinationMember()
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
                                  private const string NameMember = nameof(Destination.Name);

                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(NameMember, opt => opt.MapFrom(src => src.Name ?? "default"));
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
    public async Task AM002_ShouldNotReportDiagnostic_WhenTypedTopLevelForPathHandlesNullability()
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
                                          .ForPath((Destination dest) => dest.Name,
                                              opt => opt.MapFrom((Source src) => src.Name ?? string.Empty));
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
    public async Task AM002_ShouldReportDiagnostic_WhenGenericNullableTypeParameterMapFromUsesNullForgivingOnly()
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
                                          .ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.Value!));
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
    public async Task AM002_ShouldNotReportDiagnostic_WhenExplicitMappingProvidedWithTypedLambdaParameters()
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
                                          .ForMember((Destination dest) => dest.Name,
                                              opt => opt.MapFrom((Source src) => src.Name ?? "fallback"));
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

    [Theory]
    [InlineData("\"Value\"")]
    [InlineData("nameof(Destination.Value)")]
    [InlineData("ValueParameter")]
    [InlineData("ParameterNames.ValueConstructorParameter")]
    public async Task AM002_ShouldNotReportDiagnostic_WhenForCtorParamProducesNonNullableValue(
        string parameterNameExpression)
    {
        string testCode = $$"""

                            #nullable enable
                            using AutoMapper;

                            namespace TestNamespace
                            {
                                public sealed record Source(string? Value);
                                public sealed record Destination(string Value);

                                public sealed class TestProfile : Profile
                                {
                                    private const string ValueParameter = nameof(Destination.Value);

                                    private static class ParameterNames
                                    {
                                        public const string ValueConstructorParameter = nameof(Destination.Value);
                                    }

                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam({{parameterNameExpression}}, options =>
                                                options.MapFrom(source => source.Value ?? string.Empty));
                                    }
                                }
                            }
                            """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Theory]
    [InlineData("source.Value")]
    [InlineData("source.Value!")]
    [InlineData("(source.Value!)")]
    public async Task AM002_ShouldReportDiagnostic_WhenForCtorParamRemainsNullable(string mapFromExpression)
    {
        string testCode = $$"""

                            #nullable enable
                            using AutoMapper;

                            namespace TestNamespace
                            {
                                public sealed record Source(string? Value);
                                public sealed record Destination(string Value);

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam(nameof(Destination.Value), options =>
                                                options.MapFrom(source => {{mapFromExpression}}));
                                    }
                                }
                            }
                            """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 14, 13,
                "Value", "Source", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldAcceptNullableForCtorParamWhenOwnedPropertyIsNonNullable()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);

                              public sealed class Destination
                              {
                                  public Destination(string? Value)
                                  {
                                      this.Value = Value ?? string.Empty;
                                  }

                                  public string Value { get; }
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam(nameof(Destination.Value), options =>
                                              options.MapFrom(source => source.Value));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportNonNullableForCtorParamWhenOwnedPropertyIsNullable()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);

                              public sealed class Destination
                              {
                                  public Destination(string Value)
                                  {
                                      this.Value = Value;
                                  }

                                  public string? Value { get; }
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam(nameof(Destination.Value), options =>
                                              options.MapFrom(source => source.Value));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 23, 13,
                "Value", "Source", "string?", "Destination", "string"));
    }


    [Fact]
    public async Task AM002_ShouldReportNullableElementsForForCtorParamCollection()
    {
        string testCode = """

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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 15, 13,
                "Values", "Source", "System.Collections.Generic.List<string?>", "Destination",
                "System.Collections.Generic.List<string>"));
    }

    [Fact]
    public async Task AM002_ShouldUseLongestConstructorElementNullability()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System.Collections.Generic;

                          namespace TestNamespace
                          {
                              public sealed record Source(List<string?> Values);

                              public sealed class Destination
                              {
                                  public Destination(List<string?> Values)
                                  {
                                      this.Values = Values;
                                  }

                                  public Destination(List<string> Values, int marker = 0)
                                  {
                                      this.Values = new List<string?>();
                                  }

                                  public List<string?> Values { get; }
                              }

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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 29, 13,
                "Values", "Source", "System.Collections.Generic.List<string?>", "Destination",
                "System.Collections.Generic.List<string>"));
    }

    [Fact]
    public async Task AM002_ShouldReportNestedNullableElementsForForCtorParamCollection()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System.Collections.Generic;

                          namespace TestNamespace
                          {
                              public sealed record Source(List<List<string?>> Values);
                              public sealed record Destination(List<List<string>> Values);

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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 15, 13,
                "Values", "Source", "System.Collections.Generic.List<System.Collections.Generic.List<string?>>",
                "Destination", "System.Collections.Generic.List<System.Collections.Generic.List<string>>"));
    }

    [Fact]
    public async Task AM002_ShouldUseLongestConstructorContainerNullability()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System.Collections.Generic;

                          namespace TestNamespace
                          {
                              public sealed record Source(List<string?>? Values);

                              public sealed class Destination
                              {
                                  public Destination(List<string?> Values)
                                  {
                                      this.Values = null;
                                  }

                                  public Destination(List<string>? Values, int marker = 0)
                                  {
                                      this.Values = null;
                                  }

                                  public List<string?>? Values { get; }
                              }

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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 29, 13,
                "Values", "Source", "System.Collections.Generic.List<string?>?", "Destination",
                "System.Collections.Generic.List<string>?"));
    }

    [Fact]
    public async Task AM002_ShouldReportNullableElementsAcrossCompatibleConstructorCollections()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System.Collections.Generic;

                          namespace TestNamespace
                          {
                              public sealed record Source(List<string?> Values);
                              public sealed record Destination(IEnumerable<string> Values);

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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 15, 13,
                "Values", "Source", "System.Collections.Generic.List<string?>", "Destination",
                "System.Collections.Generic.IEnumerable<string>"));
    }

    [Fact]
    public async Task AM002_ShouldIgnoreNullabilityFromUnselectableConstructorOverload()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);
                              public sealed class Unmapped;

                              public sealed class Destination
                              {
                                  public Destination(string? Value)
                                  {
                                      this.Value = Value;
                                  }

                                  public Destination(string Value, Unmapped required)
                                  {
                                      this.Value = Value;
                                  }

                                  public string? Value { get; }
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam(nameof(Destination.Value), options =>
                                              options.MapFrom(source => source.Value));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldIncludeExplicitlyConfiguredConstructorSiblingInSelectability()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);
                              public sealed record Destination(string Value, int count);

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam(nameof(Destination.Value), options =>
                                              options.MapFrom(source => source.Value ?? string.Empty))
                                          .ForCtorParam("count", options => options.MapFrom(_ => 0));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportNullableNestedCollectionAcrossCompatibleContainers()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System.Collections.Generic;

                          namespace TestNamespace
                          {
                              public sealed record Source(List<List<string>?> Values);
                              public sealed record Destination(IEnumerable<IEnumerable<string>> Values);

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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 15, 13,
                "Values", "Source", "System.Collections.Generic.List<System.Collections.Generic.List<string>?>",
                "Destination",
                "System.Collections.Generic.IEnumerable<System.Collections.Generic.IEnumerable<string>>"));
    }

    [Fact]
    public async Task AM002_ShouldUseLongestSelectableCompatibleCollectionConstructor()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System.Collections.Generic;

                          namespace TestNamespace
                          {
                              public sealed record Source(List<string?> Values);

                              public sealed class Destination
                              {
                                  public Destination(IEnumerable<string> Values)
                                  {
                                      this.Values = new List<string?>();
                                  }

                                  public Destination(List<string> Values, int marker = 0)
                                  {
                                      this.Values = new List<string?>();
                                  }

                                  public IEnumerable<string?> Values { get; }
                              }

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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 29, 13,
                "Values", "Source", "System.Collections.Generic.List<string?>", "Destination",
                "System.Collections.Generic.List<string>"));
    }

    [Fact]
    public async Task AM002_ShouldUsePublicSourceFieldForConstructorSelectability()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed class Source
                              {
                                  public string? Value { get; set; }
                                  public int count;
                              }

                              public sealed class Destination
                              {
                                  public Destination(string Value, int count)
                                  {
                                      this.Value = null;
                                  }

                                  public string? Value { get; }
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam(nameof(Destination.Value), options =>
                                              options.MapFrom(source => source.Value));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 27, 13,
                "Value", "Source", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldUseInheritedPublicSourceFieldForConstructorSelectability()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public class SourceBase
                              {
                                  public int count;
                              }

                              public sealed class Source : SourceBase
                              {
                                  public string? Value { get; set; }
                              }

                              public sealed class Destination
                              {
                                  public Destination(string Value, int count)
                                  {
                                      this.Value = null;
                                  }

                                  public string? Value { get; }
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam(nameof(Destination.Value), options =>
                                              options.MapFrom(source => source.Value));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 31, 13,
                "Value", "Source", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldContinueAnalyzingIncompleteForCtorParamCall()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);
                              public sealed record Destination(string Value);

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam();
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 14, 13,
                "Value", "Source", "string?", "Destination", "string"),
            DiagnosticResult.CompilerError("CS7036")
                .WithLocation(15, 18));
    }

    [Fact]
    public async Task AM002_ShouldNotCycleForSelfEnumeratingConstructorTypes()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System.Collections;
                          using System.Collections.Generic;

                          namespace TestNamespace
                          {
                              public sealed class SourceSequence : IEnumerable<SourceSequence>
                              {
                                  public IEnumerator<SourceSequence> GetEnumerator()
                                  {
                                      yield break;
                                  }

                                  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                              }

                              public sealed class DestinationSequence : IEnumerable<DestinationSequence>
                              {
                                  public IEnumerator<DestinationSequence> GetEnumerator()
                                  {
                                      yield break;
                                  }

                                  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                              }

                              public sealed record Source(SourceSequence Values);
                              public sealed record Destination(DestinationSequence Values);

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

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldAnalyzeConfiguredConstructorParameterWithoutDestinationProperty()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 23, 13,
                "token", "Source", "string?", "Destination", "string"));
    }


    [Fact]
    public async Task AM002_ShouldTreatFlattenedRequiredConstructorSiblingAsSelectable()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 27, 13,
                "token", "Source", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldNotTreatIncompleteFlattenedRequiredConstructorSiblingAsSelectable()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Customer(string Other);
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }


    [Fact]
    public async Task AM002_ShouldIgnorePropertyAssignmentsFromUnselectableConstructors()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? token);
                              public sealed class Unmapped;

                              public sealed class Destination
                              {
                                  private readonly string _token;

                                  public Destination(string token)
                                  {
                                      _token = token;
                                  }

                                  public Destination(string token, Unmapped required)
                                  {
                                      Value = token;
                                  }

                                  public string Value { get; } = string.Empty;
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 31, 13,
                "token", "Source", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldAnalyzeConstructorParameterAssignedToWritablePropertyWithoutConventionSource()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input);

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
                          }
                          """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(23, 13)
            .WithArguments("valueParam", "Source", "Input", "string?", "Destination", "string");

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task AM002_ShouldTreatNestedGetMethodAsFlattenedConstructorSibling()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed class Customer
                              {
                                  public string GetName() => string.Empty;
                              }

                              public sealed record Source(string? token, Customer Customer);

                              public sealed class Destination
                              {
                                  public Destination(string token, string customerName)
                                  {
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 25, 13,
                "token", "Source", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldTreatRootGetMethodAsConstructorSibling()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed class Source
                              {
                                  public string? token { get; set; }
                                  public int GetTotal() => 0;
                              }

                              public sealed class Destination
                              {
                                  public Destination(string token, int total)
                                  {
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 24, 13,
                "token", "Source", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldAnalyzeWritableAliasConstructorInputBeforeSafePropertyMapping()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 25, 13,
                "valueParam", "Source", "Value", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldUseLongestSelectableConstructorNullability()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);

                              public sealed class Destination
                              {
                                  public Destination(string Value)
                                  {
                                  }

                                  public Destination(string? Value, int marker = 0)
                                  {
                                  }
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportLongestSelectableConstructorNullability()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);

                              public sealed class Destination
                              {
                                  public Destination(object? Value)
                                  {
                                  }

                                  public Destination(string Value, int marker = 0)
                                  {
                                  }
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 24, 13,
                "Value", "Source", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldSelectConstructorBeforeResolvingConfiguredParameter()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value, string Other);

                              public sealed class Destination
                              {
                                  public Destination(string Value)
                                  {
                                  }

                                  public Destination(string Other, int marker = 0)
                                  {
                                  }
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldIgnoreNonPublicSourcePropertyWhenSelectingConstructor()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed class Source
                              {
                                  public string? value { get; set; }
                                  internal string Hidden { get; set; } = string.Empty;
                              }

                              public sealed class Destination
                              {
                                  public Destination(string? value)
                                  {
                                  }

                                  public Destination(string value, string Hidden)
                                  {
                                  }
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("value", options =>
                                              options.MapFrom(source => source.value));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldRecognizeWritableAliasOnConstructedGenericDestination()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

                              public sealed class Destination<T> where T : notnull
                              {
                                  public Destination(T valueParam)
                                  {
                                      Value = valueParam;
                                  }

                                  public T Value { get; set; }
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination<string>>()
                                          .ForCtorParam("valueParam", options =>
                                              options.MapFrom(source => source.Input));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldDelegateWritableAliasToPublicFieldConvention()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed class Source
                              {
                                  public string? Input { get; set; }
                                  public string Value = string.Empty;
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldDelegateWritableAliasToGetMethodConvention()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed class Source
                              {
                                  public string? Input { get; set; }
                                  public string GetValue() => string.Empty;
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportNullablePublicFieldConventionForWritableAlias()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed class Source
                              {
                                  public string Input { get; set; } = string.Empty;
                                  public string? Value;
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
                          }
                          """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(27, 13)
            .WithArguments("Value", "Source", "Value", "string?", "Destination", "string");

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task AM002_ShouldReportNullableGetMethodConventionForWritableAlias()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
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
                          }
                          """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(27, 13)
            .WithArguments("Value", "Source", "GetValue", "string?", "Destination", "string");

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task AM002_ShouldIgnoreInvalidSameNameForCtorParamAfterSafeForMember()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
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
                                          .ForMember(destination => destination.Value, options =>
                                              options.MapFrom(source => source.Value ?? string.Empty))
                                          .ForCtorParam("Value", options =>
                                              options.MapFrom(source => source.Value));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldIgnoreDifferentlyCasedConstructorParameterName()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input);

                              public sealed class Destination
                              {
                                  public Destination(string valueParam)
                                  {
                                  }
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("ValueParam", options =>
                                              options.MapFrom(source => source.Input));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldDelegateWritableAliasToUnconditionalForMember()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Safe);

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
                                              options.MapFrom(source => source.Input))
                                          .ForMember(destination => destination.Value, options =>
                                              options.MapFrom(source => source.Safe));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenForMemberCanBeVetoed()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Safe);

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
                                              options.MapFrom(source => source.Input))
                                          .ForMember(destination => destination.Value, options =>
                                          {
                                              options.PreCondition((Source source) => false);
                                              options.MapFrom(source => source.Safe);
                                          });
                                  }
                              }
                          }
                          """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(23, 13)
            .WithArguments("valueParam", "Source", "Input", "string?", "Destination", "string");

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenConventionForMemberIsIgnored()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                              options.MapFrom(source => source.Input))
                                          .ForMember(destination => destination.Value, options =>
                                              options.Ignore());
                                  }
                              }
                          }
                          """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(23, 13)
            .WithArguments("valueParam", "Source", "Input", "string?", "Destination", "string");

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }


    [Fact]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenConventionForMemberHasCondition()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                              options.MapFrom(source => source.Input))
                                          .ForMember(destination => destination.Value, options =>
                                              options.Condition((Source source) => false));
                                  }
                              }
                          }
                          """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(23, 13)
            .WithArguments("valueParam", "Source", "Input", "string?", "Destination", "string");

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }


    [Fact]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenConventionForMemberHasPreCondition()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                              options.MapFrom(source => source.Input))
                                          .ForMember(destination => destination.Value, options =>
                                              options.PreCondition((Source source) => false));
                                  }
                              }
                          }
                          """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(23, 13)
            .WithArguments("valueParam", "Source", "Input", "string?", "Destination", "string");

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }


    [Fact]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenConventionForPathIsIgnored()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                              options.MapFrom(source => source.Input))
                                          .ForPath(destination => destination.Value, options =>
                                              options.Ignore());
                                  }
                              }
                          }
                          """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(23, 13)
            .WithArguments("valueParam", "Source", "Input", "string?", "Destination", "string");

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }


    [Fact]
    public async Task AM002_ShouldDelegateWritableAliasToUnconditionalTopLevelForPath()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Safe);

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
                                              options.MapFrom(source => source.Input))
                                          .ForPath(destination => destination.Value, options =>
                                              options.MapFrom(source => source.Safe));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }


    [Fact]
    public async Task AM002_ShouldUseTheLastWritableAliasConfiguration()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Safe);

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
                                              options.MapFrom(source => source.Input))
                                          .ForMember(destination => destination.Value, options =>
                                              options.Ignore())
                                          .ForPath(destination => destination.Value, options =>
                                              options.MapFrom(source => source.Safe));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }


    [Fact]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenTheLastConfigurationVetoesAssignment()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Safe);

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
                                              options.MapFrom(source => source.Input))
                                          .ForPath(destination => destination.Value, options =>
                                              options.MapFrom(source => source.Safe))
                                          .ForMember(destination => destination.Value, options =>
                                              options.Ignore());
                                  }
                              }
                          }
                          """;

        DiagnosticResult expected = new DiagnosticResult(
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            .WithLocation(23, 13)
            .WithArguments("valueParam", "Source", "Input", "string?", "Destination", "string");

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }


    [Fact]
    public async Task AM002_ShouldIgnoreUnselectableOverloadWhenResolvingReadOnlyAliasOwnership()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);
                              public sealed class Unmapped;

                              public sealed class Destination
                              {
                                  public Destination(string valueParam)
                                  {
                                      Value = valueParam;
                                  }

                                  public Destination(string valueParam, Unmapped required)
                                  {
                                      Value = string.Empty;
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 29, 13,
                "Value", "Source", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenForCtorParamTargetsAnotherParameter()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value, string Other);
                              public sealed record Destination(string Value, string Other);

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam(nameof(Destination.Other), options =>
                                              options.MapFrom(source => source.Other));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 14, 13,
                "Value", "Source", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldNotApplyReverseForCtorParamToForwardDirection()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);
                              public sealed record Destination(string Value);

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ReverseMap()
                                          .ForCtorParam(nameof(Source.Value), options =>
                                              options.MapFrom(source => source.Value));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 14, 13,
                "Value", "Source", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldPreferSafeForCtorParamOverLaterUnsafeForMember()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
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
                                              options.MapFrom(source => source.Value));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldPreferUnsafeForCtorParamOverLaterSafeForMember()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 14, 13,
                "Value", "Source", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldIgnoreReadOnlyDestinationPropertyWithCtorAlias()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportUnsafeCtorAliasAssignedToReadOnlyDestinationProperty()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 23, 13,
                "Value", "Source", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldAnalyzeConstructorParameterWhenAliasHasCompetingDirectWrite()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);

                              public sealed class Destination
                              {
                                  public Destination(string valueParam)
                                  {
                                      Value = valueParam;
                                      Value = string.Empty;
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 24, 13,
                "valueParam", "Source", "Value", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldAnalyzeConstructorParameterWhenAliasHasNestedSynchronousWrite()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);

                              public sealed class Destination
                              {
                                  public Destination(string valueParam)
                                  {
                                      Value = valueParam;
                                      {
                                          Value = string.Empty;
                                      }
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 26, 13,
                "valueParam", "Source", "Value", "string?", "Destination", "string"));
    }


    [Fact]
    public async Task AM002_ShouldAnalyzeConstructorParameterWhenAliasHasCompoundRefWrite()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed class DestinationValue
                              {
                                  public static DestinationValue operator +(
                                      DestinationValue left,
                                      DestinationValue right) => null!;
                              }

                              public sealed record Source(DestinationValue? Value);

                              public sealed class Destination
                              {
                                  private DestinationValue _value = new();

                                  public Destination(DestinationValue valueParam, DestinationValue otherParam)
                                  {
                                      Value = valueParam;
                                      Value += otherParam;
                                  }

                                  public ref DestinationValue Value => ref _value;
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("valueParam", options =>
                                              options.MapFrom(source => source.Value))
                                          .ForCtorParam("otherParam", options =>
                                              options.MapFrom(_ => new DestinationValue()));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 33, 13,
                "valueParam", "Source", "Value", "TestNamespace.DestinationValue?", "Destination", "TestNamespace.DestinationValue"));
    }


    [Fact]
    public async Task AM002_ShouldAnalyzeConstructorParameterAssignedToOtherInstance()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);

                              public sealed class Destination
                              {
                                  private string _value = string.Empty;

                                  public Destination(string valueParam, Destination other)
                                  {
                                      other.Value = valueParam;
                                  }

                                  public ref string Value => ref _value;
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("valueParam", options =>
                                              options.MapFrom(source => source.Value))
                                          .ForCtorParam("other", options =>
                                              options.MapFrom(_ => new Destination(string.Empty, null!)));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 25, 13,
                "valueParam", "Source", "Value", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldRetainCtorAliasOwnershipWhenOtherInstanceIsWritten()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);

                              public sealed class Destination
                              {
                                  private string _value = string.Empty;

                                  public Destination(string valueParam, Destination other)
                                  {
                                      Value = valueParam;
                                      other.Value = string.Empty;
                                  }

                                  public ref string Value => ref _value;
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("valueParam", options =>
                                              options.MapFrom(source => source.Value))
                                          .ForCtorParam("other", options =>
                                              options.MapFrom(_ => new Destination(string.Empty, null!)));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 26, 13,
                "Value", "Source", "string?", "Destination", "string"));
    }


    [Fact]
    public async Task AM002_ShouldAnalyzeConstructorParameterWhenMatchingOverloadDoesNotOwnProperty()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);

                              public sealed class Destination
                              {
                                  public Destination(string valueParam)
                                  {
                                      Value = valueParam;
                                  }

                                  public Destination(string valueParam, string otherParam)
                                  {
                                      Value = string.Empty;
                                  }

                                  public string Value { get; }
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("valueParam", options =>
                                              options.MapFrom(source => source.Value))
                                          .ForCtorParam("otherParam", options =>
                                              options.MapFrom(_ => string.Empty));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 28, 13,
                "valueParam", "Source", "Value", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldNotTreatDeferredLambdaAssignmentAsConstructorOwnership()
    {
        string testCode = """

                          #nullable enable
                          using System;
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);

                              public sealed class Destination
                              {
                                  public Destination(string valueParam)
                                  {
                                      _ = new Action(() => Value = valueParam);
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 24, 13,
                "Value", "Source", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldIgnoreReadOnlyDestinationPropertyWithExpressionBodiedCtorAlias()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);

                              public sealed class Destination
                              {
                                  public Destination(string valueParam) => Value = valueParam;

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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldNotTreatCompoundAssignmentAsDirectConstructorOwnership()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);

                              public sealed class Destination
                              {
                                  public Destination(string valueParam)
                                  {
                                      Value += valueParam;
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 23, 13,
                "Value", "Source", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldIgnoreBlankForCtorParamName()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);
                              public sealed record Destination(string Value);

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam(" ", options =>
                                              options.MapFrom(source => source.Value ?? string.Empty));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 14, 13,
                "Value", "Source", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportWhenAliasedConstructorPropertyRemainsWritable()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
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
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 23, 13,
                "Value", "Source", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldTreatSameNamedConstructorParameterAsConsumedDestinationMember()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);

                              public sealed class Destination
                              {
                                  public Destination(string Value)
                                  {
                                      Other = Value;
                                  }

                                  public string Value { get; set; } = string.Empty;
                                  public string Other { get; }
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam(nameof(Destination.Value), options =>
                                              options.MapFrom(source => source.Value ?? string.Empty));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Theory]
    [InlineData("options.Ignore()")]
    [InlineData("options.Condition((source, destination, sourceMember) => false)")]
    [InlineData("options.PreCondition((Source source) => false)")]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenForAllMembersVetoesConvention(
        string vetoStatement)
    {
        string testCode = $$"""

                            #nullable enable
                            using AutoMapper;

                            namespace TestNamespace
                            {
                                public sealed record Source(string? Input, string Value);

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
                                                options.MapFrom(source => source.Input))
                                            .ForAllMembers(options => {{vetoStatement}});
                                    }
                                }
                            }
                            """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 23, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenForAllMembersVetoesEarlierForMember()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Safe);

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
                                              options.MapFrom(source => source.Input))
                                          .ForMember(destination => destination.Value, options =>
                                              options.MapFrom(source => source.Safe))
                                          .ForAllMembers(options => options.Ignore());
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 23, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Theory]
    [InlineData("options.Ignore()")]
    [InlineData("options.Condition((source, destination, sourceMember) => false)")]
    [InlineData("options.PreCondition((Source source) => false)")]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenInvokedLocalFunctionVetoesForAllMembers(
        string vetoStatement)
    {
        string testCode = $$"""

                            #nullable enable
                            using AutoMapper;

                            namespace TestNamespace
                            {
                                public sealed record Source(string? Input, string Value);

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
                                                options.MapFrom(source => source.Input))
                                            .ForAllMembers(options =>
                                            {
                                                void ApplyVeto() => {{vetoStatement}};
                                                ApplyVeto();
                                            });
                                    }
                                }
                            }
                            """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 23, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldIgnoreInvokedIteratorLocalFunctionUntilItIsEnumerated()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System.Collections.Generic;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                              options.MapFrom(source => source.Input))
                                          .ForAllMembers(options =>
                                          {
                                              IEnumerable<int> Deferred()
                                              {
                                                  options.Ignore();
                                                  yield break;
                                              }

                                              _ = Deferred();
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenInvokedLocalFunctionCallsNestedVeto()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                              options.MapFrom(source => source.Input))
                                          .ForAllMembers(options =>
                                          {
                                              void ApplyVeto() => Veto();
                                              void Veto() => options.Ignore();
                                              ApplyVeto();
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 23, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenInvokedHelperDeclaresNestedVeto()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                              options.MapFrom(source => source.Input))
                                          .ForAllMembers(options =>
                                          {
                                              void Outer()
                                              {
                                                  void Veto() => options.Ignore();
                                                  Veto();
                                              }

                                              Outer();
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 23, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldTerminateWhenInvokedLocalFunctionsAreMutuallyRecursive()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                              options.MapFrom(source => source.Input))
                                          .ForAllMembers(options =>
                                          {
                                              void First() => Second();
                                              void Second() => First();
                                              First();
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Theory]
    [InlineData("current.Ignore()")]
    [InlineData("current.Condition((source, destination, sourceMember) => false)")]
    [InlineData("current.PreCondition((Source source) => false)")]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenInvokedLocalFunctionAliasesOptions(
        string vetoStatement)
    {
        string testCode = $$"""

                            #nullable enable
                            using AutoMapper;

                            namespace TestNamespace
                            {
                                public sealed record Source(string? Input, string Value);

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
                                                options.MapFrom(source => source.Input))
                                            .ForAllMembers(options =>
                                            {
                                                void ApplyVeto()
                                                {
                                                    var current = options;
                                                    {{vetoStatement}};
                                                }

                                                ApplyVeto();
                                            });
                                    }
                                }
                            }
                            """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 23, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenIteratorLocalFunctionIsEnumerated()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System.Collections.Generic;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                              options.MapFrom(source => source.Input))
                                          .ForAllMembers(options =>
                                          {
                                              IEnumerable<int> Deferred()
                                              {
                                                  options.Ignore();
                                                  yield break;
                                              }

                                              foreach (int _ in Deferred())
                                              {
                                              }
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 24, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenIteratorLocalAliasIsEnumerated()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System.Collections.Generic;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                              options.MapFrom(source => source.Input))
                                          .ForAllMembers(options =>
                                          {
                                              IEnumerable<int> Deferred()
                                              {
                                                  options.Ignore();
                                                  yield break;
                                              }

                                              var sequence = Deferred();
                                              var alias = sequence;
                                              foreach (int _ in alias)
                                              {
                                              }
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 24, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenAssignedIteratorResultIsEnumerated()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System;
                          using System.Collections.Generic;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                              options.MapFrom(source => source.Input))
                                          .ForAllMembers(options =>
                                          {
                                              IEnumerable<int> Deferred()
                                              {
                                                  options.Ignore();
                                                  yield break;
                                              }

                                              IEnumerable<int> items = Array.Empty<int>();
                                              items = Deferred();
                                              foreach (int _ in items)
                                              {
                                              }
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 25, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Theory]
    [InlineData(
        "IEnumerable<int> sequence; var alias = sequence = Deferred();",
        "alias")]
    [InlineData(
        "IEnumerable<int> first; IEnumerable<int> second; first = second = Deferred();",
        "first")]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenChainedIteratorResultIsEnumerated(
        string iteratorAssignment,
        string enumeratedAlias)
    {
        string testCode = $$"""

                            #nullable enable
                            using AutoMapper;
                            using System.Collections.Generic;

                            namespace TestNamespace
                            {
                                public sealed record Source(string? Input, string Value);

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
                                                options.MapFrom(source => source.Input))
                                            .ForAllMembers(options =>
                                            {
                                                IEnumerable<int> Deferred()
                                                {
                                                    options.Ignore();
                                                    yield break;
                                                }

                                                {{iteratorAssignment}}
                                                foreach (int _ in {{enumeratedAlias}})
                                                {
                                                }
                                            });
                                    }
                                }
                            }
                            """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 24, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Theory]
    [InlineData("sequence = Array.Empty<int>();", "alias")]
    [InlineData("alias = Array.Empty<int>();", "sequence")]
    public async Task AM002_ShouldTrackReassignmentOnEnumeratedIteratorAliasPath(
        string reassignment,
        string enumeratedAlias)
    {
        string testCode = $$"""

                            #nullable enable
                            using AutoMapper;
                            using System;
                            using System.Collections.Generic;

                            namespace TestNamespace
                            {
                                public sealed record Source(string? Input, string Value);

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
                                                options.MapFrom(source => source.Input))
                                            .ForAllMembers(options =>
                                            {
                                                IEnumerable<int> Deferred()
                                                {
                                                    options.Ignore();
                                                    yield break;
                                                }

                                                IEnumerable<int> sequence = Deferred();
                                                IEnumerable<int> alias = sequence;
                                                {{reassignment}}
                                                foreach (int _ in {{enumeratedAlias}})
                                                {
                                                }
                                            });
                                    }
                                }
                            }
                            """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 25, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldIgnoreIteratorLocalAliasReassignedBeforeEnumeration()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System;
                          using System.Collections.Generic;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                              options.MapFrom(source => source.Input))
                                          .ForAllMembers(options =>
                                          {
                                              IEnumerable<int> Deferred()
                                              {
                                                  options.Ignore();
                                                  yield break;
                                              }

                                              IEnumerable<int> sequence = Deferred();
                                              sequence = Array.Empty<int>();
                                              foreach (int _ in sequence)
                                              {
                                              }
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldIgnoreDeferredPerMemberVetoWhenConventionStillAssignsValue()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                              options.MapFrom(source => source.Input))
                                          .ForMember(destination => destination.Value, options =>
                                          {
                                              Action deferred = () => options.Ignore();
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenOptionsAliasIsAssignedBeforeVeto()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                              options.MapFrom(source => source.Input))
                                          .ForAllMembers(options =>
                                          {
                                              IMemberConfigurationExpression<Source, Destination, object> alias = null!;
                                              alias = options;
                                              alias.Ignore();
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 23, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldIgnoreOptionsAliasReassignedBeforeVeto()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                              options.MapFrom(source => source.Input))
                                          .ForAllMembers(options =>
                                          {
                                              var alias = options;
                                              alias = null!;
                                              alias.Ignore();
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenInvokedLocalFunctionReceivesOptionsParameter()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                              options.MapFrom(source => source.Input))
                                          .ForAllMembers(options =>
                                          {
                                              void ApplyVeto(
                                                  IMemberConfigurationExpression<Source, Destination, object> current) =>
                                                  current.Ignore();

                                              ApplyVeto(options);
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 23, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenOptionsAliasIsConditionallyReassignedBeforeVeto()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                  public TestProfile(bool replaceAlias)
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("valueParam", options =>
                                              options.MapFrom(source => source.Input))
                                          .ForAllMembers(options =>
                                          {
                                              var current = options;
                                              if (replaceAlias)
                                              {
                                                  current = null!;
                                              }

                                              current.Ignore();
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 23, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldIgnoreOptionsAliasDefinitelyReassignedBeforeSameBranchVeto()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                  public TestProfile(bool replaceAlias)
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("valueParam", options =>
                                              options.MapFrom(source => source.Input))
                                          .ForAllMembers(options =>
                                          {
                                              if (replaceAlias)
                                              {
                                                  var current = options;
                                                  current = null!;
                                                  current.Ignore();
                                              }
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportWhenExecutedConstructorMapFromUsesOptionsAlias()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input);

                              public sealed class Destination
                              {
                                  public Destination(string valueParam)
                                  {
                                  }
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("valueParam", options =>
                                          {
                                              var current = options;
                                              current.MapFrom(source => source.Input);
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 20, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportWhenInvokedLocalConstructorHelperUsesOptionsAlias()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input);

                              public sealed class Destination
                              {
                                  public Destination(string valueParam)
                                  {
                                  }
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("valueParam", options =>
                                          {
                                              void ApplyMap()
                                              {
                                                  var current = options;
                                                  current.MapFrom(source => source.Input);
                                              }

                                              ApplyMap();
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 20, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldIgnoreUninvokedConstructorHelperWhenExecutedMapFromIsSafe()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input);

                              public sealed class Destination
                              {
                                  public Destination(string valueParam)
                                  {
                                  }
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("valueParam", options =>
                                          {
                                              options.MapFrom(source => source.Input ?? string.Empty);

                                              void Deferred() =>
                                                  options.MapFrom(source => source.Input);
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportWhenConditionalConstructorMapFromHasUnsafeBranch()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input);

                              public sealed class Destination
                              {
                                  public Destination(string valueParam)
                                  {
                                  }
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile(bool useUnsafe)
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("valueParam", options =>
                                          {
                                              if (useUnsafe)
                                              {
                                                  options.MapFrom(source => source.Input);
                                              }
                                              else
                                              {
                                                  options.MapFrom(source => string.Empty);
                                              }
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 20, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenMapWideVetoReceiverIsParenthesized()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                              options.MapFrom(source => source.Input))
                                          .ForAllMembers(options => (options).Ignore());
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 23, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Theory]
    [InlineData("ToList")]
    [InlineData("ToArray")]
    public async Task AM002_ShouldAnalyzeWritableAliasWhenIteratorVetoIsMaterialized(
        string terminalMethod)
    {
        string testCode = $$"""

                            #nullable enable
                            using AutoMapper;
                            using System.Collections.Generic;
                            using System.Linq;

                            namespace TestNamespace
                            {
                                public sealed record Source(string? Input, string Value);

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
                                                options.MapFrom(source => source.Input))
                                            .ForAllMembers(options =>
                                            {
                                                IEnumerable<int> ApplyVeto()
                                                {
                                                    options.Ignore();
                                                    yield break;
                                                }

                                                ApplyVeto().{{terminalMethod}}();
                                            });
                                    }
                                }
                            }
                            """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 25, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldIgnoreNullSubstituteInsideUninvokedMemberHelper()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Name);

                              public sealed class Destination
                              {
                                  public string Name { get; set; } = string.Empty;
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(destination => destination.Name, options =>
                                          {
                                              options.MapFrom(source => source.Name);

                                              void Deferred()
                                              {
                                                  options.NullSubstitute(string.Empty);
                                              }
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 18, 13,
                "Name", "Source", "Name", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldRespectNestedHelperInvocationEvaluationOrder()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input);

                              public sealed class Destination
                              {
                                  public Destination(string valueParam)
                                  {
                                  }
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("valueParam", options =>
                                          {
                                              string Read()
                                              {
                                                  options.MapFrom(source => source.Input);
                                                  return string.Empty;
                                              }

                                              void Apply(string value)
                                              {
                                                  options.MapFrom(source => string.Empty);
                                              }

                                              Apply(Read());
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportWhenSafeMapFromReceiverIsOnlyConditionallyAliased()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using AutoMapper.Configuration;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? valueParam);
                              public sealed record Destination(string valueParam);

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile(
                                      ICtorParamConfigurationExpression<Source> otherOptions,
                                      bool useOther)
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("valueParam", options =>
                                          {
                                              var current = options;
                                              if (useOther)
                                              {
                                                  current = otherOptions;
                                              }

                                              current.MapFrom(source => string.Empty);
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 17, 13,
                "valueParam", "Source", "valueParam", "string?", "Destination", "string"));
    }


    [Fact]
    public async Task AM002_ShouldReportWhenInvokedHelperReassignsCapturedOptionsAlias()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using AutoMapper.Configuration;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? valueParam);
                              public sealed record Destination(string valueParam);

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile(
                                      ICtorParamConfigurationExpression<Source> otherOptions)
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("valueParam", options =>
                                          {
                                              var current = options;

                                              void Replace()
                                              {
                                                  current = otherOptions;
                                              }

                                              Replace();
                                              current.MapFrom(source => string.Empty);
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 16, 13,
                "valueParam", "Source", "valueParam", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldAnalyzeIteratorVetoAfterConditionalResultReassignment()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System;
                          using System.Collections.Generic;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                  public TestProfile(bool skip)
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("valueParam", options =>
                                              options.MapFrom(source => source.Input))
                                          .ForAllMembers(options =>
                                          {
                                              IEnumerable<int> Deferred()
                                              {
                                                  options.Ignore();
                                                  yield break;
                                              }

                                              var sequence = Deferred();
                                              if (skip)
                                              {
                                                  sequence = Array.Empty<int>();
                                              }

                                              foreach (int value in sequence)
                                              {
                                              }
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 25, 13,
                "valueParam", "Source", "Input", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportWhenArgumentHelperReassignsCapturedOptionsAlias()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using AutoMapper.Configuration;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? valueParam);
                              public sealed record Destination(string valueParam);

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile(
                                      ICtorParamConfigurationExpression<Source> otherOptions)
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("valueParam", options =>
                                          {
                                              var current = options;

                                              int Replace()
                                              {
                                                  current = otherOptions;
                                                  return 0;
                                              }

                                              void Apply(int _)
                                              {
                                                  current.MapFrom(source => string.Empty);
                                              }

                                              Apply(Replace());
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 16, 13,
                "valueParam", "Source", "valueParam", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldIgnoreIteratorVetoWhenConditionalReplacementDominatesEnumeration()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System;
                          using System.Collections.Generic;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Input, string Value);

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
                                  public TestProfile(bool skip)
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("valueParam", options =>
                                              options.MapFrom(source => source.Input))
                                          .ForAllMembers(options =>
                                          {
                                              IEnumerable<int> Deferred()
                                              {
                                                  options.Ignore();
                                                  yield break;
                                              }

                                              var sequence = Deferred();
                                              if (skip)
                                              {
                                                  sequence = Array.Empty<int>();
                                              }
                                              else
                                              {
                                                  return;
                                              }

                                              foreach (int value in sequence)
                                              {
                                              }
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportWhenRefHelperReassignsOptionsAlias()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using AutoMapper.Configuration;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? valueParam);
                              public sealed record Destination(string valueParam);

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile(
                                      ICtorParamConfigurationExpression<Source> otherOptions)
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam("valueParam", options =>
                                          {
                                              var current = options;

                                              void Replace(
                                                  ref ICtorParamConfigurationExpression<Source> candidate)
                                              {
                                                  candidate = otherOptions;
                                              }

                                              Replace(ref current);
                                              current.MapFrom(source => string.Empty);
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 16, 13,
                "valueParam", "Source", "valueParam", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldIgnoreMapFromAfterAwaitInAsyncOptionsCallback()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System.Threading.Tasks;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);
                              public sealed record Destination(string Value);

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam(nameof(Destination.Value), async options =>
                                          {
                                              await Task.Yield();
                                              options.MapFrom(source => string.Empty);
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 15, 13,
                "Value", "Source", "Value", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldRespectMapFromBeforeAwaitInAsyncOptionsCallback()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System.Threading.Tasks;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);
                              public sealed record Destination(string Value);

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam(nameof(Destination.Value), async options =>
                                          {
                                              options.MapFrom(source => string.Empty);
                                              await Task.Yield();
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM002_ShouldReportWhenNullSubstituteHelperIsOnlyConditionallyInvoked()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);
                              public sealed record Destination(string Value);

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile(bool enabled)
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForMember(destination => destination.Value, options =>
                                          {
                                              options.MapFrom(source => source.Value);

                                              void Apply() => options.NullSubstitute(string.Empty);
                                              if (enabled)
                                              {
                                                  Apply();
                                              }
                                          });
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 14, 13,
                "Value", "Source", "Value", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportWhenForCtorParamUsesLookalikeMapFromExtension()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using AutoMapper.Configuration;
                          using System;

                          namespace TestNamespace
                          {
                              public sealed record Source(string? Value);
                              public sealed record Destination(string Value);

                              public static class LookalikeExtensions
                              {
                                  public static void MapFrom(
                                      this ICtorParamConfigurationExpression<Source> options,
                                      Func<Source, string> selector,
                                      bool ignored)
                                  {
                                  }
                              }

                              public sealed class TestProfile : Profile
                              {
                                  public TestProfile()
                                  {
                                      CreateMap<Source, Destination>()
                                          .ForCtorParam(
                                              nameof(Destination.Value),
                                              options => options.MapFrom(source => string.Empty, true));
                                  }
                              }
                          }
                          """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 26, 13,
                "Value", "Source", "Value", "string?", "Destination", "string"));
    }

    [Fact]
    public async Task AM002_ShouldReportDiagnostic_WhenNullableInputFlowsThroughDoubleContravariance()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System;

                          namespace TestNamespace
                          {
                              public sealed class Source
                              {
                                  public Action<Action<string?>> Handler { get; set; }
                              }

                              public sealed class Destination
                              {
                                  public Action<Action<string>> Handler { get; set; }
                              }

                              public sealed class TestProfile : Profile
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
            Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13,
                "Handler", "Source", "System.Action<System.Action<string?>>", "Destination",
                "System.Action<System.Action<string>>"));
    }

    [Fact]
    public async Task AM002_ShouldNotReportDiagnostic_WhenContravariantSourceAcceptsNullableInput()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System;

                          namespace TestNamespace
                          {
                              public sealed class Source
                              {
                                  public Action<string?> Handler { get; set; }
                              }

                              public sealed class Destination
                              {
                                  public Action<string> Handler { get; set; }
                              }

                              public sealed class TestProfile : Profile
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
    public async Task AM002_ShouldNotReportDiagnostic_WhenNestedGenericIsInsideContravariantInput()
    {
        string testCode = """

                          #nullable enable
                          using AutoMapper;
                          using System;
                          using System.Collections.Generic;

                          namespace TestNamespace
                          {
                              public sealed class Source
                              {
                                  public Action<IEnumerable<string?>> Handler { get; set; }
                              }

                              public sealed class Destination
                              {
                                  public Action<IEnumerable<string>> Handler { get; set; }
                              }

                              public sealed class TestProfile : Profile
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

    [Theory]
    [InlineData("Action deferred = () => options.Ignore();")]
    [InlineData("void Deferred() => options.Ignore();")]
    public async Task AM002_ShouldIgnoreDeferredForAllMembersVetoCalls(string deferredStatement)
    {
        string testCode = $$"""

                            #nullable enable
                            using AutoMapper;
                            using System;

                            namespace TestNamespace
                            {
                                public sealed record Source(string? Input, string Value);

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
                                                options.MapFrom(source => source.Input))
                                            .ForAllMembers(options =>
                                            {
                                                {{deferredStatement}}
                                            });
                                    }
                                }
                            }
                            """;

        await AnalyzerVerifier<AM002_NullableCompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }
}

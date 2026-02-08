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
}

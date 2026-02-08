using AutoMapperAnalyzer.Analyzers.TypeSafety;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.TypeSafety;

public class AM001_PropertyTypeMismatchTests
{
    private static DiagnosticResult CreateDiagnostic(DiagnosticDescriptor descriptor, int line, int column,
        params object[] messageArgs)
    {
        return new DiagnosticResult(descriptor).WithLocation(line, column).WithArguments(messageArgs);
    }

    [Fact]
    public async Task Debug_AM001_SimpleTest()
    {
        const string
            testCode = """

                       using AutoMapper;

                       public class Source
                       {
                           public string Age { get; set; }
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
                       """;

        // Expect the analyzer to detect the string -> int type mismatch for Age property
        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 18, 9, "Age", "Source",
                "string", "Destination", "int"));
    }

    [Fact]
    public async Task AM001_ShouldReportDiagnostic_WhenStringMappedToInt()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Age { get; set; }
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

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 20, 13, "Age", "Source",
                "string", "Destination", "int"));
    }

    [Fact]
    public async Task AM001_ShouldReportDiagnostic_WhenSourcePropertyIsReadOnly()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Source(string age)
                                        {
                                            Age = age;
                                        }

                                        public string Age { get; }
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

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 25, 13, "Age", "Source",
                "string", "Destination", "int"));
    }

    [Fact]
    public async Task AM001_ShouldNotReportDiagnostic_WhenTypesAreCompatible()
    {
        const string testCode = """

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

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM001_ShouldNotReportDiagnostic_WhenNestedMappingExistsInSeparateProfile()
    {
        const string mainProfile = """

                                   using AutoMapper;

                                   namespace TestNamespace
                                   {
                                       public class Address
                                       {
                                           public string Street { get; set; }
                                       }

                                       public class AddressDto
                                       {
                                           public string Street { get; set; }
                                       }

                                       public class Source
                                       {
                                           public Address HomeAddress { get; set; }
                                       }

                                       public class Destination
                                       {
                                           public AddressDto HomeAddress { get; set; }
                                       }

                                       public class MappingProfile : Profile
                                       {
                                           public MappingProfile()
                                           {
                                               CreateMap<Source, Destination>();
                                           }
                                       }
                                   }
                                   """;

        const string secondaryProfile = """

                                        using AutoMapper;

                                        namespace TestNamespace
                                        {
                                            public class AddressProfile : Profile
                                            {
                                                public AddressProfile()
                                                {
                                                    CreateMap<Address, AddressDto>();
                                                }
                                            }
                                        }
                                        """;

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(
            new[] { ("ProfileOne.cs", mainProfile), ("ProfileTwo.cs", secondaryProfile) });
    }

    [Fact]
    public async Task AM001_ShouldNotReportDiagnostic_WhenExplicitConversionProvided()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Age { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Age { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Age, opt => opt.MapFrom(src => int.Parse(src.Age)));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM001_ShouldReportDiagnostic_WhenDateTimeMappedToString()
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

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 21, 13, "CreatedDate",
                "Source", "System.DateTime", "Destination", "string"));
    }

    [Fact]
    public async Task AM001_ShouldHandleComplexTypes()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceAddress
                                    {
                                        public string Street { get; set; }
                                    }

                                    public class DestinationAddress
                                    {
                                        public string Street { get; set; }
                                    }

                                    public class Source
                                    {
                                        public SourceAddress Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public DestinationAddress Address { get; set; }
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

        // When both mappings are configured, no diagnostics should be reported
        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM001_ShouldNotReportDiagnostic_ForCreateMapLikeApiOutsideAutoMapper()
    {
        const string testCode = """
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Age { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Age { get; set; }
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

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM001_ShouldNotReportDiagnostic_WhenExplicitConversionProvidedWithParenthesizedLambda()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Age { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Age { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember((dest) => dest.Age, (opt) => opt.MapFrom((src) => int.Parse(src.Age)));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM001_ShouldReportDiagnostic_WhenNullableSourceTypeIsAlsoTypeIncompatible()
    {
        const string testCode = """
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

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 20, 13, "Age", "Source",
                "string?", "Destination", "int"));
    }
}

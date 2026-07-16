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
    public async Task AM001_ShouldReportDiagnostic_WhenStringMappedToInt_Simple()
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

        // Property-token placement: Destination.Age identifier (not CreateMap invocation).
        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 11, 16, "Age", "Source",
                "string", "Destination", "int"));
    }

    [Fact]
    public async Task AM001_ShouldLandOnDestinationPropertyToken_NotCreateMapInvocation()
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

        // Line 12 col 20 is Destination.Value; CreateMap is later on the profile.
        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 12, 20, "Value", "Source",
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
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 13, 20, "Age", "Source",
                "string", "Destination", "int"));
    }

    [Fact]
    public async Task AM001_ShouldReportDiagnostic_WhenUnsignedMappedToSignedSameWidth()
    {
        // uint -> int is NOT an implicit conversion in C#; AutoMapper would need explicit configuration,
        // so AM001 must report it. The old conversion-level model wrongly treated same-width
        // signed/unsigned pairs as compatible and stayed silent.
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public uint Value { get; set; }
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

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 13, 20, "Value", "Source",
                "uint", "Destination", "int"));
    }

    [Fact]
    public async Task AM001_ShouldReportDiagnostic_WhenDoubleMappedToDecimal()
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

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 13, 24, "Amount", "Source",
                "double", "Destination", "decimal"));
    }

    [Fact]
    public async Task AM001_ShouldNotReportDiagnostic_WhenCharWidenedToInt()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public char Code { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Code { get; set; }
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
    public async Task AM001_ShouldReportReverseMapDiagnostic_WhenOnlyForwardNumericConversionIsImplicit()
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

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 8, 20, "Score",
                "Destination", "long", "Source", "int"));
    }

    [Fact]
    public async Task AM001_ShouldNotReportDiagnostic_WhenUserDefinedImplicitConversionExists()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public readonly struct Money
                                    {
                                        public Money(decimal value)
                                        {
                                            Value = value;
                                        }

                                        public decimal Value { get; }

                                        public static implicit operator decimal(Money money) => money.Value;
                                    }

                                    public class Source
                                    {
                                        public Money Amount { get; set; }
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

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM001_ShouldReportDiagnostic_WhenOnlyUserDefinedExplicitConversionExists()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public readonly struct Money
                                    {
                                        public Money(decimal value)
                                        {
                                            Value = value;
                                        }

                                        public decimal Value { get; }

                                        public static explicit operator decimal(Money money) => money.Value;
                                    }

                                    public class Source
                                    {
                                        public Money Amount { get; set; }
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

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 25, 24, "Amount", "Source",
                "TestNamespace.Money", "Destination", "decimal"));
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
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 18, 20, "Age", "Source",
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
    public async Task AM001_ShouldNotReportDiagnostic_WhenExplicitConversionProvidedWithNameofDestinationMember()
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
                                                .ForMember(nameof(Destination.Age), opt => opt.MapFrom(src => int.Parse(src.Age)));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM001_ShouldNotReportDiagnostic_WhenExplicitConversionProvidedWithStringDestinationMember()
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
                                                .ForMember("Age", opt => opt.MapFrom(src => int.Parse(src.Age)));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM001_ShouldNotReportDiagnostic_WhenExplicitConversionProvidedWithConstantDestinationMember()
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
                                        private const string AgeMember = nameof(Destination.Age);

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(AgeMember, opt => opt.MapFrom(src => int.Parse(src.Age)));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM001_ShouldNotReportDiagnostic_WhenConstructUsingHandlesMismatch()
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
                                                .ConstructUsing(src => new Destination { Age = int.Parse(src.Age) });
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM001_ShouldReportDiagnostic_WhenNonAutoMapperForMemberLooksLikeSuppression()
    {
        const string testCode = """

                                using System;
                                using System.Linq.Expressions;
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public static class MappingExtensions
                                    {
                                        public static IMappingExpression<TSource, TDestination> ForMember<TSource, TDestination, TMember>(
                                            this IMappingExpression<TSource, TDestination> expression,
                                            Expression<Func<TDestination, TMember>> destinationMember,
                                            string marker) => expression;
                                    }

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
                                                .ForMember(dest => dest.Age, "custom");
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 23, 20, "Age", "Source",
                "string", "Destination", "int"));
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
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 14, 23, "CreatedDate",
                "Source", "System.DateTime", "Destination", "string"));
    }

    [Fact]
    public async Task AM001_ShouldReportDiagnostic_WhenDateOnlyMappedToCustomType()
    {
        const string testCode = """

                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public DateOnly Value { get; set; }
                                    }

                                    public class CustomDate
                                    {
                                        public int Year { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public CustomDate Value { get; set; }
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
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 19, 27, "Value",
                "Source", "System.DateOnly", "Destination", "TestNamespace.CustomDate"));
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
    public async Task AM001_ShouldNotReportDiagnostic_WhenExplicitConversionProvidedWithTypedLambdaParameters()
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
                                                .ForMember((Destination dest) => dest.Age,
                                                    opt => opt.MapFrom((Source src) => int.Parse(src.Age)));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM001_ShouldNotReportDiagnostic_WhenExplicitConversionProvidedWithTypedForPath()
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
                                                .ForPath((Destination dest) => dest.Age,
                                                    opt => opt.MapFrom((Source src) => int.Parse(src.Age)));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Theory]
    [InlineData("\"Value\"")]
    [InlineData("nameof(Destination.Value)")]
    [InlineData("ValueParameter")]
    public async Task AM001_ShouldNotReportDiagnostic_WhenRecordConstructorParameterHandlesMismatch(
        string parameterNameExpression)
    {
        string testCode = $$"""
                            using System;
                            using AutoMapper;

                            namespace TestNamespace
                            {
                                public record Source(Guid Value);

                                public record Destination(int Value);

                                public class TestProfile : Profile
                                {
                                    private const string ValueParameter = nameof(Destination.Value);

                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForCtorParam({{parameterNameExpression}},
                                                options => options.MapFrom(source => source.Value.GetHashCode()));
                                    }
                                }
                            }
                            """;

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM001_ShouldReportDiagnostic_WhenRecordConstructorParameterIsNotConfigured()
    {
        const string testCode = """
                                using System;
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record Source(Guid Value);

                                    public record Destination(int Value);

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
            CreateDiagnostic(
                AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule,
                8,
                35,
                "Value",
                "Source",
                "System.Guid",
                "Destination",
                "int"));
    }

    [Fact]
    public async Task AM001_ShouldReportDiagnostic_WhenForCtorParamNamesDifferentParameter()
    {
        const string testCode = """
                                using System;
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record Source(Guid Value);

                                    public record Destination(int Value);

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForCtorParam("Other",
                                                    options => options.MapFrom(source => source.Value.GetHashCode()));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            CreateDiagnostic(
                AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule,
                8,
                35,
                "Value",
                "Source",
                "System.Guid",
                "Destination",
                "int"));
    }

    [Fact]
    public async Task AM001_ShouldReportForwardDiagnostic_WhenForCtorParamAppearsAfterReverseMap()
    {
        const string testCode = """
                                using System;
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record Source(Guid Value);

                                    public record Destination(int Value);

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ReverseMap()
                                                .ForCtorParam(nameof(Source.Value),
                                                    options => options.MapFrom(destination => Guid.Empty));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            CreateDiagnostic(
                AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule,
                8,
                35,
                "Value",
                "Source",
                "System.Guid",
                "Destination",
                "int"));
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
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 13, 20, "Age", "Source",
                "string?", "Destination", "int"));
    }

    [Fact]
    public async Task AM001_ShouldNotReportDiagnostic_WhenMappingClassToInterface()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public interface IDestinationAddress
                                    {
                                        string Street { get; set; }
                                    }

                                    public class SourceAddress
                                    {
                                        public string Street { get; set; }
                                    }

                                    public class Source
                                    {
                                        public SourceAddress Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public IDestinationAddress Address { get; set; }
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

        // AM001 should ignore class -> interface mappings since they are complex mappings (handled by AM020)
        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM001_ShouldNotReportDiagnostic_WhenMappingStructToClass()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public struct SourceAddress
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
                                        }
                                    }
                                }
                                """;

        // AM001 should ignore struct -> class mappings since they are complex mappings (handled by AM020)
        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM001_ShouldReportBothDirections_WhenReverseMapHasBidirectionalTypeMismatch()
    {
        // string↔int needs distinct fixes per direction; direction-preserving mismatch keys
        // must allow both diagnostics.
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
                                                .ReverseMap();
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM001_PropertyTypeMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            // Forward: lands on Destination.Age (int)
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 13, 20, "Age",
                "Source", "string", "Destination", "int"),
            // Reverse: lands on Source.Age (string)
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 8, 23, "Age",
                "Destination", "int", "Source", "string"));
    }

    [Fact]
    public async Task AM001_ShouldReportDiagnostic_WhenNullableDoubleMappedToNullableDecimal()
    {
        // Nullable<T> with different type args is a scalar mismatch, not AM021 collection ownership.
        // double→decimal is explicit-only even after nullable lifting, so AM001 must report.
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public double? Amount { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public decimal? Amount { get; set; }
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
            CreateDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 13, 25, "Amount",
                "Source", "double?", "Destination", "decimal?"));
    }

    [Fact]
    public async Task AM001_ShouldNotReportDiagnostic_WhenNullableIntWidensToNullableLong()
    {
        // int→long is implicit; lifted int?→long? must stay quiet (not suppressed-for-the-wrong-reason).
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int? Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public long? Value { get; set; }
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

}

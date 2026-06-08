using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

public class AM004_MissingDestinationPropertyTests
{
    private static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor, int line, int column,
        params object[] messageArgs)
    {
        return new DiagnosticResult(descriptor).WithLocation(line, column).WithArguments(messageArgs);
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_WhenSourcePropertyMissingInDestination()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public string ImportantData { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        // Missing ImportantData property - data loss!
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

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 9, 23,
                "ImportantData"));
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_OnUnmappedSourcePropertyIdentifier()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string ImportantData { get; set; }
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

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            new DiagnosticResult(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule)
                .WithSpan(8, 23, 8, 36)
                .WithArguments("ImportantData"));
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_WhenUnrelatedDestinationMemberHasCondition()
    {
        // A Condition() configured on a mapped destination member must not suppress the unmapped-source
        // diagnostic for an unrelated source property.
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string ImportantData { get; set; }
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

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 8, 23,
                "ImportantData"));
    }

    [Fact]
    public async Task AM004_ShouldNotReportDiagnostic_WhenAllPropertiesAreMapped()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public string Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public string Data { get; set; }
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

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldNotReportDiagnostic_WhenExplicitIgnoreConfigured()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public string TempData { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForSourceMember(src => src.TempData, opt => opt.DoNotValidate());
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldNotReportDiagnostic_WhenExplicitStringSourceMemberIgnoreConfigured()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public string TempData { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForSourceMember("TempData", opt => opt.DoNotValidate());
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_WhenStringSourceMemberIgnoreUsesDottedPath()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public string TempData { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForSourceMember("TempData.Child", opt => opt.DoNotValidate());
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 9, 23,
                "TempData"));
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_WhenStringSourceMemberIgnoreIsEmpty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                        public string TempData { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForSourceMember("", opt => opt.DoNotValidate());
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 9, 23,
                "TempData"));
    }

    [Fact]
    public async Task AM004_ShouldNotReportDiagnostic_WhenCustomMappingHandlesProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string FirstName { get; set; }
                                        public string LastName { get; set; }
                                        public string MiddleName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string FullName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => 
                                                    $"{src.FirstName} {src.MiddleName} {src.LastName}"));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldReportMultipleMissingProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string MissingProperty1 { get; set; }
                                        public string MissingProperty2 { get; set; }
                                        public int MissingNumber { get; set; }
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

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 8, 23,
                "MissingProperty1"),
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 9, 23,
                "MissingProperty2"),
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 10, 20,
                "MissingNumber"));
    }

    [Fact]
    public async Task AM004_ShouldHandleCaseSensitivePropertyMatching()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string userName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string UserName { get; set; }
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

        // AutoMapper uses case-insensitive matching by default, so userName should map to UserName
        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldIgnoreStaticProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public static string StaticProperty { get; set; }
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

        // Static properties should not be considered for mapping
        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldIgnoreReadOnlyProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string ReadOnlyProp { get; }
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

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 8, 23,
                "ReadOnlyProp"));
    }

    [Fact]
    public async Task AM004_ShouldHandleInheritedProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class BaseSource
                                    {
                                        public string BaseProperty { get; set; }
                                        public string MissingInDest { get; set; }
                                    }

                                    public class Source : BaseSource
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string BaseProperty { get; set; }
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

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 8, 23,
                "MissingInDest"));
    }

    [Fact]
    public async Task AM004_ShouldHandleComplexTypes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Address
                                    {
                                        public string Street { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public Address HomeAddress { get; set; }
                                        public Address WorkAddress { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public Address HomeAddress { get; set; }
                                        // Missing WorkAddress - data loss!
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

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 14, 24, "WorkAddress"));
    }

    [Fact]
    public async Task AM004_ShouldReportMissingCollectionProperty()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public List<string> Tags { get; set; }
                                        public string[] Categories { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public List<string> Tags { get; set; }
                                        // Missing Categories array - data loss!
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

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 10, 25, "Categories"));
    }

    [Fact]
    public async Task AM004_ShouldReportMissingNullableProperty()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public int? Age { get; set; }
                                        public DateTime? BirthDate { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public int? Age { get; set; }
                                        // Missing BirthDate - data loss!
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

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 10, 26, "BirthDate"));
    }

    [Fact]
    public async Task AM004_ShouldHandlePropertyHidingWithNewKeyword()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class BaseSource
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Source : BaseSource
                                    {
                                        public new string Name { get; set; }
                                        public string Email { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
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

        // Property hiding with 'new' keyword should not cause false positives
        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldHandleEnumProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public enum Status
                                    {
                                        Active,
                                        Inactive
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public Status CurrentStatus { get; set; }
                                        public Status PreviousStatus { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public Status CurrentStatus { get; set; }
                                        // Missing PreviousStatus - data loss!
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

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 15, 23,
                "PreviousStatus"));
    }

    [Fact]
    public async Task AM004_ShouldHandleMultipleForSourceMemberIgnores()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string TempData1 { get; set; }
                                        public string TempData2 { get; set; }
                                        public string TempData3 { get; set; }
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
                                                .ForSourceMember(src => src.TempData1, opt => opt.DoNotValidate())
                                                .ForSourceMember(src => src.TempData2, opt => opt.DoNotValidate())
                                                .ForSourceMember(src => src.TempData3, opt => opt.DoNotValidate());
                                        }
                                    }
                                }
                                """;

        // All temp properties are explicitly ignored, so no diagnostics
        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldNotReportDiagnostic_WhenSourcePropertyMappedWithForCtorParam()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string RawName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; }

                                        public Destination(string name)
                                        {
                                            Name = name;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForCtorParam("name", opt => opt.MapFrom(src => src.RawName));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldNotReportDiagnostic_WhenConstructUsingConsumesSourceProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string ExtraData { get; set; }
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
                                                .ConstructUsing(src => new Destination
                                                {
                                                    Name = src.Name + "-" + src.ExtraData
                                                });
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldNotReportDiagnostic_ForCreateMapLikeApiOutsideAutoMapper()
    {
        const string testCode = """
                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string LostData { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class FakeMapExpression<TSource, TDestination>
                                    {
                                    }

                                    public class FakeProfile
                                    {
                                        public FakeMapExpression<TSource, TDestination> CreateMap<TSource, TDestination>() => new();
                                    }

                                    public class TestProfile : FakeProfile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldNotReportDiagnostic_WhenSourcePropertyHandledByParenthesizedForMember()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string FirstName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string FullName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember((dest) => dest.FullName, (opt) => opt.MapFrom((src) => src.FirstName));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldNotTreatNonAutoMapperReverseMapAsBoundary()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public static class MappingExtensions
                                    {
                                        public static IMappingExpression<TSource, TDestination> ReverseMap<TSource, TDestination>(
                                            this IMappingExpression<TSource, TDestination> expression,
                                            int marker) => expression;
                                    }

                                    public class Source
                                    {
                                        public string FirstName { get; set; }
                                        public string LastName { get; set; }
                                        public string MiddleName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string FullName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ReverseMap(1)
                                                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src =>
                                                    src.FirstName + " " + src.LastName + " " + src.MiddleName));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_WhenNestedCustomMappingDoesNotConsumeSameNamedTopLevelProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Address
                                    {
                                        public string Street { get; set; }
                                    }

                                    public class Source
                                    {
                                        public Address Address { get; set; }
                                        public string Street { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string StreetLine { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.StreetLine, opt => opt.MapFrom(src => src.Address.Street));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 13, 23, "Street"));
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_WhenFlatteningPrefixMatchesButNestedMemberDoesNotExist()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Customer
                                    {
                                        public string FirstName { get; set; }
                                    }

                                    public class Source
                                    {
                                        public Customer Customer { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string CustomerAge { get; set; }
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

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 12, 25, "Customer"));
    }
}

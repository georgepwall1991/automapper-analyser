using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

public class AM004_RecordAndModernTypeTests
{
    private static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor, int line, int column,
        params object[] messageArgs)
    {
        return new DiagnosticResult(descriptor).WithLocation(line, column).WithArguments(messageArgs);
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_WhenRecordSourceHasMissingProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record Source(string Name, string Extra);

                                    public record Destination(string Name);

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
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 13, 13,
                "Extra"));
    }

    [Fact]
    public async Task AM004_ShouldNotReportDiagnostic_WhenRecordSourceAndDestinationMatch()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record Source(string Name);

                                    public record Destination(string Name);

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
    public async Task AM004_ShouldReportDiagnostic_WhenSourceHasInitOnlyProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; init; }
                                        public string Extra { get; init; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; init; }
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
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 20, 13,
                "Extra"));
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_WhenRecordStructHasMissingProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record struct Source(string Name, string Extra);

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
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 16, 13,
                "Extra"));
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_WhenClassSourceMissesDestinationRecordProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Data { get; set; }
                                    }

                                    public record Destination(string Name);

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
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 17, 13,
                "Data"));
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_WhenRecordWithInheritedMembersHasMissingProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record BaseSource(string Id);

                                    public record Source(string Name, string Extra) : BaseSource(System.Guid.NewGuid().ToString());

                                    public record Destination(string Id, string Name);

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
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 15, 13,
                "Extra"));
    }

    [Fact]
    public async Task AM004_ShouldHandleRecordWithExplicitIgnore()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record Source(string Name, string TempData);

                                    public record Destination(string Name);

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
    public async Task AM004_ShouldHandleRecordWithCustomMapping()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record Source(string FirstName, string LastName, string MiddleName);

                                    public record Destination(string FullName);

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
    public async Task AM004_ShouldReportMultipleMissingPropertiesInRecord()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record Source(string Name, string Missing1, string Missing2, int MissingNumber);

                                    public record Destination(string Name);

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
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 13, 13,
                "Missing1"),
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 13, 13,
                "Missing2"),
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 13, 13,
                "MissingNumber"));
    }

    [Fact]
    public async Task AM004_ShouldHandleRecordWithCaseSensitiveMatching()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record Source(string Name, string userName);

                                    public record Destination(string Name, string UserName);

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        // AutoMapper uses case-insensitive matching by default
        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldHandleInitOnlyPropertiesWithNoMissing()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; init; }
                                        public string Email { get; init; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; init; }
                                        public string Email { get; init; }
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
    public async Task AM004_ShouldHandleRecordStructWithMatchingProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record struct Source(string Name, string Email);

                                    public record struct Destination(string Name, string Email);

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
    public async Task AM004_ShouldReportMissingPropertyInRecordWithMultipleParameters()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public record Source(
                                        string Id,
                                        string Name,
                                        string Email,
                                        List<string> Tags,
                                        DateTime CreatedAt,
                                        string ExtraField);

                                    public record Destination(
                                        string Id,
                                        string Name,
                                        string Email,
                                        List<string> Tags,
                                        DateTime CreatedAt);

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
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 26, 13,
                "ExtraField"));
    }

    [Fact]
    public async Task AM004_ShouldHandleRecordWithForCtorParamMapping()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record Source(string RawName);

                                    public record Destination(string Name);

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
    public async Task AM004_ShouldHandleComplexRecordWithNestedTypes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record Address(string Street, string City);

                                    public record Source(string Name, Address HomeAddress, Address WorkAddress);

                                    public record Destination(string Name, Address HomeAddress);

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
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 15, 13,
                "WorkAddress"));
    }

    [Fact]
    public async Task AM004_ShouldReportMissingPropertyInRecordWithInitOnlyMembers()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record Source
                                    {
                                        public string Name { get; init; }
                                        public string Email { get; init; }
                                        public string TemporaryField { get; init; }
                                    }

                                    public record Destination
                                    {
                                        public string Name { get; init; }
                                        public string Email { get; init; }
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
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 22, 13,
                "TemporaryField"));
    }

    [Fact]
    public async Task AM004_ShouldHandleRecordInheritanceChain()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record BaseSource(string Id);

                                    public record MiddleSource(string Name) : BaseSource(System.Guid.NewGuid().ToString());

                                    public record Source(string Email, string ExtraData) : MiddleSource("test");

                                    public record Destination(string Id, string Name, string Email);

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
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 17, 13,
                "ExtraData"));
    }

    [Fact]
    public async Task AM004_ShouldNotReportWhenRecordSourcePropertysMappedWithForMember()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record Source(string FirstName, string LastName, string MiddleName);

                                    public record Destination(string FullName);

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src =>
                                                    src.FirstName + " " + src.MiddleName + " " + src.LastName));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }
}

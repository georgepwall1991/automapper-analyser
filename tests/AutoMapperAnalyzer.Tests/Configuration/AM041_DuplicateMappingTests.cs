using System.Threading.Tasks;
using AutoMapperAnalyzer.Analyzers.Configuration;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace AutoMapperAnalyzer.Tests.Configuration;

public class AM041_DuplicateMappingTests
{
    [Fact]
    public async Task Should_ReportDiagnostic_When_DuplicateMappingInSameProfile()
    {
        const string testCode = """
            using AutoMapper;

            public class Source {}
            public class Destination {}

            public class MyProfile : Profile
            {
                public MyProfile()
                {
                    CreateMap<Source, Destination>();
                    CreateMap<Source, Destination>();
                }
            }
            """;

        var expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation(11, 9)
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_DuplicateMappingInDifferentProfiles()
    {
        const string profile1 = """
            using AutoMapper;
            public class Source {}
            public class Destination {}

            public class Profile1 : Profile
            {
                public Profile1()
                {
                    CreateMap<Source, Destination>();
                }
            }
            """;

        const string profile2 = """
            using AutoMapper;

            public class Profile2 : Profile
            {
                public Profile2()
                {
                    CreateMap<Source, Destination>();
                }
            }
            """;

        var expected = new DiagnosticResult(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule)
            .WithLocation("Profile2.cs", 7, 9) // Line 7 in profile2
            .WithArguments("Source", "Destination");

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(
            new[] { ("Profile1.cs", profile1), ("Profile2.cs", profile2) },
            expected);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_MappingsAreUnique()
    {
        const string testCode = """
            using AutoMapper;

            public class Source {}
            public class Destination {}
            public class Other {}

            public class MyProfile : Profile
            {
                public MyProfile()
                {
                    CreateMap<Source, Destination>();
                    CreateMap<Source, Other>();
                    CreateMap<Other, Destination>();
                }
            }
            """;

        await AnalyzerVerifier<AM041_DuplicateMappingAnalyzer>.VerifyAnalyzerAsync(testCode);
    }
}


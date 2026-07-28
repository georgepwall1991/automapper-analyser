using AutoMapperAnalyzer.Analyzers.Configuration;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests.Configuration;

/// <summary>
///     Each <c>MapperConfiguration</c> is an independent container. Registering the same type pair in
///     two of them is not a duplicate registration, and AutoMapper accepts it. Found by scanning
///     AutoMapper.Collection, where this shape produced seventeen false positives.
/// </summary>
public class AM041_IndependentConfigurationTests
{
    [Fact]
    public async Task AM041_ShouldNotReportDiagnostic_WhenRegistrationsAreInSeparateMapperConfigurations()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source { public int Id { get; set; } }
                public class Destination { public int Id { get; set; } }

                public class Tests
                {
                    public void FirstTest()
                    {
                        var config = new MapperConfiguration(cfg => cfg.CreateMap<Source, Destination>());
                    }

                    public void SecondTest()
                    {
                        var config = new MapperConfiguration(cfg => cfg.CreateMap<Source, Destination>());
                    }
                }
            }
            """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM041_DuplicateMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM041_ShouldReportDiagnostic_WhenRegistrationsRepeatInsideOneMapperConfiguration()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source { public int Id { get; set; } }
                public class Destination { public int Id { get; set; } }

                public class Tests
                {
                    public void OneConfiguration()
                    {
                        var config = new MapperConfiguration(cfg =>
                        {
                            cfg.CreateMap<Source, Destination>();
                            cfg.CreateMap<Source, Destination>();
                        });
                    }
                }
            }
            """;

        // Same container, registered twice: AutoMapper rejects this and the diagnostic must survive.
        await DiagnosticTestFramework
            .ForAnalyzer<AM041_DuplicateMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule, 15, 17, "Source", "Destination")
            .RunAsync();
    }

    [Fact]
    public async Task AM041_ShouldReportDiagnostic_WhenRegistrationsRepeatInsideOneProfile()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source { public int Id { get; set; } }
                public class Destination { public int Id { get; set; } }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                        CreateMap<Source, Destination>();
                    }
                }
            }
            """;

        // Profile-scoped duplicates are unaffected by the configuration-root distinction.
        await DiagnosticTestFramework
            .ForAnalyzer<AM041_DuplicateMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM041_DuplicateMappingAnalyzer.DuplicateMappingRule, 13, 13, "Source", "Destination")
            .RunAsync();
    }
}

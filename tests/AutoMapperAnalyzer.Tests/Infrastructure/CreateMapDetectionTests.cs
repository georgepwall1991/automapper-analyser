using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests.Infrastructure;

public class CreateMapDetectionTests
{
    [Fact]
    public async Task DoesNotTrigger_OnNonAutoMapperCreateMap_WithCfgReceiver()
    {
        const string testCode = """
using System;

namespace NotAutoMapper
{
    // Intentionally mimics AutoMapper-like API but is unrelated
    public class Config
    {
        public void CreateMap<TSource, TDest>() { }
    }
}

public class Source { public string Age { get; set; } }
public class Destination { public int Age { get; set; } }

public class Test
{
    public void Configure()
    {
        var cfg = new NotAutoMapper.Config();
        // Should NOT be treated as AutoMapper CreateMap
        cfg.CreateMap<Source, Destination>();
    }
}
""";

        await DiagnosticTestFramework
            .ForAnalyzer<AM001_PropertyTypeMismatchAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task Triggers_OnProfileCreateMap_WithTypeMismatch()
    {
        const string testCode = """
using AutoMapper;

public class Source { public string Age { get; set; } }
public class Destination { public int Age { get; set; } }

public class TestProfile : Profile
{
    public TestProfile()
    {
        // Should be detected as AutoMapper CreateMap
        CreateMap<Source, Destination>();
    }
}
""";

        await DiagnosticTestFramework
            .ForAnalyzer<AM001_PropertyTypeMismatchAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule,
                11, 9,
                "Age", "Source", "string", "Destination", "int")
            .RunAsync();
    }
}

using AutoMapperAnalyzer.Tests.Framework;
using AutoMapperAnalyzer.Analyzers;
using System.Threading.Tasks;
using Xunit;

namespace AutoMapperAnalyzer.Tests
{
    public class AM001_PropertyTypeMismatchTests
    {
        [Fact]
        public async Task Debug_AM001_SimpleTest()
        {
            var testCode = @"
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
}";

            // Expect the analyzer to detect the string -> int type mismatch for Age property
            await DiagnosticTestFramework
                .ForAnalyzer<AM001_PropertyTypeMismatchAnalyzer>()
                .WithSource(testCode)
                .ExpectDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 18, 9, "Age", "Source", "string", "Destination", "int")
                .RunAsync();
        }

        [Fact]
        public async Task AM001_ShouldReportDiagnostic_WhenStringMappedToInt()
        {
            var testCode = @"
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
}";

            await DiagnosticTestFramework
                .ForAnalyzer<AM001_PropertyTypeMismatchAnalyzer>()
                .WithSource(testCode)
                .ExpectDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 20, 13, "Age", "Source", "string", "Destination", "int")
                .RunAsync();
        }

        [Fact]
        public async Task AM001_ShouldNotReportDiagnostic_WhenTypesAreCompatible()
        {
            var testCode = @"
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
}";

            await DiagnosticTestFramework
                .ForAnalyzer<AM001_PropertyTypeMismatchAnalyzer>()
                .WithSource(testCode)
                .RunWithNoDiagnosticsAsync();
        }

        [Fact]
        public async Task AM001_ShouldNotReportDiagnostic_WhenExplicitConversionProvided()
        {
            var testCode = @"
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
}";

            await DiagnosticTestFramework
                .ForAnalyzer<AM001_PropertyTypeMismatchAnalyzer>()
                .WithSource(testCode)
                .RunWithNoDiagnosticsAsync();
        }

        [Fact]
        public async Task AM001_ShouldReportDiagnostic_WhenDateTimeMappedToString()
        {
            var testCode = @"
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
}";

            await DiagnosticTestFramework
                .ForAnalyzer<AM001_PropertyTypeMismatchAnalyzer>()
                .WithSource(testCode)
                .ExpectDiagnostic(AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule, 21, 13, "CreatedDate", "Source", "System.DateTime", "Destination", "string")
                .RunAsync();
        }

        [Fact]
        public async Task AM001_ShouldReportDiagnostic_WhenNullableToNonNullable()
        {
            var testCode = @"
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
}";

            await DiagnosticTestFramework
                .ForAnalyzer<AM001_PropertyTypeMismatchAnalyzer>()
                .WithSource(testCode)
                .ExpectDiagnostic(AM001_PropertyTypeMismatchAnalyzer.NullableCompatibilityRule, 20, 13, "Name", "Source", "string?", "Destination", "string")
                .RunAsync();
        }

        [Fact]
        public async Task AM001_ShouldHandleGenericTypes()
        {
            var testCode = @"
using AutoMapper;
using System.Collections.Generic;

namespace TestNamespace
{
    public class Source
    {
        public List<string> Items { get; set; }
    }

    public class Destination
    {
        public List<int> Items { get; set; }
    }

    public class TestProfile : Profile
    {
        public TestProfile()
        {
            CreateMap<Source, Destination>();
        }
    }
}";

            await DiagnosticTestFramework
                .ForAnalyzer<AM001_PropertyTypeMismatchAnalyzer>()
                .WithSource(testCode)
                .ExpectDiagnostic(AM001_PropertyTypeMismatchAnalyzer.GenericTypeMismatchRule, 21, 13, "Items", "Source", "System.Collections.Generic.List<string>", "Destination", "System.Collections.Generic.List<int>")
                .RunAsync();
        }

        [Fact]
        public async Task AM001_ShouldHandleComplexTypes()
        {
            var testCode = @"
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
}";

            // When both mappings are configured, no diagnostics should be reported
            await DiagnosticTestFramework
                .ForAnalyzer<AM001_PropertyTypeMismatchAnalyzer>()
                .WithSource(testCode)
                .RunWithNoDiagnosticsAsync();
        }

        [Fact]
        public async Task AM001_ShouldReportDiagnostic_WhenComplexTypeMappingMissing()
        {
            var testCode = @"
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
            // Missing: CreateMap<SourceAddress, DestinationAddress>();
        }
    }
}";

            await DiagnosticTestFramework
                .ForAnalyzer<AM001_PropertyTypeMismatchAnalyzer>()
                .WithSource(testCode)
                .ExpectDiagnostic(AM001_PropertyTypeMismatchAnalyzer.ComplexTypeMappingMissingRule, 30, 13, "Address", "Source", "TestNamespace.SourceAddress", "Destination", "TestNamespace.DestinationAddress")
                .RunAsync();
        }
    }
} 
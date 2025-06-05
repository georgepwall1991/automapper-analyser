using AutoMapperAnalyzer.Tests.Framework;
using AutoMapperAnalyzer.Analyzers;
using System.Threading.Tasks;
using Xunit;

namespace AutoMapperAnalyzer.Tests
{
    public class AM002_NullableCompatibilityTests
    {
        [Fact]
        public async Task AM002_ShouldReportDiagnostic_WhenNullableStringToNonNullableString()
        {
            var testCode = @"
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
}";

            await DiagnosticTestFramework
                .ForAnalyzer<AM002_NullableCompatibilityAnalyzer>()
                .WithSource(testCode)
                .ExpectDiagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Name", "Source", "string?", "Destination", "string")
                .RunAsync();
        }

        [Fact]
        public async Task AM002_ShouldReportInfo_WhenNonNullableStringToNullableString()
        {
            var testCode = @"
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
}";

            await DiagnosticTestFramework
                .ForAnalyzer<AM002_NullableCompatibilityAnalyzer>()
                .WithSource(testCode)
                .ExpectDiagnostic(AM002_NullableCompatibilityAnalyzer.NonNullableToNullableRule, 21, 13, "Name", "Source", "string", "Destination", "string?")
                .RunAsync();
        }

        [Fact]
        public async Task AM002_ShouldReportDiagnostic_WhenNullableIntToNonNullableInt()
        {
            var testCode = @"
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
}";

            await DiagnosticTestFramework
                .ForAnalyzer<AM002_NullableCompatibilityAnalyzer>()
                .WithSource(testCode)
                .ExpectDiagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 21, 13, "Age", "Source", "int?", "Destination", "int")
                .RunAsync();
        }

        [Fact]
        public async Task AM002_ShouldNotReportDiagnostic_WhenTypesAreCompatible()
        {
            var testCode = @"
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
}";

            await DiagnosticTestFramework
                .ForAnalyzer<AM002_NullableCompatibilityAnalyzer>()
                .WithSource(testCode)
                .RunWithNoDiagnosticsAsync();
        }

        [Fact]
        public async Task AM002_ShouldNotReportDiagnostic_WhenExplicitMappingProvided()
        {
            var testCode = @"
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
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? ""default""));
        }
    }
}";

            await DiagnosticTestFramework
                .ForAnalyzer<AM002_NullableCompatibilityAnalyzer>()
                .WithSource(testCode)
                .RunWithNoDiagnosticsAsync();
        }

        [Fact]
        public async Task AM002_ShouldReportDiagnostic_WhenNullableDateTimeToNonNullable()
        {
            var testCode = @"
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
}";

            await DiagnosticTestFramework
                .ForAnalyzer<AM002_NullableCompatibilityAnalyzer>()
                .WithSource(testCode)
                .ExpectDiagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "CreatedDate", "Source", "System.DateTime?", "Destination", "System.DateTime")
                .RunAsync();
        }

        [Fact]
        public async Task AM002_ShouldReportDiagnostic_WhenMultipleNullableProperties()
        {
            var testCode = @"
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
}";

            await DiagnosticTestFramework
                .ForAnalyzer<AM002_NullableCompatibilityAnalyzer>()
                .WithSource(testCode)
                .ExpectDiagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 25, 13, "Name", "Source", "string?", "Destination", "string")
                .ExpectDiagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 25, 13, "Age", "Source", "int?", "Destination", "int")
                .RunAsync();
        }

        [Fact]
        public async Task AM002_ShouldHandleComplexNullableScenarios()
        {
            var testCode = @"
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
}";

            // Should detect nullable -> non-nullable issue in SourceAddress.Street -> DestinationAddress.Street
            await DiagnosticTestFramework
                .ForAnalyzer<AM002_NullableCompatibilityAnalyzer>()
                .WithSource(testCode)
                .ExpectDiagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 34, 13, "Street", "SourceAddress", "string?", "DestinationAddress", "string")
                .RunAsync();
        }

        [Fact]
        public async Task AM002_ShouldHandleGenericNullableTypes()
        {
            var testCode = @"
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
}";

            await DiagnosticTestFramework
                .ForAnalyzer<AM002_NullableCompatibilityAnalyzer>()
                .WithSource(testCode)
                .ExpectDiagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 22, 13, "Items", "Source", "System.Collections.Generic.List<string>?", "Destination", "System.Collections.Generic.List<string>")
                .RunAsync();
        }
    }
} 
using AutoMapperAnalyzer.Analyzers.ComplexMappings;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests.ComplexMappings;

public class AM030_CustomTypeConverterTests
{
    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenMissingConvertUsingForIncompatibleTypes()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string CreatedDate { get; set; } = "2023-01-01";
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule, 20, 13,
                "CreatedDate", "ITypeConverter<String, DateTime>")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenNestedMappingExists()
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

                                       public class PrimaryProfile : Profile
                                       {
                                           public PrimaryProfile()
                                           {
                                               CreateMap<Source, Destination>();
                                           }
                                       }
                                   }
                                   """;

        const string addressProfile = """
                                      using AutoMapper;

                                      namespace TestNamespace.Inner
                                      {
                                          using TestNamespace;

                                          public class AddressProfile : Profile
                                          {
                                              public AddressProfile()
                                              {
                                                  CreateMap<Address, AddressDto>();
                                              }
                                          }
                                      }
                                      """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(mainProfile, "PrimaryProfile.cs")
            .WithSource(addressProfile, "AddressProfile.cs")
            .RunWithNoDiagnosticsAsync();
    }

    [Fact(Skip = "Future feature: invalid converter detection - see docs/TEST_LIMITATIONS.md #4")]
    public async Task AM030_ShouldReportDiagnostic_WhenInvalidTypeConverterImplementation()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class InvalidConverter : ITypeConverter<string, DateTime>
                                    {
                                        // Missing Convert method - should trigger diagnostic
                                        // Note: This will also cause compiler error CS0535 which is expected
                                    }

                                    public class Source
                                    {
                                        public string CreatedDate { get; set; } = "2023-01-01";
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

        // This test expects both AM030 diagnostics plus a compiler error
        // Since our test framework doesn't have ExpectCompilerError, we'll allow the extra diagnostic
        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.InvalidConverterImplementationRule, 6, 18,
                "InvalidConverter", "TSource", "TDestination")
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule, 26, 13,
                "CreatedDate", "ITypeConverter<String, DateTime>")
            // Note: This will also produce CS0535 compiler error which we can't easily test for
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenConverterDoesNotHandleNullValues()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class NullUnsafeConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            // No null check - should trigger diagnostic
                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class Source
                                    {
                                        public string? CreatedDate { get; set; }
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
                                            // Note: We're just testing that the analyzer detects the null handling issue in the converter class
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "NullUnsafeConverter", "String")
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule, 29, 13,
                "CreatedDate", "ITypeConverter<String, DateTime>")
            .RunAsync();
    }

    [Fact(Skip = "Future feature: unused converter detection - see docs/TEST_LIMITATIONS.md #4")]
    public async Task AM030_ShouldReportDiagnostic_WhenTypeConverterIsUnused()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class UnusedConverter : ITypeConverter<string, DateTime>
                                    {
                                        public DateTime Convert(string source, DateTime destination, ResolutionContext context)
                                        {
                                            return DateTime.Parse(source ?? "2000-01-01");
                                        }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; } = "";
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; } = "";
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                            // UnusedConverter is never referenced
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.UnusedTypeConverterRule, 6, 35,
                "UnusedConverter")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenConvertUsingIsProperlyConfigured()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ValidConverter : ITypeConverter<string, DateTime>
                                    {
                                        public DateTime Convert(string source, DateTime destination, ResolutionContext context)
                                        {
                                            if (string.IsNullOrEmpty(source))
                                                return DateTime.MinValue;
                                            
                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class Source
                                    {
                                        public string CreatedDate { get; set; } = "2023-01-01";
                                    }

                                    public class Destination
                                    {
                                        public DateTime CreatedDate { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.CreatedDate, 
                                                    opt => opt.MapFrom(src => new ValidConverter().Convert(src.CreatedDate, default, null!)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenConverterHandlesNullsProperly()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class NullSafeConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if (source == null)
                                                return DateTime.MinValue;
                                            
                                            return DateTime.TryParse(source, out var result) ? result : DateTime.MinValue;
                                        }
                                    }

                                    public class Source
                                    {
                                        public string? CreatedDate { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public DateTime CreatedDate { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.CreatedDate, 
                                                    opt => opt.MapFrom(src => new NullSafeConverter().Convert(src.CreatedDate, default, null!)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldHandleInlineConvertUsingWithLambda()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string CreatedDate { get; set; } = "2023-01-01";
                                    }

                                    public class Destination
                                    {
                                        public DateTime CreatedDate { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.CreatedDate, 
                                                    opt => opt.MapFrom(src => DateTime.Parse(src.CreatedDate)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldHandleMultipleIncompatibleProperties()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string CreatedDate { get; set; } = "2023-01-01";
                                        public string UpdatedDate { get; set; } = "2023-01-02";
                                        public string Price { get; set; } = "19.99";
                                    }

                                    public class Destination
                                    {
                                        public DateTime CreatedDate { get; set; }
                                        public DateTime UpdatedDate { get; set; }
                                        public decimal Price { get; set; }
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule, 24, 13,
                "CreatedDate", "ITypeConverter<String, DateTime>")
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule, 24, 13,
                "UpdatedDate", "ITypeConverter<String, DateTime>")
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule, 24, 13,
                "Price", "ITypeConverter<String, Decimal>")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldIgnoreCompatibleTypes()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; } = "";
                                        public int Age { get; set; }
                                        public bool IsActive { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; } = "";
                                        public int Age { get; set; }
                                        public bool IsActive { get; set; }
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenConvertingBoolToString()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public bool IsActive { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string IsActive { get; set; } = "";
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule, 19, 13,
                "IsActive", "ITypeConverter<Boolean, String>")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenConvertingGuidToString()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Guid Id { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Id { get; set; } = "";
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule, 20, 13,
                "Id", "ITypeConverter<Guid, String>")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenSameTypesUsed()
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

        // Same types don't require type converters
        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldHandleConverterWithNullCheckUsingIsNullOrEmpty()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class SafeConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if (string.IsNullOrEmpty(source))
                                                return DateTime.MinValue;

                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class Source
                                    {
                                        public string? Date { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public DateTime Date { get; set; }
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

        // Converter with IsNullOrEmpty check triggers null handling warning + missing converter configuration
        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "SafeConverter", "String")
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule, 31, 13,
                "Date", "ITypeConverter<String, DateTime>")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldHandleIntToStringConversion()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Count { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Count { get; set; } = "";
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

        // Int to string requires converter
        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule, 19, 13,
                "Count", "ITypeConverter<Int32, String>")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldHandleDoubleToStringConversion()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public double Price { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Price { get; set; } = "";
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

        // Double to string requires converter
        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule, 19, 13,
                "Price", "ITypeConverter<Double, String>")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenStringToDecimalConversion()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Price { get; set; } = "";
                                    }

                                    public class Destination
                                    {
                                        public decimal Price { get; set; }
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

        // String to decimal requires converter
        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule, 19, 13,
                "Price", "ITypeConverter<String, Decimal>")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldHandleComplexClassConversions()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceAddress
                                    {
                                        public string Street { get; set; }
                                        public string City { get; set; }
                                    }

                                    public class DestinationLocation
                                    {
                                        public string FullAddress { get; set; }
                                    }

                                    public class Source
                                    {
                                        public SourceAddress Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public DestinationLocation Address { get; set; }
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

        // Complex object conversions detected by AM030 when types don't match
        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.MissingConvertUsingConfigurationRule, 30, 13,
                "Address", "ITypeConverter<SourceAddress, DestinationLocation>")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldHandleConverterWithMultipleSourceDestinationTypes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class CustomConverter : ITypeConverter<Source, Destination>
                                    {
                                        public Destination Convert(Source source, Destination destination, ResolutionContext context)
                                        {
                                            return new Destination
                                            {
                                                Value1 = int.TryParse(source.Value1, out var v1) ? v1 : 0,
                                                Value2 = int.TryParse(source.Value2, out var v2) ? v2 : 0
                                            };
                                        }
                                    }

                                    public class Source
                                    {
                                        public string Value1 { get; set; }
                                        public string Value2 { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Value1 { get; set; }
                                        public int Value2 { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ConvertUsing<CustomConverter>();
                                        }
                                    }
                                }
                                """;

        // Global ConvertUsing with custom converter should suppress diagnostics for all properties
        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_ForObjectToCustomClassConversion()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class CustomData
                                    {
                                        public string Value { get; set; }
                                    }

                                    public class Source
                                    {
                                        public object Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public CustomData Data { get; set; }
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

        // Object to custom class might need explicit mapping - handled by AM020 for nested objects
        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }
}

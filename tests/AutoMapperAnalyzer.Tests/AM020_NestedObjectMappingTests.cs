using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests;

public class AM020_NestedObjectMappingTests
{
    [Fact]
    public async Task AM020_ShouldReportDiagnostic_WhenNestedObjectMappingMissing()
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

                                    public class DestAddress
                                    {
                                        public string Street { get; set; }
                                        public string City { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public SourceAddress Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public DestAddress Address { get; set; }
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
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 33, 13, "Address", "SourceAddress", "DestAddress")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldNotReportDiagnostic_WhenNestedMappingExists()
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

                                    public class DestAddress
                                    {
                                        public string Street { get; set; }
                                        public string City { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public SourceAddress Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public DestAddress Address { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                            CreateMap<SourceAddress, DestAddress>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldNotReportDiagnostic_WhenTypesAreSame()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Address
                                    {
                                        public string Street { get; set; }
                                        public string City { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public Address Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public Address Address { get; set; }
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
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleMultipleNestedObjects()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceAddress
                                    {
                                        public string Street { get; set; }
                                    }

                                    public class DestAddress
                                    {
                                        public string Street { get; set; }
                                    }

                                    public class SourceContact
                                    {
                                        public string Email { get; set; }
                                    }

                                    public class DestContact
                                    {
                                        public string Email { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public SourceAddress Address { get; set; }
                                        public SourceContact Contact { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public DestAddress Address { get; set; }
                                        public DestContact Contact { get; set; }
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
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 43, 13, "Address", "SourceAddress", "DestAddress")
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 43, 13, "Contact", "SourceContact", "DestContact")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldNotReportDiagnostic_WhenExplicitlyMapped()
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

                                    public class DestAddress
                                    {
                                        public string Street { get; set; }
                                        public string City { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public SourceAddress Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public DestAddress Address { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Address, opt => opt.MapFrom(src => new DestAddress { Street = src.Address.Street, City = src.Address.City }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldIgnoreValueTypes()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public int Age { get; set; }
                                        public DateTime BirthDate { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public int Age { get; set; }
                                        public DateTime BirthDate { get; set; }
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
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldIgnoreStringProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Description { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Description { get; set; }
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
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleNullableNestedObjects()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceAddress
                                    {
                                        public string Street { get; set; }
                                    }

                                    public class DestAddress
                                    {
                                        public string Street { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public SourceAddress? Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public DestAddress? Address { get; set; }
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
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 31, 13, "Address", "SourceAddress", "DestAddress")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleInheritedProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceAddress
                                    {
                                        public string Street { get; set; }
                                    }

                                    public class DestAddress
                                    {
                                        public string Street { get; set; }
                                    }

                                    public class BaseSource
                                    {
                                        public SourceAddress Address { get; set; }
                                    }

                                    public class Source : BaseSource
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class BaseDest
                                    {
                                        public DestAddress Address { get; set; }
                                    }

                                    public class Destination : BaseDest
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 39, 13, "Address", "SourceAddress", "DestAddress")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldIgnoreCollections()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class SourceItem
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class DestItem
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public List<SourceItem> Items { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public List<DestItem> Items { get; set; }
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

        // Collections should be handled by AM021, not AM020
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }
} 
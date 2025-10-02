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
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 33, 13, "Address",
                "SourceAddress", "DestAddress")
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
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 43, 13, "Address",
                "SourceAddress", "DestAddress")
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 43, 13, "Contact",
                "SourceContact", "DestContact")
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
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 31, 13, "Address",
                "SourceAddress", "DestAddress")
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
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 39, 13, "Address",
                "SourceAddress", "DestAddress")
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

    [Fact]
    public async Task AM020_ShouldIgnoreGuidAndDecimalProperties()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public Guid Id { get; set; }
                                        public decimal Price { get; set; }
                                        public TimeSpan Duration { get; set; }
                                        public DateTimeOffset Timestamp { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public Guid Id { get; set; }
                                        public decimal Price { get; set; }
                                        public TimeSpan Duration { get; set; }
                                        public DateTimeOffset Timestamp { get; set; }
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
    public async Task AM020_ShouldIgnorePropertiesWithoutGetterOrSetter()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class NestedType
                                    {
                                        public string Value { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public NestedType ReadOnly { get; } = new NestedType();
                                        private NestedType _writeOnly;
                                        public NestedType WriteOnly { set => _writeOnly = value; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public NestedType ReadOnly { get; } = new NestedType();
                                        private NestedType _writeOnly;
                                        public NestedType WriteOnly { set => _writeOnly = value; }
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
    public async Task AM020_ShouldHandleStructTypes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public struct SourceStruct
                                    {
                                        public string Value { get; set; }
                                    }

                                    public struct DestStruct
                                    {
                                        public string Value { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public SourceStruct Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public DestStruct Data { get; set; }
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

        // Structs are value types, so they don't require nested object mapping
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleEnumProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public enum SourceStatus { Active, Inactive }
                                    public enum DestStatus { Active, Inactive }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public SourceStatus Status { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public DestStatus Status { get; set; }
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

        // Enums are value types, so they don't require nested object mapping
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleDeeplyNestedObjects()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceCity
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class DestCity
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class SourceAddress
                                    {
                                        public string Street { get; set; }
                                        public SourceCity City { get; set; }
                                    }

                                    public class DestAddress
                                    {
                                        public string Street { get; set; }
                                        public DestCity City { get; set; }
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

        // Should report Address mapping missing (it will also detect City later when analyzing SourceAddress -> DestAddress)
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 43, 13, "Address",
                "SourceAddress", "DestAddress")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleAbstractClassNestedObjects()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public abstract class SourceBase
                                    {
                                        public string Name { get; set; }
                                    }

                                    public abstract class DestBase
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Title { get; set; }
                                        public SourceBase BaseProperty { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Title { get; set; }
                                        public DestBase BaseProperty { get; set; }
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

        // Abstract classes still require mapping configuration
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 31, 13, "BaseProperty",
                "SourceBase", "DestBase")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleMultipleNestedObjectsWithSomeMapped()
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
                                        public string Phone { get; set; }
                                    }

                                    public class DestContact
                                    {
                                        public string Phone { get; set; }
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
                                            CreateMap<SourceAddress, DestAddress>();
                                            // Missing CreateMap<SourceContact, DestContact>()
                                        }
                                    }
                                }
                                """;

        // Should only report Contact mapping missing (Address is already mapped)
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 43, 13, "Contact",
                "SourceContact", "DestContact")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldIgnoreInterfaceProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public interface ISourceService
                                    {
                                        string Name { get; set; }
                                    }

                                    public interface IDestService
                                    {
                                        string Name { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Title { get; set; }
                                        public ISourceService Service { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Title { get; set; }
                                        public IDestService Service { get; set; }
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

        // Interface properties don't require nested object mapping (not classes)
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleNestedObjectWithExplicitForMemberConfiguration()
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
                                                .ForMember(dest => dest.Address, opt => opt.MapFrom(src => new DestAddress { Street = src.Address.Street }));
                                        }
                                    }
                                }
                                """;

        // Explicitly configured ForMember should suppress the diagnostic
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleSameTypeNestedObjects()
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
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public Address HomeAddress { get; set; }
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

        // Same type nested objects don't require mapping
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldDetectNestedObjectWhenMappingExistsInProfile()
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
                                            CreateMap<SourceAddress, DestAddress>();
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        // Mapping already exists before the main CreateMap, so no diagnostic
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleNullableValueTypeNestedProperty()
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
                                        public DateTime? BirthDate { get; set; }
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

        // Nullable value types are built-in types, no nested object mapping needed
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleCollectionOfComplexTypes()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

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
                                        public List<SourceAddress> Addresses { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public List<DestAddress> Addresses { get; set; }
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

        // Collections are handled by AM021, not AM020
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleStringProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Description { get; set; }
                                        public string Email { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Description { get; set; }
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

        // Strings are built-in types, no nested object mapping needed
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandlePropertiesWithInitOnlySetters()
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
                                        public SourceAddress Address { get; init; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public DestAddress Address { get; init; }
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

        // Properties with init-only setters still need mapping configuration
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 31, 13, "Address",
                "SourceAddress", "DestAddress")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleReadOnlyProperties()
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
                                        public SourceAddress Address { get; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public DestAddress Address { get; }
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

        // Read-only properties (no setter) can't be mapped by AutoMapper
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleStaticProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceConfig
                                    {
                                        public string Value { get; set; }
                                    }

                                    public class DestConfig
                                    {
                                        public string Value { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public static SourceConfig Config { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public static DestConfig Config { get; set; }
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

        // Static properties are not mapped by AutoMapper, so no diagnostic expected
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleObjectTypeProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public object Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public object Data { get; set; }
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

        // Object type properties with same type don't need mapping
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleDateTimeOffsetProperties()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public DateTimeOffset CreatedAt { get; set; }
                                        public TimeSpan Duration { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public DateTimeOffset CreatedAt { get; set; }
                                        public TimeSpan Duration { get; set; }
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

        // DateTimeOffset and TimeSpan are built-in types, no nested object mapping needed
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleNestedObjectInBaseClassOnly()
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

        // Nested object in base class should be detected
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 39, 13, "Address",
                "SourceAddress", "DestAddress")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleValueTupleProperties()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public (int X, int Y) Coordinates { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public (int X, int Y) Coordinates { get; set; }
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

        // Value tuples are value types, not classes requiring nested mapping
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }
}

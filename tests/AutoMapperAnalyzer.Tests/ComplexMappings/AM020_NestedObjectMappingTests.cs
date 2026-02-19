using AutoMapperAnalyzer.Analyzers.ComplexMappings;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests.ComplexMappings;

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
    public async Task AM020_ShouldRespectMappingsDefinedInSeparateProfiles()
    {
        const string mainProfile = """
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
                                           public SourceAddress Address { get; set; }
                                       }

                                       public class Destination
                                       {
                                           public DestAddress Address { get; set; }
                                       }

                                       public class ParentProfile : Profile
                                       {
                                           public ParentProfile()
                                           {
                                               CreateMap<Source, Destination>();
                                           }
                                       }
                                   }
                                   """;

        const string secondaryProfile = """
                                        using AutoMapper;

                                        namespace TestNamespace.Inner
                                        {
                                            using TestNamespace;

                                            public class AddressProfile : Profile
                                            {
                                                public AddressProfile()
                                                {
                                                    CreateMap<SourceAddress, DestAddress>();
                                                }
                                            }
                                        }
                                        """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(mainProfile, "MainProfile.cs")
            .WithSource(secondaryProfile, "AddressProfile.cs")
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
    public async Task AM020_ShouldReportDiagnostic_ForStructTypes()
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

        // Structs now require mapping per AM001/AM020 unification
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 31, 13, "Data",
                "SourceStruct", "DestStruct")
            .RunAsync();
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
    public async Task AM020_ShouldReportDiagnostic_ForInterfaceProperties()
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

        // Interface properties now require mapping per AM001/AM020 unification
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 31, 13, "Service",
                "ISourceService", "IDestService")
            .RunAsync();
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

    [Fact]
    public async Task AM020_ShouldReportDiagnostic_ForDefaultInterfaceImplementationProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public interface ISourceData
                                    {
                                        string Value { get; set; }
                                    }

                                    public interface IDestData
                                    {
                                        string Value { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public ISourceData Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public IDestData Data { get; set; }
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

        // Interface properties now require mapping per AM001/AM020 unification
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 31, 13, "Data",
                "ISourceData", "IDestData")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleMultipleLevelInheritance()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceGrandParent
                                    {
                                        public string GrandParentProp { get; set; }
                                    }

                                    public class SourceParent : SourceGrandParent
                                    {
                                        public string ParentProp { get; set; }
                                    }

                                    public class SourceChild : SourceParent
                                    {
                                        public string ChildProp { get; set; }
                                    }

                                    public class DestGrandParent
                                    {
                                        public string GrandParentProp { get; set; }
                                    }

                                    public class DestParent : DestGrandParent
                                    {
                                        public string ParentProp { get; set; }
                                    }

                                    public class DestChild : DestParent
                                    {
                                        public string ChildProp { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public SourceChild NestedObject { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public DestChild NestedObject { get; set; }
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

        // Should report diagnostic for nested object mapping missing
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 51, 13,
                "NestedObject", "SourceChild", "DestChild")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleExplicitInterfaceImplementation()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public interface IHasData
                                    {
                                        string Data { get; set; }
                                    }

                                    public class SourceData
                                    {
                                        public string Value { get; set; }
                                    }

                                    public class DestData
                                    {
                                        public string Value { get; set; }
                                    }

                                    public class Source : IHasData
                                    {
                                        string IHasData.Data { get; set; }
                                        public SourceData NestedData { get; set; }
                                    }

                                    public class Destination : IHasData
                                    {
                                        string IHasData.Data { get; set; }
                                        public DestData NestedData { get; set; }
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

        // Should report diagnostic for public nested object
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 36, 13,
                "NestedData", "SourceData", "DestData")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleEmptyClassesWithNoProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class EmptySource
                                    {
                                    }

                                    public class EmptyDest
                                    {
                                    }

                                    public class Source
                                    {
                                        public EmptySource Empty { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public EmptyDest Empty { get; set; }
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

        // Should report diagnostic even for empty classes
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 27, 13,
                "Empty", "EmptySource", "EmptyDest")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleGenericNestedObjects()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceWrapper<T>
                                    {
                                        public T Value { get; set; }
                                    }

                                    public class DestWrapper<T>
                                    {
                                        public T Value { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public SourceWrapper<int> Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public DestWrapper<int> Data { get; set; }
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

        // Should report diagnostic for generic nested objects (without type arguments in diagnostic message)
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 31, 13,
                "Data", "SourceWrapper", "DestWrapper")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldNotReportDiagnostic_WhenPropertyIsIgnored()
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
                                                .ForMember(dest => dest.Address, opt => opt.Ignore());
                                        }
                                    }
                                }
                                """;

        // ForMember with Ignore() should suppress the diagnostic
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldNotReportDiagnostic_WhenUsingMapFromWithCustomLogic()
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
                                                .ForMember(dest => dest.Address, opt => opt.MapFrom((src, dest, destMember, context) =>
                                                    new DestAddress { Street = src.Address?.Street }));
                                        }
                                    }
                                }
                                """;

        // MapFrom with custom logic should suppress the diagnostic
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldReportDiagnostic_WhenUsingReverseMapWithoutNestedMapping()
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
                                                .ReverseMap();
                                        }
                                    }
                                }
                                """;

        // ReverseMap alone doesn't create nested object mapping
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 31, 13,
                "Address", "SourceAddress", "DestAddress")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldNotReportDiagnostic_WhenConstructUsingHandlesForwardMapping()
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
                                        public SourceAddress Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public DestAddress Address { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ConstructUsing(src => new Destination
                                                {
                                                    Address = new DestAddress { Street = src.Address.Street }
                                                });
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
    public async Task AM020_ShouldReportDiagnostic_WhenOnlyReverseDirectionForMemberExists()
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
                                        public SourceAddress Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public DestAddress Address { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ReverseMap()
                                                .ForMember(dest => dest.Address, opt => opt.Ignore());
                                        }
                                    }
                                }
                                """;

        // ForMember after ReverseMap applies to Destination -> Source only.
        // Source -> Destination still needs nested mapping.
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 29, 13,
                "Address", "SourceAddress", "DestAddress")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldNotReportDiagnostic_WhenConvertUsingHandlesForwardMapping()
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
                                        public SourceAddress Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public DestAddress Address { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ConvertUsing(src => new Destination
                                                {
                                                    Address = new DestAddress { Street = src.Address.Street }
                                                });
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
    public async Task AM020_ShouldNotReportDiagnostic_WhenForPathConfiguresNestedProperty()
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
                                        public SourceAddress Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public DestAddress Address { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForPath(dest => dest.Address.Street, opt => opt.MapFrom(src => src.Address.Street));
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
    public async Task AM020_ShouldNotReportDiagnostic_WhenForMemberUsesStringPropertyName()
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
                                        public SourceAddress Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public DestAddress Address { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember("Address", opt => opt.Ignore());
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
    public async Task AM020_ShouldReportDiagnostic_WhenConstructUsingExistsOnlyAfterReverseMap()
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
                                        public SourceAddress Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public DestAddress Address { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ReverseMap()
                                                .ConstructUsing(dest => new Source
                                                {
                                                    Address = new SourceAddress { Street = dest.Address.Street }
                                                });
                                        }
                                    }
                                }
                                """;

        // ConstructUsing after ReverseMap applies only to Destination -> Source.
        // Source -> Destination still needs nested mapping.
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 29, 13,
                "Address", "SourceAddress", "DestAddress")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldReportDiagnostic_WhenMappingExistsOnlyInDifferentProfile()
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

                                    public class AddressProfile : Profile
                                    {
                                        public AddressProfile()
                                        {
                                            CreateMap<SourceAddress, DestAddress>();
                                        }
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

        // Analyzer should detect mappings across different profiles in the same file
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldIgnoreArrayProperties()
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
                                        public SourceAddress[] Addresses { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public DestAddress[] Addresses { get; set; }
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

        // Arrays should be handled by AM021, not AM020
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldIgnoreDictionaryProperties()
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
                                        public Dictionary<string, SourceItem> ItemsById { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public Dictionary<string, DestItem> ItemsById { get; set; }
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

        // Dictionary properties should be ignored like collections
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleRecordTypes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public record SourceAddress(string Street, string City);
                                    public record DestAddress(string Street, string City);

                                    public record Source(string Name, SourceAddress Address);
                                    public record Destination(string Name, DestAddress Address);

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        // Record types should report diagnostic like classes
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 15, 13,
                "Address", "SourceAddress", "DestAddress")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleRequiredProperties()
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
                                        public required string Name { get; set; }
                                        public required SourceAddress Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public required string Name { get; set; }
                                        public required DestAddress Address { get; set; }
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

        // Required properties should still report diagnostic for nested objects
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 31, 13,
                "Address", "SourceAddress", "DestAddress")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandlePropertyShadowing()
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
                                        public new SourceAddress Address { get; set; }
                                    }

                                    public class BaseDest
                                    {
                                        public DestAddress Address { get; set; }
                                    }

                                    public class Destination : BaseDest
                                    {
                                        public new DestAddress Address { get; set; }
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

        // Should detect shadowed property
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 39, 13,
                "Address", "SourceAddress", "DestAddress")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleInternalProperties()
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
                                        internal SourceAddress Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        internal DestAddress Address { get; set; }
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

        // Internal properties can be mapped by AutoMapper and should be analyzed
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 31, 13,
                "Address", "SourceAddress", "DestAddress")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleMixedPublicAndInternalProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class PublicAddress
                                    {
                                        public string Street { get; set; }
                                    }

                                    public class InternalAddress
                                    {
                                        public string City { get; set; }
                                    }

                                    public class PublicDestAddress
                                    {
                                        public string Street { get; set; }
                                    }

                                    public class InternalDestAddress
                                    {
                                        public string City { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public PublicAddress PublicAddr { get; set; }
                                        internal InternalAddress InternalAddr { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public PublicDestAddress PublicAddr { get; set; }
                                        internal InternalDestAddress InternalAddr { get; set; }
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

        // Both public and internal properties should be analyzed and report diagnostics
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 43, 13,
                "InternalAddr", "InternalAddress", "InternalDestAddress")
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 43, 13,
                "PublicAddr", "PublicAddress", "PublicDestAddress")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldIgnoreProtectedProperties()
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
                                        protected SourceAddress Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        protected DestAddress Address { get; set; }
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

        // Protected properties are not mapped by default
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandleCrossNamespaceNestedObjects()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public SourceNamespace.SourceAddress Address { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public DestNamespace.DestAddress Address { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }

                                namespace SourceNamespace
                                {
                                    public class SourceAddress
                                    {
                                        public string Street { get; set; }
                                    }
                                }

                                namespace DestNamespace
                                {
                                    public class DestAddress
                                    {
                                        public string Street { get; set; }
                                    }
                                }
                                """;

        // Should detect nested objects even in different namespaces
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 21, 13,
                "Address", "SourceAddress", "DestAddress")
            .RunAsync();
    }

    [Fact]
    public async Task AM020_ShouldHandlePartialClasses()
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

                                    public partial class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public partial class Source
                                    {
                                        public SourceAddress Address { get; set; }
                                    }

                                    public partial class Destination
                                    {
                                        public string Name { get; set; }
                                    }

                                    public partial class Destination
                                    {
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

        // Should detect nested objects in partial classes
        await DiagnosticTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule, 39, 13,
                "Address", "SourceAddress", "DestAddress")
            .RunAsync();
    }
}

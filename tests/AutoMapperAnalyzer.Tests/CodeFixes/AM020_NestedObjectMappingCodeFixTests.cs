using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.CodeFixes;

public class AM020_NestedObjectMappingCodeFixTests
{
    [Fact]
    public async Task AM020_CodeFix_ShouldAddMissingCreateMapForNestedObject()
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

                public class AddressDto
                {
                    public string Street { get; set; }
                    public string City { get; set; }
                }

                public class Source
                {
                    public Address HomeAddress { get; set; }
                }

                public class Destination
                {
                    public AddressDto HomeAddress { get; set; }
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

        const string expectedFixedCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Address
                {
                    public string Street { get; set; }
                    public string City { get; set; }
                }

                public class AddressDto
                {
                    public string Street { get; set; }
                    public string City { get; set; }
                }

                public class Source
                {
                    public Address HomeAddress { get; set; }
                }

                public class Destination
                {
                    public AddressDto HomeAddress { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                        CreateMap<Address, AddressDto>();
                    }
                }
            }
            """;

        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                    .WithLocation(31, 13)
                    .WithArguments("HomeAddress", "Address", "AddressDto"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldAddMultipleCreateMapForMultipleNestedObjects()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Address
                {
                    public string Street { get; set; }
                }

                public class Contact
                {
                    public string Email { get; set; }
                }

                public class AddressDto
                {
                    public string Street { get; set; }
                }

                public class ContactDto
                {
                    public string Email { get; set; }
                }

                public class Source
                {
                    public Address HomeAddress { get; set; }
                    public Contact PrimaryContact { get; set; }
                }

                public class Destination
                {
                    public AddressDto HomeAddress { get; set; }
                    public ContactDto PrimaryContact { get; set; }
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

        const string expectedFixedCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Address
                {
                    public string Street { get; set; }
                }

                public class Contact
                {
                    public string Email { get; set; }
                }

                public class AddressDto
                {
                    public string Street { get; set; }
                }

                public class ContactDto
                {
                    public string Email { get; set; }
                }

                public class Source
                {
                    public Address HomeAddress { get; set; }
                    public Contact PrimaryContact { get; set; }
                }

                public class Destination
                {
                    public AddressDto HomeAddress { get; set; }
                    public ContactDto PrimaryContact { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                        CreateMap<Address, AddressDto>();
                        CreateMap<Contact, ContactDto>();
                    }
                }
            }
            """;

        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixWithIterationsAsync(
                testCode,
                new [] {
                    new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                        .WithLocation(41, 13)
                        .WithArguments("PrimaryContact", "Contact", "ContactDto"),
                    new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                        .WithLocation(41, 13)
                        .WithArguments("HomeAddress", "Address", "AddressDto")
                },
                expectedFixedCode,
                1);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldNotDuplicateExistingCreateMap()
    {
        const string testCode = """
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

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                        CreateMap<Address, AddressDto>(); // Already exists
                    }
                }
            }
            """;

        // Should not suggest any fixes since the mapping already exists
        await AnalyzerVerifier<AM020_NestedObjectMappingAnalyzer>
            .VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldHandleNullableNestedObjects()
    {
        const string testCode = """
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
                    public Address? HomeAddress { get; set; }
                }

                public class Destination
                {
                    public AddressDto? HomeAddress { get; set; }
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

        const string expectedFixedCode = """
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
                    public Address? HomeAddress { get; set; }
                }

                public class Destination
                {
                    public AddressDto? HomeAddress { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                        CreateMap<Address, AddressDto>();
                    }
                }
            }
            """;

        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                    .WithLocation(29, 13)
                    .WithArguments("HomeAddress", "Address", "AddressDto"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldNotCreateMappingForBuiltInTypes()
    {
        const string testCode = """
            using AutoMapper;
            using System;

            namespace TestNamespace
            {
                public class Source
                {
                    public DateTime CreatedAt { get; set; }
                    public decimal Amount { get; set; }
                    public Guid Id { get; set; }
                }

                public class Destination
                {
                    public DateTime CreatedAt { get; set; }
                    public decimal Amount { get; set; }
                    public Guid Id { get; set; }
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

        await AnalyzerVerifier<AM020_NestedObjectMappingAnalyzer>
            .VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldHandleDeepNestedObjectHierarchy()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Country
                {
                    public string Name { get; set; }
                }

                public class City
                {
                    public string Name { get; set; }
                    public Country Country { get; set; }
                }

                public class Address
                {
                    public string Street { get; set; }
                    public City City { get; set; }
                }

                public class CountryDto
                {
                    public string Name { get; set; }
                }

                public class CityDto
                {
                    public string Name { get; set; }
                    public CountryDto Country { get; set; }
                }

                public class AddressDto
                {
                    public string Street { get; set; }
                    public CityDto City { get; set; }
                }

                public class Source
                {
                    public Address HomeAddress { get; set; }
                }

                public class Destination
                {
                    public AddressDto HomeAddress { get; set; }
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

        const string expectedFixedCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Country
                {
                    public string Name { get; set; }
                }

                public class City
                {
                    public string Name { get; set; }
                    public Country Country { get; set; }
                }

                public class Address
                {
                    public string Street { get; set; }
                    public City City { get; set; }
                }

                public class CountryDto
                {
                    public string Name { get; set; }
                }

                public class CityDto
                {
                    public string Name { get; set; }
                    public CountryDto Country { get; set; }
                }

                public class AddressDto
                {
                    public string Street { get; set; }
                    public CityDto City { get; set; }
                }

                public class Source
                {
                    public Address HomeAddress { get; set; }
                }

                public class Destination
                {
                    public AddressDto HomeAddress { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                        CreateMap<Address, AddressDto>();
                        CreateMap<City, CityDto>();
                        CreateMap<Country, CountryDto>();
                    }
                }
            }
            """;

        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixWithIterationsAsync(
                testCode,
                new [] {
                    new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                        .WithLocation(53, 13)
                        .WithArguments("HomeAddress", "Address", "AddressDto")
                },
                expectedFixedCode,
                3);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldNotCreateMappingForCollectionProperties()
    {
        const string testCode = """
            using AutoMapper;
            using System.Collections.Generic;

            namespace TestNamespace
            {
                public class Item
                {
                    public string Name { get; set; }
                }

                public class ItemDto
                {
                    public string Name { get; set; }
                }

                public class Source
                {
                    public List<Item> Items { get; set; }
                }

                public class Destination
                {
                    public List<ItemDto> Items { get; set; }
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
        await AnalyzerVerifier<AM020_NestedObjectMappingAnalyzer>
            .VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldHandleInterfaceAndAbstractTypes()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public interface IAddress
                {
                    string Street { get; set; }
                }

                public class Address : IAddress
                {
                    public string Street { get; set; }
                }

                public class AddressDto
                {
                    public string Street { get; set; }
                }

                public class Source
                {
                    public IAddress HomeAddress { get; set; }
                }

                public class Destination
                {
                    public AddressDto HomeAddress { get; set; }
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

        // Interfaces cannot be automatically mapped; analyzer should not emit AM020
        await AnalyzerVerifier<AM020_NestedObjectMappingAnalyzer>
            .VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldHandleRecordTypes()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public record Address(string Street, string City);
                public record AddressDto(string Street, string City);

                public class Source
                {
                    public Address HomeAddress { get; set; }
                }

                public class Destination
                {
                    public AddressDto HomeAddress { get; set; }
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

        const string expectedFixedCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public record Address(string Street, string City);
                public record AddressDto(string Street, string City);

                public class Source
                {
                    public Address HomeAddress { get; set; }
                }

                public class Destination
                {
                    public AddressDto HomeAddress { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                        CreateMap<Address, AddressDto>();
                    }
                }
            }
            """;

        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                    .WithLocation(22, 13)
                    .WithArguments("HomeAddress", "Address", "AddressDto"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldHandlePrivateSetterProperties()
    {
        const string testCode = """
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
                    public Address HomeAddress { get; private set; }
                }

                public class Destination
                {
                    public AddressDto HomeAddress { get; private set; }
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

        const string expectedFixedCode = """
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
                    public Address HomeAddress { get; private set; }
                }

                public class Destination
                {
                    public AddressDto HomeAddress { get; private set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                        CreateMap<Address, AddressDto>();
                    }
                }
            }
            """;

        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                    .WithLocation(29, 13)
                    .WithArguments("HomeAddress", "Address", "AddressDto"),
                expectedFixedCode);
    }
}

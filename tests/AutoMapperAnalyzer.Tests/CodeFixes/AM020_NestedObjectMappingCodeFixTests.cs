using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Framework;

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

        await CodeFixTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithCodeFix<AM020_NestedObjectMappingCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule,
                43,
                29,
                "HomeAddress",
                "Address",
                "AddressDto"
            )
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
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

        await CodeFixTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithCodeFix<AM020_NestedObjectMappingCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostics(
                (
                    AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule,
                    141,
                    29,
                    new object[] { "HomeAddress", "Address", "AddressDto" }
                ),
                (
                    AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule,
                    141,
                    29,
                    new object[] { "PrimaryContact", "Contact", "ContactDto" }
                )
            )
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
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
        await CodeFixTestFramework
            .ForAnalyzer<AM020_NestedObjectMappingAnalyzer>()
            .WithCodeFix<AM020_NestedObjectMappingCodeFixProvider>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }
}

using AutoMapper;
using AutoMapperAnalyzer.Analyzers.ComplexMappings;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Tests.ComplexMappings;

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
    public async Task AM020_CodeFix_ShouldAddMissingCreateMapForInternalNestedObject()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    internal class Address
                                    {
                                        public string Street { get; set; }
                                    }

                                    internal class AddressDto
                                    {
                                        public string Street { get; set; }
                                    }

                                    public class Source
                                    {
                                        internal Address HomeAddress { get; set; }
                                    }

                                    public class Destination
                                    {
                                        internal AddressDto HomeAddress { get; set; }
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
                                             internal class Address
                                             {
                                                 public string Street { get; set; }
                                             }

                                             internal class AddressDto
                                             {
                                                 public string Street { get; set; }
                                             }

                                             public class Source
                                             {
                                                 internal Address HomeAddress { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 internal AddressDto HomeAddress { get; set; }
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
    public async Task AM020_CodeFix_ShouldAddMissingCreateMapForCustomGuidNamedNestedObject()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Guid
                                    {
                                        public string Value { get; set; }
                                    }

                                    public class GuidDto
                                    {
                                        public string Value { get; set; }
                                    }

                                    public class Source
                                    {
                                        public Guid Identifier { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public GuidDto Identifier { get; set; }
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
                                             public class Guid
                                             {
                                                 public string Value { get; set; }
                                             }

                                             public class GuidDto
                                             {
                                                 public string Value { get; set; }
                                             }

                                             public class Source
                                             {
                                                 public Guid Identifier { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public GuidDto Identifier { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>();
                                                     CreateMap<Guid, GuidDto>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                    .WithLocation(29, 13)
                    .WithArguments("Identifier", "Guid", "GuidDto"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldPreserveGenericTypeArguments_ForGenericNestedObject()
    {
        // The old fixer emitted only type.Name, dropping <int> and producing CreateMap<SourceWrapper,
        // DestWrapper>() which does not compile. The generated mapping must keep the generic arguments.
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
                                        public SourceWrapper<int> Data { get; set; }
                                    }

                                    public class Destination
                                    {
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

        const string expectedFixedCode = """
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
                                                 public SourceWrapper<int> Data { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public DestWrapper<int> Data { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>();
                                                     CreateMap<SourceWrapper<int>, DestWrapper<int>>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                    .WithLocation(29, 13)
                    .WithArguments("Data", "SourceWrapper<int>", "DestWrapper<int>"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldQualifyTypeNames_ForCrossNamespaceNestedObject()
    {
        // The old fixer emitted the simple type name, so CreateMap<Address, AddressDto>() failed to
        // compile when the nested types live in a namespace not imported at the insertion point. The
        // generated mapping must qualify the names.
        const string testCode = """
                                using AutoMapper;

                                namespace Models
                                {
                                    public class Address
                                    {
                                        public string Street { get; set; }
                                    }

                                    public class AddressDto
                                    {
                                        public string Street { get; set; }
                                    }
                                }

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Models.Address Home { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Models.AddressDto Home { get; set; }
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

                                         namespace Models
                                         {
                                             public class Address
                                             {
                                                 public string Street { get; set; }
                                             }

                                             public class AddressDto
                                             {
                                                 public string Street { get; set; }
                                             }
                                         }

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public Models.Address Home { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public Models.AddressDto Home { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>();
                                                     CreateMap<Models.Address, Models.AddressDto>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                    .WithLocation(32, 13)
                    .WithArguments("Home", "Address", "AddressDto"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldUseDescriptiveActionTitleAndEquivalenceKey()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Address {}
                                public class AddressDto {}
                                public class Source { public Address HomeAddress { get; set; } }
                                public class Destination { public AddressDto HomeAddress { get; set; } }

                                public class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        Diagnostic diagnostic = (await compilation.WithAnalyzers(
                [new AM020_NestedObjectMappingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync())
            .Single();

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);
        CodeAction action = Assert.Single(actions);

        Assert.Equal("Add missing nested CreateMap registrations", action.Title);
        Assert.Equal("AM020_AddMissingNestedMappings", action.EquivalenceKey);
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
                new[]
                {
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
    public async Task AM020_CodeFix_ShouldNotAddMappingForImplicitlyConvertibleNestedProperty()
    {
        const string testCode = """
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

                                    public class SourceValue
                                    {
                                        public string Raw { get; set; }
                                    }

                                    public class DestinationValue
                                    {
                                        public string Raw { get; set; }

                                        public static implicit operator DestinationValue(SourceValue source)
                                        {
                                            return new DestinationValue { Raw = source.Raw };
                                        }
                                    }

                                    public class Source
                                    {
                                        public SourceAddress Address { get; set; }
                                        public SourceValue Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public DestinationAddress Address { get; set; }
                                        public DestinationValue Value { get; set; }
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
                                             public class SourceAddress
                                             {
                                                 public string Street { get; set; }
                                             }

                                             public class DestinationAddress
                                             {
                                                 public string Street { get; set; }
                                             }

                                             public class SourceValue
                                             {
                                                 public string Raw { get; set; }
                                             }

                                             public class DestinationValue
                                             {
                                                 public string Raw { get; set; }

                                                 public static implicit operator DestinationValue(SourceValue source)
                                                 {
                                                     return new DestinationValue { Raw = source.Raw };
                                                 }
                                             }

                                             public class Source
                                             {
                                                 public SourceAddress Address { get; set; }
                                                 public SourceValue Value { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public DestinationAddress Address { get; set; }
                                                 public DestinationValue Value { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>();
                                                     CreateMap<SourceAddress, DestinationAddress>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                    .WithLocation(46, 13)
                    .WithArguments("Address", "SourceAddress", "DestinationAddress"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldAddReverseNestedMap_WhenOnlyForwardImplicitConversionExists()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceValue
                                    {
                                        public string Raw { get; set; }

                                        public static implicit operator DestinationValue(SourceValue source)
                                        {
                                            return new DestinationValue { Raw = source.Raw };
                                        }
                                    }

                                    public class DestinationValue
                                    {
                                        public string Raw { get; set; }
                                    }

                                    public class Source
                                    {
                                        public SourceValue Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public DestinationValue Value { get; set; }
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

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class SourceValue
                                             {
                                                 public string Raw { get; set; }

                                                 public static implicit operator DestinationValue(SourceValue source)
                                                 {
                                                     return new DestinationValue { Raw = source.Raw };
                                                 }
                                             }

                                             public class DestinationValue
                                             {
                                                 public string Raw { get; set; }
                                             }

                                             public class Source
                                             {
                                                 public SourceValue Value { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public DestinationValue Value { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ReverseMap();
                                                     CreateMap<DestinationValue, SourceValue>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                    .WithLocation(34, 13)
                    .WithArguments("Value", "DestinationValue", "SourceValue"),
                expectedFixedCode);
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
                new[]
                {
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

        const string expectedFixedCode = """
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
                                                     CreateMap<IAddress, AddressDto>();
                                                 }
                                             }
                                         }
                                         """;

        // Interfaces now emit AM020 to be handled properly
        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                    .WithLocation(34, 13)
                    .WithArguments("HomeAddress", "IAddress", "AddressDto"),
                expectedFixedCode);
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

    [Fact]
    public async Task AM020_CodeFix_ShouldNotAddMappingsForExplicitlyConfiguredProperties()
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
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.PrimaryContact,
                                                    opt => opt.MapFrom(src => new ContactDto { Email = src.PrimaryContact.Email }));
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
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.PrimaryContact,
                                                             opt => opt.MapFrom(src => new ContactDto { Email = src.PrimaryContact.Email }));
                                                     CreateMap<Address, AddressDto>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                    .WithLocation(41, 13)
                    .WithArguments("HomeAddress", "Address", "AddressDto"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldNotOfferFix_WhenConstructUsingHandlesForwardMapping()
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
                                            CreateMap<Source, Destination>()
                                                .ConstructUsing(src => new Destination
                                                {
                                                    HomeAddress = new AddressDto { Street = src.HomeAddress.Street }
                                                });
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM020_NestedObjectMappingAnalyzer>
            .VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldNotOfferFix_WhenConvertUsingHandlesForwardMapping()
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
                                            CreateMap<Source, Destination>()
                                                .ConvertUsing(src => new Destination
                                                {
                                                    HomeAddress = new AddressDto { Street = src.HomeAddress.Street }
                                                });
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM020_NestedObjectMappingAnalyzer>
            .VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldAddNestedMap_WhenForMemberExistsOnlyAfterReverseMap()
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
                                            CreateMap<Source, Destination>()
                                                .ReverseMap()
                                                .ForMember(src => src.HomeAddress, opt => opt.Ignore());
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
                                                     CreateMap<Source, Destination>()
                                                         .ReverseMap()
                                                         .ForMember(src => src.HomeAddress, opt => opt.Ignore());
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
    public async Task AM020_CodeFix_ShouldNotOfferFix_WhenForPathConfiguresNestedProperty()
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
                                            CreateMap<Source, Destination>()
                                                .ForPath(dest => dest.HomeAddress.Street,
                                                    opt => opt.MapFrom(src => src.HomeAddress.Street));
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM020_NestedObjectMappingAnalyzer>
            .VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldPreserveStableConfigurationReceiver()
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

                                    public sealed class MappingInstaller
                                    {
                                        public MappingInstaller(IMapperConfigurationExpression cfg)
                                        {
                                            cfg.CreateMap<Source, Destination>();
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
                                                 public Address HomeAddress { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public AddressDto HomeAddress { get; set; }
                                             }

                                             public sealed class MappingInstaller
                                             {
                                                 public MappingInstaller(IMapperConfigurationExpression cfg)
                                                 {
                                                     cfg.CreateMap<Source, Destination>();
                                                     cfg.CreateMap<Address, AddressDto>();
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
    public async Task AM020_CodeFix_ShouldPreserveStableConfigurationFieldInMethod()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Address { }
                                public class AddressDto { }
                                public class Source { public Address HomeAddress { get; set; } }
                                public class Destination { public AddressDto HomeAddress { get; set; } }

                                public sealed class MappingInstaller
                                {
                                    private readonly IMapperConfigurationExpression _configuration;

                                    public MappingInstaller(IMapperConfigurationExpression configuration)
                                    {
                                        _configuration = configuration;
                                    }

                                    public void Configure()
                                    {
                                        this._configuration.CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         public class Address { }
                                         public class AddressDto { }
                                         public class Source { public Address HomeAddress { get; set; } }
                                         public class Destination { public AddressDto HomeAddress { get; set; } }

                                         public sealed class MappingInstaller
                                         {
                                             private readonly IMapperConfigurationExpression _configuration;

                                             public MappingInstaller(IMapperConfigurationExpression configuration)
                                             {
                                                 _configuration = configuration;
                                             }

                                             public void Configure()
                                             {
                                                 this._configuration.CreateMap<Source, Destination>();
                                                 this._configuration.CreateMap<Address, AddressDto>();
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                    .WithLocation(19, 9)
                    .WithArguments("HomeAddress", "Address", "AddressDto"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldExpandExpressionBodiedVoidMethod()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Address { }
                                public class AddressDto { }
                                public class Source { public Address HomeAddress { get; set; } }
                                public class Destination { public AddressDto HomeAddress { get; set; } }

                                public sealed class MappingInstaller
                                {
                                    public void Configure(IMapperConfigurationExpression configuration)
                                        => configuration.CreateMap<Source, Destination>();
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         public class Address { }
                                         public class AddressDto { }
                                         public class Source { public Address HomeAddress { get; set; } }
                                         public class Destination { public AddressDto HomeAddress { get; set; } }

                                         public sealed class MappingInstaller
                                         {
                                             public void Configure(IMapperConfigurationExpression configuration)
                                             {
                                                 configuration.CreateMap<Source, Destination>();
                                                 configuration.CreateMap<Address, AddressDto>();
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                    .WithLocation(11, 12)
                    .WithArguments("HomeAddress", "Address", "AddressDto"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldNotOfferFix_ForExpressionBodiedNonVoidMethod()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Address { }
                                public class AddressDto { }
                                public class Source { public Address HomeAddress { get; set; } }
                                public class Destination { public AddressDto HomeAddress { get; set; } }

                                public sealed class MappingInstaller
                                {
                                    public IMappingExpression<Source, Destination> Configure(
                                        IMapperConfigurationExpression configuration)
                                        => configuration.CreateMap<Source, Destination>();
                                }
                                """;

        Document document = CreateDocument(testCode);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        Diagnostic diagnostic = Assert.Single(await compilation.WithAnalyzers(
                [new AM020_NestedObjectMappingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync());

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldNotOfferFix_ForComputedReceiverInExpressionBodiedVoidMethod()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Address { }
                                public class AddressDto { }
                                public class Source { public Address HomeAddress { get; set; } }
                                public class Destination { public AddressDto HomeAddress { get; set; } }

                                public sealed class MappingInstaller
                                {
                                    private readonly IMapperConfigurationExpression _configuration;

                                    public MappingInstaller(IMapperConfigurationExpression configuration)
                                    {
                                        _configuration = configuration;
                                    }

                                    public void Configure()
                                        => GetConfiguration().CreateMap<Source, Destination>();

                                    private IMapperConfigurationExpression GetConfiguration() => _configuration;
                                }
                                """;

        Document document = CreateDocument(testCode);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        Diagnostic diagnostic = Assert.Single(await compilation.WithAnalyzers(
                [new AM020_NestedObjectMappingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync());

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldNotOfferFix_WhenExpressionBodiedVoidMethodDefersMapInCallback()
    {
        const string testCode = """
                                using System;
                                using AutoMapper;

                                public class Address { }
                                public class AddressDto { }
                                public class Source { public Address HomeAddress { get; set; } }
                                public class Destination { public AddressDto HomeAddress { get; set; } }

                                public sealed class MappingInstaller
                                {
                                    public void Configure(IMapperConfigurationExpression configuration)
                                        => Register(() => configuration.CreateMap<Source, Destination>());

                                    private static void Register(Action callback) => callback();
                                }
                                """;

        Document document = CreateDocument(testCode);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        Diagnostic diagnostic = Assert.Single(await compilation.WithAnalyzers(
                [new AM020_NestedObjectMappingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync());

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldNotOfferFix_WhenExpressionBodiedVoidMethodIsSplitByConditionalDirectives()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Address { }
                                public class AddressDto { }
                                public class Source { public Address HomeAddress { get; set; } }
                                public class Destination { public AddressDto HomeAddress { get; set; } }

                                public sealed class MappingInstaller
                                {
                                    public void Configure(IMapperConfigurationExpression configuration)
                                #if ALTERNATE_CONFIGURATION
                                        => configuration.CreateMap<Source, Destination>().ReverseMap();
                                #else
                                        => configuration.CreateMap<Source, Destination>();
                                #endif
                                }
                                """;

        Document document = CreateDocument(testCode);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        Diagnostic diagnostic = Assert.Single(await compilation.WithAnalyzers(
                [new AM020_NestedObjectMappingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync());

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldNotOfferFix_WhenBlockStatementIsSplitByConditionalDirectives()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Address { }
                                public class AddressDto { }
                                public class Source { public Address HomeAddress { get; set; } }
                                public class Destination { public AddressDto HomeAddress { get; set; } }

                                public sealed class MappingInstaller
                                {
                                    public void Configure(IMapperConfigurationExpression configuration)
                                    {
                                #if REVERSE_MAP
                                        configuration.CreateMap<Source, Destination>().ReverseMap();
                                #else
                                        configuration.CreateMap<Source, Destination>();
                                #endif
                                    }
                                }
                                """;

        foreach (string[] preprocessorSymbols in new[]
                 {
                     Array.Empty<string>(),
                     new[] { "REVERSE_MAP" }
                 })
        {
            Document document = CreateDocument(testCode, preprocessorSymbols);
            Compilation compilation = (await document.Project.GetCompilationAsync())!;
            Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            Diagnostic diagnostic = Assert.Single(await compilation.WithAnalyzers(
                    [new AM020_NestedObjectMappingAnalyzer()])
                .GetAnalyzerDiagnosticsAsync());

            List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

            Assert.Empty(actions);
        }
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldOfferFix_WhenConditionalRegionPrecedesUnconditionalBlockStatement()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Address { }
                                public class AddressDto { }
                                public class Source { public Address HomeAddress { get; set; } }
                                public class Destination { public AddressDto HomeAddress { get; set; } }

                                public sealed class MappingInstaller
                                {
                                    public void Configure(IMapperConfigurationExpression configuration)
                                    {
                                #if OPTIONAL_SETUP
                                        _ = 1;
                                #endif
                                        configuration.CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         public class Address { }
                                         public class AddressDto { }
                                         public class Source { public Address HomeAddress { get; set; } }
                                         public class Destination { public AddressDto HomeAddress { get; set; } }

                                         public sealed class MappingInstaller
                                         {
                                             public void Configure(IMapperConfigurationExpression configuration)
                                             {
                                         #if OPTIONAL_SETUP
                                                 _ = 1;
                                         #endif
                                                 configuration.CreateMap<Source, Destination>();
                                                 configuration.CreateMap<Address, AddressDto>();
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                    .WithLocation(15, 9)
                    .WithArguments("HomeAddress", "Address", "AddressDto"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldNotOfferFix_WhenConditionalDirectivesSplitFluentBlockStatement()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Address { }
                                public class AddressDto { }
                                public class Source { public Address HomeAddress { get; set; } }
                                public class Destination { public AddressDto HomeAddress { get; set; } }

                                public sealed class MappingInstaller
                                {
                                    public void Configure(IMapperConfigurationExpression configuration)
                                    {
                                        configuration
                                #if REVERSE_MAP
                                            .CreateMap<Source, Destination>().ReverseMap();
                                #else
                                            .CreateMap<Source, Destination>();
                                #endif
                                    }
                                }
                                """;

        foreach (string[] preprocessorSymbols in new[]
                 {
                     Array.Empty<string>(),
                     new[] { "REVERSE_MAP" }
                 })
        {
            Document document = CreateDocument(testCode, preprocessorSymbols);
            Compilation compilation = (await document.Project.GetCompilationAsync())!;
            Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            Diagnostic diagnostic = Assert.Single(await compilation.WithAnalyzers(
                    [new AM020_NestedObjectMappingAnalyzer()])
                .GetAnalyzerDiagnosticsAsync());

            List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

            Assert.Empty(actions);
        }
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldPreserveArrowAndSemicolonComments_WhenExpandingVoidMethod()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Address { }
                                public class AddressDto { }
                                public class Source { public Address HomeAddress { get; set; } }
                                public class Destination { public AddressDto HomeAddress { get; set; } }

                                public sealed class MappingInstaller
                                {
                                    public void Configure(IMapperConfigurationExpression configuration)
                                        // keep-before-arrow
                                        => /* keep-arrow */ configuration.CreateMap<Source, Destination>() /* keep-semicolon */; // keep-trailing
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         public class Address { }
                                         public class AddressDto { }
                                         public class Source { public Address HomeAddress { get; set; } }
                                         public class Destination { public AddressDto HomeAddress { get; set; } }

                                         public sealed class MappingInstaller
                                         {
                                             public void Configure(IMapperConfigurationExpression configuration)
                                             {
                                                 // keep-before-arrow
                                                 /* keep-arrow */
                                                 configuration.CreateMap<Source, Destination>() /* keep-semicolon */; // keep-trailing
                                                 configuration.CreateMap<Address, AddressDto>();
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                    .WithLocation(12, 29)
                    .WithArguments("HomeAddress", "Address", "AddressDto"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldNotOfferFix_ForComputedConfigurationReceiver()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Address { }
                                public class AddressDto { }
                                public class Source { public Address HomeAddress { get; set; } }
                                public class Destination { public AddressDto HomeAddress { get; set; } }

                                public sealed class MappingInstaller
                                {
                                    private readonly IMapperConfigurationExpression _configuration;

                                    public MappingInstaller(IMapperConfigurationExpression configuration)
                                    {
                                        _configuration = configuration;
                                        GetConfiguration().CreateMap<Source, Destination>();
                                    }

                                    private IMapperConfigurationExpression GetConfiguration() => _configuration;
                                }
                                """;

        Document document = CreateDocument(testCode);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        Diagnostic diagnostic = Assert.Single(await compilation.WithAnalyzers(
                [new AM020_NestedObjectMappingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync());

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldExpandExpressionBodiedProfileConstructor()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Address { }
                                public class AddressDto { }
                                public class Source { public Address HomeAddress { get; set; } }
                                public class Destination { public AddressDto HomeAddress { get; set; } }

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile() => CreateMap<Source, Destination>();
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         public class Address { }
                                         public class AddressDto { }
                                         public class Source { public Address HomeAddress { get; set; } }
                                         public class Destination { public AddressDto HomeAddress { get; set; } }

                                         public sealed class TestProfile : Profile
                                         {
                                             public TestProfile()
                                             {
                                                 CreateMap<Source, Destination>();
                                                 CreateMap<Address, AddressDto>();
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                    .WithLocation(10, 29)
                    .WithArguments("HomeAddress", "Address", "AddressDto"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldNotOfferFix_ForComputedReceiverInExpressionBodiedConstructor()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Address { }
                                public class AddressDto { }
                                public class Source { public Address HomeAddress { get; set; } }
                                public class Destination { public AddressDto HomeAddress { get; set; } }

                                public sealed class MappingInstaller
                                {
                                    private readonly IMapperConfigurationExpression _configuration;

                                    public MappingInstaller(IMapperConfigurationExpression configuration)
                                        => GetConfiguration().CreateMap<Source, Destination>();

                                    private IMapperConfigurationExpression GetConfiguration() => _configuration;
                                }
                                """;

        Document document = CreateDocument(testCode);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        Diagnostic diagnostic = Assert.Single(await compilation.WithAnalyzers(
                [new AM020_NestedObjectMappingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync());

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldNotOfferFix_WhenExpressionBodiedConstructorDefersMapInCallback()
    {
        const string testCode = """
                                using System;
                                using AutoMapper;

                                public class Address { }
                                public class AddressDto { }
                                public class Source { public Address HomeAddress { get; set; } }
                                public class Destination { public AddressDto HomeAddress { get; set; } }

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile() => Register(() => CreateMap<Source, Destination>());

                                    private static void Register(Action callback) => callback();
                                }
                                """;

        Document document = CreateDocument(testCode);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        Diagnostic diagnostic = Assert.Single(await compilation.WithAnalyzers(
                [new AM020_NestedObjectMappingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync());

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldNotOfferFix_WhenExpressionBodiedConstructorIsSplitByConditionalDirectives()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Address { }
                                public class AddressDto { }
                                public class Source { public Address HomeAddress { get; set; } }
                                public class Destination { public AddressDto HomeAddress { get; set; } }

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                #if REVERSE_MAP
                                        => CreateMap<Source, Destination>().ReverseMap();
                                #else
                                        => CreateMap<Source, Destination>();
                                #endif
                                }
                                """;

        Document document = CreateDocument(testCode, "REVERSE_MAP");
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        Diagnostic diagnostic = Assert.Single(await compilation.WithAnalyzers(
                [new AM020_NestedObjectMappingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync());

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldNotOfferFix_WhenConstructorBlockStatementIsSplitByConditionalDirectives()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Address { }
                                public class AddressDto { }
                                public class Source { public Address HomeAddress { get; set; } }
                                public class Destination { public AddressDto HomeAddress { get; set; } }

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                #if REVERSE_MAP
                                        CreateMap<Source, Destination>().ReverseMap();
                                #else
                                        CreateMap<Source, Destination>();
                                #endif
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode, "REVERSE_MAP");
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Diagnostic diagnostic = Assert.Single(await compilation.WithAnalyzers(
                [new AM020_NestedObjectMappingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync());

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task AM020_CodeFix_ShouldPreserveArrowAndSemicolonComments_WhenExpandingConstructor()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Address { }
                                public class AddressDto { }
                                public class Source { public Address HomeAddress { get; set; } }
                                public class Destination { public AddressDto HomeAddress { get; set; } }

                                public sealed class TestProfile : Profile
                                {
                                    public TestProfile()
                                        // keep-before-arrow
                                        => /* keep-arrow */ CreateMap<Source, Destination>() /* keep-semicolon */; // keep-trailing
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         public class Address { }
                                         public class AddressDto { }
                                         public class Source { public Address HomeAddress { get; set; } }
                                         public class Destination { public AddressDto HomeAddress { get; set; } }

                                         public sealed class TestProfile : Profile
                                         {
                                             public TestProfile()
                                             {
                                                 // keep-before-arrow
                                                 /* keep-arrow */
                                                 CreateMap<Source, Destination>() /* keep-semicolon */; // keep-trailing
                                                 CreateMap<Address, AddressDto>();
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM020_NestedObjectMappingAnalyzer, AM020_NestedObjectMappingCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule)
                    .WithLocation(12, 29)
                    .WithArguments("HomeAddress", "Address", "AddressDto"),
                expectedFixedCode);
    }

    private static Document CreateDocument(string source, params string[] preprocessorSymbols)
    {
        var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);

        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, "AM020Tests", "AM020Tests", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(
                projectId,
                CSharpParseOptions.Default
                    .WithLanguageVersion(LanguageVersion.Preview)
                    .WithPreprocessorSymbols(preprocessorSymbols));

        string trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        foreach (string assemblyPath in trustedPlatformAssemblies.Split(Path.PathSeparator))
        {
            if (!string.IsNullOrWhiteSpace(assemblyPath))
            {
                solution = solution.AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyPath));
            }
        }

        solution = solution
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Profile).Assembly.Location))
            .AddDocument(documentId, "Test0.cs", SourceText.From(source));

        return solution.GetDocument(documentId)!;
    }

    private static async Task<List<CodeAction>> RegisterActionsAsync(Document document, Diagnostic diagnostic)
    {
        var actions = new List<CodeAction>();
        var provider = new AM020_NestedObjectMappingCodeFixProvider();

        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);
        return actions;
    }
}

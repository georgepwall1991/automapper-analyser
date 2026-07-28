using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

/// <summary>
///     AutoMapper's <c>IncludeMembers</c> flattens the members of an included source member into the
///     destination map. Rules that reason about unmapped source or destination members must treat those
///     members as available, and must fail closed when the included member cannot be resolved.
/// </summary>
public class IncludeMembersTests
{
    [Fact]
    public async Task AM011_ShouldNotReportDiagnostic_WhenRequiredPropertySatisfiedByIncludeMembers()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Inner
                {
                    public string Name { get; set; }
                }

                public class Source
                {
                    public Inner Inner { get; set; }
                }

                public class Destination
                {
                    public required string Name { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner);
                        CreateMap<Inner, Destination>();
                    }
                }
            }
            """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldReportDiagnostic_WhenIncludedMemberDoesNotSatisfyRequiredProperty()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Inner
                {
                    public string Other { get; set; }
                }

                public class Source
                {
                    public Inner Inner { get; set; }
                }

                public class Destination
                {
                    public required string Name { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner);
                        CreateMap<Inner, Destination>();
                    }
                }
            }
            """;

        // Both registrations legitimately report: the included member cannot supply 'Name' for
        // Source -> Destination, and Inner -> Destination does not map it either.
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule,
                17,
                32,
                "Name"
            )
            .ExpectDiagnostic(
                AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule,
                17,
                32,
                "Name"
            )
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldNotReportDiagnostic_WhenSecondIncludedMemberSatisfiesRequiredProperty()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Contact
                {
                    public string Email { get; set; }
                }

                public class Details
                {
                    public string Name { get; set; }
                }

                public class Source
                {
                    public Contact Contact { get; set; }
                    public Details Details { get; set; }
                }

                public class Destination
                {
                    public required string Name { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Contact, s => s.Details);
                        CreateMap<Contact, Destination>();
                        CreateMap<Details, Destination>();
                    }
                }
            }
            """;

        // Source -> Destination is satisfied by the second included member and stays quiet.
        // Contact -> Destination legitimately still reports: Contact has no 'Name'.
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule,
                23,
                32,
                "Name"
            )
            .RunAsync();
    }

    [Fact]
    public async Task AM004_ShouldNotReportDiagnostic_WhenSourcePropertyConsumedByIncludeMembers()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Inner
                {
                    public string Name { get; set; }
                }

                public class Source
                {
                    public Inner Inner { get; set; }
                }

                public class Destination
                {
                    public string Name { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner);
                        CreateMap<Inner, Destination>();
                    }
                }
            }
            """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM004_MissingDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_WhenSourcePropertyNotConsumedByIncludeMembers()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Inner
                {
                    public string Name { get; set; }
                }

                public class Source
                {
                    public Inner Inner { get; set; }
                    public string Dropped { get; set; }
                }

                public class Destination
                {
                    public string Name { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner);
                        CreateMap<Inner, Destination>();
                    }
                }
            }
            """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM004_MissingDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
                13,
                23,
                "Dropped"
            )
            .RunAsync();
    }

    [Fact]
    public async Task AM006_ShouldNotReportDiagnostic_WhenDestinationPropertySatisfiedByIncludeMembers()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Inner
                {
                    public string Name { get; set; }
                }

                public class Source
                {
                    public Inner Inner { get; set; }
                }

                public class Destination
                {
                    public string Name { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner);
                        CreateMap<Inner, Destination>();
                    }
                }
            }
            """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM006_UnmappedDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM006_ShouldReportDiagnostic_WhenIncludedMemberDoesNotSatisfyDestinationProperty()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Inner
                {
                    public string Other { get; set; }
                }

                public class Source
                {
                    public Inner Inner { get; set; }
                }

                public class Destination
                {
                    public string Name { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner);
                        CreateMap<Inner, Destination>();
                    }
                }
            }
            """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM006_UnmappedDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
                17,
                23,
                "Name",
                "Source"
            )
            .ExpectDiagnostic(
                AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
                17,
                23,
                "Name",
                "Inner"
            )
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldNotReportDiagnostic_WhenIncludedMemberSatisfiesPropertyThroughFlattening()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Address
                {
                    public string City { get; set; }
                }

                public class Inner
                {
                    public Address Address { get; set; }
                }

                public class Source
                {
                    public Inner Inner { get; set; }
                }

                public class Destination
                {
                    public required string AddressCity { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner);
                        CreateMap<Inner, Destination>();
                    }
                }
            }
            """;

        // Source -> Destination is satisfied by flattening the included member and stays quiet.
        // Inner -> Destination still reports: AM011 does not resolve flattening on its own, which is
        // pre-existing behaviour independent of IncludeMembers.
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule,
                22,
                32,
                "AddressCity"
            )
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldNotReportDiagnostic_WhenIncludeMembersUsesParenthesizedLambda()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Inner
                {
                    public string Name { get; set; }
                }

                public class Source
                {
                    public Inner Inner { get; set; }
                }

                public class Destination
                {
                    public required string Name { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>().IncludeMembers((Source s) => s.Inner);
                        CreateMap<Inner, Destination>();
                    }
                }
            }
            """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldNotReportDiagnostic_WhenIncludeMembersTargetsNestedMemberPath()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Leaf
                {
                    public string Name { get; set; }
                }

                public class Inner
                {
                    public Leaf Leaf { get; set; }
                }

                public class Source
                {
                    public Inner Inner { get; set; }
                }

                public class Destination
                {
                    public required string Name { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner.Leaf);
                        CreateMap<Leaf, Destination>();
                    }
                }
            }
            """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldReportDiagnostic_WhenLaterIncludeMembersCallReplacesEarlierOne()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class First
                {
                    public string Name { get; set; }
                }

                public class Second
                {
                    public string Other { get; set; }
                }

                public class Source
                {
                    public First First { get; set; }
                    public Second Second { get; set; }
                }

                public class Destination
                {
                    public required string Name { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>().IncludeMembers(s => s.First).IncludeMembers(s => s.Second);
                    }
                }
            }
            """;

        // AutoMapper replaces IncludedMembers on each IncludeMembers call, so only the final call is
        // effective: 'First' no longer supplies 'Name' at runtime and the diagnostic must still fire.
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule,
                23,
                32,
                "Name"
            )
            .RunAsync();
    }

    [Fact]
    public async Task AM004_ShouldNotReportDiagnostic_WhenIncludeMembersSelectorIsNotResolvable()
    {
        const string testCode = """
            using System;
            using System.Linq.Expressions;
            using AutoMapper;

            namespace TestNamespace
            {
                public class Inner
                {
                    public string Name { get; set; }
                }

                public class Source
                {
                    public Inner Inner { get; set; }
                }

                public class Destination
                {
                    public string Name { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        Expression<Func<Source, object>> include = s => s.Inner;
                        CreateMap<Source, Destination>().IncludeMembers(include);
                        CreateMap<Inner, Destination>();
                    }
                }
            }
            """;

        // A selector passed through a variable is valid AutoMapper but not statically resolvable here,
        // so AM004 must fail closed rather than claim the included member is dropped.
        await DiagnosticTestFramework
            .ForAnalyzer<AM004_MissingDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }
}

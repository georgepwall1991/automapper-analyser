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

    [Fact]
    public async Task AM011_ShouldNotReportDiagnostic_WhenIncludedMapIgnoresTheMember_KnownLimitation()
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
                        CreateMap<Inner, Destination>().ForMember(d => d.Name, o => o.Ignore());
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner);
                    }
                }
            }
            """;

        // KNOWN LIMITATION (documented in docs/TEST_LIMITATIONS.md): the included type declares 'Name'
        // but its own map ignores it, so AutoMapper does reject this at configuration time. Deciding
        // that statically requires modelling the child map's full member resolution; approximating it
        // produced Error-severity false positives on valid maps, so the scope reasons about the included
        // type's shape only and stays purely suppressing. This asserts the shipped behaviour.
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldNotReportDiagnostic_WhenIncludeMembersSelectorIsNullForgiving()
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
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner!);
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
    public async Task AM011_ShouldReportDiagnostic_WhenNullForgivingIncludeDoesNotSupplyOtherMembers()
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
                    public required string Missing { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner!);
                    }
                }
            }
            """;

        // A resolvable null-forgiving selector must not blanket-suppress unrelated members:
        // 'Missing' is supplied by nobody and AutoMapper rejects the map.
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule,
                18,
                32,
                "Missing"
            )
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldNotReportDiagnostic_WhenIncludedMapIgnoresAllMembers_KnownLimitation()
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
                        CreateMap<Inner, Destination>().ForAllMembers(o => o.Ignore());
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner);
                    }
                }
            }
            """;

        // KNOWN LIMITATION (see the ForMember variant above): map-wide ignore on the included map is not
        // modelled, for the same false-positive-avoidance reason. Asserts the shipped behaviour.
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldNotReportDiagnostic_WhenIncludedChildMapSuppliesMemberFromDifferentSource()
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
                        CreateMap<Inner, Destination>().ForMember(d => d.Name, o => o.MapFrom(s => s.Other));
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner);
                    }
                }
            }
            """;

        // AutoMapper validates and executes this: the child map supplies 'Name' from a differently named
        // source. Reporting here would be an Error-severity false positive on a valid mapping.
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_WhenIncludeMembersUsesExplicitArraySyntax()
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
                        CreateMap<Source, Destination>()
                            .IncludeMembers(new Expression<Func<Source, object>>[] { s => s.Inner });
                        CreateMap<Inner, Destination>();
                    }
                }
            }
            """;

        // The explicit params-array form must resolve like a bare lambda: 'Inner' is consumed, but the
        // unrelated 'Dropped' member must still report rather than being blanket-suppressed.
        await DiagnosticTestFramework
            .ForAnalyzer<AM004_MissingDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule,
                15,
                23,
                "Dropped"
            )
            .RunAsync();
    }
}

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
    public async Task AM011_ShouldReportDiagnostic_WhenIncludedMapIgnoresTheMember()
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

        // The included type declares 'Name', but its own map explicitly ignores it, so AutoMapper
        // rejects this configuration at startup - verified against the real runtime, not assumed.
        // Reading an explicit Ignore is not the same as inferring that a child map fails to supply a
        // member: it requires a uniquely resolved child map and a direct ForMember on that exact
        // member, so it cannot fire on the unresolvable maps that caused the earlier false positives.
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule, 17, 32,
                "Name")
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
    public async Task AM011_ShouldNotReportDiagnostic_WhenAnUnrelatedIgnoreHelperIsCalledInConfiguration()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public static class Settings
                {
                    public static bool Ignore() => false;
                }

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
                        CreateMap<Inner, Destination>()
                            .ForMember(d => d.Name, o => o.Condition((src, dest, sm, dm) => !Settings.Ignore()));
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner);
                    }
                }
            }
            """;

        // A zero-argument Ignore() that is not AutoMapper's - here an unrelated helper inside a
        // Condition - must not be read as the map ignoring the member. Scanning descendants for any
        // call named Ignore would turn this valid mapping into an Error-severity false positive, so
        // the call has to resolve to the configuration lambda's own options parameter.
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldNotReportDiagnostic_WhenIgnoreTargetsADifferentMember()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Inner
                {
                    public string Name { get; set; }
                    public string Other { get; set; }
                }

                public class Source
                {
                    public Inner Inner { get; set; }
                }

                public class Destination
                {
                    public required string Name { get; set; }
                    public string Other { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Inner, Destination>().ForMember(d => d.Other, o => o.Ignore());
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner);
                    }
                }
            }
            """;

        // An Ignore on a different member says nothing about 'Name', which the included type supplies.
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldNotReportDiagnostic_WhenExplicitMapFromFollowsIgnoreAllMembers()
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
                        CreateMap<Inner, Destination>()
                            .ForAllMembers(o => o.Ignore());
                        CreateMap<Inner, Destination>()
                            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name));
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner);
                    }
                }
            }
            """;

        // Two registrations for the same pair leave the child map ambiguous, so nothing about it can be
        // read and the scope must stay suppressing - the fail-closed path an Error rule depends on.
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldNotReportDiagnostic_WhenMapFromSuppliesMemberAfterIgnoreAllMembers()
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
                        CreateMap<Inner, Destination>()
                            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name))
                            .ForAllMembers(o => o.Ignore());
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner);
                    }
                }
            }
            """;

        // Both an explicit MapFrom and a map-wide Ignore are present. Which one wins depends on the
        // order AutoMapper applies them, which this scope does not model, so it stays suppressing rather
        // than risk an Error-severity false positive. A false negative here is the deliberate trade.
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldNotReportDiagnostic_WhenForPathIgnoresANestedMemberOfTheSameName()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Detail
                {
                    public string Name { get; set; }
                }

                public class Inner
                {
                    public string Name { get; set; }
                    public Detail Detail { get; set; }
                }

                public class DestinationDetail
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
                    public DestinationDetail Detail { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Detail, DestinationDetail>();
                        CreateMap<Inner, Destination>().ForPath(d => d.Detail.Name, o => o.Ignore());
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner);
                    }
                }
            }
            """;

        // ForPath designates a nested path, so ignoring Detail.Name must not be read as ignoring the
        // top-level Name that shares its identifier.
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldReportDiagnostic_WhenIncludedMapIgnoresAllMembers()
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

        // Map-wide ignore, same reasoning as the ForMember variant above: an explicit ForAllMembers
        // Ignore states that the child map supplies nothing, so the required member is unmapped.
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule, 17, 32,
                "Name")
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
    public async Task AM004_ShouldNotReportDiagnostic_WhenIncludeMembersUsesExplicitArraySyntax_FailsClosed()
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

        // The explicit params-array form is not interpreted, so the whole mapping fails closed - even
        // the unrelated 'Dropped' member stays quiet. Blunt, but suppression can never break a build;
        // interpreting these shapes is what produced Error-severity false positives during review.
        await DiagnosticTestFramework
            .ForAnalyzer<AM004_MissingDestinationPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldNotReportDiagnostic_WhenIncludeMembersUsesCollectionSpread()
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
                    public required string Name { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        Expression<Func<Source, object>>[] selectors = [s => s.Inner];
                        CreateMap<Source, Destination>().IncludeMembers([..selectors]);
                        CreateMap<Inner, Destination>();
                    }
                }
            }
            """;

        // A spread's contents are not statically enumerable, so the include must fail closed rather than
        // look like an empty include set and report a member AutoMapper actually maps.
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldNotReportDiagnostic_WhenIncludeMembersSelectorCastsToDerivedType_FailsClosed()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Inner
                {
                }

                public class DerivedInner : Inner
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
                        CreateMap<Source, Destination>().IncludeMembers(s => (DerivedInner)s.Inner);
                        CreateMap<DerivedInner, Destination>();
                    }
                }
            }
            """;

        // A cast selector is not interpreted; the mapping fails closed and stays quiet. That is the
        // correct outcome here anyway, and it holds without the analyzer having to reason about which
        // casts AutoMapper preserves.
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM011_ShouldReportDiagnostic_WhenChildMapForPathConfiguresNestedMemberOnly()
    {
        const string testCode = """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Inner
                {
                    public string Source { get; set; }
                }

                public class Details
                {
                    public string Name { get; set; }
                }

                public class Source
                {
                    public Inner Inner { get; set; }
                }

                public class Destination
                {
                    public Details Details { get; set; }
                    public required string Name { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Inner, Destination>().ForPath(d => d.Details.Name, o => o.MapFrom(s => s.Source));
                        CreateMap<Source, Destination>().IncludeMembers(s => s.Inner);
                    }
                }
            }
            """;

        // ForPath configures the nested Details.Name, not the top-level required Name, so the parent map
        // still has no source for it and AutoMapper rejects the configuration. Both registrations report:
        // Inner -> Destination does not map Name either.
        await DiagnosticTestFramework
            .ForAnalyzer<AM011_UnmappedRequiredPropertyAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule,
                23,
                32,
                "Name"
            )
            .ExpectDiagnostic(
                AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule,
                23,
                32,
                "Name"
            )
            .RunAsync();
    }
}

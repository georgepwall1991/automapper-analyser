using AutoMapperAnalyzer.Analyzers.ComplexMappings;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests.ComplexMappings;

public class AM022_InfiniteRecursionTests
{
    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenDirectCircularReference()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule, 21, 13, "SourcePerson",
                "DestPerson")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenIndirectCircularReference()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public string Name { get; set; }
                                        public SourceB RelatedB { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public string Value { get; set; }
                                        public SourceC RelatedC { get; set; }
                                    }

                                    public class SourceC
                                    {
                                        public string Data { get; set; }
                                        public SourceA RelatedA { get; set; }
                                    }

                                    public class DestA
                                    {
                                        public string Name { get; set; }
                                        public DestB RelatedB { get; set; }
                                    }

                                    public class DestB
                                    {
                                        public string Value { get; set; }
                                        public DestC RelatedC { get; set; }
                                    }

                                    public class DestC
                                    {
                                        public string Data { get; set; }
                                        public DestA RelatedA { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestA>();
                                            CreateMap<SourceB, DestB>();
                                            CreateMap<SourceC, DestC>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule, 45, 13, "SourceA", "DestA")
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule, 46, 13, "SourceB", "DestB")
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule, 47, 13, "SourceC", "DestC")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenCustomGuidNamedTypeParticipatesInCycle()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Guid Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public GuidDto Value { get; set; }
                                    }

                                    public class Guid
                                    {
                                        public Source Owner { get; set; }
                                    }

                                    public class GuidDto
                                    {
                                        public Destination Owner { get; set; }
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule, 29, 13, "Source",
                "Destination")
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule, 30, 13, "Guid", "GuidDto")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenSelfReferencingType()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public string Name { get; set; }
                                        public SourceNode Parent { get; set; }
                                        public SourceNode LeftChild { get; set; }
                                        public SourceNode RightChild { get; set; }
                                    }

                                    public class DestNode
                                    {
                                        public string Name { get; set; }
                                        public DestNode Parent { get; set; }
                                        public DestNode LeftChild { get; set; }
                                        public DestNode RightChild { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestNode>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule, 25, 13, "SourceNode", "DestNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenMaxDepthConfigured()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .MaxDepth(3);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenCircularPropertyIgnored()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .ForMember(dest => dest.Friend, opt => opt.Ignore());
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenNoCircularReferences()
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

                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourceAddress Address { get; set; }
                                    }

                                    public class DestAddress
                                    {
                                        public string Street { get; set; }
                                        public string City { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestAddress Address { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>();
                                            CreateMap<SourceAddress, DestAddress>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldHandleCollectionReferences()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class SourceCategory
                                    {
                                        public string Name { get; set; }
                                        public List<SourceCategory> SubCategories { get; set; }
                                        public SourceCategory Parent { get; set; }
                                    }

                                    public class DestCategory
                                    {
                                        public string Name { get; set; }
                                        public List<DestCategory> SubCategories { get; set; }
                                        public DestCategory Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceCategory, DestCategory>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule, 24, 13, "SourceCategory",
                "DestCategory")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenCrossTypePropertiesAreNotMappedByConvention()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceEntity
                                    {
                                        public DestEntity RelatedDest { get; set; }
                                    }

                                    public class DestEntity
                                    {
                                        public SourceEntity RelatedSource { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceEntity, DestEntity>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportMultipleCircularPaths()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public string Name { get; set; }
                                        public SourceNode Left { get; set; }
                                        public SourceNode Right { get; set; }
                                        public SourceContainer Container { get; set; }
                                    }

                                    public class SourceContainer
                                    {
                                        public string Id { get; set; }
                                        public SourceNode Root { get; set; }
                                    }

                                    public class DestNode
                                    {
                                        public string Name { get; set; }
                                        public DestNode Left { get; set; }
                                        public DestNode Right { get; set; }
                                        public DestContainer Container { get; set; }
                                    }

                                    public class DestContainer
                                    {
                                        public string Id { get; set; }
                                        public DestNode Root { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestNode>();
                                            CreateMap<SourceContainer, DestContainer>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule, 37, 13, "SourceNode", "DestNode")
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule, 38, 13, "SourceContainer",
                "DestContainer")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldIgnoreValueTypes()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public int Count { get; set; }
                                        public DateTime Date { get; set; }
                                        public Guid Id { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public int Count { get; set; }
                                        public DateTime Date { get; set; }
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenOnlySourceTypeIsSelfReferencing()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public string Name { get; set; }
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, Destination>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenOnlyOneOfMultipleSelfReferencingPropertiesIgnored()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public string Name { get; set; }
                                        public SourceNode Parent { get; set; }
                                        public SourceNode Child { get; set; }
                                    }

                                    public class DestNode
                                    {
                                        public string Name { get; set; }
                                        public DestNode Parent { get; set; }
                                        public DestNode Child { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestNode>()
                                                .ForMember(dest => dest.Parent, opt => opt.Ignore());
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule, 23, 13, "SourceNode",
                "DestNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenForMemberIgnoreUsesStringPropertyName()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .ForMember("Friend", opt => opt.Ignore());
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenForMemberIgnoreUsesNameofPropertyName()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .ForMember(nameof(DestPerson.Friend), opt => opt.Ignore());
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenForMemberIgnoreUsesConstantPropertyName()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private const string FriendMemberName = nameof(DestPerson.Friend);

                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .ForMember(FriendMemberName, opt => opt.Ignore());
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenForMemberIgnoreUsesParenthesizedLambdas()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .ForMember((dest) => dest.Friend, (opt) => opt.Ignore());
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenForMemberIgnoreUsesTypedLambda()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .ForMember((DestPerson dest) => dest.Friend, opt => opt.Ignore());
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenIgnoreConfiguredOnlyAfterReverseMap()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .ReverseMap()
                                                .ForMember(src => src.Friend, opt => opt.Ignore());
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule, 21, 13, "SourcePerson",
                "DestPerson")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenAllSelfReferencingPropertiesAreIgnored()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public string Name { get; set; }
                                        public SourceNode Parent { get; set; }
                                        public SourceNode Child { get; set; }
                                    }

                                    public class DestNode
                                    {
                                        public string Name { get; set; }
                                        public DestNode Parent { get; set; }
                                        public DestNode Child { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestNode>()
                                                .ForMember(dest => dest.Parent, opt => opt.Ignore())
                                                .ForMember(dest => dest.Child, opt => opt.Ignore());
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenOnlySourceGraphIsCircular()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB B { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA A { get; set; }
                                    }

                                    public class DestA
                                    {
                                        public DestB B { get; set; }
                                    }

                                    public class DestB
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestA>();
                                            CreateMap<SourceB, DestB>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenMaxDepthConfiguredOnlyAfterReverseMap()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .ReverseMap()
                                                .MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule, 21, 13, "SourcePerson",
                "DestPerson")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenMaxDepthConfiguredBeforeReverseMap()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .MaxDepth(2)
                                                .ReverseMap();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenForPathIgnoreConfiguredForCircularProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .ForPath(dest => dest.Friend, opt => opt.Ignore());
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenTypedForPathIgnoreConfiguredForCircularProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .ForPath((DestPerson dest) => dest.Friend, opt => opt.Ignore());
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenForMemberTargetsPropertyNamedIgnoreButDoesNotIgnore()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                        public bool IgnoreFlag { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                        public bool IgnoreFlag { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .ForMember(dest => dest.IgnoreFlag, opt => opt.MapFrom(src => src.IgnoreFlag));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule, 23, 13, "SourcePerson",
                "DestPerson")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenForMemberOptionsCallNonAutoMapperIgnoreHelper()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public static class MappingHelpers
                                    {
                                        public static void Ignore() {}
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .ForMember(dest => dest.Friend, opt => MappingHelpers.Ignore());
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule, 26, 13, "SourcePerson",
                "DestPerson")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenSelfReferencingMemberNamesDoNotMatch()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public string Name { get; set; }
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestNode
                                    {
                                        public string Name { get; set; }
                                        public DestNode Manager { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestNode>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenCircularGraphsUseDifferentMemberNames()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Owner { get; set; }
                                    }

                                    public class DestA
                                    {
                                        public DestB Related { get; set; }
                                    }

                                    public class DestB
                                    {
                                        public DestA Root { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestA>();
                                            CreateMap<SourceB, DestB>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenIndirectRecursiveTopLevelPropertyIgnored()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Owner { get; set; }
                                    }

                                    public class DestA
                                    {
                                        public DestB Child { get; set; }
                                    }

                                    public class DestB
                                    {
                                        public DestA Owner { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestA>()
                                                .ForMember(dest => dest.Child, opt => opt.Ignore());
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenIndirectRecursiveNestedMapIsMissing()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Owner { get; set; }
                                    }

                                    public class DestA
                                    {
                                        public DestB Child { get; set; }
                                    }

                                    public class DestB
                                    {
                                        public DestA Owner { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestA>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenPreserveReferencesConfigured()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .PreserveReferences();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenPreserveReferencesConfiguredOnlyAfterReverseMap()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .ReverseMap()
                                                .PreserveReferences();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule, 21, 13, "SourcePerson",
                "DestPerson")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenOnlyCustomConstructionConfigured()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .ConstructUsing(src => new DestPerson { Name = src.Name });
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule, 21, 13, "SourcePerson",
                "DestPerson")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenConvertUsingConfigured()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .ConvertUsing(src => new DestPerson { Name = src.Name });
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenConvertUsingConfiguredOnlyAfterReverseMap()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .ReverseMap()
                                                .ConvertUsing(dest => new SourcePerson { Name = dest.Name });
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule, 21, 13, "SourcePerson",
                "DestPerson")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenPreserveReferencesIsNotAutoMapperMethod()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                        public SourcePerson Friend { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                        public DestPerson Friend { get; set; }
                                    }

                                    public sealed class CustomMappingExpression
                                    {
                                        public CustomMappingExpression PreserveReferences() => this;
                                    }

                                    public static class CustomMappingExtensions
                                    {
                                        public static CustomMappingExpression Custom<TSource, TDestination>(
                                            this IMappingExpression<TSource, TDestination> mapping) =>
                                            new();
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourcePerson, DestPerson>()
                                                .Custom()
                                                .PreserveReferences();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule, 33, 13, "SourcePerson",
                "DestPerson")
            .RunAsync();
    }

    [Theory]
    [InlineData(".PreserveReferences()")]
    [InlineData(".MaxDepth(2)")]
    [InlineData(".ConvertUsing((SourceB source) => new DestB())")]
    [InlineData(".ConvertUsing<SourceBConverter>()")]
    public async Task AM022_ShouldNotReportDiagnostic_WhenDownstreamCycleMapIsConstrained(string cycleBreaker)
    {
        string testCode = $$"""
                            using AutoMapper;

                            namespace TestNamespace
                            {
                                public class SourceA
                                {
                                    public SourceB Child { get; set; }
                                }

                                public class SourceB
                                {
                                    public SourceA Owner { get; set; }
                                }

                                public class DestA
                                {
                                    public DestB Child { get; set; }
                                }

                                public class DestB
                                {
                                    public DestA Owner { get; set; }
                                }

                                public sealed class SourceBConverter : ITypeConverter<SourceB, DestB>
                                {
                                    public DestB Convert(SourceB source, DestB destination, ResolutionContext context) =>
                                        new();
                                }

                                public class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<SourceA, DestA>();
                                        CreateMap<SourceB, DestB>(){{cycleBreaker}};
                                    }
                                }
                            }
                            """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Theory]
    [InlineData("mapping.MaxDepth(2);")]
    [InlineData("mapping.PreserveReferences();")]
    [InlineData("mapping.ConvertUsing(source => new DestinationNode());")]
    public async Task AM022_ShouldNotReportDiagnostic_WhenRootCycleBreakerIsConfiguredInLaterStatement(
        string cycleBreaker)
    {
        string testCode = $$"""
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>();
                                            {{cycleBreaker}}
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Theory]
    [InlineData("(IMappingExpression<SourceNode, DestinationNode>)CreateMap<SourceNode, DestinationNode>()")]
    [InlineData("CreateMap<SourceNode, DestinationNode>()!")]
    public async Task AM022_ShouldNotReportDiagnostic_WhenDeferredRootInitializerUsesTransparentWrapper(
        string mappingInitializer)
    {
        string testCode = $$"""
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            var mapping = {{mappingInitializer}};
                                            mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Theory]
    [InlineData("((IMappingExpression<SourceNode, DestinationNode>)mapping).MaxDepth(2);")]
    [InlineData("mapping!.MaxDepth(2);")]
    public async Task AM022_ShouldNotReportDiagnostic_WhenDeferredRootPolicyReceiverUsesTransparentWrapper(
        string cycleBreaker)
    {
        string testCode = $$"""
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>();
                                            {{cycleBreaker}}
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Theory]
    [InlineData("mapping.ForMember(destination => destination.Name, options => options.Ignore());")]
    [InlineData("mapping.ReverseMap();")]
    public async Task AM022_ShouldNotReportDiagnostic_WhenSafeAutoMapperStatementPrecedesDeferredRootPolicy(
        string interveningStatement)
    {
        string testCode = $$"""
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public string Name { get; set; }
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public string Name { get; set; }
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>();
                                            {{interveningStatement}}
                                            mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenInterveningAutoMapperCallbackWritesDeferredRootLocal()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public string Name { get; set; }
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public string Name { get; set; }
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(IMappingExpression<SourceNode, DestinationNode> externalMapping)
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>();
                                            mapping.ForMember(
                                                destination => destination.Name,
                                                options =>
                                                {
                                                    mapping = externalMapping;
                                                    options.Ignore();
                                                });
                                            mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                21,
                27,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenInterveningAutoMapperCallbackInvokesMutatingLocalFunction()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public string Name { get; set; }
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public string Name { get; set; }
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(IMappingExpression<SourceNode, DestinationNode> externalMapping)
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>();
                                            void Replace() => mapping = externalMapping;

                                            mapping.ForMember(
                                                destination => destination.Name,
                                                options =>
                                                {
                                                    Replace();
                                                    options.Ignore();
                                                });
                                            mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                21,
                27,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenDeferredRootMappingLocalComesFromSubstitutingInitializer()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public static class MappingHelpers
                                    {
                                        public static IMappingExpression<SourceNode, DestinationNode> Pick(
                                            IMappingExpression<SourceNode, DestinationNode> original,
                                            IMappingExpression<SourceNode, DestinationNode> replacement) => replacement;
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(IMappingExpression<SourceNode, DestinationNode> externalMapping)
                                        {
                                            var mapping = MappingHelpers.Pick(
                                                CreateMap<SourceNode, DestinationNode>(),
                                                externalMapping);
                                            mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                27,
                17,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenDeferredRootMappingLocalComesFromSubstitutingExtensionInitializer()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public static class MappingExtensions
                                    {
                                        public static IMappingExpression<SourceNode, DestinationNode> Pick(
                                            this IMappingExpression<SourceNode, DestinationNode> original,
                                            IMappingExpression<SourceNode, DestinationNode> replacement) => replacement;
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(IMappingExpression<SourceNode, DestinationNode> externalMapping)
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>().Pick(externalMapping);
                                            mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                26,
                27,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenSubstitutingInitializerExtensionSpoofsAutoMapperNamespace()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace AutoMapper
                                {
                                    public static class SpoofedMappingExtensions
                                    {
                                        public static IMappingExpression<TestNamespace.SourceNode, TestNamespace.DestinationNode> Pick(
                                            this IMappingExpression<TestNamespace.SourceNode, TestNamespace.DestinationNode> original,
                                            IMappingExpression<TestNamespace.SourceNode, TestNamespace.DestinationNode> replacement) => replacement;
                                    }
                                }

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(IMappingExpression<SourceNode, DestinationNode> externalMapping)
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>().Pick(externalMapping);
                                            mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                29,
                27,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Theory]
    [InlineData("CreateMap<SourceNode, DestinationNode>().MaxDepth();", 28, 13)]
    [InlineData("var mapping = CreateMap<SourceNode, DestinationNode>(); mapping.MaxDepth();", 28, 27)]
    public async Task AM022_ShouldReportDiagnostic_WhenCycleBreakerSpoofsAutoMapperNamespace(
        string mappingConfiguration,
        int line,
        int column)
    {
        string testCode = $$"""
                            using AutoMapper;

                            namespace AutoMapper
                            {
                                public static class SpoofedMappingExtensions
                                {
                                    public static IMappingExpression<TestNamespace.SourceNode, TestNamespace.DestinationNode> MaxDepth(
                                        this IMappingExpression<TestNamespace.SourceNode, TestNamespace.DestinationNode> mapping) => mapping;
                                }
                            }

                            namespace TestNamespace
                            {
                                public class SourceNode
                                {
                                    public SourceNode Parent { get; set; }
                                }

                                public class DestinationNode
                                {
                                    public DestinationNode Parent { get; set; }
                                }

                                public class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        {{mappingConfiguration}}
                                    }
                                }
                            }
                            """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                line,
                column,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenDirectCycleBreakerFollowsSubstitutingExtension()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public static class MappingExtensions
                                    {
                                        public static IMappingExpression<SourceNode, DestinationNode> Pick(
                                            this IMappingExpression<SourceNode, DestinationNode> original,
                                            IMappingExpression<SourceNode, DestinationNode> replacement) => replacement;
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(IMappingExpression<SourceNode, DestinationNode> externalMapping)
                                        {
                                            CreateMap<SourceNode, DestinationNode>()
                                                .Pick(externalMapping)
                                                .MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                26,
                13,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenLaterDeclaratorMutatesDeferredRootMappingLocal()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public static class MappingHelpers
                                    {
                                        public static IMappingExpression<SourceNode, DestinationNode> Replace(
                                            ref IMappingExpression<SourceNode, DestinationNode> mapping,
                                            IMappingExpression<SourceNode, DestinationNode> replacement)
                                        {
                                            mapping = replacement;
                                            return replacement;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(IMappingExpression<SourceNode, DestinationNode> externalMapping)
                                        {
                                            IMappingExpression<SourceNode, DestinationNode> mapping =
                                                    CreateMap<SourceNode, DestinationNode>(),
                                                other = MappingHelpers.Replace(ref mapping, externalMapping);
                                            mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                31,
                21,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenDeferredRootMappingLocalUsesAutoMapperInitializerChain()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public string Name { get; set; }
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public string Name { get; set; }
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>()
                                                .ForMember(destination => destination.Name, options => options.Ignore());
                                            mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenConditionalExitCanSkipDeferredRootCycleBreaker()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(bool skipPolicy)
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>();
                                            if (skipPolicy)
                                            {
                                                return;
                                            }

                                            mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                19,
                27,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenConditionalExpressionCanSkipDeferredRootCycleBreaker()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(bool skipPolicy)
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>();
                                            _ = skipPolicy ? mapping : mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                19,
                27,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenDeferredRootCycleBreakerReceiverOnlyContainsMappingLocal()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public static class MappingHelpers
                                    {
                                        public static IMappingExpression<SourceNode, DestinationNode> Pick(
                                            IMappingExpression<SourceNode, DestinationNode> original,
                                            IMappingExpression<SourceNode, DestinationNode> replacement) => replacement;
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>();
                                            MappingHelpers.Pick(mapping, null!).MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                26,
                27,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenDeferredRootCycleBreakerUsesSubstitutingExtension()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public static class MappingExtensions
                                    {
                                        public static IMappingExpression<SourceNode, DestinationNode> Pick(
                                            this IMappingExpression<SourceNode, DestinationNode> original,
                                            IMappingExpression<SourceNode, DestinationNode> replacement) => replacement;
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>();
                                            mapping.Pick(null!).MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                26,
                27,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Theory]
    [InlineData("mapping = externalMapping;")]
    [InlineData("MappingHelpers.Replace(ref mapping, externalMapping);")]
    public async Task AM022_ShouldReportDiagnostic_WhenDeferredRootMappingLocalIsWrittenBeforePolicy(
        string mutation)
    {
        string testCode = $$"""
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public static class MappingHelpers
                                    {
                                        public static void Replace(
                                            ref IMappingExpression<SourceNode, DestinationNode> current,
                                            IMappingExpression<SourceNode, DestinationNode> replacement)
                                        {
                                            current = replacement;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(
                                            IMappingExpression<SourceNode, DestinationNode> externalMapping)
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>();
                                            {{mutation}}
                                            mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                30,
                27,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Theory]
    [InlineData("void Replace() => mapping = externalMapping;")]
    [InlineData("void Replace() { mapping = externalMapping; }")]
    public async Task AM022_ShouldReportDiagnostic_WhenInvokedLocalFunctionWritesDeferredRootMappingLocal(
        string localFunction)
    {
        string testCode = $$"""
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(
                                            IMappingExpression<SourceNode, DestinationNode> externalMapping)
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>();
                                            {{localFunction}}
                                            Replace();
                                            mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                20,
                27,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Theory]
    [InlineData("void Replace() => Apply(); void Apply() => mapping = externalMapping;", "Replace();")]
    [InlineData("void First() => Second(); void Second() => First();", "First();")]
    public async Task AM022_ShouldReportDiagnostic_WhenInvokedLocalFunctionEffectsAreUnsafe(
        string localFunctions,
        string invocation)
    {
        string testCode = $$"""
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(
                                            IMappingExpression<SourceNode, DestinationNode> externalMapping)
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>();
                                            {{localFunctions}}
                                            {{invocation}}
                                            mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                20,
                27,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenDelegateCanWriteDeferredRootMappingLocal()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(
                                            IMappingExpression<SourceNode, DestinationNode> externalMapping)
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>();
                                            void Replace() => mapping = externalMapping;
                                            System.Action replace = Replace;
                                            replace();
                                            mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                20,
                27,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenDynamicDelegateCanWriteDeferredRootMappingLocal()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(
                                            IMappingExpression<SourceNode, DestinationNode> externalMapping)
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>();
                                            void Replace() => mapping = externalMapping;
                                            dynamic replace = (System.Action)Replace;
                                            replace();
                                            mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                20,
                27,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenDelegateDynamicInvokeCanWriteDeferredRootMappingLocal()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(
                                            IMappingExpression<SourceNode, DestinationNode> externalMapping)
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>();
                                            void Replace() => mapping = externalMapping;
                                            System.Action replace = Replace;
                                            replace.DynamicInvoke();
                                            mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                20,
                27,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenOrdinaryWrapperInvokesMappingCapturingDelegate()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(
                                            IMappingExpression<SourceNode, DestinationNode> externalMapping)
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>();
                                            void Replace() => mapping = externalMapping;
                                            System.Action replace = Replace;
                                            Run(replace);
                                            mapping.MaxDepth(2);
                                        }

                                        private static void Run(System.Action action) => action();
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                20,
                27,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenConstructorInvokesMappingCapturingDelegate()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public sealed class Runner
                                    {
                                        public Runner(System.Action action) => action();
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(
                                            IMappingExpression<SourceNode, DestinationNode> externalMapping)
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>();
                                            void Replace() => mapping = externalMapping;
                                            new Runner(Replace);
                                            mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                25,
                27,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenDeferredRootCycleBreakerAppliesOnlyAfterReverseMap()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            var mapping = CreateMap<SourceNode, DestinationNode>();
                                            mapping.ReverseMap().MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                19,
                27,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostics_WhenOnlyOneDuplicateRootMapIsDeferredConstrained()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            var constrained = CreateMap<SourceNode, DestinationNode>();
                                            constrained.MaxDepth(2);
                                            CreateMap<SourceNode, DestinationNode>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                19,
                31,
                "SourceNode",
                "DestinationNode")
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                21,
                13,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenDownstreamCycleMapIsConstrainedInLaterStatement()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Owner { get; set; }
                                    }

                                    public class DestA
                                    {
                                        public DestB Child { get; set; }
                                    }

                                    public class DestB
                                    {
                                        public DestA Owner { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestA>();
                                            var downstreamMap = CreateMap<SourceB, DestB>();
                                            downstreamMap.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenDeferredCycleBreakerAppliesOnlyAfterReverseMap()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Owner { get; set; }
                                    }

                                    public class DestA
                                    {
                                        public DestB Child { get; set; }
                                    }

                                    public class DestB
                                    {
                                        public DestA Owner { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestA>();
                                            var downstreamMap = CreateMap<SourceB, DestB>();
                                            downstreamMap.ReverseMap().PreserveReferences();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule, 29, 13, "SourceA", "DestA")
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule, 30, 33, "SourceB", "DestB")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenDownstreamCycleMapIsConstrainedOnlyAfterReverseMap()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Owner { get; set; }
                                    }

                                    public class DestA
                                    {
                                        public DestB Child { get; set; }
                                    }

                                    public class DestB
                                    {
                                        public DestA Owner { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestA>();
                                            CreateMap<SourceB, DestB>()
                                                .ReverseMap()
                                                .PreserveReferences();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule, 29, 13, "SourceA", "DestA")
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule, 30, 13, "SourceB", "DestB")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenReverseDownstreamCycleMapIsConstrainedAfterReverseMap()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Owner { get; set; }
                                    }

                                    public class DestA
                                    {
                                        public DestB Child { get; set; }
                                    }

                                    public class DestB
                                    {
                                        public DestA Owner { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<DestA, SourceA>();
                                            CreateMap<SourceB, DestB>()
                                                .ReverseMap()
                                                .PreserveReferences();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenDownstreamCycleBreakerIsNotAutoMapperMethod()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Owner { get; set; }
                                    }

                                    public class DestA
                                    {
                                        public DestB Child { get; set; }
                                    }

                                    public class DestB
                                    {
                                        public DestA Owner { get; set; }
                                    }

                                    public sealed class CustomMappingExpression
                                    {
                                        public CustomMappingExpression PreserveReferences() => this;
                                    }

                                    public static class CustomMappingExtensions
                                    {
                                        public static CustomMappingExpression Custom<TSource, TDestination>(
                                            this IMappingExpression<TSource, TDestination> mapping) =>
                                            new();
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestA>();
                                            CreateMap<SourceB, DestB>()
                                                .Custom()
                                                .PreserveReferences();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule, 41, 13, "SourceA", "DestA")
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule, 42, 13, "SourceB", "DestB")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenOnlyOneDuplicateDownstreamMapIsConstrained()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Owner { get; set; }
                                    }

                                    public class DestA
                                    {
                                        public DestB Child { get; set; }
                                    }

                                    public class DestB
                                    {
                                        public DestA Owner { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestA>();
                                            CreateMap<SourceB, DestB>()
                                                .PreserveReferences();
                                            CreateMap<SourceB, DestB>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule, 29, 13, "SourceA", "DestA")
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule, 32, 13, "SourceB", "DestB")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenOnlyLastDuplicateDownstreamMapIsConstrained()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Owner { get; set; }
                                    }

                                    public class DestA
                                    {
                                        public DestB Child { get; set; }
                                    }

                                    public class DestB
                                    {
                                        public DestA Owner { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestA>();
                                            CreateMap<SourceB, DestB>();
                                            CreateMap<SourceB, DestB>()
                                                .PreserveReferences();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule, 29, 13, "SourceA", "DestA")
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule, 30, 13, "SourceB", "DestB")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportRootDiagnostic_WhenDirectForMemberMapFromClosesConfiguredCycle()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceParent
                                    {
                                        public SourceChild Child { get; set; }
                                    }

                                    public class SourceChild
                                    {
                                        public SourceParent Parent { get; set; }
                                    }

                                    public class DestinationParent
                                    {
                                        public DestinationChild RenamedChild { get; set; }
                                    }

                                    public class DestinationChild
                                    {
                                        public DestinationParent Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceParent, DestinationParent>()
                                                .ForMember(destination => destination.RenamedChild, options => options.MapFrom(source => source.Child));
                                            CreateMap<SourceChild, DestinationChild>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule,
                29,
                13,
                "SourceParent",
                "DestinationParent")
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule,
                31,
                13,
                "SourceChild",
                "DestinationChild")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenDirectForMemberCycleNestedMapIsMissing()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceParent
                                    {
                                        public SourceChild Child { get; set; }
                                    }

                                    public class SourceChild
                                    {
                                        public SourceParent Parent { get; set; }
                                    }

                                    public class DestinationParent
                                    {
                                        public DestinationChild RenamedChild { get; set; }
                                    }

                                    public class DestinationChild
                                    {
                                        public DestinationParent Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceParent, DestinationParent>()
                                                .ForMember(destination => destination.RenamedChild, options => options.MapFrom(source => source.Child));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Theory]
    [InlineData(".MaxDepth(2)")]
    [InlineData(".PreserveReferences()")]
    [InlineData(".ConvertUsing((SourceChild source) => new DestinationChild())")]
    public async Task AM022_ShouldNotReportDiagnostic_WhenDirectForMemberCycleNestedMapIsConstrained(
        string cycleBreaker)
    {
        string testCode = $$"""
                            using AutoMapper;

                            namespace TestNamespace
                            {
                                public class SourceParent
                                {
                                    public SourceChild Child { get; set; }
                                }

                                public class SourceChild
                                {
                                    public SourceParent Parent { get; set; }
                                }

                                public class DestinationParent
                                {
                                    public DestinationChild RenamedChild { get; set; }
                                }

                                public class DestinationChild
                                {
                                    public DestinationParent Parent { get; set; }
                                }

                                public class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<SourceParent, DestinationParent>()
                                            .ForMember(destination => destination.RenamedChild, options => options.MapFrom(source => source.Child));
                                        CreateMap<SourceChild, DestinationChild>(){{cycleBreaker}};
                                    }
                                }
                            }
                            """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenMapFromExpressionIsTransformed()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceParent
                                    {
                                        public SourceChild Child { get; set; }
                                    }

                                    public class SourceChild
                                    {
                                        public SourceParent Parent { get; set; }
                                    }

                                    public class DestinationParent
                                    {
                                        public DestinationChild RenamedChild { get; set; }
                                    }

                                    public class DestinationChild
                                    {
                                        public DestinationParent Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceParent, DestinationParent>()
                                                .ForMember(destination => destination.RenamedChild, options =>
                                                    options.MapFrom(source => SelectChild(source.Child)));
                                            CreateMap<SourceChild, DestinationChild>();
                                        }

                                        private static SourceChild SelectChild(SourceChild child) => child;
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenDestinationMemberHasDuplicateConfiguration()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceParent
                                    {
                                        public SourceChild Child { get; set; }
                                    }

                                    public class SourceChild
                                    {
                                        public SourceParent Parent { get; set; }
                                    }

                                    public class DestinationParent
                                    {
                                        public DestinationChild RenamedChild { get; set; }
                                    }

                                    public class DestinationChild
                                    {
                                        public DestinationParent Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceParent, DestinationParent>()
                                                .ForMember(destination => destination.RenamedChild, options => options.MapFrom(source => source.Child))
                                                .ForMember(destination => destination.RenamedChild, options => options.MapFrom(source => source.Child));
                                            CreateMap<SourceChild, DestinationChild>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenMapFromIsNotAutoMapperOwned()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class SourceParent
                                    {
                                        public SourceChild Child { get; set; }
                                    }

                                    public class SourceChild
                                    {
                                        public SourceParent Parent { get; set; }
                                    }

                                    public class DestinationParent
                                    {
                                        public DestinationChild RenamedChild { get; set; }
                                    }

                                    public class DestinationChild
                                    {
                                        public DestinationParent Parent { get; set; }
                                    }

                                    public sealed class CustomMemberConfiguration
                                    {
                                        public void MapFrom<TSource>(Func<TSource, SourceChild> selector)
                                        {
                                        }
                                    }

                                    public static class CustomMemberExtensions
                                    {
                                        public static CustomMemberConfiguration Custom<TSource, TDestination, TMember>(
                                            this IMemberConfigurationExpression<TSource, TDestination, TMember> options) => new();
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceParent, DestinationParent>()
                                                .ForMember(
                                                    destination => destination.RenamedChild,
                                                    options => options.Custom().MapFrom((SourceParent source) => source.Child));
                                            CreateMap<SourceChild, DestinationChild>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportBothDiagnostics_WhenCycleUsesTwoDirectRenamedMemberMaps()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Parent { get; set; }
                                    }

                                    public class DestinationA
                                    {
                                        public DestinationB RenamedChild { get; set; }
                                    }

                                    public class DestinationB
                                    {
                                        public DestinationA RenamedParent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestinationA>()
                                                .ForMember(destination => destination.RenamedChild, options => options.MapFrom(source => source.Child));
                                            CreateMap<SourceB, DestinationB>()
                                                .ForMember(destination => destination.RenamedParent, options => options.MapFrom(source => source.Parent));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule,
                29,
                13,
                "SourceA",
                "DestinationA")
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule,
                31,
                13,
                "SourceB",
                "DestinationB")
            .RunAsync();
    }


    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenDownstreamDirectRenamedMemberIsIgnoredWithForPath()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Parent { get; set; }
                                    }

                                    public class DestinationA
                                    {
                                        public DestinationB RenamedChild { get; set; }
                                    }

                                    public class DestinationB
                                    {
                                        public DestinationA RenamedParent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestinationA>()
                                                .ForMember(destination => destination.RenamedChild, options => options.MapFrom(source => source.Child))
                                                .ForPath(destination => destination.RenamedChild, options => options.Ignore());
                                            CreateMap<SourceB, DestinationB>()
                                                .ForMember(destination => destination.RenamedParent, options => options.MapFrom(source => source.Parent));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportBothDiagnostics_WhenNestedForPathIgnoreDoesNotOwnDirectRenamedMember()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Parent { get; set; }
                                    }

                                    public class DestinationA
                                    {
                                        public DestinationB RenamedChild { get; set; }
                                    }

                                    public class DestinationB
                                    {
                                        public DestinationA RenamedParent { get; set; }
                                        public string Label { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestinationA>()
                                                .ForMember(destination => destination.RenamedChild, options => options.MapFrom(source => source.Child))
                                                .ForPath(destination => destination.RenamedChild.Label, options => options.Ignore());
                                            CreateMap<SourceB, DestinationB>()
                                                .ForMember(destination => destination.RenamedParent, options => options.MapFrom(source => source.Parent));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule,
                30,
                13,
                "SourceA",
                "DestinationA")
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule,
                33,
                13,
                "SourceB",
                "DestinationB")
            .RunAsync();
    }

    [Theory]
    [InlineData(".MaxDepth(2)")]
    [InlineData(".PreserveReferences()")]
    [InlineData(".ConvertUsing((SourceB source) => new DestinationB())")]
    public async Task AM022_ShouldNotReportDiagnostic_WhenDownstreamDirectRenamedMapIsConstrained(
        string cycleBreaker)
    {
        string testCode = $$"""
                            using AutoMapper;

                            namespace TestNamespace
                            {
                                public class SourceA
                                {
                                    public SourceB Child { get; set; }
                                }

                                public class SourceB
                                {
                                    public SourceA Parent { get; set; }
                                }

                                public class DestinationA
                                {
                                    public DestinationB RenamedChild { get; set; }
                                }

                                public class DestinationB
                                {
                                    public DestinationA RenamedParent { get; set; }
                                }

                                public class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<SourceA, DestinationA>()
                                            .ForMember(destination => destination.RenamedChild, options => options.MapFrom(source => source.Child));
                                        CreateMap<SourceB, DestinationB>()
                                            .ForMember(destination => destination.RenamedParent, options => options.MapFrom(source => source.Parent)){{cycleBreaker}};
                                    }
                                }
                            }
                            """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenDownstreamDirectRenamedMapIsDuplicated()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Parent { get; set; }
                                    }

                                    public class DestinationA
                                    {
                                        public DestinationB RenamedChild { get; set; }
                                    }

                                    public class DestinationB
                                    {
                                        public DestinationA RenamedParent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestinationA>()
                                                .ForMember(destination => destination.RenamedChild, options => options.MapFrom(source => source.Child));
                                            CreateMap<SourceB, DestinationB>()
                                                .ForMember(destination => destination.RenamedParent, options => options.MapFrom(source => source.Parent));
                                            CreateMap<SourceB, DestinationB>()
                                                .ForMember(destination => destination.RenamedParent, options => options.MapFrom(source => source.Parent));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenDownstreamMapFromExpressionIsTransformed()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Parent { get; set; }
                                    }

                                    public class DestinationA
                                    {
                                        public DestinationB RenamedChild { get; set; }
                                    }

                                    public class DestinationB
                                    {
                                        public DestinationA RenamedParent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestinationA>()
                                                .ForMember(destination => destination.RenamedChild, options => options.MapFrom(source => source.Child));
                                            CreateMap<SourceB, DestinationB>()
                                                .ForMember(destination => destination.RenamedParent, options =>
                                                    options.MapFrom(source => SelectParent(source.Parent)));
                                        }

                                        private static SourceA SelectParent(SourceA parent) => parent;
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotInferDownstreamMembersFromReverseMapConfiguration()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Parent { get; set; }
                                    }

                                    public class DestinationA
                                    {
                                        public DestinationB RenamedChild { get; set; }
                                    }

                                    public class DestinationB
                                    {
                                        public DestinationA RenamedParent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestinationA>()
                                                .ForMember(destination => destination.RenamedChild, options => options.MapFrom(source => source.Child));
                                            CreateMap<DestinationB, SourceB>()
                                                .ReverseMap()
                                                .ForMember(destination => destination.RenamedParent, options => options.MapFrom(source => source.Parent));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenForCtorParamOwnsRenamedSelfReference()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Ancestor { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode(DestinationNode parentNode)
                                        {
                                            Parent = parentNode;
                                        }

                                        public DestinationNode Parent { get; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestinationNode>()
                                                .ForCtorParam("parentNode", options =>
                                                    options.MapFrom(source => source.Ancestor));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                24,
                13,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldStillReportDiagnostic_WhenForMemberIgnoreCannotBreakConstructorOwnedCycle()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Ancestor { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode(DestinationNode parentNode)
                                        {
                                            Parent = parentNode;
                                        }

                                        public DestinationNode Parent { get; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestinationNode>()
                                                .ForCtorParam("parentNode", options =>
                                                    options.MapFrom(source => source.Ancestor))
                                                .ForMember(destination => destination.Parent, options => options.Ignore());
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                24,
                13,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldStillReportDiagnostic_WhenWritableConstructorOwnedCycleIsIgnoredAfterConstruction()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Ancestor { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode(DestinationNode parentNode)
                                        {
                                            Parent = parentNode;
                                        }

                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestinationNode>()
                                                .ForCtorParam("parentNode", options =>
                                                    options.MapFrom(source => source.Ancestor))
                                                .ForMember(destination => destination.Parent, options => options.Ignore());
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                24,
                13,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenForCtorParamTransformsTheSourceValue()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Ancestor { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode(DestinationNode parentNode)
                                        {
                                            Parent = parentNode;
                                        }

                                        public DestinationNode Parent { get; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestinationNode>()
                                                .ForCtorParam("parentNode", options =>
                                                    options.MapFrom(source => SelectAncestor(source.Ancestor)));
                                        }

                                        private static SourceNode SelectAncestor(SourceNode node) => node;
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenForCtorParamCycleHasIneffectiveMaxDepth()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Ancestor { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode(DestinationNode parentNode)
                                        {
                                            Parent = parentNode;
                                        }

                                        public DestinationNode Parent { get; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestinationNode>()
                                                .ForCtorParam("parentNode", options =>
                                                    options.MapFrom(source => source.Ancestor))
                                                .MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                24,
                13,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenWritableForCtorParamCycleHasIneffectiveMaxDepth()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Ancestor { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode(DestinationNode parentNode)
                                        {
                                            Parent = parentNode;
                                        }

                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestinationNode>()
                                                .ForCtorParam("parentNode", options =>
                                                    options.MapFrom(source => source.Ancestor))
                                                .MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                24,
                13,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenForCtorParamCycleHasIneffectivePreserveReferences()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Ancestor { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode(DestinationNode parentNode)
                                        {
                                            Parent = parentNode;
                                        }

                                        public DestinationNode Parent { get; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestinationNode>()
                                                .ForCtorParam("parentNode", options =>
                                                    options.MapFrom(source => source.Ancestor))
                                                .PreserveReferences();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                24,
                13,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenWritableForCtorParamCycleHasIneffectivePreserveReferences()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Ancestor { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode(DestinationNode parentNode)
                                        {
                                            Parent = parentNode;
                                        }

                                        public DestinationNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestinationNode>()
                                                .ForCtorParam("parentNode", options =>
                                                    options.MapFrom(source => source.Ancestor))
                                                .PreserveReferences();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                24,
                13,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenConvertUsingOwnsForCtorParamCycle()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Ancestor { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode(DestinationNode parentNode)
                                        {
                                            Parent = parentNode;
                                        }

                                        public DestinationNode Parent { get; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestinationNode>()
                                                .ForCtorParam("parentNode", options =>
                                                    options.MapFrom(source => source.Ancestor))
                                                .ConvertUsing(source => new DestinationNode(null));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenForCtorParamOwnershipIsAmbiguous()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Ancestor { get; set; }
                                        public SourceNode OtherAncestor { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode(DestinationNode parentNode)
                                        {
                                            Parent = parentNode;
                                        }

                                        public DestinationNode Parent { get; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestinationNode>()
                                                .ForCtorParam("parentNode", options =>
                                                    options.MapFrom(source => source.Ancestor))
                                                .ForCtorParam("parentNode", options =>
                                                    options.MapFrom(source => source.OtherAncestor));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenForCtorParamMapFromIsInUninvokedLocalHelper()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Ancestor { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode(DestinationNode parentNode)
                                        {
                                            Parent = parentNode;
                                        }

                                        public DestinationNode Parent { get; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestinationNode>()
                                                .ForCtorParam("parentNode", options =>
                                                {
                                                    void Configure() =>
                                                        options.MapFrom(source => source.Ancestor);
                                                });
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenForCtorParamNameIsNotExact()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public SourceNode Ancestor { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode(DestinationNode parentNode)
                                        {
                                            Parent = parentNode;
                                        }

                                        public DestinationNode Parent { get; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestinationNode>()
                                                .ForCtorParam("parentNode.Path", options =>
                                                    options.MapFrom(source => source.Ancestor));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostics_WhenDownstreamCycleUsesForCtorParamEdge()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Parent { get; set; }
                                    }

                                    public class DestinationA
                                    {
                                        public DestinationB Child { get; set; }
                                    }

                                    public class DestinationB
                                    {
                                        public DestinationB(DestinationA parentNode)
                                        {
                                            RenamedParent = parentNode;
                                        }

                                        public DestinationA RenamedParent { get; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestinationA>();
                                            CreateMap<SourceB, DestinationB>()
                                                .ForCtorParam("parentNode", options =>
                                                    options.MapFrom(source => source.Parent));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule,
                34,
                13,
                "SourceA",
                "DestinationA")
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule,
                35,
                13,
                "SourceB",
                "DestinationB")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostics_WhenDownstreamMaxDepthBreaksMixedMemberConstructorCycle()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Parent { get; set; }
                                    }

                                    public class DestinationA
                                    {
                                        public DestinationB Child { get; set; }
                                    }

                                    public class DestinationB
                                    {
                                        public DestinationB(DestinationA parentNode)
                                        {
                                            RenamedParent = parentNode;
                                        }

                                        public DestinationA RenamedParent { get; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestinationA>();
                                            CreateMap<SourceB, DestinationB>()
                                                .ForCtorParam("parentNode", options =>
                                                    options.MapFrom(source => source.Parent))
                                                .MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenMaxDepthBreaksMixedConstructorMemberCycle()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Parent { get; set; }
                                    }

                                    public class DestinationA
                                    {
                                        public DestinationA(DestinationB childNode)
                                        {
                                            Child = childNode;
                                        }

                                        public DestinationB Child { get; }
                                    }

                                    public class DestinationB
                                    {
                                        public DestinationA Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestinationA>()
                                                .ForCtorParam("childNode", options =>
                                                    options.MapFrom(source => source.Child))
                                                .MaxDepth(2);
                                            CreateMap<SourceB, DestinationB>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostics_WhenPreserveReferencesCannotBreakMixedConstructorMemberCycle()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Parent { get; set; }
                                    }

                                    public class DestinationA
                                    {
                                        public DestinationA(DestinationB childNode)
                                        {
                                            Child = childNode;
                                        }

                                        public DestinationB Child { get; }
                                    }

                                    public class DestinationB
                                    {
                                        public DestinationA Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestinationA>()
                                                .ForCtorParam("childNode", options =>
                                                    options.MapFrom(source => source.Child))
                                                .PreserveReferences();
                                            CreateMap<SourceB, DestinationB>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule,
                34,
                13,
                "SourceA",
                "DestinationA")
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule,
                38,
                13,
                "SourceB",
                "DestinationB")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostics_WhenMaxDepthCannotBreakConstructorOnlyCycle()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Parent { get; set; }
                                    }

                                    public class DestinationA
                                    {
                                        public DestinationA(DestinationB childNode)
                                        {
                                            Child = childNode;
                                        }

                                        public DestinationB Child { get; }
                                    }

                                    public class DestinationB
                                    {
                                        public DestinationB(DestinationA parentNode)
                                        {
                                            Parent = parentNode;
                                        }

                                        public DestinationA Parent { get; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestinationA>()
                                                .ForCtorParam("childNode", options =>
                                                    options.MapFrom(source => source.Child))
                                                .MaxDepth(2);
                                            CreateMap<SourceB, DestinationB>()
                                                .ForCtorParam("parentNode", options =>
                                                    options.MapFrom(source => source.Parent));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule,
                39,
                13,
                "SourceA",
                "DestinationA")
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule,
                43,
                13,
                "SourceB",
                "DestinationB")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenConstructorOnlyCycleReachesConvertUsing()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Parent { get; set; }
                                    }

                                    public class DestinationA
                                    {
                                        public DestinationA(DestinationB childNode)
                                        {
                                            Child = childNode;
                                        }

                                        public DestinationB Child { get; }
                                    }

                                    public class DestinationB
                                    {
                                        public DestinationB(DestinationA parentNode)
                                        {
                                            Parent = parentNode;
                                        }

                                        public DestinationA Parent { get; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestinationA>()
                                                .ForCtorParam("childNode", options =>
                                                    options.MapFrom(source => source.Child))
                                                .MaxDepth(2);
                                            CreateMap<SourceB, DestinationB>()
                                                .ForCtorParam("parentNode", options =>
                                                    options.MapFrom(source => source.Parent))
                                                .ConvertUsing(source => new DestinationB(null));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenConstructorOnlyDownstreamDirectionIsAmbiguous()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Parent { get; set; }
                                    }

                                    public class DestinationA
                                    {
                                        public DestinationA(DestinationB childNode)
                                        {
                                            Child = childNode;
                                        }

                                        public DestinationB Child { get; }
                                    }

                                    public class DestinationB
                                    {
                                        public DestinationB(DestinationA parentNode)
                                        {
                                            Parent = parentNode;
                                        }

                                        public DestinationA Parent { get; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestinationA>()
                                                .ForCtorParam("childNode", options =>
                                                    options.MapFrom(source => source.Child))
                                                .MaxDepth(2);
                                            CreateMap<SourceB, DestinationB>()
                                                .ForCtorParam("parentNode", options =>
                                                    options.MapFrom(source => source.Parent));
                                            CreateMap<SourceB, DestinationB>()
                                                .ForCtorParam("parentNode", options =>
                                                    options.MapFrom(source => source.Parent));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenConstructorTraversalEndsAtScalarParameter()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public string Label { get; set; }
                                    }

                                    public class DestinationA
                                    {
                                        public DestinationA(DestinationB childNode)
                                        {
                                            Child = childNode;
                                        }

                                        public DestinationB Child { get; }
                                    }

                                    public class DestinationB
                                    {
                                        public DestinationB(string label)
                                        {
                                            Label = label;
                                        }

                                        public string Label { get; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestinationA>()
                                                .ForCtorParam("childNode", options =>
                                                    options.MapFrom(source => source.Child))
                                                .MaxDepth(2);
                                            CreateMap<SourceB, DestinationB>()
                                                .ForCtorParam("label", options =>
                                                    options.MapFrom(source => source.Label));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenPositionalRecordOwnsForCtorParamCycle()
    {
        const string testCode = """
                                using System.Collections.Generic;
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public IReadOnlyList<SourceNode> Ancestors { get; set; }
                                    }

                                    public record DestinationNode(IReadOnlyList<DestinationNode> Parents);

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestinationNode>()
                                                .ForCtorParam("Parents", options =>
                                                    options.MapFrom(source => source.Ancestors))
                                                .MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                17,
                13,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenForMemberOverridesWritableConstructorOwnedProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceSeed
                                    {
                                    }

                                    public class SourceNode
                                    {
                                        public SourceSeed SeedChild { get; set; }
                                        public SourceNode RecursiveChild { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public DestinationNode(DestinationNode childNode)
                                        {
                                            Child = childNode;
                                        }

                                        public DestinationNode Child { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceSeed, DestinationNode>()
                                                .ConvertUsing(_ => new DestinationNode(null));
                                            CreateMap<SourceNode, DestinationNode>()
                                                .ForCtorParam("childNode", options =>
                                                    options.MapFrom(source => source.SeedChild))
                                                .ForMember(
                                                    destination => destination.Child,
                                                    options => options.MapFrom(source => source.RecursiveChild));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                31,
                13,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldNotReportDiagnostic_WhenDeferredConvertUsingOwnsConstrainedConstructorMap()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public SourceB Child { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public SourceA Owner { get; set; }
                                    }

                                    public class DestinationA
                                    {
                                        public DestinationB Child { get; set; }
                                    }

                                    public class DestinationB
                                    {
                                        public DestinationB(DestinationA ownerNode)
                                        {
                                            Owner = ownerNode;
                                        }

                                        public DestinationA Owner { get; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceA, DestinationA>();
                                            var downstreamMap = CreateMap<SourceB, DestinationB>()
                                                .ForCtorParam("ownerNode", options =>
                                                    options.MapFrom(source => source.Owner))
                                                .PreserveReferences();
                                            downstreamMap.ConvertUsing(source => new DestinationB(null));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM022_ShouldReportDiagnostic_WhenInterveningAutoMapperCallbackUsesMutatingMethodGroup()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public string Name { get; set; }
                                        public SourceNode Child { get; set; }
                                    }

                                    public class DestinationNode
                                    {
                                        public string Name { get; set; }
                                        public DestinationNode Child { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            IMappingExpression<SourceNode, DestinationNode> externalMapping = null;
                                            var mapping = CreateMap<SourceNode, DestinationNode>();

                                            void Configure(
                                                IMemberConfigurationExpression<SourceNode, DestinationNode, string> options)
                                            {
                                                mapping = externalMapping;
                                                options.Ignore();
                                            }

                                            mapping.ForMember(destination => destination.Name, Configure);
                                            mapping.MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM022_InfiniteRecursionAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule,
                22,
                27,
                "SourceNode",
                "DestinationNode")
            .RunAsync();
    }
}

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
}

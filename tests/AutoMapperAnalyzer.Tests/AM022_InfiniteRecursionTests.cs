using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests;

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
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule, 21, 13, "SourcePerson", "DestPerson")
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
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule, 21, 13, "SourcePerson", "DestPerson")
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
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule, 24, 13, "SourceCategory", "DestCategory")
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
            .ExpectDiagnostic(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule, 38, 13, "SourceContainer", "DestContainer")
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
}
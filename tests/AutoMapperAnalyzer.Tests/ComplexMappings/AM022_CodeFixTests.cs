using AutoMapperAnalyzer.Analyzers.ComplexMappings;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.ComplexMappings;

public class AM022_CodeFixTests
{
    [Fact]
    public async Task AM022_ShouldAddMaxDepth_ForMultipleSelfReferencingProperties()
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

        const string expectedFixedCode = """
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
                                                     CreateMap<SourceNode, DestNode>().MaxDepth(2);
                                                 }
                                             }
                                         }
                                         """;
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(25, 13)
                    .WithArguments("SourceNode", "DestNode"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM022_ShouldIgnoreSelfReferencingProperty()
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

        const string expectedFixedCode = """
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
                                                     CreateMap<SourcePerson, DestPerson>().ForMember(dest => dest.Friend, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(21, 13)
                    .WithArguments("SourcePerson", "DestPerson"),
                expectedFixedCode, 1);
    }

    [Fact]
    public async Task AM022_ShouldAddMaxDepth_ForCollectionAndDirectSelfReferences()
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

        const string expectedFixedCode = """
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
                                                     CreateMap<SourceCategory, DestCategory>().MaxDepth(2);
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(24, 13)
                    .WithArguments("SourceCategory", "DestCategory"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM022_ShouldAddMaxDepth_ForTwoTypeCircularReferenceRisk()
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

                                    public class DestParent
                                    {
                                        public DestChild Child { get; set; }
                                    }

                                    public class DestChild
                                    {
                                        public DestParent Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceParent, DestParent>();
                                            CreateMap<SourceChild, DestChild>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
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

                                             public class DestParent
                                             {
                                                 public DestChild Child { get; set; }
                                             }

                                             public class DestChild
                                             {
                                                 public DestParent Parent { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<SourceParent, DestParent>().MaxDepth(2);
                                                     CreateMap<SourceChild, DestChild>();
                                                 }
                                             }
                                         }
                                         """;

        string expectedBatchFixedCode = expectedFixedCode.Replace(
            "CreateMap<SourceChild, DestChild>();",
            "CreateMap<SourceChild, DestChild>().MaxDepth(2);");

        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[]
                {
                    new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule)
                        .WithLocation(29, 13)
                        .WithArguments("SourceParent", "DestParent"),
                    new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule)
                        .WithLocation(30, 13)
                        .WithArguments("SourceChild", "DestChild")
                },
                expectedFixedCode,
                codeActionIndex: null,
                incrementalIterations: 1,
                fixAllIterations: 1,
                remainingDiagnostics: null,
                batchFixedSource: expectedBatchFixedCode);
    }

    [Fact]
    public async Task AM022_ShouldAppendMaxDepth_WhenExistingForMemberChain()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public string Name { get; set; }
                                        public string DisplayName { get; set; }
                                        public SourceNode Parent { get; set; }
                                    }

                                    public class DestNode
                                    {
                                        public string Name { get; set; }
                                        public string Label { get; set; }
                                        public DestNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestNode>()
                                                .ForMember(dest => dest.Label, opt => opt.MapFrom(src => src.DisplayName));
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class SourceNode
                                             {
                                                 public string Name { get; set; }
                                                 public string DisplayName { get; set; }
                                                 public SourceNode Parent { get; set; }
                                             }

                                             public class DestNode
                                             {
                                                 public string Name { get; set; }
                                                 public string Label { get; set; }
                                                 public DestNode Parent { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<SourceNode, DestNode>()
                                         .MaxDepth(2).ForMember(dest => dest.Label, opt => opt.MapFrom(src => src.DisplayName));
                                                 }
                                             }
                                         }
                                         """;
        // Index 0: MaxDepth first (Ignore second)
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(23, 13)
                    .WithArguments("SourceNode", "DestNode"),
                expectedFixedCode,
                0);
    }

    [Fact]
    public async Task AM022_ShouldOfferMultipleCodeActions_MaxDepthAndIgnore()
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

        const string expectedFixedCodeIgnore = """
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
                                                            CreateMap<SourcePerson, DestPerson>().ForMember(dest => dest.Friend, opt => opt.Ignore());
                                                        }
                                                    }
                                                }
                                                """;

        const string expectedFixedCodeMaxDepth = """
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
                                                              CreateMap<SourcePerson, DestPerson>().MaxDepth(2);
                                                          }
                                                      }
                                                  }
                                                  """;

        // Index 0: MaxDepth (best-first)
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(21, 13)
                    .WithArguments("SourcePerson", "DestPerson"),
                expectedFixedCodeMaxDepth,
                0);

        // Index 1: Ignore
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(21, 13)
                    .WithArguments("SourcePerson", "DestPerson"),
                expectedFixedCodeIgnore,
                1);
    }

    [Fact]
    public async Task AM022_ShouldHandleSelfReferenceInGenericCollection()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class SourceNode<T>
                                    {
                                        public T Value { get; set; }
                                        public List<SourceNode<T>> Children { get; set; }
                                    }

                                    public class DestNode<T>
                                    {
                                        public T Value { get; set; }
                                        public List<DestNode<T>> Children { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode<int>, DestNode<int>>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System.Collections.Generic;

                                         namespace TestNamespace
                                         {
                                             public class SourceNode<T>
                                             {
                                                 public T Value { get; set; }
                                                 public List<SourceNode<T>> Children { get; set; }
                                             }

                                             public class DestNode<T>
                                             {
                                                 public T Value { get; set; }
                                                 public List<DestNode<T>> Children { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<SourceNode<int>, DestNode<int>>().MaxDepth(2);
                                                 }
                                             }
                                         }
                                         """;

        // Analyzer reports base type name without generic parameters
        // Index 0: MaxDepth first (Ignore second)
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(22, 13)
                    .WithArguments("SourceNode", "DestNode"),
                expectedFixedCode,
                0);
    }

    [Fact]
    public async Task AM022_ShouldHandleCircularReferenceChain_ThreeTypes()
    {
        // Simplified test: Just validate that circular reference in A→B→C→A is detected
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public string Name { get; set; }
                                        public SourceB BReference { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public string Data { get; set; }
                                        public SourceC CReference { get; set; }
                                    }

                                    public class SourceC
                                    {
                                        public int Value { get; set; }
                                        public SourceA AReference { get; set; }
                                    }

                                    public class DestA
                                    {
                                        public string Name { get; set; }
                                        public DestB BReference { get; set; }
                                    }

                                    public class DestB
                                    {
                                        public string Data { get; set; }
                                        public DestC CReference { get; set; }
                                    }

                                    public class DestC
                                    {
                                        public int Value { get; set; }
                                        public DestA AReference { get; set; }
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

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class SourceA
                                             {
                                                 public string Name { get; set; }
                                                 public SourceB BReference { get; set; }
                                             }

                                             public class SourceB
                                             {
                                                 public string Data { get; set; }
                                                 public SourceC CReference { get; set; }
                                             }

                                             public class SourceC
                                             {
                                                 public int Value { get; set; }
                                                 public SourceA AReference { get; set; }
                                             }

                                             public class DestA
                                             {
                                                 public string Name { get; set; }
                                                 public DestB BReference { get; set; }
                                             }

                                             public class DestB
                                             {
                                                 public string Data { get; set; }
                                                 public DestC CReference { get; set; }
                                             }

                                             public class DestC
                                             {
                                                 public int Value { get; set; }
                                                 public DestA AReference { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<SourceA, DestA>().MaxDepth(2);
                                                     CreateMap<SourceB, DestB>();
                                                     CreateMap<SourceC, DestC>();
                                                 }
                                             }
                                         }
                                         """;

        string expectedBatchFixedCode = expectedFixedCode
            .Replace(
                "CreateMap<SourceB, DestB>();",
                "CreateMap<SourceB, DestB>().MaxDepth(2);")
            .Replace(
                "CreateMap<SourceC, DestC>();",
                "CreateMap<SourceC, DestC>().MaxDepth(2);");

        // Circular chain A→B→C→A detected as infinite recursion risk
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[]
                {
                    new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule)
                        .WithLocation(45, 13)
                        .WithArguments("SourceA", "DestA"),
                    new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule)
                        .WithLocation(46, 13)
                        .WithArguments("SourceB", "DestB"),
                    new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule)
                        .WithLocation(47, 13)
                        .WithArguments("SourceC", "DestC")
                },
                expectedFixedCode,
                codeActionIndex: null,
                incrementalIterations: 1,
                fixAllIterations: 1,
                remainingDiagnostics: null,
                batchFixedSource: expectedBatchFixedCode);
    }

    [Fact]
    public async Task AM022_ShouldOfferIgnoreForGraphCycleEdge_ThreeTypes()
    {
        // Multi-type A→B→C→A has no destination self-ref; Ignore must use graph-aware edges.
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public string Name { get; set; }
                                        public SourceB BReference { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public string Data { get; set; }
                                        public SourceC CReference { get; set; }
                                    }

                                    public class SourceC
                                    {
                                        public int Value { get; set; }
                                        public SourceA AReference { get; set; }
                                    }

                                    public class DestA
                                    {
                                        public string Name { get; set; }
                                        public DestB BReference { get; set; }
                                    }

                                    public class DestB
                                    {
                                        public string Data { get; set; }
                                        public DestC CReference { get; set; }
                                    }

                                    public class DestC
                                    {
                                        public int Value { get; set; }
                                        public DestA AReference { get; set; }
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

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class SourceA
                                             {
                                                 public string Name { get; set; }
                                                 public SourceB BReference { get; set; }
                                             }

                                             public class SourceB
                                             {
                                                 public string Data { get; set; }
                                                 public SourceC CReference { get; set; }
                                             }

                                             public class SourceC
                                             {
                                                 public int Value { get; set; }
                                                 public SourceA AReference { get; set; }
                                             }

                                             public class DestA
                                             {
                                                 public string Name { get; set; }
                                                 public DestB BReference { get; set; }
                                             }

                                             public class DestB
                                             {
                                                 public string Data { get; set; }
                                                 public DestC CReference { get; set; }
                                             }

                                             public class DestC
                                             {
                                                 public int Value { get; set; }
                                                 public DestA AReference { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<SourceA, DestA>().ForMember(dest => dest.BReference, opt => opt.Ignore());
                                                     CreateMap<SourceB, DestB>();
                                                     CreateMap<SourceC, DestC>();
                                                 }
                                             }
                                         }
                                         """;

        // Index 0 MaxDepth, index 1 Ignore graph edge BReference (not a destination self-ref).
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[]
                {
                    new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule)
                        .WithLocation(45, 13)
                        .WithArguments("SourceA", "DestA"),
                    new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule)
                        .WithLocation(46, 13)
                        .WithArguments("SourceB", "DestB"),
                    new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule)
                        .WithLocation(47, 13)
                        .WithArguments("SourceC", "DestC")
                },
                expectedFixedCode,
                codeActionIndex: 1,
                incrementalIterations: 1,
                fixAllIterations: 1,
                remainingDiagnostics: null);
    }

    [Fact]
    public async Task AM022_ShouldOfferEffectiveIgnoreForDirectForMemberCycleEdge()
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

        const string expectedFixedCode = """
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
                                                         .ForMember(destination => destination.RenamedChild, options => options.MapFrom(source => source.Child)).ForMember(dest => dest.RenamedChild, opt => opt.Ignore());
                                                     CreateMap<SourceChild, DestinationChild>();
                                                 }
                                             }
                                         }
                                         """;

        var expectedDiagnostics = new[]
        {
            new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule)
                .WithLocation(29, 13)
                .WithArguments("SourceParent", "DestinationParent"),
            new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule)
                .WithLocation(31, 13)
                .WithArguments("SourceChild", "DestinationChild")
        };

        // Index 0 remains MaxDepth; index 1 appends Ignore after the existing MapFrom so Ignore wins.
        // Breaking the root edge clears both diagnostics in one application.
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                expectedDiagnostics,
                expectedFixedCode,
                1,
                1);
    }

    [Fact]
    public async Task AM022_ShouldIgnoreAllGraphEdges_WhenSelfRefAndMultiTypeCycleBothPresent()
    {
        // DestA has Parent: DestA (self-ref) and BReference: DestB (multi-type edge).
        // Self-ref-only discovery would Ignore Parent alone and leave the B↔A cycle live.
        // Graph-aware discovery offers Ignore-all of both cycle-breaking edges.
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceA
                                    {
                                        public string Name { get; set; }
                                        public SourceA Parent { get; set; }
                                        public SourceB BReference { get; set; }
                                    }

                                    public class SourceB
                                    {
                                        public string Data { get; set; }
                                        public SourceA AReference { get; set; }
                                    }

                                    public class DestA
                                    {
                                        public string Name { get; set; }
                                        public DestA Parent { get; set; }
                                        public DestB BReference { get; set; }
                                    }

                                    public class DestB
                                    {
                                        public string Data { get; set; }
                                        public DestA AReference { get; set; }
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

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class SourceA
                                             {
                                                 public string Name { get; set; }
                                                 public SourceA Parent { get; set; }
                                                 public SourceB BReference { get; set; }
                                             }

                                             public class SourceB
                                             {
                                                 public string Data { get; set; }
                                                 public SourceA AReference { get; set; }
                                             }

                                             public class DestA
                                             {
                                                 public string Name { get; set; }
                                                 public DestA Parent { get; set; }
                                                 public DestB BReference { get; set; }
                                             }

                                             public class DestB
                                             {
                                                 public string Data { get; set; }
                                                 public DestA AReference { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<SourceA, DestA>().ForMember(dest => dest.BReference, opt => opt.Ignore()).ForMember(dest => dest.Parent, opt => opt.Ignore());
                                                     CreateMap<SourceB, DestB>();
                                                 }
                                             }
                                         }
                                         """;

        // Index 0 MaxDepth; index 1 ignores SourceA's graph edges, which breaks both cycles.
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new[]
                {
                    new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                        .WithLocation(35, 13)
                        .WithArguments("SourceA", "DestA"),
                    new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule)
                        .WithLocation(36, 13)
                        .WithArguments("SourceB", "DestB")
                },
                expectedFixedCode,
                codeActionIndex: 1,
                incrementalIterations: 1,
                fixAllIterations: 1,
                remainingDiagnostics: null);
    }

    [Fact]
    public async Task AM022_ShouldHandleReverseMapWithRecursion()
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
                                        public DestNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestNode>().ReverseMap();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
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
                                                 public DestNode Parent { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<SourceNode, DestNode>().MaxDepth(2).ReverseMap();
                                                 }
                                             }
                                         }
                                         """;

        // Index 0: MaxDepth first (Ignore second)
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(21, 13)
                    .WithArguments("SourceNode", "DestNode"),
                expectedFixedCode,
                0);
    }

    [Fact]
    public async Task AM022_ShouldHandleInterfaceMapping()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public interface ISourceNode
                                    {
                                        string Name { get; set; }
                                        ISourceNode Parent { get; }
                                    }

                                    public interface IDestNode
                                    {
                                        string Name { get; set; }
                                        IDestNode Parent { get; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<ISourceNode, IDestNode>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public interface ISourceNode
                                             {
                                                 string Name { get; set; }
                                                 ISourceNode Parent { get; }
                                             }

                                             public interface IDestNode
                                             {
                                                 string Name { get; set; }
                                                 IDestNode Parent { get; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<ISourceNode, IDestNode>().ForMember(dest => dest.Parent, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(21, 13)
                    .WithArguments("ISourceNode", "IDestNode"),
                expectedFixedCode, 1);
    }

    [Fact]
    public async Task AM022_ShouldHandleNullableSelfReference()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public string Name { get; set; }
                                        public SourceNode? Parent { get; set; }
                                    }

                                    public class DestNode
                                    {
                                        public string Name { get; set; }
                                        public DestNode? Parent { get; set; }
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

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class SourceNode
                                             {
                                                 public string Name { get; set; }
                                                 public SourceNode? Parent { get; set; }
                                             }

                                             public class DestNode
                                             {
                                                 public string Name { get; set; }
                                                 public DestNode? Parent { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<SourceNode, DestNode>().ForMember(dest => dest.Parent, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(21, 13)
                    .WithArguments("SourceNode", "DestNode"),
                expectedFixedCode, 1);
    }

    [Fact]
    public async Task AM022_ShouldHandleAbstractBaseTypes()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public string Name { get; set; }
                                        public List<SourceNode> Children { get; set; }
                                        public SourceNode Sibling { get; set; }
                                    }

                                    public class DestNode
                                    {
                                        public string Name { get; set; }
                                        public List<DestNode> Children { get; set; }
                                        public DestNode Sibling { get; set; }
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

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System.Collections.Generic;

                                         namespace TestNamespace
                                         {
                                             public class SourceNode
                                             {
                                                 public string Name { get; set; }
                                                 public List<SourceNode> Children { get; set; }
                                                 public SourceNode Sibling { get; set; }
                                             }

                                             public class DestNode
                                             {
                                                 public string Name { get; set; }
                                                 public List<DestNode> Children { get; set; }
                                                 public DestNode Sibling { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<SourceNode, DestNode>().MaxDepth(2);
                                                 }
                                             }
                                         }
                                         """;

        // Multiple self-references: MaxDepth is offered first (index 0)
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(24, 13)
                    .WithArguments("SourceNode", "DestNode"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM022_ShouldBeIdempotent_WhenMaxDepthAlreadyExists()
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
                                        public DestNode Parent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode, DestNode>().MaxDepth(2);
                                        }
                                    }
                                }
                                """;

        // No diagnostic should be raised when MaxDepth already exists
        await AnalyzerVerifier<AM022_InfiniteRecursionAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM022_ShouldHandleInternalProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceNode
                                    {
                                        public string Name { get; set; }
                                        internal SourceNode InternalParent { get; set; }
                                    }

                                    public class DestNode
                                    {
                                        public string Name { get; set; }
                                        internal DestNode InternalParent { get; set; }
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

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class SourceNode
                                             {
                                                 public string Name { get; set; }
                                                 internal SourceNode InternalParent { get; set; }
                                             }

                                             public class DestNode
                                             {
                                                 public string Name { get; set; }
                                                 internal DestNode InternalParent { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<SourceNode, DestNode>().ForMember(dest => dest.InternalParent, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        // Internal self-referencing property should still be detected
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(21, 13)
                    .WithArguments("SourceNode", "DestNode"),
                expectedFixedCode, 1);
    }

    [Fact]
    public async Task AM022_ShouldHandleProtectedPropertiesInInheritance()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class BaseNode
                                    {
                                        protected BaseNode ProtectedParent { get; set; }
                                    }

                                    public class SourceNode : BaseNode
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class DestNode : BaseNode
                                    {
                                        public string Name { get; set; }
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

        // Protected properties are not publicly accessible and should not trigger diagnostics
        await AnalyzerVerifier<AM022_InfiniteRecursionAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM022_ShouldHandleGenericTypesWithConstraints()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class SourceNode<T> where T : class
                                    {
                                        public string Name { get; set; }
                                        public List<SourceNode<T>> Children { get; set; }
                                    }

                                    public class DestNode<T> where T : class
                                    {
                                        public string Name { get; set; }
                                        public List<DestNode<T>> Children { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceNode<object>, DestNode<object>>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System.Collections.Generic;

                                         namespace TestNamespace
                                         {
                                             public class SourceNode<T> where T : class
                                             {
                                                 public string Name { get; set; }
                                                 public List<SourceNode<T>> Children { get; set; }
                                             }

                                             public class DestNode<T> where T : class
                                             {
                                                 public string Name { get; set; }
                                                 public List<DestNode<T>> Children { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<SourceNode<object>, DestNode<object>>().MaxDepth(2);
                                                 }
                                             }
                                         }
                                         """;

        // Generic types with constraints should be detected (analyzer reports base type name)
        // Index 0: MaxDepth first (Ignore second)
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(22, 13)
                    .WithArguments("SourceNode", "DestNode"),
                expectedFixedCode,
                0);
    }

    [Fact]
    public async Task AM022_ShouldIgnoreRootMember_WhenCycleUsesTwoDirectRenamedMemberMaps()
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

        const string expectedFixedCode = """
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
                                                         .ForMember(destination => destination.RenamedChild, options => options.MapFrom(source => source.Child)).ForMember(dest => dest.RenamedChild, opt => opt.Ignore());
                                                     CreateMap<SourceB, DestinationB>()
                                                         .ForMember(destination => destination.RenamedParent, options => options.MapFrom(source => source.Parent));
                                                 }
                                             }
                                         }
                                         """;

        var expectedDiagnostics = new[]
        {
            new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule)
                .WithLocation(29, 13)
                .WithArguments("SourceA", "DestinationA"),
            new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule)
                .WithLocation(31, 13)
                .WithArguments("SourceB", "DestinationB")
        };

        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                expectedDiagnostics,
                expectedFixedCode,
                1,
                1);
    }

    [Fact]
    public async Task AM022_ShouldOfferNoAutomaticFix_WhenCycleIsOwnedByForCtorParam()
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
                                                    options.MapFrom(source => source.Ancestor));
                                        }
                                    }
                                }
                                """;

        Document document = AggregateFixTestHarness.CreateDocument(testCode, nameof(AM022_CodeFixTests));
        var diagnostics = await AggregateFixTestHarness
            .GetDiagnosticsAsync<AM022_InfiniteRecursionAnalyzer>(document);
        Diagnostic diagnostic = Assert.Single(diagnostics);

        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions = await CodeFixActionInspector.GetActionsAsync(
            document,
            new AM022_InfiniteRecursionCodeFixProvider(),
            diagnostic);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task AM022_ShouldOfferNoAutomaticFix_WhenConstructorCycleHasIneffectiveMaxDepth()
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

        Document document = AggregateFixTestHarness.CreateDocument(testCode, nameof(AM022_CodeFixTests));
        var diagnostics = await AggregateFixTestHarness
            .GetDiagnosticsAsync<AM022_InfiniteRecursionAnalyzer>(document);
        Diagnostic diagnostic = Assert.Single(diagnostics);

        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions = await CodeFixActionInspector.GetActionsAsync(
            document,
            new AM022_InfiniteRecursionCodeFixProvider(),
            diagnostic);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task AM022_ShouldOfferNoAutomaticFix_WhenPositionalRecordOwnsCycle()
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
                                                    options.MapFrom(source => source.Ancestors));
                                        }
                                    }
                                }
                                """;

        Document document = AggregateFixTestHarness.CreateDocument(testCode, nameof(AM022_CodeFixTests));
        var diagnostics = await AggregateFixTestHarness
            .GetDiagnosticsAsync<AM022_InfiniteRecursionAnalyzer>(document);
        Diagnostic diagnostic = Assert.Single(diagnostics);

        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions = await CodeFixActionInspector.GetActionsAsync(
            document,
            new AM022_InfiniteRecursionCodeFixProvider(),
            diagnostic);

        Assert.Empty(actions);
    }
}

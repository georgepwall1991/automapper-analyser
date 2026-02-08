using AutoMapperAnalyzer.Analyzers.ComplexMappings;
using AutoMapperAnalyzer.Tests.Infrastructure;
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
                expectedFixedCode);
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
    public async Task AM022_ShouldAddMaxDepth_ForCrossTypeCircularReferenceRisk()
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

        const string expectedFixedCode = """
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
                                                     CreateMap<SourceEntity, DestEntity>().MaxDepth(2);
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule)
                    .WithLocation(19, 13)
                    .WithArguments("SourceEntity", "DestEntity"),
                expectedFixedCode);
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

        // Index 1: MaxDepth (for single property, index 0 is Ignore)
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(23, 13)
                    .WithArguments("SourceNode", "DestNode"),
                expectedFixedCode,
                1);
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

        // Index 0: Ignore (for single property)
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(21, 13)
                    .WithArguments("SourcePerson", "DestPerson"),
                expectedFixedCodeIgnore,
                0);

        // Index 1: MaxDepth
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(21, 13)
                    .WithArguments("SourcePerson", "DestPerson"),
                expectedFixedCodeMaxDepth,
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
        // Index 1: MaxDepth (for single property, index 0 is Ignore)
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(22, 13)
                    .WithArguments("SourceNode", "DestNode"),
                expectedFixedCode,
                1);
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
                                                 }
                                             }
                                         }
                                         """;

        // Circular chain A→B→C→A detected as infinite recursion risk
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule)
                    .WithLocation(45, 13)
                    .WithArguments("SourceA", "DestA"),
                expectedFixedCode);
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

        // Index 1: MaxDepth (for single property, index 0 is Ignore)
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(21, 13)
                    .WithArguments("SourceNode", "DestNode"),
                expectedFixedCode,
                1);
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
                expectedFixedCode);
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
                expectedFixedCode);
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
                expectedFixedCode);
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
        // Index 1: MaxDepth (for single property, index 0 is Ignore)
        await CodeFixVerifier<AM022_InfiniteRecursionAnalyzer, AM022_InfiniteRecursionCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule)
                    .WithLocation(22, 13)
                    .WithArguments("SourceNode", "DestNode"),
                expectedFixedCode,
                1);
    }
}

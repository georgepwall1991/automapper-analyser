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
}

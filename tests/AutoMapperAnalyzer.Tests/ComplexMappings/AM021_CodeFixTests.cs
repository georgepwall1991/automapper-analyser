using System.Collections.Immutable;
using System.IO;
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

public class AM021_CodeFixTests
{
    [Fact]
    public async Task AM021_ShouldFixSimpleElementConversion_WithSelect()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Numbers { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<int> Numbers { get; set; }
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
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public List<string> Numbers { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public List<int> Numbers { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Numbers, opt => opt.MapFrom(src => src.Numbers.Select(x => global::System.Convert.ToInt32(x)).ToList()));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(20, 13)
                    .WithArguments("Numbers", "Source", "string", "Destination", "Numbers", "int"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM021_ShouldFixComplexElementMapping_WithNestedMapCall()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class SourcePerson
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class DestPerson
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Source
                                    {
                                        public List<SourcePerson> People { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<DestPerson> People { get; set; }
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
                                         using System.Collections.Generic;

                                         namespace TestNamespace
                                         {
                                             public class SourcePerson
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class DestPerson
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class Source
                                             {
                                                 public List<SourcePerson> People { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public List<DestPerson> People { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>();
                                                     CreateMap<TestNamespace.SourcePerson, TestNamespace.DestPerson>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(30, 13)
                    .WithArguments("People", "Source", "TestNamespace.SourcePerson", "Destination",
                        "People", "TestNamespace.DestPerson"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM021_ShouldFixComplexElementMapping_WhenTypeNamesContainPrimitiveTerms()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class SourceStringWrapper
                                    {
                                        public string Value { get; set; }
                                    }

                                    public class DestStringWrapper
                                    {
                                        public string Value { get; set; }
                                    }

                                    public class Source
                                    {
                                        public List<SourceStringWrapper> Items { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<DestStringWrapper> Items { get; set; }
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
                                         using System.Collections.Generic;

                                         namespace TestNamespace
                                         {
                                             public class SourceStringWrapper
                                             {
                                                 public string Value { get; set; }
                                             }

                                             public class DestStringWrapper
                                             {
                                                 public string Value { get; set; }
                                             }

                                             public class Source
                                             {
                                                 public List<SourceStringWrapper> Items { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public List<DestStringWrapper> Items { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>();
                                                     CreateMap<TestNamespace.SourceStringWrapper, TestNamespace.DestStringWrapper>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(30, 13)
                    .WithArguments("Items", "Source", "TestNamespace.SourceStringWrapper", "Destination",
                        "Items", "TestNamespace.DestStringWrapper"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM021_ShouldFixArrayToArray_WithSelect()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string[] Tags { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int[] Tags { get; set; }
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
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string[] Tags { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public int[] Tags { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags.Select(x => global::System.Convert.ToInt32(x)).ToArray()));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(20, 13)
                .WithArguments("Tags", "Source", "string", "Destination", "Tags", "int"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM021_ShouldFixQueueToQueue_WithSelectWrappedInQueueConstructor()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Queue<string> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Queue<int> Values { get; set; }
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
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public Queue<string> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public Queue<int> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => new Queue<int>(src.Values.Select(x => global::System.Convert.ToInt32(x)))));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(20, 13)
                    .WithArguments("Values", "Source", "string", "Destination", "Values", "int"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM021_ShouldFixStackToStack_WithSelectWrappedInStackConstructor()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Stack<string> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Stack<int> Values { get; set; }
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
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public Stack<string> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public Stack<int> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => new Stack<int>(src.Values.Select(x => global::System.Convert.ToInt32(x)))));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(20, 13)
                    .WithArguments("Values", "Source", "string", "Destination", "Values", "int"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM021_ShouldFixIEnumerableToIEnumerable_WithSelectToListFallback()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public IEnumerable<string> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public IEnumerable<int> Values { get; set; }
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
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public IEnumerable<string> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public IEnumerable<int> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => src.Values.Select(x => global::System.Convert.ToInt32(x)).ToList()));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(20, 13)
                    .WithArguments("Values", "Source", "string", "Destination", "Values", "int"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM021_ShouldFixHashSetToHashSet_WithSelectWrappedInHashSetConstructor()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public HashSet<string> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public HashSet<int> Values { get; set; }
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
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public HashSet<string> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public HashSet<int> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => new global::System.Collections.Generic.HashSet<int>(src.Values.Select(x => global::System.Convert.ToInt32(x)))));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(20, 13)
                    .WithArguments("Values", "Source", "string", "Destination", "Values", "int"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM021_ShouldFixListToISet_WithSelectWrappedInHashSetConstructor()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public ISet<int> Values { get; set; }
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
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public List<string> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public ISet<int> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => new global::System.Collections.Generic.HashSet<int>(src.Values.Select(x => global::System.Convert.ToInt32(x)))));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(20, 13)
                    .WithArguments("Values", "Source", "string", "Destination", "Values", "int"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM021_ShouldFixListToReadOnlySet_WithSelectWrappedInHashSetConstructor()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public IReadOnlySet<int> Values { get; set; }
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
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public List<string> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public IReadOnlySet<int> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => new global::System.Collections.Generic.HashSet<int>(src.Values.Select(x => global::System.Convert.ToInt32(x)))));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(20, 13)
                    .WithArguments("Values", "Source", "string", "Destination", "Values", "int"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM021_ShouldFixListToReadOnlyList_WithSelectAndToList()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public IReadOnlyList<int> Values { get; set; }
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
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public List<string> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public IReadOnlyList<int> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => src.Values.Select(x => global::System.Convert.ToInt32(x)).ToList()));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(20, 13)
                    .WithArguments("Values", "Source", "string", "Destination", "Values", "int"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM021_ShouldFixListToImmutableList_WithSelectAndCreateRange()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Collections.Immutable;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public ImmutableList<int> Values { get; set; }
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
                                         using System.Collections.Generic;
                                         using System.Collections.Immutable;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public List<string> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public ImmutableList<int> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => global::System.Collections.Immutable.ImmutableList.CreateRange(src.Values.Select(x => global::System.Convert.ToInt32(x)))));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(21, 13)
                    .WithArguments("Values", "Source", "string", "Destination", "Values", "int"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM021_ShouldFixListToImmutableArray_WithSelectAndCreateRange()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Collections.Immutable;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public ImmutableArray<int> Values { get; set; }
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
                                         using System.Collections.Generic;
                                         using System.Collections.Immutable;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public List<string> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public ImmutableArray<int> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => global::System.Collections.Immutable.ImmutableArray.CreateRange(src.Values.Select(x => global::System.Convert.ToInt32(x)))));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(21, 13)
                    .WithArguments("Values", "Source", "string", "Destination", "Values", "int"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM021_ShouldFixListToImmutableHashSet_WithSelectAndCreateRange()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Collections.Immutable;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public ImmutableHashSet<int> Values { get; set; }
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
                                         using System.Collections.Generic;
                                         using System.Collections.Immutable;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public List<string> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public ImmutableHashSet<int> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => global::System.Collections.Immutable.ImmutableHashSet.CreateRange(src.Values.Select(x => global::System.Convert.ToInt32(x)))));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(21, 13)
                    .WithArguments("Values", "Source", "string", "Destination", "Values", "int"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM021_ShouldFixListToFrozenSet_WithSelectAndToFrozenSet()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Frozen;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public FrozenSet<int> Values { get; set; }
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
                                         using System.Collections.Frozen;
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public List<string> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public FrozenSet<int> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => global::System.Collections.Frozen.FrozenSet.ToFrozenSet(src.Values.Select(x => global::System.Convert.ToInt32(x)))));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(21, 13)
                    .WithArguments("Values", "Source", "string", "Destination", "Values", "int"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM021_ShouldOfferOnlyManualIgnoreForCustomCollectionNameContainingImmutableList()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class ImmutableListLike<T> : List<T>
                                    {
                                    }

                                    public class Source
                                    {
                                        public List<string> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public ImmutableListLike<int> Values { get; set; }
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

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        CodeAction action = Assert.Single(actions);
        Assert.Equal("Ignore property 'Values' (manual review)", action.Title);
        Assert.Equal("AM021_Ignore_Values", action.EquivalenceKey);
    }

    [Fact]
    public async Task AM021_ShouldOfferToDictionaryFix_WhenDictionaryValueTypesMismatch()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Dictionary<string, int> Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Dictionary<string, string> Data { get; set; }
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

        Document document = CreateDocument(testCode);
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(document);
        Diagnostic diagnostic = Assert.Single(diagnostics);
        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        // Now offers an executable ToDictionary conversion alongside ignore — but never a
        // CreateMap<KeyValuePair<...>, KeyValuePair<...>> (which would not compile).
        Assert.Contains(actions, a => a.EquivalenceKey == "AM021_DictionaryConversion_Data");
        Assert.Contains(actions, a => a.EquivalenceKey == "AM021_Ignore_Data");
        Assert.DoesNotContain(actions, codeAction =>
            codeAction.Title.StartsWith("Add CreateMap<KeyValuePair", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AM021_ShouldFixDictionaryValueConversion_WithToDictionary()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Dictionary<string, int> Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Dictionary<string, string> Data { get; set; }
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
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public Dictionary<string, int> Data { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public Dictionary<string, string> Data { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Data, opt => opt.MapFrom(src => src.Data.ToDictionary(kvp => kvp.Key, kvp => global::System.Convert.ToString(kvp.Value))));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(20, 13)
                    .WithArguments("Data", "Source", "System.Collections.Generic.KeyValuePair<string, int>",
                        "Destination", "Data", "System.Collections.Generic.KeyValuePair<string, string>"),
                expectedFixedCode,
                codeActionIndex: 0);
    }

    [Fact]
    public async Task AM021_ShouldFixDictionaryKeyAndValueConversion_WithToDictionary()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Dictionary<string, int> Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Dictionary<int, string> Data { get; set; }
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
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public Dictionary<string, int> Data { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public Dictionary<int, string> Data { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Data, opt => opt.MapFrom(src => src.Data.ToDictionary(kvp => global::System.Convert.ToInt32(kvp.Key), kvp => global::System.Convert.ToString(kvp.Value))));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(20, 13)
                    .WithArguments("Data", "Source", "System.Collections.Generic.KeyValuePair<string, int>",
                        "Destination", "Data", "System.Collections.Generic.KeyValuePair<int, string>"),
                expectedFixedCode,
                codeActionIndex: 0);
    }

    [Fact]
    public async Task AM021_ShouldOfferCreateMapFix_WhenDictionaryValueIsComplexType()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Foo { public string Name { get; set; } }

                                    public class FooDto { public string Name { get; set; } }

                                    public class Source
                                    {
                                        public Dictionary<int, Foo> Lookup { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Dictionary<int, FooDto> Lookup { get; set; }
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

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));
        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        Assert.Contains(actions, a =>
            a.Title == "Add CreateMap<Foo, FooDto>() for element mapping");
        Assert.Contains(actions, a => a.EquivalenceKey == "AM021_Ignore_Lookup");
        Assert.DoesNotContain(actions, codeAction =>
            codeAction.Title.StartsWith("Add CreateMap<KeyValuePair", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AM021_ShouldOfferOnlyIgnore_WhenBothDictionaryAxesMismatch()
    {
        // Dictionary<string, Foo> -> Dictionary<int, FooDto>: both key (string -> int) and value
        // (Foo -> FooDto) mismatch. A value-only CreateMap<Foo, FooDto> would leave the key unresolved, so
        // the partial fix is withheld and only the manual-review ignore is offered.
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Foo { public string Name { get; set; } }

                                    public class FooDto { public string Name { get; set; } }

                                    public class Source
                                    {
                                        public Dictionary<string, Foo> Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Dictionary<int, FooDto> Data { get; set; }
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

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));
        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        CodeAction action = Assert.Single(actions);
        Assert.Equal("AM021_Ignore_Data", action.EquivalenceKey);
    }

    [Fact]
    public async Task AM021_ShouldOfferOnlyIgnore_WhenDictionaryValueParsesFromNonStringSource()
    {
        // int -> DateTime would emit DateTime.Parse(kvp.Value) where kvp.Value is int, which does not
        // compile (DateTime.Parse only accepts a string). The ToDictionary fix must be withheld.
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Dictionary<string, int> Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Dictionary<string, DateTime> Data { get; set; }
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

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));
        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        CodeAction action = Assert.Single(actions);
        Assert.Equal("AM021_Ignore_Data", action.EquivalenceKey);
    }

    [Fact]
    public async Task AM021_ShouldOfferToDictionaryFix_WhenDictionaryValueParsesFromStringSource()
    {
        // string -> DateTime is a safe Parse conversion, so the ToDictionary fix is still offered.
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Dictionary<string, string> Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Dictionary<string, DateTime> Data { get; set; }
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

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));
        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        Assert.Contains(actions, a => a.EquivalenceKey == "AM021_DictionaryConversion_Data");
    }

    [Fact]
    public async Task AM021_ShouldOfferOnlyIgnore_WhenDictionaryValueIsMismatchedGenericCollection()
    {
        // Dictionary<string, List<int>> -> Dictionary<int, List<string>>: the value axis is a generic
        // collection whose inner elements mismatch. Neither a ToDictionary pass-through (would not compile)
        // nor a CreateMap<List<int>, List<string>> is a valid fix, so only the manual-review ignore is offered.
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Dictionary<string, List<int>> Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Dictionary<int, List<string>> Data { get; set; }
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

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));
        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        CodeAction action = Assert.Single(actions);
        Assert.Equal("AM021_Ignore_Data", action.EquivalenceKey);
    }

    [Fact]
    public async Task AM021_ShouldFixCaseOnlyPropertyMismatch_UsingSourceAndDestinationNamesSeparately()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> numbers { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<int> Numbers { get; set; }
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
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public List<string> numbers { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public List<int> Numbers { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Numbers, opt => opt.MapFrom(src => src.numbers.Select(x => global::System.Convert.ToInt32(x)).ToList()));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(20, 13)
                    .WithArguments("numbers", "Source", "string", "Destination", "Numbers", "int"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM021_CodeFix_ShouldNotOfferFix_WhenForMemberUsesStringPropertyName()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Numbers { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<int> Numbers { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember("Numbers", opt => opt.Ignore());
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM021_CollectionElementMismatchAnalyzer>
            .VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM021_CodeFix_ShouldNotOfferFix_WhenConstructUsingHandlesForwardMapping()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Linq;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Numbers { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<int> Numbers { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ConstructUsing(src => new Destination
                                                {
                                                    Numbers = src.Numbers.Select(x => int.Parse(x)).ToList()
                                                });
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM021_CollectionElementMismatchAnalyzer>
            .VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM021_CodeFix_ShouldNotOfferFix_WhenConvertUsingHandlesForwardMapping()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Linq;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Numbers { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<int> Numbers { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ConvertUsing(src => new Destination
                                                {
                                                    Numbers = src.Numbers.Select(x => int.Parse(x)).ToList()
                                                });
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM021_CollectionElementMismatchAnalyzer>
            .VerifyAnalyzerAsync(testCode);
    }

    private static Document CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);

        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, "AM021Tests", "AM021Tests", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview));

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

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(Document document)
    {
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        return (await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new AM021_CollectionElementMismatchAnalyzer()))
            .GetAnalyzerDiagnosticsAsync())
            .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .ThenBy(diagnostic => diagnostic.GetMessage(), StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static async Task<List<CodeAction>> RegisterActionsAsync(Document document, params Diagnostic[] diagnostics)
    {
        var actions = new List<CodeAction>();
        var provider = new AM021_CollectionElementMismatchCodeFixProvider();

        var context = new CodeFixContext(
            document,
            diagnostics[0].Location.SourceSpan,
            diagnostics.ToImmutableArray(),
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);
        return actions;
    }
}

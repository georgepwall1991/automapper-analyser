using AutoMapperAnalyzer.Analyzers.ComplexMappings;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis.Testing;

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
                                         using System;

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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Numbers, opt => opt.MapFrom(src => src.Numbers.Select(x => Convert.ToInt32(x)).ToList()));
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
                                         using System;

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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags.Select(x => Convert.ToInt32(x)).ToArray()));
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
                                         using System;

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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => new Queue<int>(src.Values.Select(x => Convert.ToInt32(x)))));
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
                                         using System;

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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => new Stack<int>(src.Values.Select(x => Convert.ToInt32(x)))));
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
                                         using System;

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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => src.Values.Select(x => Convert.ToInt32(x)).ToList()));
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
                                         using System;

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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Numbers, opt => opt.MapFrom(src => src.numbers.Select(x => Convert.ToInt32(x)).ToList()));
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
}

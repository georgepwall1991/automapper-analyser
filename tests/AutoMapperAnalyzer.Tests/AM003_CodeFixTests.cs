using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests;

public class AM003_CodeFixTests
{
    private static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor, int line, int column, params object[] messageArgs)
    {
        var result = new DiagnosticResult(descriptor).WithLocation(line, column);
        if (messageArgs.Length > 0)
        {
            result = result.WithArguments(messageArgs);
        }

        return result;
    }

    private static Task VerifyFixAsync(string source, DiagnosticDescriptor descriptor, int line, int column, string fixedCode, params object[] messageArgs)
        => CodeFixVerifier<AM003_CollectionTypeIncompatibilityAnalyzer, AM003_CollectionTypeIncompatibilityCodeFixProvider>
            .VerifyFixAsync(source, Diagnostic(descriptor, line, column, messageArgs), fixedCode);

    [Fact]
    public async Task AM003_ShouldFixHashSetToListWithToList()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public HashSet<string> Tags { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<string> Tags { get; set; }
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
                                                 public HashSet<string> Tags { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public List<string> Tags { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags.ToList()));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            21,
            13,
            expectedFixedCode,
            "Tags",
            "Source",
            "System.Collections.Generic.HashSet<string>",
            "Destination",
            "System.Collections.Generic.List<string>");
    }

    [Fact]
    public async Task AM003_ShouldFixElementTypeIncompatibilityWithSelect()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Items { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<int> Items { get; set; }
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
                                                 public List<string> Items { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public List<int> Items { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items.Select(x => int.Parse(x))));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionElementIncompatibilityRule,
            21,
            13,
            expectedFixedCode,
            "Items",
            "Source",
            "string",
            "Destination",
            "int");
    }

    [Fact]
    public async Task AM003_ShouldFixStackToListWithConstructor()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Stack<int> Numbers { get; set; }
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

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public Stack<int> Numbers { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public List<int> Numbers { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Numbers, opt => opt.MapFrom(src => new List<int>(src.Numbers)));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            21,
            13,
            expectedFixedCode,
            "Numbers",
            "Source",
            "System.Collections.Generic.Stack<int>",
            "Destination",
            "System.Collections.Generic.List<int>");
    }

    [Fact]
    public async Task AM003_ShouldFixArrayToIEnumerableConversion()
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
                                        public IEnumerable<string> Tags { get; set; }
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
                                                 public IEnumerable<string> Tags { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags.AsEnumerable()));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            20,
            13,
            expectedFixedCode,
            "Tags",
            "Source",
            "string[]",
            "Destination",
            "System.Collections.Generic.IEnumerable<string>");
    }

    [Fact]
    public async Task AM003_ShouldFixICollectionToHashSetConversion()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public ICollection<int> Values { get; set; }
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

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public ICollection<int> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public HashSet<int> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => new HashSet<int>(src.Values)));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            20,
            13,
            expectedFixedCode,
            "Values",
            "Source",
            "System.Collections.Generic.ICollection<int>",
            "Destination",
            "System.Collections.Generic.HashSet<int>");
    }

    [Fact]
    public async Task AM003_ShouldFixQueueToListConversion()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Queue<string> Messages { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<string> Messages { get; set; }
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
                                             public class Source
                                             {
                                                 public Queue<string> Messages { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public List<string> Messages { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Messages, opt => opt.MapFrom(src => new List<string>(src.Messages)));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            20,
            13,
            expectedFixedCode,
            "Messages",
            "Source",
            "System.Collections.Generic.Queue<string>",
            "Destination",
            "System.Collections.Generic.List<string>");
    }

    [Fact]
    public async Task AM003_ShouldFixListWithComplexElementTypeConversion()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<object> Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<string> Data { get; set; }
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
                                                 public List<object> Data { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public List<string> Data { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Data, opt => opt.MapFrom(src => src.Data.Select(x => x != null ? x.ToString() : string.Empty)));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionElementIncompatibilityRule,
            20,
            13,
            expectedFixedCode,
            "Data",
            "Source",
            "object",
            "Destination",
            "string");
    }

    [Fact]
    public async Task AM003_ShouldFixListToBoolConversionWithParse()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Flags { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<bool> Flags { get; set; }
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
                                                 public List<string> Flags { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public List<bool> Flags { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Flags, opt => opt.MapFrom(src => src.Flags.Select(x => bool.Parse(x))));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionElementIncompatibilityRule,
            20,
            13,
            expectedFixedCode,
            "Flags",
            "Source",
            "string",
            "Destination",
            "bool");
    }

    [Fact]
    public async Task AM003_ShouldHandleListToQueueConversion()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<int> Items { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Queue<int> Items { get; set; }
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
                                             public class Source
                                             {
                                                 public List<int> Items { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public Queue<int> Items { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Items, opt => opt.MapFrom(src => new Queue<int>(src.Items)));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            20,
            13,
            expectedFixedCode,
            "Items",
            "Source",
            "System.Collections.Generic.List<int>",
            "Destination",
            "System.Collections.Generic.Queue<int>");
    }

    [Fact]
    public async Task AM003_ShouldHandleIEnumerableToStackConversion()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public IEnumerable<string> Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Stack<string> Data { get; set; }
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
                                             public class Source
                                             {
                                                 public IEnumerable<string> Data { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public Stack<string> Data { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Data, opt => opt.MapFrom(src => new Stack<string>(src.Data)));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            20,
            13,
            expectedFixedCode,
            "Data",
            "Source",
            "System.Collections.Generic.IEnumerable<string>",
            "Destination",
            "System.Collections.Generic.Stack<string>");
    }

    [Fact]
    public async Task AM003_ShouldHandleDoubleToIntElementConversion()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<double> Measurements { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<int> Measurements { get; set; }
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
                                                 public List<double> Measurements { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public List<int> Measurements { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Measurements, opt => opt.MapFrom(src => src.Measurements.Select(x => global::System.Convert.ToInt32(x))));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionElementIncompatibilityRule,
            20,
            13,
            expectedFixedCode,
            "Measurements",
            "Source",
            "double",
            "Destination",
            "int");
    }

    [Fact]
    public async Task AM003_ShouldHandleEnumerableToHashSetWithTypeConversion()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public IEnumerable<int> UniqueIds { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public HashSet<int> UniqueIds { get; set; }
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
                                             public class Source
                                             {
                                                 public IEnumerable<int> UniqueIds { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public HashSet<int> UniqueIds { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.UniqueIds, opt => opt.MapFrom(src => new HashSet<int>(src.UniqueIds)));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            20,
            13,
            expectedFixedCode,
            "UniqueIds",
            "Source",
            "System.Collections.Generic.IEnumerable<int>",
            "Destination",
            "System.Collections.Generic.HashSet<int>");
    }
}

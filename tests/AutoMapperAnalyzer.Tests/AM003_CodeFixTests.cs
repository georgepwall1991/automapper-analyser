using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests;

public class AM003_CodeFixTests
{
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
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags.ToList()));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM003_CollectionTypeIncompatibilityAnalyzer>()
            .WithCodeFix<AM003_CollectionTypeIncompatibilityCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule, 23, 29)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
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
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items.Select(x => int.Parse(x))));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM003_CollectionTypeIncompatibilityAnalyzer>()
            .WithCodeFix<AM003_CollectionTypeIncompatibilityCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionElementIncompatibilityRule, 23, 29)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
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
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Numbers, opt => opt.MapFrom(src => new List<int>(src.Numbers)));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM003_CollectionTypeIncompatibilityAnalyzer>()
            .WithCodeFix<AM003_CollectionTypeIncompatibilityCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule, 23, 29)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
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
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags.AsEnumerable()));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM003_CollectionTypeIncompatibilityAnalyzer>()
            .WithCodeFix<AM003_CollectionTypeIncompatibilityCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule, 16, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
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
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Values, opt => opt.MapFrom(src => new HashSet<int>(src.Values)));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM003_CollectionTypeIncompatibilityAnalyzer>()
            .WithCodeFix<AM003_CollectionTypeIncompatibilityCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule, 16, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
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
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Messages, opt => opt.MapFrom(src => new List<string>(src.Messages)));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM003_CollectionTypeIncompatibilityAnalyzer>()
            .WithCodeFix<AM003_CollectionTypeIncompatibilityCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule, 16, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
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
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Data, opt => opt.MapFrom(src => src.Data.Select(x => x.ToString())));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM003_CollectionTypeIncompatibilityAnalyzer>()
            .WithCodeFix<AM003_CollectionTypeIncompatibilityCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionElementIncompatibilityRule, 16, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
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
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Flags, opt => opt.MapFrom(src => src.Flags.Select(x => bool.Parse(x))));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM003_CollectionTypeIncompatibilityAnalyzer>()
            .WithCodeFix<AM003_CollectionTypeIncompatibilityCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM003_CollectionTypeIncompatibilityAnalyzer.CollectionElementIncompatibilityRule, 16, 13)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }
}
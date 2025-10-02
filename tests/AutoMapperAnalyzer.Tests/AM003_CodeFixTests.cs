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
}
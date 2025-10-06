using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests;

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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Numbers, opt => opt.MapFrom(src => src.Numbers.Select(x => int.Parse(x)).ToList()));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(20, 13)
                    .WithArguments("Numbers", "Source", "string", "Destination", "int"),
                expectedFixedCode);
    }

    [Fact(Skip = "Complex element mapping requires analyzer enhancement to detect element CreateMaps")]
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
                                                     CreateMap<SourcePerson, DestPerson>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(30, 13)
                    .WithArguments("People", "Source", "TestNamespace.SourcePerson", "Destination", "TestNamespace.DestPerson"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM021_ShouldFixArrayToHashSet_WithSelect()
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
                                        public HashSet<int> Tags { get; set; }
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
                                                 public HashSet<int> Tags { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags.Select(x => int.Parse(x)).ToHashSet()));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM021_CollectionElementMismatchAnalyzer, AM021_CollectionElementMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule)
                    .WithLocation(20, 13)
                    .WithArguments("Tags", "Source", "string", "Destination", "int"),
                expectedFixedCode);
    }
}

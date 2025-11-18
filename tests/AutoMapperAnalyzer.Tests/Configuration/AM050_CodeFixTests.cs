using AutoMapperAnalyzer.Analyzers.Configuration;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.Configuration;

public class AM050_CodeFixTests
{
    [Fact]
    public async Task Should_Remove_RedundantMapping_AtEndOfChain()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string Name { get; set; } }
                                public class Destination { public string Name { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 using AutoMapper;

                                 public class Source { public string Name { get; set; } }
                                 public class Destination { public string Name { get; set; } }

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Destination>();
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
            .WithLocation(11, 42)
            .WithArguments("Name");

        await CodeFixVerifier<AM050_RedundantMapFromAnalyzer, AM050_RedundantMapFromCodeFixProvider>
            .VerifyFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task Should_Remove_RedundantMapping_InMiddleOfChain()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source 
                                { 
                                    public string Name { get; set; } 
                                    public int Age { get; set; }
                                }
                                public class Destination 
                                { 
                                    public string Name { get; set; } 
                                    public int UserAge { get; set; }
                                }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name))
                                            .ForMember(d => d.UserAge, o => o.MapFrom(s => s.Age));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 using AutoMapper;

                                 public class Source 
                                 { 
                                     public string Name { get; set; } 
                                     public int Age { get; set; }
                                 }
                                 public class Destination 
                                 { 
                                     public string Name { get; set; } 
                                     public int UserAge { get; set; }
                                 }

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                             .ForMember(d => d.UserAge, o => o.MapFrom(s => s.Age));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
            .WithLocation(19, 42)
            .WithArguments("Name");

        await CodeFixVerifier<AM050_RedundantMapFromAnalyzer, AM050_RedundantMapFromCodeFixProvider>
            .VerifyFixAsync(testCode, expected, fixedCode);
    }
}

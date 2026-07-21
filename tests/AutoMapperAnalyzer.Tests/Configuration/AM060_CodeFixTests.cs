using AutoMapperAnalyzer.Analyzers.Configuration;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.Configuration;

public class AM060_CodeFixTests
{
    [Fact]
    public async Task AM060_CodeFix_ShouldAddCreateMapToProfile()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}
                                public class Other {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Other>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, Source source)
                                    {
                                        var destination = mapper.Map<Destination>(source);
                                    }
                                }
                                """;

        const string fixedCode = """
                                 using AutoMapper;

                                 public class Source {}
                                 public class Destination {}
                                 public class Other {}

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Other>();
                                         CreateMap<Source, Destination>();
                                     }
                                 }

                                 public class Service
                                 {
                                     public void Run(IMapper mapper, Source source)
                                     {
                                         var destination = mapper.Map<Destination>(source);
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(AM060_UnregisteredTypeMapAnalyzer.UnregisteredTypeMapRule)
            .WithLocation(19, 34)
            .WithArguments("Source", "Destination");

        await CodeFixVerifier<AM060_UnregisteredTypeMapAnalyzer, AM060_UnregisteredTypeMapCodeFixProvider>
            .VerifyFixByKeyAsync(
                testCode,
                expected,
                fixedCode,
                "AM060_CreateMap_Source_Destination");
    }

    [Fact]
    public async Task AM060_CodeFix_ShouldAddElementCreateMapForCollectionCall()
    {
        const string testCode = """
                                using System.Collections.Generic;
                                using AutoMapper;

                                public class Source {}
                                public class Destination {}

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Source>();
                                    }
                                }

                                public class Service
                                {
                                    public void Run(IMapper mapper, List<Source> sources)
                                    {
                                        var destination = mapper.Map<List<Destination>>(sources);
                                    }
                                }
                                """;

        const string fixedCode = """
                                 using System.Collections.Generic;
                                 using AutoMapper;

                                 public class Source {}
                                 public class Destination {}

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Source>();
                                         CreateMap<Source, Destination>();
                                     }
                                 }

                                 public class Service
                                 {
                                     public void Run(IMapper mapper, List<Source> sources)
                                     {
                                         var destination = mapper.Map<List<Destination>>(sources);
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(AM060_UnregisteredTypeMapAnalyzer.UnregisteredTypeMapRule)
            .WithLocation(19, 34)
            .WithArguments("Source", "Destination");

        await CodeFixVerifier<AM060_UnregisteredTypeMapAnalyzer, AM060_UnregisteredTypeMapCodeFixProvider>
            .VerifyFixByKeyAsync(
                testCode,
                expected,
                fixedCode,
                "AM060_CreateMap_Source_Destination");
    }
}

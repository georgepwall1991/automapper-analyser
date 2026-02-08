using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

public class AM006_CodeFixTests
{
    private static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor, int line, int column,
        params object[] messageArgs)
    {
        DiagnosticResult result = new DiagnosticResult(descriptor).WithLocation(line, column);
        if (messageArgs.Length > 0)
        {
            result = result.WithArguments(messageArgs);
        }

        return result;
    }

    private static Task VerifyFixAsync(string source, DiagnosticDescriptor descriptor, int line, int column,
        string fixedCode, int? codeActionIndex = null, DiagnosticResult[]? remainingDiagnostics = null,
        params object[] messageArgs)
    {
        return CodeFixVerifier<AM006_UnmappedDestinationPropertyAnalyzer,
                AM006_UnmappedDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(source, Diagnostic(descriptor, line, column, messageArgs), fixedCode, codeActionIndex,
                remainingDiagnostics);
    }

    [Fact]
    public async Task AM006_ShouldIgnoreSingleUnmappedDestinationProperty()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string ExtraInfo { get; set; }
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

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public string ExtraInfo { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.ExtraInfo, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            21, 13,
            expectedFixedCode,
            codeActionIndex: 0,
            messageArgs: ["ExtraInfo", "Source"]);
    }

    [Fact]
    public async Task AM006_ShouldNotOfferFix_WhenAllPropertiesMapped()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
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

        // No diagnostics expected — no fix should be offered
        await CodeFixVerifier<AM006_UnmappedDestinationPropertyAnalyzer,
                AM006_UnmappedDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(testCode, Array.Empty<DiagnosticResult>(), testCode, null, null, 0);
    }

    [Fact]
    public async Task AM006_ShouldIgnoreUnmappedDestinationProperty_WithReverseMap()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string SourceOnly { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string DestOnly { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>().ReverseMap();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """

                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                                 public string SourceOnly { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public string DestOnly { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.DestOnly, opt => opt.Ignore()).ReverseMap();
                                                 }
                                             }
                                         }
                                         """;

        // The forward-map diagnostic: DestOnly not mapped from Source
        // ReverseMap also generates: SourceOnly not mapped from Destination
        // We fix the forward-map DestOnly diagnostic
        var forwardDiag = Diagnostic(
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            22, 13, "DestOnly", "Source");
        var reverseDiag = Diagnostic(
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            22, 13, "SourceOnly", "Destination");

        await CodeFixVerifier<AM006_UnmappedDestinationPropertyAnalyzer,
                AM006_UnmappedDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(testCode, [forwardDiag, reverseDiag], expectedFixedCode, 0,
                remainingDiagnostics: [reverseDiag]);
    }

    [Fact]
    public async Task AM006_ShouldSuggestFuzzyMatch_WhenSimilarSourcePropertyExists()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Emial { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Email { get; set; }
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

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                                 public string Emial { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public string Email { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Emial));
                                                 }
                                             }
                                         }
                                         """;

        // Code action index 2 = fuzzy match suggestion (after Ignore and Create Property)
        await VerifyFixAsync(
            testCode,
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            22, 13,
            expectedFixedCode,
            codeActionIndex: 2,
            messageArgs: ["Email", "Source"]);
    }

    [Fact]
    public async Task AM006_ShouldBulkIgnoreAllUnmappedDestinationProperties()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Extra1 { get; set; }
                                        public string Extra2 { get; set; }
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

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public string Extra1 { get; set; }
                                                 public string Extra2 { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Extra1, opt => opt.Ignore()).ForMember(dest => dest.Extra2, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        var diag1 = Diagnostic(
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            22, 13, "Extra1", "Source");
        var diag2 = Diagnostic(
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            22, 13, "Extra2", "Source");

        await CodeFixVerifier<AM006_UnmappedDestinationPropertyAnalyzer,
                AM006_UnmappedDestinationPropertyCodeFixProvider>
            .VerifyFixWithIterationsAsync(testCode, [diag1, diag2], expectedFixedCode, 1);
    }

    [Fact]
    public async Task AM006_ShouldCreateSourceProperty()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Description { get; set; }
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

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                                 public string Description { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public string Description { get; set; }
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

        // Code action index 1 = "Create property in source type" (after Ignore)
        await VerifyFixAsync(
            testCode,
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            21, 13,
            expectedFixedCode,
            codeActionIndex: 1,
            messageArgs: ["Description", "Source"]);
    }

    [Fact]
    public async Task AM006_BulkIgnore_WithReverseMap_ShouldInsertForMemberBeforeReverseMap()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Extra1 { get; set; }
                                        public string Extra2 { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>().ReverseMap();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """

                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public string Extra1 { get; set; }
                                                 public string Extra2 { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Extra1, opt => opt.Ignore()).ForMember(dest => dest.Extra2, opt => opt.Ignore()).ReverseMap();
                                                 }
                                             }
                                         }
                                         """;

        var forwardDiag1 = Diagnostic(
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            22, 13, "Extra1", "Source");
        var forwardDiag2 = Diagnostic(
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            22, 13, "Extra2", "Source");

        await CodeFixVerifier<AM006_UnmappedDestinationPropertyAnalyzer,
                AM006_UnmappedDestinationPropertyCodeFixProvider>
            .VerifyFixWithIterationsAsync(testCode, [forwardDiag1, forwardDiag2], expectedFixedCode, 1);
    }

    [Fact]
    public async Task AM006_ShouldIgnoreWithExistingForMemberChain()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public string Mapped { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Mapped { get; set; }
                                        public string Unmapped { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Mapped, opt => opt.MapFrom(src => src.Mapped));
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """

                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                                 public string Mapped { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public string Mapped { get; set; }
                                                 public string Unmapped { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                         .ForMember(dest => dest.Unmapped, opt => opt.Ignore()).ForMember(dest => dest.Mapped, opt => opt.MapFrom(src => src.Mapped));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            23, 13,
            expectedFixedCode,
            codeActionIndex: 0,
            messageArgs: ["Unmapped", "Source"]);
    }

    [Fact]
    public async Task AM006_BulkIgnore_ShouldPreserveExistingForMemberCalls()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string AlreadyIgnored { get; set; }
                                        public string NotYetIgnored { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.AlreadyIgnored, opt => opt.Ignore());
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """

                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public string AlreadyIgnored { get; set; }
                                                 public string NotYetIgnored { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                         .ForMember(dest => dest.NotYetIgnored, opt => opt.Ignore()).ForMember(dest => dest.AlreadyIgnored, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        var diag = Diagnostic(
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            22, 13, "NotYetIgnored", "Source");

        await CodeFixVerifier<AM006_UnmappedDestinationPropertyAnalyzer,
                AM006_UnmappedDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(testCode, [diag], expectedFixedCode, null, null, 1);
    }

    [Fact]
    public async Task AM006_ShouldCreateSourcePropertyWithNonStringType()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public int Count { get; set; }
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

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string Name { get; set; }
                                                 public int Count { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public int Count { get; set; }
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

        // Code action index 1 = "Create property in source type" (after Ignore)
        await VerifyFixAsync(
            testCode,
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            21, 13,
            expectedFixedCode,
            codeActionIndex: 1,
            messageArgs: ["Count", "Source"]);
    }
}

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

    private static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor, string source, params object[] messageArgs)
    {
        if (messageArgs.Length == 0 || messageArgs[0] is not string propertyName)
        {
            return new DiagnosticResult(descriptor);
        }

        (int line, int column) = FindDestinationPropertyLocation(source, propertyName);
        return Diagnostic(descriptor, line, column, messageArgs);
    }

    private static Task VerifyFixAsync(string source, DiagnosticDescriptor descriptor, int line, int column,
        string fixedCode, int? codeActionIndex = null, DiagnosticResult[]? remainingDiagnostics = null,
        params object[] messageArgs)
    {
        return CodeFixVerifier<AM006_UnmappedDestinationPropertyAnalyzer,
                AM006_UnmappedDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(source, Diagnostic(descriptor, source, messageArgs), fixedCode, codeActionIndex,
                remainingDiagnostics);
    }

    private static (int Line, int Column) FindDestinationPropertyLocation(string source, string propertyName)
    {
        string[] lines = source.Split(["\r\n", "\n"], StringSplitOptions.None);
        var candidates = new List<(int Line, int Column, string ClassName)>();
        string currentClass = string.Empty;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            string[] tokens = trimmed.Split([' ', '<', '(', '{'], StringSplitOptions.RemoveEmptyEntries);
            int classIndex = Array.FindIndex(tokens, token => token is "class" or "record" or "interface" or "struct");
            if (classIndex >= 0 && classIndex + 1 < tokens.Length)
            {
                currentClass = tokens[classIndex + 1];
            }

            if (line.Contains("CreateMap", StringComparison.Ordinal) ||
                line.Contains("ForMember", StringComparison.Ordinal) ||
                line.Contains("MapFrom", StringComparison.Ordinal))
            {
                continue;
            }

            int column = line.IndexOf(propertyName, StringComparison.Ordinal);
            if (column >= 0)
            {
                candidates.Add((i + 1, column + 1, currentClass));
            }
        }

        (int Line, int Column, string ClassName) destination = candidates.FirstOrDefault(candidate =>
            candidate.ClassName.Contains("Destination", StringComparison.Ordinal) ||
            candidate.ClassName.Contains("Dest", StringComparison.Ordinal));
        if (destination.Line != 0)
        {
            return (destination.Line, destination.Column);
        }

        (int Line, int Column, string ClassName) first = candidates.FirstOrDefault();
        if (first.Line != 0)
        {
            return (first.Line, first.Column);
        }

        throw new InvalidOperationException($"Could not find property '{propertyName}' in the test source.");
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
    public async Task AM006_ShouldIgnorePositionalRecordDestinationProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public record Destination(string Name, string ExtraInfo);

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

                                             public record Destination(string Name, string ExtraInfo);

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
            10, 60,
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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.DestOnly, opt => opt.Ignore()).ReverseMap().ForMember(dest => dest.SourceOnly, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        // The forward-map diagnostic: DestOnly not mapped from Source
        // ReverseMap also generates: SourceOnly not mapped from Destination
        // Both diagnostics are fixed in a single operation with the simplified fixer
        var forwardDiag = Diagnostic(
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            testCode,
            "DestOnly", "Source");
        var reverseDiag = Diagnostic(
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            testCode,
            "SourceOnly", "Destination");

        await CodeFixVerifier<AM006_UnmappedDestinationPropertyAnalyzer,
                AM006_UnmappedDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(testCode, [forwardDiag, reverseDiag], expectedFixedCode, codeActionIndex: 0, remainingDiagnostics: null);
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

        // Code action index 0 = fuzzy match suggestion (only fuzzy match and ignore are available)
        await VerifyFixAsync(
            testCode,
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            22, 13,
            expectedFixedCode,
            codeActionIndex: 0,
            messageArgs: ["Email", "Source"]);
    }

    [Fact]
    public async Task AM006_ShouldSuggestFuzzyMatch_ForReverseMapDiagnostic()
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
                                        public string Emial { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Emial, opt => opt.MapFrom(src => src.Email))
                                                .ReverseMap();
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
                                                 public string Email { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public string Emial { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Emial, opt => opt.MapFrom(src => src.Email))
                                                         .ReverseMap().ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Emial));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM006_UnmappedDestinationPropertyAnalyzer, AM006_UnmappedDestinationPropertyCodeFixProvider>
            .VerifyFixByKeyAsync(
                testCode,
                Diagnostic(
                    AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
                    testCode,
                    "Email", "Destination"),
                expectedFixedCode,
                "AM006_FuzzyMatch_Email_Emial");
    }

    [Fact]
    public async Task AM006_ShouldSuppressFuzzyMatch_WhenBestCandidateIsAmbiguous()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Eamil { get; set; }
                                        public string Emial { get; set; }
                                    }

                                    public class Destination
                                    {
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
                                                 public string Eamil { get; set; }
                                                 public string Emial { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Email { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Email, opt => opt.Ignore());
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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Extra2, opt => opt.Ignore()).ForMember(dest => dest.Extra1, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        var diag1 = Diagnostic(
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            testCode,
            "Extra1", "Source");
        var diag2 = Diagnostic(
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            testCode,
            "Extra2", "Source");

        await CodeFixVerifier<AM006_UnmappedDestinationPropertyAnalyzer,
                AM006_UnmappedDestinationPropertyCodeFixProvider>
            .VerifyFixWithIterationsAsync(testCode, [diag1, diag2], expectedFixedCode, 2);
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
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Extra2, opt => opt.Ignore()).ForMember(dest => dest.Extra1, opt => opt.Ignore()).ReverseMap();
                                                 }
                                             }
                                         }
                                         """;

        var forwardDiag1 = Diagnostic(
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            testCode,
            "Extra1", "Source");
        var forwardDiag2 = Diagnostic(
            AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule,
            testCode,
            "Extra2", "Source");

        await CodeFixVerifier<AM006_UnmappedDestinationPropertyAnalyzer,
                AM006_UnmappedDestinationPropertyCodeFixProvider>
            .VerifyFixWithIterationsAsync(testCode, [forwardDiag1, forwardDiag2], expectedFixedCode, 2);
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
            testCode,
            "NotYetIgnored", "Source");

        await CodeFixVerifier<AM006_UnmappedDestinationPropertyAnalyzer,
                AM006_UnmappedDestinationPropertyCodeFixProvider>
            .VerifyFixAsync(testCode, [diag], expectedFixedCode, null, null, 1);
    }

    [Fact]
    public async Task AM006_ShouldHandleInternalProperties()
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
                                        internal string InternalField { get; set; }
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
                                                 internal string InternalField { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.InternalField, opt => opt.Ignore());
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
            messageArgs: ["InternalField", "Source"]);
    }

    [Fact]
    public async Task AM006_ShouldHandleProtectedPropertiesInInheritance()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class BaseDestination
                                    {
                                        protected string ProtectedValue { get; set; }
                                    }

                                    public class Destination : BaseDestination
                                    {
                                        public string Name { get; set; }
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

        // Protected properties are not publicly accessible and should not be flagged
        await AnalyzerVerifier<AM006_UnmappedDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM006_ShouldHandleGenericTypesWithConstraints()
    {
        const string testCode = """

                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source<T> where T : class
                                    {
                                        public string Name { get; set; }
                                        public T Value { get; set; }
                                    }

                                    public class Destination<T> where T : class
                                    {
                                        public string Name { get; set; }
                                        public T Value { get; set; }
                                        public string ExtraField { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source<string>, Destination<string>>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """

                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source<T> where T : class
                                             {
                                                 public string Name { get; set; }
                                                 public T Value { get; set; }
                                             }

                                             public class Destination<T> where T : class
                                             {
                                                 public string Name { get; set; }
                                                 public T Value { get; set; }
                                                 public string ExtraField { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source<string>, Destination<string>>().ForMember(dest => dest.ExtraField, opt => opt.Ignore());
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
            messageArgs: ["ExtraField", "Source"]);
    }
}

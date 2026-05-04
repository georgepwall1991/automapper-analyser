using System.Collections.Immutable;
using System.IO;
using AutoMapper;
using AutoMapperAnalyzer.Analyzers.TypeSafety;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.TypeSafety;

public class AM002_CodeFixTests
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
        string fixedCode, params object[] messageArgs)
    {
        return VerifyFixAsync(source, descriptor, line, column, fixedCode, null, messageArgs);
    }

    private static Task VerifyFixAsync(string source, DiagnosticDescriptor descriptor, int line, int column,
        string fixedCode, DiagnosticResult[]? remainingDiagnostics, params object[] messageArgs)
    {
        return CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixAsync(source, Diagnostic(descriptor, line, column, messageArgs), fixedCode, remainingDiagnostics);
    }

    [Fact]
    public async Task AM002_ShouldFixNullableToNonNullableWithNullCoalescing()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string? Name { get; set; }
                                    }

                                    public class Destination
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

        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string? Name { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? string.Empty));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            19,
            13,
            expectedFixedCode,
            "Name",
            "Source",
            "string?",
            "Destination",
            "string");
    }

    [Fact]
    public async Task AM002_ShouldFixNullableIntToNonNullableWithDefault()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int? Age { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Age { get; set; }
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
                                                 public int? Age { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public int Age { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age ?? 0));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            19,
            13,
            expectedFixedCode,
            "Age",
            "Source",
            "int?",
            "Destination",
            "int");
    }

    [Fact]
    public async Task AM002_ShouldFixNullableBoolToNonNullable()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public bool? IsActive { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool IsActive { get; set; }
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
                                                 public bool? IsActive { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public bool IsActive { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive ?? false));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            19,
            13,
            expectedFixedCode,
            "IsActive",
            "Source",
            "bool?",
            "Destination",
            "bool");
    }

    [Fact]
    public async Task AM002_ShouldFixMultipleNullableProperties()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string? Name { get; set; }
                                        public int? Age { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public int Age { get; set; }
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

        // Both properties get fixed via per-property fixes
        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string? Name { get; set; }
                                                 public int? Age { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                                 public int Age { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? string.Empty)).ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age ?? 0));
                                                 }
                                             }
                                         }
                                         """;

        DiagnosticResult ageDiagnostic = Diagnostic(
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            21,
            13,
            "Age",
            "Source",
            "int?",
            "Destination",
            "int");

        DiagnosticResult nameDiagnostic = Diagnostic(
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            21,
            13,
            "Name",
            "Source",
            "string?",
            "Destination",
            "string");

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixWithIterationsAsync(
                testCode,
                new[] { ageDiagnostic, nameDiagnostic },
                expectedFixedCode,
                2);
    }

    [Fact]
    public async Task AM002_ShouldFixNullableIntWithDefaultCoalescing()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int? Count { get; set; }
                                    }

                                    public class Destination
                                    {
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
                                                 public int? Count { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public int Count { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Count, opt => opt.MapFrom(src => src.Count ?? 0));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            19,
            13,
            expectedFixedCode,
            "Count",
            "Source",
            "int?",
            "Destination",
            "int");
    }

    [Fact]
    public async Task AM002_ShouldFixNullableDecimalWithDefaultCoalescing()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public decimal? Amount { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public decimal Amount { get; set; }
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
                                                 public decimal? Amount { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public decimal Amount { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount ?? 0m));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
            19,
            13,
            expectedFixedCode,
            "Amount",
            "Source",
            "decimal?",
            "Destination",
            "decimal");
    }

    [Fact]
    public async Task AM002_ShouldIgnoreSingleProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string? Name { get; set; }
                                    }

                                    public class Destination
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

        // Expect property to be ignored
        const string expectedFixedCode = """
                                         using AutoMapper;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public string? Name { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public string Name { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Name, opt => opt.Ignore());
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM002_NullableCompatibilityAnalyzer, AM002_NullableCompatibilityCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                Diagnostic(AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule, 19, 13,
                    "Name", "Source", "string?", "Destination", "string"),
                expectedFixedCode,
                codeActionIndex: 1); // Use the second fix (Ignore property)
    }

    [Fact]
    public async Task AM002_ShouldNotRegisterCodeActions_ForNonNullableToNullableInfoDiagnostic()
    {
        const string testCode = """
                                #nullable enable
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; } = string.Empty;
                                    }

                                    public class Destination
                                    {
                                        public string? Name { get; set; }
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
        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Descriptor == AM002_NullableCompatibilityAnalyzer.NonNullableToNullableRule);

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        Assert.Empty(actions);
    }

    private static Document CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);

        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, "AM002Tests", "AM002Tests", LanguageNames.CSharp)
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
                ImmutableArray.Create<DiagnosticAnalyzer>(new AM002_NullableCompatibilityAnalyzer()))
            .GetAnalyzerDiagnosticsAsync())
            .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .ThenBy(diagnostic => diagnostic.GetMessage(), StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static async Task<List<CodeAction>> RegisterActionsAsync(Document document, params Diagnostic[] diagnostics)
    {
        var actions = new List<CodeAction>();
        var provider = new AM002_NullableCompatibilityCodeFixProvider();

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

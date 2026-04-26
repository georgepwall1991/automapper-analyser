using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AutoMapper;
using AutoMapperAnalyzer.Analyzers.Configuration;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
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

    [Fact]
    public async Task Should_Remove_MultipleRedundantMappings()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source
                                {
                                    public string Name { get; set; }
                                    public string Email { get; set; }
                                    public int Age { get; set; }
                                }

                                public class Destination
                                {
                                    public string Name { get; set; }
                                    public string Email { get; set; }
                                    public int Age { get; set; }
                                }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name))
                                            .ForMember(d => d.Email, o => o.MapFrom(s => s.Email))
                                            .ForMember(d => d.Age, o => o.MapFrom(s => s.Age));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 using AutoMapper;

                                 public class Source
                                 {
                                     public string Name { get; set; }
                                     public string Email { get; set; }
                                     public int Age { get; set; }
                                 }

                                 public class Destination
                                 {
                                     public string Name { get; set; }
                                     public string Email { get; set; }
                                     public int Age { get; set; }
                                 }

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Destination>();
                                     }
                                 }
                                 """;

        DiagnosticResult[] diagnostics = [
            new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
                .WithLocation(22, 42).WithArguments("Name"),
            new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
                .WithLocation(23, 43).WithArguments("Email"),
            new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
                .WithLocation(24, 41).WithArguments("Age")
        ];

        await CodeFixVerifier<AM050_RedundantMapFromAnalyzer, AM050_RedundantMapFromCodeFixProvider>
            .VerifyFixWithIterationsAsync(testCode, diagnostics, fixedCode, iterations: 3);
    }

    [Fact]
    public async Task Should_Remove_RedundantMapping_WithReverseMap()
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
                                            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name))
                                            .ReverseMap();
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
                                         CreateMap<Source, Destination>()
                                             .ReverseMap();
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
    public async Task Should_Remove_RedundantMapping_WithStringDestinationMember()
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
                                            .ForMember("Name", o => o.MapFrom(s => s.Name));
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
            .WithLocation(11, 37)
            .WithArguments("Name");

        await CodeFixVerifier<AM050_RedundantMapFromAnalyzer, AM050_RedundantMapFromCodeFixProvider>
            .VerifyFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task Should_Remove_RedundantMapping_NumericTypes()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source
                                {
                                    public int Count { get; set; }
                                    public decimal Price { get; set; }
                                }

                                public class Destination
                                {
                                    public int Count { get; set; }
                                    public decimal Price { get; set; }
                                }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.Count, o => o.MapFrom(s => s.Count))
                                            .ForMember(d => d.Price, o => o.MapFrom(s => s.Price));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 using AutoMapper;

                                 public class Source
                                 {
                                     public int Count { get; set; }
                                     public decimal Price { get; set; }
                                 }

                                 public class Destination
                                 {
                                     public int Count { get; set; }
                                     public decimal Price { get; set; }
                                 }

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Destination>();
                                     }
                                 }
                                 """;

        DiagnosticResult[] diagnostics = [
            new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
                .WithLocation(20, 43).WithArguments("Count"),
            new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
                .WithLocation(21, 43).WithArguments("Price")
        ];

        await CodeFixVerifier<AM050_RedundantMapFromAnalyzer, AM050_RedundantMapFromCodeFixProvider>
            .VerifyFixWithIterationsAsync(testCode, diagnostics, fixedCode, iterations: 2);
    }

    [Fact]
    public async Task Should_NotRemove_NonRedundantMapping_ComplexTypes()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Address { public string Street { get; set; } }
                                public class AddressDto { public string Street { get; set; } }

                                public class Source { public Address Address { get; set; } }
                                public class Destination { public AddressDto Address { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Address, AddressDto>();
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.Address, o => o.MapFrom(s => s.Address));
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>
            .VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotRemove_NullableMismatch()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public int? Age { get; set; } }
                                public class Destination { public int Age { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.Age, o => o.MapFrom(s => s.Age));
                                    }
                                }
                                """;

        // No diagnostics expected - nullable to non-nullable should NOT be flagged as redundant
        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>
            .VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_Remove_RedundantMapping_RecordTypes()
    {
        const string testCode = """
                                using AutoMapper;

                                public record Source(string Name, int Age);
                                public record Destination(string Name, int Age);

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name))
                                            .ForMember(d => d.Age, o => o.MapFrom(s => s.Age));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 using AutoMapper;

                                 public record Source(string Name, int Age);
                                 public record Destination(string Name, int Age);

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Destination>();
                                     }
                                 }
                                 """;

        DiagnosticResult[] diagnostics = [
            new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
                .WithLocation(11, 42).WithArguments("Name"),
            new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
                .WithLocation(12, 41).WithArguments("Age")
        ];

        await CodeFixVerifier<AM050_RedundantMapFromAnalyzer, AM050_RedundantMapFromCodeFixProvider>
            .VerifyFixWithIterationsAsync(testCode, diagnostics, fixedCode, iterations: 2);
    }

    [Fact]
    public async Task Should_Offer_SingleCodeAction()
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

        // Verify code action at index 0 (only one fix should be offered)
        await CodeFixVerifier<AM050_RedundantMapFromAnalyzer, AM050_RedundantMapFromCodeFixProvider>
            .VerifyFixAsync(testCode, [expected], fixedCode, codeActionIndex: 0, remainingDiagnostics: null);
    }

    [Fact]
    public async Task Should_BeIdempotent()
    {
        const string testCode = """
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

        // No diagnostics expected - already clean, applying fix again should be safe
        await AnalyzerVerifier<AM050_RedundantMapFromAnalyzer>
            .VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_Preserve_NonRedundantMappings_InChain()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source
                                {
                                    public string Name { get; set; }
                                    public int Age { get; set; }
                                    public string Status { get; set; }
                                }

                                public class Destination
                                {
                                    public string Name { get; set; }
                                    public int Age { get; set; }
                                    public string UserStatus { get; set; }
                                }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name))
                                            .ForMember(d => d.Age, o => o.MapFrom(s => s.Age))
                                            .ForMember(d => d.UserStatus, o => o.MapFrom(s => s.Status));
                                    }
                                }
                                """;

        const string fixedCode = """
                                 using AutoMapper;

                                 public class Source
                                 {
                                     public string Name { get; set; }
                                     public int Age { get; set; }
                                     public string Status { get; set; }
                                 }

                                 public class Destination
                                 {
                                     public string Name { get; set; }
                                     public int Age { get; set; }
                                     public string UserStatus { get; set; }
                                 }

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                             .ForMember(d => d.UserStatus, o => o.MapFrom(s => s.Status));
                                     }
                                 }
                                 """;

        DiagnosticResult[] diagnostics = [
            new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
                .WithLocation(22, 42).WithArguments("Name"),
            new DiagnosticResult(AM050_RedundantMapFromAnalyzer.RedundantMapFromRule)
                .WithLocation(23, 41).WithArguments("Age")
        ];

        await CodeFixVerifier<AM050_RedundantMapFromAnalyzer, AM050_RedundantMapFromCodeFixProvider>
            .VerifyFixWithIterationsAsync(testCode, diagnostics, fixedCode, iterations: 2);
    }

    [Fact]
    public async Task Should_Register_PropertySpecific_CodeAction_Title()
    {
        const string testCode = """
                                using AutoMapper;

                                public class Source { public string Email { get; set; } }
                                public class Destination { public string Email { get; set; } }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(d => d.Email, o => o.MapFrom(s => s.Email));
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        Diagnostic diagnostic = (await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new AM050_RedundantMapFromAnalyzer()))
            .GetAnalyzerDiagnosticsAsync())
            .Single();

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);
        CodeAction action = Assert.Single(actions);
        Assert.Equal("Remove redundant ForMember for 'Email'", action.Title);
    }

    private static Document CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);

        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, "AM050Tests", "AM050Tests", LanguageNames.CSharp)
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

    private static async Task<List<CodeAction>> RegisterActionsAsync(Document document, Diagnostic diagnostic)
    {
        var actions = new List<CodeAction>();
        var provider = new AM050_RedundantMapFromCodeFixProvider();

        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);
        return actions;
    }
}

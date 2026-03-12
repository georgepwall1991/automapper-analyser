using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoMapper;
using AutoMapperAnalyzer.Analyzers.Performance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Tests.Performance;

public class AM031_CodeFixTests
{
    [Fact]
    public async Task AM031_ShouldRegisterAndApplyCachingFix_ForMultipleEnumeration()
    {
        const string source = """
                              using AutoMapper;
                              using System.Collections.Generic;
                              using System.Linq;

                              namespace TestNamespace
                              {
                                  public class Source
                                  {
                                      public List<int> Numbers { get; set; } = new();
                                  }

                                  public class Destination
                                  {
                                      public int Total { get; set; }
                                  }

                                  public class TestProfile : Profile
                                  {
                                      public TestProfile()
                                      {
                                          CreateMap<Source, Destination>()
                                              .ForMember(dest => dest.Total, opt => opt.MapFrom(src => src.Numbers.Sum() + src.Numbers.Average()));
                                      }
                                  }
                              }
                              """;

        Document document = CreateDocument(source);
        var diagnostic = await CreateDiagnosticAtSourceLambdaAsync(
            document,
            AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule,
            "MultipleEnumeration",
            "Total",
            "Numbers",
            "Total",
            "Numbers");

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        CodeAction cacheAction = Assert.Single(
            actions,
            action => action.Title.Contains("Cache 'Numbers' with ToList() for 'Total'", StringComparison.Ordinal));

        string updatedCode = await ApplyActionAsync(cacheAction, document);
        Assert.Contains("numbersCache", updatedCode, StringComparison.Ordinal);
        Assert.Contains("src.Numbers.ToList()", updatedCode, StringComparison.Ordinal);
        Assert.Contains("numbersCache.Sum()", updatedCode, StringComparison.Ordinal);
        Assert.Contains("numbersCache.Average()", updatedCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AM031_ShouldOfferIgnoreOnly_WhenConventionMappingIsNotPossible()
    {
        const string source = """
                              using AutoMapper;

                              namespace TestNamespace
                              {
                                  public class ScoreService
                                  {
                                      public int Calculate(int id) => id * 2;
                                  }

                                  public class Source
                                  {
                                      public int Id { get; set; }
                                  }

                                  public class Destination
                                  {
                                      public int Score { get; set; }
                                  }

                                  public class TestProfile : Profile
                                  {
                                      private readonly ScoreService _service;

                                      public TestProfile(ScoreService service)
                                      {
                                          _service = service;
                                          CreateMap<Source, Destination>()
                                              .ForMember(dest => dest.Score, opt => opt.MapFrom(src => _service.Calculate(src.Id)));
                                      }
                                  }
                              }
                              """;

        Document document = CreateDocument(source);
        var diagnostic = await CreateDiagnosticAtSourceLambdaAsync(
            document,
            AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule,
            "ExpensiveOperation",
            "Score",
            "Score",
            "method call");

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        CodeAction ignoreAction = Assert.Single(
            actions,
            action => action.Title.Contains("Ignore mapping for 'Score'", StringComparison.Ordinal));
        Assert.DoesNotContain(actions, action => action.Title.Contains("Remove redundant ForMember", StringComparison.Ordinal));

        string updatedCode = await ApplyActionAsync(ignoreAction, document);
        Assert.Contains(".ForMember(dest => dest.Score, opt => opt.Ignore())", updatedCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AM031_ShouldRegisterAndApplyRemoveForMemberFix()
    {
        const string source = """
                              using AutoMapper;

                              namespace TestNamespace
                              {
                                  public class Source
                                  {
                                      public int Score { get; set; }
                                  }

                                  public class Destination
                                  {
                                      public int Score { get; set; }
                                  }

                                  public class TestProfile : Profile
                                  {
                                      public TestProfile()
                                      {
                                          CreateMap<Source, Destination>()
                                              .ForMember(dest => dest.Score, opt => opt.MapFrom(src => src.Score + 1));
                                      }
                                  }
                              }
                              """;

        Document document = CreateDocument(source);
        var diagnostic = await CreateDiagnosticAtSourceLambdaAsync(
            document,
            AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule,
            "ExpensiveOperation",
            "Score",
            "Score",
            "method call");
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        InvocationExpressionSyntax forMemberInvocation = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .First(invocation => invocation.Expression is MemberAccessExpressionSyntax
            {
                Name.Identifier.Text: "ForMember"
            });

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        MethodInfo registerRemoveFixMethod = typeof(AM031_PerformanceWarningCodeFixProvider).GetMethod(
            "RegisterRemoveForMemberFix",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var provider = new AM031_PerformanceWarningCodeFixProvider();
        registerRemoveFixMethod.Invoke(provider, [context, root, forMemberInvocation, "Score", ImmutableArray.Create(diagnostic)]);

        CodeAction removeAction = Assert.Single(actions);
        Assert.Contains("Remove redundant ForMember for 'Score'", removeAction.Title, StringComparison.Ordinal);

        string updatedCode = await ApplyActionAsync(removeAction, document);
        Assert.Contains("CreateMap<Source, Destination>();", updatedCode, StringComparison.Ordinal);
        Assert.DoesNotContain("ForMember(dest => dest.Score", updatedCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AM031_ShouldRegisterCacheFix_WhenMultipleDiagnosticsShareSameLambda()
    {
        const string source = """
                              using AutoMapper;
                              using System;
                              using System.Collections.Generic;
                              using System.Linq;

                              namespace TestNamespace
                              {
                                  public class Source
                                  {
                                      public int Total { get; set; }
                                      public List<int> Numbers { get; set; } = new();
                                  }

                                  public class Destination
                                  {
                                      public int Total { get; set; }
                                  }

                                  public class TestProfile : Profile
                                  {
                                      public TestProfile()
                                      {
                                          CreateMap<Source, Destination>()
                                              .ForMember(dest => dest.Total, opt => opt.MapFrom(src => src.Numbers.Sum() + src.Numbers.Average() + DateTime.Now.Year));
                                      }
                                  }
                              }
                              """;

        Document document = CreateDocument(source);
        Diagnostic nonDeterministicDiagnostic = await CreateDiagnosticAtSourceLambdaAsync(
            document,
            AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule,
            "NonDeterministic",
            "Total",
            null,
            "Total",
            "DateTime.Now");
        Diagnostic multipleEnumerationDiagnostic = await CreateDiagnosticAtSourceLambdaAsync(
            document,
            AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule,
            "MultipleEnumeration",
            "Total",
            "Numbers",
            "Total",
            "Numbers");

        List<CodeAction> actions = await RegisterActionsAsync(document, nonDeterministicDiagnostic, multipleEnumerationDiagnostic);

        Assert.Contains(actions, action => action.Title.Contains("Cache 'Numbers' with ToList() for 'Total'", StringComparison.Ordinal));
        Assert.Contains(actions, action => action.Title.Contains("Ignore mapping for 'Total'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AM031_ShouldPreserveParenthesizedLambdaParameter_WhenApplyingCachingFix()
    {
        const string source = """
                              using AutoMapper;
                              using System.Collections.Generic;
                              using System.Linq;

                              namespace TestNamespace
                              {
                                  public class Source
                                  {
                                      public List<int> Numbers { get; set; } = new();
                                  }

                                  public class Destination
                                  {
                                      public int Total { get; set; }
                                  }

                                  public class TestProfile : Profile
                                  {
                                      public TestProfile()
                                      {
                                          CreateMap<Source, Destination>()
                                              .ForMember(dest => dest.Total, opt => opt.MapFrom((sourceValue) => sourceValue.Numbers.Sum() + sourceValue.Numbers.Average()));
                                      }
                                  }
                              }
                              """;

        Document document = CreateDocument(source);
        Diagnostic diagnostic = await CreateDiagnosticAtSourceLambdaAsync(
            document,
            AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule,
            "MultipleEnumeration",
            "Total",
            "Numbers",
            "Total",
            "Numbers");

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);
        CodeAction cacheAction = Assert.Single(
            actions,
            action => action.Title.Contains("Cache 'Numbers' with ToList() for 'Total'", StringComparison.Ordinal));

        string updatedCode = await ApplyActionAsync(cacheAction, document);
        Assert.Contains("numbersCache", updatedCode, StringComparison.Ordinal);
        Assert.Contains("sourceValue.Numbers.ToList()", updatedCode, StringComparison.Ordinal);
        Assert.Contains("numbersCache.Sum()", updatedCode, StringComparison.Ordinal);
        Assert.Contains("numbersCache.Average()", updatedCode, StringComparison.Ordinal);
        Assert.Contains("opt.MapFrom((sourceValue) =>", updatedCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AM031_ShouldNotRegisterFixes_ForNonAm031DiagnosticId()
    {
        const string source = """
                              using AutoMapper;

                              namespace TestNamespace
                              {
                                  public class Source
                                  {
                                      public int Score { get; set; }
                                  }

                                  public class Destination
                                  {
                                      public int Score { get; set; }
                                  }

                                  public class TestProfile : Profile
                                  {
                                      public TestProfile()
                                      {
                                          CreateMap<Source, Destination>()
                                              .ForMember(dest => dest.Score, opt => opt.MapFrom(src => src.Score));
                                      }
                                  }
                              }
                              """;

        Document document = CreateDocument(source);
        var nonAm031Descriptor = new DiagnosticDescriptor(
            "AM999",
            "Synthetic diagnostic",
            "Synthetic diagnostic",
            "Test",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
        var diagnostic = await CreateDiagnosticAtSourceLambdaAsync(
            document,
            nonAm031Descriptor,
            "ExpensiveOperation",
            "Score");

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);
        Assert.Empty(actions);
    }

    private static Document CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);

        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, "AM031Tests", "AM031Tests", LanguageNames.CSharp)
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

    private static async Task<Diagnostic> CreateDiagnosticAtSourceLambdaAsync(
        Document document,
        DiagnosticDescriptor descriptor,
        string issueType,
        string propertyName,
        string? collectionName = null,
        params object[] messageArgs)
    {
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        LambdaExpressionSyntax sourceLambda = root.DescendantNodes()
            .OfType<LambdaExpressionSyntax>()
            .Last();

        ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
            .Add("IssueType", issueType)
            .Add("PropertyName", propertyName);

        if (!string.IsNullOrEmpty(collectionName))
        {
            properties = properties.Add("CollectionName", collectionName);
        }

        return Diagnostic.Create(descriptor, sourceLambda.GetLocation(), properties, messageArgs);
    }

    private static async Task<List<CodeAction>> RegisterActionsAsync(Document document, params Diagnostic[] diagnostics)
    {
        var actions = new List<CodeAction>();
        var provider = new AM031_PerformanceWarningCodeFixProvider();

        var context = new CodeFixContext(
            document,
            diagnostics[0].Location.SourceSpan,
            diagnostics.ToImmutableArray(),
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);
        return actions;
    }

    private static async Task<string> ApplyActionAsync(CodeAction action, Document originalDocument)
    {
        ImmutableArray<CodeActionOperation> operations = await action.GetOperationsAsync(CancellationToken.None);
        ApplyChangesOperation applyChanges = Assert.IsType<ApplyChangesOperation>(operations.Single());

        Document updatedDocument = applyChanges.ChangedSolution.GetDocument(originalDocument.Id)!;
        SourceText updatedText = await updatedDocument.GetTextAsync();
        return updatedText.ToString();
    }
}

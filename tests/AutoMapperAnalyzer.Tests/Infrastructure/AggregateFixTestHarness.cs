using System.Collections.Immutable;
using System.IO;
using AutoMapper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace AutoMapperAnalyzer.Tests.Infrastructure;

/// <summary>
///     Shared helpers for testing aggregate / nested code-fix actions, which are multi-diagnostic and so
///     must be exercised with the full diagnostic set in one context (as the IDE does) rather than via the
///     per-diagnostic code-fix verifier.
/// </summary>
internal static class AggregateFixTestHarness
{
    public static Document CreateDocument(string source, string projectName)
    {
        var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);

        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, projectName, projectName, LanguageNames.CSharp)
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

    /// <summary>
    ///     Creates a document whose <c>Source</c>/<c>Destination</c> models come from a referenced
    ///     (compiled, metadata) assembly. Property-anchored rules (AM004/AM006) then anchor every diagnostic
    ///     to the CreateMap invocation (the model properties have no source location), reproducing the
    ///     metadata-type "pile-up" where aggregate fixes apply.
    /// </summary>
    public static Document CreateDocumentWithMetadataModels(
        string profileSource,
        string modelsSource,
        string projectName)
    {
        var trustedReferences = new List<MetadataReference>();
        string trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        foreach (string assemblyPath in trustedPlatformAssemblies.Split(Path.PathSeparator))
        {
            if (!string.IsNullOrWhiteSpace(assemblyPath))
            {
                trustedReferences.Add(MetadataReference.CreateFromFile(assemblyPath));
            }
        }

        CSharpCompilation modelsCompilation = CSharpCompilation.Create(
            $"{projectName}.Models",
            new[] { CSharpSyntaxTree.ParseText(modelsSource) },
            trustedReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var peStream = new MemoryStream();
        EmitResult emitResult = modelsCompilation.Emit(peStream);
        Assert.True(
            emitResult.Success,
            "Models assembly failed to compile: " + string.Join("; ", emitResult.Diagnostics));
        peStream.Position = 0;
        MetadataReference modelsReference = MetadataReference.CreateFromStream(peStream);

        var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);

        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, projectName, projectName, LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview))
            .AddMetadataReferences(projectId, trustedReferences)
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Profile).Assembly.Location))
            .AddMetadataReference(projectId, modelsReference)
            .AddDocument(documentId, "Test0.cs", SourceText.From(profileSource));

        return solution.GetDocument(documentId)!;
    }

    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync<TAnalyzer>(Document document)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        return await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new TAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
    }

    /// <summary>
    ///     Applies the aggregate action selected by <paramref name="equivalenceKey"/> against the full
    ///     diagnostic set and asserts the result matches <paramref name="expectedFixedCode"/> and clears
    ///     every diagnostic in a single edit.
    /// </summary>
    public static async Task AssertAggregateClearsAllAsync<TAnalyzer, TCodeFix>(
        Document document,
        string equivalenceKey,
        string expectedFixedCode)
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync<TAnalyzer>(document);
        Assert.True(diagnostics.Length >= 2, $"Expected 2+ diagnostics, got {diagnostics.Length}.");

        Document fixedDocument = await CodeFixActionInspector.ApplyActionByKeyAsync(
            document, new TCodeFix(), diagnostics, equivalenceKey);

        string actualFixedCode = (await fixedDocument.GetTextAsync()).ToString();
        Assert.Equal(Normalize(expectedFixedCode), Normalize(actualFixedCode));

        Assert.Empty(await GetDiagnosticsAsync<TAnalyzer>(fixedDocument));
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n");
}

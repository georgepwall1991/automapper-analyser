using System.Collections.Immutable;
using System.Composition;
using System.Text.RegularExpressions;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Analyzers.TypeSafety;

/// <summary>
///     Code fix provider for AM002 Nullable Compatibility diagnostics.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM002_NullableCompatibilityCodeFixProvider))]
[Shared]
public class AM002_NullableCompatibilityCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <summary>
    ///     Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM002");

    /// <summary>
    ///     Registers code fixes for the diagnostics.
    /// </summary>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return;
        }

        SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel == null)
        {
            return;
        }

        var handledInvocations = new HashSet<InvocationExpressionSyntax>();

        foreach (Diagnostic? diagnostic in context.Diagnostics)
        {
            if (diagnostic.Descriptor != AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            {
                continue;
            }

            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

            InvocationExpressionSyntax? invocation = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(i => i.Span.Contains(diagnosticSpan));

            if (invocation == null)
            {
                continue;
            }

            string? propertyName = ExtractPropertyNameFromDiagnostic(diagnostic);
            if (string.IsNullOrEmpty(propertyName))
            {
                continue;
            }

            // 1. Register Bulk Fixes (only once per invocation)
            if (handledInvocations.Add(invocation))
            {
                RegisterBulkFixes(context, invocation, semanticModel, root);
            }

            // 2. Register Grouped Per-Property Fixes
            RegisterPerPropertyFixes(context, diagnostic, invocation, propertyName!, root);
        }
    }

    private void RegisterPerPropertyFixes(CodeFixContext context, Diagnostic diagnostic,
        InvocationExpressionSyntax invocation, string propertyName, SyntaxNode root)
    {
        string? destinationType = ExtractDestinationTypeFromDiagnostic(diagnostic);
        string defaultValue = TypeConversionHelper.GetDefaultValueForType(destinationType ?? string.Empty);
        string sampleValue = TypeConversionHelper.GetSampleValueForType(destinationType ?? string.Empty);

        var nestedActions = ImmutableArray.CreateBuilder<CodeAction>();

        // Fix 1: Null coalescing with default value
        nestedActions.Add(CodeAction.Create(
            $"Use default value ({defaultValue})",
            cancellationToken =>
                AddMapFromAsync(context.Document, invocation, propertyName,
                    $"src.{propertyName} ?? {defaultValue}", cancellationToken),
            $"AM002_NullCoalescing_{propertyName}"));

        // Fix 2: Null coalescing with sample value (if different)
        if (sampleValue != defaultValue)
        {
            nestedActions.Add(CodeAction.Create(
                $"Use sample value ({sampleValue})",
                cancellationToken =>
                    AddMapFromAsync(context.Document, invocation, propertyName,
                        $"src.{propertyName} ?? {sampleValue}", cancellationToken),
                $"AM002_SampleValue_{propertyName}"));
        }

        // Fix 3: Make destination property nullable
        nestedActions.Add(CodeAction.Create(
            "Make destination property nullable",
            cancellationToken =>
                MakeDestinationNullableAsync(context.Document, invocation, propertyName, cancellationToken),
            $"AM002_MakeNullable_{propertyName}"));

        // Fix 4: Ignore property
        nestedActions.Add(CodeAction.Create(
            "Ignore property",
            cancellationToken =>
                AddIgnoreAsync(context.Document, invocation, propertyName, cancellationToken),
            $"AM002_Ignore_{propertyName}"));

        // Register grouped action using base class helper
        var groupAction = CreateGroupedAction($"Fix nullable issue for '{propertyName}'...", nestedActions);
        context.RegisterCodeFix(groupAction, diagnostic);
    }

    private void RegisterBulkFixes(CodeFixContext context, InvocationExpressionSyntax invocation,
        SemanticModel semanticModel, SyntaxNode root)
    {
        // Bulk Fix 1: Handle all with default values
        var defaultAction = CodeAction.Create(
            "Handle all nullable warnings with default values",
            cancellationToken => BulkFixAsync(context.Document, root, invocation, semanticModel, "Default"),
            "AM002_Bulk_Default");

        // Bulk Fix 2: Make all mismatched destination properties nullable
        var makeNullableAction = CodeAction.Create(
            "Make all mismatched destination properties nullable",
            cancellationToken => BulkFixAsync(context.Document, root, invocation, semanticModel, "MakeNullable"),
            "AM002_Bulk_MakeNullable");

        // Bulk Fix 3: Ignore all properties with nullable warnings
        var ignoreAction = CodeAction.Create(
            "Ignore all properties with nullable warnings",
            cancellationToken => BulkFixAsync(context.Document, root, invocation, semanticModel, "Ignore"),
            "AM002_Bulk_Ignore");

        // Register all bulk fixes using base class helper
        RegisterBulkFixes(context, defaultAction, makeNullableAction, ignoreAction);
    }

    private async Task<Solution> BulkFixAsync(Document document, SyntaxNode root, InvocationExpressionSyntax invocation,
        SemanticModel semanticModel, string mode)
    {
        // Identify all properties needing fix
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType == null || destType == null)
        {
            return document.Project.Solution;
        }

        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType);
        var destProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destType, false);

        var propertiesToFix = new List<(IPropertySymbol Source, IPropertySymbol Dest)>();

        foreach (var sourceProp in sourceProperties)
        {
            var destProp = destProperties.FirstOrDefault(p => p.Name == sourceProp.Name);
            if (destProp == null) continue;

            // Check if configured
            if (AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember(invocation, destProp.Name, semanticModel))
            {
                continue;
            }

            // Check nullable -> non-nullable condition
            bool isSourceNullable = IsNullableType(sourceProp.Type);
            bool isDestNullable = IsNullableType(destProp.Type);

            if (isSourceNullable && !isDestNullable)
            {
                propertiesToFix.Add((sourceProp, destProp));
            }
        }

        if (!propertiesToFix.Any())
        {
            return document.Project.Solution;
        }

        if (mode == "MakeNullable")
        {
            // Handle multiple property type changes in destination
            return await MakePropertiesNullableAsync(document, destType, propertiesToFix.Select(p => p.Dest));
        }

        // Handle profile modifications
        InvocationExpressionSyntax currentInvocation = invocation;

        foreach (var (sourceProp, destProp) in propertiesToFix)
        {
            if (mode == "Ignore")
            {
                currentInvocation = CodeFixSyntaxHelper.CreateForMemberWithIgnore(currentInvocation, destProp.Name);
            }
            else // Default
            {
                string defaultValue = TypeConversionHelper.GetDefaultValueForType(destProp.Type.ToDisplayString());
                string expression = $"src.{sourceProp.Name} ?? {defaultValue}";
                currentInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(currentInvocation, destProp.Name, expression);
            }
        }

        SyntaxNode newRoot = root.ReplaceNode(invocation, currentInvocation);
        Document newDocument = document.WithSyntaxRoot(newRoot);
        return newDocument.Project.Solution;
    }

    private async Task<Document> AddMapFromAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string propertyName,
        string mapFromExpression,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        InvocationExpressionSyntax newInvocation =
            CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName, mapFromExpression);
        return await ReplaceNodeAsync(document, root, invocation, newInvocation);
    }

    private async Task<Document> AddIgnoreAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string propertyName,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        InvocationExpressionSyntax newInvocation =
            CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
        return await ReplaceNodeAsync(document, root, invocation, newInvocation);
    }

    private async Task<Solution> MakeDestinationNullableAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel == null) return document.Project.Solution;

        var (_, destType) = AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
        if (destType == null) return document.Project.Solution;

        // Find the property symbol
        IPropertySymbol? property = AutoMapperAnalysisHelpers.GetMappableProperties(destType, false)
            .FirstOrDefault(p => p.Name == propertyName);

        if (property == null) return document.Project.Solution;

        return await MakePropertiesNullableAsync(document, destType, new[] { property });
    }

    private async Task<Solution> MakePropertiesNullableAsync(
        Document document,
        ITypeSymbol destType,
        IEnumerable<IPropertySymbol> propertiesToFix)
    {
        // Check if the destination type is source code (not metadata)
        if (destType.Locations.All(l => !l.IsInSource))
        {
             return document.Project.Solution;
        }

        var syntaxReference = destType.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxReference == null) return document.Project.Solution;

        var destSyntaxRoot = await syntaxReference.SyntaxTree.GetRootAsync();
        var destClassDecl = destSyntaxRoot.FindNode(syntaxReference.Span);

        // Might be class, struct, record
        if (destClassDecl == null) return document.Project.Solution;

        var editor = new SyntaxEditor(destSyntaxRoot, document.Project.Solution.Workspace.Services);

        foreach (var property in propertiesToFix)
        {
            var propSyntaxRef = property.DeclaringSyntaxReferences.FirstOrDefault();
            if (propSyntaxRef == null) continue;

            var propNode = await propSyntaxRef.GetSyntaxAsync();
            if (propNode is PropertyDeclarationSyntax propDecl)
            {
                TypeSyntax newType;
                if (propDecl.Type is NullableTypeSyntax)
                {
                    continue; // Already nullable
                }

                // If it's a value type (int, DateTime), wrap in Nullable<T> (T?)
                // If it's a reference type (string), just add ?
                // Syntax-wise, both are NullableTypeSyntax: T?

                newType = SyntaxFactory.NullableType(propDecl.Type.WithoutTrivia())
                    .WithTriviaFrom(propDecl.Type);

                editor.ReplaceNode(propDecl.Type, newType);
            }
        }

        var newDestRoot = editor.GetChangedRoot();
        var destDocument = document.Project.Solution.GetDocument(destSyntaxRoot.SyntaxTree);

        if (destDocument == null) return document.Project.Solution;

        return document.Project.Solution.WithDocumentSyntaxRoot(destDocument.Id, newDestRoot);
    }

    private string? ExtractPropertyNameFromDiagnostic(Diagnostic diagnostic)
    {
        // Try to get property name from diagnostic properties
        if (diagnostic.Properties.TryGetValue("PropertyName", out string? propertyName))
        {
            return propertyName;
        }

        // Fallback: extract from diagnostic message
        string message = diagnostic.GetMessage();
        Match match = Regex.Match(message, @"Property '(\w+)'");
        return match.Success ? match.Groups[1].Value : null;
    }

    private string? ExtractDestinationTypeFromDiagnostic(Diagnostic diagnostic)
    {
        // Try to get from diagnostic properties
        if (diagnostic.Properties.TryGetValue("DestType", out string? destType))
        {
            return destType;
        }

        // Fallback: extract from diagnostic message (e.g., "int?")
        string message = diagnostic.GetMessage();
        // Match pattern like "(...) is nullable" to extract the type before
        Match match = Regex.Match(message, @"\(([^)]+)\)\s+is non-nullable");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool IsNullableType(ITypeSymbol type)
    {
        // Duplicated helper logic to avoid dependency on internal Analyzer logic not exposed
        if (type.NullableAnnotation == NullableAnnotation.Annotated) return true;
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) return true;
        string typeString = type.ToDisplayString();
        return typeString.EndsWith("?");
    }
}

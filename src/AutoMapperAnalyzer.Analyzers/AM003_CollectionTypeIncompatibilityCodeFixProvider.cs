using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
/// Code fix provider for AM003 diagnostic - Collection Type Incompatibility.
/// Provides fixes for collection type mismatches and element type conversions.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM003_CollectionTypeIncompatibilityCodeFixProvider)), Shared]
public class AM003_CollectionTypeIncompatibilityCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ["AM003"];

    /// <summary>
    /// Gets the fix all provider for batch fixes.
    /// </summary>
    /// <returns>The batch fixer provider.</returns>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>
    /// Registers code fixes for the specified context.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue("PropertyName", out var propertyName) ||
                !diagnostic.Properties.TryGetValue("SourceType", out var sourceType) ||
                !diagnostic.Properties.TryGetValue("DestType", out var destType) ||
                !diagnostic.Properties.TryGetValue("SourceElementType", out var sourceElementType) ||
                !diagnostic.Properties.TryGetValue("DestElementType", out var destElementType) ||
                string.IsNullOrEmpty(propertyName) || string.IsNullOrEmpty(sourceType) ||
                string.IsNullOrEmpty(destType) || string.IsNullOrEmpty(sourceElementType) ||
                string.IsNullOrEmpty(destElementType))
            {
                continue;
            }

            var node = root.FindNode(diagnostic.Location.SourceSpan);
            if (node is not InvocationExpressionSyntax invocation)
            {
                continue;
            }

            // Check if this is a collection type incompatibility vs element type incompatibility
            bool isCollectionTypeIncompatibility = !string.IsNullOrEmpty(sourceType) &&
                (sourceType!.Contains("HashSet") || sourceType.Contains("Queue") || sourceType.Contains("Stack"));

            if (isCollectionTypeIncompatibility)
            {
                // Collection type incompatibility fixes
                RegisterCollectionTypeIncompatibilityFixes(context, root, invocation, propertyName!, sourceType!, destType!);
            }
            else
            {
                // Element type incompatibility fixes
                RegisterElementTypeIncompatibilityFixes(context, root, invocation, propertyName!, sourceElementType!, destElementType!);
            }
        }
    }

    private void RegisterCollectionTypeIncompatibilityFixes(CodeFixContext context, SyntaxNode root,
        InvocationExpressionSyntax invocation, string propertyName, string sourceType, string destType)
    {
        // Fix 1: Add ForMember with ToList() conversion
        if (sourceType.Contains("HashSet") && destType.Contains("List"))
        {
            var toListAction = CodeAction.Create(
                title: $"Convert {propertyName} using ToList()",
                createChangedDocument: cancellationToken =>
                {
                    var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                        invocation,
                        propertyName,
                        $"src.{propertyName}.ToList()");
                    var newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"ToList_{propertyName}");

            context.RegisterCodeFix(toListAction, context.Diagnostics);
        }

        // Fix 2: Add ForMember with ToArray() conversion
        if ((sourceType.Contains("Queue") || sourceType.Contains("Stack")) && destType.Contains("List"))
        {
            var toArrayAction = CodeAction.Create(
                title: $"Convert {propertyName} using ToArray()",
                createChangedDocument: cancellationToken =>
                {
                    var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                        invocation,
                        propertyName,
                        $"src.{propertyName}.ToArray()");
                    var newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"ToArray_{propertyName}");

            context.RegisterCodeFix(toArrayAction, context.Diagnostics);
        }

        // Fix 3: Add ForMember with specific collection constructor
        var constructorAction = CodeAction.Create(
            title: $"Convert {propertyName} using collection constructor",
            createChangedDocument: cancellationToken =>
            {
                string collectionType = GetCollectionTypeName(destType);
                var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                    invocation,
                    propertyName,
                    $"new {collectionType}(src.{propertyName})");
                var newRoot = root.ReplaceNode(invocation, newInvocation);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            equivalenceKey: $"Constructor_{propertyName}");

        context.RegisterCodeFix(constructorAction, context.Diagnostics);
    }

    private void RegisterElementTypeIncompatibilityFixes(CodeFixContext context, SyntaxNode root,
        InvocationExpressionSyntax invocation, string propertyName, string sourceElementType, string destElementType)
    {
        // Fix 1: Add ForMember with Select() for element type conversion
        var selectAction = CodeAction.Create(
            title: $"Convert {propertyName} elements using Select()",
            createChangedDocument: cancellationToken =>
            {
                string conversion = GetElementConversion(sourceElementType, destElementType);
                var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                    invocation,
                    propertyName,
                    $"src.{propertyName}.Select({conversion})");
                var newRoot = root.ReplaceNode(invocation, newInvocation);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            equivalenceKey: $"Select_{propertyName}");

        context.RegisterCodeFix(selectAction, context.Diagnostics);

        // Fix 2: Ignore the property
        var ignoreAction = CodeAction.Create(
            title: $"Ignore property '{propertyName}'",
            createChangedDocument: cancellationToken =>
            {
                var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
                var newRoot = root.ReplaceNode(invocation, newInvocation);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            equivalenceKey: $"Ignore_{propertyName}");

        context.RegisterCodeFix(ignoreAction, context.Diagnostics);
    }

    private string GetCollectionTypeName(string destType)
    {
        if (destType.Contains("List")) return "List<>";
        if (destType.Contains("HashSet")) return "HashSet<>";
        if (destType.Contains("Queue")) return "Queue<>";
        if (destType.Contains("Stack")) return "Stack<>";
        return "IEnumerable<>";
    }

    private string GetElementConversion(string sourceElementType, string destElementType)
    {
        // Generate a simple conversion lambda
        // This is a placeholder - in real scenarios might need more complex logic
        return $"x => x /* TODO: Convert from {sourceElementType} to {destElementType} */";
    }
}

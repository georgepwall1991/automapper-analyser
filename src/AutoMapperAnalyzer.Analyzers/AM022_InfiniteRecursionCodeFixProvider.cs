using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
/// Code fix provider for AM022 Infinite Recursion diagnostics.
/// Provides fixes for self-referencing types and circular reference risks.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM022_InfiniteRecursionCodeFixProvider)), Shared]
public class AM022_InfiniteRecursionCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM022");

    /// <summary>
    /// Gets whether this provider can fix multiple diagnostics in a single code action.
    /// </summary>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>
    /// Registers code fixes for the diagnostics.
    /// </summary>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel == null) return;

        foreach (var diagnostic in context.Diagnostics)
        {
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var invocation = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(i => i.Span.Contains(diagnosticSpan));

            if (invocation == null)
            {
                continue;
            }

            // Get the type arguments to identify self-referencing properties
            var createMapTypes = AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
            if (createMapTypes.Item1 == null || createMapTypes.Item2 == null)
            {
                continue;
            }

            // Find all self-referencing properties
            var selfReferencingProperties = FindSelfReferencingProperties(
                createMapTypes.Item1,
                createMapTypes.Item2);

            // Register fixes based on complexity:
            // - Single property: Ignore first (specific and simple)
            // - Multiple properties or none: MaxDepth first (simpler than ignoring all)

            if (selfReferencingProperties.Count == 1)
            {
                // Single property: offer Ignore first
                var propertyName = selfReferencingProperties[0];
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Ignore self-referencing property '{propertyName}'",
                        createChangedDocument: cancellationToken =>
                            AddIgnoreAsync(context.Document, invocation, propertyName, cancellationToken),
                        equivalenceKey: $"AM022_Ignore_{propertyName}"),
                    diagnostic);

                // Offer MaxDepth as alternative
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Add MaxDepth(2) to prevent infinite recursion",
                        createChangedDocument: cancellationToken =>
                            AddMaxDepthAsync(context.Document, invocation, cancellationToken),
                        equivalenceKey: "AM022_AddMaxDepth"),
                    diagnostic);
            }
            else
            {
                // Multiple properties or none: offer MaxDepth first (simpler)
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Add MaxDepth(2) to prevent infinite recursion",
                        createChangedDocument: cancellationToken =>
                            AddMaxDepthAsync(context.Document, invocation, cancellationToken),
                        equivalenceKey: "AM022_AddMaxDepth"),
                    diagnostic);

                if (selfReferencingProperties.Count > 1)
                {
                    // Offer to ignore all self-referencing properties as alternative
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: $"Ignore all {selfReferencingProperties.Count} self-referencing properties",
                            createChangedDocument: cancellationToken =>
                                AddIgnoreMultipleAsync(context.Document, invocation, selfReferencingProperties, cancellationToken),
                            equivalenceKey: "AM022_IgnoreAll"),
                        diagnostic);
                }
            }
        }
    }

    private static ImmutableList<string> FindSelfReferencingProperties(
        ITypeSymbol sourceType,
        ITypeSymbol destType)
    {
        var selfReferencingProps = ImmutableList.CreateBuilder<string>();

        foreach (var destProperty in destType.GetMembers().OfType<IPropertySymbol>())
        {
            if (destProperty.Type.Equals(destType, SymbolEqualityComparer.Default))
            {
                selfReferencingProps.Add(destProperty.Name);
            }
        }

        return selfReferencingProps.ToImmutable();
    }

    private static async Task<Document> AddMaxDepthAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Create .MaxDepth(2) invocation
        var maxDepthInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                invocation,
                SyntaxFactory.IdentifierName("MaxDepth")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.NumericLiteralExpression,
                            SyntaxFactory.Literal(2))))));

        var newRoot = root.ReplaceNode(invocation, maxDepthInvocation);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> AddIgnoreAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
        var newRoot = root.ReplaceNode(invocation, newInvocation);

        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> AddIgnoreMultipleAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        ImmutableList<string> propertyNames,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        var newInvocation = invocation;
        foreach (var propertyName in propertyNames)
        {
            newInvocation = CodeFixSyntaxHelper.CreateForMemberWithIgnore(newInvocation, propertyName);
        }

        var newRoot = root.ReplaceNode(invocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }
}

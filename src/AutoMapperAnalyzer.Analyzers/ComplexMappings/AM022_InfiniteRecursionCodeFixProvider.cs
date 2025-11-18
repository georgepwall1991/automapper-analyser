using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Analyzers.ComplexMappings;

/// <summary>
///     Code fix provider for AM022 Infinite Recursion diagnostics.
///     Provides fixes for self-referencing types and circular reference risks.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM022_InfiniteRecursionCodeFixProvider))]
[Shared]
public class AM022_InfiniteRecursionCodeFixProvider : CodeFixProvider
{
    /// <summary>
    ///     Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM022");

    /// <summary>
    ///     Gets whether this provider can fix multiple diagnostics in a single code action.
    /// </summary>
    public sealed override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

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

        SemanticModel? semanticModel =
            await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel == null)
        {
            return;
        }

        foreach (Diagnostic? diagnostic in context.Diagnostics)
        {
            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

            InvocationExpressionSyntax? invocation = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(i => i.Span.Contains(diagnosticSpan));

            if (invocation == null)
            {
                continue;
            }

            // Get the type arguments to identify self-referencing properties
            (ITypeSymbol? sourceType, ITypeSymbol? destType) createMapTypes =
                AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
            if (createMapTypes.Item1 == null || createMapTypes.Item2 == null)
            {
                continue;
            }

            // Find all self-referencing properties
            ImmutableList<string> selfReferencingProperties = FindSelfReferencingProperties(
                createMapTypes.Item1,
                createMapTypes.Item2);

            // Register fixes based on complexity:
            // - Single property: Ignore first (specific and simple)
            // - Multiple properties or none: MaxDepth first (simpler than ignoring all)

            if (selfReferencingProperties.Count == 1)
            {
                // Single property: offer Ignore first
                string propertyName = selfReferencingProperties[0];
                context.RegisterCodeFix(
                    CodeAction.Create(
                        $"Ignore self-referencing property '{propertyName}'",
                        cancellationToken =>
                            AddIgnoreAsync(context.Document, invocation, propertyName, cancellationToken),
                        $"AM022_Ignore_{propertyName}"),
                    diagnostic);

                // Offer MaxDepth as alternative
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "Add MaxDepth(2) to prevent infinite recursion",
                        cancellationToken =>
                            AddMaxDepthAsync(context.Document, invocation, cancellationToken),
                        "AM022_AddMaxDepth"),
                    diagnostic);
            }
            else
            {
                // Multiple properties or none: offer MaxDepth first (simpler)
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "Add MaxDepth(2) to prevent infinite recursion",
                        cancellationToken =>
                            AddMaxDepthAsync(context.Document, invocation, cancellationToken),
                        "AM022_AddMaxDepth"),
                    diagnostic);

                if (selfReferencingProperties.Count > 1)
                {
                    // Offer to ignore all self-referencing properties as alternative
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            $"Ignore all {selfReferencingProperties.Count} self-referencing properties",
                            cancellationToken =>
                                AddIgnoreMultipleAsync(context.Document, invocation, selfReferencingProperties,
                                    cancellationToken),
                            "AM022_IgnoreAll"),
                        diagnostic);
                }
            }
        }
    }

    private static ImmutableList<string> FindSelfReferencingProperties(
        ITypeSymbol sourceType,
        ITypeSymbol destType)
    {
        ImmutableList<string>.Builder selfReferencingProps = ImmutableList.CreateBuilder<string>();

        foreach (IPropertySymbol? destProperty in destType.GetMembers().OfType<IPropertySymbol>())
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
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return document;
        }

        // Create .MaxDepth(2) invocation
        InvocationExpressionSyntax maxDepthInvocation = SyntaxFactory.InvocationExpression(
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

        SyntaxNode newRoot = root.ReplaceNode(invocation, maxDepthInvocation);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> AddIgnoreAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string propertyName,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return document;
        }

        InvocationExpressionSyntax newInvocation =
            CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
        SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);

        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> AddIgnoreMultipleAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        ImmutableList<string> propertyNames,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return document;
        }

        InvocationExpressionSyntax newInvocation = invocation;
        foreach (string? propertyName in propertyNames)
        {
            newInvocation = CodeFixSyntaxHelper.CreateForMemberWithIgnore(newInvocation, propertyName);
        }

        SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }
}

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
public class AM022_InfiniteRecursionCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <summary>
    ///     Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM022");

    /// <summary>
    ///     Registers code fixes for the diagnostics.
    /// </summary>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        foreach (Diagnostic? diagnostic in context.Diagnostics)
        {
            InvocationExpressionSyntax? invocation = GetCreateMapInvocation(operationContext.Root, diagnostic);
            if (invocation == null)
            {
                continue;
            }

            // Get the type arguments to identify self-referencing properties
            (ITypeSymbol? sourceType, ITypeSymbol? destType) createMapTypes =
                AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, operationContext.SemanticModel);
            if (createMapTypes.Item1 == null || createMapTypes.Item2 == null)
            {
                continue;
            }

            // Find all self-referencing properties
            ImmutableList<string> selfReferencingProperties = FindSelfReferencingProperties(createMapTypes.Item2);

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
                            AddIgnoreAsync(context.Document, operationContext.Root, invocation, propertyName),
                        $"AM022_Ignore_{propertyName}"),
                    diagnostic);

                // Offer MaxDepth as alternative
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "Add MaxDepth(2) to prevent infinite recursion",
                        cancellationToken =>
                            AddMaxDepthAsync(context.Document, operationContext.Root, invocation),
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
                            AddMaxDepthAsync(context.Document, operationContext.Root, invocation),
                        "AM022_AddMaxDepth"),
                    diagnostic);

                if (selfReferencingProperties.Count > 1)
                {
                    // Offer to ignore all self-referencing properties as alternative
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            $"Ignore all {selfReferencingProperties.Count} self-referencing properties",
                            cancellationToken =>
                                AddIgnoreMultipleAsync(context.Document, operationContext.Root, invocation,
                                    selfReferencingProperties),
                            "AM022_IgnoreAll"),
                        diagnostic);
                }
            }
        }
    }

    private static ImmutableList<string> FindSelfReferencingProperties(ITypeSymbol destType)
    {
        var selfReferencingProps = new HashSet<string>();
        IEnumerable<IPropertySymbol> destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(destType, requireSetter: false);

        foreach (IPropertySymbol? destProperty in destinationProperties)
        {
            if (destProperty.Type.Equals(destType, SymbolEqualityComparer.Default))
            {
                selfReferencingProps.Add(destProperty.Name);
                continue;
            }

            ITypeSymbol? elementType = AutoMapperAnalysisHelpers.GetCollectionElementType(destProperty.Type);
            if (elementType != null && elementType.Equals(destType, SymbolEqualityComparer.Default))
            {
                selfReferencingProps.Add(destProperty.Name);
            }
        }

        return selfReferencingProps.ToImmutableList();
    }

    private Task<Document> AddMaxDepthAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation)
    {
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

        return ReplaceNodeAsync(document, root, invocation, maxDepthInvocation);
    }

    private Task<Document> AddIgnoreAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        string propertyName)
    {
        InvocationExpressionSyntax newInvocation =
            CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
        return ReplaceNodeAsync(document, root, invocation, newInvocation);
    }

    private Task<Document> AddIgnoreMultipleAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        ImmutableList<string> propertyNames)
    {
        InvocationExpressionSyntax newInvocation = invocation;
        foreach (string? propertyName in propertyNames)
        {
            newInvocation = CodeFixSyntaxHelper.CreateForMemberWithIgnore(newInvocation, propertyName);
        }

        return ReplaceNodeAsync(document, root, invocation, newInvocation);
    }
}

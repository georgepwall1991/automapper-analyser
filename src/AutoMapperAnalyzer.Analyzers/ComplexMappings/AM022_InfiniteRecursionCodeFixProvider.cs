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

            // Get the type arguments to identify cycle-breaking properties
            (ITypeSymbol? sourceType, ITypeSymbol? destType) createMapTypes =
                AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, operationContext.SemanticModel);
            if (createMapTypes.Item1 == null || createMapTypes.Item2 == null)
            {
                continue;
            }

            // Analyzer graph edges (includes self-ref + multi-type cycle properties).
            ImmutableList<string> cycleProperties = FindCycleBreakingProperties(
                createMapTypes.Item1,
                createMapTypes.Item2,
                invocation,
                operationContext.SemanticModel);
            InvocationExpressionSyntax ignoreInsertionPoint = FindIgnoreInsertionPoint(
                invocation,
                operationContext.SemanticModel);

            // Best-first: MaxDepth scaffold first (consistent single- and multi-property), then Ignore.
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add MaxDepth(2) scaffold (review depth)",
                    cancellationToken =>
                        AddMaxDepthAsync(context.Document, operationContext.Root, invocation),
                    "AM022_AddMaxDepth"),
                diagnostic);

            if (cycleProperties.Count == 1)
            {
                string propertyName = cycleProperties[0];
                context.RegisterCodeFix(
                    CodeAction.Create(
                        $"Ignore circular property '{propertyName}' (manual review)",
                        cancellationToken =>
                            AddIgnoreAsync(
                                context.Document,
                                operationContext.Root,
                                ignoreInsertionPoint,
                                propertyName),
                        $"AM022_Ignore_{propertyName}"),
                    diagnostic);
            }
            else if (cycleProperties.Count > 1)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        $"Ignore all {cycleProperties.Count} circular properties (manual review)",
                        cancellationToken =>
                            AddIgnoreMultipleAsync(context.Document, operationContext.Root, ignoreInsertionPoint,
                                cycleProperties),
                        "AM022_IgnoreAll"),
                    diagnostic);
            }
        }
    }

    private static ImmutableList<string> FindCycleBreakingProperties(
        ITypeSymbol sourceType,
        ITypeSymbol destType,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        // Prefer the same graph edges the analyzer uses for Ignore suppression so the lightbulb
        // cannot suggest a self-ref Ignore that leaves a multi-type cycle diagnostic alive.
        if (sourceType is INamedTypeSymbol namedSource && destType is INamedTypeSymbol namedDest)
        {
            CreateMapRegistry registry = CreateMapRegistry.FromCompilation(semanticModel.Compilation);
            HashSet<string> recursive = AM022_InfiniteRecursionAnalyzer
                .FindRecursiveDestinationProperties(
                    namedSource,
                    namedDest,
                    registry,
                    invocation,
                    semanticModel);
            if (recursive.Count > 0)
            {
                return recursive.OrderBy(name => name, StringComparer.Ordinal).ToImmutableList();
            }
        }

        // Fallback: destination same-type self-references when the graph walk is empty.
        return FindSelfReferencingProperties(destType);
    }

    private static InvocationExpressionSyntax FindIgnoreInsertionPoint(
        InvocationExpressionSyntax createMapInvocation,
        SemanticModel semanticModel)
    {
        foreach (InvocationExpressionSyntax chainedInvocation in MappingChainAnalysisHelper
                     .GetScopedChainInvocations(
                         createMapInvocation,
                         semanticModel,
                         stopAtReverseMapBoundary: true)
                     .Reverse())
        {
            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(chainedInvocation);
            IEnumerable<IMethodSymbol> methodSymbols = symbolInfo.Symbol is IMethodSymbol method
                ? new[] { method }
                : symbolInfo.CandidateSymbols.OfType<IMethodSymbol>();

            if (methodSymbols.Any(candidate =>
                    MappingChainAnalysisHelper.IsAutoMapperMethod(candidate, candidate.Name)))
            {
                return chainedInvocation;
            }
        }

        return createMapInvocation;
    }

    private static ImmutableList<string> FindSelfReferencingProperties(ITypeSymbol destType)
    {
        var selfReferencingProps = new HashSet<string>(StringComparer.Ordinal);
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

        return selfReferencingProps.OrderBy(name => name, StringComparer.Ordinal).ToImmutableList();
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

using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.DataIntegrity;

/// <summary>
///     Code fix provider for AM006 diagnostic - Unmapped Destination Property.
///     Provides fixes for destination properties that have no corresponding source property or explicit mapping.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM006_UnmappedDestinationPropertyCodeFixProvider))]
[Shared]
public class AM006_UnmappedDestinationPropertyCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <summary>
    ///     Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ["AM006"];

    /// <summary>
    ///     Registers code fixes for the specified context.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        return RegisterGroupedPerPropertyFixesAsync(
            context,
            ["PropertyName", "PropertyType", "DestinationTypeName", "SourceTypeName"],
            "Fix individual destination property…",
            (operationContext, group, _, properties) =>
                BuildPerPropertyActions(context, operationContext, group, properties["PropertyName"]),
            (operationContext, group) => BuildAggregateActions(context, operationContext, group));
    }

    private (string SubMenuTitle, ImmutableArray<CodeAction> Actions)? BuildPerPropertyActions(
        CodeFixContext context,
        CodeFixOperationContext operationContext,
        DiagnosticInvocationGroup group,
        string propertyName)
    {
        InvocationExpressionSyntax invocation = group.Invocation;
        SyntaxNode root = operationContext.Root;
        var actions = ImmutableArray.CreateBuilder<CodeAction>();

        IPropertySymbol? bestMatch = FindFuzzySourceMatch(invocation, operationContext.SemanticModel, propertyName);
        if (bestMatch != null)
        {
            string sourceName = bestMatch.Name;
            string sourceExpression = $"src.{CodeFixSyntaxHelper.EscapeIdentifier(sourceName)}";
            actions.Add(CodeAction.Create(
                $"Map '{propertyName}' from similar source property '{sourceName}'",
                _ => ReplaceNodeAsync(
                    context.Document, root, invocation,
                    CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName, sourceExpression)),
                $"AM006_FuzzyMatch_{propertyName}_{sourceName}"));
        }

        actions.Add(CodeAction.Create(
            $"Ignore destination property '{propertyName}' (manual review)",
            _ => ReplaceNodeAsync(
                context.Document, root, invocation,
                CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName)),
            $"AM006_Ignore_{propertyName}"));

        return ($"Destination property '{propertyName}'", actions.ToImmutable());
    }

    private ImmutableArray<CodeAction> BuildAggregateActions(
        CodeFixContext context,
        CodeFixOperationContext operationContext,
        DiagnosticInvocationGroup group)
    {
        // Reverse-aware: a ReverseMap() diagnostic resolves its (swapped) types from the forward CreateMap.
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            ResolveCreateMapTypesWithReverse(group.Invocation, operationContext.SemanticModel);
        if (destType == null)
        {
            return ImmutableArray<CodeAction>.Empty;
        }

        var flaggedNames = new HashSet<string>(group.PropertyNames);
        List<IPropertySymbol> orderedProperties = AutoMapperAnalysisHelpers
            .GetMappableProperties(destType, requireGetter: false, requireSetter: true)
            .Where(p => flaggedNames.Contains(p.Name))
            .ToList();
        if (orderedProperties.Count < 2)
        {
            return ImmutableArray<CodeAction>.Empty;
        }

        int count = orderedProperties.Count;
        InvocationExpressionSyntax invocation = group.Invocation;
        SyntaxNode root = operationContext.Root;
        var actions = ImmutableArray.CreateBuilder<CodeAction>();

        // "Map all" only when every flagged property has a unique fuzzy source match, so the title is honest.
        List<IPropertySymbol> sourceProperties = sourceType == null
            ? new List<IPropertySymbol>()
            : AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false).ToList();
        var matches = orderedProperties
            .Select(p => (Property: p,
                Match: FuzzyMatchHelper.FindUniqueBestFuzzyMatch(p.Name, sourceProperties, p.Type)))
            .ToList();

        if (sourceType != null && matches.All(m => m.Match != null))
        {
            List<PropertyFixSpec> mapSpecs = matches
                .Select(m => PropertyFixSpec.MapFrom(
                    m.Property.Name, $"src.{CodeFixSyntaxHelper.EscapeIdentifier(m.Match!.Name)}"))
                .ToList();
            actions.Add(CodeAction.Create(
                $"Map all {count} from similar source properties",
                _ => ReplaceNodeAsync(
                    context.Document, root, invocation, FoldAggregateForMembers(invocation, mapSpecs)),
                "AM006_MapAll"));
        }

        List<PropertyFixSpec> ignoreSpecs = orderedProperties
            .Select(p => PropertyFixSpec.Ignore(p.Name))
            .ToList();
        actions.Add(CodeAction.Create(
            $"Ignore all {count} unmapped destination properties",
            _ => ReplaceNodeAsync(
                context.Document, root, invocation, FoldAggregateForMembers(invocation, ignoreSpecs)),
            "AM006_IgnoreAll"));

        return actions.ToImmutable();
    }

    private static IPropertySymbol? FindFuzzySourceMatch(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string propertyName)
    {
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            ResolveCreateMapTypesWithReverse(invocation, semanticModel);
        if (sourceType == null || destType == null)
        {
            return null;
        }

        IPropertySymbol? destinationProperty = AutoMapperAnalysisHelpers
            .GetMappableProperties(destType, requireGetter: false, requireSetter: true)
            .FirstOrDefault(p => p.Name == propertyName);
        if (destinationProperty == null)
        {
            return null;
        }

        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        return FuzzyMatchHelper.FindUniqueBestFuzzyMatch(propertyName, sourceProperties, destinationProperty.Type);
    }
}

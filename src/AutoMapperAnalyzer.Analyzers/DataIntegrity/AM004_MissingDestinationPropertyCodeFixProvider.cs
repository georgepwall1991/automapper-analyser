using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.DataIntegrity;

/// <summary>
///     Code fix provider for AM004 diagnostic - Missing Destination Property.
///     Provides fixes for source properties that don't have corresponding destination properties.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM004_MissingDestinationPropertyCodeFixProvider))]
[Shared]
public class AM004_MissingDestinationPropertyCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <summary>
    ///     Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ["AM004"];

    private sealed class MappingAnalysisContext
    {
        public InvocationExpressionSyntax MappingInvocation { get; }
        public ITypeSymbol SourceType { get; }
        public ITypeSymbol DestinationType { get; }
        public bool StopAtReverseMapBoundary { get; }

        public MappingAnalysisContext(
            InvocationExpressionSyntax mappingInvocation,
            ITypeSymbol sourceType,
            ITypeSymbol destinationType,
            bool stopAtReverseMapBoundary)
        {
            MappingInvocation = mappingInvocation;
            SourceType = sourceType;
            DestinationType = destinationType;
            StopAtReverseMapBoundary = stopAtReverseMapBoundary;
        }
    }

    /// <summary>
    ///     Registers code fixes for the specified context.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        return RegisterGroupedPerPropertyFixesAsync(
            context,
            ["PropertyName", "PropertyType", "SourceTypeName", "DestinationTypeName", "IsReverseMap"],
            "Fix individual source property…",
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

        if (TryResolveMappingContext(invocation, operationContext.SemanticModel, out MappingAnalysisContext? mappingContext))
        {
            IPropertySymbol? bestMatch = FindFuzzyDestinationMatch(mappingContext!, propertyName);
            if (bestMatch != null)
            {
                string destName = bestMatch.Name;
                string sourceExpression = $"src.{CodeFixSyntaxHelper.EscapeIdentifier(propertyName)}";
                actions.Add(CodeAction.Create(
                    $"Map '{propertyName}' to similar property '{destName}'",
                    _ => ReplaceNodeAsync(
                        context.Document, root, invocation,
                        CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, destName, sourceExpression)),
                    $"AM004_FuzzyMatch_{propertyName}_{destName}"));
            }
        }

        actions.Add(CodeAction.Create(
            $"Suppress source validation for '{propertyName}' with DoNotValidate() (manual review)",
            _ => ReplaceNodeAsync(
                context.Document, root, invocation,
                CodeFixSyntaxHelper.CreateForSourceMemberWithDoNotValidate(invocation, propertyName)),
            $"AM004_DoNotValidate_{propertyName}"));

        return ($"Source property '{propertyName}'", actions.ToImmutable());
    }

    private ImmutableArray<CodeAction> BuildAggregateActions(
        CodeFixContext context,
        CodeFixOperationContext operationContext,
        DiagnosticInvocationGroup group)
    {
        if (!TryResolveMappingContext(group.Invocation, operationContext.SemanticModel, out MappingAnalysisContext? mappingContext))
        {
            return ImmutableArray<CodeAction>.Empty;
        }

        var flaggedNames = new HashSet<string>(group.PropertyNames);
        List<IPropertySymbol> orderedSourceProperties = AutoMapperAnalysisHelpers
            .GetMappableProperties(mappingContext!.SourceType, requireSetter: false)
            .Where(p => flaggedNames.Contains(p.Name))
            .ToList();
        if (orderedSourceProperties.Count < 2)
        {
            return ImmutableArray<CodeAction>.Empty;
        }

        int count = orderedSourceProperties.Count;
        InvocationExpressionSyntax invocation = group.Invocation;
        SyntaxNode root = operationContext.Root;
        var actions = ImmutableArray.CreateBuilder<CodeAction>();

        // "Map all" only when every flagged source property has a unique-best fuzzy destination match.
        // Ties (same Levenshtein distance) withhold the action so aggregate rewrites never invent a mapping.
        List<IPropertySymbol> destProperties = AutoMapperAnalysisHelpers
            .GetMappableProperties(mappingContext.DestinationType, requireSetter: true)
            .ToList();
        var matches = orderedSourceProperties
            .Select(p => (Source: p,
                Match: FuzzyMatchHelper.FindUniqueBestFuzzyMatch(p.Name, destProperties, p.Type)))
            .ToList();

        if (matches.All(m => m.Match != null))
        {
            List<PropertyFixSpec> mapSpecs = matches
                .Select(m => PropertyFixSpec.MapFrom(
                    m.Match!.Name, $"src.{CodeFixSyntaxHelper.EscapeIdentifier(m.Source.Name)}"))
                .ToList();
            actions.Add(CodeAction.Create(
                $"Map all {count} to similar destination properties",
                _ => ReplaceNodeAsync(
                    context.Document, root, invocation, FoldAggregateForMembers(invocation, mapSpecs)),
                "AM004_MapAll"));
        }

        List<PropertyFixSpec> doNotValidateSpecs = orderedSourceProperties
            .Select(p => PropertyFixSpec.DoNotValidate(p.Name))
            .ToList();
        actions.Add(CodeAction.Create(
            $"Suppress validation for all {count} source properties (DoNotValidate)",
            _ => ReplaceNodeAsync(
                context.Document, root, invocation, FoldAggregateForMembers(invocation, doNotValidateSpecs)),
            "AM004_DoNotValidateAll"));

        return actions.ToImmutable();
    }

    private static IPropertySymbol? FindFuzzyDestinationMatch(MappingAnalysisContext mappingContext, string propertyName)
    {
        IPropertySymbol? sourcePropertySymbol = AutoMapperAnalysisHelpers
            .GetMappableProperties(mappingContext.SourceType, requireSetter: false)
            .FirstOrDefault(p => p.Name == propertyName);
        if (sourcePropertySymbol == null)
        {
            return null;
        }

        var destProperties = AutoMapperAnalysisHelpers.GetMappableProperties(
            mappingContext.DestinationType, requireSetter: true);
        // Require a unique lowest-distance destination (same gate as AM006/AM011). First-match
        // ordering is order-dependent and can map a source property to the wrong destination.
        return FuzzyMatchHelper.FindUniqueBestFuzzyMatch(
            propertyName, destProperties, sourcePropertySymbol.Type);
    }

    private static bool TryResolveMappingContext(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out MappingAnalysisContext? mappingContext)
    {
        mappingContext = null;

        // Reverse-map diagnostic location
        if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "ReverseMap"))
        {
            InvocationExpressionSyntax? createMapInvocation = FindForwardCreateMapInvocation(invocation, semanticModel);
            if (createMapInvocation == null)
            {
                return false;
            }

            var forwardTypes = MappingChainAnalysisHelper.GetCreateMapTypeArguments(createMapInvocation, semanticModel);
            if (forwardTypes.sourceType == null || forwardTypes.destinationType == null)
            {
                return false;
            }

            mappingContext = new MappingAnalysisContext(
                invocation,
                forwardTypes.destinationType,
                forwardTypes.sourceType,
                false);
            return true;
        }

        // Forward-map diagnostic location
        if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "CreateMap"))
        {
            var forwardTypes = MappingChainAnalysisHelper.GetCreateMapTypeArguments(invocation, semanticModel);
            if (forwardTypes.sourceType == null || forwardTypes.destinationType == null)
            {
                return false;
            }

            mappingContext = new MappingAnalysisContext(
                invocation,
                forwardTypes.sourceType,
                forwardTypes.destinationType,
                true);
            return true;
        }

        return false;
    }

    private static InvocationExpressionSyntax? FindForwardCreateMapInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        // Walk up the fluent chain to find CreateMap (ancestor direction)
        var current = invocation;
        while (current != null)
        {
            if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(current, semanticModel, "CreateMap"))
                return current;
            current = (current.Expression as MemberAccessExpressionSyntax)?.Expression as InvocationExpressionSyntax;
        }

        // Also check descendants (for cases where invocation IS the CreateMap chain)
        return invocation.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(node => MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(node, semanticModel, "CreateMap"));
    }
}

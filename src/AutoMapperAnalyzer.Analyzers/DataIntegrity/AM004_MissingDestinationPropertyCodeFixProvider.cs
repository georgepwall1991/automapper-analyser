using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

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
    ///     Registers code fixes. Recomputes all unmapped source properties for the CreateMap so
    ///     Map-all / DoNotValidate-all work from a single property-token caret (same-document).
    /// </summary>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        CodeFixOperationContext? operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        var processedInvocations = new HashSet<TextSpan>();
        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            Dictionary<string, string>? properties = TryGetDiagnosticProperties(
                diagnostic,
                "PropertyName",
                "PropertyType",
                "SourceTypeName",
                "DestinationTypeName",
                "IsReverseMap");
            if (properties == null)
            {
                continue;
            }

            InvocationExpressionSyntax? invocation =
                GetCreateMapInvocation(operationContext.Root, diagnostic, properties);
            if (invocation == null ||
                !TryResolveMappingContext(invocation, operationContext.SemanticModel, out MappingAnalysisContext? mappingContext))
            {
                continue;
            }

            List<IPropertySymbol> unmapped = FindUnmappedSourceProperties(
                mappingContext!,
                operationContext.SemanticModel);

            IPropertySymbol? diagnosticProperty = unmapped.FirstOrDefault(
                p => string.Equals(p.Name, properties["PropertyName"], StringComparison.Ordinal));
            if (diagnosticProperty == null)
            {
                // Stale diagnostic — withhold rather than inventing a second suppress action.
                continue;
            }

            if (unmapped.Count < 2)
            {
                (string title, ImmutableArray<CodeAction> actions)? built =
                    BuildPerPropertyActions(context, operationContext, mappingContext!, diagnosticProperty.Name);
                if (built == null)
                {
                    continue;
                }

                foreach (CodeAction action in built.Value.actions)
                {
                    context.RegisterCodeFix(action, diagnostic);
                }

                continue;
            }

            // Multi-property map: register aggregates once per CreateMap, even when several
            // property-token diagnostics share the same IDE caret context.
            if (!processedInvocations.Add(invocation.Span))
            {
                continue;
            }

            foreach (CodeAction aggregate in BuildAggregateActions(
                         context, operationContext, mappingContext!, unmapped))
            {
                context.RegisterCodeFix(aggregate, diagnostic);
            }

            var perPropertySubMenus = new List<CodeAction>();
            foreach (IPropertySymbol sourceProperty in unmapped)
            {
                (string title, ImmutableArray<CodeAction> actions)? built =
                    BuildPerPropertyActions(context, operationContext, mappingContext!, sourceProperty.Name);
                if (built == null || built.Value.actions.IsDefaultOrEmpty)
                {
                    continue;
                }

                perPropertySubMenus.Add(
                    CodeAction.Create(built.Value.title, built.Value.actions, isInlinable: false));
            }

            if (perPropertySubMenus.Count > 0)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "Fix individual source property…",
                        perPropertySubMenus.ToImmutableArray(),
                        isInlinable: false),
                    diagnostic);
            }
        }
    }

    private static List<IPropertySymbol> FindUnmappedSourceProperties(
        MappingAnalysisContext mappingContext,
        SemanticModel semanticModel)
    {
        if (MappingChainAnalysisHelper.HasCustomConstructionOrConversion(
                mappingContext.MappingInvocation,
                semanticModel,
                mappingContext.StopAtReverseMapBoundary))
        {
            return [];
        }

        return MappingChainAnalysisHelper.GetUnmappedSourceProperties(
            mappingContext.MappingInvocation,
            mappingContext.SourceType,
            mappingContext.DestinationType,
            semanticModel,
            mappingContext.StopAtReverseMapBoundary);
    }

    private (string SubMenuTitle, ImmutableArray<CodeAction> Actions)? BuildPerPropertyActions(
        CodeFixContext context,
        CodeFixOperationContext operationContext,
        MappingAnalysisContext mappingContext,
        string propertyName)
    {
        InvocationExpressionSyntax invocation = mappingContext.MappingInvocation;
        SyntaxNode root = operationContext.Root;
        var actions = ImmutableArray.CreateBuilder<CodeAction>();

        IPropertySymbol? bestMatch = FindFuzzyDestinationMatch(mappingContext, propertyName);
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
        MappingAnalysisContext mappingContext,
        List<IPropertySymbol> orderedSourceProperties)
    {
        if (orderedSourceProperties.Count < 2)
        {
            return ImmutableArray<CodeAction>.Empty;
        }

        int count = orderedSourceProperties.Count;
        InvocationExpressionSyntax invocation = mappingContext.MappingInvocation;
        SyntaxNode root = operationContext.Root;
        var actions = ImmutableArray.CreateBuilder<CodeAction>();

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
            $"Suppress validation for all {count} source properties (DoNotValidate) (manual review)",
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
        return FuzzyMatchHelper.FindUniqueBestFuzzyMatch(
            propertyName, destProperties, sourcePropertySymbol.Type);
    }

    private static bool TryResolveMappingContext(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out MappingAnalysisContext? mappingContext)
    {
        mappingContext = null;

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
        var current = invocation;
        while (current != null)
        {
            if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(current, semanticModel, "CreateMap"))
                return current;
            current = (current.Expression as MemberAccessExpressionSyntax)?.Expression as InvocationExpressionSyntax;
        }

        return invocation.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(node => MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(node, semanticModel, "CreateMap"));
    }
}

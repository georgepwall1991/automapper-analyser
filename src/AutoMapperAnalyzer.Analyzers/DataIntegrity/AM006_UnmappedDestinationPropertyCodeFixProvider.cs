using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

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
    ///     Registers code fixes. Recomputes all unmapped destination properties for the CreateMap so
    ///     Map-all / Ignore-all work from a single property-token caret (same-document).
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
                "DestinationTypeName",
                "SourceTypeName");
            if (properties == null)
            {
                continue;
            }

            InvocationExpressionSyntax? invocation =
                GetCreateMapInvocation(operationContext.Root, diagnostic, properties);
            if (invocation == null)
            {
                continue;
            }

            List<IPropertySymbol> unmapped = FindUnmappedDestinationProperties(
                invocation,
                operationContext.SemanticModel);

            IPropertySymbol? diagnosticProperty = unmapped.FirstOrDefault(
                p => string.Equals(p.Name, properties["PropertyName"], StringComparison.Ordinal));
            if (diagnosticProperty == null)
            {
                continue;
            }

            if (unmapped.Count < 2)
            {
                (string title, ImmutableArray<CodeAction> actions)? built =
                    BuildPerPropertyActions(context, operationContext, invocation, diagnosticProperty.Name);
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

            if (!processedInvocations.Add(invocation.Span))
            {
                continue;
            }

            foreach (CodeAction aggregate in BuildAggregateActions(
                         context, operationContext, invocation, unmapped))
            {
                context.RegisterCodeFix(aggregate, diagnostic);
            }

            var perPropertySubMenus = new List<CodeAction>();
            foreach (IPropertySymbol destProperty in unmapped)
            {
                (string title, ImmutableArray<CodeAction> actions)? built =
                    BuildPerPropertyActions(context, operationContext, invocation, destProperty.Name);
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
                        "Fix individual destination property…",
                        perPropertySubMenus.ToImmutableArray(),
                        isInlinable: false),
                    diagnostic);
            }
        }
    }

    private static List<IPropertySymbol> FindUnmappedDestinationProperties(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) =
            ResolveCreateMapTypesWithReverse(invocation, semanticModel);
        if (sourceType == null || destinationType == null)
        {
            return [];
        }

        bool isReverseMap = MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
            invocation, semanticModel, "ReverseMap");

        return AM006_UnmappedDestinationPropertyAnalyzer.GetUnmappedDestinationProperties(
            invocation,
            sourceType,
            destinationType,
            semanticModel,
            stopAtReverseMapBoundary: !isReverseMap);
    }

    private (string SubMenuTitle, ImmutableArray<CodeAction> Actions)? BuildPerPropertyActions(
        CodeFixContext context,
        CodeFixOperationContext operationContext,
        InvocationExpressionSyntax invocation,
        string propertyName)
    {
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
        InvocationExpressionSyntax invocation,
        List<IPropertySymbol> orderedProperties)
    {
        if (orderedProperties.Count < 2)
        {
            return ImmutableArray<CodeAction>.Empty;
        }

        (ITypeSymbol? sourceType, ITypeSymbol? _) =
            ResolveCreateMapTypesWithReverse(invocation, operationContext.SemanticModel);

        int count = orderedProperties.Count;
        SyntaxNode root = operationContext.Root;
        var actions = ImmutableArray.CreateBuilder<CodeAction>();

        List<IPropertySymbol> sourceProperties = sourceType == null
            ? []
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
            $"Ignore all {count} unmapped destination properties (manual review)",
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

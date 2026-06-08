using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.DataIntegrity;

/// <summary>
///     Code fix provider for AM011 diagnostic - Unmapped Required Property.
///     Provides fixes for required properties that are not mapped from source.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM011_UnmappedRequiredPropertyCodeFixProvider))]
[Shared]
public class AM011_UnmappedRequiredPropertyCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ["AM011"];

    /// <inheritdoc />
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        return RegisterGroupedPerPropertyFixesAsync(
            context,
            ["PropertyName", "PropertyType"],
            "Fix individual required property…",
            (operationContext, group, _, properties) =>
            {
                (CodeAction primary, CodeAction ignore) = BuildPerPropertyActions(
                    context.Document,
                    operationContext.Root,
                    operationContext.SemanticModel,
                    group.Invocation,
                    properties["PropertyName"],
                    properties["PropertyType"]);

                return ($"Required property '{properties["PropertyName"]}'", ImmutableArray.Create(primary, ignore));
            },
            (operationContext, group) => BuildAggregateActions(context, operationContext, group));
    }

    private (CodeAction Primary, CodeAction Ignore) BuildPerPropertyActions(
        Document document,
        SyntaxNode root,
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        string propertyName,
        string propertyType)
    {
        // Try to find best fuzzy match (forward-map types only, matching the original per-property behaviour).
        IPropertySymbol? bestFuzzyMatch = null;
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            MappingChainAnalysisHelper.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType != null)
        {
            var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
            IPropertySymbol? destinationProperty = destType == null
                ? null
                : AutoMapperAnalysisHelpers.GetMappableProperties(destType, false)
                    .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

            if (destinationProperty != null)
            {
                bestFuzzyMatch =
                    FuzzyMatchHelper.FindUniqueBestFuzzyMatch(propertyName, sourceProperties, destinationProperty.Type);
            }
        }

        // Option 1 (primary): fuzzy match if found, else default value.
        CodeAction primary;
        if (bestFuzzyMatch != null)
        {
            string fuzzyName = bestFuzzyMatch.Name;
            string sourceExpression = $"src.{CodeFixSyntaxHelper.EscapeIdentifier(fuzzyName)}";
            primary = CodeAction.Create(
                $"Map from similar property '{fuzzyName}'",
                _ => ReplaceNodeAsync(
                    document, root, invocation,
                    CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName, sourceExpression)),
                $"AM011_FuzzyMatch_{propertyName}_{fuzzyName}");
        }
        else
        {
            string defaultValue = TypeConversionHelper.GetDefaultValueForType(propertyType);
            primary = CodeAction.Create(
                $"Scaffold default mapping for '{propertyName}' ({defaultValue})",
                _ => ReplaceNodeAsync(
                    document, root, invocation,
                    CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName, defaultValue)),
                $"AM011_DefaultValue_{propertyName}");
        }

        // Option 2 (fallback): Ignore.
        CodeAction ignore = CodeAction.Create(
            $"Ignore required property '{propertyName}' (manual review)",
            _ => ReplaceNodeAsync(
                document, root, invocation,
                CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName)),
            $"AM011_Ignore_{propertyName}");

        return (primary, ignore);
    }

    private ImmutableArray<CodeAction> BuildAggregateActions(
        CodeFixContext context,
        CodeFixOperationContext operationContext,
        DiagnosticInvocationGroup group)
    {
        // Resolve types for the aggregate. For a ReverseMap() diagnostic, group.Invocation is the
        // ReverseMap() node (which has no generic type args), so walk up to the forward CreateMap and
        // swap source/destination — the ForMember chain is still folded onto the ReverseMap() node.
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

        List<IPropertySymbol> sourceProperties = sourceType == null
            ? new List<IPropertySymbol>()
            : AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false).ToList();

        int count = orderedProperties.Count;
        InvocationExpressionSyntax invocation = group.Invocation;
        SyntaxNode root = operationContext.Root;

        List<PropertyFixSpec> mapSpecs = orderedProperties
            .Select(property =>
            {
                IPropertySymbol? fuzzyMatch =
                    FuzzyMatchHelper.FindUniqueBestFuzzyMatch(property.Name, sourceProperties, property.Type);
                return fuzzyMatch != null
                    ? PropertyFixSpec.MapFrom(
                        property.Name, $"src.{CodeFixSyntaxHelper.EscapeIdentifier(fuzzyMatch.Name)}")
                    : PropertyFixSpec.MapFrom(
                        property.Name, TypeConversionHelper.GetDefaultValueForType(property.Type.ToDisplayString()));
            })
            .ToList();

        CodeAction mapAll = CodeAction.Create(
            $"Map all {count} unmapped required properties",
            _ => ReplaceNodeAsync(
                context.Document, root, invocation, FoldAggregateForMembers(invocation, mapSpecs)),
            "AM011_MapAll");

        List<PropertyFixSpec> ignoreSpecs = orderedProperties
            .Select(property => PropertyFixSpec.Ignore(property.Name))
            .ToList();

        CodeAction ignoreAll = CodeAction.Create(
            $"Ignore all {count} unmapped required properties",
            _ => ReplaceNodeAsync(
                context.Document, root, invocation, FoldAggregateForMembers(invocation, ignoreSpecs)),
            "AM011_IgnoreAll");

        return ImmutableArray.Create(mapAll, ignoreAll);
    }
}

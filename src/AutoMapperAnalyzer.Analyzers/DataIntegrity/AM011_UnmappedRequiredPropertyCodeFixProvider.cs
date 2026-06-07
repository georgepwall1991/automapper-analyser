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
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        foreach (DiagnosticInvocationGroup group in GroupDiagnosticsByInvocation(
                     operationContext.Root, context.Diagnostics, "PropertyName", "PropertyType"))
        {
            // A single unmapped required property stays a flat per-property choice; 2+ on one CreateMap
            // get the aggregate "Map all / Ignore all" actions plus a nested "Fix individual…" submenu so
            // the lightbulb does not flood with one entry per property.
            bool isBatch = group.PropertyNames.Count >= 2;

            var perProperty = new List<(string Name, CodeAction Primary, CodeAction Ignore)>();
            foreach (Diagnostic diagnostic in group.Diagnostics)
            {
                var properties = TryGetDiagnosticProperties(diagnostic, "PropertyName", "PropertyType");
                if (properties == null)
                {
                    continue;
                }

                string propertyName = properties["PropertyName"];
                (CodeAction primary, CodeAction ignore) = BuildPerPropertyActions(
                    context.Document,
                    operationContext.Root,
                    operationContext.SemanticModel,
                    group.Invocation,
                    propertyName,
                    properties["PropertyType"]);

                if (isBatch)
                {
                    perProperty.Add((propertyName, primary, ignore));
                }
                else
                {
                    context.RegisterCodeFix(primary, diagnostic);
                    context.RegisterCodeFix(ignore, diagnostic);
                }
            }

            if (!isBatch)
            {
                continue;
            }

            // Aggregate actions first so they surface at the top of the lightbulb.
            RegisterAggregateForGroup(context, operationContext, group);

            ImmutableArray<CodeAction> perPropertySubMenus = perProperty
                .Select(entry => CodeAction.Create(
                    $"Required property '{entry.Name}'",
                    ImmutableArray.Create(entry.Primary, entry.Ignore),
                    isInlinable: false))
                .ToImmutableArray();

            if (!perPropertySubMenus.IsEmpty)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "Fix individual required property…",
                        perPropertySubMenus,
                        isInlinable: false),
                    group.Diagnostics);
            }
        }
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

    private void RegisterAggregateForGroup(
        CodeFixContext context,
        CodeFixOperationContext operationContext,
        DiagnosticInvocationGroup group)
    {
        // Resolve types for the aggregate. For a ReverseMap() diagnostic, group.Invocation is the
        // ReverseMap() node (which has no generic type args), so walk up to the forward CreateMap and
        // swap source/destination — the ForMember chain is still folded onto the ReverseMap() node.
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            ResolveAggregateMapTypes(group.Invocation, operationContext.SemanticModel);
        if (destType == null)
        {
            return;
        }

        var flaggedNames = new HashSet<string>(group.PropertyNames);
        List<IPropertySymbol> orderedProperties = AutoMapperAnalysisHelpers
            .GetMappableProperties(destType, requireGetter: false, requireSetter: true)
            .Where(p => flaggedNames.Contains(p.Name))
            .ToList();
        if (orderedProperties.Count < 2)
        {
            return;
        }

        List<IPropertySymbol> sourceProperties = sourceType == null
            ? new List<IPropertySymbol>()
            : AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false).ToList();

        int count = orderedProperties.Count;
        InvocationExpressionSyntax invocation = group.Invocation;
        SyntaxNode root = operationContext.Root;
        ImmutableArray<Diagnostic> diagnostics = group.Diagnostics;

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

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Map all {count} unmapped required properties",
                _ => ReplaceNodeAsync(
                    context.Document, root, invocation, FoldAggregateForMembers(invocation, mapSpecs)),
                "AM011_MapAll"),
            diagnostics);

        List<PropertyFixSpec> ignoreSpecs = orderedProperties
            .Select(property => PropertyFixSpec.Ignore(property.Name))
            .ToList();

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Ignore all {count} unmapped required properties",
                _ => ReplaceNodeAsync(
                    context.Document, root, invocation, FoldAggregateForMembers(invocation, ignoreSpecs)),
                "AM011_IgnoreAll"),
            diagnostics);
    }

    private static (ITypeSymbol? sourceType, ITypeSymbol? destinationType) ResolveAggregateMapTypes(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "ReverseMap"))
        {
            InvocationExpressionSyntax? createMap = FindCreateMapInvocation(invocation, semanticModel);
            if (createMap == null)
            {
                return (null, null);
            }

            (ITypeSymbol? forwardSource, ITypeSymbol? forwardDestination) =
                MappingChainAnalysisHelper.GetCreateMapTypeArguments(createMap, semanticModel);

            // Reverse direction: source/destination are swapped.
            return (forwardDestination, forwardSource);
        }

        return MappingChainAnalysisHelper.GetCreateMapTypeArguments(invocation, semanticModel);
    }

    private static InvocationExpressionSyntax? FindCreateMapInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        InvocationExpressionSyntax? current = invocation;
        while (current != null)
        {
            if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(current, semanticModel, "CreateMap"))
            {
                return current;
            }

            current = (current.Expression as MemberAccessExpressionSyntax)?.Expression as InvocationExpressionSyntax;
        }

        return null;
    }
}

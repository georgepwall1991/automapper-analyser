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
        CodeFixOperationContext? operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        var processedInvocations = new HashSet<TextSpan>();
        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            Dictionary<string, string>? properties = TryGetDiagnosticProperties(diagnostic, "PropertyName", "PropertyType");
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

            List<IPropertySymbol> unmappedRequiredProperties = FindUnmappedRequiredProperties(
                invocation,
                operationContext.SemanticModel);

            IPropertySymbol? diagnosticProperty = unmappedRequiredProperties.FirstOrDefault(
                property => string.Equals(property.Name, properties["PropertyName"], StringComparison.Ordinal));

            // Stale or already-configured diagnostics: withhold rather than scaffolding a
            // second ForMember from diagnostic metadata that no longer matches live unmapped set.
            if (diagnosticProperty == null)
            {
                continue;
            }

            if (unmappedRequiredProperties.Count < 2 || !processedInvocations.Add(invocation.Span))
            {
                (CodeAction? primary, CodeAction ignore) = BuildPerPropertyActions(
                    context.Document,
                    operationContext.Root,
                    operationContext.SemanticModel,
                    invocation,
                    diagnosticProperty.Name);
                if (primary != null)
                {
                    context.RegisterCodeFix(primary, diagnostic);
                }

                context.RegisterCodeFix(ignore, diagnostic);
                continue;
            }

            foreach (CodeAction aggregateAction in BuildAggregateActions(context, operationContext, invocation,
                         unmappedRequiredProperties))
            {
                context.RegisterCodeFix(aggregateAction, diagnostic);
            }

            var perPropertySubMenus = new List<CodeAction>();
            foreach (IPropertySymbol property in unmappedRequiredProperties)
            {
                (CodeAction? primary, CodeAction ignore) = BuildPerPropertyActions(
                    context.Document,
                    operationContext.Root,
                    operationContext.SemanticModel,
                    invocation,
                    property.Name);

                ImmutableArray<CodeAction> propertyActions = primary == null
                    ? [ignore]
                    : [primary, ignore];

                perPropertySubMenus.Add(CodeAction.Create(
                    $"Required property '{property.Name}'",
                    propertyActions,
                    isInlinable: false));
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Fix individual required property…",
                    perPropertySubMenus.ToImmutableArray(),
                    isInlinable: false),
                diagnostic);
        }
    }

    private (CodeAction? Primary, CodeAction Ignore) BuildPerPropertyActions(
        Document document,
        SyntaxNode root,
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        string propertyName)
    {
        // Resolve types through ReverseMap the same way aggregate fixes do, so reverse-direction
        // per-property fuzzy suggestions work when the diagnostic anchors on ReverseMap().
        IPropertySymbol? bestFuzzyMatch = null;
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            ResolveCreateMapTypesWithReverse(invocation, semanticModel);
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

        // Offer an executable mapping only when a unique compatible fuzzy source exists.
        // Otherwise require the developer to make the explicit manual-review Ignore choice
        // instead of manufacturing required domain data with string.Empty/0/false.
        CodeAction? primary = null;
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

        // Explicit fallback: Ignore.
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
        InvocationExpressionSyntax invocation,
        IReadOnlyList<IPropertySymbol> orderedProperties)
    {
        // Resolve types for the aggregate. For a ReverseMap() diagnostic, group.Invocation is the
        // ReverseMap() node (which has no generic type args), so walk up to the forward CreateMap and
        // swap source/destination — the ForMember chain is still folded onto the ReverseMap() node.
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            ResolveCreateMapTypesWithReverse(invocation, operationContext.SemanticModel);
        if (destType == null)
        {
            return ImmutableArray<CodeAction>.Empty;
        }

        if (orderedProperties.Count < 2)
        {
            return ImmutableArray<CodeAction>.Empty;
        }

        List<IPropertySymbol> sourceProperties = sourceType == null
            ? new List<IPropertySymbol>()
            : AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false).ToList();

        int count = orderedProperties.Count;
        SyntaxNode root = operationContext.Root;

        var matches = orderedProperties
            .Select(property => (
                Property: property,
                Match: FuzzyMatchHelper.FindUniqueBestFuzzyMatch(property.Name, sourceProperties, property.Type)))
            .ToList();

        var actions = ImmutableArray.CreateBuilder<CodeAction>();

        // Honest "Map all": only when every required property has a unique fuzzy source match
        // (same gate as AM004/AM006). Never claim "Map" while injecting defaults.
        if (matches.All(m => m.Match != null))
        {
            List<PropertyFixSpec> mapSpecs = matches
                .Select(m => PropertyFixSpec.MapFrom(
                    m.Property.Name, $"src.{CodeFixSyntaxHelper.EscapeIdentifier(m.Match!.Name)}"))
                .ToList();
            actions.Add(CodeAction.Create(
                $"Map all {count} unmapped required properties",
                _ => ReplaceNodeAsync(
                    context.Document, root, invocation, FoldAggregateForMembers(invocation, mapSpecs)),
                "AM011_MapAll"));
        }
        else
        {
            // Mixed or no fuzzy: bulk default/fuzzy scaffold with an honest title.
            List<PropertyFixSpec> scaffoldSpecs = matches
                .Select(m => m.Match != null
                    ? PropertyFixSpec.MapFrom(
                        m.Property.Name, $"src.{CodeFixSyntaxHelper.EscapeIdentifier(m.Match.Name)}")
                    : PropertyFixSpec.MapFrom(
                        m.Property.Name,
                        TypeConversionHelper.GetDefaultValueForType(m.Property.Type.ToDisplayString())))
                .ToList();
            actions.Add(CodeAction.Create(
                $"Scaffold maps for all {count} required properties (manual review)",
                _ => ReplaceNodeAsync(
                    context.Document, root, invocation, FoldAggregateForMembers(invocation, scaffoldSpecs)),
                "AM011_ScaffoldAll"));
        }

        List<PropertyFixSpec> ignoreSpecs = orderedProperties
            .Select(property => PropertyFixSpec.Ignore(property.Name))
            .ToList();

        actions.Add(CodeAction.Create(
            $"Ignore all {count} unmapped required properties (manual review)",
            _ => ReplaceNodeAsync(
                context.Document, root, invocation, FoldAggregateForMembers(invocation, ignoreSpecs)),
            "AM011_IgnoreAll"));

        return actions.ToImmutable();
    }

    private static List<IPropertySymbol> FindUnmappedRequiredProperties(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) =
            ResolveCreateMapTypesWithReverse(invocation, semanticModel);
        if (sourceType == null || destinationType == null)
        {
            return [];
        }

        List<IPropertySymbol> sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false).ToList();

        return AutoMapperAnalysisHelpers
            .GetMappableProperties(destinationType, requireGetter: false, requireSetter: true)
            .Where(property => property.IsRequired)
            .Where(property => !sourceProperties.Any(sourceProperty =>
                string.Equals(sourceProperty.Name, property.Name, StringComparison.OrdinalIgnoreCase)))
            .Where(property => !IsPropertyConfiguredWithDestinationConfiguration(
                invocation,
                property.Name,
                semanticModel))
            .Where(property => !IsPropertyConfiguredWithForCtorParam(invocation, property.Name, semanticModel))
            .ToList();
    }

    private static bool IsPropertyConfiguredWithForCtorParam(
        InvocationExpressionSyntax invocation,
        string propertyName,
        SemanticModel semanticModel)
    {
        SyntaxNode? currentNode = invocation.Parent;

        while (currentNode is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            string methodName = memberAccess.Name.Identifier.ValueText;
            if (methodName == "ReverseMap")
            {
                break;
            }

            if (methodName == "ForCtorParam" &&
                MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ForCtorParam") &&
                chainedInvocation.ArgumentList.Arguments.Count > 0)
            {
                Optional<object?> constantValue = semanticModel.GetConstantValue(
                    chainedInvocation.ArgumentList.Arguments[0].Expression);

                if (constantValue.HasValue &&
                    constantValue.Value is string configuredParam &&
                    string.Equals(configuredParam, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            currentNode = chainedInvocation.Parent;
        }

        return false;
    }

    private static bool IsPropertyConfiguredWithDestinationConfiguration(
        InvocationExpressionSyntax invocation,
        string propertyName,
        SemanticModel semanticModel)
    {
        foreach (InvocationExpressionSyntax mappingCall in GetScopedDestinationConfigurationCalls(invocation))
        {
            if (!MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(mappingCall, semanticModel, "ForMember") &&
                !MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(mappingCall, semanticModel, "ForPath"))
            {
                continue;
            }

            if (mappingCall.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            string? selectedMember = AM020MappingConfigurationHelpers.GetSelectedTopLevelMemberNameWithSemanticModel(
                mappingCall.ArgumentList.Arguments[0].Expression,
                semanticModel);
            if (selectedMember == null)
            {
                continue;
            }

            if (string.Equals(selectedMember, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<InvocationExpressionSyntax> GetScopedDestinationConfigurationCalls(
        InvocationExpressionSyntax invocation)
    {
        var mappingCalls = new List<InvocationExpressionSyntax>();
        SyntaxNode? currentNode = invocation.Parent;

        while (currentNode is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            string methodName = memberAccess.Name.Identifier.ValueText;
            if (methodName == "ReverseMap")
            {
                break;
            }

            if (methodName is "ForMember" or "ForPath")
            {
                mappingCalls.Add(chainedInvocation);
            }

            currentNode = chainedInvocation.Parent;
        }

        return mappingCalls;
    }
}

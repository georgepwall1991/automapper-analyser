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
        // Aggregate "Map all / Ignore all" fixes (registered first so they surface ahead of the
        // per-property fixes) when 2+ required properties are unmapped on one CreateMap.
        await RegisterAggregateFixesAsync(context);

        await ProcessDiagnosticsAsync(
            context,
            propertyNames: ["PropertyName", "PropertyType"],
            registerPerPropertyFixes: (ctx, diagnostic, invocation, properties, semanticModel, root) =>
            {
                var propertyName = properties["PropertyName"];
                var propertyType = properties["PropertyType"];

                // Try to find best fuzzy match
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

                // Option 1 (primary): fuzzy match if found, else default value
                if (bestFuzzyMatch != null)
                {
                    ctx.RegisterCodeFix(
                        CodeAction.Create(
                            $"Map from similar property '{bestFuzzyMatch.Name}'",
                            cancellationToken =>
                            {
                                var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                                    invocation, propertyName, $"src.{CodeFixSyntaxHelper.EscapeIdentifier(bestFuzzyMatch.Name)}");
                                return ReplaceNodeAsync(ctx.Document, root, invocation, newInvocation);
                            },
                            $"AM011_FuzzyMatch_{propertyName}_{bestFuzzyMatch.Name}"),
                        diagnostic);
                }
                else
                {
                    string defaultValue = TypeConversionHelper.GetDefaultValueForType(propertyType);
                    ctx.RegisterCodeFix(
                        CodeAction.Create(
                            $"Scaffold default mapping for '{propertyName}' ({defaultValue})",
                            cancellationToken =>
                            {
                                var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                                    invocation, propertyName, defaultValue);
                                return ReplaceNodeAsync(ctx.Document, root, invocation, newInvocation);
                            },
                            $"AM011_DefaultValue_{propertyName}"),
                        diagnostic);
                }

                // Option 2 (fallback): Ignore
                ctx.RegisterCodeFix(
                    CodeAction.Create(
                        $"Ignore required property '{propertyName}' (manual review)",
                        cancellationToken =>
                        {
                            var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
                            return ReplaceNodeAsync(ctx.Document, root, invocation, newInvocation);
                        },
                        $"AM011_Ignore_{propertyName}"),
                    diagnostic);
            });
    }

    private async Task RegisterAggregateFixesAsync(CodeFixContext context)
    {
        var operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        foreach (DiagnosticInvocationGroup group in GroupDiagnosticsByInvocation(
                     operationContext.Root, context.Diagnostics, "PropertyName", "PropertyType"))
        {
            if (group.PropertyNames.Count < 2)
            {
                continue;
            }

            // Resolve types for the aggregate. For a ReverseMap() diagnostic, group.Invocation is the
            // ReverseMap() node (which has no generic type args), so walk up to the forward CreateMap and
            // swap source/destination — the ForMember chain is still folded onto the ReverseMap() node.
            (ITypeSymbol? sourceType, ITypeSymbol? destType) =
                ResolveAggregateMapTypes(group.Invocation, operationContext.SemanticModel);
            if (destType == null)
            {
                continue;
            }

            var flaggedNames = new HashSet<string>(group.PropertyNames);
            List<IPropertySymbol> orderedProperties = AutoMapperAnalysisHelpers
                .GetMappableProperties(destType, requireGetter: false, requireSetter: true)
                .Where(p => flaggedNames.Contains(p.Name))
                .ToList();
            if (orderedProperties.Count < 2)
            {
                continue;
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

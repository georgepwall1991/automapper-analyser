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
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        await ProcessDiagnosticsAsync(
            context,
            propertyNames: ["PropertyName", "PropertyType", "SourceTypeName", "DestinationTypeName", "IsReverseMap"],
            registerPerPropertyFixes: (ctx, diagnostic, invocation, properties, semanticModel, root) =>
            {
                var propertyName = properties["PropertyName"];

                // Try to find best fuzzy match
                if (TryResolveMappingContext(invocation, semanticModel, out var mappingContext))
                {
                    var destProperties = AutoMapperAnalysisHelpers.GetMappableProperties(
                        mappingContext!.DestinationType, requireSetter: true);

                    IPropertySymbol? sourcePropertySymbol = AutoMapperAnalysisHelpers
                        .GetMappableProperties(mappingContext.SourceType, requireSetter: false)
                        .FirstOrDefault(p => p.Name == propertyName);

                    if (sourcePropertySymbol != null)
                    {
                        var bestMatch = FuzzyMatchHelper.FindFuzzyMatches(propertyName, destProperties, sourcePropertySymbol.Type)
                            .FirstOrDefault();

                        if (bestMatch != null)
                        {
                            string destName = bestMatch.Name;
                            ctx.RegisterCodeFix(
                                CodeAction.Create(
                                    $"Map '{propertyName}' to similar property '{destName}'",
                                    cancellationToken =>
                                    {
                                        var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                                            invocation, destName, $"src.{propertyName}");
                                        return ReplaceNodeAsync(ctx.Document, root, invocation, newInvocation);
                                    },
                                    $"AM004_FuzzyMatch_{propertyName}_{destName}"),
                                diagnostic);
                        }
                    }
                }

                // Always register ignore option
                ctx.RegisterCodeFix(
                    CodeAction.Create(
                        $"Ignore '{propertyName}' via DoNotValidate()",
                        cancellationToken =>
                        {
                            var newInvocation = CodeFixSyntaxHelper.CreateForSourceMemberWithDoNotValidate(
                                invocation, propertyName);
                            return ReplaceNodeAsync(ctx.Document, root, invocation, newInvocation);
                        },
                        $"AM004_Ignore_{propertyName}"),
                    diagnostic);
            });
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
            InvocationExpressionSyntax? createMapInvocation = FindCreateMapInvocation(invocation, semanticModel);
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

    private static InvocationExpressionSyntax? FindCreateMapInvocation(
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

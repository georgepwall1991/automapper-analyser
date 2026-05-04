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
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        await ProcessDiagnosticsAsync(
            context,
            propertyNames: ["PropertyName", "PropertyType", "DestinationTypeName", "SourceTypeName"],
            registerPerPropertyFixes: (ctx, diagnostic, invocation, properties, semanticModel, root) =>
            {
                var propertyName = properties["PropertyName"];

                // Try to find best fuzzy match
                (ITypeSymbol? sourceType, ITypeSymbol? destType) = MappingChainAnalysisHelper.GetCreateMapTypeArguments(invocation, semanticModel);
                if (sourceType != null && destType != null)
                {
                    IPropertySymbol? destPropertySymbol = AutoMapperAnalysisHelpers
                        .GetMappableProperties(destType, requireSetter: true)
                        .FirstOrDefault(p => p.Name == propertyName);

                    if (destPropertySymbol != null)
                    {
                        var sourceProperties = AutoMapperAnalysisHelpers
                            .GetMappableProperties(sourceType, requireSetter: false);

                        IPropertySymbol? bestMatch =
                            FuzzyMatchHelper.FindUniqueBestFuzzyMatch(propertyName, sourceProperties, destPropertySymbol.Type);

                        if (bestMatch != null)
                        {
                            string srcName = bestMatch.Name;
                            ctx.RegisterCodeFix(
                                CodeAction.Create(
                                    $"Map '{propertyName}' from similar source property '{srcName}'",
                                    cancellationToken =>
                                    {
                                        var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                                            invocation, propertyName, $"src.{srcName}");
                                        return ReplaceNodeAsync(ctx.Document, root, invocation, newInvocation);
                                    },
                                    $"AM006_FuzzyMatch_{propertyName}_{srcName}"),
                                diagnostic);
                        }
                    }
                }

                // Always register ignore option
                ctx.RegisterCodeFix(
                    CodeAction.Create(
                        $"Ignore destination property '{propertyName}' (manual review)",
                        cancellationToken =>
                        {
                            var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
                            return ReplaceNodeAsync(ctx.Document, root, invocation, newInvocation);
                        },
                        $"AM006_Ignore_{propertyName}"),
                    diagnostic);
            });
    }
}

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
    public override ImmutableArray<string> FixableDiagnosticIds => ["AM011"];

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
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
                        bestFuzzyMatch = FuzzyMatchHelper.FindFuzzyMatches(propertyName, sourceProperties, destinationProperty.Type)
                            .FirstOrDefault();
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
                                    invocation, propertyName, $"src.{bestFuzzyMatch.Name}");
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
                            $"Map '{propertyName}' to default value ({defaultValue})",
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
                        $"Ignore required property '{propertyName}'",
                        cancellationToken =>
                        {
                            var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
                            return ReplaceNodeAsync(ctx.Document, root, invocation, newInvocation);
                        },
                        $"AM011_Ignore_{propertyName}"),
                    diagnostic);
            });
    }
}

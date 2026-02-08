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
            propertyNames: ["PropertyName", "DestinationTypeName", "SourceTypeName"],
            registerBulkFixes: RegisterBulkFixes,
            registerPerPropertyFixes: (ctx, diagnostic, invocation, properties, semanticModel, root) =>
            {
                RegisterPerPropertyFixes(ctx, diagnostic, invocation,
                    properties["PropertyName"],
                    properties["SourceTypeName"],
                    properties["DestinationTypeName"],
                    semanticModel, root);
            });
    }

    private void RegisterPerPropertyFixes(CodeFixContext context, Diagnostic diagnostic,
        InvocationExpressionSyntax invocation, string propertyName, string sourceTypeName,
        string destinationTypeName, SemanticModel semanticModel, SyntaxNode root)
    {
        var nestedActions = ImmutableArray.CreateBuilder<CodeAction>();

        // Phase 1: Fuzzy match suggestions — find similar source properties
        (ITypeSymbol? sourceType, ITypeSymbol? destType) = MappingChainAnalysisHelper.GetCreateMapTypeArguments(invocation, semanticModel);
        IPropertySymbol? destPropertySymbol = null;
        if (sourceType != null && destType != null)
        {
            destPropertySymbol = AutoMapperAnalysisHelpers
                .GetMappableProperties(destType, requireSetter: true)
                .FirstOrDefault(p => p.Name == propertyName);

            if (destPropertySymbol != null)
            {
                var sourceProperties = AutoMapperAnalysisHelpers
                    .GetMappableProperties(sourceType, requireSetter: false);

                foreach (var srcProp in FuzzyMatchHelper.FindFuzzyMatches(propertyName, sourceProperties, destPropertySymbol.Type))
                {
                    string srcName = srcProp.Name;
                    nestedActions.Add(CodeAction.Create(
                        $"Map from similar source property '{srcName}'",
                        cancellationToken =>
                        {
                            InvocationExpressionSyntax newInvocation =
                                CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                                    invocation, propertyName, $"src.{srcName}");
                            return ReplaceNodeAsync(context.Document, root, invocation, newInvocation);
                        },
                        $"FuzzyMatch_{propertyName}_{srcName}"));
                }
            }
        }

        // Phase 2: Ignore destination property
        nestedActions.Add(CodeAction.Create(
            "Ignore destination property",
            cancellationToken =>
            {
                InvocationExpressionSyntax newInvocation =
                    CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
                return ReplaceNodeAsync(context.Document, root, invocation, newInvocation);
            },
            $"Ignore_{propertyName}"));

        // Phase 3: Create source property
        if (sourceType != null && destType != null)
        {
            string propertyType = destPropertySymbol?.Type.ToDisplayString() ?? "object";

            nestedActions.Add(CodeAction.Create(
                "Create property in source type",
                cancellationToken => CreateSourcePropertyAsync(context.Document, sourceType,
                    propertyName, propertyType),
                $"CreateProperty_{propertyName}"));
        }

        // Register grouped action
        var groupAction = CreateGroupedAction($"Fix unmapped destination '{propertyName}'...", nestedActions);
        context.RegisterCodeFix(groupAction, diagnostic);
    }

    private void RegisterBulkFixes(CodeFixContext context, InvocationExpressionSyntax invocation,
        SemanticModel semanticModel, SyntaxNode root)
    {
        // Bulk Fix 1: Ignore all unmapped destination properties
        var ignoreAction = CodeAction.Create(
            "Ignore all unmapped destination properties",
            cancellationToken => BulkIgnoreAsync(context.Document, root, invocation, semanticModel),
            "AM006_Bulk_Ignore"
        );

        // Bulk Fix 2: Create all missing source properties
        var createPropsAction = CodeAction.Create(
            "Create all missing properties in source type",
            cancellationToken => BulkCreateSourcePropertiesAsync(context.Document, invocation, semanticModel),
            "AM006_Bulk_CreateProperties"
        );

        RegisterBulkFixes(context, ignoreAction, createPropsAction);
    }

    private Task<Document> BulkIgnoreAsync(Document document, SyntaxNode root,
        InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            MappingChainAnalysisHelper.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType == null || destType == null)
        {
            return Task.FromResult(document);
        }

        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        var destProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destType, requireSetter: true).ToList();

        var unmappedDestProps = destProperties
            .Where(dp => !dp.IsRequired)
            .Where(dp => !sourceProperties.Any(sp =>
                string.Equals(sp.Name, dp.Name, StringComparison.OrdinalIgnoreCase)))
            .Where(dp => !AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember(invocation, dp.Name, semanticModel))
            .ToList();

        if (!unmappedDestProps.Any())
        {
            return Task.FromResult(document);
        }

        InvocationExpressionSyntax currentInvocation = invocation;
        foreach (var prop in unmappedDestProps)
        {
            currentInvocation = CodeFixSyntaxHelper.CreateForMemberWithIgnore(currentInvocation, prop.Name);
        }

        SyntaxNode newRoot = root.ReplaceNode(invocation, currentInvocation);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private async Task<Solution> BulkCreateSourcePropertiesAsync(Document document,
        InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            MappingChainAnalysisHelper.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType == null || destType == null)
        {
            return document.Project.Solution;
        }

        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        var destProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destType, requireSetter: true).ToList();

        List<(string Name, string Type)> propertiesToAdd = destProperties
            .Where(dp => !dp.IsRequired)
            .Where(dp => !sourceProperties.Any(sp =>
                string.Equals(sp.Name, dp.Name, StringComparison.OrdinalIgnoreCase)))
            .Where(dp => !AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember(invocation, dp.Name, semanticModel))
            .Select(dp => (dp.Name, dp.Type.ToDisplayString()))
            .ToList();

        if (!propertiesToAdd.Any())
        {
            return document.Project.Solution;
        }

        return await CodeFixSyntaxHelper.AddPropertiesToTypeAsync(document, sourceType, propertiesToAdd);
    }

    private async Task<Solution> CreateSourcePropertyAsync(
        Document document,
        ITypeSymbol sourceType,
        string propertyName,
        string propertyType)
    {
        return await CodeFixSyntaxHelper.AddPropertiesToTypeAsync(document, sourceType, [(propertyName, propertyType)]);
    }
}

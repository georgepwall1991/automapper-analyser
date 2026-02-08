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
            propertyNames: ["PropertyName", "PropertyType"],
            registerBulkFixes: RegisterBulkFixes,
            registerPerPropertyFixes: (ctx, diagnostic, invocation, properties, semanticModel, root) =>
            {
                RegisterPerPropertyFixes(ctx, diagnostic, invocation, properties["PropertyName"],
                    properties["PropertyType"], semanticModel, root);
            });
    }

    private void RegisterPerPropertyFixes(CodeFixContext context, Diagnostic diagnostic,
        InvocationExpressionSyntax invocation, string propertyName, string propertyType,
        SemanticModel semanticModel, SyntaxNode root)
    {
        var nestedActions = ImmutableArray.CreateBuilder<CodeAction>();

        // Phase 1: Fuzzy match suggestions
        if (TryResolveMappingContext(invocation, semanticModel, out MappingAnalysisContext? mappingContext))
        {
            var destProperties = AutoMapperAnalysisHelpers.GetMappableProperties(
                mappingContext!.DestinationType, requireSetter: true);

            IPropertySymbol? sourcePropertySymbol = AutoMapperAnalysisHelpers
                .GetMappableProperties(mappingContext.SourceType, requireSetter: false)
                .FirstOrDefault(p => p.Name == propertyName);

            if (sourcePropertySymbol != null)
            {
                foreach (var destProp in FuzzyMatchHelper.FindFuzzyMatches(propertyName, destProperties, sourcePropertySymbol.Type))
                {
                    string destName = destProp.Name;
                    nestedActions.Add(CodeAction.Create(
                        $"Map to similar property '{destName}'",
                        cancellationToken =>
                        {
                            InvocationExpressionSyntax newInvocation =
                                CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                                    invocation, destName, $"src.{propertyName}");
                            return ReplaceNodeAsync(context.Document, root, invocation, newInvocation);
                        },
                        $"FuzzyMatch_{propertyName}_{destName}"));
                }
            }
        }

        // Phase 2: Create property in destination type
        nestedActions.Add(CodeAction.Create(
            "Create property in destination type",
            cancellationToken => CreateDestinationPropertyAsync(context.Document, invocation, propertyName, propertyType, cancellationToken),
            $"CreateProperty_{propertyName}"));

        // Phase 3: Ignore the source property
        nestedActions.Add(CodeAction.Create(
            "Ignore source property",
            cancellationToken =>
            {
                InvocationExpressionSyntax newInvocation =
                    CodeFixSyntaxHelper.CreateForSourceMemberWithDoNotValidate(invocation, propertyName);
                return ReplaceNodeAsync(context.Document, root, invocation, newInvocation);
            },
            $"Ignore_{propertyName}"));

        // Register grouped action using base class helper
        var groupAction = CreateGroupedAction($"Fix missing destination for '{propertyName}'...", nestedActions);
        context.RegisterCodeFix(groupAction, diagnostic);
    }

    private void RegisterBulkFixes(CodeFixContext context, InvocationExpressionSyntax invocation,
        SemanticModel semanticModel, SyntaxNode root)
    {
        // Bulk Fix 1: Ignore all unmapped source properties
        var ignoreAction = CodeAction.Create(
            "Ignore all unmapped source properties",
            cancellationToken => BulkIgnoreAsync(context.Document, root, invocation, semanticModel),
            "AM004_Bulk_Ignore"
        );

        // Bulk Fix 2: Create all missing properties in destination
        var createPropsAction = CodeAction.Create(
            "Create all missing properties in destination type",
            cancellationToken => BulkCreatePropertiesAsync(context.Document, invocation, semanticModel),
            "AM004_Bulk_CreateProperties"
        );

        // Register both bulk fixes using base class helper
        RegisterBulkFixes(context, ignoreAction, createPropsAction);
    }

    private Task<Document> BulkIgnoreAsync(Document document, SyntaxNode root, InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        if (!TryResolveMappingContext(invocation, semanticModel, out MappingAnalysisContext? mappingContext))
        {
            return Task.FromResult(document);
        }

        if (MappingChainAnalysisHelper.HasCustomConstructionOrConversion(
                mappingContext!.MappingInvocation,
                semanticModel,
                mappingContext.StopAtReverseMapBoundary))
        {
            return Task.FromResult(document);
        }

        List<IPropertySymbol> propertiesToIgnore =
            MappingChainAnalysisHelper.GetUnmappedSourceProperties(
                mappingContext.MappingInvocation,
                mappingContext.SourceType,
                mappingContext.DestinationType,
                semanticModel,
                mappingContext.StopAtReverseMapBoundary);

        if (!propertiesToIgnore.Any())
        {
            return Task.FromResult(document);
        }

        InvocationExpressionSyntax currentInvocation = mappingContext.MappingInvocation;
        foreach (var prop in propertiesToIgnore)
        {
            currentInvocation = CodeFixSyntaxHelper.CreateForSourceMemberWithDoNotValidate(currentInvocation, prop.Name);
        }

        SyntaxNode newRoot = root.ReplaceNode(mappingContext.MappingInvocation, currentInvocation);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private async Task<Solution> CreateDestinationPropertyAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string propertyName,
        string propertyType,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel == null) return document.Project.Solution;

        if (!TryResolveMappingContext(invocation, semanticModel, out MappingAnalysisContext? mappingContext))
        {
            return document.Project.Solution;
        }

        return await CodeFixSyntaxHelper.AddPropertiesToTypeAsync(document, mappingContext!.DestinationType, new[] { (propertyName, propertyType) });
    }

    private async Task<Solution> BulkCreatePropertiesAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        if (!TryResolveMappingContext(invocation, semanticModel, out MappingAnalysisContext? mappingContext))
        {
            return document.Project.Solution;
        }

        if (MappingChainAnalysisHelper.HasCustomConstructionOrConversion(
                mappingContext!.MappingInvocation,
                semanticModel,
                mappingContext.StopAtReverseMapBoundary))
        {
            return document.Project.Solution;
        }

        List<(string Name, string Type)> propertiesToAdd = MappingChainAnalysisHelper.GetUnmappedSourceProperties(
                mappingContext.MappingInvocation,
                mappingContext.SourceType,
                mappingContext.DestinationType,
                semanticModel,
                mappingContext.StopAtReverseMapBoundary)
            .Select(sourceProp => (sourceProp.Name, sourceProp.Type.ToDisplayString()))
            .ToList();

        if (!propertiesToAdd.Any()) return document.Project.Solution;

        return await CodeFixSyntaxHelper.AddPropertiesToTypeAsync(document, mappingContext.DestinationType, propertiesToAdd);
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

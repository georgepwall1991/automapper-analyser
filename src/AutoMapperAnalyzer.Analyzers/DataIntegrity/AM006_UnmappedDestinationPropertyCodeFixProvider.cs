using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

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
        if (sourceType != null && destType != null)
        {
            IPropertySymbol? destPropertySymbol = AutoMapperAnalysisHelpers
                .GetMappableProperties(destType, requireSetter: true)
                .FirstOrDefault(p => p.Name == propertyName);

            if (destPropertySymbol != null)
            {
                var sourceProperties = AutoMapperAnalysisHelpers
                    .GetMappableProperties(sourceType, requireSetter: false).ToList();

                foreach (var srcProp in sourceProperties)
                {
                    if (FuzzyMatchHelper.IsFuzzyMatchCandidate(propertyName, srcProp, destPropertySymbol.Type))
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
            IPropertySymbol? destPropertySymbol = AutoMapperAnalysisHelpers
                .GetMappableProperties(destType, requireSetter: true)
                .FirstOrDefault(p => p.Name == propertyName);

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

        return await AddPropertiesToTypeAsync(document, sourceType, propertiesToAdd);
    }

    private async Task<Solution> CreateSourcePropertyAsync(
        Document document,
        ITypeSymbol sourceType,
        string propertyName,
        string propertyType)
    {
        return await AddPropertiesToTypeAsync(document, sourceType, [(propertyName, propertyType)]);
    }

    private static async Task<Solution> AddPropertiesToTypeAsync(
        Document document,
        ITypeSymbol targetType,
        IEnumerable<(string Name, string Type)> properties)
    {
        if (targetType.Locations.All(l => !l.IsInSource))
        {
            return document.Project.Solution;
        }

        var syntaxReference = targetType.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxReference == null)
        {
            return document.Project.Solution;
        }

        var targetSyntaxRoot = await syntaxReference.SyntaxTree.GetRootAsync();
        var targetDecl = targetSyntaxRoot.FindNode(syntaxReference.Span);

        if (targetDecl == null)
        {
            return document.Project.Solution;
        }

        var editor = new SyntaxEditor(targetSyntaxRoot, document.Project.Solution.Workspace.Services);
        var propertyList = properties.ToList();

        editor.ReplaceNode(targetDecl, (originalNode, generator) =>
        {
            if (originalNode is RecordDeclarationSyntax recordDecl && recordDecl.ParameterList != null)
            {
                var newParams = propertyList.Select(prop =>
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier(prop.Name))
                            .WithType(SyntaxFactory.ParseTypeName(prop.Type)
                                .WithTrailingTrivia(SyntaxFactory.Space)))
                    .ToArray();

                return recordDecl.WithParameterList(recordDecl.ParameterList.AddParameters(newParams));
            }

            var typeDecl = (TypeDeclarationSyntax)originalNode;
            bool useInitAccessor = typeDecl.Members.OfType<PropertyDeclarationSyntax>()
                .SelectMany(p => p.AccessorList?.Accessors ?? SyntaxFactory.List<AccessorDeclarationSyntax>())
                .Any(a => a.IsKind(SyntaxKind.InitAccessorDeclaration));

            var setterKind = useInitAccessor
                ? SyntaxKind.InitAccessorDeclaration
                : SyntaxKind.SetAccessorDeclaration;

            var newMembers = new List<MemberDeclarationSyntax>();

            foreach (var (name, type) in propertyList)
            {
                var newProperty = SyntaxFactory.PropertyDeclaration(
                        SyntaxFactory.ParseTypeName(type),
                        SyntaxFactory.Identifier(name))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(new[]
                    {
                        SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                        SyntaxFactory.AccessorDeclaration(setterKind)
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                    })));

                newMembers.Add(newProperty);
            }

            return typeDecl.AddMembers(newMembers.ToArray());
        });

        var newTargetRoot = editor.GetChangedRoot();
        var targetDocument = document.Project.Solution.GetDocument(targetSyntaxRoot.SyntaxTree);

        if (targetDocument == null)
        {
            return document.Project.Solution;
        }

        return document.Project.Solution.WithDocumentSyntaxRoot(targetDocument.Id, newTargetRoot);
    }
}

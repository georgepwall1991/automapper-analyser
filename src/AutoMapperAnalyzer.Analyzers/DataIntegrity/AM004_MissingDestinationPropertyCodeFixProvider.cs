using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

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
                mappingContext.DestinationType, requireSetter: true).ToList();

            // Find the source property symbol to get its type
            IPropertySymbol? sourcePropertySymbol = AutoMapperAnalysisHelpers
                .GetMappableProperties(mappingContext.SourceType, requireSetter: false)
                .FirstOrDefault(p => p.Name == propertyName);

            if (sourcePropertySymbol != null)
            {
                foreach (var destProp in destProperties)
                {
                    if (IsFuzzyMatchCandidate(propertyName, destProp, sourcePropertySymbol.Type))
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
                mappingContext.MappingInvocation,
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

        return await AddPropertiesToDestinationAsync(document, mappingContext.DestinationType, new[] { (propertyName, propertyType) });
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
                mappingContext.MappingInvocation,
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

        return await AddPropertiesToDestinationAsync(document, mappingContext.DestinationType, propertiesToAdd);
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
        return invocation
            .DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(node => MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(node, semanticModel, "CreateMap"));
    }

    private async Task<Solution> AddPropertiesToDestinationAsync(
        Document document,
        ITypeSymbol destType,
        IEnumerable<(string Name, string Type)> properties)
    {
        // Check if the destination type is source code
        if (destType.Locations.All(l => !l.IsInSource))
        {
             return document.Project.Solution;
        }

        var syntaxReference = destType.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxReference == null) return document.Project.Solution;

        var destSyntaxRoot = await syntaxReference.SyntaxTree.GetRootAsync();
        var destClassDecl = destSyntaxRoot.FindNode(syntaxReference.Span);

        if (destClassDecl == null) return document.Project.Solution;

        var editor = new SyntaxEditor(destSyntaxRoot, document.Project.Solution.Workspace.Services);
        var propertyList = properties.ToList();

        editor.ReplaceNode(destClassDecl, (originalNode, generator) =>
        {
            // Handle record types with positional parameters
            if (originalNode is RecordDeclarationSyntax recordDecl && recordDecl.ParameterList != null)
            {
                var newParams = propertyList.Select(prop =>
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier(prop.Name))
                        .WithType(SyntaxFactory.ParseTypeName(prop.Type).WithTrailingTrivia(SyntaxFactory.Space)))
                    .ToArray();

                return recordDecl.WithParameterList(recordDecl.ParameterList.AddParameters(newParams));
            }

            // For class and body-style record types
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

        var newDestRoot = editor.GetChangedRoot();
        var destDocument = document.Project.Solution.GetDocument(destSyntaxRoot.SyntaxTree);

        if (destDocument == null) return document.Project.Solution;

        return document.Project.Solution.WithDocumentSyntaxRoot(destDocument.Id, newDestRoot);
    }

    /// <summary>
    ///     Determines whether a destination property is a fuzzy match candidate for the given source property name.
    ///     Returns true when the Levenshtein distance is at most 2, length difference at most 2, and types are compatible.
    /// </summary>
    private static bool IsFuzzyMatchCandidate(string sourcePropertyName, IPropertySymbol destProperty,
        ITypeSymbol sourcePropertyType)
    {
        int distance = ComputeLevenshteinDistance(sourcePropertyName, destProperty.Name);
        if (distance > 2 || Math.Abs(sourcePropertyName.Length - destProperty.Name.Length) > 2)
        {
            return false;
        }

        // distance == 0 means exact match — the analyzer wouldn't flag it, so skip
        if (distance == 0)
        {
            return false;
        }

        return AutoMapperAnalysisHelpers.AreTypesCompatible(sourcePropertyType, destProperty.Type);
    }

    /// <summary>
    ///     Computes the Levenshtein distance between two strings.
    /// </summary>
    private static int ComputeLevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.IsNullOrEmpty(t) ? 0 : t.Length;
        }

        if (string.IsNullOrEmpty(t))
        {
            return s.Length;
        }

        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++)
        {
            d[i, 0] = i;
        }

        for (int j = 0; j <= m; j++)
        {
            d[0, j] = j;
        }

        for (int j = 1; j <= m; j++)
        {
            for (int i = 1; i <= n; i++)
            {
                if (s[i - 1] == t[j - 1])
                {
                    d[i, j] = d[i - 1, j - 1];
                }
                else
                {
                    d[i, j] = Math.Min(Math.Min(
                            d[i - 1, j] + 1,
                            d[i, j - 1] + 1),
                        d[i - 1, j - 1] + 1);
                }
            }
        }

        return d[n, m];
    }
}

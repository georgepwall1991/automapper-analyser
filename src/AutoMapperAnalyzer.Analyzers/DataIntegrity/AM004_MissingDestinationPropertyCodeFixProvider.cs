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
                    properties["PropertyType"], root);
            });
    }

    private void RegisterPerPropertyFixes(CodeFixContext context, Diagnostic diagnostic,
        InvocationExpressionSyntax invocation, string propertyName, string propertyType, SyntaxNode root)
    {
        var nestedActions = ImmutableArray.CreateBuilder<CodeAction>();

        // Fix 1: Ignore the source property
        nestedActions.Add(CodeAction.Create(
            "Ignore source property",
            cancellationToken =>
            {
                InvocationExpressionSyntax newInvocation =
                    CodeFixSyntaxHelper.CreateForSourceMemberWithDoNotValidate(invocation, propertyName);
                return ReplaceNodeAsync(context.Document, root, invocation, newInvocation);
            },
            $"Ignore_{propertyName}"));

        // Fix 2: Add custom mapping comment
        nestedActions.Add(CodeAction.Create(
            "Add custom mapping (comment)",
            cancellationToken =>
            {
                SyntaxTrivia commentTrivia = SyntaxFactory.Comment(
                    $"// TODO: Create destination property or map '{propertyName}' to an existing property");
                InvocationExpressionSyntax newInvocation = invocation.WithLeadingTrivia(
                    invocation.GetLeadingTrivia()
                        .Add(commentTrivia)
                        .Add(SyntaxFactory.EndOfLine("\n")));

                return ReplaceNodeAsync(context.Document, root, invocation, newInvocation);
            },
            $"CustomMapping_{propertyName}"));

        // Fix 3: Combine properties (string only)
        if (!string.IsNullOrEmpty(propertyType) && TypeConversionHelper.IsStringType(propertyType))
        {
            nestedActions.Add(CodeAction.Create(
                "Map to existing property with custom logic",
                cancellationToken =>
                {
                    InvocationExpressionSyntax newInvocation = CodeFixSyntaxHelper
                        .CreateForSourceMemberWithDoNotValidate(invocation, propertyName)
                        .WithLeadingTrivia(
                            SyntaxFactory.Comment(
                                $"// TODO: Map '{propertyName}' to destination property with custom logic"));

                    return ReplaceNodeAsync(context.Document, root, invocation, newInvocation);
                },
                $"Combine_{propertyName}"));
        }

        // Fix 4: Create destination property
        nestedActions.Add(CodeAction.Create(
            "Create property in destination type",
            cancellationToken => CreateDestinationPropertyAsync(context.Document, invocation, propertyName, propertyType, cancellationToken),
            $"CreateProperty_{propertyName}"));

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

        if (HasCustomConstructionOrConversion(
                mappingContext.MappingInvocation,
                semanticModel,
                mappingContext.StopAtReverseMapBoundary))
        {
            return Task.FromResult(document);
        }

        List<IPropertySymbol> propertiesToIgnore =
            GetUnmappedSourceProperties(
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

    private static bool IsSourcePropertyExplicitlyIgnored(
        InvocationExpressionSyntax mappingInvocation,
        string sourcePropertyName,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        foreach (InvocationExpressionSyntax chainedInvocation in GetScopedChainInvocations(
                     mappingInvocation,
                     semanticModel,
                     stopAtReverseMapBoundary))
        {
            if (!IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ForSourceMember"))
            {
                continue;
            }

            if (IsForSourceMemberOfProperty(chainedInvocation, sourcePropertyName) &&
                HasDoNotValidateCall(chainedInvocation))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsForSourceMemberOfProperty(
        InvocationExpressionSyntax forSourceMemberInvocation,
        string propertyName)
    {
        if (forSourceMemberInvocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        string? selectedMember = GetSelectedMemberName(forSourceMemberInvocation.ArgumentList.Arguments[0].Expression);
        return string.Equals(selectedMember, propertyName, StringComparison.Ordinal);
    }

    private static bool HasDoNotValidateCall(InvocationExpressionSyntax forSourceMemberInvocation)
    {
        if (forSourceMemberInvocation.ArgumentList.Arguments.Count <= 1)
        {
            return false;
        }

        ExpressionSyntax secondArg = forSourceMemberInvocation.ArgumentList.Arguments[1].Expression;
        return secondArg.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.ValueText == "DoNotValidate");
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

        if (HasCustomConstructionOrConversion(
                mappingContext.MappingInvocation,
                semanticModel,
                mappingContext.StopAtReverseMapBoundary))
        {
            return document.Project.Solution;
        }

        List<(string Name, string Type)> propertiesToAdd = GetUnmappedSourceProperties(
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

    private static List<IPropertySymbol> GetUnmappedSourceProperties(
        InvocationExpressionSyntax mappingInvocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        IEnumerable<IPropertySymbol> sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        IEnumerable<IPropertySymbol> destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, false);

        var unmappedProperties = new List<IPropertySymbol>();

        foreach (IPropertySymbol sourceProp in sourceProperties)
        {
            if (destinationProperties.Any(p => string.Equals(p.Name, sourceProp.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (IsFlatteningMatch(sourceProp, destinationProperties))
            {
                continue;
            }

            if (IsSourcePropertyHandledByCustomMapping(
                    mappingInvocation,
                    sourceProp.Name,
                    semanticModel,
                    stopAtReverseMapBoundary))
            {
                continue;
            }

            if (IsSourcePropertyHandledByCtorParamMapping(
                    mappingInvocation,
                    sourceProp.Name,
                    semanticModel,
                    stopAtReverseMapBoundary))
            {
                continue;
            }

            if (IsSourcePropertyExplicitlyIgnored(
                    mappingInvocation,
                    sourceProp.Name,
                    semanticModel,
                    stopAtReverseMapBoundary))
            {
                continue;
            }

            unmappedProperties.Add(sourceProp);
        }

        return unmappedProperties;
    }

    private static bool IsSourcePropertyHandledByCustomMapping(
        InvocationExpressionSyntax mappingInvocation,
        string sourcePropertyName,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        foreach (InvocationExpressionSyntax chainedInvocation in GetScopedChainInvocations(
                     mappingInvocation,
                     semanticModel,
                     stopAtReverseMapBoundary))
        {
            if (!IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ForMember"))
            {
                continue;
            }

            if (ForMemberReferencesSourceProperty(chainedInvocation, sourcePropertyName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ForMemberReferencesSourceProperty(InvocationExpressionSyntax forMemberInvocation,
        string sourcePropertyName)
    {
        // ForMember's second argument contains the mapping lambda where source usage appears.
        if (forMemberInvocation.ArgumentList.Arguments.Count > 1)
        {
            return ContainsPropertyReference(forMemberInvocation.ArgumentList.Arguments[1].Expression, sourcePropertyName);
        }

        return false;
    }

    private static bool ContainsPropertyReference(SyntaxNode node, string propertyName)
    {
        if (node is MemberAccessExpressionSyntax rootMemberAccess &&
            rootMemberAccess.Name.Identifier.ValueText == propertyName)
        {
            return true;
        }

        return node.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
            .Any(memberAccess => memberAccess.Name.Identifier.ValueText == propertyName);
    }

    private static bool IsSourcePropertyHandledByCtorParamMapping(
        InvocationExpressionSyntax mappingInvocation,
        string sourcePropertyName,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        foreach (InvocationExpressionSyntax chainedInvocation in GetScopedChainInvocations(
                     mappingInvocation,
                     semanticModel,
                     stopAtReverseMapBoundary))
        {
            if (!IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ForCtorParam") ||
                chainedInvocation.ArgumentList.Arguments.Count <= 1)
            {
                continue;
            }

            ExpressionSyntax ctorMappingArg = chainedInvocation.ArgumentList.Arguments[1].Expression;
            if (ContainsPropertyReference(ctorMappingArg, sourcePropertyName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCustomConstructionOrConversion(
        InvocationExpressionSyntax mappingInvocation,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        foreach (InvocationExpressionSyntax chainedInvocation in GetScopedChainInvocations(
                     mappingInvocation,
                     semanticModel,
                     stopAtReverseMapBoundary))
        {
            if (IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ConstructUsing") ||
                IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ConvertUsing"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFlatteningMatch(
        IPropertySymbol sourceProperty,
        IEnumerable<IPropertySymbol> destinationProperties)
    {
        if (AutoMapperAnalysisHelpers.IsBuiltInType(sourceProperty.Type))
        {
            return false;
        }

        IEnumerable<IPropertySymbol> nestedProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceProperty.Type, requireSetter: false);

        foreach (IPropertySymbol destinationProperty in destinationProperties)
        {
            if (!destinationProperty.Name.StartsWith(sourceProperty.Name, StringComparison.OrdinalIgnoreCase) ||
                destinationProperty.Name.Length <= sourceProperty.Name.Length)
            {
                continue;
            }

            string flattenedMemberName = destinationProperty.Name.Substring(sourceProperty.Name.Length);
            if (nestedProperties.Any(p => string.Equals(p.Name, flattenedMemberName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveMappingContext(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out MappingAnalysisContext? mappingContext)
    {
        mappingContext = null;

        // Reverse-map diagnostic location
        if (IsAutoMapperMethodInvocation(invocation, semanticModel, "ReverseMap"))
        {
            InvocationExpressionSyntax? createMapInvocation = FindCreateMapInvocation(invocation, semanticModel);
            if (createMapInvocation == null)
            {
                return false;
            }

            (ITypeSymbol? sourceType, ITypeSymbol? destinationType) forwardTypes =
                GetCreateMapTypeArguments(createMapInvocation, semanticModel);
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
        if (IsAutoMapperMethodInvocation(invocation, semanticModel, "CreateMap"))
        {
            (ITypeSymbol? sourceType, ITypeSymbol? destinationType) forwardTypes =
                GetCreateMapTypeArguments(invocation, semanticModel);
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
            .FirstOrDefault(node => IsAutoMapperMethodInvocation(node, semanticModel, "CreateMap"));
    }

    private static string? GetSelectedMemberName(ExpressionSyntax expression)
    {
        return expression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda when simpleLambda.Body is MemberAccessExpressionSyntax memberAccess =>
                memberAccess.Name.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda
                when parenthesizedLambda.Body is MemberAccessExpressionSyntax memberAccess =>
                memberAccess.Name.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => null
        };
    }

    private static IEnumerable<InvocationExpressionSyntax> GetScopedChainInvocations(
        InvocationExpressionSyntax mappingInvocation,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        SyntaxNode? currentNode = mappingInvocation.Parent;

        while (currentNode is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if (stopAtReverseMapBoundary &&
                IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ReverseMap"))
            {
                break;
            }

            yield return chainedInvocation;
            currentNode = chainedInvocation.Parent;
        }
    }

    private static bool IsAutoMapperMethodInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string methodName)
    {
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);
        if (IsAutoMapperMethod(symbolInfo.Symbol as IMethodSymbol, methodName))
        {
            return true;
        }

        foreach (ISymbol candidateSymbol in symbolInfo.CandidateSymbols)
        {
            if (IsAutoMapperMethod(candidateSymbol as IMethodSymbol, methodName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAutoMapperMethod(IMethodSymbol? methodSymbol, string methodName)
    {
        if (methodSymbol == null || methodSymbol.Name != methodName)
        {
            return false;
        }

        string? namespaceName = methodSymbol.ContainingNamespace?.ToDisplayString();
        return namespaceName == "AutoMapper" ||
               (namespaceName?.StartsWith("AutoMapper.", StringComparison.Ordinal) ?? false);
    }

    private static (ITypeSymbol? sourceType, ITypeSymbol? destinationType) GetCreateMapTypeArguments(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);

        if (TryGetCreateMapTypeArgumentsFromMethod(symbolInfo.Symbol as IMethodSymbol, out ITypeSymbol? sourceType,
                out ITypeSymbol? destinationType))
        {
            return (sourceType, destinationType);
        }

        foreach (ISymbol candidateSymbol in symbolInfo.CandidateSymbols)
        {
            if (TryGetCreateMapTypeArgumentsFromMethod(candidateSymbol as IMethodSymbol, out sourceType,
                    out destinationType))
            {
                return (sourceType, destinationType);
            }
        }

        return AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
    }

    private static bool TryGetCreateMapTypeArgumentsFromMethod(
        IMethodSymbol? methodSymbol,
        out ITypeSymbol? sourceType,
        out ITypeSymbol? destinationType)
    {
        sourceType = null;
        destinationType = null;

        if (methodSymbol?.TypeArguments.Length != 2)
        {
            return false;
        }

        sourceType = methodSymbol.TypeArguments[0];
        destinationType = methodSymbol.TypeArguments[1];
        return true;
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
        
        // We need to know which node to replace/edit. Since we are adding members, we can just edit the class declaration.
        // However, SyntaxEditor.Edit usually takes a node.
        
        editor.ReplaceNode(destClassDecl, (originalNode, generator) =>
        {
            var currentClassDecl = (ClassDeclarationSyntax)originalNode;
            var newMembers = new List<MemberDeclarationSyntax>();

            foreach (var (name, type) in properties)
            {
                // Create property syntax
                var newProperty = SyntaxFactory.PropertyDeclaration(
                    SyntaxFactory.ParseTypeName(type),
                    SyntaxFactory.Identifier(name))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(new[]
                    {
                        SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                        SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                    })));
                
                newMembers.Add(newProperty);
            }
            
            return currentClassDecl.AddMembers(newMembers.ToArray());
        });

        var newDestRoot = editor.GetChangedRoot();
        var destDocument = document.Project.Solution.GetDocument(destSyntaxRoot.SyntaxTree);
        
        if (destDocument == null) return document.Project.Solution;
        
        return document.Project.Solution.WithDocumentSyntaxRoot(destDocument.Id, newDestRoot);
    }
}

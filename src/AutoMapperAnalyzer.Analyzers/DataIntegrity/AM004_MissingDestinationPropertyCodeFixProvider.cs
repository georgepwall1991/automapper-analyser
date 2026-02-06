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
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType == null || destType == null)
        {
            return Task.FromResult(document);
        }

        if (HasCustomConstructionOrConversion(invocation))
        {
            return Task.FromResult(document);
        }

        List<IPropertySymbol> propertiesToIgnore =
            GetUnmappedSourceProperties(invocation, sourceType, destType);

        if (!propertiesToIgnore.Any())
        {
            return Task.FromResult(document);
        }

        InvocationExpressionSyntax currentInvocation = invocation;
        foreach (var prop in propertiesToIgnore)
        {
            currentInvocation = CodeFixSyntaxHelper.CreateForSourceMemberWithDoNotValidate(currentInvocation, prop.Name);
        }

        SyntaxNode newRoot = root.ReplaceNode(invocation, currentInvocation);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static bool IsPropertyConfiguredWithForSourceMember(InvocationExpressionSyntax invocation, string propertyName)
    {
        SyntaxNode? parent = invocation.Parent;
        while (parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if (memberAccess.Name.Identifier.Text == "ForSourceMember" &&
                IsForSourceMemberOfProperty(chainedInvocation, propertyName))
            {
                return true;
            }

            parent = chainedInvocation.Parent;
        }

        return false;
    }

    private static bool IsForSourceMemberOfProperty(InvocationExpressionSyntax forSourceMemberInvocation,
        string propertyName)
    {
        if (forSourceMemberInvocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        string? selectedMember = GetSelectedMemberName(forSourceMemberInvocation.ArgumentList.Arguments[0].Expression);
        return string.Equals(selectedMember, propertyName, StringComparison.Ordinal);
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

    private async Task<Solution> CreateDestinationPropertyAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string propertyName,
        string propertyType,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel == null) return document.Project.Solution;

        var (sourceType, destType) = AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
        if (destType == null) return document.Project.Solution;

        return await AddPropertiesToDestinationAsync(document, destType, new[] { (propertyName, propertyType) });
    }

    private async Task<Solution> BulkCreatePropertiesAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        var (sourceType, destType) = AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType == null || destType == null) return document.Project.Solution;

        if (HasCustomConstructionOrConversion(invocation))
        {
            return document.Project.Solution;
        }

        List<(string Name, string Type)> propertiesToAdd = GetUnmappedSourceProperties(invocation, sourceType, destType)
            .Select(sourceProp => (sourceProp.Name, sourceProp.Type.ToDisplayString()))
            .ToList();

        if (!propertiesToAdd.Any()) return document.Project.Solution;

        return await AddPropertiesToDestinationAsync(document, destType, propertiesToAdd);
    }

    private static List<IPropertySymbol> GetUnmappedSourceProperties(
        InvocationExpressionSyntax createMapInvocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
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

            if (!AutoMapperAnalysisHelpers.IsBuiltInType(sourceProp.Type))
            {
                bool matchesFlattening = destinationProperties
                    .Any(p => p.Name.StartsWith(sourceProp.Name, StringComparison.OrdinalIgnoreCase));
                if (matchesFlattening)
                {
                    continue;
                }
            }

            if (IsSourcePropertyHandledByCustomMapping(createMapInvocation, sourceProp.Name))
            {
                continue;
            }

            if (IsSourcePropertyHandledByCtorParamMapping(createMapInvocation, sourceProp.Name))
            {
                continue;
            }

            if (IsPropertyConfiguredWithForSourceMember(createMapInvocation, sourceProp.Name))
            {
                continue;
            }

            unmappedProperties.Add(sourceProp);
        }

        return unmappedProperties;
    }

    private static bool IsSourcePropertyHandledByCustomMapping(
        InvocationExpressionSyntax createMapInvocation,
        string sourcePropertyName)
    {
        IEnumerable<InvocationExpressionSyntax> forMemberCalls =
            AutoMapperAnalysisHelpers.GetForMemberCalls(createMapInvocation);

        foreach (InvocationExpressionSyntax forMember in forMemberCalls)
        {
            if (ForMemberReferencesSourceProperty(forMember, sourcePropertyName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ForMemberReferencesSourceProperty(InvocationExpressionSyntax forMemberInvocation,
        string sourcePropertyName)
    {
        foreach (ArgumentSyntax arg in forMemberInvocation.ArgumentList.Arguments)
        {
            if (ContainsPropertyReference(arg.Expression, sourcePropertyName))
            {
                return true;
            }
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
        InvocationExpressionSyntax createMapInvocation,
        string sourcePropertyName)
    {
        SyntaxNode? parent = createMapInvocation.Parent;

        while (parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if (memberAccess.Name.Identifier.ValueText == "ForCtorParam" &&
                chainedInvocation.ArgumentList.Arguments.Count > 1)
            {
                ExpressionSyntax ctorMappingArg = chainedInvocation.ArgumentList.Arguments[1].Expression;
                if (ContainsPropertyReference(ctorMappingArg, sourcePropertyName))
                {
                    return true;
                }
            }

            parent = chainedInvocation.Parent;
        }

        return false;
    }

    private static bool HasCustomConstructionOrConversion(InvocationExpressionSyntax createMapInvocation)
    {
        SyntaxNode? parent = createMapInvocation.Parent;

        while (parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if (memberAccess.Name.Identifier.ValueText is "ConstructUsing" or "ConvertUsing")
            {
                return true;
            }

            parent = chainedInvocation.Parent;
        }

        return false;
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

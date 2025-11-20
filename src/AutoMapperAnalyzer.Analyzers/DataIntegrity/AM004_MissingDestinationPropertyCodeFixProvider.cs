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

    private async Task<Document> BulkIgnoreAsync(Document document, SyntaxNode root, InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType == null || destType == null)
        {
            return document;
        }

        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        var destProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destType, false);

        var propertiesToIgnore = new List<IPropertySymbol>();

        foreach (var sourceProp in sourceProperties)
        {
            // Check direct mapping
            if (destProperties.Any(p => string.Equals(p.Name, sourceProp.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // Check explicit configuration
            if (AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember(invocation, sourceProp.Name, semanticModel))
            {
                continue;
            }
            
            // Flattening check (simplified)
            bool matchesFlattening = false;
            foreach (var destProp in destProperties)
            {
                if (destProp.Name.StartsWith(sourceProp.Name, StringComparison.OrdinalIgnoreCase) &&
                    destProp.Name.Length > sourceProp.Name.Length)
                {
                    matchesFlattening = true; 
                    break;
                }
            }
            if (matchesFlattening) continue;

            // Check if already configured with ForSourceMember (Ignore)
            if (IsPropertyConfiguredWithForSourceMember(invocation, sourceProp.Name))
            {
                continue;
            }

            propertiesToIgnore.Add(sourceProp);
        }

        if (!propertiesToIgnore.Any())
        {
            return document;
        }

        InvocationExpressionSyntax currentInvocation = invocation;
        foreach (var prop in propertiesToIgnore)
        {
            currentInvocation = CodeFixSyntaxHelper.CreateForSourceMemberWithDoNotValidate(currentInvocation, prop.Name);
        }

        SyntaxNode newRoot = root.ReplaceNode(invocation, currentInvocation);
        return document.WithSyntaxRoot(newRoot);
    }

    private bool IsPropertyConfiguredWithForSourceMember(InvocationExpressionSyntax invocation, string propertyName)
    {
        // Helper to check if ForSourceMember is already present to avoid duplicates
        var current = invocation;
        while (current.Parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax parentInvocation)
        {
            if (memberAccess.Name.Identifier.Text == "ForSourceMember" &&
                parentInvocation.ArgumentList.Arguments.Count > 0)
            {
                var firstArg = parentInvocation.ArgumentList.Arguments[0].Expression;
                if (firstArg.ToString().Contains($".{propertyName}")) // Simple string check for speed
                {
                    return true;
                }
            }
            current = parentInvocation;
        }
        return false;
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

        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        var destProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destType, false);

        var propertiesToAdd = new List<(string Name, string Type)>();

        foreach (var sourceProp in sourceProperties)
        {
            // Check direct mapping
            if (destProperties.Any(p => string.Equals(p.Name, sourceProp.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            
            // Check configuration or flattening... (simplified for creation logic - assume if unmapped we create it)
            if (AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember(invocation, sourceProp.Name, semanticModel))
            {
                continue;
            }
            
            bool matchesFlattening = false;
            foreach (var destProp in destProperties)
            {
                if (destProp.Name.StartsWith(sourceProp.Name, StringComparison.OrdinalIgnoreCase) &&
                    destProp.Name.Length > sourceProp.Name.Length)
                {
                    matchesFlattening = true; 
                    break;
                }
            }
            if (matchesFlattening) continue;

            propertiesToAdd.Add((sourceProp.Name, sourceProp.Type.ToDisplayString()));
        }

        if (!propertiesToAdd.Any()) return document.Project.Solution;

        return await AddPropertiesToDestinationAsync(document, destType, propertiesToAdd);
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

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.DataIntegrity;

/// <summary>
///     Code fix provider for AM004 diagnostic - Missing Destination Property.
///     Provides fixes for source properties that don't have corresponding destination properties.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM004_MissingDestinationPropertyCodeFixProvider))]
[Shared]
public class AM004_MissingDestinationPropertyCodeFixProvider : CodeFixProvider
{
    /// <summary>
    ///     Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ["AM004"];

    /// <summary>
    ///     Gets the fix all provider for batch fixes.
    /// </summary>
    /// <returns>The batch fixer provider.</returns>
    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <summary>
    ///     Registers code fixes for the specified context.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return;
        }

        foreach (Diagnostic? diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue("PropertyName", out string? propertyName) ||
                !diagnostic.Properties.TryGetValue("PropertyType", out string? propertyType) ||
                string.IsNullOrEmpty(propertyName) || string.IsNullOrEmpty(propertyType))
            {
                continue;
            }

            SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan);
            if (node is not InvocationExpressionSyntax invocation)
            {
                continue;
            }

            // Fix 1: Ignore the source property using ForSourceMember with DoNotValidate
            var ignoreAction = CodeAction.Create(
                $"Ignore source property '{propertyName}' (prevent data loss warning)",
                cancellationToken =>
                {
                    InvocationExpressionSyntax newInvocation =
                        CodeFixSyntaxHelper.CreateForSourceMemberWithDoNotValidate(invocation, propertyName!);
                    SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                $"Ignore_{propertyName}");

            context.RegisterCodeFix(ignoreAction, diagnostic);

            // Fix 2: Add custom mapping using ForMember (if destination property doesn't exist)
            var customMappingAction = CodeAction.Create(
                $"Add custom mapping for '{propertyName}' (requires destination property)",
                cancellationToken =>
                {
                    SyntaxTrivia commentTrivia = SyntaxFactory.Comment(
                        $"// TODO: Create destination property or map '{propertyName}' to an existing property");
                    InvocationExpressionSyntax newInvocation = invocation.WithLeadingTrivia(
                        invocation.GetLeadingTrivia()
                            .Add(commentTrivia)
                            .Add(SyntaxFactory.EndOfLine("\n")));

                    SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                $"CustomMapping_{propertyName}");

            context.RegisterCodeFix(customMappingAction, diagnostic);

            // Fix 3: Add ForMember with MapFrom to combine multiple properties (for string types)
            if (!string.IsNullOrEmpty(propertyType) && TypeConversionHelper.IsStringType(propertyType!))
            {
                var combineAction = CodeAction.Create(
                    $"Map '{propertyName}' to existing property with custom logic",
                    cancellationToken =>
                    {
                        // Create a placeholder mapping that concatenates properties
                        InvocationExpressionSyntax newInvocation = CodeFixSyntaxHelper
                            .CreateForSourceMemberWithDoNotValidate(invocation, propertyName!)
                            .WithLeadingTrivia(
                                SyntaxFactory.Comment(
                                    $"// TODO: Map '{propertyName}' to destination property with custom logic"));

                        SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    },
                    $"Combine_{propertyName}");

                context.RegisterCodeFix(combineAction, diagnostic);
            }

            // Fix 4: Create missing destination property
            var createPropertyAction = CodeAction.Create(
                $"Create property '{propertyName}' in destination type",
                cancellationToken => CreateDestinationPropertyAsync(context.Document, invocation, propertyName!, propertyType!, cancellationToken),
                $"CreateProperty_{propertyName}");
            
            context.RegisterCodeFix(createPropertyAction, diagnostic);
        }
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
        
        // Check if the destination type is source code (not metadata)
        if (destType.Locations.All(l => !l.IsInSource))
        {
             return document.Project.Solution;
        }

        var syntaxReference = destType.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxReference == null) return document.Project.Solution;
        
        var destSyntaxRoot = await syntaxReference.SyntaxTree.GetRootAsync(cancellationToken);
        var destClassDecl = destSyntaxRoot.FindNode(syntaxReference.Span) as ClassDeclarationSyntax;
        
        if (destClassDecl == null) return document.Project.Solution;
        
        // Create property syntax
        var newProperty = SyntaxFactory.PropertyDeclaration(
            SyntaxFactory.ParseTypeName(propertyType),
            SyntaxFactory.Identifier(propertyName))
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(new[]
            {
                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            })));
            
        var newClassDecl = destClassDecl.AddMembers(newProperty);
        var newDestRoot = destSyntaxRoot.ReplaceNode(destClassDecl, newClassDecl);
        
        var destDocument = document.Project.Solution.GetDocument(destSyntaxRoot.SyntaxTree);
        if (destDocument == null) return document.Project.Solution;
        
        return document.Project.Solution.WithDocumentSyntaxRoot(destDocument.Id, newDestRoot);
    }
}

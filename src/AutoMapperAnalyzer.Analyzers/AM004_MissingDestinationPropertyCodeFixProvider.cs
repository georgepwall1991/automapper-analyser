using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
/// Code fix provider for AM004 diagnostic - Missing Destination Property.
/// Provides fixes for source properties that don't have corresponding destination properties.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM004_MissingDestinationPropertyCodeFixProvider)), Shared]
public class AM004_MissingDestinationPropertyCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ["AM004"];

    /// <summary>
    /// Gets the fix all provider for batch fixes.
    /// </summary>
    /// <returns>The batch fixer provider.</returns>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>
    /// Registers code fixes for the specified context.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue("PropertyName", out var propertyName) ||
                !diagnostic.Properties.TryGetValue("PropertyType", out var propertyType) ||
                string.IsNullOrEmpty(propertyName) || string.IsNullOrEmpty(propertyType))
            {
                continue;
            }

            var node = root.FindNode(diagnostic.Location.SourceSpan);
            if (node is not InvocationExpressionSyntax invocation)
            {
                continue;
            }

            // Fix 1: Ignore the source property using ForSourceMember with DoNotValidate
            var ignoreAction = CodeAction.Create(
                title: $"Ignore source property '{propertyName}' (prevent data loss warning)",
                createChangedDocument: cancellationToken =>
                {
                    var newRoot = AddForSourceMemberIgnore(root, invocation, propertyName!);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"Ignore_{propertyName}");

            context.RegisterCodeFix(ignoreAction, context.Diagnostics);

            // Fix 2: Add custom mapping using ForMember (if destination property doesn't exist)
            var customMappingAction = CodeAction.Create(
                title: $"Add custom mapping for '{propertyName}' (requires destination property)",
                createChangedDocument: cancellationToken =>
                {
                    var newRoot = AddCustomMappingComment(root, invocation, propertyName!, propertyType!);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"CustomMapping_{propertyName}");

            context.RegisterCodeFix(customMappingAction, context.Diagnostics);

            // Fix 3: Add ForMember with MapFrom to combine multiple properties
            if (!string.IsNullOrEmpty(propertyType) && IsStringType(propertyType!))
            {
                var combineAction = CodeAction.Create(
                    title: $"Map '{propertyName}' to existing property with custom logic",
                    createChangedDocument: cancellationToken =>
                    {
                        var newRoot = AddCombinedPropertyMapping(root, invocation, propertyName!);
                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    },
                    equivalenceKey: $"Combine_{propertyName}");

                context.RegisterCodeFix(combineAction, context.Diagnostics);
            }
        }
    }

    private SyntaxNode AddForSourceMemberIgnore(SyntaxNode root, InvocationExpressionSyntax invocation, string propertyName)
    {
        var forSourceMemberCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                invocation,
                SyntaxFactory.IdentifierName("ForSourceMember")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("src")),
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("src"),
                                    SyntaxFactory.IdentifierName(propertyName)))),
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("opt")),
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName("opt"),
                                        SyntaxFactory.IdentifierName("DoNotValidate")))))
                    })));

        return root.ReplaceNode(invocation, forSourceMemberCall);
    }

    private SyntaxNode AddCustomMappingComment(SyntaxNode root, InvocationExpressionSyntax invocation, 
        string propertyName, string propertyType)
    {
        // Add a ForMember call with a comment indicating the user needs to add the destination property
        var forMemberCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                invocation,
                SyntaxFactory.IdentifierName("ForMember")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("dest")),
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("dest"),
                                    SyntaxFactory.IdentifierName(propertyName)))), // This will cause compilation error until user adds the property
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("opt")),
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName("opt"),
                                        SyntaxFactory.IdentifierName("MapFrom")))
                                    .WithArgumentList(
                                        SyntaxFactory.ArgumentList(
                                            SyntaxFactory.SingletonSeparatedList(
                                                SyntaxFactory.Argument(
                                                    SyntaxFactory.SimpleLambdaExpression(
                                                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("src")),
                                                        SyntaxFactory.MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            SyntaxFactory.IdentifierName("src"),
                                                            SyntaxFactory.IdentifierName(propertyName)))))))))
                    })))
            .WithLeadingTrivia(
                SyntaxFactory.Comment($"// TODO: Add '{propertyName}' property of type '{propertyType}' to destination class"));

        return root.ReplaceNode(invocation, forMemberCall);
    }

    private SyntaxNode AddCombinedPropertyMapping(SyntaxNode root, InvocationExpressionSyntax invocation, string propertyName)
    {
        // For string properties, provide an example of combining with existing properties
        var forMemberCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                invocation,
                SyntaxFactory.IdentifierName("ForMember")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("dest")),
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("dest"),
                                    SyntaxFactory.IdentifierName("CombinedProperty")))), // Generic name for combined property
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("opt")),
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName("opt"),
                                        SyntaxFactory.IdentifierName("MapFrom")))
                                    .WithArgumentList(
                                        SyntaxFactory.ArgumentList(
                                            SyntaxFactory.SingletonSeparatedList(
                                                SyntaxFactory.Argument(
                                                    SyntaxFactory.SimpleLambdaExpression(
                                                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("src")),
                                                        SyntaxFactory.ParseExpression($"$\"{{src.{propertyName}}}\""))))))))
                    })))
            .WithLeadingTrivia(
                SyntaxFactory.Comment($"// TODO: Replace 'CombinedProperty' with actual destination property name"));

        return root.ReplaceNode(invocation, forMemberCall);
    }

    private bool IsStringType(string propertyType)
    {
        return propertyType == "string" || propertyType == "System.String";
    }
}
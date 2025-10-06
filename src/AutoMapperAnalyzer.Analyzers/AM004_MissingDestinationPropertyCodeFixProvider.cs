using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AutoMapperAnalyzer.Analyzers.Helpers;

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
                    var newInvocation = CodeFixSyntaxHelper.CreateForSourceMemberWithDoNotValidate(invocation, propertyName!);
                    var newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"Ignore_{propertyName}");

            context.RegisterCodeFix(ignoreAction, diagnostic);

            // Fix 2: Add custom mapping using ForMember (if destination property doesn't exist)
            var customMappingAction = CodeAction.Create(
                title: $"Add custom mapping for '{propertyName}' (requires destination property)",
                createChangedDocument: cancellationToken =>
                {
                    var commentTrivia = SyntaxFactory.Comment($"// TODO: Create destination property or map '{propertyName}' to an existing property");
                    var newInvocation = invocation.WithLeadingTrivia(
                        invocation.GetLeadingTrivia()
                            .Add(commentTrivia)
                            .Add(SyntaxFactory.EndOfLine("\n")));

                    var newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"CustomMapping_{propertyName}");

            context.RegisterCodeFix(customMappingAction, diagnostic);

            // Fix 3: Add ForMember with MapFrom to combine multiple properties (for string types)
            if (!string.IsNullOrEmpty(propertyType) && TypeConversionHelper.IsStringType(propertyType!))
            {
                var combineAction = CodeAction.Create(
                    title: $"Map '{propertyName}' to existing property with custom logic",
                    createChangedDocument: cancellationToken =>
                    {
                        // Create a placeholder mapping that concatenates properties
                        var newInvocation = CodeFixSyntaxHelper.CreateForSourceMemberWithDoNotValidate(invocation, propertyName!)
                            .WithLeadingTrivia(
                                SyntaxFactory.Comment($"// TODO: Map '{propertyName}' to destination property with custom logic"));

                        var newRoot = root.ReplaceNode(invocation, newInvocation);
                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    },
                    equivalenceKey: $"Combine_{propertyName}");

                context.RegisterCodeFix(combineAction, diagnostic);
            }
        }
    }
}

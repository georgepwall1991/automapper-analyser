using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers.DataIntegrity;

/// <summary>
/// Code fix provider for AM011 diagnostic - Unmapped Required Property.
/// Provides fixes for required properties that are not mapped from source.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM011_UnmappedRequiredPropertyCodeFixProvider)), Shared]
public class AM011_UnmappedRequiredPropertyCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ["AM011"];

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

            // Fix 1: Add ForMember mapping with default value
            var defaultValueAction = CodeAction.Create(
                title: $"Map '{propertyName}' to default value",
                createChangedDocument: cancellationToken =>
                {
                    var defaultValue = TypeConversionHelper.GetDefaultValueForType(propertyType!);
                    var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName!, defaultValue);
                    var newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"DefaultValue_{propertyName}");

            context.RegisterCodeFix(defaultValueAction, context.Diagnostics);

            // Fix 2: Add ForMember mapping with constant value
            var constantValueAction = CodeAction.Create(
                title: $"Map '{propertyName}' to constant value",
                createChangedDocument: cancellationToken =>
                {
                    var constantValue = TypeConversionHelper.GetSampleValueForType(propertyType!);
                    var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName!, constantValue);
                    var newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"ConstantValue_{propertyName}");

            context.RegisterCodeFix(constantValueAction, context.Diagnostics);

            // Fix 3: Add ForMember mapping with custom logic placeholder
            var customLogicAction = CodeAction.Create(
                title: $"Map '{propertyName}' with custom logic (requires implementation)",
                createChangedDocument: cancellationToken =>
                {
                    var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName!, "null")
                        .WithLeadingTrivia(
                            SyntaxFactory.Comment($"// TODO: Implement custom mapping logic for required property '{propertyName}'"));
                    var newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"CustomLogic_{propertyName}");

            context.RegisterCodeFix(customLogicAction, context.Diagnostics);

            // Fix 4: Add comment suggesting to add property to source class
            var addPropertyAction = CodeAction.Create(
                title: $"Add comment to suggest adding '{propertyName}' to source class",
                createChangedDocument: cancellationToken =>
                {
                    var commentTrivia = SyntaxFactory.Comment($"// TODO: Consider adding '{propertyName}' property of type '{propertyType}' to source class");
                    var secondCommentTrivia = SyntaxFactory.Comment($"// This will ensure the required property is automatically mapped");

                    var newInvocation = invocation.WithLeadingTrivia(
                        invocation.GetLeadingTrivia()
                            .Add(commentTrivia)
                            .Add(SyntaxFactory.EndOfLine("\n"))
                            .Add(secondCommentTrivia)
                            .Add(SyntaxFactory.EndOfLine("\n")));

                    var newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"AddProperty_{propertyName}");

            context.RegisterCodeFix(addPropertyAction, context.Diagnostics);
        }
    }
}

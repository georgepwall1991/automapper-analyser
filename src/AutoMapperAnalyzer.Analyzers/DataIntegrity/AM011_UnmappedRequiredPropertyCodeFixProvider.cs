using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.DataIntegrity;

/// <summary>
///     Code fix provider for AM011 diagnostic - Unmapped Required Property.
///     Provides fixes for required properties that are not mapped from source.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM011_UnmappedRequiredPropertyCodeFixProvider))]
[Shared]
public class AM011_UnmappedRequiredPropertyCodeFixProvider : CodeFixProvider
{
    /// <summary>
    ///     Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ["AM011"];

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

            // Fix 1: Add ForMember mapping with default value
            var defaultValueAction = CodeAction.Create(
                $"Map '{propertyName}' to default value",
                cancellationToken =>
                {
                    string defaultValue = TypeConversionHelper.GetDefaultValueForType(propertyType!);
                    InvocationExpressionSyntax newInvocation =
                        CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName!, defaultValue);
                    SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                $"DefaultValue_{propertyName}");

            context.RegisterCodeFix(defaultValueAction, context.Diagnostics);

            // Fix 2: Add ForMember mapping with constant value
            var constantValueAction = CodeAction.Create(
                $"Map '{propertyName}' to constant value",
                cancellationToken =>
                {
                    string constantValue = TypeConversionHelper.GetSampleValueForType(propertyType!);
                    InvocationExpressionSyntax newInvocation =
                        CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName!, constantValue);
                    SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                $"ConstantValue_{propertyName}");

            context.RegisterCodeFix(constantValueAction, context.Diagnostics);

            // Fix 3: Add ForMember mapping with custom logic placeholder
            var customLogicAction = CodeAction.Create(
                $"Map '{propertyName}' with custom logic (requires implementation)",
                cancellationToken =>
                {
                    // Use default(T) as a safe placeholder that compiles for both reference and value types
                    InvocationExpressionSyntax newInvocation = CodeFixSyntaxHelper
                        .CreateForMemberWithMapFrom(invocation, propertyName!, $"default({propertyType})")
                        .WithLeadingTrivia(
                            invocation.GetLeadingTrivia()
                                .Add(SyntaxFactory.Comment(
                                    $"// TODO: Implement custom mapping logic for required property '{propertyName}'"))
                                .Add(SyntaxFactory.ElasticCarriageReturnLineFeed));
                    SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                $"CustomLogic_{propertyName}");

            context.RegisterCodeFix(customLogicAction, context.Diagnostics);

            // Fix 4: Add comment suggesting to add property to source class
            var addPropertyAction = CodeAction.Create(
                $"Add comment to suggest adding '{propertyName}' to source class",
                cancellationToken =>
                {
                    SyntaxTrivia commentTrivia = SyntaxFactory.Comment(
                        $"// TODO: Consider adding '{propertyName}' property of type '{propertyType}' to source class");
                    SyntaxTrivia secondCommentTrivia =
                        SyntaxFactory.Comment("// This will ensure the required property is automatically mapped");

                    InvocationExpressionSyntax newInvocation = invocation.WithLeadingTrivia(
                        invocation.GetLeadingTrivia()
                            .Add(commentTrivia)
                            .Add(SyntaxFactory.EndOfLine("\n"))
                            .Add(secondCommentTrivia)
                            .Add(SyntaxFactory.EndOfLine("\n")));

                    SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                $"AddProperty_{propertyName}");

            context.RegisterCodeFix(addPropertyAction, context.Diagnostics);
        }
    }
}

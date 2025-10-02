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
/// Code fix provider for AM005 diagnostic - Case Sensitivity Mismatch.
/// Provides fixes for property mapping issues caused by case sensitivity differences.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM005_CaseSensitivityMismatchCodeFixProvider)), Shared]
public class AM005_CaseSensitivityMismatchCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ["AM005"];

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
            if (!diagnostic.Properties.TryGetValue("SourcePropertyName", out var sourcePropertyName) ||
                !diagnostic.Properties.TryGetValue("DestinationPropertyName", out var destinationPropertyName) ||
                string.IsNullOrEmpty(sourcePropertyName) || string.IsNullOrEmpty(destinationPropertyName))
            {
                continue;
            }

            var node = root.FindNode(diagnostic.Location.SourceSpan);
            if (node is not InvocationExpressionSyntax invocation)
            {
                continue;
            }

            // Fix 1: Add explicit ForMember mapping to handle case sensitivity
            var explicitMappingAction = CodeAction.Create(
                title: $"Map '{sourcePropertyName}' to '{destinationPropertyName}' explicitly",
                createChangedDocument: cancellationToken =>
                {
                    var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                        invocation,
                        destinationPropertyName!,
                        $"src.{sourcePropertyName}");
                    var newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"ExplicitMapping_{sourcePropertyName}_{destinationPropertyName}");

            context.RegisterCodeFix(explicitMappingAction, context.Diagnostics);

            // Fix 2: Add configuration comment for case-insensitive mapping
            var caseInsensitiveConfigAction = CodeAction.Create(
                title: "Add comment about case-insensitive configuration",
                createChangedDocument: cancellationToken =>
                {
                    var commentTrivia = SyntaxFactory.Comment($"// TODO: Consider configuring case-insensitive property matching in MapperConfiguration");
                    var secondCommentTrivia = SyntaxFactory.Comment($"// Alternative: cfg.DestinationMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;");
                    var thirdCommentTrivia = SyntaxFactory.Comment($"// or cfg.SourceMemberNamingConvention = PascalCaseNamingConvention.Instance;");

                    var newInvocation = invocation.WithLeadingTrivia(
                        invocation.GetLeadingTrivia()
                            .Add(commentTrivia)
                            .Add(SyntaxFactory.EndOfLine("\n"))
                            .Add(secondCommentTrivia)
                            .Add(SyntaxFactory.EndOfLine("\n"))
                            .Add(thirdCommentTrivia)
                            .Add(SyntaxFactory.EndOfLine("\n")));

                    var newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"CaseInsensitiveConfig_{sourcePropertyName}_{destinationPropertyName}");

            context.RegisterCodeFix(caseInsensitiveConfigAction, context.Diagnostics);

            // Fix 3: Add proper casing correction comment
            var casingCorrectionAction = CodeAction.Create(
                title: $"Add comment to standardize casing (rename '{sourcePropertyName}' to '{destinationPropertyName}')",
                createChangedDocument: cancellationToken =>
                {
                    var commentTrivia = SyntaxFactory.Comment($"// TODO: Standardize property casing - consider renaming '{sourcePropertyName}' to '{destinationPropertyName}' in source class");
                    var secondCommentTrivia = SyntaxFactory.Comment($"// This will eliminate case sensitivity issues and improve code consistency");

                    var newInvocation = invocation.WithLeadingTrivia(
                        invocation.GetLeadingTrivia()
                            .Add(commentTrivia)
                            .Add(SyntaxFactory.EndOfLine("\n"))
                            .Add(secondCommentTrivia)
                            .Add(SyntaxFactory.EndOfLine("\n")));

                    var newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"CasingCorrection_{sourcePropertyName}_{destinationPropertyName}");

            context.RegisterCodeFix(casingCorrectionAction, context.Diagnostics);
        }
    }
}

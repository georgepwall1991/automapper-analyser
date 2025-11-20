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
///     Code fix provider for AM005 diagnostic - Case Sensitivity Mismatch.
///     Provides fixes for property mapping issues caused by case sensitivity differences.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM005_CaseSensitivityMismatchCodeFixProvider))]
[Shared]
public class AM005_CaseSensitivityMismatchCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <summary>
    ///     Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ["AM005"];

    /// <summary>
    ///     Registers code fixes for the specified context.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        foreach (Diagnostic? diagnostic in context.Diagnostics)
        {
            var properties = TryGetDiagnosticProperties(diagnostic, "SourcePropertyName", "DestinationPropertyName");
            if (properties == null)
            {
                continue;
            }

            InvocationExpressionSyntax? invocation = GetCreateMapInvocation(operationContext.Root, diagnostic);
            if (invocation == null)
            {
                continue;
            }

            string sourcePropertyName = properties["SourcePropertyName"];
            string destinationPropertyName = properties["DestinationPropertyName"];

            // Fix 1: Add explicit ForMember mapping to handle case sensitivity
            var explicitMappingAction = CodeAction.Create(
                $"Map '{sourcePropertyName}' to '{destinationPropertyName}' explicitly",
                cancellationToken =>
                {
                    InvocationExpressionSyntax newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                        invocation,
                        destinationPropertyName,
                        $"src.{sourcePropertyName}");
                    return ReplaceNodeAsync(context.Document, operationContext.Root, invocation, newInvocation);
                },
                $"ExplicitMapping_{sourcePropertyName}_{destinationPropertyName}");

            context.RegisterCodeFix(explicitMappingAction, context.Diagnostics);

            // Fix 2: Add configuration comment for case-insensitive mapping
            var caseInsensitiveConfigAction = CodeAction.Create(
                "Add comment about case-insensitive configuration",
                cancellationToken =>
                {
                    SyntaxTrivia commentTrivia = SyntaxFactory.Comment(
                        "// TODO: Consider configuring case-insensitive property matching in MapperConfiguration");
                    SyntaxTrivia secondCommentTrivia = SyntaxFactory.Comment(
                        "// Alternative: cfg.DestinationMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;");
                    SyntaxTrivia thirdCommentTrivia =
                        SyntaxFactory.Comment(
                            "// or cfg.SourceMemberNamingConvention = PascalCaseNamingConvention.Instance;");

                    InvocationExpressionSyntax newInvocation = invocation.WithLeadingTrivia(
                        invocation.GetLeadingTrivia()
                            .Add(commentTrivia)
                            .Add(SyntaxFactory.EndOfLine("\n"))
                            .Add(secondCommentTrivia)
                            .Add(SyntaxFactory.EndOfLine("\n"))
                            .Add(thirdCommentTrivia)
                            .Add(SyntaxFactory.EndOfLine("\n")));

                    return ReplaceNodeAsync(context.Document, operationContext.Root, invocation, newInvocation);
                },
                $"CaseInsensitiveConfig_{sourcePropertyName}_{destinationPropertyName}");

            context.RegisterCodeFix(caseInsensitiveConfigAction, context.Diagnostics);

            // Fix 3: Add proper casing correction comment
            var casingCorrectionAction = CodeAction.Create(
                $"Add comment to standardize casing (rename '{sourcePropertyName}' to '{destinationPropertyName}')",
                cancellationToken =>
                {
                    SyntaxTrivia commentTrivia = SyntaxFactory.Comment(
                        $"// TODO: Standardize property casing - consider renaming '{sourcePropertyName}' to '{destinationPropertyName}' in source class");
                    SyntaxTrivia secondCommentTrivia =
                        SyntaxFactory.Comment(
                            "// This will eliminate case sensitivity issues and improve code consistency");

                    InvocationExpressionSyntax newInvocation = invocation.WithLeadingTrivia(
                        invocation.GetLeadingTrivia()
                            .Add(commentTrivia)
                            .Add(SyntaxFactory.EndOfLine("\n"))
                            .Add(secondCommentTrivia)
                            .Add(SyntaxFactory.EndOfLine("\n")));

                    return ReplaceNodeAsync(context.Document, operationContext.Root, invocation, newInvocation);
                },
                $"CasingCorrection_{sourcePropertyName}_{destinationPropertyName}");

            context.RegisterCodeFix(casingCorrectionAction, context.Diagnostics);
        }
    }
}

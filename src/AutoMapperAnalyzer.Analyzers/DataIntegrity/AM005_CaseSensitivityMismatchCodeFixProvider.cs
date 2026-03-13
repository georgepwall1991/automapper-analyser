using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
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

            // Fix 1: Add explicit ForMember mapping to handle case sensitivity.
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

            context.RegisterCodeFix(explicitMappingAction, diagnostic);
        }
    }
}

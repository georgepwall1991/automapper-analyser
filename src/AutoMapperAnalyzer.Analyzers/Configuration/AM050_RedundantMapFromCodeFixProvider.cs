using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Configuration;

/// <summary>
///     Code fix provider for removing redundant MapFrom configurations.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM050_RedundantMapFromCodeFixProvider))]
[Shared]
public class AM050_RedundantMapFromCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM050");

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        foreach (Diagnostic? diagnostic in context.Diagnostics)
        {
            SyntaxNode? node = operationContext.Root.FindNode(diagnostic.Location.SourceSpan);

            // The analyzer reports on the 'MapFrom' invocation, which is inside ForMember
            // We need to find the outer ForMember invocation
            InvocationExpressionSyntax? forMemberInvocation = node?.AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(inv =>
                    inv.Expression is MemberAccessExpressionSyntax ma &&
                    ma.Name.Identifier.Text == "ForMember");

            if (forMemberInvocation != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "Remove redundant mapping",
                        c => RemoveRedundantMapping(context.Document, operationContext.Root, forMemberInvocation),
                        "RemoveRedundantMapping"),
                    diagnostic);
            }
        }
    }

    private Task<Document> RemoveRedundantMapping(Document document, SyntaxNode root,
        InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            // Replace the whole ForMember(...) invocation with the expression it was called on
            // e.g. CreateMap<A,B>().ForMember(...) -> CreateMap<A,B>()
            return ReplaceNodeAsync(document, root, invocation,
                memberAccess.Expression.WithTrailingTrivia(invocation.GetTrailingTrivia()));
        }

        return Task.FromResult(document);
    }
}

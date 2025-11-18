using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Analyzers.Configuration;

/// <summary>
///     Code fix provider for removing redundant MapFrom configurations.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM050_RedundantMapFromCodeFixProvider))]
[Shared]
public class AM050_RedundantMapFromCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM050");

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (Diagnostic? diagnostic in context.Diagnostics)
        {
            SyntaxNode? root =
                await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;
            SyntaxNode? node = root?.FindNode(diagnosticSpan);

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
                        c => RemoveRedundantMapping(context.Document, forMemberInvocation, c),
                        "RemoveRedundantMapping"),
                    diagnostic);
            }
        }
    }

    private async Task<Document> RemoveRedundantMapping(Document document, InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return document;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            // Replace the whole ForMember(...) invocation with the expression it was called on
            // e.g. CreateMap<A,B>().ForMember(...) -> CreateMap<A,B>()

            // We assume the chain structure invocation -> memberAccess -> expression
            // Replacing invocation with expression effectively removes ".ForMember(...)"

            SyntaxNode newRoot = root.ReplaceNode(invocation,
                memberAccess.Expression.WithTrailingTrivia(invocation.GetTrailingTrivia()));
            return document.WithSyntaxRoot(newRoot);
        }

        return document;
    }
}

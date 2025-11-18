using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Analyzers.Configuration;

/// <summary>
///     Code fix provider for removing duplicate mapping registrations.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM041_DuplicateMappingCodeFixProvider))]
[Shared]
public class AM041_DuplicateMappingCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM041");

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

            // Find the invocation expression
            InvocationExpressionSyntax? invocation = node as InvocationExpressionSyntax ??
                                                     node?.AncestorsAndSelf().OfType<InvocationExpressionSyntax>()
                                                         .FirstOrDefault();

            if (invocation != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "Remove duplicate mapping",
                        c => RemoveDuplicateMapping(context.Document, invocation, c),
                        "RemoveDuplicateMapping"),
                    diagnostic);
            }
        }
    }

    private async Task<Document> RemoveDuplicateMapping(Document document, InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return document;
        }

        // Check if it is ReverseMap()
        if (IsReverseMapInvocation(invocation))
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                // Replace the whole ReverseMap() invocation with the expression it was called on
                // e.g. CreateMap<A,B>().ReverseMap() -> CreateMap<A,B>()
                SyntaxNode newRoot = root.ReplaceNode(invocation, memberAccess.Expression);
                return document.WithSyntaxRoot(newRoot);
            }
        }

        // Otherwise, assume it's CreateMap and remove the whole statement
        ExpressionStatementSyntax? statement =
            invocation.AncestorsAndSelf().OfType<ExpressionStatementSyntax>().FirstOrDefault();
        if (statement != null)
        {
            SyntaxNode? newRoot = root.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
            return document.WithSyntaxRoot(newRoot!);
        }

        return document;
    }

    private bool IsReverseMapInvocation(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.Text == "ReverseMap";
        }

        return false;
    }
}

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Configuration;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM041_DuplicateMappingCodeFixProvider)), Shared]
public class AM041_DuplicateMappingCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM041");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var node = root?.FindNode(diagnosticSpan);

            // Find the invocation expression
            var invocation = node as InvocationExpressionSyntax ?? 
                             node?.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
            
            if (invocation != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Remove duplicate mapping",
                        createChangedDocument: c => RemoveDuplicateMapping(context.Document, invocation, c),
                        equivalenceKey: "RemoveDuplicateMapping"),
                    diagnostic);
            }
        }
    }

    private async Task<Document> RemoveDuplicateMapping(Document document, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Check if it is ReverseMap()
        if (IsReverseMapInvocation(invocation))
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                // Replace the whole ReverseMap() invocation with the expression it was called on
                // e.g. CreateMap<A,B>().ReverseMap() -> CreateMap<A,B>()
                var newRoot = root.ReplaceNode(invocation, memberAccess.Expression);
                return document.WithSyntaxRoot(newRoot);
            }
        }

        // Otherwise, assume it's CreateMap and remove the whole statement
        var statement = invocation.AncestorsAndSelf().OfType<ExpressionStatementSyntax>().FirstOrDefault();
        if (statement != null)
        {
            var newRoot = root.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
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


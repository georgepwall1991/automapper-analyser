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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM050_RedundantMapFromCodeFixProvider)), Shared]
public class AM050_RedundantMapFromCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM050");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var node = root?.FindNode(diagnosticSpan);

            // The analyzer reports on the 'MapFrom' invocation, which is inside ForMember
            // We need to find the outer ForMember invocation
            var forMemberInvocation = node?.AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(inv => 
                    inv.Expression is MemberAccessExpressionSyntax ma && 
                    ma.Name.Identifier.Text == "ForMember");
            
            if (forMemberInvocation != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Remove redundant mapping",
                        createChangedDocument: c => RemoveRedundantMapping(context.Document, forMemberInvocation, c),
                        equivalenceKey: "RemoveRedundantMapping"),
                    diagnostic);
            }
        }
    }

    private async Task<Document> RemoveRedundantMapping(Document document, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            // Replace the whole ForMember(...) invocation with the expression it was called on
            // e.g. CreateMap<A,B>().ForMember(...) -> CreateMap<A,B>()
            
            // We assume the chain structure invocation -> memberAccess -> expression
            // Replacing invocation with expression effectively removes ".ForMember(...)"
            
            var newRoot = root.ReplaceNode(invocation, memberAccess.Expression.WithTrailingTrivia(invocation.GetTrailingTrivia()));
            return document.WithSyntaxRoot(newRoot);
        }

        return document;
    }
}


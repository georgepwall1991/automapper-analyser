using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Configuration;

/// <summary>
///     Code fix provider for removing duplicate mapping registrations.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM041_DuplicateMappingCodeFixProvider))]
[Shared]
public class AM041_DuplicateMappingCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM041");

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

            // Find the invocation expression (could be CreateMap or ReverseMap)
            InvocationExpressionSyntax? invocation = node as InvocationExpressionSyntax ??
                                                     node?.AncestorsAndSelf().OfType<InvocationExpressionSyntax>()
                                                         .FirstOrDefault();

            if (invocation != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "Remove duplicate mapping",
                        c => RemoveDuplicateMapping(context.Document, operationContext.Root, invocation, c),
                        "RemoveDuplicateMapping"),
                    diagnostic);
            }
        }
    }

    private Task<Document> RemoveDuplicateMapping(Document document, SyntaxNode root,
        InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
    {
        // Check if it is ReverseMap()
        if (IsReverseMapInvocation(invocation))
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                // Replace the whole ReverseMap() invocation with the expression it was called on
                // e.g. CreateMap<A,B>().ReverseMap() -> CreateMap<A,B>()
                return ReplaceNodeAsync(document, root, invocation, memberAccess.Expression);
            }
        }

        // Otherwise, assume it's CreateMap and remove the whole statement
        ExpressionStatementSyntax? statement =
            invocation.AncestorsAndSelf().OfType<ExpressionStatementSyntax>().FirstOrDefault();
        if (statement != null)
        {
            SyntaxNode? newRoot = root.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
            return Task.FromResult(document.WithSyntaxRoot(newRoot!));
        }

        return Task.FromResult(document);
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

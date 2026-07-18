using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Helpers;

internal static class ConditionalDirectiveSafety
{
    public static bool IsInsideConditionalDirectiveRegion(ExpressionStatementSyntax statement)
    {
        int conditionalDepth = 0;
        foreach (SyntaxTrivia trivia in statement.SyntaxTree.GetRoot()
                     .DescendantTrivia(descendIntoTrivia: true))
        {
            if (trivia.SpanStart >= statement.SpanStart)
            {
                break;
            }

            if (trivia.IsKind(SyntaxKind.IfDirectiveTrivia))
            {
                conditionalDepth++;
            }
            else if (trivia.IsKind(SyntaxKind.EndIfDirectiveTrivia) && conditionalDepth > 0)
            {
                conditionalDepth--;
            }
        }

        return conditionalDepth > 0 ||
               statement.DescendantTrivia(descendIntoTrivia: true).Any(trivia =>
                   trivia.SpanStart >= statement.SpanStart &&
                   trivia.SpanStart < statement.Span.End &&
                   (trivia.IsKind(SyntaxKind.IfDirectiveTrivia) ||
                    trivia.IsKind(SyntaxKind.ElifDirectiveTrivia) ||
                    trivia.IsKind(SyntaxKind.ElseDirectiveTrivia) ||
                    trivia.IsKind(SyntaxKind.EndIfDirectiveTrivia)));
    }
}

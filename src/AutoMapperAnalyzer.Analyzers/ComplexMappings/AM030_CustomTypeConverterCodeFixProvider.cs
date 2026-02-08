using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.ComplexMappings;

/// <summary>
///     Code fix provider for AM030 diagnostics.
///     Provides executable fixes for converter-quality diagnostics.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM030_CustomTypeConverterCodeFixProvider))]
[Shared]
public class AM030_CustomTypeConverterCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <summary>
    ///     Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM030");

    /// <summary>
    ///     Registers code fixes for the specified context.
    /// </summary>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (diagnostic.Descriptor != AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule)
            {
                continue;
            }

            SyntaxNode node = operationContext.Root.FindNode(diagnostic.Location.SourceSpan);
            MethodDeclarationSyntax? convertMethod = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (convertMethod == null)
            {
                continue;
            }

            if (convertMethod.ParameterList.Parameters.Count == 0)
            {
                continue;
            }

            string sourceParameterName = convertMethod.ParameterList.Parameters[0].Identifier.ValueText;
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Add null guard for '{sourceParameterName}'",
                    cancellationToken => AddNullGuardAsync(
                        context.Document,
                        operationContext.Root,
                        convertMethod,
                        sourceParameterName,
                        cancellationToken),
                    $"AM030_AddNullGuard_{sourceParameterName}"),
                diagnostic);
        }
    }

    private async Task<Document> AddNullGuardAsync(
        Document document,
        SyntaxNode root,
        MethodDeclarationSyntax convertMethod,
        string sourceParameterName,
        CancellationToken cancellationToken)
    {
        StatementSyntax guardStatement = SyntaxFactory.ParseStatement(
            $"if ({sourceParameterName} == null) throw new ArgumentNullException(nameof({sourceParameterName}));")
            .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed);

        MethodDeclarationSyntax updatedMethod;

        if (convertMethod.Body != null)
        {
            StatementSyntax formattedGuard = guardStatement;
            StatementSyntax? firstStatement = convertMethod.Body.Statements.FirstOrDefault();
            if (firstStatement != null)
            {
                formattedGuard = formattedGuard.WithLeadingTrivia(firstStatement.GetLeadingTrivia());
            }

            BlockSyntax updatedBody = convertMethod.Body.WithStatements(convertMethod.Body.Statements.Insert(0, formattedGuard));
            updatedMethod = convertMethod.WithBody(updatedBody);
        }
        else if (convertMethod.ExpressionBody != null)
        {
            ReturnStatementSyntax returnStatement = SyntaxFactory.ReturnStatement(convertMethod.ExpressionBody.Expression);
            BlockSyntax newBody = SyntaxFactory.Block(guardStatement, returnStatement);

            updatedMethod = convertMethod
                .WithBody(newBody)
                .WithExpressionBody(null)
                .WithSemicolonToken(default);
        }
        else
        {
            return document;
        }

        SyntaxNode newRoot = root.ReplaceNode(convertMethod, updatedMethod);
        if (newRoot is CompilationUnitSyntax compilationUnit)
        {
            newRoot = AddUsingIfMissing(compilationUnit, "System");
        }

        return document.WithSyntaxRoot(newRoot);
    }

    private static CompilationUnitSyntax AddUsingIfMissing(CompilationUnitSyntax root, string namespaceName)
    {
        if (root.Usings.Any(u =>
                u.Name != null &&
                string.Equals(u.Name.ToString(), namespaceName, StringComparison.Ordinal)))
        {
            return root;
        }

        UsingDirectiveSyntax usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName))
            .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed);

        return root.AddUsings(usingDirective);
    }
}

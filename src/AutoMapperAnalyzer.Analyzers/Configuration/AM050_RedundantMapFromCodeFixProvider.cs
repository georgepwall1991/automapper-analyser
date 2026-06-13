using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
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

            // The analyzer reports on the 'MapFrom' invocation, which is inside ForMember/ForPath.
            // We need to find the outer destination configuration invocation.
            InvocationExpressionSyntax? mapFromInvocation = node?.AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(inv =>
                    inv.Expression is MemberAccessExpressionSyntax ma &&
                    ma.Name.Identifier.Text == "MapFrom");

            InvocationExpressionSyntax? destinationConfigurationInvocation = node?.AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(inv =>
                    inv.Expression is MemberAccessExpressionSyntax ma &&
                    ma.Name.Identifier.Text is "ForMember" or "ForPath" &&
                    MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                        inv,
                        operationContext.SemanticModel,
                        ma.Name.Identifier.Text));

            if (destinationConfigurationInvocation != null &&
                mapFromInvocation != null &&
                DestinationConfigurationLambdaContainsOnlyTheMapFrom(destinationConfigurationInvocation, mapFromInvocation))
            {
                string configurationMethodName = GetConfigurationMethodName(destinationConfigurationInvocation) ?? "mapping";
                string propertyName = GetDestinationPropertyName(
                    destinationConfigurationInvocation,
                    operationContext.SemanticModel) ?? "property";
                string equivalenceKey = configurationMethodName == "ForMember"
                    ? $"AM050_RemoveRedundantMapping_{propertyName}"
                    : $"AM050_RemoveRedundant{configurationMethodName}_{propertyName}";
                context.RegisterCodeFix(
                    CodeAction.Create(
                        $"Remove redundant {configurationMethodName} for '{propertyName}'",
                        c => RemoveRedundantMapping(context.Document, operationContext.Root, destinationConfigurationInvocation),
                        equivalenceKey),
                    diagnostic);
            }
        }
    }

    private static bool DestinationConfigurationLambdaContainsOnlyTheMapFrom(
        InvocationExpressionSyntax destinationConfigurationInvocation,
        InvocationExpressionSyntax mapFromInvocation)
    {
        if (destinationConfigurationInvocation.ArgumentList.Arguments.Count < 2)
        {
            return false;
        }

        ExpressionSyntax optionsArgument = destinationConfigurationInvocation.ArgumentList.Arguments[1].Expression;
        CSharpSyntaxNode? body = optionsArgument switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Body,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.Body,
            _ => null
        };

        if (body is BlockSyntax block)
        {
            return block.Statements.Count == 1 &&
                   block.Statements[0] is ExpressionStatementSyntax statement &&
                   statement.Expression == mapFromInvocation;
        }

        return body == mapFromInvocation;
    }

    private Task<Document> RemoveRedundantMapping(Document document, SyntaxNode root,
        InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            // Replace the whole ForMember/ForPath invocation with the expression it was called on.
            // e.g. CreateMap<A,B>().ForMember(...) -> CreateMap<A,B>().
            return ReplaceNodeAsync(document, root, invocation,
                memberAccess.Expression.WithTrailingTrivia(invocation.GetTrailingTrivia()));
        }

        return Task.FromResult(document);
    }

    private static string? GetConfigurationMethodName(InvocationExpressionSyntax destinationConfigurationInvocation)
    {
        return destinationConfigurationInvocation.Expression is MemberAccessExpressionSyntax memberAccess
            ? memberAccess.Name.Identifier.ValueText
            : null;
    }

    private static string? GetDestinationPropertyName(
        InvocationExpressionSyntax destinationConfigurationInvocation,
        SemanticModel semanticModel)
    {
        if (destinationConfigurationInvocation.ArgumentList.Arguments.Count == 0)
        {
            return null;
        }

        SyntaxNode? lambdaBody = AutoMapperAnalysisHelpers.GetLambdaBody(destinationConfigurationInvocation.ArgumentList.Arguments[0].Expression);
        if (lambdaBody is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.ValueText;
        }

        ExpressionSyntax expression = destinationConfigurationInvocation.ArgumentList.Arguments[0].Expression;
        if (expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }

        Optional<object?> constantValue = semanticModel.GetConstantValue(expression);
        return constantValue.HasValue && constantValue.Value is string value
            ? value
            : null;
    }
}

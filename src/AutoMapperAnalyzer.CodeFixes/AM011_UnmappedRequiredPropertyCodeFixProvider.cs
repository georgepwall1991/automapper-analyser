using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM011_UnmappedRequiredPropertyCodeFixProvider)), Shared]
public class AM011_UnmappedRequiredPropertyCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["AM011"];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue("PropertyName", out var propertyName) ||
                !diagnostic.Properties.TryGetValue("PropertyType", out var propertyType))
            {
                continue;
            }

            var node = root.FindNode(diagnostic.Location.SourceSpan);
            if (node is not InvocationExpressionSyntax invocation)
            {
                continue;
            }

            // Fix 1: Add ForMember mapping with default value
            var defaultValueAction = CodeAction.Create(
                title: $"Map '{propertyName}' to default value",
                createChangedDocument: cancellationToken =>
                {
                    var newRoot = AddForMemberWithDefaultValue(root, invocation, propertyName, propertyType);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"DefaultValue_{propertyName}");

            context.RegisterCodeFix(defaultValueAction, context.Diagnostics);

            // Fix 2: Add ForMember mapping with constant value
            var constantValueAction = CodeAction.Create(
                title: $"Map '{propertyName}' to constant value",
                createChangedDocument: cancellationToken =>
                {
                    var newRoot = AddForMemberWithConstantValue(root, invocation, propertyName, propertyType);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"ConstantValue_{propertyName}");

            context.RegisterCodeFix(constantValueAction, context.Diagnostics);

            // Fix 3: Add ForMember mapping with custom logic placeholder
            var customLogicAction = CodeAction.Create(
                title: $"Map '{propertyName}' with custom logic (requires implementation)",
                createChangedDocument: cancellationToken =>
                {
                    var newRoot = AddForMemberWithCustomLogic(root, invocation, propertyName, propertyType);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"CustomLogic_{propertyName}");

            context.RegisterCodeFix(customLogicAction, context.Diagnostics);

            // Fix 4: Add comment suggesting to add property to source class
            var addPropertyAction = CodeAction.Create(
                title: $"Add comment to suggest adding '{propertyName}' to source class",
                createChangedDocument: cancellationToken =>
                {
                    var newRoot = AddSourcePropertySuggestionComment(root, invocation, propertyName, propertyType);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"AddProperty_{propertyName}");

            context.RegisterCodeFix(addPropertyAction, context.Diagnostics);
        }
    }

    private SyntaxNode AddForMemberWithDefaultValue(SyntaxNode root, InvocationExpressionSyntax invocation, 
        string propertyName, string propertyType)
    {
        string defaultValue = GetDefaultValueForType(propertyType);
        
        var forMemberCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                invocation,
                SyntaxFactory.IdentifierName("ForMember")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("dest")),
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("dest"),
                                    SyntaxFactory.IdentifierName(propertyName)))),
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("opt")),
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName("opt"),
                                        SyntaxFactory.IdentifierName("MapFrom")))
                                    .WithArgumentList(
                                        SyntaxFactory.ArgumentList(
                                            SyntaxFactory.SingletonSeparatedList(
                                                SyntaxFactory.Argument(
                                                    SyntaxFactory.SimpleLambdaExpression(
                                                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("src")),
                                                        SyntaxFactory.ParseExpression(defaultValue))))))))
                    })));

        return root.ReplaceNode(invocation, forMemberCall);
    }

    private SyntaxNode AddForMemberWithConstantValue(SyntaxNode root, InvocationExpressionSyntax invocation, 
        string propertyName, string propertyType)
    {
        string constantValue = GetSampleValueForType(propertyType);
        
        var forMemberCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                invocation,
                SyntaxFactory.IdentifierName("ForMember")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("dest")),
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("dest"),
                                    SyntaxFactory.IdentifierName(propertyName)))),
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("opt")),
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName("opt"),
                                        SyntaxFactory.IdentifierName("MapFrom")))
                                    .WithArgumentList(
                                        SyntaxFactory.ArgumentList(
                                            SyntaxFactory.SingletonSeparatedList(
                                                SyntaxFactory.Argument(
                                                    SyntaxFactory.SimpleLambdaExpression(
                                                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("src")),
                                                        SyntaxFactory.ParseExpression(constantValue))))))))
                    })));

        return root.ReplaceNode(invocation, forMemberCall);
    }

    private SyntaxNode AddForMemberWithCustomLogic(SyntaxNode root, InvocationExpressionSyntax invocation, 
        string propertyName, string propertyType)
    {
        var forMemberCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                invocation,
                SyntaxFactory.IdentifierName("ForMember")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("dest")),
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("dest"),
                                    SyntaxFactory.IdentifierName(propertyName)))),
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("opt")),
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName("opt"),
                                        SyntaxFactory.IdentifierName("MapFrom")))
                                    .WithArgumentList(
                                        SyntaxFactory.ArgumentList(
                                            SyntaxFactory.SingletonSeparatedList(
                                                SyntaxFactory.Argument(
                                                    SyntaxFactory.SimpleLambdaExpression(
                                                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("src")),
                                                        SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))))))))
                    })))
            .WithLeadingTrivia(
                SyntaxFactory.Comment($"// TODO: Implement custom mapping logic for required property '{propertyName}'"));

        return root.ReplaceNode(invocation, forMemberCall);
    }

    private SyntaxNode AddSourcePropertySuggestionComment(SyntaxNode root, InvocationExpressionSyntax invocation, 
        string propertyName, string propertyType)
    {
        var commentTrivia = SyntaxFactory.Comment($"// TODO: Consider adding '{propertyName}' property of type '{propertyType}' to source class");
        var secondCommentTrivia = SyntaxFactory.Comment($"// This will ensure the required property is automatically mapped");
        
        var newInvocation = invocation.WithLeadingTrivia(
            invocation.GetLeadingTrivia()
                .Add(commentTrivia)
                .Add(SyntaxFactory.EndOfLine("\n"))
                .Add(secondCommentTrivia)
                .Add(SyntaxFactory.EndOfLine("\n")));

        return root.ReplaceNode(invocation, newInvocation);
    }

    private string GetDefaultValueForType(string propertyType)
    {
        return propertyType.ToLower() switch
        {
            "string" => "string.Empty",
            "int" => "0",
            "long" => "0L",
            "double" => "0.0",
            "float" => "0.0f",
            "decimal" => "0m",
            "bool" => "false",
            "datetime" => "DateTime.MinValue",
            "guid" => "Guid.Empty",
            _ => "default"
        };
    }

    private string GetSampleValueForType(string propertyType)
    {
        return propertyType.ToLower() switch
        {
            "string" => "\"DefaultValue\"",
            "int" => "1",
            "long" => "1L",
            "double" => "1.0",
            "float" => "1.0f",
            "decimal" => "1.0m",
            "bool" => "true",
            "datetime" => "DateTime.Now",
            "guid" => "Guid.NewGuid()",
            _ => "new " + propertyType + "()"
        };
    }
}
using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
/// Code fix provider for AM030 diagnostic - Custom Type Converter issues.
/// Provides fixes for missing ConvertUsing configurations and invalid converter implementations.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM030_CustomTypeConverterCodeFixProvider)), Shared]
public class AM030_CustomTypeConverterCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM030");

    /// <summary>
    /// Gets the fix all provider for batch fixes.
    /// </summary>
    /// <returns>The batch fixer provider.</returns>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>
    /// Registers code fixes for the specified context.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue("PropertyName", out var propertyName) || 
                string.IsNullOrEmpty(propertyName))
            {
                continue;
            }

            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var node = root.FindNode(diagnosticSpan);

            if (node.FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation)
                continue;

            var diagnosticId = diagnostic.Id;
            
            switch (diagnosticId)
            {
                case "AM030" when diagnostic.Descriptor.Title.ToString().Contains("Missing ConvertUsing"):
                    RegisterMissingConvertUsingFixes(context, root, invocation, propertyName!, diagnostic);
                    break;
                    
                case "AM030" when diagnostic.Descriptor.Title.ToString().Contains("Invalid type converter"):
                    RegisterInvalidConverterFixes(context, root, node, diagnostic);
                    break;
                    
                case "AM030" when diagnostic.Descriptor.Title.ToString().Contains("null values"):
                    RegisterNullHandlingFixes(context, root, node, diagnostic);
                    break;
            }
        }
    }

    private void RegisterMissingConvertUsingFixes(CodeFixContext context, SyntaxNode root, 
        InvocationExpressionSyntax invocation, string propertyName, Diagnostic diagnostic)
    {
        // Fix 1: Add ConvertUsing with lambda for string to primitive conversions
        if (diagnostic.Properties.TryGetValue("ConverterType", out var converterType) && 
            converterType!.Contains("String") && IsStringToPrimitiveConversion(converterType))
        {
            var lambdaFix = CodeAction.Create(
                title: $"Add ConvertUsing with lambda for '{propertyName}'",
                createChangedDocument: cancellationToken =>
                {
                    var newRoot = AddConvertUsingLambda(root, invocation, propertyName, converterType!);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"ConvertUsingLambda_{propertyName}");

            context.RegisterCodeFix(lambdaFix, diagnostic);
        }

        // Fix 2: Add ConvertUsing with custom converter class
        var converterClassFix = CodeAction.Create(
            title: $"Add ConvertUsing with custom converter for '{propertyName}'",
            createChangedDocument: cancellationToken =>
            {
                var newRoot = AddConvertUsingWithConverter(root, invocation, propertyName,
                    diagnostic.Properties.GetValueOrDefault("ConverterType", "CustomConverter") ?? "CustomConverter");
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            equivalenceKey: $"ConvertUsingConverter_{propertyName}");

        context.RegisterCodeFix(converterClassFix, diagnostic);

        // Fix 3: Add ForMember with ConvertUsing configuration
        var forMemberFix = CodeAction.Create(
            title: $"Add ForMember with ConvertUsing for '{propertyName}'",
            createChangedDocument: cancellationToken =>
            {
                var newRoot = AddForMemberConvertUsing(root, invocation, propertyName,
                    diagnostic.Properties.GetValueOrDefault("ConverterType", "CustomConverter") ?? "CustomConverter");
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            equivalenceKey: $"ForMemberConvertUsing_{propertyName}");

        context.RegisterCodeFix(forMemberFix, diagnostic);
    }

    private void RegisterInvalidConverterFixes(CodeFixContext context, SyntaxNode root, 
        SyntaxNode node, Diagnostic diagnostic)
    {
        if (node.FirstAncestorOrSelf<ClassDeclarationSyntax>() is not { } classDeclaration)
            return;

        // Fix 1: Add missing Convert method
        var addConvertMethodFix = CodeAction.Create(
            title: "Add missing Convert method",
            createChangedDocument: cancellationToken =>
            {
                var newRoot = AddConvertMethod(root, classDeclaration);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            equivalenceKey: "AddConvertMethod");

        context.RegisterCodeFix(addConvertMethodFix, diagnostic);
    }

    private void RegisterNullHandlingFixes(CodeFixContext context, SyntaxNode root, 
        SyntaxNode node, Diagnostic diagnostic)
    {
        if (node.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } methodDeclaration)
            return;

        // Fix 1: Add null check to Convert method
        var addNullCheckFix = CodeAction.Create(
            title: "Add null check to Convert method",
            createChangedDocument: cancellationToken =>
            {
                var newRoot = AddNullCheckToConvertMethod(root, methodDeclaration);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            equivalenceKey: "AddNullCheck");

        context.RegisterCodeFix(addNullCheckFix, diagnostic);
    }

    private SyntaxNode AddConvertUsingLambda(SyntaxNode root, InvocationExpressionSyntax invocation, 
        string propertyName, string converterType)
    {
        var lambdaExpression = GetConversionLambda(propertyName, converterType);
        
        var forMemberCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                invocation,
                SyntaxFactory.IdentifierName("ForMember")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList<ArgumentSyntax>(new[]
                    {
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("dest")))
                            .WithExpressionBody(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("dest"),
                                    SyntaxFactory.IdentifierName(propertyName)))),
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("opt")))
                            .WithExpressionBody(
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName("opt"),
                                        SyntaxFactory.IdentifierName("ConvertUsing")))
                                .WithArgumentList(
                                    SyntaxFactory.ArgumentList(
                                        SyntaxFactory.SingletonSeparatedList(
                                            SyntaxFactory.Argument(lambdaExpression))))))
                    })));

        return root.ReplaceNode(invocation, forMemberCall);
    }

    private SyntaxNode AddConvertUsingWithConverter(SyntaxNode root, InvocationExpressionSyntax invocation, 
        string propertyName, string converterType)
    {
        var converterName = GetConverterClassName(propertyName, converterType);
        
        var convertUsingCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                invocation,
                SyntaxFactory.GenericName("ConvertUsing")
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                SyntaxFactory.IdentifierName(converterName))))));

        return root.ReplaceNode(invocation, convertUsingCall);
    }

    private SyntaxNode AddForMemberConvertUsing(SyntaxNode root, InvocationExpressionSyntax invocation, 
        string propertyName, string converterType)
    {
        var converterName = GetConverterClassName(propertyName, converterType);
        
        var forMemberCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                invocation,
                SyntaxFactory.IdentifierName("ForMember")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList<ArgumentSyntax>(new[]
                    {
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("dest")))
                            .WithExpressionBody(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("dest"),
                                    SyntaxFactory.IdentifierName(propertyName)))),
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("opt")))
                            .WithExpressionBody(
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName("opt"),
                                        SyntaxFactory.GenericName("ConvertUsing")
                                            .WithTypeArgumentList(
                                                SyntaxFactory.TypeArgumentList(
                                                    SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                                        SyntaxFactory.IdentifierName(converterName))))))))
                    })));

        return root.ReplaceNode(invocation, forMemberCall);
    }

    private SyntaxNode AddConvertMethod(SyntaxNode root, ClassDeclarationSyntax classDeclaration)
    {
        var convertMethod = SyntaxFactory.MethodDeclaration(
            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword)),
            "Convert")
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(
                SyntaxFactory.ParameterList(
                    SyntaxFactory.SeparatedList<ParameterSyntax>(new[]
                    {
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("source"))
                            .WithType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword))),
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("destination"))
                            .WithType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword))),
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("context"))
                            .WithType(SyntaxFactory.IdentifierName("ResolutionContext"))
                    })))
            .WithBody(
                SyntaxFactory.Block(
                    SyntaxFactory.SingletonList<StatementSyntax>(
                        SyntaxFactory.ThrowStatement(
                            SyntaxFactory.ObjectCreationExpression(
                                SyntaxFactory.IdentifierName("NotImplementedException"))
                            .WithArgumentList(
                                SyntaxFactory.ArgumentList(
                                    SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.LiteralExpression(
                                                SyntaxKind.StringLiteralExpression,
                                                SyntaxFactory.Literal("TODO: Implement conversion logic"))))))))));

        var newClassDeclaration = classDeclaration.AddMembers(convertMethod);
        return root.ReplaceNode(classDeclaration, newClassDeclaration);
    }

    private SyntaxNode AddNullCheckToConvertMethod(SyntaxNode root, MethodDeclarationSyntax methodDeclaration)
    {
        var nullCheck = SyntaxFactory.IfStatement(
            SyntaxFactory.BinaryExpression(
                SyntaxKind.EqualsExpression,
                SyntaxFactory.IdentifierName("source"),
                SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)),
            SyntaxFactory.ReturnStatement(
                SyntaxFactory.IdentifierName("destination")));

        if (methodDeclaration.Body != null)
        {
            var newBody = methodDeclaration.Body.WithStatements(
                methodDeclaration.Body.Statements.Insert(0, nullCheck));
            var newMethod = methodDeclaration.WithBody(newBody);
            return root.ReplaceNode(methodDeclaration, newMethod);
        }

        return root;
    }

    private ExpressionSyntax GetConversionLambda(string propertyName, string converterType)
    {
        if (converterType.Contains("String") && converterType.Contains("DateTime"))
        {
            return SyntaxFactory.SimpleLambdaExpression(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("src")))
                .WithExpressionBody(
                    SyntaxFactory.ConditionalExpression(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                                SyntaxFactory.IdentifierName("IsNullOrEmpty")))
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Argument(
                                        SyntaxFactory.MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            SyntaxFactory.IdentifierName("src"),
                                            SyntaxFactory.IdentifierName(propertyName)))))),
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("DateTime"),
                            SyntaxFactory.IdentifierName("MinValue")),
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("DateTime"),
                                SyntaxFactory.IdentifierName("Parse")))
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Argument(
                                        SyntaxFactory.MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            SyntaxFactory.IdentifierName("src"),
                                            SyntaxFactory.IdentifierName(propertyName))))))));
        }

        // Default lambda for other conversions
        return SyntaxFactory.SimpleLambdaExpression(
            SyntaxFactory.Parameter(SyntaxFactory.Identifier("src")))
            .WithExpressionBody(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("src"),
                    SyntaxFactory.IdentifierName(propertyName)));
    }

    private bool IsStringToPrimitiveConversion(string converterType)
    {
        return converterType.Contains("String") && 
               (converterType.Contains("DateTime") || 
                converterType.Contains("Int") || 
                converterType.Contains("Decimal") || 
                converterType.Contains("Double") || 
                converterType.Contains("Float"));
    }

    private string GetConverterClassName(string propertyName, string converterType)
    {
        var parts = converterType.Replace("ITypeConverter<", "").Replace(">", "").Split(',');
        if (parts.Length == 2)
        {
            var sourceType = parts[0].Trim().Split('.').Last();
            var destType = parts[1].Trim().Split('.').Last();
            return $"{sourceType}To{destType}Converter";
        }
        
        return $"{propertyName}Converter";
    }
}
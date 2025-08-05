using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.CodeFixes;

/// <summary>
/// Code fix provider for AM003 diagnostic - Collection Type Incompatibility.
/// Provides fixes for collection type mismatches and element type conversions.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM003_CollectionTypeIncompatibilityCodeFixProvider)), Shared]
public class AM003_CollectionTypeIncompatibilityCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ["AM003"];

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
        if (root == null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue("PropertyName", out var propertyName) ||
                !diagnostic.Properties.TryGetValue("SourceType", out var sourceType) ||
                !diagnostic.Properties.TryGetValue("DestType", out var destType) ||
                !diagnostic.Properties.TryGetValue("SourceElementType", out var sourceElementType) ||
                !diagnostic.Properties.TryGetValue("DestElementType", out var destElementType) ||
                string.IsNullOrEmpty(propertyName) || string.IsNullOrEmpty(sourceType) ||
                string.IsNullOrEmpty(destType) || string.IsNullOrEmpty(sourceElementType) ||
                string.IsNullOrEmpty(destElementType))
            {
                continue;
            }

            var node = root.FindNode(diagnostic.Location.SourceSpan);
            if (node is not InvocationExpressionSyntax invocation)
            {
                continue;
            }

            // Check if this is a collection type incompatibility vs element type incompatibility
            bool isCollectionTypeIncompatibility = !string.IsNullOrEmpty(sourceType) && 
                (sourceType!.Contains("HashSet") || sourceType.Contains("Queue") || sourceType.Contains("Stack"));

            if (isCollectionTypeIncompatibility)
            {
                // Collection type incompatibility fixes
                RegisterCollectionTypeIncompatibilityFixes(context, root, invocation, propertyName!, sourceType!, destType!, sourceElementType!, destElementType!);
            }
            else
            {
                // Element type incompatibility fixes
                RegisterElementTypeIncompatibilityFixes(context, root, invocation, propertyName!, sourceType!, destType!, sourceElementType!, destElementType!);
            }
        }
    }

    private void RegisterCollectionTypeIncompatibilityFixes(CodeFixContext context, SyntaxNode root, 
        InvocationExpressionSyntax invocation, string propertyName, string sourceType, string destType, 
        string sourceElementType, string destElementType)
    {
        // Fix 1: Add ForMember with ToList() conversion
        if (sourceType.Contains("HashSet") && destType.Contains("List"))
        {
            var toListAction = CodeAction.Create(
                title: $"Convert {propertyName} using ToList()",
                createChangedDocument: cancellationToken =>
                {
                    var newRoot = AddForMemberWithConversion(root, invocation, propertyName, "src.{0}.ToList()");
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"ToList_{propertyName}");

            context.RegisterCodeFix(toListAction, context.Diagnostics);
        }

        // Fix 2: Add ForMember with ToArray() conversion
        if ((sourceType.Contains("Queue") || sourceType.Contains("Stack")) && destType.Contains("List"))
        {
            var toArrayAction = CodeAction.Create(
                title: $"Convert {propertyName} using ToArray()",
                createChangedDocument: cancellationToken =>
                {
                    var newRoot = AddForMemberWithConversion(root, invocation, propertyName, "src.{0}.ToArray()");
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"ToArray_{propertyName}");

            context.RegisterCodeFix(toArrayAction, context.Diagnostics);
        }

        // Fix 3: Add ForMember with specific collection constructor
        var constructorAction = CodeAction.Create(
            title: $"Convert {propertyName} using collection constructor",
            createChangedDocument: cancellationToken =>
            {
                string collectionType = GetCollectionTypeName(destType);
                var newRoot = AddForMemberWithConversion(root, invocation, propertyName, $"new {collectionType}(src.{0})");
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            equivalenceKey: $"Constructor_{propertyName}");

        context.RegisterCodeFix(constructorAction, context.Diagnostics);
    }

    private void RegisterElementTypeIncompatibilityFixes(CodeFixContext context, SyntaxNode root, 
        InvocationExpressionSyntax invocation, string propertyName, string sourceType, string destType, 
        string sourceElementType, string destElementType)
    {
        // Fix 1: Add ForMember with Select conversion
        var selectAction = CodeAction.Create(
            title: $"Convert {propertyName} elements using Select",
            createChangedDocument: cancellationToken =>
            {
                string conversion = GetElementConversion(sourceElementType, destElementType);
                var newRoot = AddForMemberWithConversion(root, invocation, propertyName, $"src.{0}.Select({conversion})");
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            equivalenceKey: $"Select_{propertyName}");

        context.RegisterCodeFix(selectAction, context.Diagnostics);

        // Fix 2: Add CreateMap for complex element types (if both are complex types)
        if (!IsPrimitiveType(sourceElementType) && !IsPrimitiveType(destElementType))
        {
            var createMapAction = CodeAction.Create(
                title: $"Add CreateMap for element types {GetSimpleTypeName(sourceElementType)} -> {GetSimpleTypeName(destElementType)}",
                createChangedDocument: cancellationToken =>
                {
                    var newRoot = AddCreateMapForElementTypes(root, invocation, sourceElementType, destElementType);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"CreateMap_{sourceElementType}_{destElementType}");

            context.RegisterCodeFix(createMapAction, context.Diagnostics);
        }
    }

    private SyntaxNode AddForMemberWithConversion(SyntaxNode root, InvocationExpressionSyntax invocation, 
        string propertyName, string conversionTemplate)
    {
        var conversion = string.Format(conversionTemplate, propertyName);
        
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
                                                        SyntaxFactory.ParseExpression(conversion))))))))
                    })));

        return root.ReplaceNode(invocation, forMemberCall);
    }

    private SyntaxNode AddCreateMapForElementTypes(SyntaxNode root, InvocationExpressionSyntax invocation, 
        string sourceElementType, string destElementType)
    {
        var createMapCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                invocation,
                SyntaxFactory.GenericName(SyntaxFactory.Identifier("CreateMap"))
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SeparatedList(new TypeSyntax[]
                            {
                                SyntaxFactory.ParseTypeName(GetSimpleTypeName(sourceElementType)),
                                SyntaxFactory.ParseTypeName(GetSimpleTypeName(destElementType))
                            })))));

        return root.ReplaceNode(invocation, createMapCall);
    }

    private string GetCollectionTypeName(string fullTypeName)
    {
        if (fullTypeName.Contains("List<"))
            return "List<" + ExtractElementType(fullTypeName) + ">";
        if (fullTypeName.Contains("HashSet<"))
            return "HashSet<" + ExtractElementType(fullTypeName) + ">";
        if (fullTypeName.Contains("Queue<"))
            return "Queue<" + ExtractElementType(fullTypeName) + ">";
        if (fullTypeName.Contains("Stack<"))
            return "Stack<" + ExtractElementType(fullTypeName) + ">";
        
        return fullTypeName;
    }

    private string ExtractElementType(string collectionType)
    {
        int start = collectionType.IndexOf('<') + 1;
        int end = collectionType.LastIndexOf('>');
        return collectionType.Substring(start, end - start);
    }

    private string GetElementConversion(string sourceElementType, string destElementType)
    {
        // Handle common conversions
        if (sourceElementType == "string" && destElementType == "int")
            return "x => int.Parse(x)";
        if (sourceElementType == "int" && destElementType == "string")
            return "x => x.ToString()";
        if (sourceElementType == "string" && destElementType == "double")
            return "x => double.Parse(x)";
        if (sourceElementType == "double" && destElementType == "string")
            return "x => x.ToString()";
        if (sourceElementType == "string" && destElementType == "decimal")
            return "x => decimal.Parse(x)";
        if (sourceElementType == "decimal" && destElementType == "string")
            return "x => x.ToString()";

        // For complex types, assume mapping exists or will be created
        return "x => x";
    }

    private bool IsPrimitiveType(string typeName)
    {
        string[] primitiveTypes = { "string", "int", "double", "float", "decimal", "bool", "char", "byte", "sbyte", "short", "ushort", "uint", "long", "ulong" };
        return primitiveTypes.Contains(typeName.ToLower());
    }

    private string GetSimpleTypeName(string fullTypeName)
    {
        if (fullTypeName.Contains('.'))
        {
            return fullTypeName.Substring(fullTypeName.LastIndexOf('.') + 1);
        }
        return fullTypeName;
    }
}
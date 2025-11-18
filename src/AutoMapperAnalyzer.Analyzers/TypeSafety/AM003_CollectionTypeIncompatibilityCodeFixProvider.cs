using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.TypeSafety;

/// <summary>
///     Code fix provider for AM003 diagnostic - Collection Type Incompatibility.
///     Provides fixes for collection type mismatches and element type conversions.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM003_CollectionTypeIncompatibilityCodeFixProvider))]
[Shared]
public class AM003_CollectionTypeIncompatibilityCodeFixProvider : CodeFixProvider
{
    /// <summary>
    ///     Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ["AM003"];

    /// <summary>
    ///     Gets the fix all provider for batch fixing multiple diagnostics.
    /// </summary>
    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <summary>
    ///     Registers code fixes for AM003 diagnostics.
    /// </summary>
    /// <param name="context">The code fix context containing diagnostic information.</param>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (Diagnostic? diagnostic in context.Diagnostics)
        {
            SyntaxNode? root =
                await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                continue;
            }

            if (!diagnostic.Properties.TryGetValue("PropertyName", out string? propertyName) ||
                !diagnostic.Properties.TryGetValue("SourceType", out string? sourceType) ||
                !diagnostic.Properties.TryGetValue("DestType", out string? destType) ||
                !diagnostic.Properties.TryGetValue("SourceElementType", out string? sourceElementType) ||
                !diagnostic.Properties.TryGetValue("DestElementType", out string? destElementType) ||
                string.IsNullOrWhiteSpace(propertyName) ||
                string.IsNullOrWhiteSpace(sourceType) ||
                string.IsNullOrWhiteSpace(destType) ||
                string.IsNullOrWhiteSpace(sourceElementType) ||
                string.IsNullOrWhiteSpace(destElementType))
            {
                continue;
            }

            if (root.FindNode(diagnostic.Location.SourceSpan) is not InvocationExpressionSyntax invocation)
            {
                continue;
            }

            // After the null checks above, we know these are not null
            string safePropertyName = propertyName!;
            string safeSourceType = sourceType!;
            string safeDestType = destType!;
            string safeSourceElementType = sourceElementType!;
            string safeDestElementType = destElementType!;

            if (diagnostic.Descriptor == AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule)
            {
                foreach ((string Title, string Expression, bool RequiresLinq, string EquivalenceKey) fix in
                         CreateCollectionFixes(safePropertyName, safeSourceType, safeDestType, safeSourceElementType,
                             safeDestElementType))
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            fix.Title,
                            ct => AddMapFromAsync(context.Document, invocation, safePropertyName, fix.Expression,
                                fix.RequiresLinq, ct),
                            fix.EquivalenceKey),
                        diagnostic);
                }
            }
            else
            {
                string conversionLambda = GetElementConversion(safeSourceElementType, safeDestElementType);
                if (!string.IsNullOrEmpty(conversionLambda))
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            $"Convert {safePropertyName} elements using Select()",
                            ct => AddMapFromAsync(
                                context.Document,
                                invocation,
                                safePropertyName,
                                $"src.{safePropertyName}.Select({conversionLambda})",
                                true,
                                ct),
                            $"Select_{safePropertyName}"),
                        diagnostic);
                }

                context.RegisterCodeFix(
                    CodeAction.Create(
                        $"Ignore property '{safePropertyName}'",
                        ct => AddIgnoreAsync(context.Document, invocation, safePropertyName, ct),
                        $"Ignore_{safePropertyName}"),
                    diagnostic);
            }
        }
    }

    private static async Task<Document> AddMapFromAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string propertyName,
        string mapFromExpression,
        bool requiresLinq,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return document;
        }

        InvocationExpressionSyntax newInvocation =
            CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName, mapFromExpression);
        SyntaxNode newRoot = compilationUnit.ReplaceNode(invocation, newInvocation);

        if (requiresLinq && newRoot is CompilationUnitSyntax updatedCompilationUnit)
        {
            newRoot = AddUsingIfMissing(updatedCompilationUnit, "System.Linq");
        }

        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> AddIgnoreAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string propertyName,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        InvocationExpressionSyntax newInvocation =
            CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
        SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }

    private static IEnumerable<(string Title, string Expression, bool RequiresLinq, string EquivalenceKey)>
        CreateCollectionFixes(
            string propertyName,
            string sourceType,
            string destType,
            string sourceElementType,
            string destElementType)
    {
        var fixes = new List<(string Title, string Expression, bool RequiresLinq, string EquivalenceKey)>();
        string simplifiedDestType = SimplifyCollectionType(destType);

        // Determine if element conversion is needed
        string elementConversionLambda = GetElementConversion(sourceElementType, destElementType);
        bool needsElementConversion =
            !string.IsNullOrEmpty(elementConversionLambda) && elementConversionLambda != "x => x";

        // Helper to build expression with optional element conversion
        string BuildExpression(string collectionConversion, bool isConstructor = false)
        {
            if (needsElementConversion)
            {
                if (isConstructor)
                {
                    // new List<T>(src.Prop.Select(x => ...))
                    return collectionConversion.Replace($"src.{propertyName}",
                        $"src.{propertyName}.Select({elementConversionLambda})");
                }

                // src.Prop.Select(x => ...).ToList()
                return $"src.{propertyName}.Select({elementConversionLambda}).{collectionConversion.Split('.').Last()}";
            }

            return collectionConversion;
        }

        if (Contains(sourceType, "HashSet") && Contains(destType, "List"))
        {
            fixes.Add((
                $"Convert {propertyName} using ToList()",
                BuildExpression($"src.{propertyName}.ToList()"),
                true, // Always true if using ToList or Select
                $"ToList_{propertyName}"));
        }

        if (Contains(sourceType, "Queue") && Contains(destType, "List"))
        {
            (string title, string expr, _, string key) = CreateConstructorFix(propertyName, simplifiedDestType);
            fixes.Add((title, BuildExpression(expr, true), needsElementConversion, key));
        }

        if (Contains(sourceType, "Stack") && Contains(destType, "List"))
        {
            (string title, string expr, _, string key) = CreateConstructorFix(propertyName, simplifiedDestType);
            fixes.Add((title, BuildExpression(expr, true), needsElementConversion, key));
        }

        if (Contains(sourceType, "List") && Contains(destType, "Queue"))
        {
            (string title, string expr, _, string key) = CreateConstructorFix(propertyName, simplifiedDestType);
            fixes.Add((title, BuildExpression(expr, true), needsElementConversion, key));
        }

        if (Contains(sourceType, "IEnumerable") && (Contains(destType, "Stack") || Contains(destType, "HashSet")))
        {
            (string title, string expr, _, string key) = CreateConstructorFix(propertyName, simplifiedDestType);
            fixes.Add((title, BuildExpression(expr, true), needsElementConversion, key));
        }

        if (IsArrayType(sourceType) && Contains(destType, "IEnumerable"))
        {
            // Arrays are IEnumerable, so AsEnumerable is fine, but if we need element conversion, we need Select
            string expr = needsElementConversion
                ? $"src.{propertyName}.Select({elementConversionLambda})"
                : $"src.{propertyName}.AsEnumerable()";

            fixes.Add((
                $"Convert {propertyName} using {(needsElementConversion ? "Select" : "AsEnumerable")}()",
                expr,
                true,
                $"AsEnumerable_{propertyName}"));
        }

        if (!fixes.Any())
        {
            (string title, string expr, _, string key) = CreateConstructorFix(propertyName, simplifiedDestType);
            fixes.Add((title, BuildExpression(expr, true), needsElementConversion, key));
        }

        return fixes;

        static (string Title, string Expression, bool RequiresLinq, string EquivalenceKey) CreateConstructorFix(
            string propertyName, string simplifiedDestType)
        {
            string targetType = string.IsNullOrWhiteSpace(simplifiedDestType)
                ? "System.Collections.Generic.List<object>"
                : simplifiedDestType;
            return ($"Convert {propertyName} using collection constructor", $"new {targetType}(src.{propertyName})",
                false, $"Constructor_{propertyName}");
        }
    }

    private static string GetElementConversion(string sourceElementType, string destElementType)
    {
        string source = TypeConversionHelper.NormalizeTypeName(sourceElementType);
        string destination = TypeConversionHelper.NormalizeTypeName(destElementType);

        if (string.Equals(source, destination, StringComparison.Ordinal))
        {
            return "x => x";
        }

        return (source, destination) switch
        {
            ("string", "int") or ("string", "int32") => "x => int.Parse(x)",
            ("string", "long") or ("string", "int64") => "x => long.Parse(x)",
            ("string", "double") => "x => double.Parse(x)",
            ("string", "decimal") => "x => decimal.Parse(x)",
            ("string", "bool") => "x => bool.Parse(x)",
            ("string", "datetime") => "x => global::System.DateTime.Parse(x)",
            ("string", "guid") => "x => global::System.Guid.Parse(x)",
            ("object", "string") => "x => x != null ? x.ToString() : string.Empty",
            (_, "string") => "x => x != null ? x.ToString() : string.Empty",
            ("double", "int") or ("double", "int32") => "x => global::System.Convert.ToInt32(x)",
            ("double", "long") or ("double", "int64") => "x => global::System.Convert.ToInt64(x)",
            ("float", "int") or ("single", "int") => "x => global::System.Convert.ToInt32(x)",
            _ => $"x => ({destElementType})x"
        };
    }

    private static bool Contains(string typeName, string value)
    {
        return typeName?.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsArrayType(string typeName)
    {
        return typeName?.EndsWith("[]", StringComparison.Ordinal) == true;
    }

    private static string SimplifyCollectionType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return typeName;
        }

        const string collectionsPrefix = "System.Collections.Generic.";
        const string systemPrefix = "System.";

        if (typeName.StartsWith(collectionsPrefix, StringComparison.Ordinal))
        {
            return typeName.Substring(collectionsPrefix.Length);
        }

        if (typeName.StartsWith(systemPrefix, StringComparison.Ordinal))
        {
            return typeName.Substring(systemPrefix.Length);
        }

        return typeName;
    }

    private static CompilationUnitSyntax AddUsingIfMissing(CompilationUnitSyntax root, string namespaceName)
    {
        if (root.Usings.Any(u =>
                u.Name != null && string.Equals(u.Name.ToString(), namespaceName, StringComparison.Ordinal)))
        {
            return root;
        }

        UsingDirectiveSyntax usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName))
            .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed);

        return root.AddUsings(usingDirective);
    }
}

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers.TypeSafety;

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
    /// Gets the fix all provider for batch fixing multiple diagnostics.
    /// </summary>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>
    /// Registers code fixes for AM003 diagnostics.
    /// </summary>
    /// <param name="context">The code fix context containing diagnostic information.</param>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                continue;
            }

            if (!diagnostic.Properties.TryGetValue("PropertyName", out var propertyName) ||
                !diagnostic.Properties.TryGetValue("SourceType", out var sourceType) ||
                !diagnostic.Properties.TryGetValue("DestType", out var destType) ||
                !diagnostic.Properties.TryGetValue("SourceElementType", out var sourceElementType) ||
                !diagnostic.Properties.TryGetValue("DestElementType", out var destElementType) ||
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
            var safePropertyName = propertyName!;
            var safeSourceType = sourceType!;
            var safeDestType = destType!;
            var safeSourceElementType = sourceElementType!;
            var safeDestElementType = destElementType!;

            if (diagnostic.Descriptor == AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule)
            {
                foreach (var fix in CreateCollectionFixes(safePropertyName, safeSourceType, safeDestType))
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: fix.Title,
                            createChangedDocument: ct => AddMapFromAsync(context.Document, invocation, safePropertyName, fix.Expression, fix.RequiresLinq, ct),
                            equivalenceKey: fix.EquivalenceKey),
                        diagnostic);
                }
            }
            else
            {
                var conversionLambda = GetElementConversion(safeSourceElementType, safeDestElementType);
                if (!string.IsNullOrEmpty(conversionLambda))
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: $"Convert {safePropertyName} elements using Select()",
                            createChangedDocument: ct => AddMapFromAsync(
                                context.Document,
                                invocation,
                                safePropertyName,
                                $"src.{safePropertyName}.Select({conversionLambda})",
                                requiresLinq: true,
                                ct),
                            equivalenceKey: $"Select_{safePropertyName}"),
                        diagnostic);
                }

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Ignore property '{safePropertyName}'",
                        createChangedDocument: ct => AddIgnoreAsync(context.Document, invocation, safePropertyName, ct),
                        equivalenceKey: $"Ignore_{safePropertyName}"),
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
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return document;
        }

        var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName, mapFromExpression);
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
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
        var newRoot = root.ReplaceNode(invocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }

    private static IEnumerable<(string Title, string Expression, bool RequiresLinq, string EquivalenceKey)> CreateCollectionFixes(
        string propertyName,
        string sourceType,
        string destType)
    {
        var fixes = new List<(string Title, string Expression, bool RequiresLinq, string EquivalenceKey)>();
        var simplifiedDestType = SimplifyCollectionType(destType);

        if (Contains(sourceType, "HashSet") && Contains(destType, "List"))
        {
            fixes.Add((
                $"Convert {propertyName} using ToList()",
                $"src.{propertyName}.ToList()",
                true,
                $"ToList_{propertyName}"));
        }

        if (Contains(sourceType, "Queue") && Contains(destType, "List"))
        {
            fixes.Add(CreateConstructorFix(propertyName, simplifiedDestType));
        }

        if (Contains(sourceType, "Stack") && Contains(destType, "List"))
        {
            fixes.Add(CreateConstructorFix(propertyName, simplifiedDestType));
        }

        if (Contains(sourceType, "List") && Contains(destType, "Queue"))
        {
            fixes.Add(CreateConstructorFix(propertyName, simplifiedDestType));
        }

        if (Contains(sourceType, "IEnumerable") && (Contains(destType, "Stack") || Contains(destType, "HashSet")))
        {
            fixes.Add(CreateConstructorFix(propertyName, simplifiedDestType));
        }

        if (IsArrayType(sourceType) && Contains(destType, "IEnumerable"))
        {
            fixes.Add((
                $"Convert {propertyName} using AsEnumerable()",
                $"src.{propertyName}.AsEnumerable()",
                true,
                $"AsEnumerable_{propertyName}"));
        }

        if (!fixes.Any())
        {
            fixes.Add(CreateConstructorFix(propertyName, simplifiedDestType));
        }

        return fixes;

        static (string Title, string Expression, bool RequiresLinq, string EquivalenceKey) CreateConstructorFix(string propertyName, string simplifiedDestType)
        {
            var targetType = string.IsNullOrWhiteSpace(simplifiedDestType) ? "System.Collections.Generic.List<object>" : simplifiedDestType;
            return ($"Convert {propertyName} using collection constructor", $"new {targetType}(src.{propertyName})", false, $"Constructor_{propertyName}");
        }
    }

    private static string GetElementConversion(string sourceElementType, string destElementType)
    {
        var source = TypeConversionHelper.NormalizeTypeName(sourceElementType);
        var destination = TypeConversionHelper.NormalizeTypeName(destElementType);

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
        => typeName?.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool IsArrayType(string typeName)
        => typeName?.EndsWith("[]", StringComparison.Ordinal) == true;

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
        if (root.Usings.Any(u => u.Name != null && string.Equals(u.Name.ToString(), namespaceName, StringComparison.Ordinal)))
        {
            return root;
        }

        var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName))
            .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed);

        return root.AddUsings(usingDirective);
    }
}

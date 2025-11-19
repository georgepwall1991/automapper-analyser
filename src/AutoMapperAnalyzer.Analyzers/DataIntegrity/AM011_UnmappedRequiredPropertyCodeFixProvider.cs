using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.DataIntegrity;

/// <summary>
///     Code fix provider for AM011 diagnostic - Unmapped Required Property.
///     Provides fixes for required properties that are not mapped from source.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM011_UnmappedRequiredPropertyCodeFixProvider))]
[Shared]
public class AM011_UnmappedRequiredPropertyCodeFixProvider : CodeFixProvider
{
    /// <summary>
    ///     Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ["AM011"];

    /// <summary>
    ///     Gets the fix all provider for batch fixes.
    /// </summary>
    /// <returns>The batch fixer provider.</returns>
    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <summary>
    ///     Registers code fixes for the specified context.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return;
        }

        SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel == null)
        {
            return;
        }

        // Deduplicate invocations for bulk fixes
        var handledInvocations = new HashSet<InvocationExpressionSyntax>();

        foreach (Diagnostic? diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue("PropertyName", out string? propertyName) ||
                !diagnostic.Properties.TryGetValue("PropertyType", out string? propertyType) ||
                string.IsNullOrEmpty(propertyName) || string.IsNullOrEmpty(propertyType))
            {
                continue;
            }

            SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan);
            if (node is not InvocationExpressionSyntax invocation)
            {
                continue;
            }

            // 1. Register Bulk Fixes (only once per invocation)
            if (handledInvocations.Add(invocation))
            {
                RegisterBulkFixes(context, invocation, semanticModel, root);
            }

            // 2. Register Grouped Per-Property Fixes
            RegisterPerPropertyFixes(context, diagnostic, invocation, propertyName!, propertyType!, semanticModel, root);
        }
    }

    private void RegisterPerPropertyFixes(
        CodeFixContext context,
        Diagnostic diagnostic,
        InvocationExpressionSyntax invocation,
        string propertyName,
        string propertyType,
        SemanticModel semanticModel,
        SyntaxNode root)
    {
        var nestedActions = ImmutableArray.CreateBuilder<CodeAction>();

        // Fix 0: Fuzzy Matching - Find similar source properties
        (ITypeSymbol? sourceType, ITypeSymbol? destType) = AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType != null)
        {
            var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType);
            foreach (var sourceProp in sourceProperties)
            {
                int distance = ComputeLevenshteinDistance(propertyName, sourceProp.Name);
                // Threshold: match if distance is small (<= 2) and not too different in length
                if (distance <= 2 && Math.Abs(propertyName.Length - sourceProp.Name.Length) <= 2)
                {
                    var matchAction = CodeAction.Create(
                        $"Map from similar property '{sourceProp.Name}'",
                        cancellationToken =>
                        {
                            InvocationExpressionSyntax newInvocation =
                                CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName, $"src.{sourceProp.Name}");
                            SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                            return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                        },
                        $"FuzzyMatch_{propertyName}_{sourceProp.Name}");
                    nestedActions.Add(matchAction);
                }
            }
        }

        // Fix 1: Add ForMember mapping with default value
        var defaultValueAction = CodeAction.Create(
            "Map to default value",
            cancellationToken =>
            {
                string defaultValue = TypeConversionHelper.GetDefaultValueForType(propertyType);
                InvocationExpressionSyntax newInvocation =
                    CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName, defaultValue);
                SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            $"DefaultValue_{propertyName}");
        nestedActions.Add(defaultValueAction);

        // Fix 2: Add ForMember mapping with constant value
        var constantValueAction = CodeAction.Create(
            "Map to constant value",
            cancellationToken =>
            {
                string constantValue = TypeConversionHelper.GetSampleValueForType(propertyType);
                InvocationExpressionSyntax newInvocation =
                    CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName, constantValue);
                SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            $"ConstantValue_{propertyName}");
        nestedActions.Add(constantValueAction);

        // Fix 3: Add ForMember mapping with custom logic placeholder
        var customLogicAction = CodeAction.Create(
            "Map with custom logic (requires implementation)",
            cancellationToken =>
            {
                // Use default(T) as a safe placeholder that compiles for both reference and value types
                InvocationExpressionSyntax newInvocation = CodeFixSyntaxHelper
                    .CreateForMemberWithMapFrom(invocation, propertyName, $"default({propertyType})")
                    .WithLeadingTrivia(
                        invocation.GetLeadingTrivia()
                            .Add(SyntaxFactory.Comment(
                                $"// TODO: Implement custom mapping logic for required property '{propertyName}'"))
                            .Add(SyntaxFactory.ElasticCarriageReturnLineFeed));
                SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            $"CustomLogic_{propertyName}");
        nestedActions.Add(customLogicAction);

        // Fix 4: Add comment suggesting to add property to source class
        var addPropertyAction = CodeAction.Create(
            "Add comment to suggest adding property to source class",
            cancellationToken =>
            {
                SyntaxTrivia commentTrivia = SyntaxFactory.Comment(
                    $"// TODO: Consider adding '{propertyName}' property of type '{propertyType}' to source class");
                SyntaxTrivia secondCommentTrivia =
                    SyntaxFactory.Comment("// This will ensure the required property is automatically mapped");

                InvocationExpressionSyntax newInvocation = invocation.WithLeadingTrivia(
                    invocation.GetLeadingTrivia()
                        .Add(commentTrivia)
                        .Add(SyntaxFactory.EndOfLine("\n"))
                        .Add(secondCommentTrivia)
                        .Add(SyntaxFactory.EndOfLine("\n")));

                SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            $"AddProperty_{propertyName}");
        nestedActions.Add(addPropertyAction);

        // Register the grouped action
        var groupAction = CodeAction.Create(
            $"Fix mapping for '{propertyName}'...",
            nestedActions.ToImmutable(),
            isInlinable: true);

        context.RegisterCodeFix(groupAction, diagnostic);
    }

    private void RegisterBulkFixes(CodeFixContext context, InvocationExpressionSyntax invocation,
        SemanticModel semanticModel, SyntaxNode root)
    {
        // Bulk Fix 1: Map all unmapped properties to default value
        var bulkDefaultAction = CodeAction.Create(
            "Map all unmapped properties to default value",
            cancellationToken => BulkFixAsync(context.Document, root, invocation, semanticModel, "Default"),
            "AM011_Bulk_Default"
        );
        context.RegisterCodeFix(bulkDefaultAction, context.Diagnostics);

        // Bulk Fix 2: Map all unmapped properties to constant value
        var bulkConstantAction = CodeAction.Create(
            "Map all unmapped properties to constant value",
            cancellationToken => BulkFixAsync(context.Document, root, invocation, semanticModel, "Constant"),
            "AM011_Bulk_Constant"
        );
        context.RegisterCodeFix(bulkConstantAction, context.Diagnostics);
    }

    private async Task<Document> BulkFixAsync(Document document, SyntaxNode root, InvocationExpressionSyntax invocation,
        SemanticModel semanticModel, string mode)
    {
        // 1. Identify unmapped properties
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType == null || destType == null)
        {
            return document;
        }

        // This logic mimics the Analyzer logic to find *all* missing properties
        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType);
        var destProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destType, false);

        var missingProperties = new List<IPropertySymbol>();

        foreach (var destProp in destProperties)
        {
            // Check if required
            if (!destProp.IsRequired)
            {
                continue;
            }

            // Check if mapped (name match)
            if (sourceProperties.Any(p => string.Equals(p.Name, destProp.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // Check if explicitly configured
            if (AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember(invocation, destProp.Name, semanticModel))
            {
                continue;
            }

            missingProperties.Add(destProp);
        }

        if (!missingProperties.Any())
        {
            return document;
        }

        // 2. Apply fixes
        InvocationExpressionSyntax currentInvocation = invocation;

        foreach (var prop in missingProperties)
        {
            string valueToMap = mode == "Default"
                ? TypeConversionHelper.GetDefaultValueForType(prop.Type.ToDisplayString())
                : TypeConversionHelper.GetSampleValueForType(prop.Type.ToDisplayString());

            currentInvocation =
                CodeFixSyntaxHelper.CreateForMemberWithMapFrom(currentInvocation, prop.Name, valueToMap);
        }

        SyntaxNode newRoot = root.ReplaceNode(invocation, currentInvocation);
        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    /// Compute the Levenshtein distance between two strings.
    /// </summary>
    private static int ComputeLevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.IsNullOrEmpty(t) ? 0 : t.Length;
        }

        if (string.IsNullOrEmpty(t))
        {
            return s.Length;
        }

        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++)
        {
            d[i, 0] = i;
        }

        for (int j = 0; j <= m; j++)
        {
            d[0, j] = j;
        }

        for (int j = 1; j <= m; j++)
        {
            for (int i = 1; i <= n; i++)
            {
                if (s[i - 1] == t[j - 1])
                {
                    d[i, j] = d[i - 1, j - 1];
                }
                else
                {
                    d[i, j] = Math.Min(Math.Min(
                            d[i - 1, j] + 1, // deletion
                            d[i, j - 1] + 1), // insertion
                        d[i - 1, j - 1] + 1 // substitution
                    );
                }
            }
        }

        return d[n, m];
    }
}

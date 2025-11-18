using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers.TypeSafety;

/// <summary>
/// Code fix provider for AM002 Nullable Compatibility diagnostics.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM002_NullableCompatibilityCodeFixProvider)), Shared]
public class AM002_NullableCompatibilityCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM002");

    /// <summary>
    /// Gets whether this provider can fix multiple diagnostics in a single code action.
    /// </summary>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>
    /// Registers code fixes for the diagnostics.
    /// </summary>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        foreach (var diagnostic in context.Diagnostics)
        {
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var invocation = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(i => i.Span.Contains(diagnosticSpan));

            if (invocation == null)
            {
                continue;
            }

            var propertyName = ExtractPropertyNameFromDiagnostic(diagnostic);
            if (string.IsNullOrEmpty(propertyName))
            {
                continue;
            }

            if (diagnostic.Descriptor != AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            {
                continue;
            }

            var destinationType = ExtractDestinationTypeFromDiagnostic(diagnostic);
            var defaultValue = TypeConversionHelper.GetDefaultValueForType(destinationType ?? string.Empty);
            var defaultTitle = $"Add null coalescing operator for '{propertyName}'";

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: defaultTitle,
                    createChangedDocument: cancellationToken =>
                        AddMapFromAsync(context.Document, invocation, propertyName!,
                            $"src.{propertyName} ?? {defaultValue}", cancellationToken),
                    equivalenceKey: $"AM002_NullCoalescing_{propertyName}"),
                diagnostic);

            var sampleValue = TypeConversionHelper.GetSampleValueForType(destinationType ?? string.Empty);
            if (sampleValue != defaultValue)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Add null coalescing with sample value for '{propertyName}'",
                        createChangedDocument: cancellationToken =>
                            AddMapFromAsync(context.Document, invocation, propertyName!,
                                $"src.{propertyName} ?? {sampleValue}", cancellationToken),
                        equivalenceKey: $"AM002_SampleValue_{propertyName}"),
                    diagnostic);
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Ignore property '{propertyName}'",
                    createChangedDocument: cancellationToken =>
                        AddIgnoreAsync(context.Document, invocation, propertyName!, cancellationToken),
                    equivalenceKey: $"AM002_Ignore_{propertyName}"),
                diagnostic);
        }
    }

    private static async Task<Document> AddMapFromAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string propertyName,
        string mapFromExpression,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName, mapFromExpression);
        var newRoot = root.ReplaceNode(invocation, newInvocation);
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

    private string? ExtractPropertyNameFromDiagnostic(Diagnostic diagnostic)
    {
        // Try to get property name from diagnostic properties
        if (diagnostic.Properties.TryGetValue("PropertyName", out var propertyName))
        {
            return propertyName;
        }

        // Fallback: extract from diagnostic message
        var message = diagnostic.GetMessage();
        var match = System.Text.RegularExpressions.Regex.Match(message, @"Property '(\w+)'");
        return match.Success ? match.Groups[1].Value : null;
    }

    private string? ExtractDestinationTypeFromDiagnostic(Diagnostic diagnostic)
    {
        // Try to get from diagnostic properties
        if (diagnostic.Properties.TryGetValue("DestType", out var destType))
        {
            return destType;
        }

        // Fallback: extract from diagnostic message (e.g., "int?")
        var message = diagnostic.GetMessage();
        // Match pattern like "(...) is nullable" to extract the type before
        var match = System.Text.RegularExpressions.Regex.Match(message, @"\(([^)]+)\)\s+is non-nullable");
        return match.Success ? match.Groups[1].Value : null;
    }
}

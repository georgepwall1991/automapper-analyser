using System.Collections.Immutable;
using System.Composition;
using System.Text.RegularExpressions;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Analyzers.TypeSafety;

/// <summary>
///     Code fix provider for AM002 Nullable Compatibility diagnostics.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM002_NullableCompatibilityCodeFixProvider))]
[Shared]
public class AM002_NullableCompatibilityCodeFixProvider : CodeFixProvider
{
    /// <summary>
    ///     Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM002");

    /// <summary>
    ///     Gets whether this provider can fix multiple diagnostics in a single code action.
    /// </summary>
    public sealed override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <summary>
    ///     Registers code fixes for the diagnostics.
    /// </summary>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return;
        }

        foreach (Diagnostic? diagnostic in context.Diagnostics)
        {
            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

            InvocationExpressionSyntax? invocation = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(i => i.Span.Contains(diagnosticSpan));

            if (invocation == null)
            {
                continue;
            }

            string? propertyName = ExtractPropertyNameFromDiagnostic(diagnostic);
            if (string.IsNullOrEmpty(propertyName))
            {
                continue;
            }

            if (diagnostic.Descriptor != AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
            {
                continue;
            }

            string? destinationType = ExtractDestinationTypeFromDiagnostic(diagnostic);
            string defaultValue = TypeConversionHelper.GetDefaultValueForType(destinationType ?? string.Empty);
            string defaultTitle = $"Add null coalescing operator for '{propertyName}'";

            context.RegisterCodeFix(
                CodeAction.Create(
                    defaultTitle,
                    cancellationToken =>
                        AddMapFromAsync(context.Document, invocation, propertyName!,
                            $"src.{propertyName} ?? {defaultValue}", cancellationToken),
                    $"AM002_NullCoalescing_{propertyName}"),
                diagnostic);

            string sampleValue = TypeConversionHelper.GetSampleValueForType(destinationType ?? string.Empty);
            if (sampleValue != defaultValue)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        $"Add null coalescing with sample value for '{propertyName}'",
                        cancellationToken =>
                            AddMapFromAsync(context.Document, invocation, propertyName!,
                                $"src.{propertyName} ?? {sampleValue}", cancellationToken),
                        $"AM002_SampleValue_{propertyName}"),
                    diagnostic);
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Ignore property '{propertyName}'",
                    cancellationToken =>
                        AddIgnoreAsync(context.Document, invocation, propertyName!, cancellationToken),
                    $"AM002_Ignore_{propertyName}"),
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
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        InvocationExpressionSyntax newInvocation =
            CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName, mapFromExpression);
        SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
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

    private string? ExtractPropertyNameFromDiagnostic(Diagnostic diagnostic)
    {
        // Try to get property name from diagnostic properties
        if (diagnostic.Properties.TryGetValue("PropertyName", out string? propertyName))
        {
            return propertyName;
        }

        // Fallback: extract from diagnostic message
        string message = diagnostic.GetMessage();
        Match match = Regex.Match(message, @"Property '(\w+)'");
        return match.Success ? match.Groups[1].Value : null;
    }

    private string? ExtractDestinationTypeFromDiagnostic(Diagnostic diagnostic)
    {
        // Try to get from diagnostic properties
        if (diagnostic.Properties.TryGetValue("DestType", out string? destType))
        {
            return destType;
        }

        // Fallback: extract from diagnostic message (e.g., "int?")
        string message = diagnostic.GetMessage();
        // Match pattern like "(...) is nullable" to extract the type before
        Match match = Regex.Match(message, @"\(([^)]+)\)\s+is non-nullable");
        return match.Success ? match.Groups[1].Value : null;
    }
}

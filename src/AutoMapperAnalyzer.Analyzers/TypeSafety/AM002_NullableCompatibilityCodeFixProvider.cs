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
public class AM002_NullableCompatibilityCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <summary>
    ///     Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM002");

    /// <summary>
    ///     Registers code fixes for the diagnostics.
    /// </summary>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        CodeFixOperationContext? operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        ImmutableArray<Diagnostic> diagnostics = context.Diagnostics
            .Where(diag =>
                diag.Id == "AM002" &&
                diag.Descriptor == AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule &&
                diag.Location.IsInSource &&
                diag.Location.SourceTree == operationContext.Root.SyntaxTree &&
                diag.Location.SourceSpan.IntersectsWith(context.Span))
            .ToImmutableArray();

        foreach (Diagnostic? diagnostic in diagnostics)
        {
            if (diagnostic == null)
            {
                continue;
            }

            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

            InvocationExpressionSyntax? invocation = GetCreateMapInvocation(operationContext.Root, diagnostic) ??
                operationContext.Root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf()
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

            string? destinationType = ExtractDestinationTypeFromDiagnostic(diagnostic);
            string defaultValue = TypeConversionHelper.GetDefaultValueForType(destinationType ?? string.Empty);

            // Option 1: Map with default value
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Scaffold default mapping for '{propertyName}' ({defaultValue})",
                    cancellationToken => AddMapFromAsync(context.Document, invocation, propertyName!,
                        $"src.{propertyName} ?? {defaultValue}", cancellationToken),
                    $"AM002_DefaultValue_{propertyName}"),
                diagnostic);

            // Option 2: Ignore property
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Ignore property '{propertyName}' (manual review)",
                    cancellationToken => AddIgnoreAsync(context.Document, invocation, propertyName!, cancellationToken),
                    $"AM002_Ignore_{propertyName}"),
                diagnostic);
        }
    }

    private async Task<Document> AddMapFromAsync(
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
        return await ReplaceNodeAsync(document, root, invocation, newInvocation);
    }

    private async Task<Document> AddIgnoreAsync(
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
        return await ReplaceNodeAsync(document, root, invocation, newInvocation);
    }

    private string? ExtractPropertyNameFromDiagnostic(Diagnostic diagnostic)
    {
        // Try to get property name from diagnostic properties
        if (diagnostic.Properties.TryGetValue(AM002_NullableCompatibilityAnalyzer.PropertyNamePropertyName,
                out string? propertyName))
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
        if (diagnostic.Properties.TryGetValue(AM002_NullableCompatibilityAnalyzer.DestinationPropertyTypePropertyName,
                out string? destinationType))
        {
            return destinationType;
        }

        // Backward-compatible fallback for older diagnostics.
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

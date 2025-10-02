using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers;

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

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the CreateMap invocation that triggered the diagnostic
        var invocation = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(i => i.Span.Contains(diagnosticSpan));

        if (invocation == null) return;

        // Extract property name from diagnostic message
        var propertyName = ExtractPropertyNameFromDiagnostic(diagnostic);
        if (string.IsNullOrEmpty(propertyName)) return;

        // Check if this is NullableToNonNullable or NonNullableToNullable
        if (diagnostic.Descriptor == AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule)
        {
            // Offer to add null handling with null coalescing operator
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Add null coalescing operator for '{propertyName}'",
                    createChangedDocument: cancellationToken =>
                    {
                        var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                            invocation,
                            propertyName!,
                            $"src.{propertyName} ?? default");
                        var newRoot = root.ReplaceNode(invocation, newInvocation);
                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    },
                    equivalenceKey: $"AM002_NullCoalescing_{propertyName}"),
                diagnostic);

            // Offer to add null check with explicit value
            var destType = ExtractDestinationTypeFromDiagnostic(diagnostic);
            if (!string.IsNullOrEmpty(destType))
            {
                var defaultValue = TypeConversionHelper.GetDefaultValueForType(destType!);
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Add null check with default value for '{propertyName}'",
                        createChangedDocument: cancellationToken =>
                        {
                            var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                                invocation,
                                propertyName!,
                                $"src.{propertyName} ?? {defaultValue}");
                            var newRoot = root.ReplaceNode(invocation, newInvocation);
                            return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                        },
                        equivalenceKey: $"AM002_DefaultValue_{propertyName}"),
                    diagnostic);
            }

            // Offer to ignore the property
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Ignore property '{propertyName}'",
                    createChangedDocument: cancellationToken =>
                    {
                        var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName!);
                        var newRoot = root.ReplaceNode(invocation, newInvocation);
                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    },
                    equivalenceKey: $"AM002_Ignore_{propertyName}"),
                diagnostic);
        }
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

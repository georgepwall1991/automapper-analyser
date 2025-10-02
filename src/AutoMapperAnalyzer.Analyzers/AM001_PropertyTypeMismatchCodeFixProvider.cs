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
/// Code fix provider for AM001 Property Type Mismatch diagnostics.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM001_PropertyTypeMismatchCodeFixProvider)), Shared]
public class AM001_PropertyTypeMismatchCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM001");

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

        // Register different fix strategies based on the diagnostic descriptor
        if (diagnostic.Descriptor.Id == "AM001")
        {
            // Check which specific rule was triggered
            var messageFormat = diagnostic.Descriptor.MessageFormat.ToString();

            if (messageFormat.Contains("incompatible types"))
            {
                // Property Type Mismatch - offer ForMember with conversion
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Add ForMember configuration for '{propertyName}' with type conversion",
                        createChangedDocument: cancellationToken =>
                        {
                            var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                                invocation,
                                propertyName!,
                                $"src.{propertyName}.ToString()");
                            var newRoot = root.ReplaceNode(invocation, newInvocation);
                            return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                        },
                        equivalenceKey: $"AM001_ForMember_{propertyName}"),
                    diagnostic);

                // Offer to ignore the property if types are incompatible
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Ignore property '{propertyName}'",
                        createChangedDocument: cancellationToken =>
                        {
                            var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName!);
                            var newRoot = root.ReplaceNode(invocation, newInvocation);
                            return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                        },
                        equivalenceKey: $"AM001_Ignore_{propertyName}"),
                    diagnostic);
            }
            else if (messageFormat.Contains("nullable compatibility"))
            {
                // Nullable Compatibility Issue - offer null handling
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Add null handling for property '{propertyName}'",
                        createChangedDocument: cancellationToken =>
                        {
                            var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                                invocation,
                                propertyName!,
                                $"src.{propertyName} ?? default");
                            var newRoot = root.ReplaceNode(invocation, newInvocation);
                            return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                        },
                        equivalenceKey: $"AM001_NullHandling_{propertyName}"),
                    diagnostic);
            }
            else if (messageFormat.Contains("Complex type mapping"))
            {
                // Complex Type Mapping Missing - offer to create mapping
                var types = ExtractTypesFromDiagnostic(diagnostic);
                if (!string.IsNullOrEmpty(types.sourceType) && !string.IsNullOrEmpty(types.destType))
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: $"Add CreateMap<{types.sourceType}, {types.destType}>() configuration",
                            createChangedDocument: cancellationToken =>
                            {
                                // TODO: Implement complex type mapping creation
                                // This would require more sophisticated syntax manipulation
                                return Task.FromResult(context.Document);
                            },
                            equivalenceKey: $"AM001_CreateMap_{types.sourceType}_{types.destType}"),
                        diagnostic);
                }
            }
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

    private (string? sourceType, string? destType) ExtractTypesFromDiagnostic(Diagnostic diagnostic)
    {
        // Try to get from diagnostic properties first
        var sourceType = diagnostic.Properties.TryGetValue("SourceType", out var st) ? st : null;
        var destType = diagnostic.Properties.TryGetValue("DestType", out var dt) ? dt : null;

        if (!string.IsNullOrEmpty(sourceType) && !string.IsNullOrEmpty(destType))
        {
            return (sourceType, destType);
        }

        // Fallback: extract from diagnostic message
        var message = diagnostic.GetMessage();
        var match = System.Text.RegularExpressions.Regex.Match(message, @"from '(\w+)' to '(\w+)'");
        if (match.Success && match.Groups.Count >= 3)
        {
            return (match.Groups[1].Value, match.Groups[2].Value);
        }

        return (null, null);
    }
}

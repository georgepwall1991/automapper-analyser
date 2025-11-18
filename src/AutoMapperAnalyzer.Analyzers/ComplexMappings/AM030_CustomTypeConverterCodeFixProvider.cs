using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers.ComplexMappings;

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
                    RegisterInvalidConverterFixes(context, root, invocation, propertyName!, diagnostic);
                    break;

                case "AM030" when diagnostic.Descriptor.Title.ToString().Contains("null values"):
                    RegisterNullHandlingFixes(context, root, invocation, propertyName!, diagnostic);
                    break;
            }
        }
    }

    private void RegisterMissingConvertUsingFixes(CodeFixContext context, SyntaxNode root,
        InvocationExpressionSyntax invocation, string propertyName, Diagnostic diagnostic)
    {
        // Fix 1: Add ForMember with ConvertUsing for string to primitive conversions
        if (diagnostic.Properties.TryGetValue("ConverterType", out var converterType) &&
            converterType!.Contains("String") && IsStringToPrimitiveConversion(converterType))
        {
            var lambdaFix = CodeAction.Create(
                title: $"Add ConvertUsing with lambda for '{propertyName}'",
                createChangedDocument: cancellationToken =>
                {
                    var conversion = GetStringToPrimitiveConversion(converterType!);
                    var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                        invocation,
                        propertyName,
                        $"{conversion}(src.{propertyName})");
                    var newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"ConvertUsingLambda_{propertyName}");

            context.RegisterCodeFix(lambdaFix, diagnostic);
        }

        // Fix 2: Add ForMember with custom converter placeholder
        var converterClassFix = CodeAction.Create(
            title: $"Add ConvertUsing with custom converter for '{propertyName}'",
            createChangedDocument: cancellationToken =>
            {
                var converterName = diagnostic.Properties.GetValueOrDefault("ConverterType", "CustomConverter") ?? "CustomConverter";
                var comment = SyntaxFactory.Comment($"// TODO: Implement {converterName} converter class");
                var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                        invocation,
                        propertyName,
                        $"/* TODO: Use ConvertUsing<{converterName}>() */ src.{propertyName}")
                    .WithLeadingTrivia(comment, SyntaxFactory.EndOfLine("\n"));
                var newRoot = root.ReplaceNode(invocation, newInvocation);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            equivalenceKey: $"ConvertUsingConverter_{propertyName}");

        context.RegisterCodeFix(converterClassFix, diagnostic);

        // Fix 3: Ignore the property if conversion is too complex
        var ignoreFix = CodeAction.Create(
            title: $"Ignore property '{propertyName}' (conversion too complex)",
            createChangedDocument: cancellationToken =>
            {
                var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
                var newRoot = root.ReplaceNode(invocation, newInvocation);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            equivalenceKey: $"Ignore_{propertyName}");

        context.RegisterCodeFix(ignoreFix, diagnostic);
    }

    private void RegisterInvalidConverterFixes(CodeFixContext context, SyntaxNode root,
        InvocationExpressionSyntax invocation, string propertyName, Diagnostic diagnostic)
    {
        // Fix 1: Add comment about implementing proper converter
        var commentFix = CodeAction.Create(
            title: "Add comment about fixing converter implementation",
            createChangedDocument: cancellationToken =>
            {
                var comment = SyntaxFactory.Comment("// TODO: Ensure converter implements ITypeConverter<TSource, TDestination>");
                var newInvocation = invocation.WithLeadingTrivia(
                    invocation.GetLeadingTrivia()
                        .Add(comment)
                        .Add(SyntaxFactory.EndOfLine("\n")));
                var newRoot = root.ReplaceNode(invocation, newInvocation);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            equivalenceKey: "FixConverterImplementation");

        context.RegisterCodeFix(commentFix, diagnostic);

        // Fix 2: Replace with simple MapFrom if possible
        var mapFromFix = CodeAction.Create(
            title: $"Replace converter with simple MapFrom for '{propertyName}'",
            createChangedDocument: cancellationToken =>
            {
                var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                    invocation,
                    propertyName,
                    $"src.{propertyName}");
                var newRoot = root.ReplaceNode(invocation, newInvocation);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            equivalenceKey: $"ReplaceWithMapFrom_{propertyName}");

        context.RegisterCodeFix(mapFromFix, diagnostic);
    }

    private void RegisterNullHandlingFixes(CodeFixContext context, SyntaxNode root,
        InvocationExpressionSyntax invocation, string propertyName, Diagnostic diagnostic)
    {
        // Fix 1: Add null handling in MapFrom
        var nullHandlingFix = CodeAction.Create(
            title: $"Add null handling for '{propertyName}'",
            createChangedDocument: cancellationToken =>
            {
                var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                    invocation,
                    propertyName,
                    $"src.{propertyName} ?? default");
                var newRoot = root.ReplaceNode(invocation, newInvocation);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            equivalenceKey: $"NullHandling_{propertyName}");

        context.RegisterCodeFix(nullHandlingFix, diagnostic);

        // Fix 2: Use null coalescing operator with specific default
        if (diagnostic.Properties.TryGetValue("PropertyType", out var propertyType))
        {
            var defaultValueFix = CodeAction.Create(
                title: $"Add null handling with default value for '{propertyName}'",
                createChangedDocument: cancellationToken =>
                {
                    var defaultValue = TypeConversionHelper.GetDefaultValueForType(propertyType!);
                    var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                        invocation,
                        propertyName,
                        $"src.{propertyName} ?? {defaultValue}");
                    var newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                equivalenceKey: $"NullHandlingWithDefault_{propertyName}");

            context.RegisterCodeFix(defaultValueFix, diagnostic);
        }
    }

    private bool IsStringToPrimitiveConversion(string converterType)
    {
        return converterType.Contains("Int") ||
               converterType.Contains("Double") ||
               converterType.Contains("Decimal") ||
               converterType.Contains("Boolean") ||
               converterType.Contains("DateTime") ||
               converterType.Contains("Guid");
    }

    private string GetStringToPrimitiveConversion(string converterType)
    {
        if (converterType.Contains("Int")) return "int.Parse";
        if (converterType.Contains("Double")) return "double.Parse";
        if (converterType.Contains("Decimal")) return "decimal.Parse";
        if (converterType.Contains("Boolean")) return "bool.Parse";
        if (converterType.Contains("DateTime")) return "DateTime.Parse";
        if (converterType.Contains("Guid")) return "Guid.Parse";
        return "Convert.ChangeType";
    }
}

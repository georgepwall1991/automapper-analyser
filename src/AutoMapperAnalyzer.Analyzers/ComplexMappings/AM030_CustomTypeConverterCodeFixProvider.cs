using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Analyzers.ComplexMappings;

/// <summary>
///     Code fix provider for AM030 diagnostic - Custom Type Converter issues.
///     Provides fixes for missing ConvertUsing configurations and invalid converter implementations.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM030_CustomTypeConverterCodeFixProvider))]
[Shared]
public class AM030_CustomTypeConverterCodeFixProvider : CodeFixProvider
{
    /// <summary>
    ///     Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM030");

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

        foreach (Diagnostic? diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue("PropertyName", out string? propertyName) ||
                string.IsNullOrEmpty(propertyName))
            {
                continue;
            }

            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;
            SyntaxNode node = root.FindNode(diagnosticSpan);

            if (node.FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation)
            {
                continue;
            }

            string diagnosticId = diagnostic.Id;

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
        if (diagnostic.Properties.TryGetValue("ConverterType", out string? converterType) &&
            converterType!.Contains("String") && IsStringToPrimitiveConversion(converterType))
        {
            var lambdaFix = CodeAction.Create(
                $"Add ConvertUsing with lambda for '{propertyName}'",
                cancellationToken =>
                {
                    string conversion = GetStringToPrimitiveConversion(converterType!);
                    InvocationExpressionSyntax newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                        invocation,
                        propertyName,
                        $"{conversion}(src.{propertyName})");
                    SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                $"ConvertUsingLambda_{propertyName}");

            context.RegisterCodeFix(lambdaFix, diagnostic);
        }

        // Fix 2: Add ForMember with custom converter placeholder
        var converterClassFix = CodeAction.Create(
            $"Add ConvertUsing with custom converter for '{propertyName}'",
            cancellationToken =>
            {
                string converterName = diagnostic.Properties.GetValueOrDefault("ConverterType", "CustomConverter") ??
                                       "CustomConverter";
                SyntaxTrivia comment = SyntaxFactory.Comment($"// TODO: Implement {converterName} converter class");
                InvocationExpressionSyntax newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                        invocation,
                        propertyName,
                        $"/* TODO: Use ConvertUsing<{converterName}>() */ src.{propertyName}")
                    .WithLeadingTrivia(comment, SyntaxFactory.EndOfLine("\n"));
                SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            $"ConvertUsingConverter_{propertyName}");

        context.RegisterCodeFix(converterClassFix, diagnostic);

        // Fix 3: Ignore the property if conversion is too complex
        var ignoreFix = CodeAction.Create(
            $"Ignore property '{propertyName}' (conversion too complex)",
            cancellationToken =>
            {
                InvocationExpressionSyntax newInvocation =
                    CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
                SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            $"Ignore_{propertyName}");

        context.RegisterCodeFix(ignoreFix, diagnostic);
    }

    private void RegisterInvalidConverterFixes(CodeFixContext context, SyntaxNode root,
        InvocationExpressionSyntax invocation, string propertyName, Diagnostic diagnostic)
    {
        // Fix 1: Add comment about implementing proper converter
        var commentFix = CodeAction.Create(
            "Add comment about fixing converter implementation",
            cancellationToken =>
            {
                SyntaxTrivia comment =
                    SyntaxFactory.Comment("// TODO: Ensure converter implements ITypeConverter<TSource, TDestination>");
                InvocationExpressionSyntax newInvocation = invocation.WithLeadingTrivia(
                    invocation.GetLeadingTrivia()
                        .Add(comment)
                        .Add(SyntaxFactory.EndOfLine("\n")));
                SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            "FixConverterImplementation");

        context.RegisterCodeFix(commentFix, diagnostic);

        // Fix 2: Replace with simple MapFrom if possible
        var mapFromFix = CodeAction.Create(
            $"Replace converter with simple MapFrom for '{propertyName}'",
            cancellationToken =>
            {
                InvocationExpressionSyntax newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                    invocation,
                    propertyName,
                    $"src.{propertyName}");
                SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            $"ReplaceWithMapFrom_{propertyName}");

        context.RegisterCodeFix(mapFromFix, diagnostic);
    }

    private void RegisterNullHandlingFixes(CodeFixContext context, SyntaxNode root,
        InvocationExpressionSyntax invocation, string propertyName, Diagnostic diagnostic)
    {
        // Fix 1: Add null handling in MapFrom
        var nullHandlingFix = CodeAction.Create(
            $"Add null handling for '{propertyName}'",
            cancellationToken =>
            {
                InvocationExpressionSyntax newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                    invocation,
                    propertyName,
                    $"src.{propertyName} ?? default");
                SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            $"NullHandling_{propertyName}");

        context.RegisterCodeFix(nullHandlingFix, diagnostic);

        // Fix 2: Use null coalescing operator with specific default
        if (diagnostic.Properties.TryGetValue("PropertyType", out string? propertyType))
        {
            var defaultValueFix = CodeAction.Create(
                $"Add null handling with default value for '{propertyName}'",
                cancellationToken =>
                {
                    string defaultValue = TypeConversionHelper.GetDefaultValueForType(propertyType!);
                    InvocationExpressionSyntax newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                        invocation,
                        propertyName,
                        $"src.{propertyName} ?? {defaultValue}");
                    SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                $"NullHandlingWithDefault_{propertyName}");

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
        if (converterType.Contains("Int"))
        {
            return "Convert.ToInt32";
        }

        if (converterType.Contains("Double"))
        {
            return "Convert.ToDouble";
        }

        if (converterType.Contains("Decimal"))
        {
            return "Convert.ToDecimal";
        }

        if (converterType.Contains("Boolean"))
        {
            return "Convert.ToBoolean";
        }

        if (converterType.Contains("DateTime"))
        {
            return "DateTime.Parse"; // Convert.ToDateTime also exists but Parse is idiomatic
        }

        if (converterType.Contains("Guid"))
        {
            return "Guid.Parse";
        }

        return "Convert.ChangeType";
    }
}

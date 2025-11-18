using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers.TypeSafety;

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

        if (diagnostic.Descriptor != AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel == null)
        {
            return;
        }

        var semanticInfo = semanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (semanticInfo.Symbol is not IMethodSymbol methodSymbol || methodSymbol.TypeArguments.Length != 2)
        {
            return;
        }

        var sourceType = methodSymbol.TypeArguments[0];
        var destinationType = methodSymbol.TypeArguments[1];

        var sourceProperty = FindProperty(sourceType, propertyName!, expectSetter: false);
        var destinationProperty = FindProperty(destinationType, propertyName!, expectSetter: true);
        if (sourceProperty == null || destinationProperty == null)
        {
            return;
        }

        var sourcePropertyType = sourceProperty.Type;
        var destinationPropertyType = destinationProperty.Type;

        if (SymbolEqualityComparer.Default.Equals(sourcePropertyType, destinationPropertyType))
        {
            return;
        }

        // Prepare map-from expression that either converts or casts.
        var conversionExpression = CreateConversionExpression(sourcePropertyType, destinationPropertyType, propertyName!);
        if (conversionExpression != null)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Map '{propertyName}' with conversion",
                    createChangedDocument: cancellationToken =>
                    {
                        var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                            invocation,
                            propertyName!,
                            conversionExpression);
                        var newRoot = root.ReplaceNode(invocation, newInvocation);
                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    },
                    equivalenceKey: $"AM001_MapWithConversion_{propertyName}"),
                diagnostic);
        }

        // Always provide ignore option as a safe fallback.
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

    private static IPropertySymbol? FindProperty(ITypeSymbol typeSymbol, string name, bool expectSetter)
    {
        if (typeSymbol is not INamedTypeSymbol namedType)
        {
            return null;
        }

        return AutoMapperAnalysisHelpers
            .GetMappableProperties(namedType, requireSetter: expectSetter)
            .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string? CreateConversionExpression(ITypeSymbol sourceType, ITypeSymbol destinationType, string propertyName)
    {
        if (SymbolEqualityComparer.Default.Equals(sourceType, destinationType))
        {
            return null;
        }

        var destinationTypeName = destinationType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        // Nullable source to non-nullable destination -> coalesce to default literal
        if (sourceType.NullableAnnotation == NullableAnnotation.Annotated)
        {
            var fallback = TypeConversionHelper.GetDefaultValueForType(destinationTypeName);
            return $"src.{propertyName} ?? {fallback}";
        }

        // Numeric conversions: add cast
        if (IsNumericConversion(sourceType.SpecialType) && IsNumericConversion(destinationType.SpecialType))
        {
            return $"({destinationTypeName})src.{propertyName}";
        }

        // String -> primitive: use parse pattern inside MapFrom
        if (IsString(sourceType) && IsNumericConversion(destinationType.SpecialType))
        {
            return $"src.{propertyName} is not null ? {destinationTypeName}.Parse(src.{propertyName}) : {TypeConversionHelper.GetDefaultValueForType(destinationTypeName)}";
        }

        // Primitive -> string: use ToString with invariant culture for numeric types
        if (IsNumericConversion(sourceType.SpecialType) && IsString(destinationType))
        {
            return $"src.{propertyName}.ToString()";
        }

        // As a safe catch-all, allow cast when the destination is assignable from source
        if (destinationType.IsReferenceType && sourceType.IsReferenceType)
        {
            return $"({destinationTypeName})src.{propertyName}";
        }

        return null;
    }

    private static bool IsNumericConversion(SpecialType specialType)
    {
        return specialType is SpecialType.System_Byte or SpecialType.System_SByte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 or
            SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int64 or SpecialType.System_UInt64 or
            SpecialType.System_Single or SpecialType.System_Double or
            SpecialType.System_Decimal;
    }

    private static bool IsString(ITypeSymbol typeSymbol)
    {
        return typeSymbol.SpecialType == SpecialType.System_String;
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

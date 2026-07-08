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
///     Code fix provider for AM001 Property Type Mismatch diagnostics.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM001_PropertyTypeMismatchCodeFixProvider))]
[Shared]
public class AM001_PropertyTypeMismatchCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <summary>
    ///     Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM001");

    /// <summary>
    ///     Registers code fixes for the diagnostics.
    /// </summary>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        foreach (Diagnostic diagnostic in GetRelevantDiagnostics(context, operationContext.Root))
        {
            InvocationExpressionSyntax? invocation = GetCreateMapInvocation(operationContext.Root, diagnostic);
            if (invocation == null)
            {
                continue;
            }

            string? propertyName = ExtractPropertyNameFromDiagnostic(diagnostic);
            if (string.IsNullOrEmpty(propertyName))
            {
                continue;
            }

            string propertyNameValue = propertyName!;

            (ITypeSymbol? sourceType, ITypeSymbol? destinationType) =
                ResolveCreateMapTypesWithReverse(invocation, operationContext.SemanticModel);
            if (sourceType == null || destinationType == null)
            {
                continue;
            }

            IPropertySymbol? sourceProperty = FindProperty(sourceType, propertyNameValue, false);
            IPropertySymbol? destinationProperty = FindProperty(destinationType, propertyNameValue, true);
            if (sourceProperty == null || destinationProperty == null)
            {
                continue;
            }

            ITypeSymbol sourcePropertyType = sourceProperty.Type;
            ITypeSymbol destinationPropertyType = destinationProperty.Type;

            if (SymbolEqualityComparer.Default.Equals(sourcePropertyType, destinationPropertyType))
            {
                continue;
            }

            string? conversionExpression =
                CreateConversionExpression(sourcePropertyType, destinationPropertyType, propertyNameValue);
            // Equivalence keys are property-name scoped so Fix All can batch same-named
            // mismatches across CreateMap calls; each action still closes over its diagnostic location.
            if (conversionExpression != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        $"Map '{propertyNameValue}' with conversion",
                        cancellationToken =>
                            AddMapFromAsync(context.Document, diagnostic, propertyNameValue, conversionExpression, cancellationToken),
                        $"AM001_MapWithConversion_{propertyNameValue}"),
                    diagnostic);
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Ignore property '{propertyNameValue}' (manual review)",
                    cancellationToken =>
                        AddIgnoreAsync(context.Document, diagnostic, propertyNameValue, cancellationToken),
                    $"AM001_Ignore_{propertyNameValue}"),
                diagnostic);
        }
    }

    private static IEnumerable<Diagnostic> GetRelevantDiagnostics(CodeFixContext context, SyntaxNode root)
    {
        return context.Diagnostics
            .Where(diag =>
                diag.Id == "AM001" &&
                diag.Location.IsInSource &&
                diag.Location.SourceTree == root.SyntaxTree &&
                diag.Location.SourceSpan.IntersectsWith(context.Span))
            .OrderBy(diag => diag.Location.SourceSpan.Start)
            .ThenBy(diag => diag.Properties.TryGetValue(
                    AM001_PropertyTypeMismatchAnalyzer.PropertyNamePropertyName,
                    out string? propertyName)
                ? propertyName
                : diag.GetMessage(), StringComparer.Ordinal);
    }

    private async Task<Document> AddMapFromAsync(
        Document document,
        Diagnostic diagnostic,
        string propertyName,
        string conversionExpression,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return document;
        }

        InvocationExpressionSyntax? invocation = GetCreateMapInvocation(root, diagnostic);
        if (invocation == null)
        {
            return document;
        }

        InvocationExpressionSyntax newInvocation =
            CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName, conversionExpression);
        return await ReplaceNodeAsync(document, root, invocation, newInvocation);
    }

    private async Task<Document> AddIgnoreAsync(
        Document document,
        Diagnostic diagnostic,
        string propertyName,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return document;
        }

        InvocationExpressionSyntax? invocation = GetCreateMapInvocation(root, diagnostic);
        if (invocation == null)
        {
            return document;
        }

        InvocationExpressionSyntax newInvocation =
            CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
        return await ReplaceNodeAsync(document, root, invocation, newInvocation);
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

    private static string? CreateConversionExpression(ITypeSymbol sourceType, ITypeSymbol destinationType,
        string propertyName)
    {
        if (SymbolEqualityComparer.Default.Equals(sourceType, destinationType))
        {
            return null;
        }

        string escapedPropertyName = CodeFixSyntaxHelper.EscapeIdentifier(propertyName);
        string srcMember = $"src.{escapedPropertyName}";

        // Peel Nullable<T> / nullable annotations so conversion recipes key off underlying SpecialTypes.
        ITypeSymbol sourceUnderlying = AutoMapperAnalysisHelpers.GetUnderlyingType(sourceType);
        ITypeSymbol destinationUnderlying = AutoMapperAnalysisHelpers.GetUnderlyingType(destinationType);
        bool sourceIsNullable = IsNullableType(sourceType);
        bool destinationIsNullable = IsNullableType(destinationType);

        string destinationTypeName =
            destinationUnderlying.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        string destinationDisplayForCast =
            destinationType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        const string invariantCulture = "global::System.Globalization.CultureInfo.InvariantCulture";

        // Numeric conversions: add cast (after peeling nullable wrappers).
        if (IsNumericConversion(sourceUnderlying.SpecialType) &&
            IsNumericConversion(destinationUnderlying.SpecialType))
        {
            if (!SymbolEqualityComparer.Default.Equals(sourceUnderlying, destinationUnderlying))
            {
                if (sourceIsNullable && !destinationIsNullable)
                {
                    string fallback = TypeConversionHelper.GetDefaultValueForType(destinationTypeName);
                    return $"({destinationTypeName})({srcMember} ?? {fallback})";
                }

                if (sourceIsNullable)
                {
                    return $"{srcMember}.HasValue ? ({destinationTypeName}){srcMember}.Value : null";
                }

                return $"({destinationDisplayForCast}){srcMember}";
            }

            // Same underlying numeric type, nullable → non-nullable.
            if (sourceIsNullable && !destinationIsNullable)
            {
                string fallback = TypeConversionHelper.GetDefaultValueForType(destinationTypeName);
                return $"{srcMember} ?? {fallback}";
            }
        }

        // String -> numeric: Parse with invariant culture.
        if (IsString(sourceUnderlying) && IsNumericConversion(destinationUnderlying.SpecialType))
        {
            string fallback = TypeConversionHelper.GetDefaultValueForType(destinationTypeName);
            return
                $"{srcMember} != null ? {destinationTypeName}.Parse({srcMember}, {invariantCulture}) : {fallback}";
        }

        // Numeric -> string: invariant culture ToString.
        if (IsNumericConversion(sourceUnderlying.SpecialType) && IsString(destinationUnderlying))
        {
            if (sourceIsNullable)
            {
                return $"{srcMember}.HasValue ? {srcMember}.Value.ToString({invariantCulture}) : string.Empty";
            }

            return $"{srcMember}.ToString({invariantCulture})";
        }

        // Enum -> string
        if (sourceUnderlying.TypeKind == TypeKind.Enum && IsString(destinationUnderlying))
        {
            if (sourceIsNullable)
            {
                return $"{srcMember}.HasValue ? {srcMember}.Value.ToString() : string.Empty";
            }

            return $"{srcMember}.ToString()";
        }

        // String -> enum
        if (IsString(sourceUnderlying) && destinationUnderlying.TypeKind == TypeKind.Enum)
        {
            string fullyQualifiedDestinationTypeName =
                destinationUnderlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return
                $"{srcMember} != null ? global::System.Enum.Parse<{fullyQualifiedDestinationTypeName}>({srcMember}) : default";
        }

        // bool <-> string
        if (sourceUnderlying.SpecialType == SpecialType.System_Boolean && IsString(destinationUnderlying))
        {
            if (sourceIsNullable)
            {
                return $"{srcMember}.HasValue ? {srcMember}.Value.ToString() : string.Empty";
            }

            return $"{srcMember}.ToString()";
        }

        if (IsString(sourceUnderlying) && destinationUnderlying.SpecialType == SpecialType.System_Boolean)
        {
            return $"{srcMember} != null ? bool.Parse({srcMember}) : false";
        }

        // char -> string
        if (sourceUnderlying.SpecialType == SpecialType.System_Char && IsString(destinationUnderlying))
        {
            if (sourceIsNullable)
            {
                return $"{srcMember}.HasValue ? {srcMember}.Value.ToString() : string.Empty";
            }

            return $"{srcMember}.ToString()";
        }

        // Framework scalar <-> string (DateTime, Guid, Uri, DateOnly, TimeOnly, ...)
        if (TryCreateFrameworkStringConversion(
                sourceUnderlying,
                destinationUnderlying,
                srcMember,
                sourceIsNullable,
                out string? frameworkConversion))
        {
            return frameworkConversion;
        }

        // Nullable source to non-nullable destination where underlying types are compatible.
        if (sourceIsNullable &&
            !destinationIsNullable &&
            AutoMapperAnalysisHelpers.AreTypesCompatible(sourceUnderlying, destinationUnderlying))
        {
            string fallback = TypeConversionHelper.GetDefaultValueForType(destinationTypeName);
            return $"{srcMember} ?? {fallback}";
        }

        return null;
    }

    private static bool TryCreateFrameworkStringConversion(
        ITypeSymbol sourceUnderlying,
        ITypeSymbol destinationUnderlying,
        string srcMember,
        bool sourceIsNullable,
        out string? expression)
    {
        expression = null;

        if (IsString(destinationUnderlying) && IsFrameworkToStringType(sourceUnderlying))
        {
            bool needsInvariant = RequiresInvariantCultureFormat(sourceUnderlying);
            const string invariantCulture = "global::System.Globalization.CultureInfo.InvariantCulture";
            if (sourceIsNullable)
            {
                expression = needsInvariant
                    ? $"{srcMember}.HasValue ? {srcMember}.Value.ToString({invariantCulture}) : string.Empty"
                    : $"{srcMember} != null ? {srcMember}.ToString() : string.Empty";
            }
            else
            {
                expression = needsInvariant
                    ? $"{srcMember}.ToString({invariantCulture})"
                    : $"{srcMember}.ToString()";
            }

            return true;
        }

        if (IsString(sourceUnderlying) && IsFrameworkParseType(destinationUnderlying, out string parseCall))
        {
            string fallback = TypeConversionHelper.GetDefaultValueForType(
                destinationUnderlying.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            expression = $"{srcMember} != null ? {parseCall}({srcMember}) : {fallback}";
            return true;
        }

        return false;
    }

    private static bool IsFrameworkToStringType(ITypeSymbol type)
    {
        if (type.SpecialType is SpecialType.System_DateTime or SpecialType.System_Boolean or SpecialType.System_Char)
        {
            return true;
        }

        return type.ContainingNamespace?.ToDisplayString() == "System" &&
               type.Name is "DateTimeOffset" or "DateOnly" or "TimeOnly" or "TimeSpan" or "Guid" or "Uri";
    }

    private static bool RequiresInvariantCultureFormat(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_DateTime)
        {
            return true;
        }

        return type.ContainingNamespace?.ToDisplayString() == "System" &&
               type.Name is "DateTimeOffset" or "DateOnly" or "TimeOnly" or "TimeSpan" or "Decimal";
    }

    private static bool IsFrameworkParseType(ITypeSymbol type, out string parseCall)
    {
        parseCall = string.Empty;

        if (type.SpecialType == SpecialType.System_DateTime)
        {
            parseCall = "global::System.DateTime.Parse";
            return true;
        }

        if (type.ContainingNamespace?.ToDisplayString() == "System")
        {
            switch (type.Name)
            {
                case "Guid":
                    parseCall = "global::System.Guid.Parse";
                    return true;
                case "DateTimeOffset":
                    parseCall = "global::System.DateTimeOffset.Parse";
                    return true;
                case "DateOnly":
                    parseCall = "global::System.DateOnly.Parse";
                    return true;
                case "TimeOnly":
                    parseCall = "global::System.TimeOnly.Parse";
                    return true;
                case "Uri":
                    parseCall = "new global::System.Uri";
                    return true;
            }
        }

        return false;
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

    private static bool IsNullableType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return true;
        }

        return type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.Annotated;
    }

    private static string? ExtractPropertyNameFromDiagnostic(Diagnostic diagnostic)
    {
        if (diagnostic.Properties.TryGetValue(AM001_PropertyTypeMismatchAnalyzer.PropertyNamePropertyName,
                out string? propertyName))
        {
            return propertyName;
        }

        // Fallback: extract from diagnostic message
        string message = diagnostic.GetMessage();
        Match match = Regex.Match(message, @"Property '(\w+)'");
        return match.Success ? match.Groups[1].Value : null;
    }
}

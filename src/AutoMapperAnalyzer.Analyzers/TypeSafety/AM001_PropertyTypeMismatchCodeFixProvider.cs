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
    private const string PropertyNamePropertyName = "PropertyName";
    private const string SourcePropertyTypePropertyName = "SourcePropertyType";
    private const string DestinationPropertyTypePropertyName = "DestinationPropertyType";

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

        Diagnostic? diagnostic = context.Diagnostics.FirstOrDefault(diag =>
            diag.Id == "AM001" &&
            diag.Location.IsInSource &&
            diag.Location.SourceTree == operationContext.Root.SyntaxTree &&
            diag.Location.SourceSpan.IntersectsWith(context.Span));

        if (diagnostic == null)
        {
            return;
        }

        // Find the CreateMap invocation that triggered the diagnostic
        InvocationExpressionSyntax? invocation = GetCreateMapInvocation(operationContext.Root, diagnostic);
        if (invocation == null)
        {
            return;
        }

        // Extract property name from diagnostic message
        string? propertyName = ExtractPropertyNameFromDiagnostic(diagnostic);
        if (string.IsNullOrEmpty(propertyName))
        {
            return;
        }

        SymbolInfo semanticInfo = operationContext.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (semanticInfo.Symbol is not IMethodSymbol methodSymbol || methodSymbol.TypeArguments.Length != 2)
        {
            return;
        }

        ITypeSymbol sourceType = methodSymbol.TypeArguments[0];
        ITypeSymbol destinationType = methodSymbol.TypeArguments[1];

        IPropertySymbol? sourceProperty = FindProperty(sourceType, propertyName!, false);
        IPropertySymbol? destinationProperty = FindProperty(destinationType, propertyName!, true);
        if (sourceProperty == null || destinationProperty == null)
        {
            return;
        }

        ITypeSymbol sourcePropertyType = sourceProperty.Type;
        ITypeSymbol destinationPropertyType = destinationProperty.Type;

        if (SymbolEqualityComparer.Default.Equals(sourcePropertyType, destinationPropertyType))
        {
            return;
        }

        // Prepare map-from expression that either converts or casts.
        string? conversionExpression =
            CreateConversionExpression(sourcePropertyType, destinationPropertyType, propertyName!);
        if (conversionExpression != null)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Map '{propertyName}' with conversion",
                    cancellationToken =>
                    {
                        InvocationExpressionSyntax newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                            invocation,
                            propertyName!,
                            conversionExpression);
                        return ReplaceNodeAsync(context.Document, operationContext.Root, invocation, newInvocation);
                    },
                    $"AM001_MapWithConversion_{propertyName}"),
                diagnostic);
        }

        // Always provide ignore option as a safe fallback.
        context.RegisterCodeFix(
            CodeAction.Create(
                $"Ignore property '{propertyName}'",
                cancellationToken =>
                {
                    InvocationExpressionSyntax newInvocation =
                        CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName!);
                    return ReplaceNodeAsync(context.Document, operationContext.Root, invocation, newInvocation);
                },
                $"AM001_Ignore_{propertyName}"),
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

    private static string? CreateConversionExpression(ITypeSymbol sourceType, ITypeSymbol destinationType,
        string propertyName)
    {
        if (SymbolEqualityComparer.Default.Equals(sourceType, destinationType))
        {
            return null;
        }

        string destinationTypeName = destinationType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        // Numeric conversions: add cast
        if (IsNumericConversion(sourceType.SpecialType) && IsNumericConversion(destinationType.SpecialType))
        {
            return $"({destinationTypeName})src.{propertyName}";
        }

        // String -> primitive: use parse pattern inside MapFrom
        if (IsString(sourceType) && IsNumericConversion(destinationType.SpecialType))
        {
            return
                $"src.{propertyName} != null ? {destinationTypeName}.Parse(src.{propertyName}) : {TypeConversionHelper.GetDefaultValueForType(destinationTypeName)}";
        }

        // Primitive -> string: use ToString with invariant culture for numeric types
        if (IsNumericConversion(sourceType.SpecialType) && IsString(destinationType))
        {
            return $"src.{propertyName}.ToString()";
        }

        // Nullable source to non-nullable destination where underlying types are compatible.
        ITypeSymbol sourceUnderlyingType = AutoMapperAnalysisHelpers.GetUnderlyingType(sourceType);
        if (IsNullableType(sourceType) &&
            !IsNullableType(destinationType) &&
            AutoMapperAnalysisHelpers.AreTypesCompatible(sourceUnderlyingType, destinationType))
        {
            string fallback = TypeConversionHelper.GetDefaultValueForType(destinationTypeName);
            if (IsNumericConversion(sourceUnderlyingType.SpecialType) &&
                IsNumericConversion(destinationType.SpecialType) &&
                !SymbolEqualityComparer.Default.Equals(sourceUnderlyingType, destinationType))
            {
                return $"({destinationTypeName})(src.{propertyName} ?? {fallback})";
            }

            return $"src.{propertyName} ?? {fallback}";
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

    private static bool IsNullableType(ITypeSymbol type)
    {
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }

        return type is INamedTypeSymbol namedType &&
               namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
    }

    private string? ExtractPropertyNameFromDiagnostic(Diagnostic diagnostic)
    {
        // Try to get property name from diagnostic properties
        if (diagnostic.Properties.TryGetValue(PropertyNamePropertyName, out string? propertyName))
        {
            return propertyName;
        }

        // Fallback: extract from diagnostic message
        string message = diagnostic.GetMessage();
        Match match = Regex.Match(message, @"Property '(\w+)'");
        return match.Success ? match.Groups[1].Value : null;
    }

    private (string? sourceType, string? destType) ExtractTypesFromDiagnostic(Diagnostic diagnostic)
    {
        // Try to get from diagnostic properties first
        string? sourceType =
            diagnostic.Properties.TryGetValue(SourcePropertyTypePropertyName, out string? st) ? st : null;
        string? destType =
            diagnostic.Properties.TryGetValue(DestinationPropertyTypePropertyName, out string? dt) ? dt : null;

        if (!string.IsNullOrEmpty(sourceType) && !string.IsNullOrEmpty(destType))
        {
            return (sourceType, destType);
        }

        // Fallback: extract from diagnostic message
        string message = diagnostic.GetMessage();
        Match match = Regex.Match(message, @"from '(\w+)' to '(\w+)'");
        if (match.Success && match.Groups.Count >= 3)
        {
            return (match.Groups[1].Value, match.Groups[2].Value);
        }

        return (null, null);
    }
}

using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.TypeSafety;

/// <summary>
///     Analyzer for detecting nullable reference type compatibility issues in AutoMapper configurations.
///     Inherits from AutoMapperAnalyzerBase for common functionality.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM002_NullableCompatibilityAnalyzer : AutoMapperAnalyzerBase
{
    /// <summary>
    ///     AM002: Nullable to non-nullable assignment without proper handling.
    /// </summary>
    public static readonly DiagnosticDescriptor NullableToNonNullableRule = new(
        "AM002",
        "Nullable to non-nullable mapping issue in AutoMapper configuration",
        "Property '{0}' has nullable compatibility issue: {1}.{0} ({2}) can be null but {3}.{0} ({4}) is non-nullable",
        AutoMapperConstants.CategoryTypeSafety,
        DiagnosticSeverity.Error,
        true,
        "Source property is nullable but destination property is non-nullable, which could cause null reference exceptions at runtime.");

    /// <summary>
    ///     AM002: Non-nullable to nullable assignment (informational).
    /// </summary>
    public static readonly DiagnosticDescriptor NonNullableToNullableRule = new(
        "AM002",
        "Non-nullable to nullable mapping in AutoMapper configuration",
        "Property '{0}' mapping: {1}.{0} ({2}) is non-nullable but {3}.{0} ({4}) is nullable",
        AutoMapperConstants.CategoryTypeSafety,
        DiagnosticSeverity.Info,
        true,
        "Non-nullable source property is being mapped to nullable destination property.");

    /// <summary>
    ///     Gets the supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [NullableToNonNullableRule, NonNullableToNullableRule];

    /// <summary>
    ///     Analyzes a CreateMap invocation for nullable compatibility issues.
    /// </summary>
    protected override void AnalyzeCreateMapInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        var sourceProperties = GetMappableProperties(sourceType, requireSetter: false);
        var destinationProperties = GetMappableProperties(destinationType, requireGetter: false);

        foreach (var sourceProperty in sourceProperties)
        {
            var destinationProperty = destinationProperties
                .FirstOrDefault(p => StringUtilities.EqualsIgnoreCase(p.Name, sourceProperty.Name));

            if (destinationProperty != null)
            {
                // Check for explicit property mapping that might handle nullability
                if (IsPropertyConfigured(invocation, sourceProperty.Name, context.SemanticModel))
                {
                    continue;
                }

                AnalyzeNullableCompatibility(
                    context,
                    invocation,
                    sourceProperty,
                    destinationProperty,
                    sourceType,
                    destinationType);
            }
        }
    }

    private void AnalyzeNullableCompatibility(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IPropertySymbol sourceProperty,
        IPropertySymbol destinationProperty,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        string sourceTypeName = sourceProperty.Type.ToDisplayString();
        string destTypeName = destinationProperty.Type.ToDisplayString();

        // Case 1: Nullable source -> Non-nullable destination (WARNING)
        if (IsNullableType(sourceProperty.Type) && !IsNullableType(destinationProperty.Type))
        {
            var sourceUnderlyingType = GetUnderlyingTypeInternal(sourceProperty.Type);
            var destUnderlyingType = GetUnderlyingTypeInternal(destinationProperty.Type);

            if (AreTypesCompatible(sourceUnderlyingType, destUnderlyingType))
            {
                var diagnostic = CreateDiagnostic(
                    NullableToNonNullableRule,
                    invocation.GetLocation(),
                    sourceProperty.Name,
                    GetTypeName(sourceType),
                    sourceTypeName,
                    GetTypeName(destinationType),
                    destTypeName);
                context.ReportDiagnostic(diagnostic);
            }
        }
        // Case 2: Non-nullable source -> Nullable destination (INFO)
        else if (!IsNullableType(sourceProperty.Type) && IsNullableType(destinationProperty.Type))
        {
            var sourceUnderlyingType = GetUnderlyingTypeInternal(sourceProperty.Type);
            var destUnderlyingType = GetUnderlyingTypeInternal(destinationProperty.Type);

            if (AreTypesCompatible(sourceUnderlyingType, destUnderlyingType))
            {
                var diagnostic = CreateDiagnostic(
                    NonNullableToNullableRule,
                    invocation.GetLocation(),
                    sourceProperty.Name,
                    GetTypeName(sourceType),
                    sourceTypeName,
                    GetTypeName(destinationType),
                    destTypeName);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool IsNullableType(ITypeSymbol type)
    {
        // Check for nullable reference types (string?, object?, etc.)
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }

        // Check for nullable value types (int?, DateTime?, etc.)
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return true;
        }

        // Check by string representation for cases where annotation might not be detected
        string typeString = type.ToDisplayString();
        return typeString.EndsWith("?");
    }

    private static ITypeSymbol GetUnderlyingTypeInternal(ITypeSymbol type)
    {
        // For nullable value types (int?, DateTime?), get the underlying type
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            type is INamedTypeSymbol namedType && namedType.TypeArguments.Length == 1)
        {
            return namedType.TypeArguments[0];
        }

        // For nullable reference types (string?, object?), the type itself is the underlying type
        return type;
    }
}

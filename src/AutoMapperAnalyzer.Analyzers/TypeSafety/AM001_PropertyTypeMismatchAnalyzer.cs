using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.TypeSafety;

/// <summary>
///     Analyzer that detects type mismatches between source and destination properties in AutoMapper mappings.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM001_PropertyTypeMismatchAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     Diagnostic rule for property type mismatches.
    /// </summary>
    public static readonly DiagnosticDescriptor PropertyTypeMismatchRule = new(
        "AM001",
        "Property type mismatch in AutoMapper configuration",
        "Property '{0}' has incompatible types: {1}.{0} ({2}) cannot be mapped to {3}.{0} ({4}) without explicit conversion",
        "AutoMapper.TypeSafety",
        DiagnosticSeverity.Error,
        true,
        "Source and destination properties have incompatible types that require explicit conversion configuration."
    );

    /// <summary>
    ///     Gets the supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        PropertyTypeMismatchRule
    ];

    /// <summary>
    ///     Initializes the analyzer.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCreateMapInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeCreateMapInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;

        // Check if this is a CreateMap<TSource, TDestination>() call
        if (!AutoMapperAnalysisHelpers.IsCreateMapInvocation(invocationExpr, context.SemanticModel))
        {
            return;
        }

        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (sourceType == null || destinationType == null)
        {
            return;
        }

        // Analyze property mappings between source and destination types
        AnalyzePropertyMappings(
            context,
            invocationExpr,
            sourceType,
            destinationType
        );
    }

    private static void AnalyzePropertyMappings(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        IPropertySymbol[] sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false).ToArray();
        IPropertySymbol[] destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, false).ToArray();

        // Check each source property for mapping compatibility
        foreach (IPropertySymbol sourceProp in sourceProperties)
        {
            IPropertySymbol? destProp = destinationProperties.FirstOrDefault(p =>
                string.Equals(p.Name, sourceProp.Name, StringComparison.OrdinalIgnoreCase));

            if (destProp == null)
            {
                continue; // Missing property will be handled by AM010
            }

            // Check if explicit mapping is configured for this property
            if (AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember(invocation, sourceProp.Name,
                    context.SemanticModel))
            {
                continue;
            }

            AnalyzePropertyTypeCompatibility(
                context,
                invocation,
                sourceProp,
                destProp,
                sourceType,
                destinationType
            );
        }
    }

    private static void AnalyzePropertyTypeCompatibility(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IPropertySymbol sourceProperty,
        IPropertySymbol destinationProperty,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        string sourceTypeName = sourceProperty.Type.ToDisplayString();
        string destTypeName = destinationProperty.Type.ToDisplayString();

        // Check for exact type match
        if (SymbolEqualityComparer.Default.Equals(sourceProperty.Type, destinationProperty.Type))
        {
            return;
        }

        // Check for nullable compatibility issues (Handled by AM002)
        if (IsNullableCompatibilityIssue(sourceProperty.Type, destinationProperty.Type))
        {
            return;
        }

        // Check for generic type mismatches (Handled by AM021 for collections)
        if (IsGenericTypeMismatch(sourceProperty.Type, destinationProperty.Type))
        {
            return;
        }

        // Check for complex type mapping requirements (Handled by AM020)
        if (IsComplexTypeMappingRequired(sourceProperty.Type, destinationProperty.Type))
        {
            return;
        }

        // Check for basic type incompatibilities
        if (!AreTypesCompatible(sourceProperty.Type, destinationProperty.Type))
        {
            var diagnostic = Diagnostic.Create(
                PropertyTypeMismatchRule,
                invocation.GetLocation(),
                sourceProperty.Name,
                GetTypeName(sourceType),
                sourceTypeName,
                GetTypeName(destinationType),
                destTypeName
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsNullableCompatibilityIssue(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        // Check if source is nullable reference type and destination is non-nullable
        return sourceType is { CanBeReferencedByName: true, NullableAnnotation: NullableAnnotation.Annotated } &&
               destinationType.NullableAnnotation == NullableAnnotation.NotAnnotated;
    }

    private static bool IsGenericTypeMismatch(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        if (sourceType is INamedTypeSymbol sourceNamed && destinationType is INamedTypeSymbol destNamed)
        {
            // Both are generic types with same generic definition but different type arguments
            if (sourceNamed.IsGenericType && destNamed.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(sourceNamed.OriginalDefinition, destNamed.OriginalDefinition))
            {
                // Check if type arguments are different
                for (int i = 0; i < sourceNamed.TypeArguments.Length; i++)
                {
                    if (!SymbolEqualityComparer.Default.Equals(
                            sourceNamed.TypeArguments[i],
                            destNamed.TypeArguments[i]))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsComplexTypeMappingRequired(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        // Both are named types (classes/structs) but not the same type
        if (sourceType is INamedTypeSymbol sourceNamed && destinationType is INamedTypeSymbol destNamed)
        {
            // Skip primitive types and common framework types
            if (IsPrimitiveOrFrameworkType(sourceType) || IsPrimitiveOrFrameworkType(destinationType))
            {
                return false;
            }

            // Different named types that aren't generic collections
            return !SymbolEqualityComparer.Default.Equals(sourceType, destinationType) &&
                   sourceNamed.TypeKind == TypeKind.Class &&
                   destNamed.TypeKind == TypeKind.Class;
        }

        return false;
    }

    private static bool HasExistingCreateMapForTypes(
        SyntaxNodeAnalysisContext context,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        return AutoMapperAnalysisHelpers.HasExistingCreateMapForTypes(
            context.Compilation,
            sourceType,
            destinationType);
    }

    private static bool AreTypesCompatible(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        // Use the centralized helper method for comprehensive compatibility checking.
        // This includes: exact type match, nullable compatibility, collection compatibility,
        // and numeric type implicit conversions (using SpecialType for accuracy).
        return AutoMapperAnalysisHelpers.AreTypesCompatible(sourceType, destinationType);
    }

    private static bool IsPrimitiveOrFrameworkType(ITypeSymbol type)
    {
        string typeName = type.ToDisplayString();
        string[] primitiveAndFrameworkTypes =
        [
            "string", "int", "long", "short", "byte", "bool", "char", "float", "double", "decimal",
            "System.DateTime", "System.DateTimeOffset", "System.TimeSpan", "System.Guid"
        ];

        return primitiveAndFrameworkTypes.Contains(typeName);
    }

    private static string GetTypeName(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType)
        {
            return namedType.Name;
        }

        return type.Name;
    }
}

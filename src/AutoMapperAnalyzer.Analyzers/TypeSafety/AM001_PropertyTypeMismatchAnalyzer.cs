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
    internal const string PropertyNamePropertyName = "PropertyName";
    internal const string SourcePropertyTypePropertyName = "SourcePropertyType";
    internal const string DestinationPropertyTypePropertyName = "DestinationPropertyType";

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

        // Ensure this invocation resolves to AutoMapper APIs to avoid lookalike false positives.
        if (!IsAutoMapperCreateMapInvocation(invocationExpr, context.SemanticModel))
        {
            return;
        }

        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (sourceType == null || destinationType == null)
        {
            return;
        }

        var reportedMismatches = new HashSet<string>(StringComparer.Ordinal);

        // Analyze property mappings between source and destination types
        AnalyzePropertyMappings(
            context,
            invocationExpr,
            sourceType,
            destinationType,
            reportedMismatches
        );

        InvocationExpressionSyntax? reverseMapInvocation =
            AutoMapperAnalysisHelpers.GetReverseMapInvocation(invocationExpr);
        if (reverseMapInvocation != null)
        {
            AnalyzePropertyMappings(
                context,
                reverseMapInvocation,
                destinationType,
                sourceType,
                reportedMismatches
            );
        }
    }

    private static void AnalyzePropertyMappings(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        HashSet<string> reportedMismatches)
    {
        if (AM020MappingConfigurationHelpers.HasCustomConstructionOrConversion(invocation, context.SemanticModel))
        {
            return;
        }

        IPropertySymbol[] sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false).ToArray();
        IPropertySymbol[] destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(
                destinationType,
                requireGetter: false,
                requireSetter: true).ToArray();

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
            if (AM020MappingConfigurationHelpers.IsDestinationPropertyExplicitlyConfigured(
                    invocation,
                    destProp.Name,
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
                destinationType,
                reportedMismatches
            );
        }
    }

    private static void AnalyzePropertyTypeCompatibility(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IPropertySymbol sourceProperty,
        IPropertySymbol destinationProperty,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        HashSet<string> reportedMismatches)
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
        if (!AreTypesCompatible(context.Compilation, sourceProperty.Type, destinationProperty.Type))
        {
            string mismatchKey = CreateMismatchKey(
                sourceProperty.Name,
                destinationProperty.Name,
                sourceProperty.Type,
                destinationProperty.Type);
            if (!reportedMismatches.Add(mismatchKey))
            {
                return;
            }

            var properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add(PropertyNamePropertyName, sourceProperty.Name);
            properties.Add(SourcePropertyTypePropertyName, sourceTypeName);
            properties.Add(DestinationPropertyTypePropertyName, destTypeName);

            var diagnostic = Diagnostic.Create(
                PropertyTypeMismatchRule,
                invocation.GetLocation(),
                properties.ToImmutable(),
                sourceProperty.Name,
                AutoMapperAnalysisHelpers.GetTypeName(sourceType),
                sourceTypeName,
                AutoMapperAnalysisHelpers.GetTypeName(destinationType),
                destTypeName
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static string CreateMismatchKey(
        string sourcePropertyName,
        string destinationPropertyName,
        ITypeSymbol sourcePropertyType,
        ITypeSymbol destinationPropertyType)
    {
        // Preserve direction so ReverseMap can report both sides of a bidirectional
        // mismatch (e.g. string→int forward and int→string reverse need different fixes).
        return string.Concat(
            sourcePropertyName,
            "→",
            destinationPropertyName,
            "::",
            sourcePropertyType.ToDisplayString(),
            "→",
            destinationPropertyType.ToDisplayString());
    }

    private static bool IsNullableCompatibilityIssue(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        if (!IsNullableType(sourceType) || IsNullableType(destinationType))
        {
            return false;
        }

        // Only suppress AM001 when the underlying types are otherwise compatible.
        // This leaves purely nullability concerns to AM002 but still reports true type mismatches.
        ITypeSymbol sourceUnderlyingType = AutoMapperAnalysisHelpers.GetUnderlyingType(sourceType);
        ITypeSymbol destinationUnderlyingType = AutoMapperAnalysisHelpers.GetUnderlyingType(destinationType);
        return AutoMapperAnalysisHelpers.AreTypesCompatible(sourceUnderlyingType, destinationUnderlyingType);
    }

    private static bool IsGenericTypeMismatch(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        // AM021 owns same-open-generic collection element mismatches (List<int>→List<string>).
        // Do NOT suppress Nullable<T> or other non-collection generics here — those remain AM001.
        if (!AutoMapperAnalysisHelpers.IsCollectionType(sourceType) ||
            !AutoMapperAnalysisHelpers.IsCollectionType(destinationType))
        {
            return false;
        }

        if (sourceType is INamedTypeSymbol sourceNamed && destinationType is INamedTypeSymbol destNamed)
        {
            // Both are generic collections with same generic definition but different type arguments
            if (sourceNamed.IsGenericType && destNamed.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(sourceNamed.OriginalDefinition, destNamed.OriginalDefinition))
            {
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

    private static bool IsNullableType(ITypeSymbol type)
    {
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }

        return type is INamedTypeSymbol namedType &&
               namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
    }

    private static bool IsComplexTypeMappingRequired(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        // Peel Nullable<T> so int?→long? stays a scalar AM001 concern (not AM020 nested mapping).
        sourceType = AutoMapperAnalysisHelpers.GetUnderlyingType(sourceType);
        destinationType = AutoMapperAnalysisHelpers.GetUnderlyingType(destinationType);

        // Both are named types (classes/structs/interfaces) but not the same type
        if (sourceType is INamedTypeSymbol sourceNamed && destinationType is INamedTypeSymbol destNamed)
        {
            // Skip primitive types and common framework types
            if (IsPrimitiveOrFrameworkType(sourceType) || IsPrimitiveOrFrameworkType(destinationType))
            {
                return false;
            }

            // Different named types that aren't generic collections
            return !SymbolEqualityComparer.Default.Equals(sourceType, destinationType) &&
                   (sourceNamed.TypeKind == TypeKind.Class || sourceNamed.TypeKind == TypeKind.Struct || sourceNamed.TypeKind == TypeKind.Interface) &&
                   (destNamed.TypeKind == TypeKind.Class || destNamed.TypeKind == TypeKind.Struct || destNamed.TypeKind == TypeKind.Interface);
        }

        return false;
    }

    private static bool AreTypesCompatible(Compilation compilation, ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        if (AutoMapperAnalysisHelpers.AreTypesCompatible(sourceType, destinationType))
        {
            return true;
        }

        Conversion conversion = compilation.ClassifyConversion(sourceType, destinationType);
        return conversion.Exists && conversion.IsImplicit;
    }

    private static bool IsPrimitiveOrFrameworkType(ITypeSymbol type)
    {
        return AutoMapperAnalysisHelpers.IsBuiltInType(type);
    }


    private static bool IsAutoMapperCreateMapInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        return MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "CreateMap");
    }


}

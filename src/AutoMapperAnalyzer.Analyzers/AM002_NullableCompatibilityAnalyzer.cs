using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
/// Analyzer for detecting nullable reference type compatibility issues in AutoMapper configurations.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM002_NullableCompatibilityAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// AM002: Nullable to non-nullable assignment without proper handling.
    /// </summary>
    public static readonly DiagnosticDescriptor NullableToNonNullableRule = new(
        "AM002",
        "Nullable to non-nullable mapping issue in AutoMapper configuration",
        "Property '{0}' has nullable compatibility issue: {1}.{0} ({2}) can be null but {3}.{0} ({4}) is non-nullable",
        "AutoMapper.NullSafety",
        DiagnosticSeverity.Error,
        true,
        "Source property is nullable but destination property is non-nullable, which could cause null reference exceptions at runtime.");

    /// <summary>
    /// AM002: Non-nullable to nullable assignment (informational).
    /// </summary>
    public static readonly DiagnosticDescriptor NonNullableToNullableRule = new(
        "AM002",
        "Non-nullable to nullable mapping in AutoMapper configuration",
        "Property '{0}' mapping: {1}.{0} ({2}) is non-nullable but {3}.{0} ({4}) is nullable",
        "AutoMapper.NullSafety",
        DiagnosticSeverity.Info,
        true,
        "Non-nullable source property is being mapped to nullable destination property.");

    /// <summary>
    /// Gets the supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [NullableToNonNullableRule, NonNullableToNullableRule];

    /// <summary>
    /// Initializes the analyzer.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
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

        var (sourceType, destinationType) = AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (sourceType == null || destinationType == null)
        {
            return;
        }

        // Analyze nullable compatibility for property mappings
        AnalyzeNullablePropertyMappings(
            context,
            invocationExpr,
            sourceType,
            destinationType
        );
    }

    private static void AnalyzeNullablePropertyMappings(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false).ToArray();
        var destinationProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, requireGetter: false).ToArray();

        foreach (IPropertySymbol sourceProperty in sourceProperties)
        {
            IPropertySymbol? destinationProperty = destinationProperties
                .FirstOrDefault(p => p.Name == sourceProperty.Name);

            if (destinationProperty != null)
            {
                // Check for explicit property mapping that might handle nullability
                if (AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember(invocation, sourceProperty.Name, context.SemanticModel))
                {
                    continue;
                }

                AnalyzeNullableCompatibility(
                    context,
                    invocation,
                    sourceProperty,
                    destinationProperty,
                    sourceType,
                    destinationType
                );
            }
        }
    }

    private static void AnalyzeNullableCompatibility(
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
            // Check if the underlying types are compatible
            ITypeSymbol sourceUnderlyingType = GetUnderlyingType(sourceProperty.Type);
            ITypeSymbol destUnderlyingType = GetUnderlyingType(destinationProperty.Type);

            if (AreUnderlyingTypesCompatible(sourceUnderlyingType, destUnderlyingType))
            {
                var diagnostic = Diagnostic.Create(
                    NullableToNonNullableRule,
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
        // Case 2: Non-nullable source -> Nullable destination (INFO)
        else if (!IsNullableType(sourceProperty.Type) && IsNullableType(destinationProperty.Type))
        {
            ITypeSymbol sourceUnderlyingType = GetUnderlyingType(sourceProperty.Type);
            ITypeSymbol destUnderlyingType = GetUnderlyingType(destinationProperty.Type);

            if (AreUnderlyingTypesCompatible(sourceUnderlyingType, destUnderlyingType))
            {
                var diagnostic = Diagnostic.Create(
                    NonNullableToNullableRule,
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

    private static ITypeSymbol GetUnderlyingType(ITypeSymbol type)
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

    private static bool AreUnderlyingTypesCompatible(ITypeSymbol sourceType, ITypeSymbol destType)
    {
        // Use helper for comprehensive compatibility checking
        return AutoMapperAnalysisHelpers.AreTypesCompatible(sourceType, destType);
    }

    private static string GetTypeName(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType)
            return namedType.Name;
        return type.Name;
    }
}

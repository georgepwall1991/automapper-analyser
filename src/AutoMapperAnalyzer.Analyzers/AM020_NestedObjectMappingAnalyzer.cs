using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
///     Analyzer for detecting nested object mapping issues where complex types require explicit mapping configuration
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM020_NestedObjectMappingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     AM020: Nested object mapping missing - requires explicit mapping configuration
    /// </summary>
    public static readonly DiagnosticDescriptor NestedObjectMappingMissingRule = new(
        "AM020",
        "Nested object mapping configuration missing",
        "Property '{0}' requires mapping configuration between '{1}' and '{2}'. Consider adding CreateMap<{1}, {2}>() or explicit ForMember configuration",
        "AutoMapper.NestedObjects",
        DiagnosticSeverity.Warning,
        true,
        "Complex nested objects require explicit mapping configuration to ensure proper data transformation and avoid runtime exceptions.");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [NestedObjectMappingMissingRule];

    /// <inheritdoc/>
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

        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) typeArguments =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (typeArguments.sourceType == null || typeArguments.destinationType == null)
        {
            return;
        }

        // Analyze nested object mapping requirements
        AnalyzeNestedObjectMappingRequirements(
            context,
            invocationExpr,
            typeArguments.sourceType,
            typeArguments.destinationType
        );
    }


    private static void AnalyzeNestedObjectMappingRequirements(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        var destinationProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, requireGetter: false);

        var createMapRegistry = CreateMapRegistry.FromCompilation(context.Compilation);

        // Check each property pair for nested object mapping requirements
        foreach (IPropertySymbol sourceProperty in sourceProperties)
        {
            // Find corresponding destination property
            IPropertySymbol? destinationProperty = destinationProperties
                .FirstOrDefault(p => string.Equals(p.Name, sourceProperty.Name, StringComparison.OrdinalIgnoreCase));

            if (destinationProperty == null)
            {
                continue; // No corresponding property, handled by other analyzers
            }

            // Check if this property requires nested object mapping
            if (RequiresNestedObjectMapping(sourceProperty.Type, destinationProperty.Type))
            {
                var sourceNestedType = AutoMapperAnalysisHelpers.GetUnderlyingType(sourceProperty.Type);
                var destNestedType = AutoMapperAnalysisHelpers.GetUnderlyingType(destinationProperty.Type);

                // Check if mapping already exists
                if (createMapRegistry.Contains(sourceNestedType, destNestedType))
                {
                    continue; // Mapping already configured
                }

                // Check if property is explicitly mapped via ForMember
                if (AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember(invocation, destinationProperty.Name, context.SemanticModel))
                {
                    continue; // Property is explicitly handled
                }

                // Report diagnostic for missing nested object mapping
                var diagnostic = Diagnostic.Create(
                    NestedObjectMappingMissingRule,
                    invocation.GetLocation(),
                    sourceProperty.Name,
                    AutoMapperAnalysisHelpers.GetTypeNameWithoutNullability(sourceProperty.Type),
                    AutoMapperAnalysisHelpers.GetTypeNameWithoutNullability(destinationProperty.Type));

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool RequiresNestedObjectMapping(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        // Get the underlying types (remove nullable wrappers)
        ITypeSymbol sourceUnderlyingType = AutoMapperAnalysisHelpers.GetUnderlyingType(sourceType);
        ITypeSymbol destUnderlyingType = AutoMapperAnalysisHelpers.GetUnderlyingType(destinationType);

        // Same types don't need mapping
        if (SymbolEqualityComparer.Default.Equals(sourceUnderlyingType, destUnderlyingType))
        {
            return false;
        }

        // Built-in value types and strings don't need nested object mapping
        if (AutoMapperAnalysisHelpers.IsBuiltInType(sourceUnderlyingType) || AutoMapperAnalysisHelpers.IsBuiltInType(destUnderlyingType))
        {
            return false;
        }

        // Collections should be handled by AM021, not AM020
        if (AutoMapperAnalysisHelpers.IsCollectionType(sourceUnderlyingType) || AutoMapperAnalysisHelpers.IsCollectionType(destUnderlyingType))
        {
            return false;
        }

        // Both must be reference types (classes) that are different
        return sourceUnderlyingType.TypeKind == TypeKind.Class &&
               destUnderlyingType.TypeKind == TypeKind.Class;
    }
}

using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.ComplexMappings;

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
        "Property '{0}' requires mapping configuration between '{1}' and '{2}'. Consider adding CreateMap<{1}, {2}>() or explicit ForMember configuration.",
        "AutoMapper.NestedObjects",
        DiagnosticSeverity.Warning,
        true,
        "Complex nested objects require explicit mapping configuration to ensure proper data transformation and avoid runtime exceptions.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [NestedObjectMappingMissingRule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCreateMapInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeCreateMapInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;

        // Ensure strict AutoMapper semantic matching to avoid lookalike false positives.
        if (!MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocationExpr, context.SemanticModel, "CreateMap"))
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
        InvocationExpressionSyntax? reverseMapInvocation =
            AutoMapperAnalysisHelpers.GetReverseMapInvocation(invocation);
        if (AM020MappingConfigurationHelpers.HasCustomConstructionOrConversion(invocation, reverseMapInvocation))
        {
            return;
        }

        IEnumerable<IPropertySymbol> sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        IEnumerable<IPropertySymbol> destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, false);

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
                ITypeSymbol sourceNestedType = AutoMapperAnalysisHelpers.GetUnderlyingType(sourceProperty.Type);
                ITypeSymbol destNestedType = AutoMapperAnalysisHelpers.GetUnderlyingType(destinationProperty.Type);

                // Check if mapping already exists
                if (createMapRegistry.Contains(sourceNestedType, destNestedType))
                {
                    continue; // Mapping already configured
                }

                // Check if property is explicitly mapped via ForMember/ForPath in forward direction
                if (AM020MappingConfigurationHelpers.IsDestinationPropertyExplicitlyConfigured(
                        invocation,
                        destinationProperty.Name,
                        reverseMapInvocation))
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
        if (AutoMapperAnalysisHelpers.IsBuiltInType(sourceUnderlyingType) ||
            AutoMapperAnalysisHelpers.IsBuiltInType(destUnderlyingType))
        {
            return false;
        }

        // Collections should be handled by AM021, not AM020
        if (AutoMapperAnalysisHelpers.IsCollectionType(sourceUnderlyingType) ||
            AutoMapperAnalysisHelpers.IsCollectionType(destUnderlyingType))
        {
            return false;
        }

        // Both must be reference types (classes) that are different
        return sourceUnderlyingType.TypeKind == TypeKind.Class &&
               destUnderlyingType.TypeKind == TypeKind.Class;
    }
}

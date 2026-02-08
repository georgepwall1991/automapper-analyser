using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.ComplexMappings;

/// <summary>
///     Analyzer for detecting collection element type mismatch issues in AutoMapper configurations
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM021_CollectionElementMismatchAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     AM021: Collection element type incompatibility
    /// </summary>
    public static readonly DiagnosticDescriptor CollectionElementIncompatibilityRule = new(
        "AM021",
        "Collection element type incompatibility in AutoMapper configuration",
        "Property '{0}' has incompatible collection element types: {1}.{0} ({2}) elements cannot be mapped to {3}.{0} ({4}) elements without explicit conversion",
        "AutoMapper.Collections",
        DiagnosticSeverity.Warning,
        true,
        "Collection properties have compatible collection types but incompatible element types that may require custom mapping.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(CollectionElementIncompatibilityRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocationExpr)
        {
            return;
        }

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

        // Analyze collection element compatibility for property mappings
        AnalyzeCollectionElementCompatibility(context, invocationExpr, typeArguments.sourceType,
            typeArguments.destinationType);
    }

    private static void AnalyzeCollectionElementCompatibility(SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation, ITypeSymbol sourceType, ITypeSymbol destinationType)
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

        foreach (IPropertySymbol sourceProperty in sourceProperties)
        {
            IPropertySymbol? destinationProperty = destinationProperties
                .FirstOrDefault(p => p.Name == sourceProperty.Name);

            if (destinationProperty == null)
            {
                continue;
            }

            // Check for explicit property mapping that might handle collection conversion
            if (AM020MappingConfigurationHelpers.IsDestinationPropertyExplicitlyConfigured(
                    invocation,
                    destinationProperty.Name,
                    reverseMapInvocation))
            {
                continue;
            }

            // Check if both properties are collections and analyze element types
            if (AutoMapperAnalysisHelpers.IsCollectionType(sourceProperty.Type) &&
                AutoMapperAnalysisHelpers.IsCollectionType(destinationProperty.Type))
            {
                AnalyzeCollectionElementTypes(context, invocation, sourceProperty, destinationProperty,
                    sourceType, destinationType);
            }
        }
    }

    private static void AnalyzeCollectionElementTypes(SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IPropertySymbol sourceProperty,
        IPropertySymbol destinationProperty,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        // Get element types from collections
        ITypeSymbol? sourceElementType = AutoMapperAnalysisHelpers.GetCollectionElementType(sourceProperty.Type);
        ITypeSymbol? destElementType = AutoMapperAnalysisHelpers.GetCollectionElementType(destinationProperty.Type);

        if (sourceElementType == null || destElementType == null)
        {
            return;
        }

        // Check if element types are compatible
        if (!AutoMapperAnalysisHelpers.AreTypesCompatible(sourceElementType, destElementType))
        {
            // Check if there's an explicit CreateMap for the element types
            var registry = CreateMapRegistry.FromCompilation(context.Compilation);
            if (registry.Contains(sourceElementType, destElementType))
            {
                // Element mapping exists, so AutoMapper can handle this collection mapping
                return;
            }

            ImmutableDictionary<string, string?>.Builder properties =
                ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("PropertyName", sourceProperty.Name);
            properties.Add("SourceElementType", sourceElementType.ToDisplayString());
            properties.Add("DestElementType", destElementType.ToDisplayString());

            var diagnostic = Diagnostic.Create(
                CollectionElementIncompatibilityRule,
                invocation.GetLocation(),
                properties.ToImmutable(),
                sourceProperty.Name,
                AutoMapperAnalysisHelpers.GetTypeName(sourceType),
                sourceElementType.ToDisplayString(),
                AutoMapperAnalysisHelpers.GetTypeName(destinationType),
                destElementType.ToDisplayString());

            context.ReportDiagnostic(diagnostic);
        }
    }

}

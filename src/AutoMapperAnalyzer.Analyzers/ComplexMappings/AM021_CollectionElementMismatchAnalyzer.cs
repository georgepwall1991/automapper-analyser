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

        // AM003 owns collection container incompatibilities.
        if (AreCollectionTypesIncompatible(sourceProperty.Type, destinationProperty.Type))
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

    private static bool AreCollectionTypesIncompatible(ITypeSymbol sourceType, ITypeSymbol destType)
    {
        bool sourceIsArray = sourceType.TypeKind == TypeKind.Array;
        bool destIsArray = destType.TypeKind == TypeKind.Array;
        if (sourceIsArray && destIsArray)
        {
            return false;
        }

        bool sourceIsHashSet = IsConstructedFromType(sourceType, "System.Collections.Generic.HashSet<T>");
        bool destIsHashSet = IsConstructedFromType(destType, "System.Collections.Generic.HashSet<T>");
        bool sourceIsQueue = IsConstructedFromType(sourceType, "System.Collections.Generic.Queue<T>");
        bool destIsQueue = IsConstructedFromType(destType, "System.Collections.Generic.Queue<T>");
        bool sourceIsStack = IsConstructedFromType(sourceType, "System.Collections.Generic.Stack<T>");
        bool destIsStack = IsConstructedFromType(destType, "System.Collections.Generic.Stack<T>");

        if (sourceIsHashSet && !destIsHashSet)
        {
            return true;
        }

        if (!sourceIsHashSet && destIsHashSet)
        {
            return true;
        }

        if (sourceIsQueue && !destIsQueue)
        {
            return true;
        }

        if (!sourceIsQueue && destIsQueue)
        {
            return true;
        }

        if (sourceIsStack && !destIsStack)
        {
            return true;
        }

        if (!sourceIsStack && destIsStack)
        {
            return true;
        }

        if (!IsGenericCollection(sourceType) && IsGenericCollection(destType))
        {
            return true;
        }

        return false;
    }

    private static bool IsGenericCollection(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { IsGenericType: true } namedType &&
               namedType.TypeArguments.Length > 0;
    }

    private static bool IsConstructedFromType(ITypeSymbol type, string genericDefinitionName)
    {
        return type is INamedTypeSymbol namedType &&
               namedType.OriginalDefinition.ToDisplayString() == genericDefinitionName;
    }
}

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
        "Property '{0}' has incompatible collection element types: {1}.{0} ({2}) elements cannot be mapped to {3}.{4} ({5}) elements without explicit conversion",
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

        // Analyze collection element compatibility for property mappings.
        HashSet<string> forwardDiagnostics = AnalyzeCollectionElementCompatibility(
            context,
            invocationExpr,
            typeArguments.sourceType,
            typeArguments.destinationType);

        InvocationExpressionSyntax? reverseMapInvocation =
            AutoMapperAnalysisHelpers.GetReverseMapInvocation(invocationExpr);
        if (reverseMapInvocation != null)
        {
            AnalyzeCollectionElementCompatibility(
                context,
                reverseMapInvocation,
                typeArguments.destinationType,
                typeArguments.sourceType,
                forwardDiagnostics);
        }
    }

    private static HashSet<string> AnalyzeCollectionElementCompatibility(SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation, ITypeSymbol sourceType, ITypeSymbol destinationType,
        ISet<string>? suppressedCollectionProperties = null)
    {
        var reportedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (AM020MappingConfigurationHelpers.HasCustomConstructionOrConversion(invocation, context.SemanticModel))
        {
            return reportedProperties;
        }

        IEnumerable<IPropertySymbol> sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        IEnumerable<IPropertySymbol> destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, false);

        foreach (IPropertySymbol sourceProperty in sourceProperties)
        {
            IPropertySymbol? destinationProperty = destinationProperties
                .FirstOrDefault(p => string.Equals(p.Name, sourceProperty.Name, StringComparison.OrdinalIgnoreCase));

            if (destinationProperty == null)
            {
                continue;
            }

            if (suppressedCollectionProperties?.Contains(sourceProperty.Name) == true ||
                suppressedCollectionProperties?.Contains(destinationProperty.Name) == true)
            {
                continue;
            }

            // Check for explicit property mapping that might handle collection conversion
            if (AM020MappingConfigurationHelpers.IsDestinationPropertyExplicitlyConfigured(
                    invocation,
                    destinationProperty.Name,
                    context.SemanticModel))
            {
                continue;
            }

            // Check if both properties are collections and analyze element types
            if (AutoMapperAnalysisHelpers.IsCollectionType(sourceProperty.Type) &&
                AutoMapperAnalysisHelpers.IsCollectionType(destinationProperty.Type))
            {
                AnalyzeCollectionElementTypes(context, invocation, sourceProperty, destinationProperty,
                    sourceType, destinationType, reportedProperties);
            }
        }

        return reportedProperties;
    }

    private static void AnalyzeCollectionElementTypes(SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IPropertySymbol sourceProperty,
        IPropertySymbol destinationProperty,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        HashSet<string> reportedProperties)
    {
        // Get element types from collections
        ITypeSymbol? sourceElementType = AutoMapperAnalysisHelpers.GetCollectionElementType(sourceProperty.Type);
        ITypeSymbol? destElementType = AutoMapperAnalysisHelpers.GetCollectionElementType(destinationProperty.Type);

        if (sourceElementType == null || destElementType == null)
        {
            return;
        }

        // AM003 owns collection container incompatibilities (HashSet/Queue/Stack/SortedSet/LinkedList/
        // immutable/frozen). Defer entirely so combined container+element mismatches do not double-report.
        if (AutoMapperAnalysisHelpers.AreCollectionTypesIncompatible(sourceProperty.Type, destinationProperty.Type))
        {
            return;
        }

        var registry = CreateMapRegistry.FromCompilation(context.Compilation);

        // Dictionary destinations surface as KeyValuePair<TKey, TValue> element types. Decompose into
        // key/value axes: AutoMapper maps Dictionary<K, Foo> -> Dictionary<K, FooDto> correctly when a
        // CreateMap<Foo, FooDto> is registered, so the previous "is there a CreateMap<KeyValuePair<..>,
        // KeyValuePair<..>>?" check reported a false positive. Decomposing also lets the fixer offer a
        // ToDictionary / element-CreateMap rewrite instead of a manual-ignore dead end.
        if (TryGetKeyValuePairTypes(sourceElementType, out ITypeSymbol? keySource, out ITypeSymbol? valueSource) &&
            TryGetKeyValuePairTypes(destElementType, out ITypeSymbol? keyDest, out ITypeSymbol? valueDest))
        {
            bool keyMappable = IsElementAxisMappable(context.Compilation, keySource!, keyDest!, registry);
            bool valueMappable = IsElementAxisMappable(context.Compilation, valueSource!, valueDest!, registry);
            if (keyMappable && valueMappable)
            {
                // Both axes are individually mappable; AutoMapper can map this dictionary.
                return;
            }

            ImmutableDictionary<string, string?>.Builder dictionaryProperties =
                ImmutableDictionary.CreateBuilder<string, string?>();
            dictionaryProperties.Add("IsDictionary", "true");
            dictionaryProperties.Add("KeySourceType", keySource!.ToDisplayString());
            dictionaryProperties.Add("KeyDestType", keyDest!.ToDisplayString());
            dictionaryProperties.Add("ValueSourceType", valueSource!.ToDisplayString());
            dictionaryProperties.Add("ValueDestType", valueDest!.ToDisplayString());

            ReportElementMismatch(context, invocation, sourceProperty, destinationProperty, sourceType,
                destinationType, sourceElementType, destElementType, reportedProperties, dictionaryProperties);
            return;
        }

        // Check if element types are compatible
        if (!AreElementTypesCompatible(context.Compilation, sourceElementType, destElementType))
        {
            // Check if there's an explicit CreateMap for the element types
            if (registry.Contains(sourceElementType, destElementType))
            {
                // Element mapping exists, so AutoMapper can handle this collection mapping
                return;
            }

            ReportElementMismatch(context, invocation, sourceProperty, destinationProperty, sourceType,
                destinationType, sourceElementType, destElementType, reportedProperties, extraProperties: null);
        }
    }

    private static void ReportElementMismatch(SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IPropertySymbol sourceProperty,
        IPropertySymbol destinationProperty,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        ITypeSymbol sourceElementType,
        ITypeSymbol destElementType,
        HashSet<string> reportedProperties,
        ImmutableDictionary<string, string?>.Builder? extraProperties)
    {
        ImmutableDictionary<string, string?>.Builder properties =
            ImmutableDictionary.CreateBuilder<string, string?>();
        properties.Add("PropertyName", destinationProperty.Name);
        properties.Add("SourcePropertyName", sourceProperty.Name);
        properties.Add("DestinationPropertyName", destinationProperty.Name);
        properties.Add("SourceElementType", sourceElementType.ToDisplayString());
        properties.Add("DestElementType", destElementType.ToDisplayString());
        if (extraProperties != null)
        {
            foreach (KeyValuePair<string, string?> entry in extraProperties)
            {
                properties[entry.Key] = entry.Value;
            }
        }

        var diagnostic = Diagnostic.Create(
            CollectionElementIncompatibilityRule,
            invocation.GetLocation(),
            properties.ToImmutable(),
            sourceProperty.Name,
            AutoMapperAnalysisHelpers.GetTypeName(sourceType),
            sourceElementType.ToDisplayString(),
            AutoMapperAnalysisHelpers.GetTypeName(destinationType),
            destinationProperty.Name,
            destElementType.ToDisplayString());

        context.ReportDiagnostic(diagnostic);
        reportedProperties.Add(sourceProperty.Name);
        reportedProperties.Add(destinationProperty.Name);
    }

    /// <summary>
    ///     Decomposes a <c>KeyValuePair&lt;TKey, TValue&gt;</c> element type into its key and value type
    ///     arguments. Used to analyze dictionary destinations per axis.
    /// </summary>
    private static bool TryGetKeyValuePairTypes(ITypeSymbol elementType, out ITypeSymbol? keyType,
        out ITypeSymbol? valueType)
    {
        keyType = null;
        valueType = null;
        if (elementType is INamedTypeSymbol named &&
            named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.KeyValuePair<TKey, TValue>" &&
            named.TypeArguments.Length == 2)
        {
            keyType = named.TypeArguments[0];
            valueType = named.TypeArguments[1];
            return true;
        }

        return false;
    }

    /// <summary>
    ///     A dictionary key/value axis is mappable when the types are directly compatible (including
    ///     compiler-known implicit conversions) or a CreateMap is registered for them.
    /// </summary>
    private static bool IsElementAxisMappable(Compilation compilation, ITypeSymbol sourceType, ITypeSymbol destType,
        CreateMapRegistry registry)
    {
        return AreElementTypesCompatible(compilation, sourceType, destType) ||
               registry.Contains(sourceType, destType);
    }

    private static bool AreElementTypesCompatible(Compilation compilation, ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        if (AutoMapperAnalysisHelpers.AreTypesCompatible(sourceType, destinationType))
        {
            return true;
        }

        Conversion conversion = compilation.ClassifyConversion(sourceType, destinationType);
        return conversion.Exists && conversion.IsImplicit;
    }
}

using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.TypeSafety;

/// <summary>
///     Analyzer for detecting collection type incompatibility issues in AutoMapper configurations
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM003_CollectionTypeIncompatibilityAnalyzer : DiagnosticAnalyzer
{
    internal const string PropertyNamePropertyName = "PropertyName";
    internal const string SourcePropertyTypePropertyName = "SourcePropertyType";
    internal const string DestinationPropertyTypePropertyName = "DestinationPropertyType";
    internal const string SourceElementTypePropertyName = "SourceElementType";
    internal const string DestinationElementTypePropertyName = "DestinationElementType";

    /// <summary>
    ///     "true" when the source element type is implicitly convertible to the destination element type
    ///     (e.g. a derived-to-base upcast). The fixer uses this to decide whether a <c>(Dest)x</c> element
    ///     cast is safe to generate when no named string conversion exists.
    /// </summary>
    internal const string ElementImplicitlyConvertiblePropertyName = "ElementImplicitlyConvertible";

    /// <summary>
    ///     AM003: Collection type incompatibility without proper conversion
    /// </summary>
    public static readonly DiagnosticDescriptor CollectionTypeIncompatibilityRule = new(
        "AM003",
        "Collection type incompatibility in AutoMapper configuration",
        "Property '{0}' has incompatible collection types: {1}.{0} ({2}) cannot be mapped to {3}.{0} ({4}) without explicit collection conversion",
        "AutoMapper.Collections",
        DiagnosticSeverity.Error,
        true,
        "Source and destination properties have incompatible collection types that require explicit conversion configuration.");

    /// <summary>
    ///     Legacy descriptor retained for binary compatibility. Element-level incompatibility is owned by
    ///     <c>AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule</c>; this AM003
    ///     descriptor is never registered in <see cref="SupportedDiagnostics" /> and never reports. Do not
    ///     reuse — the drift guard
    ///     <c>RuleCatalogTests.Analyzers_ShouldRegisterEveryDeclaredDiagnosticDescriptor</c> requires every
    ///     additional orphan to be marked <see cref="ObsoleteAttribute" /> so legacy intent stays explicit.
    /// </summary>
    [Obsolete(
        "AM003 no longer owns collection element incompatibility diagnostics. AM021's CollectionElement"
        + "IncompatibilityRule handles element mismatches. This descriptor is retained only for binary "
        + "compatibility and never appears in SupportedDiagnostics.",
        error: false)]
    public static readonly DiagnosticDescriptor CollectionElementIncompatibilityRule = new(
        "AM003",
        "Collection element type incompatibility in AutoMapper configuration",
        "Property '{0}' has incompatible collection element types: {1}.{0} ({2}) elements cannot be mapped to {3}.{0} ({4}) elements without explicit conversion",
        "AutoMapper.Collections",
        DiagnosticSeverity.Warning,
        true,
        "Collection properties have compatible collection types but incompatible element types that may require custom mapping.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [CollectionTypeIncompatibilityRule];

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

        // Ensure this invocation resolves to AutoMapper CreateMap to avoid lookalike false positives.
        if (!IsAutoMapperCreateMapInvocation(invocationExpr, context.SemanticModel))
        {
            return;
        }

        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) typeArguments =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (typeArguments.sourceType == null || typeArguments.destinationType == null)
        {
            return;
        }

        var reportedMismatches = new HashSet<string>(StringComparer.Ordinal);

        // Analyze collection compatibility for property mappings
        AnalyzeCollectionCompatibility(context, invocationExpr, typeArguments.sourceType,
            typeArguments.destinationType, reportedMismatches);

        InvocationExpressionSyntax? reverseMapInvocation =
            AutoMapperAnalysisHelpers.GetReverseMapInvocation(invocationExpr);
        if (reverseMapInvocation != null)
        {
            AnalyzeCollectionCompatibility(context, reverseMapInvocation, typeArguments.destinationType,
                typeArguments.sourceType, reportedMismatches);
        }
    }


    private static void AnalyzeCollectionCompatibility(SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        HashSet<string> reportedMismatches)
    {
        if (AM020MappingConfigurationHelpers.HasCustomConstructionOrConversion(invocation, context.SemanticModel))
        {
            return;
        }

        IEnumerable<IPropertySymbol> sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        IEnumerable<IPropertySymbol> destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, false);

        foreach (IPropertySymbol sourceProperty in sourceProperties)
        {
            IPropertySymbol? destinationProperty =
                destinationProperties.FirstOrDefault(p => p.Name == sourceProperty.Name) ??
                destinationProperties.FirstOrDefault(p =>
                    string.Equals(p.Name, sourceProperty.Name, StringComparison.OrdinalIgnoreCase));

            if (destinationProperty == null)
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

            // Check if both properties are collections
            if (AutoMapperAnalysisHelpers.IsCollectionType(sourceProperty.Type) &&
                AutoMapperAnalysisHelpers.IsCollectionType(destinationProperty.Type))
            {
                AnalyzeCollectionPropertyCompatibility(context, invocation, sourceProperty, destinationProperty,
                    sourceType, destinationType, reportedMismatches);
            }
        }
    }


    private static void AnalyzeCollectionPropertyCompatibility(SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IPropertySymbol sourceProperty,
        IPropertySymbol destinationProperty,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        HashSet<string> reportedMismatches)
    {
        string sourceTypeName = sourceProperty.Type.ToDisplayString();
        string destTypeName = destinationProperty.Type.ToDisplayString();

        // Get element types for comparison
        ITypeSymbol? sourceElementType = AutoMapperAnalysisHelpers.GetCollectionElementType(sourceProperty.Type);
        ITypeSymbol? destElementType = AutoMapperAnalysisHelpers.GetCollectionElementType(destinationProperty.Type);

        if (sourceElementType == null || destElementType == null)
        {
            return;
        }

        // AM003 owns collection container incompatibilities, but should stay quiet
        // when the source collection can already satisfy the destination contract.
        if (!IsImplicitlyAssignable(sourceProperty.Type, destinationProperty.Type, context.SemanticModel.Compilation) &&
            AreCollectionTypesIncompatible(sourceProperty.Type, destinationProperty.Type))
        {
            string mismatchKey = CreateMismatchKey(
                sourceType,
                destinationType,
                sourceProperty.Name,
                sourceProperty.Type,
                destinationProperty.Type);
            if (!reportedMismatches.Add(mismatchKey))
            {
                return;
            }

            ImmutableDictionary<string, string?>.Builder properties =
                ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add(PropertyNamePropertyName, sourceProperty.Name);
            properties.Add(SourcePropertyTypePropertyName, sourceTypeName);
            properties.Add(DestinationPropertyTypePropertyName, destTypeName);
            properties.Add(SourceElementTypePropertyName, sourceElementType.ToDisplayString());
            properties.Add(DestinationElementTypePropertyName, destElementType.ToDisplayString());
            bool elementImplicitlyConvertible = context.SemanticModel.Compilation
                .ClassifyConversion(sourceElementType, destElementType).IsImplicit;
            properties.Add(ElementImplicitlyConvertiblePropertyName,
                elementImplicitlyConvertible ? "true" : "false");
            // Backward-compatible aliases for existing fixers/consumers.
            properties.Add("SourceType", sourceTypeName);
            properties.Add("DestType", destTypeName);
            properties.Add("SourceTypeName", AutoMapperAnalysisHelpers.GetTypeName(sourceType));
            properties.Add("DestTypeName", AutoMapperAnalysisHelpers.GetTypeName(destinationType));

            var diagnostic = Diagnostic.Create(
                CollectionTypeIncompatibilityRule,
                invocation.GetLocation(),
                properties.ToImmutable(),
                sourceProperty.Name,
                AutoMapperAnalysisHelpers.GetTypeName(sourceType),
                sourceTypeName,
                AutoMapperAnalysisHelpers.GetTypeName(destinationType),
                destTypeName);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static string CreateMismatchKey(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        string propertyName,
        ITypeSymbol sourcePropertyType,
        ITypeSymbol destinationPropertyType)
    {
        string[] typeNames = [sourceType.ToDisplayString(), destinationType.ToDisplayString()];
        Array.Sort(typeNames, StringComparer.Ordinal);

        string[] propertyTypeNames = [sourcePropertyType.ToDisplayString(), destinationPropertyType.ToDisplayString()];
        Array.Sort(propertyTypeNames, StringComparer.Ordinal);

        return $"{typeNames[0]}|{typeNames[1]}::{propertyName}::{propertyTypeNames[0]}|{propertyTypeNames[1]}";
    }


    private static bool AreCollectionTypesIncompatible(ITypeSymbol sourceType, ITypeSymbol destType)
    {
        // Arrays and lists are generally compatible
        bool sourceIsArray = sourceType.TypeKind == TypeKind.Array;
        bool destIsArray = destType.TypeKind == TypeKind.Array;

        if (sourceIsArray && destIsArray)
        {
            return false; // Arrays to arrays are compatible
        }

        // Check for specific incompatible combinations
        bool sourceIsHashSet = IsConstructedFromType(sourceType, "System.Collections.Generic.HashSet<T>");
        bool destIsHashSet = IsConstructedFromType(destType, "System.Collections.Generic.HashSet<T>");
        bool sourceIsQueue = IsConstructedFromType(sourceType, "System.Collections.Generic.Queue<T>");
        bool destIsQueue = IsConstructedFromType(destType, "System.Collections.Generic.Queue<T>");
        bool sourceIsStack = IsConstructedFromType(sourceType, "System.Collections.Generic.Stack<T>");
        bool destIsStack = IsConstructedFromType(destType, "System.Collections.Generic.Stack<T>");
        bool sourceIsSortedSet = IsConstructedFromType(sourceType, "System.Collections.Generic.SortedSet<T>");
        bool destIsSortedSet = IsConstructedFromType(destType, "System.Collections.Generic.SortedSet<T>");
        bool sourceIsLinkedList = IsConstructedFromType(sourceType, "System.Collections.Generic.LinkedList<T>");
        bool destIsLinkedList = IsConstructedFromType(destType, "System.Collections.Generic.LinkedList<T>");
        bool sourceIsImmutableList =
            IsConstructedFromType(sourceType, "System.Collections.Immutable.ImmutableList<T>");
        bool destIsImmutableList =
            IsConstructedFromType(destType, "System.Collections.Immutable.ImmutableList<T>");
        bool sourceIsImmutableArray =
            IsConstructedFromType(sourceType, "System.Collections.Immutable.ImmutableArray<T>");
        bool destIsImmutableArray =
            IsConstructedFromType(destType, "System.Collections.Immutable.ImmutableArray<T>");
        bool sourceIsImmutableHashSet =
            IsConstructedFromType(sourceType, "System.Collections.Immutable.ImmutableHashSet<T>");
        bool destIsImmutableHashSet =
            IsConstructedFromType(destType, "System.Collections.Immutable.ImmutableHashSet<T>");
        bool sourceIsFrozenSet = IsConstructedFromType(sourceType, "System.Collections.Frozen.FrozenSet<T>");
        bool destIsFrozenSet = IsConstructedFromType(destType, "System.Collections.Frozen.FrozenSet<T>");

        // HashSet ↔ List/Array/IEnumerable bidirectional incompatibility
        if (sourceIsHashSet && !destIsHashSet)
        {
            return true;
        }

        if (!sourceIsHashSet && destIsHashSet)
        {
            return true;
        }

        // Queue ↔ other collections bidirectional incompatibility
        if (sourceIsQueue && !destIsQueue)
        {
            return true;
        }

        if (!sourceIsQueue && destIsQueue)
        {
            return true;
        }

        // Stack ↔ other collections bidirectional incompatibility
        if (sourceIsStack && !destIsStack)
        {
            return true;
        }

        if (!sourceIsStack && destIsStack)
        {
            return true;
        }

        // SortedSet/LinkedList carry destination container semantics that should be explicit.
        if (sourceIsSortedSet && !destIsSortedSet)
        {
            return true;
        }

        if (!sourceIsSortedSet && destIsSortedSet)
        {
            return true;
        }

        if (sourceIsLinkedList && !destIsLinkedList)
        {
            return true;
        }

        if (!sourceIsLinkedList && destIsLinkedList)
        {
            return true;
        }

        // Immutable/frozen destination containers require explicit factory conversion.
        if (sourceIsImmutableList && !destIsImmutableList)
        {
            return true;
        }

        if (!sourceIsImmutableList && destIsImmutableList)
        {
            return true;
        }

        if (sourceIsImmutableArray && !destIsImmutableArray)
        {
            return true;
        }

        if (!sourceIsImmutableArray && destIsImmutableArray)
        {
            return true;
        }

        if (sourceIsImmutableHashSet && !destIsImmutableHashSet)
        {
            return true;
        }

        if (!sourceIsImmutableHashSet && destIsImmutableHashSet)
        {
            return true;
        }

        if (sourceIsFrozenSet && !destIsFrozenSet)
        {
            return true;
        }

        if (!sourceIsFrozenSet && destIsFrozenSet)
        {
            return true;
        }

        // Non-generic to generic collections
        if (!IsGenericCollection(sourceType) && IsGenericCollection(destType))
        {
            return true;
        }

        return false;
    }

    private static bool IsImplicitlyAssignable(ITypeSymbol sourceType, ITypeSymbol destinationType, Compilation compilation)
    {
        Conversion conversion = compilation.ClassifyConversion(sourceType, destinationType);
        return conversion.Exists && conversion.IsImplicit;
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

    private static bool IsAutoMapperCreateMapInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        return MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "CreateMap");
    }


}

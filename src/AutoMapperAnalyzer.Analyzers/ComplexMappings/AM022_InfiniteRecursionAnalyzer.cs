using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.ComplexMappings;

/// <summary>
///     Analyzer for detecting infinite recursion risks in AutoMapper configurations
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM022_InfiniteRecursionAnalyzer : DiagnosticAnalyzer
{
    internal const string ConstructorOwnedCycleProperty = "ConstructorOwnedCycle";

    private readonly struct MappedPropertyPair
    {
        public MappedPropertyPair(
            IPropertySymbol sourceProperty,
            IPropertySymbol destinationProperty,
            bool isConstructorOwned = false)
        {
            SourceProperty = sourceProperty;
            DestinationProperty = destinationProperty;
            IsConstructorOwned = isConstructorOwned;
        }

        public IPropertySymbol SourceProperty { get; }
        public IPropertySymbol DestinationProperty { get; }
        public bool IsConstructorOwned { get; }
    }

    /// <summary>
    ///     AM022: Infinite recursion risk detected
    /// </summary>
    public static readonly DiagnosticDescriptor InfiniteRecursionRiskRule = new(
        "AM022",
        "Infinite recursion risk in AutoMapper configuration",
        "Potential infinite recursion detected: {0} to {1} mapping may cause stack overflow due to circular references",
        "AutoMapper.Recursion",
        DiagnosticSeverity.Warning,
        true,
        "Circular object references can cause infinite recursion during mapping. Consider using MaxDepth() or ignoring circular properties."
    );

    /// <summary>
    ///     AM022: Self-referencing type detected
    /// </summary>
    public static readonly DiagnosticDescriptor SelfReferencingTypeRule = new(
        "AM022",
        "Self-referencing type in AutoMapper configuration",
        "Self-referencing type detected: {0} contains properties of its own type, which may cause infinite recursion",
        "AutoMapper.Recursion",
        DiagnosticSeverity.Warning,
        true,
        "Self-referencing types can cause infinite recursion during mapping. Consider using MaxDepth() or ignoring self-referencing properties."
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(InfiniteRecursionRiskRule, SelfReferencingTypeRule);

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

        InvocationExpressionSyntax? reverseMapInvocation =
            AutoMapperAnalysisHelpers.GetReverseMapInvocation(invocationExpr);
        CreateMapRegistry createMapRegistry = CreateMapRegistry.FromCompilation(context.Compilation);
        ImmutableArray<MappedPropertyPair> rootMappedPairs =
            GetRootMappedPropertyPairs(
                invocationExpr,
                typeArguments.sourceType,
                typeArguments.destinationType,
                context.SemanticModel,
                createMapRegistry);

        bool hasRootMaxDepth = createMapRegistry.IsMaxDepthConstrained(
                                   typeArguments.sourceType,
                                   typeArguments.destinationType) ||
                               HasForwardAutoMapperConfiguration(
                                   invocationExpr,
                                   reverseMapInvocation,
                                   context.SemanticModel,
                                   "MaxDepth");
        bool hasRootPreserveReferences = createMapRegistry.IsPreserveReferencesConstrained(
                                             typeArguments.sourceType,
                                             typeArguments.destinationType) ||
                                         HasForwardAutoMapperConfiguration(
                                             invocationExpr,
                                             reverseMapInvocation,
                                             context.SemanticModel,
                                             "PreserveReferences");

        // A custom converter owns construction completely. Member/depth policies are evaluated
        // against the proven root edges because constructor arguments execute before member mapping.
        if (createMapRegistry.IsConvertUsingConstrained(
                typeArguments.sourceType,
                typeArguments.destinationType) ||
            HasForwardAutoMapperConfiguration(
                invocationExpr,
                reverseMapInvocation,
                context.SemanticModel,
                "ConvertUsing")
            || HasCircularPropertyIgnored(
                invocationExpr,
                typeArguments.sourceType as INamedTypeSymbol,
                typeArguments.destinationType as INamedTypeSymbol,
                reverseMapInvocation,
                context.SemanticModel,
                createMapRegistry,
                rootMappedPairs
            )
        )
        {
            return;
        }

        // Analyze for infinite recursion risks
        AnalyzeRecursionRisk(
            context,
            invocationExpr,
            typeArguments.sourceType,
            typeArguments.destinationType,
            createMapRegistry,
            rootMappedPairs,
            hasRootMaxDepth,
            hasRootPreserveReferences
        );
    }


    private static bool HasForwardAutoMapperConfiguration(
        InvocationExpressionSyntax invocation,
        InvocationExpressionSyntax? reverseMapInvocation,
        SemanticModel semanticModel,
        string methodName
    )
    {
        if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol createMapMethod)
        {
            return false;
        }

        IAssemblySymbol autoMapperAssembly = (createMapMethod.ReducedFrom ?? createMapMethod).ContainingAssembly;
        SyntaxNode? parent = invocation.Parent;
        while (parent != null)
        {
            if (
                parent is InvocationExpressionSyntax chainedCall
                && MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(chainedCall, semanticModel, methodName)
                && semanticModel.GetSymbolInfo(chainedCall).Symbol is IMethodSymbol method
                && SymbolEqualityComparer.Default.Equals(
                    (method.ReducedFrom ?? method).ContainingAssembly,
                    autoMapperAssembly)
                && CreateMapRegistry.IsMappingInitializerRootedAtCreateMap(
                    chainedCall,
                    invocation,
                    semanticModel,
                    autoMapperAssembly)
                && AppliesToForwardDirection(chainedCall, reverseMapInvocation)
            )
            {
                return true;
            }

            parent = parent.Parent;
        }

        return false;
    }

    private static bool HasCircularPropertyIgnored(
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol? sourceType,
        INamedTypeSymbol? destinationType,
        InvocationExpressionSyntax? reverseMapInvocation,
        SemanticModel semanticModel,
        CreateMapRegistry createMapRegistry,
        ImmutableArray<MappedPropertyPair> rootMappedPairs
    )
    {
        if (sourceType == null || destinationType == null)
        {
            return false;
        }

        HashSet<string> ignoredProperties = GetIgnoredProperties(invocation, reverseMapInvocation, semanticModel);
        HashSet<string> selfReferencingDestProperties = FindRecursiveDestinationProperties(
            sourceType,
            destinationType,
            createMapRegistry,
            rootMappedPairs);
        if (rootMappedPairs.Any(pair =>
                pair.IsConstructorOwned &&
                selfReferencingDestProperties.Contains(pair.DestinationProperty.Name)))
        {
            return false;
        }

        if (
            selfReferencingDestProperties.Count > 0
            && selfReferencingDestProperties.All(prop => ignoredProperties.Contains(prop))
        )
        {
            return true;
        }

        return false;
    }

    private static HashSet<string> FindSelfReferencingProperties(INamedTypeSymbol type)
    {
        var selfRefProps = new HashSet<string>();
        IEnumerable<IPropertySymbol> properties =
            AutoMapperAnalysisHelpers.GetMappableProperties(type, requireSetter: false);

        foreach (IPropertySymbol? property in properties)
        {
            // Check if property type is same as the containing type
            if (SymbolEqualityComparer.Default.Equals(property.Type, type))
            {
                selfRefProps.Add(property.Name);
            }

            // Check collection element types
            ITypeSymbol? elementType = AutoMapperAnalysisHelpers.GetCollectionElementType(property.Type);
            if (elementType != null && SymbolEqualityComparer.Default.Equals(elementType, type))
            {
                selfRefProps.Add(property.Name);
            }
        }

        return selfRefProps;
    }

    private static HashSet<string> GetIgnoredProperties(
        InvocationExpressionSyntax invocation,
        InvocationExpressionSyntax? reverseMapInvocation,
        SemanticModel semanticModel
    )
    {
        var ignoredProperties = new HashSet<string>();

        // Look for chained ForMember/ForPath calls with Ignore()
        SyntaxNode? parent = invocation.Parent;
        while (parent != null)
        {
            if (
                parent is InvocationExpressionSyntax chainedCall
                && (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(chainedCall, semanticModel, "ForMember")
                    || MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(chainedCall, semanticModel, "ForPath"))
                && AppliesToForwardDirection(chainedCall, reverseMapInvocation)
            )
            {
                // Check if this mapping call has Ignore()
                if (HasIgnoreConfiguration(chainedCall, semanticModel))
                {
                    string? propertyName = ExtractPropertyNameFromForMemberOrPath(chainedCall, semanticModel);
                    if (!string.IsNullOrEmpty(propertyName))
                    {
                        ignoredProperties.Add(propertyName!);
                    }
                }
            }

            parent = parent.Parent;
        }

        return ignoredProperties;
    }

    private static bool HasIgnoreConfiguration(
        InvocationExpressionSyntax forMemberCall,
        SemanticModel semanticModel)
    {
        return forMemberCall.ArgumentList.Arguments.Count >= 2
               && forMemberCall.ArgumentList.Arguments[1].Expression
                   .DescendantNodesAndSelf()
                   .OfType<InvocationExpressionSyntax>()
                   .Any(invocation =>
                       MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "Ignore"));
    }

    private static string? ExtractPropertyNameFromForMemberOrPath(
        InvocationExpressionSyntax forMemberCall,
        SemanticModel semanticModel
    )
    {
        if (forMemberCall.ArgumentList.Arguments.Count == 0)
        {
            return null;
        }

        ExpressionSyntax destinationSelector = forMemberCall.ArgumentList.Arguments[0].Expression;
        if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(forMemberCall, semanticModel, "ForPath"))
        {
            return TryGetDirectSelectedProperty(destinationSelector, semanticModel, out IPropertySymbol property)
                ? property.Name
                : null;
        }

        return AM020MappingConfigurationHelpers.GetSelectedTopLevelMemberNameWithSemanticModel(
            destinationSelector,
            semanticModel);
    }

    private static void AnalyzeRecursionRisk(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol? sourceType,
        ITypeSymbol? destinationType,
        CreateMapRegistry createMapRegistry,
        ImmutableArray<MappedPropertyPair> rootMappedPairs,
        bool hasRootMaxDepth,
        bool hasRootPreserveReferences
    )
    {
        if (sourceType == null || destinationType == null)
        {
            return;
        }

        // Check for self-referencing mapped properties on both sides to reduce false positives.
        bool hasMappedSelfReference = HasMappedSelfReference(
            sourceType,
            destinationType,
            rootMappedPairs);
        bool hasConstructorOwnedSelfReference = HasConstructorOwnedSelfReference(
            sourceType,
            destinationType,
            rootMappedPairs);
        bool hasRootDepthOrReferencePolicy = hasRootMaxDepth || hasRootPreserveReferences;
        if (hasMappedSelfReference &&
            (hasConstructorOwnedSelfReference || !hasRootDepthOrReferencePolicy))
        {
            ImmutableDictionary<string, string?> properties = hasConstructorOwnedSelfReference
                ? ImmutableDictionary<string, string?>.Empty.Add(ConstructorOwnedCycleProperty, bool.TrueString)
                : ImmutableDictionary<string, string?>.Empty;
            var diagnostic = Diagnostic.Create(
                SelfReferencingTypeRule,
                invocation.GetLocation(),
                properties,
                AutoMapperAnalysisHelpers.GetTypeName(sourceType),
                AutoMapperAnalysisHelpers.GetTypeName(destinationType)
            );

            context.ReportDiagnostic(diagnostic);
            return;
        }

        // Check for circular references reachable through proven root member paths.
        bool hasMappedCircularReference = HasMappedCircularReference(
            sourceType,
            destinationType,
            createMapRegistry,
            rootMappedPairs);
        bool hasConstructorOwnedCircularReference = HasConstructorOwnedCircularReference(
            sourceType,
            destinationType,
            createMapRegistry,
            rootMappedPairs);
        bool hasConstructorOnlyCircularReference = HasConstructorOnlyCircularReference(
            sourceType,
            destinationType,
            createMapRegistry,
            rootMappedPairs);
        bool hasIneffectiveConstructorPolicy =
            hasRootPreserveReferences && hasConstructorOwnedCircularReference ||
            hasRootMaxDepth && hasConstructorOnlyCircularReference;
        if (hasMappedCircularReference &&
            (hasIneffectiveConstructorPolicy || !hasRootDepthOrReferencePolicy))
        {
            ImmutableDictionary<string, string?> properties = hasConstructorOwnedCircularReference
                ? ImmutableDictionary<string, string?>.Empty.Add(ConstructorOwnedCycleProperty, bool.TrueString)
                : ImmutableDictionary<string, string?>.Empty;
            var diagnostic = Diagnostic.Create(
                InfiniteRecursionRiskRule,
                invocation.GetLocation(),
                properties,
                AutoMapperAnalysisHelpers.GetTypeName(sourceType),
                AutoMapperAnalysisHelpers.GetTypeName(destinationType)
            );

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool HasMappedSelfReference(
        ITypeSymbol? sourceType,
        ITypeSymbol? destinationType,
        ImmutableArray<MappedPropertyPair> rootMappedPairs)
    {
        if (sourceType == null || destinationType == null)
        {
            return false;
        }

        foreach (MappedPropertyPair mappedPair in rootMappedPairs)
        {
            IPropertySymbol sourceProperty = mappedPair.SourceProperty;
            IPropertySymbol destinationProperty = mappedPair.DestinationProperty;
            if (
                IsSelfReference(sourceProperty.Type, sourceType)
                && IsSelfReference(destinationProperty.Type, destinationType)
            )
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasConstructorOwnedSelfReference(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        ImmutableArray<MappedPropertyPair> rootMappedPairs)
    {
        return rootMappedPairs.Any(pair =>
            pair.IsConstructorOwned &&
            IsSelfReference(pair.SourceProperty.Type, sourceType) &&
            IsSelfReference(pair.DestinationProperty.Type, destinationType));
    }

    /// <summary>
    ///     Destination properties on a CreateMap pair whose mapped path participates
    ///     in a configured circular graph (self-ref or multi-type). Used by the analyzer for
    ///     Ignore suppression and by the code fix for graph-aware Ignore actions.
    /// </summary>
    internal static HashSet<string> FindRecursiveDestinationProperties(
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType,
        CreateMapRegistry createMapRegistry
    )
    {
        return FindRecursiveDestinationProperties(
            sourceType,
            destinationType,
            createMapRegistry,
            GetConventionMappedPropertyPairs(sourceType, destinationType).ToImmutableArray());
    }

    /// <summary>
    ///     Finds recursive destination members using the current CreateMap's proven root edges,
    ///     including direct property-to-property ForMember MapFrom configuration.
    /// </summary>
    internal static HashSet<string> FindRecursiveDestinationProperties(
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType,
        CreateMapRegistry createMapRegistry,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel
    )
    {
        return FindRecursiveDestinationProperties(
            sourceType,
            destinationType,
            createMapRegistry,
            GetRootMappedPropertyPairs(
                invocation,
                sourceType,
                destinationType,
                semanticModel,
                createMapRegistry));
    }

    private static HashSet<string> FindRecursiveDestinationProperties(
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType,
        CreateMapRegistry createMapRegistry,
        ImmutableArray<MappedPropertyPair> rootMappedPairs
    )
    {
        var recursiveProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        visited.Add(GetTypePairKey(sourceType, destinationType));

        foreach (MappedPropertyPair mappedPair in rootMappedPairs)
        {
            IPropertySymbol sourceProperty = mappedPair.SourceProperty;
            IPropertySymbol destinationProperty = mappedPair.DestinationProperty;
            ITypeSymbol sourcePropertyType = UnwrapCollectionElementType(sourceProperty.Type);
            ITypeSymbol destinationPropertyType = UnwrapCollectionElementType(destinationProperty.Type);

            if (IsSimpleType(sourcePropertyType) || IsSimpleType(destinationPropertyType))
            {
                continue;
            }

            if (
                HasMappedCircularReferenceRecursive(
                    sourcePropertyType,
                    destinationPropertyType,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    createMapRegistry,
                    1,
                    10
                )
            )
            {
                recursiveProperties.Add(destinationProperty.Name);
            }
        }

        return recursiveProperties;
    }

    private static bool HasMappedCircularReference(
        ITypeSymbol? sourceType,
        ITypeSymbol? destinationType,
        CreateMapRegistry createMapRegistry,
        ImmutableArray<MappedPropertyPair> rootMappedPairs
    )
    {
        if (sourceType == null || destinationType == null)
        {
            return false;
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        return HasMappedCircularReferenceRecursive(
            sourceType,
            destinationType,
            visited,
            createMapRegistry,
            0,
            10,
            rootMappedPairs);
    }

    private static bool HasMappedCircularReferenceRecursive(
        ITypeSymbol? currentSourceType,
        ITypeSymbol? currentDestinationType,
        HashSet<string> visited,
        CreateMapRegistry createMapRegistry,
        int depth,
        int maxDepth,
        ImmutableArray<MappedPropertyPair> rootMappedPairs = default
    )
    {
        // Prevent stack overflow by limiting recursion depth
        if (depth > maxDepth || currentSourceType == null || currentDestinationType == null)
        {
            return false;
        }

        bool requireConstructorOwnedEdge = false;

        // MaxDepth/PreserveReferences terminate ordinary member recursion, but AutoMapper resolves
        // constructor arguments before a destination instance exists. A map with a proven
        // constructor-owned edge must therefore keep traversing unless ConvertUsing owns creation.
        if (createMapRegistry.IsCycleConstrained(currentSourceType, currentDestinationType))
        {
            if (!createMapRegistry.TryGetUniqueForwardMapping(
                    currentSourceType,
                    currentDestinationType,
                    out InvocationExpressionSyntax constrainedInvocation,
                    out SemanticModel constrainedSemanticModel))
            {
                return false;
            }

            InvocationExpressionSyntax? constrainedReverseMap =
                AutoMapperAnalysisHelpers.GetReverseMapInvocation(constrainedInvocation);
            if (createMapRegistry.IsConvertUsingConstrained(
                    currentSourceType,
                    currentDestinationType) ||
                HasForwardAutoMapperConfiguration(
                    constrainedInvocation,
                    constrainedReverseMap,
                    constrainedSemanticModel,
                    "ConvertUsing"))
            {
                return false;
            }

            ImmutableArray<MappedPropertyPair> constrainedMappedPairs = GetRootMappedPropertyPairs(
                constrainedInvocation,
                currentSourceType,
                currentDestinationType,
                constrainedSemanticModel,
                createMapRegistry);
            if (!constrainedMappedPairs.Any(pair => pair.IsConstructorOwned))
            {
                return false;
            }

            bool hasMaxDepth = createMapRegistry.IsMaxDepthConstrained(
                currentSourceType,
                currentDestinationType);
            bool hasPreserveReferences = createMapRegistry.IsPreserveReferencesConstrained(
                currentSourceType,
                currentDestinationType);
            if (!hasMaxDepth && !hasPreserveReferences)
            {
                return false;
            }

            if (hasMaxDepth &&
                !hasPreserveReferences &&
                !HasConstructorOnlyCircularReference(
                    currentSourceType,
                    currentDestinationType,
                    createMapRegistry,
                    constrainedMappedPairs))
            {
                return false;
            }

            requireConstructorOwnedEdge = true;
        }

        string typePairKey = GetTypePairKey(currentSourceType, currentDestinationType);
        if (visited.Contains(typePairKey))
        {
            return true;
        }

        visited.Add(typePairKey);

        IEnumerable<MappedPropertyPair> mappedPairs =
            depth == 0 && !rootMappedPairs.IsDefault
                ? rootMappedPairs
                : GetDownstreamMappedPropertyPairs(
                    currentSourceType,
                    currentDestinationType,
                    createMapRegistry);
        if (requireConstructorOwnedEdge)
        {
            mappedPairs = mappedPairs.Where(pair => pair.IsConstructorOwned);
        }

        foreach (MappedPropertyPair mappedPair in mappedPairs)
        {
            IPropertySymbol sourceProperty = mappedPair.SourceProperty;
            IPropertySymbol destinationProperty = mappedPair.DestinationProperty;
            ITypeSymbol sourcePropertyType = UnwrapCollectionElementType(sourceProperty.Type);
            ITypeSymbol destinationPropertyType = UnwrapCollectionElementType(destinationProperty.Type);

            // Skip value types and system types
            if (IsSimpleType(sourcePropertyType) || IsSimpleType(destinationPropertyType))
            {
                continue;
            }

            if (
                !IsSameTypePair(sourcePropertyType, destinationPropertyType, currentSourceType, currentDestinationType)
                && !createMapRegistry.Contains(sourcePropertyType, destinationPropertyType)
            )
            {
                continue;
            }

            if (
                HasMappedCircularReferenceRecursive(
                    sourcePropertyType,
                    destinationPropertyType,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    createMapRegistry,
                    depth + 1,
                    maxDepth
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasConstructorOwnedCircularReference(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        CreateMapRegistry createMapRegistry,
        ImmutableArray<MappedPropertyPair> rootMappedPairs)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal)
        {
            GetTypePairKey(sourceType, destinationType)
        };

        foreach (MappedPropertyPair pair in rootMappedPairs.Where(pair => pair.IsConstructorOwned))
        {
            ITypeSymbol sourcePropertyType = UnwrapCollectionElementType(pair.SourceProperty.Type);
            ITypeSymbol destinationPropertyType = UnwrapCollectionElementType(pair.DestinationProperty.Type);
            if (!IsSimpleType(sourcePropertyType) &&
                !IsSimpleType(destinationPropertyType) &&
                HasMappedCircularReferenceRecursive(
                    sourcePropertyType,
                    destinationPropertyType,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    createMapRegistry,
                    1,
                    10))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasConstructorOnlyCircularReference(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        CreateMapRegistry createMapRegistry,
        ImmutableArray<MappedPropertyPair> rootMappedPairs)
    {
        string targetTypePairKey = GetTypePairKey(sourceType, destinationType);
        foreach (MappedPropertyPair pair in rootMappedPairs.Where(pair => pair.IsConstructorOwned))
        {
            ITypeSymbol sourcePropertyType = UnwrapCollectionElementType(pair.SourceProperty.Type);
            ITypeSymbol destinationPropertyType = UnwrapCollectionElementType(pair.DestinationProperty.Type);
            if (!IsSimpleType(sourcePropertyType) &&
                !IsSimpleType(destinationPropertyType) &&
                (IsSameTypePair(
                     sourcePropertyType,
                     destinationPropertyType,
                     sourceType,
                     destinationType) ||
                 createMapRegistry.Contains(sourcePropertyType, destinationPropertyType)) &&
                HasConstructorOnlyCircularReferenceRecursive(
                    sourcePropertyType,
                    destinationPropertyType,
                    targetTypePairKey,
                    new HashSet<string>(StringComparer.Ordinal),
                    createMapRegistry,
                    1,
                    10))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasConstructorOnlyCircularReferenceRecursive(
        ITypeSymbol? currentSourceType,
        ITypeSymbol? currentDestinationType,
        string targetTypePairKey,
        HashSet<string> visited,
        CreateMapRegistry createMapRegistry,
        int depth,
        int maxDepth)
    {
        if (depth > maxDepth || currentSourceType == null || currentDestinationType == null)
        {
            return false;
        }

        string typePairKey = GetTypePairKey(currentSourceType, currentDestinationType);
        if (string.Equals(typePairKey, targetTypePairKey, StringComparison.Ordinal))
        {
            return true;
        }

        if (!visited.Add(typePairKey) ||
            !createMapRegistry.TryGetUniqueForwardMapping(
                currentSourceType,
                currentDestinationType,
                out InvocationExpressionSyntax invocation,
                out SemanticModel semanticModel))
        {
            return false;
        }

        InvocationExpressionSyntax? reverseMapInvocation =
            AutoMapperAnalysisHelpers.GetReverseMapInvocation(invocation);
        if (HasForwardAutoMapperConfiguration(
                invocation,
                reverseMapInvocation,
                semanticModel,
                "ConvertUsing"))
        {
            return false;
        }

        foreach (MappedPropertyPair pair in GetRootMappedPropertyPairs(
                     invocation,
                     currentSourceType,
                     currentDestinationType,
                     semanticModel,
                     createMapRegistry).Where(pair => pair.IsConstructorOwned))
        {
            ITypeSymbol sourcePropertyType = UnwrapCollectionElementType(pair.SourceProperty.Type);
            ITypeSymbol destinationPropertyType = UnwrapCollectionElementType(pair.DestinationProperty.Type);
            if (IsSimpleType(sourcePropertyType) ||
                IsSimpleType(destinationPropertyType) ||
                !IsSameTypePair(
                    sourcePropertyType,
                    destinationPropertyType,
                    currentSourceType,
                    currentDestinationType) &&
                !createMapRegistry.Contains(sourcePropertyType, destinationPropertyType))
            {
                continue;
            }

            if (HasConstructorOnlyCircularReferenceRecursive(
                    sourcePropertyType,
                    destinationPropertyType,
                    targetTypePairKey,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    createMapRegistry,
                    depth + 1,
                    maxDepth))
            {
                return true;
            }
        }

        return false;
    }

    private static ImmutableArray<MappedPropertyPair>
        GetDownstreamMappedPropertyPairs(
            ITypeSymbol sourceType,
            ITypeSymbol destinationType,
            CreateMapRegistry createMapRegistry)
    {
        if (createMapRegistry.TryGetUniqueForwardMapping(
                sourceType,
                destinationType,
                out var invocation,
                out SemanticModel semanticModel))
        {
            return GetRootMappedPropertyPairs(
                invocation,
                sourceType,
                destinationType,
                semanticModel,
                createMapRegistry);
        }

        return GetConventionMappedPropertyPairs(sourceType, destinationType).ToImmutableArray();
    }

    private static ImmutableArray<MappedPropertyPair>
        GetRootMappedPropertyPairs(
            InvocationExpressionSyntax invocation,
            ITypeSymbol sourceType,
            ITypeSymbol destinationType,
            SemanticModel semanticModel,
            CreateMapRegistry createMapRegistry
        )
    {
        var mappedPairs = GetConventionMappedPropertyPairs(sourceType, destinationType).ToList();
        if (!createMapRegistry.TryGetUniqueForwardMapping(
                sourceType,
                destinationType,
                out var registeredInvocation,
                out _)
            || registeredInvocation.SyntaxTree != invocation.SyntaxTree
            || registeredInvocation.Span != invocation.Span)
        {
            return mappedPairs.ToImmutableArray();
        }

        HashSet<string> ignoredProperties = GetIgnoredProperties(
            invocation,
            AutoMapperAnalysisHelpers.GetReverseMapInvocation(invocation),
            semanticModel);
        mappedPairs.RemoveAll(pair => ignoredProperties.Contains(pair.DestinationProperty.Name));

        AddConstructorOwnedMappedPropertyPairs(
            mappedPairs,
            invocation,
            sourceType,
            destinationType,
            semanticModel);

        var configuredMembers = new List<(
            IPropertySymbol DestinationProperty,
            InvocationExpressionSyntax ForMemberInvocation)>();

        foreach (InvocationExpressionSyntax chainedInvocation in
                 MappingChainAnalysisHelper.GetScopedChainInvocations(
                     invocation,
                     semanticModel,
                     stopAtReverseMapBoundary: true))
        {
            if (!MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                    chainedInvocation,
                    semanticModel,
                    "ForMember")
                || chainedInvocation.ArgumentList.Arguments.Count < 2
                || !TryGetDirectSelectedProperty(
                    chainedInvocation.ArgumentList.Arguments[0].Expression,
                    semanticModel,
                    out IPropertySymbol destinationProperty))
            {
                continue;
            }

            configuredMembers.Add((destinationProperty, chainedInvocation));
        }

        foreach ((IPropertySymbol destinationProperty, InvocationExpressionSyntax forMemberInvocation) in
                 configuredMembers)
        {
            int configurationCount = configuredMembers.Count(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate.DestinationProperty, destinationProperty));
            if (ignoredProperties.Contains(destinationProperty.Name)
                || configurationCount != 1
                || !TryGetDirectMapFromSourceProperty(
                    forMemberInvocation,
                    semanticModel,
                    out IPropertySymbol sourceProperty)
                || mappedPairs.Any(pair =>
                    !pair.IsConstructorOwned &&
                    SymbolEqualityComparer.Default.Equals(pair.DestinationProperty, destinationProperty)))
            {
                continue;
            }

            mappedPairs.Add(new MappedPropertyPair(sourceProperty, destinationProperty));
        }

        return mappedPairs.ToImmutableArray();
    }

    private static void AddConstructorOwnedMappedPropertyPairs(
        List<MappedPropertyPair> mappedPairs,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        SemanticModel semanticModel)
    {
        var configuredParameters = new List<(string Name, InvocationExpressionSyntax Invocation)>();
        foreach (InvocationExpressionSyntax chainedInvocation in MappingChainAnalysisHelper.GetScopedChainInvocations(
                     invocation,
                     semanticModel,
                     stopAtReverseMapBoundary: true))
        {
            if (!MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                    chainedInvocation,
                    semanticModel,
                    "ForCtorParam") ||
                chainedInvocation.ArgumentList.Arguments.Count < 2)
            {
                continue;
            }

            Optional<object?> parameterNameConstant = semanticModel.GetConstantValue(
                chainedInvocation.ArgumentList.Arguments[0].Expression);
            if (parameterNameConstant is { HasValue: true, Value: string parameterName } &&
                !string.IsNullOrWhiteSpace(parameterName) &&
                parameterName.IndexOf('.') < 0)
            {
                configuredParameters.Add((parameterName, chainedInvocation));
            }
        }

        string[] configuredParameterNames = configuredParameters
            .Select(parameter => parameter.Name)
            .ToArray();
        foreach ((string parameterName, InvocationExpressionSyntax forCtorParamInvocation) in configuredParameters)
        {
            if (configuredParameters.Count(candidate =>
                    string.Equals(candidate.Name, parameterName, StringComparison.Ordinal)) != 1 ||
                !TryGetDirectForCtorParamSourceProperty(
                    forCtorParamInvocation,
                    semanticModel,
                    out IPropertySymbol sourceProperty))
            {
                continue;
            }

            string? destinationPropertyName = AM020MappingConfigurationHelpers
                .GetDestinationPropertyNameForConstructorParameter(
                    destinationType,
                    sourceType,
                    parameterName,
                    configuredParameterNames,
                    semanticModel) ??
                AM020MappingConfigurationHelpers
                    .GetPositionalRecordPropertyNameForConstructorParameter(
                        destinationType,
                        sourceType,
                        parameterName,
                        configuredParameterNames,
                        semanticModel);
            if (destinationPropertyName == null)
            {
                continue;
            }

            IPropertySymbol? destinationProperty = AutoMapperAnalysisHelpers
                .GetMappableProperties(destinationType, requireSetter: false)
                .FirstOrDefault(property => string.Equals(
                    property.Name,
                    destinationPropertyName,
                    StringComparison.Ordinal));
            if (destinationProperty != null &&
                !mappedPairs.Any(pair =>
                    pair.IsConstructorOwned &&
                    SymbolEqualityComparer.Default.Equals(
                        pair.DestinationProperty,
                        destinationProperty)))
            {
                mappedPairs.Add(new MappedPropertyPair(
                    sourceProperty,
                    destinationProperty,
                    isConstructorOwned: true));
            }
        }
    }

    private static bool TryGetDirectForCtorParamSourceProperty(
        InvocationExpressionSyntax forCtorParamInvocation,
        SemanticModel semanticModel,
        out IPropertySymbol sourceProperty)
    {
        sourceProperty = null!;
        if (forCtorParamInvocation.ArgumentList.Arguments.Count < 2)
        {
            return false;
        }

        AnonymousFunctionExpressionSyntax? optionsLambda =
            forCtorParamInvocation.ArgumentList.Arguments[1].Expression as AnonymousFunctionExpressionSyntax;
        string? optionsParameterName = optionsLambda switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesizedLambda =>
                parenthesizedLambda.ParameterList.Parameters[0].Identifier.ValueText,
            _ => null
        };
        if (optionsLambda == null || optionsParameterName == null)
        {
            return false;
        }

        InvocationExpressionSyntax? mapFromInvocation = optionsLambda.Body switch
        {
            InvocationExpressionSyntax expressionBody => expressionBody,
            BlockSyntax
            {
                Statements.Count: 1
            } block when block.Statements[0] is ExpressionStatementSyntax
            {
                Expression: InvocationExpressionSyntax blockInvocation
            } => blockInvocation,
            _ => null
        };
        if (mapFromInvocation?.Expression is not MemberAccessExpressionSyntax mapFromAccess ||
            !IsDirectReceiver(mapFromAccess.Expression, optionsParameterName) ||
            !MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                mapFromInvocation,
                semanticModel,
                "MapFrom") ||
            mapFromInvocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        return TryGetDirectSelectedProperty(
            mapFromInvocation.ArgumentList.Arguments[0].Expression,
            semanticModel,
            out sourceProperty);
    }

    private static bool IsDirectReceiver(ExpressionSyntax receiver, string parameterName)
    {
        while (receiver is ParenthesizedExpressionSyntax parenthesized)
        {
            receiver = parenthesized.Expression;
        }

        return receiver is IdentifierNameSyntax identifier &&
               string.Equals(
                   identifier.Identifier.ValueText,
                   parameterName,
                   StringComparison.Ordinal);
    }

    private static bool TryGetDirectMapFromSourceProperty(
        InvocationExpressionSyntax forMemberInvocation,
        SemanticModel semanticModel,
        out IPropertySymbol sourceProperty)
    {
        sourceProperty = null!;
        if (forMemberInvocation.ArgumentList.Arguments.Count < 2)
        {
            return false;
        }

        ImmutableArray<InvocationExpressionSyntax> mapFromInvocations = forMemberInvocation.ArgumentList.Arguments[1]
            .Expression
            .DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Where(candidate => MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                candidate,
                semanticModel,
                "MapFrom"))
            .ToImmutableArray();

        return mapFromInvocations.Length == 1
               && mapFromInvocations[0].ArgumentList.Arguments.Count == 1
               && TryGetDirectSelectedProperty(
                   mapFromInvocations[0].ArgumentList.Arguments[0].Expression,
                   semanticModel,
                   out sourceProperty);
    }

    private static bool TryGetDirectSelectedProperty(
        ExpressionSyntax selector,
        SemanticModel semanticModel,
        out IPropertySymbol property)
    {
        property = null!;

        string? parameterName;
        MemberAccessExpressionSyntax? memberAccess;
        switch (selector)
        {
            case SimpleLambdaExpressionSyntax
            {
                Body: MemberAccessExpressionSyntax simpleMemberAccess
            } simpleLambda:
                parameterName = simpleLambda.Parameter.Identifier.ValueText;
                memberAccess = simpleMemberAccess;
                break;
            case ParenthesizedLambdaExpressionSyntax
            {
                ParameterList.Parameters.Count: 1,
                Body: MemberAccessExpressionSyntax parenthesizedMemberAccess
            } parenthesizedLambda:
                parameterName = parenthesizedLambda.ParameterList.Parameters[0].Identifier.ValueText;
                memberAccess = parenthesizedMemberAccess;
                break;
            default:
                return false;
        }

        if (memberAccess.Expression is not IdentifierNameSyntax identifier
            || !string.Equals(identifier.Identifier.ValueText, parameterName, StringComparison.Ordinal))
        {
            return false;
        }

        if (semanticModel.GetSymbolInfo(memberAccess).Symbol is not IPropertySymbol resolvedProperty)
        {
            return false;
        }

        property = resolvedProperty;
        return true;
    }

    private static IEnumerable<MappedPropertyPair>
        GetConventionMappedPropertyPairs(
            ITypeSymbol sourceType,
            ITypeSymbol destinationType
    )
    {
        Dictionary<string, IPropertySymbol> sourceProperties = AutoMapperAnalysisHelpers
            .GetMappableProperties(sourceType, requireSetter: false)
            .GroupBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (IPropertySymbol destinationProperty in
                 AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, requireSetter: false))
        {
            if (sourceProperties.TryGetValue(destinationProperty.Name, out IPropertySymbol? sourceProperty))
            {
                yield return new MappedPropertyPair(sourceProperty, destinationProperty);
            }
        }
    }

    private static bool IsSelfReference(ITypeSymbol propertyType, ITypeSymbol containingType)
    {
        return SymbolEqualityComparer.Default.Equals(propertyType, containingType)
               || SymbolEqualityComparer.Default.Equals(
                   AutoMapperAnalysisHelpers.GetCollectionElementType(propertyType),
                   containingType
               );
    }

    private static ITypeSymbol UnwrapCollectionElementType(ITypeSymbol type)
    {
        return AutoMapperAnalysisHelpers.GetCollectionElementType(type) ?? type;
    }

    private static bool IsSameTypePair(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        ITypeSymbol currentSourceType,
        ITypeSymbol currentDestinationType)
    {
        return SymbolEqualityComparer.Default.Equals(sourceType, currentSourceType)
               && SymbolEqualityComparer.Default.Equals(destinationType, currentDestinationType);
    }

    private static string GetTypePairKey(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        return string.Concat(
            sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            "->",
            destinationType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
        );
    }

    private static bool IsSimpleType(ITypeSymbol type)
    {
        return type.TypeKind == TypeKind.Enum ||
               AutoMapperAnalysisHelpers.IsBuiltInType(type);
    }

    private static bool AppliesToForwardDirection(
        InvocationExpressionSyntax mappingMethod,
        InvocationExpressionSyntax? reverseMapInvocation
    )
    {
        if (reverseMapInvocation == null)
        {
            return true;
        }

        // In Roslyn's fluent-call syntax tree, methods appended after ReverseMap()
        // are ancestors of the ReverseMap invocation node.
        return !reverseMapInvocation.Ancestors().Contains(mappingMethod);
    }
}

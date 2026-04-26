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

        // Check if recursion is constrained or the mapping body is custom-owned.
        if (
            HasMaxDepthConfiguration(invocationExpr, reverseMapInvocation)
            || HasPreserveReferencesConfiguration(invocationExpr, reverseMapInvocation)
            || MappingChainAnalysisHelper.HasCustomConstructionOrConversion(
                invocationExpr,
                context.SemanticModel,
                stopAtReverseMapBoundary: true)
            || HasCircularPropertyIgnored(
                invocationExpr,
                typeArguments.sourceType as INamedTypeSymbol,
                typeArguments.destinationType as INamedTypeSymbol,
                reverseMapInvocation,
                createMapRegistry
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
            createMapRegistry
        );
    }


    private static bool HasMaxDepthConfiguration(
        InvocationExpressionSyntax invocation,
        InvocationExpressionSyntax? reverseMapInvocation
    )
    {
        // Look for chained MaxDepth calls
        SyntaxNode? parent = invocation.Parent;
        while (parent != null)
        {
            if (
                parent is InvocationExpressionSyntax chainedCall
                && chainedCall.Expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Name.Identifier.ValueText == "MaxDepth"
                && AppliesToForwardDirection(chainedCall, reverseMapInvocation)
            )
            {
                return true;
            }

            parent = parent.Parent;
        }

        return false;
    }

    private static bool HasPreserveReferencesConfiguration(
        InvocationExpressionSyntax invocation,
        InvocationExpressionSyntax? reverseMapInvocation
    )
    {
        SyntaxNode? parent = invocation.Parent;
        while (parent != null)
        {
            if (
                parent is InvocationExpressionSyntax chainedCall
                && chainedCall.Expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Name.Identifier.ValueText == "PreserveReferences"
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
        CreateMapRegistry createMapRegistry
    )
    {
        if (sourceType == null || destinationType == null)
        {
            return false;
        }

        HashSet<string> ignoredProperties = GetIgnoredProperties(invocation, reverseMapInvocation);
        HashSet<string> selfReferencingDestProperties = FindRecursiveDestinationProperties(
            sourceType,
            destinationType,
            createMapRegistry);
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
        InvocationExpressionSyntax? reverseMapInvocation
    )
    {
        var ignoredProperties = new HashSet<string>();

        // Look for chained ForMember/ForPath calls with Ignore()
        SyntaxNode? parent = invocation.Parent;
        while (parent != null)
        {
            if (
                parent is InvocationExpressionSyntax chainedCall
                && chainedCall.Expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Name.Identifier.ValueText is "ForMember" or "ForPath"
                && AppliesToForwardDirection(chainedCall, reverseMapInvocation)
            )
            {
                // Check if this mapping call has Ignore()
                if (HasIgnoreConfiguration(chainedCall))
                {
                    string? propertyName = ExtractPropertyNameFromForMemberOrPath(chainedCall);
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

    private static bool HasIgnoreConfiguration(InvocationExpressionSyntax forMemberCall)
    {
        return forMemberCall.ArgumentList.Arguments.Count >= 2
               && forMemberCall.ArgumentList.Arguments[1].Expression
                   .DescendantNodesAndSelf()
                   .OfType<InvocationExpressionSyntax>()
                   .Any(invocation =>
                       invocation.Expression is MemberAccessExpressionSyntax memberAccess
                       && memberAccess.Name.Identifier.ValueText == "Ignore");
    }

    private static string? ExtractPropertyNameFromForMemberOrPath(
        InvocationExpressionSyntax forMemberCall
    )
    {
        if (forMemberCall.ArgumentList.Arguments.Count == 0)
        {
            return null;
        }

        return GetSelectedTopLevelMemberName(forMemberCall.ArgumentList.Arguments[0].Expression);
    }

    private static void AnalyzeRecursionRisk(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol? sourceType,
        ITypeSymbol? destinationType,
        CreateMapRegistry createMapRegistry
    )
    {
        if (sourceType == null || destinationType == null)
        {
            return;
        }

        // Check for self-referencing mapped properties on both sides to reduce false positives.
        if (HasMappedSelfReference(sourceType, destinationType))
        {
            var diagnostic = Diagnostic.Create(
                SelfReferencingTypeRule,
                invocation.GetLocation(),
                AutoMapperAnalysisHelpers.GetTypeName(sourceType),
                AutoMapperAnalysisHelpers.GetTypeName(destinationType)
            );

            context.ReportDiagnostic(diagnostic);
            return;
        }

        // Check for circular references reachable through convention-mapped member paths.
        if (HasMappedCircularReference(sourceType, destinationType, createMapRegistry))
        {
            var diagnostic = Diagnostic.Create(
                InfiniteRecursionRiskRule,
                invocation.GetLocation(),
                AutoMapperAnalysisHelpers.GetTypeName(sourceType),
                AutoMapperAnalysisHelpers.GetTypeName(destinationType)
            );

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool HasMappedSelfReference(ITypeSymbol? sourceType, ITypeSymbol? destinationType)
    {
        if (sourceType == null || destinationType == null)
        {
            return false;
        }

        foreach ((IPropertySymbol sourceProperty, IPropertySymbol destinationProperty) in
                 GetConventionMappedPropertyPairs(sourceType, destinationType))
        {
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

    private static HashSet<string> FindRecursiveDestinationProperties(
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType,
        CreateMapRegistry createMapRegistry
    )
    {
        var recursiveProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        visited.Add(GetTypePairKey(sourceType, destinationType));

        foreach ((IPropertySymbol sourceProperty, IPropertySymbol destinationProperty) in
                 GetConventionMappedPropertyPairs(sourceType, destinationType))
        {
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
        CreateMapRegistry createMapRegistry
    )
    {
        if (sourceType == null || destinationType == null)
        {
            return false;
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        return HasMappedCircularReferenceRecursive(sourceType, destinationType, visited, createMapRegistry, 0, 10);
    }

    private static bool HasMappedCircularReferenceRecursive(
        ITypeSymbol? currentSourceType,
        ITypeSymbol? currentDestinationType,
        HashSet<string> visited,
        CreateMapRegistry createMapRegistry,
        int depth,
        int maxDepth
    )
    {
        // Prevent stack overflow by limiting recursion depth
        if (depth > maxDepth || currentSourceType == null || currentDestinationType == null)
        {
            return false;
        }

        string typePairKey = GetTypePairKey(currentSourceType, currentDestinationType);
        if (visited.Contains(typePairKey))
        {
            return true;
        }

        visited.Add(typePairKey);

        foreach ((IPropertySymbol sourceProperty, IPropertySymbol destinationProperty) in
                 GetConventionMappedPropertyPairs(currentSourceType, currentDestinationType))
        {
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

    private static IEnumerable<(IPropertySymbol SourceProperty, IPropertySymbol DestinationProperty)>
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
                yield return (sourceProperty, destinationProperty);
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
        return type.SpecialType != SpecialType.None
               || type.TypeKind == TypeKind.Enum
               || IsNumericType(type)
               || type.Name == "String"
               || type.Name == "DateTime"
               || type.Name == "Guid";
    }

    private static bool IsNumericType(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Byte
                or SpecialType.System_SByte
                or SpecialType.System_Int16
                or SpecialType.System_UInt16
                or SpecialType.System_Int32
                or SpecialType.System_UInt32
                or SpecialType.System_Int64
                or SpecialType.System_UInt64
                or SpecialType.System_Single
                or SpecialType.System_Double
                or SpecialType.System_Decimal => true,
            _ => false
        };
    }

    private static string? GetSelectedTopLevelMemberName(SyntaxNode expression)
    {
        return expression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => GetSelectedTopLevelMemberName(simpleLambda.Body),
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda =>
                GetSelectedTopLevelMemberName(parenthesizedLambda.Body),
            MemberAccessExpressionSyntax memberAccess => GetTopLevelMemberName(memberAccess),
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) =>
                literal.Token.ValueText,
            _ => null
        };
    }

    private static string? GetTopLevelMemberName(MemberAccessExpressionSyntax memberAccess)
    {
        if (memberAccess.Expression is IdentifierNameSyntax)
        {
            return memberAccess.Name.Identifier.ValueText;
        }

        if (memberAccess.Expression is not MemberAccessExpressionSyntax currentAccess)
        {
            return null;
        }

        while (currentAccess.Expression is MemberAccessExpressionSyntax nestedAccess)
        {
            currentAccess = nestedAccess;
        }

        return currentAccess.Expression is IdentifierNameSyntax ? currentAccess.Name.Identifier.ValueText : null;
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

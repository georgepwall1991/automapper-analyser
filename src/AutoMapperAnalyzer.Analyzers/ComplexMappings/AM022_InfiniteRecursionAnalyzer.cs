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

        // Check if MaxDepth is configured or circular properties are ignored
        if (
            HasMaxDepthConfiguration(invocationExpr, reverseMapInvocation)
            || HasCircularPropertyIgnored(
                invocationExpr,
                typeArguments.sourceType as INamedTypeSymbol,
                typeArguments.destinationType as INamedTypeSymbol,
                reverseMapInvocation
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
            typeArguments.destinationType
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

    private static bool HasCircularPropertyIgnored(
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol? sourceType,
        INamedTypeSymbol? destinationType,
        InvocationExpressionSyntax? reverseMapInvocation
    )
    {
        if (sourceType == null || destinationType == null)
        {
            return false;
        }

        HashSet<string> ignoredProperties = GetIgnoredProperties(invocation, reverseMapInvocation);

        // Check circular properties between types
        HashSet<string> circularProperties = FindCircularProperties(sourceType, destinationType);
        if (circularProperties.Count > 0 && circularProperties.All(prop => ignoredProperties.Contains(prop)))
        {
            return true;
        }

        // Check self-referencing properties in destination type
        HashSet<string> selfReferencingDestProperties = FindSelfReferencingProperties(destinationType);
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
        ITypeSymbol? destinationType
    )
    {
        if (sourceType == null || destinationType == null)
        {
            return;
        }

        // Check for self-referencing types on both sides to reduce false positives.
        if (IsSelfReferencing(sourceType) && IsSelfReferencing(destinationType))
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

        // Check for circular references on both source and destination graphs.
        if (HasCircularReference(sourceType, sourceType) && HasCircularReference(destinationType, destinationType))
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

    private static bool IsSelfReferencing(ITypeSymbol? type)
    {
        if (type == null)
        {
            return false;
        }

        IEnumerable<IPropertySymbol> properties =
            AutoMapperAnalysisHelpers.GetMappableProperties(type, requireSetter: false);

        foreach (IPropertySymbol? property in properties)
        {
            // Check direct self-reference
            if (SymbolEqualityComparer.Default.Equals(property.Type, type))
            {
                return true;
            }

            // Check collection of self-type
            ITypeSymbol? elementType = AutoMapperAnalysisHelpers.GetCollectionElementType(property.Type);
            if (elementType != null && SymbolEqualityComparer.Default.Equals(elementType, type))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCircularReference(
        ITypeSymbol? sourceType,
        ITypeSymbol? destinationType
    )
    {
        if (sourceType == null || destinationType == null)
        {
            return false;
        }

        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        return HasCircularReferenceRecursive(sourceType, destinationType, visited, 0, 10);
    }

    private static bool HasCircularReferenceRecursive(
        ITypeSymbol? currentSourceType,
        ITypeSymbol? targetDestType,
        HashSet<ITypeSymbol> visited,
        int depth,
        int maxDepth
    )
    {
        // Prevent stack overflow by limiting recursion depth
        if (depth > maxDepth || currentSourceType == null || targetDestType == null)
        {
            return false;
        }

        // If we've already visited this type, we have a cycle
        if (visited.Contains(currentSourceType))
        {
            return true;
        }

        visited.Add(currentSourceType);

        IEnumerable<IPropertySymbol> properties =
            AutoMapperAnalysisHelpers.GetMappableProperties(currentSourceType, requireSetter: false);

        foreach (IPropertySymbol? property in properties)
        {
            ITypeSymbol propertyType = property.Type;

            // Skip value types and system types
            if (IsSimpleType(propertyType))
            {
                continue;
            }

            // Check collection element types
            ITypeSymbol? elementType = AutoMapperAnalysisHelpers.GetCollectionElementType(propertyType);
            if (elementType != null)
            {
                propertyType = elementType;
            }

            if (propertyType is ITypeSymbol namedPropertyType)
            {
                // If this property references back to our target destination type
                if (SymbolEqualityComparer.Default.Equals(namedPropertyType, targetDestType))
                {
                    return true;
                }

                // Recursively check this property's type
                if (
                    HasCircularReferenceRecursive(
                        namedPropertyType,
                        targetDestType,
                        new HashSet<ITypeSymbol>(visited, SymbolEqualityComparer.Default),
                        depth + 1,
                        maxDepth
                    )
                )
                {
                    return true;
                }
            }
        }

        visited.Remove(currentSourceType);
        return false;
    }

    private static HashSet<string> FindCircularProperties(
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType
    )
    {
        var circularProperties = new HashSet<string>();
        IEnumerable<IPropertySymbol> sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);

        foreach (IPropertySymbol? property in sourceProperties)
        {
            // Check if property type references back to destination type
            if (SymbolEqualityComparer.Default.Equals(property.Type, destinationType))
            {
                circularProperties.Add(property.Name);
                continue;
            }

            // Check collection element types
            ITypeSymbol? elementType = AutoMapperAnalysisHelpers.GetCollectionElementType(property.Type);
            if (
                elementType != null
                && SymbolEqualityComparer.Default.Equals(elementType, destinationType)
            )
            {
                circularProperties.Add(property.Name);
            }
        }

        return circularProperties;
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

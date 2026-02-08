using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Helpers;

/// <summary>
///     Provides shared chain-traversal and property-analysis methods used by both the AM004 analyzer and code fix provider.
/// </summary>
public static class MappingChainAnalysisHelper
{
    /// <summary>
    ///     Walks the fluent method chain starting from a mapping invocation, optionally stopping at a ReverseMap boundary.
    /// </summary>
    public static IEnumerable<InvocationExpressionSyntax> GetScopedChainInvocations(
        InvocationExpressionSyntax mappingInvocation,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        SyntaxNode? currentNode = mappingInvocation.Parent;

        while (currentNode is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if (stopAtReverseMapBoundary &&
                IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ReverseMap"))
            {
                break;
            }

            yield return chainedInvocation;
            currentNode = chainedInvocation.Parent;
        }
    }

    /// <summary>
    ///     Checks whether the given invocation is an AutoMapper method with the specified name,
    ///     checking both the resolved symbol and candidate symbols.
    /// </summary>
    public static bool IsAutoMapperMethodInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string methodName)
    {
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);

        if (IsAutoMapperMethod(symbolInfo.Symbol as IMethodSymbol, methodName))
        {
            return true;
        }

        foreach (ISymbol candidateSymbol in symbolInfo.CandidateSymbols)
        {
            if (IsAutoMapperMethod(candidateSymbol as IMethodSymbol, methodName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Verifies that a method symbol belongs to the AutoMapper namespace and has the expected name.
    /// </summary>
    public static bool IsAutoMapperMethod(IMethodSymbol? methodSymbol, string methodName)
    {
        if (methodSymbol == null || methodSymbol.Name != methodName)
        {
            return false;
        }

        string? namespaceName = methodSymbol.ContainingNamespace?.ToDisplayString();
        return namespaceName == "AutoMapper" ||
               (namespaceName?.StartsWith("AutoMapper.", StringComparison.Ordinal) ?? false);
    }

    /// <summary>
    ///     Resolves the TSource and TDest type arguments from a CreateMap invocation,
    ///     checking the resolved symbol, candidate symbols, and falling back to syntax-based resolution.
    /// </summary>
    public static (ITypeSymbol? sourceType, ITypeSymbol? destinationType) GetCreateMapTypeArguments(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);

        if (TryGetCreateMapTypeArgumentsFromMethod(symbolInfo.Symbol as IMethodSymbol, out ITypeSymbol? sourceType,
                out ITypeSymbol? destinationType))
        {
            return (sourceType, destinationType);
        }

        foreach (ISymbol candidateSymbol in symbolInfo.CandidateSymbols)
        {
            if (TryGetCreateMapTypeArgumentsFromMethod(candidateSymbol as IMethodSymbol, out sourceType,
                    out destinationType))
            {
                return (sourceType, destinationType);
            }
        }

        return AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
    }

    /// <summary>
    ///     Extracts type arguments from a single method symbol if it has exactly two type arguments.
    /// </summary>
    public static bool TryGetCreateMapTypeArgumentsFromMethod(
        IMethodSymbol? methodSymbol,
        out ITypeSymbol? sourceType,
        out ITypeSymbol? destinationType)
    {
        sourceType = null;
        destinationType = null;

        if (methodSymbol?.TypeArguments.Length != 2)
        {
            return false;
        }

        sourceType = methodSymbol.TypeArguments[0];
        destinationType = methodSymbol.TypeArguments[1];
        return true;
    }

    /// <summary>
    ///     Checks whether a ForMember's second argument references the given source property.
    /// </summary>
    public static bool IsSourcePropertyHandledByCustomMapping(
        InvocationExpressionSyntax mappingInvocation,
        string sourcePropertyName,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        foreach (InvocationExpressionSyntax chainedInvocation in GetScopedChainInvocations(
                     mappingInvocation, semanticModel, stopAtReverseMapBoundary))
        {
            if (!IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ForMember"))
            {
                continue;
            }

            if (ForMemberReferencesSourceProperty(chainedInvocation, sourcePropertyName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Checks whether the second argument of a ForMember call references a specific source property.
    /// </summary>
    public static bool ForMemberReferencesSourceProperty(
        InvocationExpressionSyntax forMemberInvocation,
        string sourcePropertyName)
    {
        if (forMemberInvocation.ArgumentList.Arguments.Count > 1)
        {
            return ContainsPropertyReference(forMemberInvocation.ArgumentList.Arguments[1].Expression, sourcePropertyName);
        }

        return false;
    }

    /// <summary>
    ///     Checks whether a ForCtorParam call's second argument references the given source property.
    /// </summary>
    public static bool IsSourcePropertyHandledByCtorParamMapping(
        InvocationExpressionSyntax mappingInvocation,
        string sourcePropertyName,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        foreach (InvocationExpressionSyntax chainedInvocation in GetScopedChainInvocations(
                     mappingInvocation, semanticModel, stopAtReverseMapBoundary))
        {
            if (!IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ForCtorParam") ||
                chainedInvocation.ArgumentList.Arguments.Count <= 1)
            {
                continue;
            }

            ExpressionSyntax ctorMappingArg = chainedInvocation.ArgumentList.Arguments[1].Expression;
            if (ContainsPropertyReference(ctorMappingArg, sourcePropertyName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Checks whether a source property is explicitly ignored via ForSourceMember + DoNotValidate.
    /// </summary>
    public static bool IsSourcePropertyExplicitlyIgnored(
        InvocationExpressionSyntax mappingInvocation,
        string sourcePropertyName,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        foreach (InvocationExpressionSyntax chainedInvocation in GetScopedChainInvocations(
                     mappingInvocation, semanticModel, stopAtReverseMapBoundary))
        {
            if (!IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ForSourceMember"))
            {
                continue;
            }

            if (IsForSourceMemberOfProperty(chainedInvocation, sourcePropertyName) &&
                HasDoNotValidateCall(chainedInvocation))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Detects ConstructUsing or ConvertUsing in the mapping chain.
    /// </summary>
    public static bool HasCustomConstructionOrConversion(
        InvocationExpressionSyntax mappingInvocation,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        foreach (InvocationExpressionSyntax chainedInvocation in GetScopedChainInvocations(
                     mappingInvocation, semanticModel, stopAtReverseMapBoundary))
        {
            if (IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ConstructUsing") ||
                IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ConvertUsing"))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Detects AutoMapper flattening patterns (e.g. Customer.Name -> CustomerName).
    /// </summary>
    public static bool IsFlatteningMatch(
        IPropertySymbol sourceProperty,
        IEnumerable<IPropertySymbol> destinationProperties)
    {
        if (AutoMapperAnalysisHelpers.IsBuiltInType(sourceProperty.Type))
        {
            return false;
        }

        IEnumerable<IPropertySymbol> nestedProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceProperty.Type, requireSetter: false);

        foreach (IPropertySymbol destinationProperty in destinationProperties)
        {
            if (!destinationProperty.Name.StartsWith(sourceProperty.Name, StringComparison.OrdinalIgnoreCase) ||
                destinationProperty.Name.Length <= sourceProperty.Name.Length)
            {
                continue;
            }

            string flattenedMemberName = destinationProperty.Name.Substring(sourceProperty.Name.Length);
            if (nestedProperties.Any(p => string.Equals(p.Name, flattenedMemberName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Checks whether a ForSourceMember call targets a specific property by examining the first argument.
    /// </summary>
    public static bool IsForSourceMemberOfProperty(
        InvocationExpressionSyntax forSourceMemberInvocation,
        string propertyName)
    {
        if (forSourceMemberInvocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        string? selectedMember = GetSelectedMemberName(forSourceMemberInvocation.ArgumentList.Arguments[0].Expression);
        return string.Equals(selectedMember, propertyName, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Checks for a DoNotValidate() call in the second argument of a ForSourceMember invocation.
    /// </summary>
    public static bool HasDoNotValidateCall(InvocationExpressionSyntax forSourceMemberInvocation)
    {
        if (forSourceMemberInvocation.ArgumentList.Arguments.Count <= 1)
        {
            return false;
        }

        ExpressionSyntax secondArg = forSourceMemberInvocation.ArgumentList.Arguments[1].Expression;
        return secondArg.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.ValueText == "DoNotValidate");
    }

    /// <summary>
    ///     Checks whether a syntax node references a property by name via member access.
    /// </summary>
    public static bool ContainsPropertyReference(SyntaxNode node, string propertyName)
    {
        if (node is MemberAccessExpressionSyntax rootMemberAccess &&
            rootMemberAccess.Name.Identifier.ValueText == propertyName)
        {
            return true;
        }

        return node.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
            .Any(memberAccess => memberAccess.Name.Identifier.ValueText == propertyName);
    }

    /// <summary>
    ///     Extracts the member name from a lambda or member access expression.
    /// </summary>
    public static string? GetSelectedMemberName(ExpressionSyntax expression)
    {
        return expression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda when simpleLambda.Body is MemberAccessExpressionSyntax memberAccess =>
                memberAccess.Name.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda
                when parenthesizedLambda.Body is MemberAccessExpressionSyntax memberAccess =>
                memberAccess.Name.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => null
        };
    }

    /// <summary>
    ///     Returns all source properties that have no corresponding destination property and are not handled
    ///     by custom mapping, constructor parameter mapping, explicit ignore, or flattening.
    /// </summary>
    public static List<IPropertySymbol> GetUnmappedSourceProperties(
        InvocationExpressionSyntax mappingInvocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        IEnumerable<IPropertySymbol> sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        IEnumerable<IPropertySymbol> destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, false);

        var unmappedProperties = new List<IPropertySymbol>();

        foreach (IPropertySymbol sourceProp in sourceProperties)
        {
            if (destinationProperties.Any(p => string.Equals(p.Name, sourceProp.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (IsFlatteningMatch(sourceProp, destinationProperties))
            {
                continue;
            }

            if (IsSourcePropertyHandledByCustomMapping(mappingInvocation, sourceProp.Name, semanticModel, stopAtReverseMapBoundary))
            {
                continue;
            }

            if (IsSourcePropertyHandledByCtorParamMapping(mappingInvocation, sourceProp.Name, semanticModel, stopAtReverseMapBoundary))
            {
                continue;
            }

            if (IsSourcePropertyExplicitlyIgnored(mappingInvocation, sourceProp.Name, semanticModel, stopAtReverseMapBoundary))
            {
                continue;
            }

            unmappedProperties.Add(sourceProp);
        }

        return unmappedProperties;
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        // RegisterSyntaxNodeAction fires on every invocation in the compilation, not just mapping
        // configuration, so this binds symbols for calls that cannot possibly match. Compare the
        // invoked name syntactically first: unrecognised shapes fall through to the semantic check, so
        // the gate can only skip work, never change an answer.
        if (!InvokedNameCouldMatch(invocation, methodName))
        {
            return false;
        }

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
    ///     Cheap syntactic gate for an invocation's simple name. Returns true for shapes whose name
    ///     cannot be read, so an unfamiliar construct is still resolved semantically rather than
    ///     silently skipped.
    /// </summary>
    private static bool InvokedNameCouldMatch(InvocationExpressionSyntax invocation, string methodName)
    {
        SimpleNameSyntax? invokedName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name,
            SimpleNameSyntax simpleName => simpleName,
            _ => null
        };

        return invokedName == null ||
               string.Equals(invokedName.Identifier.ValueText, methodName, StringComparison.Ordinal);
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

            if (IsForSourceMemberOfProperty(chainedInvocation, sourcePropertyName, semanticModel) &&
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
        string propertyName,
        SemanticModel? semanticModel = null)
    {
        if (forSourceMemberInvocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        string? selectedMember = GetSelectedMemberName(
            forSourceMemberInvocation.ArgumentList.Arguments[0].Expression,
            semanticModel);
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
        return node.DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Select(GetTopLevelSourceMemberName)
            .Any(memberName => string.Equals(memberName, propertyName, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Extracts the member name from a lambda or member access expression.
    /// </summary>
    public static string? GetSelectedMemberName(SyntaxNode expression, SemanticModel? semanticModel = null)
    {
        return expression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => GetSelectedMemberName(simpleLambda.Body, semanticModel),
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda
                => GetSelectedMemberName(parenthesizedLambda.Body, semanticModel),
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) =>
                GetTopLevelMemberName(literal.Token.ValueText),
            ExpressionSyntax expressionSyntax when TryGetStringConstant(
                expressionSyntax,
                semanticModel,
                out string memberPath) => GetTopLevelMemberName(memberPath),
            _ => null
        };
    }

    private static bool TryGetStringConstant(
        ExpressionSyntax expression,
        SemanticModel? semanticModel,
        out string value)
    {
        value = string.Empty;
        if (semanticModel == null)
        {
            return false;
        }

        Optional<object?> constantValue = semanticModel.GetConstantValue(expression);
        if (constantValue is { HasValue: true, Value: string stringValue })
        {
            value = stringValue;
            return true;
        }

        return false;
    }

    private static string? GetTopLevelMemberName(string memberPath)
    {
        string topLevelMemberName = memberPath.Split('.')[0].Trim();
        return string.IsNullOrWhiteSpace(topLevelMemberName) ? null : topLevelMemberName;
    }

    private static string? GetTopLevelSourceMemberName(MemberAccessExpressionSyntax memberAccess)
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

        return currentAccess.Expression is IdentifierNameSyntax
            ? currentAccess.Name.Identifier.ValueText
            : null;
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

        IncludeMembersScope includeMembers =
            GetIncludeMembersScope(mappingInvocation, semanticModel, stopAtReverseMapBoundary, destinationType);

        var unmappedProperties = new List<IPropertySymbol>();

        // An IncludeMembers selector we cannot resolve may consume any source member, so reporting
        // data loss here would be a guess. Stay quiet for the whole mapping instead.
        if (includeMembers.HasUnresolvedMember)
        {
            return unmappedProperties;
        }

        foreach (IPropertySymbol sourceProp in sourceProperties)
        {
            if (destinationProperties.Any(p => string.Equals(p.Name, sourceProp.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // IncludeMembers(s => s.Member) hands the member's own properties to AutoMapper, so the
            // member itself is consumed rather than dropped.
            if (includeMembers.ConsumesSourceMember(sourceProp.Name))
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

    /// <summary>
    ///     Collects the source members pulled into a mapping by IncludeMembers(...). AutoMapper flattens
    ///     each included member's own properties into the destination, so those properties are available
    ///     to the map even though the source type does not declare them.
    /// </summary>
    internal static IncludeMembersScope GetIncludeMembersScope(
        InvocationExpressionSyntax mappingInvocation,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary,
        ITypeSymbol? destinationType = null)
    {
        List<IncludedMember>? includedTypes = null;
        HashSet<string>? includedMemberNames = null;
        var hasUnresolvedMember = false;

        // AutoMapper replaces TypeMap.IncludedMembers on every IncludeMembers call rather than
        // accumulating, so only the final call in the chain is effective at runtime.
        InvocationExpressionSyntax? effectiveIncludeMembers = null;
        foreach (InvocationExpressionSyntax chainedInvocation in GetScopedChainInvocations(
                     mappingInvocation, semanticModel, stopAtReverseMapBoundary))
        {
            if (IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "IncludeMembers"))
            {
                effectiveIncludeMembers = chainedInvocation;
            }
        }

        if (effectiveIncludeMembers != null)
        {
            foreach (ArgumentSyntax argument in effectiveIncludeMembers.ArgumentList.Arguments)
            {
                if (GetIncludeMemberBody(argument.Expression) is not MemberAccessExpressionSyntax body)
                {
                    // Unrecognized selector shape: fail closed so callers stay quiet instead of
                    // reporting members the included type may well supply.
                    hasUnresolvedMember = true;
                    continue;
                }

                ITypeSymbol? memberType = semanticModel.GetTypeInfo(body).Type;
                string? topLevelMemberName = GetTopLevelSourceMemberName(body);

                if (memberType is null or IErrorTypeSymbol || string.IsNullOrEmpty(topLevelMemberName))
                {
                    hasUnresolvedMember = true;
                    continue;
                }

                (includedTypes ??= []).Add(ResolveIncludedMember(memberType, destinationType, semanticModel));
                (includedMemberNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase))
                    .Add(topLevelMemberName!);
            }
        }

        if (includedTypes == null && !hasUnresolvedMember)
        {
            return IncludeMembersScope.Empty;
        }

        return new IncludeMembersScope(includedTypes, includedMemberNames, hasUnresolvedMember);
    }

    /// <summary>
    ///     Pairs an included member type with its uniquely registered child map, when one exists. The
    ///     child map can supply a destination member the included type does not declare under that name.
    /// </summary>
    private static IncludedMember ResolveIncludedMember(
        ITypeSymbol memberType,
        ITypeSymbol? destinationType,
        SemanticModel semanticModel)
    {
        if (destinationType == null)
        {
            return new IncludedMember(memberType, null, null);
        }

        CreateMapRegistry registry = CreateMapRegistry.FromCompilation(semanticModel.Compilation);
        return registry.TryGetUniqueForwardMapping(
                   memberType,
                   destinationType,
                   out InvocationExpressionSyntax childInvocation,
                   out SemanticModel childSemanticModel)
            ? new IncludedMember(memberType, childInvocation, childSemanticModel)
            : new IncludedMember(memberType, null, null);
    }

    /// <summary>
    ///     Checks whether a map explicitly supplies a destination member through
    ///     ForMember/ForPath(... MapFrom(...)). Only the positive direction is modelled: proving a member
    ///     IS supplied can only suppress a diagnostic, whereas inferring that a child map fails to supply
    ///     one would add diagnostics and risks Error-severity false positives on valid mappings.
    /// </summary>
    private static bool IsDestinationMemberSuppliedByMap(
        InvocationExpressionSyntax mapInvocation,
        string destinationMemberName,
        SemanticModel semanticModel)
    {
        foreach (InvocationExpressionSyntax chainedInvocation in GetScopedChainInvocations(
                     mapInvocation, semanticModel, stopAtReverseMapBoundary: true))
        {
            if (chainedInvocation.ArgumentList.Arguments.Count <= 1 ||
                (!IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ForMember") &&
                 !IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ForPath")))
            {
                continue;
            }

            // Resolve against the top-level member: ForPath(d => d.Details.Name, ...) configures a
            // nested path and supplies Details, not a top-level Name.
            string? selectedMember = GetTopLevelSelectedMemberName(
                chainedInvocation.ArgumentList.Arguments[0].Expression, semanticModel);
            if (!string.Equals(selectedMember, destinationMemberName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (chainedInvocation.ArgumentList.Arguments[1].Expression.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                                   memberAccess.Name.Identifier.ValueText == "MapFrom"))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Resolves the top-level member a destination selector designates. Lambda member accesses walk
    ///     back to the root member so a nested path is not mistaken for the member it terminates in;
    ///     string and nameof forms already resolve to their top-level segment.
    /// </summary>
    private static string? GetTopLevelSelectedMemberName(
        ExpressionSyntax selectorExpression,
        SemanticModel semanticModel)
    {
        ExpressionSyntax? body = selectorExpression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Body as ExpressionSyntax,
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda =>
                parenthesizedLambda.Body as ExpressionSyntax,
            _ => null
        };

        while (body is ParenthesizedExpressionSyntax parenthesized)
        {
            body = parenthesized.Expression;
        }

        return body is MemberAccessExpressionSyntax memberAccess
            ? GetTopLevelSourceMemberName(memberAccess)
            : GetSelectedMemberName(selectorExpression, semanticModel);
    }

    /// <summary>
    ///     An included member type paired with its uniquely registered child map, when one exists.
    /// </summary>
    internal readonly struct IncludedMember
    {
        internal IncludedMember(
            ITypeSymbol type,
            InvocationExpressionSyntax? map,
            SemanticModel? semanticModel)
        {
            Type = type;
            Map = map;
            SemanticModel = semanticModel;
        }

        internal ITypeSymbol Type { get; }
        internal InvocationExpressionSyntax? Map { get; }
        internal SemanticModel? SemanticModel { get; }
    }

    /// <summary>
    ///     Unwraps the lambda selector passed to IncludeMembers, returning the member access it selects.
    ///     Non-lambda and non-member-access shapes return null so callers fail closed.
    /// </summary>
    private static ExpressionSyntax? GetIncludeMemberBody(ExpressionSyntax argumentExpression)
    {
        ExpressionSyntax? body = argumentExpression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Body as ExpressionSyntax,
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda =>
                parenthesizedLambda.Body as ExpressionSyntax,
            _ => null
        };

        // Only parentheses and the null-forgiving operator are peeled, because neither changes which
        // member or type the selector designates. Every other shape - casts, arrays, collection
        // expressions, spreads, variables, method calls - is left unrecognized on purpose so it takes
        // the fail-closed path. Interpreting those shapes is what produced Error-severity false
        // positives; declining to interpret them can only suppress.
        while (true)
        {
            switch (body)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    body = parenthesized.Expression;
                    continue;
                case PostfixUnaryExpressionSyntax suppression
                    when suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    body = suppression.Operand;
                    continue;
            }

            break;
        }

        return body;
    }

    /// <summary>
    ///     Checks whether a destination member name is satisfied by flattening a source property's own
    ///     nested member (e.g. a property exposing Address.City satisfies AddressCity).
    /// </summary>
    private static bool IsFlatteningMatchForName(IPropertySymbol sourceProperty, string destinationPropertyName)
    {
        if (AutoMapperAnalysisHelpers.IsBuiltInType(sourceProperty.Type))
        {
            return false;
        }

        if (!destinationPropertyName.StartsWith(sourceProperty.Name, StringComparison.OrdinalIgnoreCase) ||
            destinationPropertyName.Length <= sourceProperty.Name.Length)
        {
            return false;
        }

        string flattenedMemberName = destinationPropertyName.Substring(sourceProperty.Name.Length);
        return AutoMapperAnalysisHelpers.GetMappableProperties(sourceProperty.Type, requireSetter: false)
            .Any(p => string.Equals(p.Name, flattenedMemberName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     The source members contributed to a mapping by IncludeMembers(...).
    /// </summary>
    internal sealed class IncludeMembersScope
    {
        internal static readonly IncludeMembersScope Empty = new(null, null, false);

        private readonly HashSet<string>? _includedMemberNames;
        private readonly List<IncludedMember>? _includedTypes;

        internal IncludeMembersScope(
            List<IncludedMember>? includedTypes,
            HashSet<string>? includedMemberNames,
            bool hasUnresolvedMember)
        {
            _includedTypes = includedTypes;
            _includedMemberNames = includedMemberNames;
            HasUnresolvedMember = hasUnresolvedMember;
        }

        /// <summary>
        ///     True when an IncludeMembers selector could not be resolved. Callers stay quiet rather than
        ///     report members the unresolved include might supply.
        /// </summary>
        internal bool HasUnresolvedMember { get; }

        /// <summary>
        ///     True when the mapping consumes the named source member via IncludeMembers.
        /// </summary>
        internal bool ConsumesSourceMember(string sourceMemberName)
        {
            return _includedMemberNames?.Contains(sourceMemberName) == true;
        }

        /// <summary>
        ///     True when an included member supplies the named destination member, directly or through
        ///     AutoMapper's flattening convention. An unresolved include satisfies every member so the
        ///     caller fails closed.
        ///     <para>
        ///     Deliberately reasons about the included type's shape only, never its child map. Deciding
        ///     what a child map actually supplies means modelling its full member resolution
        ///     (ForMember/MapFrom, ForAllMembers, reverse-generated registrations, semantic Ignore
        ///     binding); approximating it produced Error-severity false positives. Shape-only keeps this
        ///     purely suppressing, so it can never add a diagnostic. Known cost: a child map that ignores
        ///     or does not supply the member is not diagnosed here.
        ///     </para>
        /// </summary>
        internal bool SatisfiesDestinationMember(string destinationMemberName)
        {
            if (HasUnresolvedMember)
            {
                return true;
            }

            if (_includedTypes == null)
            {
                return false;
            }

            foreach (IncludedMember includedMember in _includedTypes)
            {
                if (includedMember.Map != null &&
                    includedMember.SemanticModel != null &&
                    IsDestinationMemberSuppliedByMap(
                        includedMember.Map, destinationMemberName, includedMember.SemanticModel))
                {
                    return true;
                }

                List<IPropertySymbol> includedProperties = AutoMapperAnalysisHelpers
                    .GetMappableProperties(includedMember.Type, requireSetter: false)
                    .ToList();

                if (includedProperties.Any(p =>
                        string.Equals(p.Name, destinationMemberName, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                if (includedProperties.Any(p => IsFlatteningMatchForName(p, destinationMemberName)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

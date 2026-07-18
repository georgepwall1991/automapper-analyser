using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Helpers;

internal readonly struct MappableSourceMember
{
    public MappableSourceMember(
        string destinationName,
        string sourceName,
        ITypeSymbol type,
        bool requiresInvocation)
    {
        DestinationName = destinationName;
        SourceName = sourceName;
        Type = type;
        RequiresInvocation = requiresInvocation;
    }

    public string DestinationName { get; }
    public string SourceName { get; }
    public ITypeSymbol Type { get; }
    public bool RequiresInvocation { get; }
}

/// <summary>
///     Shared helpers for identifying whether destination members are explicitly configured
///     and whether mapping construction/conversion methods apply to the forward direction.
/// </summary>
internal static class AM020MappingConfigurationHelpers
{
    public static bool IsDestinationPropertyExplicitlyConfigured(
        InvocationExpressionSyntax createMapInvocation,
        string destinationPropertyName,
        SemanticModel semanticModel)
    {
        foreach (InvocationExpressionSyntax mappingConfigCall in GetMappingConfigurationCalls(createMapInvocation, semanticModel))
        {
            if (mappingConfigCall.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            string? selectedMember = GetSelectedTopLevelMemberNameCore(
                mappingConfigCall.ArgumentList.Arguments[0].Expression,
                semanticModel);
            if (string.Equals(selectedMember, destinationPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasCustomConstructionOrConversion(
        InvocationExpressionSyntax createMapInvocation,
        SemanticModel semanticModel)
    {
        return MappingChainAnalysisHelper.HasCustomConstructionOrConversion(
            createMapInvocation,
            semanticModel,
            ShouldStopAtReverseMapBoundary(createMapInvocation, semanticModel));
    }

    public static string? GetDestinationPropertyNameForConstructorParameter(
        ITypeSymbol destinationType,
        ITypeSymbol sourceType,
        string constructorParameterName,
        IReadOnlyCollection<string> configuredConstructorParameterNames,
        SemanticModel semanticModel)
    {
        if (destinationType is not INamedTypeSymbol namedDestinationType ||
            string.IsNullOrWhiteSpace(constructorParameterName))
        {
            return null;
        }

        HashSet<string> sourceMemberNames = GetMappableSourceMemberNames(sourceType);
        var configuredParameterNames = new HashSet<string>(
            configuredConstructorParameterNames,
            StringComparer.Ordinal);
        IMethodSymbol? constructor = GetSelectedConstructor(
            namedDestinationType,
            sourceType,
            sourceMemberNames,
            configuredParameterNames);
        IParameterSymbol? constructorParameter = constructor?.Parameters.FirstOrDefault(parameter =>
            string.Equals(parameter.Name, constructorParameterName, StringComparison.Ordinal));
        if (constructor == null || constructorParameter == null)
        {
            return null;
        }

        var assignedPropertyNames = new HashSet<string>(StringComparer.Ordinal);
        var independentlyWrittenPropertyNames = new HashSet<string>(StringComparer.Ordinal);
        bool hasConstructorSyntax = false;
        foreach (SyntaxReference syntaxReference in constructor.DeclaringSyntaxReferences)
        {
            var directAssignmentRightSides = new List<ExpressionSyntax>();
            hasConstructorSyntax = true;
            SyntaxNode constructorSyntax = syntaxReference.GetSyntax();
            SemanticModel constructorSemanticModel =
                semanticModel.Compilation.GetSemanticModel(constructorSyntax.SyntaxTree);
            foreach (AssignmentExpressionSyntax assignment in constructorSyntax
                         .DescendantNodes()
                         .OfType<AssignmentExpressionSyntax>())
            {
                if (!IsSynchronousConstructorAssignment(assignment, constructorSyntax) ||
                    !IsAssignmentToConstructedInstance(assignment) ||
                    constructorSemanticModel.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property ||
                    !SymbolEqualityComparer.Default.Equals(
                        property.ContainingType.OriginalDefinition,
                        namedDestinationType.OriginalDefinition))
                {
                    continue;
                }

                if (IsDirectConstructorAssignment(assignment, constructorSyntax) &&
                    SymbolEqualityComparer.Default.Equals(
                        constructorSemanticModel.GetSymbolInfo(assignment.Right).Symbol?.OriginalDefinition,
                        constructorParameter.OriginalDefinition))
                {
                    assignedPropertyNames.Add(property.Name);
                    directAssignmentRightSides.Add(assignment.Right);
                }
                else
                {
                    independentlyWrittenPropertyNames.Add(property.Name);
                }
            }

            bool hasAdditionalParameterUse = constructorSyntax
                .DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Any(identifier =>
                    SymbolEqualityComparer.Default.Equals(
                        constructorSemanticModel.GetSymbolInfo(identifier).Symbol?.OriginalDefinition,
                        constructorParameter.OriginalDefinition) &&
                    !directAssignmentRightSides.Any(rightSide => rightSide.Span.Contains(identifier.Span)));
            if (hasAdditionalParameterUse)
            {
                return null;
            }
        }

        if (!hasConstructorSyntax || assignedPropertyNames.Count != 1)
        {
            return null;
        }

        string constructorOwnedPropertyName = assignedPropertyNames.Single();
        if (independentlyWrittenPropertyNames.Contains(constructorOwnedPropertyName))
        {
            return null;
        }

        return constructorOwnedPropertyName;
    }

    public static IReadOnlyList<ITypeSymbol> GetConstructorParameterTypes(
        ITypeSymbol destinationType,
        ITypeSymbol sourceType,
        string constructorParameterName,
        IReadOnlyCollection<string> configuredConstructorParameterNames)
    {
        if (destinationType is not INamedTypeSymbol namedDestinationType ||
            string.IsNullOrWhiteSpace(constructorParameterName))
        {
            return Array.Empty<ITypeSymbol>();
        }

        HashSet<string> sourceMemberNames = GetMappableSourceMemberNames(sourceType);
        var configuredParameterNames = new HashSet<string>(
            configuredConstructorParameterNames,
            StringComparer.Ordinal);
        IMethodSymbol? selectedConstructor = GetSelectedConstructor(
            namedDestinationType,
            sourceType,
            sourceMemberNames,
            configuredParameterNames);
        IParameterSymbol? selectedParameter = selectedConstructor?.Parameters.FirstOrDefault(parameter =>
            string.Equals(parameter.Name, constructorParameterName, StringComparison.Ordinal));
        return selectedParameter == null
            ? Array.Empty<ITypeSymbol>()
            : new[] { selectedParameter.Type };
    }

    private static HashSet<string> GetMappableSourceMemberNames(ITypeSymbol sourceType)
    {
        var sourceMemberNames = new HashSet<string>(
            GetMappableSourceProperties(sourceType)
                .Select(property => property.Name),
            StringComparer.OrdinalIgnoreCase);
        for (INamedTypeSymbol? currentSourceType = sourceType as INamedTypeSymbol;
             currentSourceType != null;
             currentSourceType = currentSourceType.BaseType)
        {
            foreach (IFieldSymbol sourceField in currentSourceType.GetMembers().OfType<IFieldSymbol>())
            {
                if (!sourceField.IsStatic &&
                    sourceField.DeclaredAccessibility == Accessibility.Public)
                {
                    sourceMemberNames.Add(sourceField.Name);
                }
            }
        }

        foreach (IMethodSymbol sourceMethod in GetMappableSourceMethods(sourceType))
        {
            sourceMemberNames.Add(sourceMethod.Name);
            if (sourceMethod.Name.StartsWith("Get", StringComparison.Ordinal) &&
                sourceMethod.Name.Length > 3)
            {
                sourceMemberNames.Add(sourceMethod.Name.Substring(3));
            }
        }

        return sourceMemberNames;
    }

    public static bool HasMappableSourceMember(
        ITypeSymbol sourceType,
        string sourceMemberName)
    {
        return GetMappableSourceMemberNames(sourceType).Contains(sourceMemberName);
    }

    public static MappableSourceMember? GetMappableSourceMember(
        ITypeSymbol sourceType,
        string destinationMemberName)
    {
        var exactMembers = new List<MappableSourceMember>();
        foreach (IPropertySymbol sourceProperty in GetMappableSourceProperties(sourceType))
        {
            exactMembers.Add(new MappableSourceMember(
                sourceProperty.Name,
                sourceProperty.Name,
                sourceProperty.Type,
                requiresInvocation: false));
        }

        for (INamedTypeSymbol? currentSourceType = sourceType as INamedTypeSymbol;
             currentSourceType != null;
             currentSourceType = currentSourceType.BaseType)
        {
            foreach (IFieldSymbol sourceField in currentSourceType.GetMembers().OfType<IFieldSymbol>())
            {
                if (!sourceField.IsStatic &&
                    sourceField.DeclaredAccessibility == Accessibility.Public)
                {
                    exactMembers.Add(new MappableSourceMember(
                        sourceField.Name,
                        sourceField.Name,
                        sourceField.Type,
                        requiresInvocation: false));
                }
            }
        }

        var getMethodAliases = new List<MappableSourceMember>();
        foreach (IMethodSymbol sourceMethod in GetMappableSourceMethods(sourceType))
        {
            exactMembers.Add(new MappableSourceMember(
                sourceMethod.Name,
                sourceMethod.Name,
                sourceMethod.ReturnType,
                requiresInvocation: true));
            if (sourceMethod.Name.StartsWith("Get", StringComparison.Ordinal) &&
                sourceMethod.Name.Length > 3)
            {
                getMethodAliases.Add(new MappableSourceMember(
                    sourceMethod.Name.Substring(3),
                    sourceMethod.Name,
                    sourceMethod.ReturnType,
                    requiresInvocation: true));
            }
        }

        foreach (MappableSourceMember sourceMember in exactMembers)
        {
            if (string.Equals(
                    sourceMember.DestinationName,
                    destinationMemberName,
                    StringComparison.Ordinal))
            {
                return sourceMember;
            }
        }

        foreach (MappableSourceMember sourceMember in exactMembers)
        {
            if (string.Equals(
                    sourceMember.DestinationName,
                    destinationMemberName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return sourceMember;
            }
        }

        foreach (MappableSourceMember sourceMember in getMethodAliases)
        {
            if (string.Equals(
                    sourceMember.DestinationName,
                    destinationMemberName,
                    StringComparison.Ordinal))
            {
                return sourceMember;
            }
        }

        foreach (MappableSourceMember sourceMember in getMethodAliases)
        {
            if (string.Equals(
                    sourceMember.DestinationName,
                    destinationMemberName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return sourceMember;
            }
        }

        return null;
    }

    private static IEnumerable<IPropertySymbol> GetMappableSourceProperties(ITypeSymbol sourceType)
    {
        return AutoMapperAnalysisHelpers
            .GetMappableProperties(sourceType, requireSetter: false)
            .Where(property =>
                property.DeclaredAccessibility == Accessibility.Public &&
                property.GetMethod?.DeclaredAccessibility == Accessibility.Public);
    }

    private static IEnumerable<IMethodSymbol> GetMappableSourceMethods(ITypeSymbol sourceType)
    {
        for (INamedTypeSymbol? currentSourceType = sourceType as INamedTypeSymbol;
             currentSourceType != null && currentSourceType.SpecialType != SpecialType.System_Object;
             currentSourceType = currentSourceType.BaseType)
        {
            foreach (IMethodSymbol sourceMethod in currentSourceType.GetMembers().OfType<IMethodSymbol>())
            {
                if (sourceMethod.MethodKind == MethodKind.Ordinary &&
                    sourceMethod.DeclaredAccessibility == Accessibility.Public &&
                    !sourceMethod.IsStatic &&
                    !sourceMethod.IsGenericMethod &&
                    sourceMethod.Parameters.Length == 0 &&
                    !sourceMethod.ReturnsVoid)
                {
                    yield return sourceMethod;
                }
            }
        }
    }

    private static IMethodSymbol? GetSelectedConstructor(
        INamedTypeSymbol destinationType,
        ITypeSymbol sourceType,
        ISet<string> sourceMemberNames,
        ISet<string> configuredParameterNames)
    {
        return destinationType.InstanceConstructors
            .OrderByDescending(constructor => constructor.Parameters.Length)
            .FirstOrDefault(constructor => IsConstructorSelectable(
                constructor,
                sourceType,
                sourceMemberNames,
                configuredParameterNames));
    }

    private static bool IsConstructorSelectable(
        IMethodSymbol constructor,
        ITypeSymbol sourceType,
        ISet<string> sourceMemberNames,
        ISet<string> configuredParameterNames)
    {
        return !constructor.Parameters.Any(parameter =>
            !parameter.IsOptional &&
            !parameter.HasExplicitDefaultValue &&
            !parameter.IsParams &&
            !sourceMemberNames.Contains(parameter.Name) &&
            !HasMappableFlattenedSourcePath(sourceType, parameter.Name) &&
            !configuredParameterNames.Contains(parameter.Name));
    }

    private static bool HasMappableFlattenedSourcePath(
        ITypeSymbol sourceType,
        string destinationMemberName)
    {
        foreach (IPropertySymbol sourceProperty in GetMappableSourceProperties(sourceType))
        {
            if (IsFlattenedSourcePathPrefix(
                    sourceProperty.Name,
                    sourceProperty.Type,
                    destinationMemberName))
            {
                return true;
            }
        }

        for (INamedTypeSymbol? currentSourceType = sourceType as INamedTypeSymbol;
             currentSourceType != null;
             currentSourceType = currentSourceType.BaseType)
        {
            foreach (IFieldSymbol sourceField in currentSourceType.GetMembers().OfType<IFieldSymbol>())
            {
                if (!sourceField.IsStatic &&
                    sourceField.DeclaredAccessibility == Accessibility.Public &&
                    IsFlattenedSourcePathPrefix(
                        sourceField.Name,
                        sourceField.Type,
                        destinationMemberName))
                {
                    return true;
                }
            }
        }

        foreach (IMethodSymbol sourceMethod in GetMappableSourceMethods(sourceType))
        {
            if (IsFlattenedSourcePathPrefix(
                    sourceMethod.Name,
                    sourceMethod.ReturnType,
                    destinationMemberName))
            {
                return true;
            }

            if (sourceMethod.Name.StartsWith("Get", StringComparison.Ordinal) &&
                sourceMethod.Name.Length > 3 &&
                IsFlattenedSourcePathPrefix(
                    sourceMethod.Name.Substring(3),
                    sourceMethod.ReturnType,
                    destinationMemberName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFlattenedSourcePathPrefix(
        string sourceMemberName,
        ITypeSymbol sourceMemberType,
        string destinationMemberName)
    {
        if (AutoMapperAnalysisHelpers.IsBuiltInType(sourceMemberType) ||
            !destinationMemberName.StartsWith(sourceMemberName, StringComparison.OrdinalIgnoreCase) ||
            destinationMemberName.Length <= sourceMemberName.Length)
        {
            return false;
        }

        string remainingMemberName = destinationMemberName.Substring(sourceMemberName.Length);
        return GetMappableSourceMemberNames(sourceMemberType).Contains(remainingMemberName) ||
               HasMappableFlattenedSourcePath(sourceMemberType, remainingMemberName);
    }

    public static bool CanMapDestinationPropertyAfterConstruction(
        ITypeSymbol destinationType,
        string destinationPropertyName)
    {
        return AutoMapperAnalysisHelpers
            .GetMappableProperties(destinationType, requireSetter: true)
            .Any(property => string.Equals(
                property.Name,
                destinationPropertyName,
                StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<InvocationExpressionSyntax> GetMappingConfigurationCalls(
        InvocationExpressionSyntax createMapInvocation,
        SemanticModel semanticModel)
    {
        var mappingCalls = new List<InvocationExpressionSyntax>();

        foreach (InvocationExpressionSyntax invocation in MappingChainAnalysisHelper.GetScopedChainInvocations(
                     createMapInvocation,
                     semanticModel,
                     ShouldStopAtReverseMapBoundary(createMapInvocation, semanticModel)))
        {
            if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "ForMember") ||
                MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "ForPath") ||
                MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "ForCtorParam"))
            {
                mappingCalls.Add(invocation);
            }
        }

        return mappingCalls;
    }

    public static string? GetSelectedTopLevelMemberName(SyntaxNode expression)
    {
        return GetSelectedTopLevelMemberNameCore(expression, semanticModel: null);
    }

    public static string? GetSelectedTopLevelMemberNameWithSemanticModel(
        SyntaxNode expression,
        SemanticModel semanticModel)
    {
        return GetSelectedTopLevelMemberNameCore(expression, semanticModel);
    }

    private static string? GetSelectedTopLevelMemberNameCore(SyntaxNode expression, SemanticModel? semanticModel)
    {
        return expression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => GetSelectedTopLevelMemberNameCore(simpleLambda.Body, semanticModel),
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda =>
                GetSelectedTopLevelMemberNameCore(parenthesizedLambda.Body, semanticModel),
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) =>
                GetTopLevelMemberName(literal.Token.ValueText),
            ExpressionSyntax expressionSyntax when TryGetStringConstant(
                expressionSyntax,
                semanticModel,
                out string memberPath) => GetTopLevelMemberName(memberPath),
            MemberAccessExpressionSyntax memberAccess => GetTopLevelMemberName(memberAccess),
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

    private static bool IsDirectConstructorAssignment(
        AssignmentExpressionSyntax assignment,
        SyntaxNode constructorSyntax)
    {
        if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) ||
            !IsSynchronousConstructorAssignment(assignment, constructorSyntax))
        {
            return false;
        }

        return assignment.Parent switch
        {
            ExpressionStatementSyntax { Parent: BlockSyntax { Parent: ConstructorDeclarationSyntax constructor } } =>
                constructor == constructorSyntax,
            ArrowExpressionClauseSyntax { Parent: ConstructorDeclarationSyntax constructor } =>
                constructor == constructorSyntax,
            _ => false
        };
    }

    private static bool IsSynchronousConstructorAssignment(
        AssignmentExpressionSyntax assignment,
        SyntaxNode constructorSyntax)
    {
        foreach (SyntaxNode ancestor in assignment.Ancestors())
        {
            if (ancestor == constructorSyntax)
            {
                return true;
            }

            if (ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or ConstructorDeclarationSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsAssignmentToConstructedInstance(AssignmentExpressionSyntax assignment)
    {
        return assignment.Left switch
        {
            IdentifierNameSyntax => true,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } => true,
            _ => false
        };
    }
    private static bool ShouldStopAtReverseMapBoundary(
        InvocationExpressionSyntax mappingInvocation,
        SemanticModel semanticModel)
    {
        return !MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(mappingInvocation, semanticModel, "ReverseMap");
    }
}

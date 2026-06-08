using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.TypeSafety;

/// <summary>
///     Analyzer for detecting nullable reference type compatibility issues in AutoMapper configurations.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM002_NullableCompatibilityAnalyzer : DiagnosticAnalyzer
{
    internal const string PropertyNamePropertyName = "PropertyName";
    internal const string SourcePropertyNamePropertyName = "SourcePropertyName";
    internal const string SourcePropertyTypePropertyName = "SourcePropertyType";
    internal const string DestinationPropertyTypePropertyName = "DestinationPropertyType";

    /// <summary>
    ///     Marker property set when the diagnostic is a collection-element nullability mismatch
    ///     (e.g. <c>List&lt;string?&gt;</c> → <c>List&lt;string&gt;</c>) rather than a top-level one. The
    ///     code-fix provider uses it to withhold the <c>?? default</c> scaffold (which cannot fix element
    ///     nullability) and offer only the manual-review ignore action.
    /// </summary>
    internal const string ElementNullabilityPropertyName = "ElementNullability";

    /// <summary>
    ///     AM002: Nullable to non-nullable assignment without proper handling.
    /// </summary>
    public static readonly DiagnosticDescriptor NullableToNonNullableRule = new(
        "AM002",
        "Nullable to non-nullable mapping issue in AutoMapper configuration",
        "Property '{0}' has nullable compatibility issue: {1}.{2} ({3}) can be null but {4}.{0} ({5}) is non-nullable",
        "AutoMapper.NullSafety",
        DiagnosticSeverity.Error,
        true,
        "Source property is nullable but destination property is non-nullable, which could cause null reference exceptions at runtime.");

    /// <summary>
    ///     AM002: Non-nullable to nullable assignment (informational).
    /// </summary>
    public static readonly DiagnosticDescriptor NonNullableToNullableRule = new(
        "AM002",
        "Non-nullable to nullable mapping in AutoMapper configuration",
        "Property '{0}' mapping: {1}.{0} ({2}) is non-nullable but {3}.{0} ({4}) is nullable",
        "AutoMapper.NullSafety",
        DiagnosticSeverity.Info,
        true,
        "Non-nullable source property is being mapped to nullable destination property.");

    /// <summary>
    ///     Gets the supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [NullableToNonNullableRule, NonNullableToNullableRule];

    /// <summary>
    ///     Initializes the analyzer.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeCreateMapInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeCreateMapInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;

        // Ensure this resolves to AutoMapper CreateMap to avoid lookalike API false positives.
        if (!IsAutoMapperCreateMapInvocation(invocationExpr, context.SemanticModel))
        {
            return;
        }

        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (sourceType == null || destinationType == null)
        {
            return;
        }

        // Analyze nullable compatibility for property mappings
        AnalyzeNullablePropertyMappings(
            context,
            invocationExpr,
            sourceType,
            destinationType
        );
    }

    private static void AnalyzeNullablePropertyMappings(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        if (AM020MappingConfigurationHelpers.HasCustomConstructionOrConversion(invocation, context.SemanticModel))
        {
            return;
        }

        IPropertySymbol[] sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false).ToArray();
        IPropertySymbol[] destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, false).ToArray();
        var explicitlyHandledDestinationProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (IPropertySymbol destinationProperty in destinationProperties)
        {
            bool hasExplicitNullabilityConfiguration = TryGetExplicitNullabilityConfiguration(
                    invocation,
                    destinationProperty.Name,
                    destinationProperty.Type,
                    context.SemanticModel,
                    out bool configurationHandlesNullability,
                    out ITypeSymbol? explicitNullableSourceType,
                    out string? explicitNullableSourceName,
                    out string? explicitNullableSourceDisplayTypeName);
            if (!hasExplicitNullabilityConfiguration)
            {
                continue;
            }

            if (configurationHandlesNullability)
            {
                explicitlyHandledDestinationProperties.Add(destinationProperty.Name);
                continue;
            }

            if (explicitNullableSourceType != null)
            {
                ReportNullableToNonNullableDiagnostic(
                    context,
                    invocation,
                    destinationProperty.Name,
                    explicitNullableSourceName ?? destinationProperty.Name,
                    explicitNullableSourceType,
                    sourceType,
                    destinationType,
                    destinationProperty.Type,
                    sourceDisplayTypeName: explicitNullableSourceDisplayTypeName);
                explicitlyHandledDestinationProperties.Add(destinationProperty.Name);
            }
        }

        foreach (IPropertySymbol sourceProperty in sourceProperties)
        {
            IPropertySymbol? destinationProperty = destinationProperties.FirstOrDefault(p => p.Name == sourceProperty.Name) ??
                                                   destinationProperties.FirstOrDefault(p =>
                                                       string.Equals(p.Name, sourceProperty.Name,
                                                           StringComparison.OrdinalIgnoreCase));

            if (destinationProperty != null)
            {
                if (explicitlyHandledDestinationProperties.Contains(destinationProperty.Name))
                {
                    continue;
                }

                bool hasExplicitNullabilityConfiguration = TryGetExplicitNullabilityConfiguration(
                        invocation,
                        destinationProperty.Name,
                        destinationProperty.Type,
                        context.SemanticModel,
                        out bool configurationHandlesNullability,
                        out ITypeSymbol? explicitNullableSourceType,
                        out string? explicitNullableSourceName,
                        out _);
                if (hasExplicitNullabilityConfiguration &&
                    configurationHandlesNullability)
                {
                    continue;
                }

                if (explicitNullableSourceType != null)
                {
                    ReportNullableToNonNullableDiagnostic(
                        context,
                        invocation,
                        destinationProperty.Name,
                        explicitNullableSourceName ?? destinationProperty.Name,
                        explicitNullableSourceType,
                        sourceType,
                        destinationType,
                        destinationProperty.Type);
                    continue;
                }

                if (hasExplicitNullabilityConfiguration &&
                    !IsNullableToNonNullableCompatible(sourceProperty.Type, destinationProperty.Type))
                {
                    continue;
                }

                AnalyzeNullableCompatibility(
                    context,
                    invocation,
                    sourceProperty,
                    destinationProperty,
                    sourceType,
                    destinationType
                );
            }
        }
    }

    private static bool TryGetExplicitNullabilityConfiguration(
        InvocationExpressionSyntax createMapInvocation,
        string destinationPropertyName,
        ITypeSymbol destinationPropertyType,
        SemanticModel semanticModel,
        out bool handlesNullability,
        out ITypeSymbol? explicitNullableSourceType,
        out string? explicitNullableSourceName,
        out string? explicitNullableSourceDisplayTypeName)
    {
        handlesNullability = false;
        explicitNullableSourceType = null;
        explicitNullableSourceName = null;
        explicitNullableSourceDisplayTypeName = null;
        InvocationExpressionSyntax? effectiveMappingCall = null;
        foreach (InvocationExpressionSyntax mappingCall in GetDestinationConfigurationCalls(createMapInvocation, semanticModel))
        {
            if (!ConfigurationTargetsTopLevelDestinationMember(mappingCall, semanticModel, destinationPropertyName))
            {
                continue;
            }

            effectiveMappingCall = mappingCall;
        }

        if (effectiveMappingCall == null)
        {
            return false;
        }

        if (ConfigurationCallsMethod(effectiveMappingCall, "Ignore") ||
            ConfigurationCallsMethod(effectiveMappingCall, "ConvertUsing"))
        {
            handlesNullability = true;
            return true;
        }

        if (TryGetMapFromBody(effectiveMappingCall, out ExpressionSyntax? mapFromBody))
        {
            if (mapFromBody == null)
            {
                handlesNullability = true;
                return true;
            }

            if (MapFromExpressionProducesNonNullableValue(mapFromBody, destinationPropertyType, semanticModel))
            {
                handlesNullability = true;
                return true;
            }

            if (TryGetNullableMapFromType(
                    mapFromBody,
                    destinationPropertyType,
                    semanticModel,
                    out ITypeSymbol? nullableMapFromType,
                    out string? nullableMapFromName,
                    out string? nullableMapFromDisplayTypeName))
            {
                if (!ExpressionDereferencesNullableReceiver(mapFromBody, semanticModel) &&
                    !ExpressionDereferencesSuppressedNullableReceiver(mapFromBody, semanticModel) &&
                    ConfigurationCallsSafeNullSubstitute(effectiveMappingCall, destinationPropertyType, semanticModel))
                {
                    handlesNullability = true;
                    return true;
                }

                explicitNullableSourceType = nullableMapFromType;
                explicitNullableSourceName = nullableMapFromName;
                explicitNullableSourceDisplayTypeName = nullableMapFromDisplayTypeName;
                return true;
            }
        }

        if (ConfigurationCallsSafeNullSubstitute(effectiveMappingCall, destinationPropertyType, semanticModel))
        {
            handlesNullability = true;
            return true;
        }

        return true;
    }

    private static IEnumerable<InvocationExpressionSyntax> GetDestinationConfigurationCalls(
        InvocationExpressionSyntax createMapInvocation,
        SemanticModel semanticModel)
    {
        foreach (InvocationExpressionSyntax invocation in MappingChainAnalysisHelper.GetScopedChainInvocations(
                     createMapInvocation,
                     semanticModel,
                     stopAtReverseMapBoundary: true))
        {
            if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "ForMember") ||
                MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "ForPath"))
            {
                yield return invocation;
            }
        }
    }

    private static bool ConfigurationTargetsTopLevelDestinationMember(
        InvocationExpressionSyntax mappingCall,
        SemanticModel semanticModel,
        string destinationPropertyName)
    {
        if (mappingCall.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        ExpressionSyntax destinationExpression = mappingCall.ArgumentList.Arguments[0].Expression;
        if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(mappingCall, semanticModel, "ForMember"))
        {
            string? selectedMember =
                AM020MappingConfigurationHelpers.GetSelectedTopLevelMemberName(destinationExpression);
            return string.Equals(selectedMember, destinationPropertyName, StringComparison.OrdinalIgnoreCase);
        }

        return MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(mappingCall, semanticModel, "ForPath") &&
               TryGetSelectedMemberPath(destinationExpression, out string memberPath) &&
               !memberPath.Contains('.') &&
               string.Equals(memberPath, destinationPropertyName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetSelectedMemberPath(ExpressionSyntax expression, out string memberPath)
    {
        memberPath = string.Empty;
        if (expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            memberPath = literal.Token.ValueText.Trim();
            return !string.IsNullOrEmpty(memberPath);
        }

        CSharpSyntaxNode? body = AutoMapperAnalysisHelpers.GetLambdaBody(expression);
        if (body is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var pathSegments = new Stack<string>();
        ExpressionSyntax currentExpression = memberAccess;
        while (currentExpression is MemberAccessExpressionSyntax currentMemberAccess)
        {
            pathSegments.Push(currentMemberAccess.Name.Identifier.ValueText);
            currentExpression = currentMemberAccess.Expression;
        }

        if (currentExpression is not IdentifierNameSyntax || pathSegments.Count == 0)
        {
            return false;
        }

        memberPath = string.Join(".", pathSegments);
        return true;
    }

    private static bool ConfigurationCallsMethod(
        InvocationExpressionSyntax mappingCall,
        string methodName)
    {
        if (mappingCall.ArgumentList.Arguments.Count <= 1)
        {
            return false;
        }

        ExpressionSyntax optionsExpression = mappingCall.ArgumentList.Arguments[1].Expression;
        string? optionsParameterName = GetSingleParameterName(optionsExpression);
        if (string.IsNullOrEmpty(optionsParameterName))
        {
            return false;
        }

        CSharpSyntaxNode? optionsBody = AutoMapperAnalysisHelpers.GetLambdaBody(optionsExpression);
        if (optionsBody == null)
        {
            return false;
        }

        return optionsBody
            .DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression is IdentifierNameSyntax receiver &&
                string.Equals(receiver.Identifier.ValueText, optionsParameterName, StringComparison.Ordinal) &&
                memberAccess.Name.Identifier.ValueText == methodName);
    }

    private static bool ConfigurationCallsSafeNullSubstitute(
        InvocationExpressionSyntax mappingCall,
        ITypeSymbol destinationPropertyType,
        SemanticModel semanticModel)
    {
        if (mappingCall.ArgumentList.Arguments.Count <= 1)
        {
            return false;
        }

        ExpressionSyntax optionsExpression = mappingCall.ArgumentList.Arguments[1].Expression;
        string? optionsParameterName = GetSingleParameterName(optionsExpression);
        if (string.IsNullOrEmpty(optionsParameterName))
        {
            return false;
        }

        CSharpSyntaxNode? optionsBody = AutoMapperAnalysisHelpers.GetLambdaBody(optionsExpression);
        if (optionsBody == null)
        {
            return false;
        }

        return optionsBody
            .DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression is IdentifierNameSyntax receiver &&
                string.Equals(receiver.Identifier.ValueText, optionsParameterName, StringComparison.Ordinal) &&
                memberAccess.Name.Identifier.ValueText == "NullSubstitute" &&
                invocation.ArgumentList.Arguments.Count > 0 &&
                NullSubstituteValueIsSafe(
                    invocation.ArgumentList.Arguments[0].Expression,
                    destinationPropertyType,
                    semanticModel));
    }

    private static bool NullSubstituteValueIsSafe(
        ExpressionSyntax substituteExpression,
        ITypeSymbol destinationPropertyType,
        SemanticModel semanticModel)
    {
        if (substituteExpression.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return false;
        }

        TypeInfo typeInfo = semanticModel.GetTypeInfo(substituteExpression);
        ITypeSymbol? substituteType = typeInfo.Type ?? typeInfo.ConvertedType;
        if (substituteType == null)
        {
            return false;
        }

        if ((substituteExpression.IsKind(SyntaxKind.DefaultLiteralExpression) ||
             substituteExpression is DefaultExpressionSyntax) &&
            !IsNonNullableValueType(substituteType))
        {
            return false;
        }

        if (IsNullableType(substituteType))
        {
            return false;
        }

        Conversion conversion = semanticModel.ClassifyConversion(substituteExpression, destinationPropertyType);
        if (conversion.Exists && conversion.IsImplicit)
        {
            return true;
        }

        return AreUnderlyingTypesCompatible(
            AutoMapperAnalysisHelpers.GetUnderlyingType(substituteType),
            AutoMapperAnalysisHelpers.GetUnderlyingType(destinationPropertyType));
    }

    private static bool IsNonNullableValueType(ITypeSymbol type)
    {
        return type.IsValueType && !IsNullableType(type);
    }

    private static string? GetSingleParameterName(ExpressionSyntax expression)
    {
        return expression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesizedLambda =>
                parenthesizedLambda.ParameterList.Parameters[0].Identifier.ValueText,
            _ => null
        };
    }

    private static bool TryGetMapFromBody(
        InvocationExpressionSyntax mappingCall,
        out ExpressionSyntax? mapFromBody)
    {
        mapFromBody = null;
        if (mappingCall.ArgumentList.Arguments.Count <= 1)
        {
            return false;
        }

        ExpressionSyntax optionsExpression = mappingCall.ArgumentList.Arguments[1].Expression;
        string? optionsParameterName = GetSingleParameterName(optionsExpression);
        if (string.IsNullOrEmpty(optionsParameterName))
        {
            return false;
        }

        CSharpSyntaxNode? optionsBody = AutoMapperAnalysisHelpers.GetLambdaBody(optionsExpression);
        if (optionsBody == null)
        {
            return false;
        }

        bool foundMapFrom = false;
        foreach (InvocationExpressionSyntax invocation in optionsBody
                     .DescendantNodesAndSelf()
                     .OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                memberAccess.Expression is not IdentifierNameSyntax receiver ||
                !string.Equals(receiver.Identifier.ValueText, optionsParameterName, StringComparison.Ordinal) ||
                memberAccess.Name.Identifier.ValueText != "MapFrom")
            {
                continue;
            }

            if (invocation.ArgumentList.Arguments.Count == 0)
            {
                mapFromBody = null;
                foundMapFrom = true;
                continue;
            }

            if (memberAccess.Name is GenericNameSyntax
                {
                    TypeArgumentList.Arguments.Count: > 1
                })
            {
                mapFromBody = null;
                foundMapFrom = true;
                continue;
            }

            ExpressionSyntax mapFromArgument = invocation.ArgumentList.Arguments[0].Expression;
            mapFromBody = mapFromArgument switch
            {
                SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Body as ExpressionSyntax,
                ParenthesizedLambdaExpressionSyntax parenthesizedLambda => parenthesizedLambda.Body as ExpressionSyntax,
                _ => null
            };
            foundMapFrom = true;
        }

        return foundMapFrom;
    }

    private static bool MapFromExpressionProducesNonNullableValue(
        ExpressionSyntax mapFromBody,
        ITypeSymbol destinationPropertyType,
        SemanticModel semanticModel)
    {
        TypeInfo typeInfo = semanticModel.GetTypeInfo(mapFromBody);
        ITypeSymbol? mappedType = typeInfo.ConvertedType ?? typeInfo.Type;
        if (mappedType == null)
        {
            return false;
        }

        return !TryGetNullableSuppressedMapFromSource(
                   mapFromBody,
                   destinationPropertyType,
                   semanticModel,
                   out _,
                   out _,
                   out _) &&
               !ExpressionDereferencesNullableReceiver(mapFromBody, semanticModel) &&
               !IsNullableType(mappedType) &&
               AreUnderlyingTypesCompatible(
                   AutoMapperAnalysisHelpers.GetUnderlyingType(mappedType),
                   AutoMapperAnalysisHelpers.GetUnderlyingType(destinationPropertyType));
    }

    private static bool ExpressionDereferencesNullableReceiver(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        return expression
            .DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(memberAccess =>
                !IsSafeNullableValueMemberAccess(memberAccess) &&
                !IsGuardedSuppressedNullableReceiverAccess(memberAccess, semanticModel) &&
                !IsExtensionMethodReceiverAccess(memberAccess, semanticModel) &&
                IsNullableExpression(memberAccess.Expression, semanticModel));
    }

    private static bool IsExtensionMethodReceiverAccess(
        MemberAccessExpressionSyntax memberAccess,
        SemanticModel semanticModel)
    {
        if (memberAccess.Parent is not InvocationExpressionSyntax invocation ||
            invocation.Expression != memberAccess)
        {
            return false;
        }

        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);
        IMethodSymbol? methodSymbol = symbolInfo.Symbol as IMethodSymbol ??
                                      symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        return methodSymbol is { IsExtensionMethod: true } or { ReducedFrom: not null };
    }

    private static bool IsSafeNullableValueMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        return memberAccess.Name.Identifier.ValueText == "GetValueOrDefault" &&
               memberAccess.Parent is InvocationExpressionSyntax invocation &&
               invocation.Expression == memberAccess;
    }

    private static bool IsGuardedSuppressedNullableReceiverAccess(
        MemberAccessExpressionSyntax memberAccess,
        SemanticModel semanticModel)
    {
        return memberAccess.Expression is PostfixUnaryExpressionSyntax suppression &&
               suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression) &&
               IsSuppressedOperandGuardedByConditional(suppression, semanticModel);
    }

    private static bool IsNullableExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        TypeInfo typeInfo = semanticModel.GetTypeInfo(expression);
        ITypeSymbol? expressionType = typeInfo.Type ?? typeInfo.ConvertedType;
        return expressionType != null && IsNullableType(expressionType);
    }

    private static bool TryGetNullableMapFromType(
        ExpressionSyntax mapFromBody,
        ITypeSymbol destinationPropertyType,
        SemanticModel semanticModel,
        out ITypeSymbol? nullableMapFromType,
        out string? nullableMapFromName,
        out string? nullableMapFromDisplayTypeName)
    {
        nullableMapFromType = null;
        nullableMapFromName = null;
        nullableMapFromDisplayTypeName = null;
        TypeInfo typeInfo = semanticModel.GetTypeInfo(mapFromBody);
        ITypeSymbol? mappedType = typeInfo.ConvertedType ?? typeInfo.Type;
        if (TryGetNullableSuppressedMapFromSource(
                mapFromBody,
                destinationPropertyType,
                semanticModel,
                out nullableMapFromType,
                out nullableMapFromName,
                out nullableMapFromDisplayTypeName))
        {
            return true;
        }

        if (TryGetNullableDereferencedReceiver(
                mapFromBody,
                destinationPropertyType,
                semanticModel,
                out nullableMapFromType,
                out nullableMapFromName))
        {
            return true;
        }

        if (mappedType == null ||
            !IsNullableType(mappedType) ||
            IsNullableType(destinationPropertyType) ||
            !AreUnderlyingTypesCompatible(
                AutoMapperAnalysisHelpers.GetUnderlyingType(mappedType),
                AutoMapperAnalysisHelpers.GetUnderlyingType(destinationPropertyType)))
        {
            return false;
        }

        nullableMapFromType = mappedType;
        if (mapFromBody is not ConditionalExpressionSyntax &&
            mapFromBody is not SwitchExpressionSyntax &&
            TryGetSourceMemberPath(mapFromBody, out string sourceMemberPath))
        {
            nullableMapFromName = sourceMemberPath;
        }
        else if (TryGetNullableExpressionDiagnosticDisplay(
                     mapFromBody,
                     semanticModel,
                     out string displayTypeName,
                     out string displayName))
        {
            nullableMapFromName = displayName;
            nullableMapFromDisplayTypeName = displayTypeName;
        }

        return true;
    }

    private static bool TryGetNullableSuppressedMapFromSource(
        ExpressionSyntax expression,
        ITypeSymbol destinationPropertyType,
        SemanticModel semanticModel,
        out ITypeSymbol? nullableSourceType,
        out string? nullableSourceName,
        out string? nullableSourceDisplayTypeName)
    {
        nullableSourceType = null;
        nullableSourceName = null;
        nullableSourceDisplayTypeName = null;
        ExpressionSyntax directExpression = UnwrapParentheses(expression);
        foreach (PostfixUnaryExpressionSyntax suppression in expression
                     .DescendantNodesAndSelf()
                     .OfType<PostfixUnaryExpressionSyntax>())
        {
            if (!suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            {
                continue;
            }

            if (!TryGetNullableSuppressedOperandType(suppression, semanticModel, out ITypeSymbol? operandType) ||
                operandType == null)
            {
                continue;
            }

            bool mapsSuppressedValueDirectly =
                (suppression == directExpression ||
                 SuppressedValueCanFlowToExpressionResult(suppression, directExpression, operandType, semanticModel)) &&
                !IsNullableType(destinationPropertyType) &&
                SuppressedValueCanMapToDestination(
                    suppression,
                    operandType,
                    destinationPropertyType,
                    semanticModel);
            bool dereferencesSuppressedReceiver =
                !IsNullableType(destinationPropertyType) &&
                IsSuppressedNullableReceiverDereferenced(suppression, semanticModel);
            if (!mapsSuppressedValueDirectly && !dereferencesSuppressedReceiver)
            {
                continue;
            }

            if (TryGetCoalesceExpressionForSuppressedDefaultFallback(
                    suppression,
                    out BinaryExpressionSyntax? coalesceExpression))
            {
                ITypeSymbol? coalesceLeftType = GetNullableExpressionDeclaredType(coalesceExpression.Left, semanticModel);
                if (coalesceLeftType == null)
                {
                    continue;
                }

                nullableSourceType = coalesceLeftType;
                if (TryGetSourceMemberPath(coalesceExpression.Left, out string coalesceLeftSourceMemberPath))
                {
                    nullableSourceName = coalesceLeftSourceMemberPath;
                }
                else if (TryGetNullableExpressionDiagnosticDisplay(
                             coalesceExpression.Left,
                             semanticModel,
                             out string coalesceLeftDisplayTypeName,
                             out string coalesceLeftDisplayName))
                {
                    nullableSourceName = coalesceLeftDisplayName;
                    nullableSourceDisplayTypeName = coalesceLeftDisplayTypeName;
                }

                return true;
            }

            nullableSourceType = operandType;
            ExpressionSyntax suppressedOperand = UnwrapParentheses(suppression.Operand);
            if (suppressedOperand is not ElementAccessExpressionSyntax &&
                suppressedOperand is not ConditionalExpressionSyntax &&
                suppressedOperand is not SwitchExpressionSyntax &&
                TryGetSourceMemberPath(suppression, out string sourceMemberPath))
            {
                nullableSourceName = sourceMemberPath;
            }
            else if (TryGetNullableExpressionDiagnosticDisplay(
                         suppressedOperand,
                         semanticModel,
                         out string displayTypeName,
                         out string displayName))
            {
                nullableSourceName = displayName;
                nullableSourceDisplayTypeName = displayTypeName;
            }

            return true;
        }

        return false;
    }

    private static bool SuppressedValueCanMapToDestination(
        PostfixUnaryExpressionSyntax suppression,
        ITypeSymbol operandType,
        ITypeSymbol destinationPropertyType,
        SemanticModel semanticModel)
    {
        if (AreUnderlyingTypesCompatible(
                AutoMapperAnalysisHelpers.GetUnderlyingType(operandType),
                AutoMapperAnalysisHelpers.GetUnderlyingType(destinationPropertyType)))
        {
            return true;
        }

        Conversion conversion = semanticModel.ClassifyConversion(suppression, destinationPropertyType);
        return conversion.Exists && conversion.IsImplicit;
    }

    private static bool SuppressedValueCanFlowToExpressionResult(
        PostfixUnaryExpressionSyntax suppression,
        ExpressionSyntax expression,
        ITypeSymbol operandType,
        SemanticModel semanticModel)
    {
        bool suppressedDefaultIsGenericFallback =
            IsNullOrDefaultExpression(suppression.Operand) &&
            operandType.TypeKind == TypeKind.TypeParameter;
        SyntaxNode current = suppression;
        while (current != expression)
        {
            SyntaxNode? parent = current.Parent;
            switch (parent)
            {
                case ParenthesizedExpressionSyntax parenthesizedExpression
                    when parenthesizedExpression.Expression == current:
                    current = parenthesizedExpression;
                    break;
                case CastExpressionSyntax castExpression
                    when castExpression.Expression == current:
                    current = castExpression;
                    break;
                case ConditionalExpressionSyntax conditionalExpression
                    when conditionalExpression.WhenTrue == current || conditionalExpression.WhenFalse == current:
                    current = conditionalExpression;
                    break;
                case SwitchExpressionArmSyntax switchArm
                    when switchArm.Expression == current:
                    current = switchArm;
                    break;
                case SwitchExpressionSyntax switchExpression
                    when current is SwitchExpressionArmSyntax arm &&
                         switchExpression.Arms.Any(candidate => candidate == arm):
                    current = switchExpression;
                    break;
                case BinaryExpressionSyntax coalesceExpression
                    when coalesceExpression.IsKind(SyntaxKind.CoalesceExpression) &&
                         coalesceExpression.Right == current:
                    if (!ExpressionCanProduceNullAt(coalesceExpression.Left, coalesceExpression, semanticModel))
                    {
                        return false;
                    }

                    current = coalesceExpression;
                    break;
                case BinaryExpressionSyntax coalesceExpression
                    when coalesceExpression.IsKind(SyntaxKind.CoalesceExpression) &&
                         coalesceExpression.Left == current:
                    return false;
                default:
                    return false;
            }
        }

        return !suppressedDefaultIsGenericFallback;
    }

    private static bool TryGetCoalesceExpressionForSuppressedDefaultFallback(
        PostfixUnaryExpressionSyntax suppression,
        out BinaryExpressionSyntax coalesceExpression)
    {
        coalesceExpression = null!;
        if (!IsNullOrDefaultExpression(suppression.Operand))
        {
            return false;
        }

        SyntaxNode current = suppression;
        while (current.Parent is ParenthesizedExpressionSyntax parenthesizedExpression &&
               parenthesizedExpression.Expression == current)
        {
            current = parenthesizedExpression;
        }

        if (current.Parent is not BinaryExpressionSyntax parentCoalesceExpression ||
            !parentCoalesceExpression.IsKind(SyntaxKind.CoalesceExpression) ||
            parentCoalesceExpression.Right != current)
        {
            return false;
        }

        coalesceExpression = parentCoalesceExpression;
        return true;
    }

    private static bool ExpressionDereferencesSuppressedNullableReceiver(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        return expression
            .DescendantNodesAndSelf()
            .OfType<PostfixUnaryExpressionSyntax>()
            .Any(suppression =>
                suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression) &&
                TryGetNullableSuppressedOperandType(suppression, semanticModel, out _) &&
                IsSuppressedNullableReceiverDereferenced(suppression, semanticModel));
    }

    private static bool TryGetNullableSuppressedOperandType(
        PostfixUnaryExpressionSyntax suppression,
        SemanticModel semanticModel,
        out ITypeSymbol? operandType)
    {
        operandType = GetNullableExpressionDeclaredType(suppression.Operand, semanticModel);
        if (operandType == null &&
            IsNullOrDefaultExpression(suppression.Operand))
        {
            operandType = GetNullableSuppressedLiteralType(suppression, semanticModel);
        }

        if (operandType == null)
        {
            return false;
        }

        if (IsSuppressedOperandGuardedByConditional(suppression, semanticModel))
        {
            return false;
        }

        return true;
    }

    private static ITypeSymbol? GetNullableSuppressedLiteralType(
        PostfixUnaryExpressionSyntax suppression,
        SemanticModel semanticModel)
    {
        TypeInfo typeInfo = semanticModel.GetTypeInfo(suppression);
        ITypeSymbol? expressionType = typeInfo.Type ?? typeInfo.ConvertedType;
        if (expressionType == null &&
            suppression.Parent is CastExpressionSyntax castExpression &&
            castExpression.Expression == suppression)
        {
            TypeInfo castTypeInfo = semanticModel.GetTypeInfo(castExpression.Type);
            expressionType = castTypeInfo.Type ?? castTypeInfo.ConvertedType;
        }

        if (expressionType != null &&
            IsNullableType(expressionType))
        {
            return expressionType;
        }

        return expressionType?.IsReferenceType == true
            ? expressionType.WithNullableAnnotation(NullableAnnotation.Annotated)
            : null;
    }

    private static bool IsSuppressedOperandGuardedByConditional(
        PostfixUnaryExpressionSyntax suppression,
        SemanticModel semanticModel)
    {
        ExpressionSyntax operand = UnwrapParentheses(suppression.Operand);
        return IsExpressionProvenNonNullByContainingFlow(suppression, operand, semanticModel);
    }

    private static bool ExpressionCanProduceNullAt(
        ExpressionSyntax expression,
        SyntaxNode context,
        SemanticModel semanticModel)
    {
        return ExpressionCanProduceNull(expression, semanticModel) &&
               !IsExpressionProvenNonNullByContainingFlow(context, expression, semanticModel);
    }

    private static bool IsExpressionProvenNonNullByContainingFlow(
        SyntaxNode context,
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        expression = UnwrapParentheses(expression);
        if (IsExpressionGuardedByShortCircuitCondition(context, expression, semanticModel))
        {
            return true;
        }

        for (SyntaxNode? current = context.Parent; current != null; current = current.Parent)
        {
            if (current is SwitchExpressionArmSyntax switchArm &&
                switchArm.Expression.Span.Contains(context.SpanStart) &&
                switchArm.Parent is SwitchExpressionSyntax switchExpression &&
                SwitchArmProvesExpressionNonNull(switchExpression, switchArm, expression, semanticModel))
            {
                return true;
            }

            if (current is not ConditionalExpressionSyntax conditionalExpression)
            {
                continue;
            }

            bool isInWhenTrue = conditionalExpression.WhenTrue.Span.Contains(context.SpanStart);
            bool isInWhenFalse = conditionalExpression.WhenFalse.Span.Contains(context.SpanStart);
            if ((isInWhenFalse && ConditionProvesOperandNonNullWhenFalse(conditionalExpression.Condition, expression, semanticModel)) ||
                (isInWhenTrue && ConditionProvesOperandNonNullWhenTrue(conditionalExpression.Condition, expression, semanticModel)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExpressionGuardedByShortCircuitCondition(
        SyntaxNode context,
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        for (SyntaxNode? current = context; current?.Parent != null; current = current.Parent)
        {
            if (current.Parent is not BinaryExpressionSyntax binaryExpression ||
                binaryExpression.Right != current)
            {
                continue;
            }

            if (binaryExpression.IsKind(SyntaxKind.LogicalAndExpression) &&
                ConditionProvesOperandNonNullWhenTrue(binaryExpression.Left, expression, semanticModel))
            {
                return true;
            }

            if (binaryExpression.IsKind(SyntaxKind.LogicalOrExpression) &&
                ConditionProvesOperandNonNullWhenFalse(binaryExpression.Left, expression, semanticModel))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ConditionProvesOperandNonNullWhenTrue(
        ExpressionSyntax condition,
        ExpressionSyntax operand,
        SemanticModel semanticModel)
    {
        condition = UnwrapParentheses(condition);
        if (ConditionTestsOperandNotNull(condition, operand, semanticModel) ||
            ConditionComparesOperandToKnownNonNullExpression(condition, operand, SyntaxKind.EqualsExpression, semanticModel))
        {
            return true;
        }

        if (condition is PrefixUnaryExpressionSyntax logicalNot &&
            logicalNot.IsKind(SyntaxKind.LogicalNotExpression))
        {
            return ConditionProvesOperandNonNullWhenFalse(logicalNot.Operand, operand, semanticModel);
        }

        return condition is BinaryExpressionSyntax binaryExpression &&
               binaryExpression.IsKind(SyntaxKind.LogicalAndExpression) &&
               (ConditionProvesOperandNonNullWhenTrue(binaryExpression.Left, operand, semanticModel) ||
                ConditionProvesOperandNonNullWhenTrue(binaryExpression.Right, operand, semanticModel));
    }

    private static bool ConditionProvesOperandNonNullWhenFalse(
        ExpressionSyntax condition,
        ExpressionSyntax operand,
        SemanticModel semanticModel)
    {
        condition = UnwrapParentheses(condition);
        if (ConditionTestsOperandNull(condition, operand, semanticModel) ||
            ConditionComparesOperandToKnownNonNullExpression(condition, operand, SyntaxKind.NotEqualsExpression, semanticModel))
        {
            return true;
        }

        if (condition is PrefixUnaryExpressionSyntax logicalNot &&
            logicalNot.IsKind(SyntaxKind.LogicalNotExpression))
        {
            return ConditionProvesOperandNonNullWhenTrue(logicalNot.Operand, operand, semanticModel);
        }

        return condition is BinaryExpressionSyntax binaryExpression &&
               binaryExpression.IsKind(SyntaxKind.LogicalOrExpression) &&
               (ConditionProvesOperandNonNullWhenFalse(binaryExpression.Left, operand, semanticModel) ||
                ConditionProvesOperandNonNullWhenFalse(binaryExpression.Right, operand, semanticModel));
    }

    private static bool ConditionTestsOperandNull(
        ExpressionSyntax condition,
        ExpressionSyntax operand,
        SemanticModel semanticModel)
    {
        condition = UnwrapParentheses(condition);
        return ConditionComparesOperandToNull(condition, operand, SyntaxKind.EqualsExpression, semanticModel) ||
               condition is IsPatternExpressionSyntax isPatternExpression &&
               ExpressionsAreEquivalent(isPatternExpression.Expression, operand) &&
               PatternMatchesNullWhenTrue(isPatternExpression.Pattern, semanticModel);
    }

    private static bool ConditionTestsOperandNotNull(
        ExpressionSyntax condition,
        ExpressionSyntax operand,
        SemanticModel semanticModel)
    {
        condition = UnwrapParentheses(condition);
        return ConditionComparesOperandToNull(condition, operand, SyntaxKind.NotEqualsExpression, semanticModel) ||
               condition is IsPatternExpressionSyntax isPatternExpression &&
               ExpressionsAreEquivalent(isPatternExpression.Expression, operand) &&
               PatternMatchesNonNullWhenTrue(isPatternExpression.Pattern, semanticModel);
    }

    private static bool ConditionComparesOperandToNull(
        ExpressionSyntax condition,
        ExpressionSyntax operand,
        SyntaxKind comparisonKind,
        SemanticModel semanticModel)
    {
        condition = UnwrapParentheses(condition);
        return condition is BinaryExpressionSyntax binaryExpression &&
               binaryExpression.IsKind(comparisonKind) &&
               ((ExpressionsAreEquivalent(binaryExpression.Left, operand) &&
                 IsNullLikeExpression(binaryExpression.Right, semanticModel)) ||
                (IsNullLikeExpression(binaryExpression.Left, semanticModel) &&
                 ExpressionsAreEquivalent(binaryExpression.Right, operand)));
    }

    private static bool ConditionComparesOperandToKnownNonNullExpression(
        ExpressionSyntax condition,
        ExpressionSyntax operand,
        SyntaxKind comparisonKind,
        SemanticModel semanticModel)
    {
        condition = UnwrapParentheses(condition);
        return condition is BinaryExpressionSyntax binaryExpression &&
               binaryExpression.IsKind(comparisonKind) &&
               ((ExpressionsAreEquivalent(binaryExpression.Left, operand) &&
                 IsKnownNonNullExpression(binaryExpression.Right, semanticModel)) ||
                (IsKnownNonNullExpression(binaryExpression.Left, semanticModel) &&
                 ExpressionsAreEquivalent(binaryExpression.Right, operand)));
    }

    private static bool IsKnownNonNullExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        expression = UnwrapParentheses(expression);
        if (expression is LiteralExpressionSyntax &&
            !IsNullOrDefaultExpression(expression))
        {
            return true;
        }

        if (!IsNullOrDefaultExpression(expression))
        {
            return false;
        }

        TypeInfo typeInfo = semanticModel.GetTypeInfo(expression);
        ITypeSymbol? expressionType = typeInfo.Type ?? typeInfo.ConvertedType;
        return expressionType != null && IsNonNullableValueType(expressionType);
    }

    private static bool PatternMatchesNullWhenTrue(PatternSyntax pattern, SemanticModel semanticModel)
    {
        pattern = UnwrapParenthesizedPattern(pattern);
        return pattern switch
        {
            ConstantPatternSyntax constantPattern => IsNullLikeExpression(constantPattern.Expression, semanticModel),
            UnaryPatternSyntax unaryPattern when unaryPattern.IsKind(SyntaxKind.NotPattern) =>
                PatternMatchesNonNullWhenTrue(unaryPattern.Pattern, semanticModel),
            BinaryPatternSyntax binaryPattern when binaryPattern.IsKind(SyntaxKind.AndPattern) =>
                PatternMatchesNullWhenTrue(binaryPattern.Left, semanticModel) &&
                PatternMatchesNullWhenTrue(binaryPattern.Right, semanticModel),
            BinaryPatternSyntax binaryPattern when binaryPattern.IsKind(SyntaxKind.OrPattern) =>
                PatternMatchesNullWhenTrue(binaryPattern.Left, semanticModel) ||
                PatternMatchesNullWhenTrue(binaryPattern.Right, semanticModel),
            _ => false
        };
    }

    private static bool PatternMatchesNonNullWhenTrue(PatternSyntax pattern, SemanticModel semanticModel)
    {
        pattern = UnwrapParenthesizedPattern(pattern);
        return pattern switch
        {
            ConstantPatternSyntax constantPattern => !IsNullLikeExpression(constantPattern.Expression, semanticModel),
            UnaryPatternSyntax unaryPattern when unaryPattern.IsKind(SyntaxKind.NotPattern) =>
                PatternMatchesNullWhenTrue(unaryPattern.Pattern, semanticModel),
            BinaryPatternSyntax binaryPattern when binaryPattern.IsKind(SyntaxKind.AndPattern) =>
                PatternMatchesNonNullWhenTrue(binaryPattern.Left, semanticModel) ||
                PatternMatchesNonNullWhenTrue(binaryPattern.Right, semanticModel),
            BinaryPatternSyntax binaryPattern when binaryPattern.IsKind(SyntaxKind.OrPattern) =>
                PatternMatchesNonNullWhenTrue(binaryPattern.Left, semanticModel) &&
                PatternMatchesNonNullWhenTrue(binaryPattern.Right, semanticModel),
            RecursivePatternSyntax => true,
            DeclarationPatternSyntax => true,
            _ => false
        };
    }

    private static PatternSyntax UnwrapParenthesizedPattern(PatternSyntax pattern)
    {
        while (pattern is ParenthesizedPatternSyntax parenthesizedPattern)
        {
            pattern = parenthesizedPattern.Pattern;
        }

        return pattern;
    }

    private static bool ExpressionsAreEquivalent(ExpressionSyntax left, ExpressionSyntax right)
    {
        ExpressionSyntax unwrappedLeft = UnwrapExpressionForEquivalence(left);
        ExpressionSyntax unwrappedRight = UnwrapExpressionForEquivalence(right);
        return IsStableGuardExpression(unwrappedLeft) &&
               IsStableGuardExpression(unwrappedRight) &&
               string.Equals(
                   unwrappedLeft.ToString(),
                   unwrappedRight.ToString(),
                   StringComparison.Ordinal);
    }

    private static bool IsStableGuardExpression(ExpressionSyntax expression)
    {
        expression = UnwrapExpressionForEquivalence(expression);
        return expression switch
        {
            IdentifierNameSyntax => true,
            MemberAccessExpressionSyntax memberAccess => IsStableGuardExpression(memberAccess.Expression),
            _ => false
        };
    }

    private static ExpressionSyntax UnwrapExpressionForEquivalence(ExpressionSyntax expression)
    {
        while (true)
        {
            expression = UnwrapParentheses(expression);
            if (expression is PostfixUnaryExpressionSyntax suppression &&
                suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            {
                expression = suppression.Operand;
                continue;
            }

            return expression;
        }
    }

    private static bool IsNullOrDefaultExpression(ExpressionSyntax expression)
    {
        expression = UnwrapParentheses(expression);
        return expression.IsKind(SyntaxKind.NullLiteralExpression) ||
               expression.IsKind(SyntaxKind.DefaultLiteralExpression) ||
               expression is DefaultExpressionSyntax;
    }

    private static bool IsNullLikeExpression(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        expression = UnwrapParentheses(expression);
        if (expression.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return true;
        }

        if (!expression.IsKind(SyntaxKind.DefaultLiteralExpression) &&
            expression is not DefaultExpressionSyntax)
        {
            return false;
        }

        ITypeSymbol? expressionType;
        if (expression is DefaultExpressionSyntax defaultExpression)
        {
            TypeInfo defaultTypeInfo = semanticModel.GetTypeInfo(defaultExpression.Type);
            expressionType = defaultTypeInfo.Type ?? defaultTypeInfo.ConvertedType;
        }
        else
        {
            TypeInfo typeInfo = semanticModel.GetTypeInfo(expression);
            expressionType = typeInfo.ConvertedType ?? typeInfo.Type;
        }

        return expressionType != null && TypeDefaultCanBeNull(expressionType);
    }

    private static bool TypeDefaultCanBeNull(ITypeSymbol type)
    {
        return type.IsReferenceType ||
               IsNullableType(type) ||
               type is ITypeParameterSymbol typeParameter && typeParameter.HasReferenceTypeConstraint;
    }

    private static bool TryGetNullableExpressionDiagnosticDisplay(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        out string displayTypeName,
        out string displayName)
    {
        displayTypeName = "MapFrom expression";
        expression = UnwrapParentheses(expression);
        displayName = expression.ToString();
        if (expression is ElementAccessExpressionSyntax)
        {
            return true;
        }

        ISymbol? symbol = semanticModel.GetSymbolInfo(expression).Symbol;
        switch (symbol)
        {
            case IFieldSymbol fieldSymbol:
                displayTypeName = GetContainingTypeDisplayName(fieldSymbol.ContainingType);
                displayName = fieldSymbol.Name;
                return true;
            case IPropertySymbol propertySymbol:
                displayTypeName = GetContainingTypeDisplayName(propertySymbol.ContainingType);
                displayName = propertySymbol.Name;
                return true;
            case IMethodSymbol methodSymbol:
                displayTypeName = GetContainingTypeDisplayName(methodSymbol.ContainingType);
                return !string.IsNullOrWhiteSpace(displayName);
            case ILocalSymbol localSymbol:
                displayName = localSymbol.Name;
                return true;
            case IParameterSymbol parameterSymbol:
                displayName = parameterSymbol.Name;
                return true;
            default:
                return !string.IsNullOrWhiteSpace(displayName);
        }
    }

    private static string GetContainingTypeDisplayName(INamedTypeSymbol? containingType)
    {
        return containingType == null ? "MapFrom expression" : GetDiagnosticTypeName(containingType);
    }

    private static ITypeSymbol? GetNullableExpressionDeclaredType(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        expression = UnwrapParentheses(expression);
        if (IsNullOrDefaultExpression(expression))
        {
            return GetNullableTypeFromExpressionTypeInfo(expression, semanticModel);
        }

        if (expression is BinaryExpressionSyntax asExpression &&
            asExpression.IsKind(SyntaxKind.AsExpression))
        {
            return GetNullableTypeFromExpressionTypeInfo(expression, semanticModel);
        }

        if (expression is BinaryExpressionSyntax coalesceExpression &&
            coalesceExpression.IsKind(SyntaxKind.CoalesceExpression))
        {
            ITypeSymbol? leftType = GetNullableExpressionDeclaredType(coalesceExpression.Left, semanticModel);
            if (leftType != null &&
                ExpressionCanProduceNull(coalesceExpression.Right, semanticModel))
            {
                return leftType;
            }
        }

        if (expression is ConditionalAccessExpressionSyntax)
        {
            return GetNullableTypeFromExpressionTypeInfo(expression, semanticModel);
        }

        if (expression is ConditionalExpressionSyntax conditionalExpression &&
            (IsNullOrDefaultExpression(conditionalExpression.WhenTrue) ||
             IsNullOrDefaultExpression(conditionalExpression.WhenFalse)))
        {
            return GetNullableTypeFromExpressionTypeInfo(expression, semanticModel);
        }

        if (expression is ConditionalExpressionSyntax nullableConditionalExpression &&
            TryGetNullableConditionalBranchType(
                nullableConditionalExpression,
                semanticModel,
                out ITypeSymbol? nullableConditionalType))
        {
            return nullableConditionalType;
        }

        if (expression is SwitchExpressionSyntax switchExpression &&
            TryGetNullableSwitchArmType(switchExpression, semanticModel, out ITypeSymbol? nullableSwitchArmType))
        {
            return nullableSwitchArmType;
        }

        if (expression is CastExpressionSyntax castExpression)
        {
            TypeInfo castTypeInfo = semanticModel.GetTypeInfo(castExpression.Type);
            ITypeSymbol? castType = castTypeInfo.Type ?? castTypeInfo.ConvertedType;
            if (castType != null &&
                (IsNullableType(castType) || castExpression.Type is NullableTypeSyntax))
            {
                return castExpression.Type is NullableTypeSyntax
                    ? castType.WithNullableAnnotation(NullableAnnotation.Annotated)
                    : castType;
            }
        }

        if (expression is ElementAccessExpressionSyntax elementAccess &&
            TryGetNullableElementAccessType(elementAccess, semanticModel, out ITypeSymbol? nullableElementType))
        {
            return nullableElementType;
        }

        ISymbol? symbol = semanticModel.GetSymbolInfo(expression).Symbol;
        ITypeSymbol? nullableSymbolType = symbol switch
        {
            IPropertySymbol propertySymbol when IsNullableType(propertySymbol.Type) => propertySymbol.Type,
            IFieldSymbol fieldSymbol when IsNullableType(fieldSymbol.Type) => fieldSymbol.Type,
            ILocalSymbol localSymbol when IsNullableType(localSymbol.Type) => localSymbol.Type,
            IParameterSymbol parameterSymbol when IsNullableType(parameterSymbol.Type) => parameterSymbol.Type,
            IMethodSymbol methodSymbol when IsNullableType(methodSymbol.ReturnType) => methodSymbol.ReturnType,
            _ => null
        };

        if (nullableSymbolType != null ||
            symbol != null)
        {
            return nullableSymbolType;
        }

        return GetNullableAnnotationFromExpressionTypeInfo(expression, semanticModel);
    }

    private static bool TryGetNullableConditionalBranchType(
        ConditionalExpressionSyntax conditionalExpression,
        SemanticModel semanticModel,
        out ITypeSymbol? nullableBranchType)
    {
        nullableBranchType = null;
        ITypeSymbol? whenTrueType = GetUnguardedNullableBranchType(
            conditionalExpression.WhenTrue,
            conditionalExpression.Condition,
            branchWhenTrue: true,
            semanticModel);
        if (whenTrueType != null)
        {
            nullableBranchType = whenTrueType;
            return true;
        }

        ITypeSymbol? whenFalseType = GetUnguardedNullableBranchType(
            conditionalExpression.WhenFalse,
            conditionalExpression.Condition,
            branchWhenTrue: false,
            semanticModel);
        if (whenFalseType != null)
        {
            nullableBranchType = whenFalseType;
            return true;
        }

        return false;
    }

    private static bool TryGetNullableSwitchArmType(
        SwitchExpressionSyntax switchExpression,
        SemanticModel semanticModel,
        out ITypeSymbol? nullableArmType)
    {
        nullableArmType = null;
        foreach (SwitchExpressionArmSyntax arm in switchExpression.Arms)
        {
            ExpressionSyntax armExpression = UnwrapParentheses(arm.Expression);
            ITypeSymbol? armType = GetNullableExpressionDeclaredType(armExpression, semanticModel);
            if (armType == null ||
                SwitchArmProvesExpressionNonNull(switchExpression, arm, armExpression, semanticModel))
            {
                continue;
            }

            nullableArmType = armType;
            return true;
        }

        return false;
    }

    private static bool SwitchArmProvesExpressionNonNull(
        SwitchExpressionSyntax switchExpression,
        SwitchExpressionArmSyntax arm,
        ExpressionSyntax armExpression,
        SemanticModel semanticModel)
    {
        if (arm.WhenClause is { Condition: ExpressionSyntax whenCondition } &&
            ConditionProvesOperandNonNullWhenTrue(whenCondition, armExpression, semanticModel))
        {
            return true;
        }

        if (!ExpressionsAreEquivalent(switchExpression.GoverningExpression, armExpression))
        {
            return false;
        }

        return PatternMatchesNonNullWhenTrue(arm.Pattern, semanticModel) ||
               PreviousSwitchArmHandlesNull(switchExpression, arm, semanticModel);
    }

    private static bool PreviousSwitchArmHandlesNull(
        SwitchExpressionSyntax switchExpression,
        SwitchExpressionArmSyntax currentArm,
        SemanticModel semanticModel)
    {
        return switchExpression.Arms
            .TakeWhile(arm => arm != currentArm)
            .Any(arm => arm.WhenClause == null && PatternMatchesNullWhenTrue(arm.Pattern, semanticModel));
    }

    private static ITypeSymbol? GetUnguardedNullableBranchType(
        ExpressionSyntax branch,
        ExpressionSyntax condition,
        bool branchWhenTrue,
        SemanticModel semanticModel)
    {
        branch = UnwrapParentheses(branch);
        ITypeSymbol? branchType = GetNullableExpressionDeclaredType(branch, semanticModel);
        if (branchType == null)
        {
            return null;
        }

        bool branchIsGuarded = branchWhenTrue
            ? ConditionProvesOperandNonNullWhenTrue(condition, branch, semanticModel)
            : ConditionProvesOperandNonNullWhenFalse(condition, branch, semanticModel);
        return branchIsGuarded ? null : branchType;
    }

    private static bool TryGetNullableElementAccessType(
        ElementAccessExpressionSyntax elementAccess,
        SemanticModel semanticModel,
        out ITypeSymbol? nullableElementType)
    {
        nullableElementType = null;
        ITypeSymbol? receiverType = semanticModel.GetTypeInfo(elementAccess.Expression).Type;
        if (receiverType is IArrayTypeSymbol arrayType &&
            IsNullableType(arrayType.ElementType))
        {
            nullableElementType = arrayType.ElementType;
            return true;
        }

        ISymbol? elementSymbol = semanticModel.GetSymbolInfo(elementAccess).Symbol;
        if (elementSymbol is IPropertySymbol indexerProperty &&
            IsNullableType(indexerProperty.Type))
        {
            nullableElementType = indexerProperty.Type;
            return true;
        }

        return false;
    }

    private static ITypeSymbol? GetNullableTypeFromExpressionTypeInfo(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        TypeInfo typeInfo = semanticModel.GetTypeInfo(expression);
        ITypeSymbol? expressionType = typeInfo.Type ?? typeInfo.ConvertedType;
        if (expressionType != null && IsNullableType(expressionType))
        {
            return expressionType;
        }

        return expressionType?.IsReferenceType == true
            ? expressionType.WithNullableAnnotation(NullableAnnotation.Annotated)
            : null;
    }

    private static ITypeSymbol? GetNullableAnnotationFromExpressionTypeInfo(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        TypeInfo typeInfo = semanticModel.GetTypeInfo(expression);
        ITypeSymbol? expressionType = typeInfo.Type ?? typeInfo.ConvertedType;
        return expressionType != null && IsNullableType(expressionType)
            ? expressionType
            : null;
    }

    private static bool ExpressionCanProduceNull(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        expression = UnwrapParentheses(expression);
        if (expression.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return true;
        }

        if (expression.IsKind(SyntaxKind.DefaultLiteralExpression))
        {
            TypeInfo defaultTypeInfo = semanticModel.GetTypeInfo(expression);
            ITypeSymbol? defaultType = defaultTypeInfo.ConvertedType ?? defaultTypeInfo.Type;
            return defaultType == null || !IsNonNullableValueType(defaultType);
        }

        if (expression is DefaultExpressionSyntax defaultExpression)
        {
            TypeInfo defaultTypeInfo = semanticModel.GetTypeInfo(defaultExpression.Type);
            ITypeSymbol? defaultType = defaultTypeInfo.Type ?? defaultTypeInfo.ConvertedType;
            return defaultType == null || !IsNonNullableValueType(defaultType);
        }

        if (expression is PostfixUnaryExpressionSyntax suppression &&
            suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression))
        {
            return TryGetNullableSuppressedOperandType(suppression, semanticModel, out _);
        }

        return GetNullableExpressionDeclaredType(expression, semanticModel) != null;
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesizedExpression)
        {
            expression = parenthesizedExpression.Expression;
        }

        return expression;
    }

    private static bool IsSuppressedNullableReceiverDereferenced(
        PostfixUnaryExpressionSyntax suppression,
        SemanticModel semanticModel)
    {
        ExpressionSyntax receiverExpression = suppression;
        SyntaxNode? parent = receiverExpression.Parent;
        while (parent != null)
        {
            if (parent is ParenthesizedExpressionSyntax parenthesizedExpression &&
                parenthesizedExpression.Expression == receiverExpression)
            {
                receiverExpression = parenthesizedExpression;
                parent = parenthesizedExpression.Parent;
                continue;
            }

            if (parent is CastExpressionSyntax castExpression &&
                castExpression.Expression == receiverExpression)
            {
                receiverExpression = castExpression;
                parent = castExpression.Parent;
                continue;
            }

            if (parent is ConditionalExpressionSyntax conditionalExpression &&
                (conditionalExpression.WhenTrue == receiverExpression ||
                 conditionalExpression.WhenFalse == receiverExpression))
            {
                receiverExpression = conditionalExpression;
                parent = conditionalExpression.Parent;
                continue;
            }

            if (parent is BinaryExpressionSyntax coalesceExpression &&
                coalesceExpression.IsKind(SyntaxKind.CoalesceExpression) &&
                coalesceExpression.Right == receiverExpression)
            {
                if (!ExpressionCanProduceNullAt(coalesceExpression.Left, coalesceExpression, semanticModel))
                {
                    return false;
                }

                receiverExpression = coalesceExpression;
                parent = coalesceExpression.Parent;
                continue;
            }

            break;
        }

        if (parent is ElementAccessExpressionSyntax elementAccess &&
            elementAccess.Expression == receiverExpression)
        {
            return true;
        }

        if (parent is InvocationExpressionSyntax invocation &&
            invocation.Expression == receiverExpression)
        {
            return true;
        }

        if (parent is not MemberAccessExpressionSyntax memberAccess ||
            memberAccess.Expression != receiverExpression)
        {
            return false;
        }

        return !IsSafeNullableValueMemberAccess(memberAccess) &&
               !IsExtensionMethodReceiverAccess(memberAccess, semanticModel);
    }

    private static bool TryGetNullableDereferencedReceiver(
        ExpressionSyntax expression,
        ITypeSymbol destinationPropertyType,
        SemanticModel semanticModel,
        out ITypeSymbol? nullableReceiverType,
        out string? nullableReceiverName)
    {
        nullableReceiverType = null;
        nullableReceiverName = null;
        foreach (MemberAccessExpressionSyntax memberAccess in expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
        {
            if (IsSafeNullableValueMemberAccess(memberAccess))
            {
                continue;
            }

            if (IsGuardedSuppressedNullableReceiverAccess(memberAccess, semanticModel))
            {
                continue;
            }

            TypeInfo receiverTypeInfo = semanticModel.GetTypeInfo(memberAccess.Expression);
            ITypeSymbol? receiverType = receiverTypeInfo.Type ?? receiverTypeInfo.ConvertedType;
            if (receiverType == null ||
                !IsNullableType(receiverType) ||
                IsNullableType(destinationPropertyType))
            {
                continue;
            }

            nullableReceiverType = receiverType;
            nullableReceiverName = TryGetSourceMemberPath(memberAccess.Expression, out string sourceMemberPath)
                ? sourceMemberPath
                : null;
            return true;
        }

        return false;
    }

    private static bool TryGetSourceMemberPath(ExpressionSyntax expression, out string sourceMemberPath)
    {
        sourceMemberPath = string.Empty;
        if (TryGetSourceMemberPathFromExpression(expression, out sourceMemberPath))
        {
            return true;
        }

        foreach (MemberAccessExpressionSyntax memberAccess in expression.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (TryGetSourceMemberPathFromExpression(memberAccess, out sourceMemberPath))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetSourceMemberPathFromExpression(ExpressionSyntax expression, out string sourceMemberPath)
    {
        sourceMemberPath = string.Empty;
        if (expression is InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax invocationMemberAccess
            })
        {
            return TryGetSourceMemberPathFromExpression(invocationMemberAccess.Expression, out sourceMemberPath);
        }

        expression = UnwrapParentheses(expression);
        if (expression is PostfixUnaryExpressionSyntax suppression &&
            suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression))
        {
            expression = UnwrapParentheses(suppression.Operand);
        }

        if (expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var pathSegments = new Stack<string>();
        ExpressionSyntax currentExpression = memberAccess;
        while (true)
        {
            currentExpression = UnwrapParentheses(currentExpression);
            if (currentExpression is PostfixUnaryExpressionSyntax currentSuppression &&
                currentSuppression.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            {
                currentExpression = currentSuppression.Operand;
                continue;
            }

            if (currentExpression is not MemberAccessExpressionSyntax currentMemberAccess)
            {
                break;
            }

            pathSegments.Push(currentMemberAccess.Name.Identifier.ValueText);
            currentExpression = currentMemberAccess.Expression;
        }

        currentExpression = UnwrapParentheses(currentExpression);
        if (currentExpression is not IdentifierNameSyntax || pathSegments.Count == 0)
        {
            return false;
        }

        sourceMemberPath = string.Join(".", pathSegments);
        return true;
    }

    private static void AnalyzeNullableCompatibility(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IPropertySymbol sourceProperty,
        IPropertySymbol destinationProperty,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        string sourceTypeName = sourceProperty.Type.ToDisplayString();
        string destTypeName = destinationProperty.Type.ToDisplayString();

        // Case 1: Nullable source -> Non-nullable destination (ERROR)
        if (IsNullableType(sourceProperty.Type) && !IsNullableType(destinationProperty.Type))
        {
            // Check if the underlying types are compatible
            ITypeSymbol sourceUnderlyingType = AutoMapperAnalysisHelpers.GetUnderlyingType(sourceProperty.Type);
            ITypeSymbol destUnderlyingType = AutoMapperAnalysisHelpers.GetUnderlyingType(destinationProperty.Type);

            if (AreUnderlyingTypesCompatible(sourceUnderlyingType, destUnderlyingType))
            {
                ReportNullableToNonNullableDiagnostic(
                    context,
                    invocation,
                    sourceProperty.Name,
                    sourceProperty.Name,
                    sourceProperty.Type,
                    sourceType,
                    destinationType,
                    destinationProperty.Type);
            }
        }
        // Case 2: Non-nullable source -> Nullable destination (INFO)
        else if (!IsNullableType(sourceProperty.Type) && IsNullableType(destinationProperty.Type))
        {
            ITypeSymbol sourceUnderlyingType = AutoMapperAnalysisHelpers.GetUnderlyingType(sourceProperty.Type);
            ITypeSymbol destUnderlyingType = AutoMapperAnalysisHelpers.GetUnderlyingType(destinationProperty.Type);

            if (AreUnderlyingTypesCompatible(sourceUnderlyingType, destUnderlyingType))
            {
                var properties = ImmutableDictionary.CreateBuilder<string, string?>();
                properties.Add(PropertyNamePropertyName, sourceProperty.Name);
                properties.Add(SourcePropertyTypePropertyName, sourceTypeName);
                properties.Add(DestinationPropertyTypePropertyName, destTypeName);

                var diagnostic = Diagnostic.Create(
                    NonNullableToNullableRule,
                    invocation.GetLocation(),
                    properties.ToImmutable(),
                    sourceProperty.Name,
                    GetDiagnosticTypeName(sourceType),
                    sourceTypeName,
                    GetDiagnosticTypeName(destinationType),
                    destTypeName
                );
                context.ReportDiagnostic(diagnostic);
            }
        }
        // Case 3: Collection whose element nullability is lost (e.g. List<string?> -> List<string>).
        // The container is non-nullable on both sides (so Cases 1/2 do not apply), but a nullable source
        // element flows into a non-nullable destination element — the same NRE risk one level down.
        else if (HasNullableElementMismatch(sourceProperty.Type, destinationProperty.Type))
        {
            ReportNullableToNonNullableDiagnostic(
                context,
                invocation,
                sourceProperty.Name,
                sourceProperty.Name,
                sourceProperty.Type,
                sourceType,
                destinationType,
                destinationProperty.Type,
                isElementNullability: true);
        }
    }

    /// <summary>
    ///     Determines whether the source collection's reference-type element is nullable while the
    ///     destination collection's element of the same underlying type is non-nullable. Value-type nullable
    ///     elements and genuine element-type mismatches are intentionally excluded — those stay with
    ///     AM001/AM021 so the rules never double-report.
    /// </summary>
    private static bool HasNullableElementMismatch(ITypeSymbol sourceType, ITypeSymbol destType)
    {
        if (!AutoMapperAnalysisHelpers.IsCollectionType(sourceType) ||
            !AutoMapperAnalysisHelpers.IsCollectionType(destType))
        {
            return false;
        }

        ITypeSymbol? sourceElement = AutoMapperAnalysisHelpers.GetCollectionElementType(sourceType);
        ITypeSymbol? destElement = AutoMapperAnalysisHelpers.GetCollectionElementType(destType);
        if (sourceElement == null || destElement == null)
        {
            return false;
        }

        if (!sourceElement.IsReferenceType || !destElement.IsReferenceType)
        {
            return false;
        }

        if (!IsNullableType(sourceElement) || IsNullableType(destElement))
        {
            return false;
        }

        // Same underlying reference type, differing only in nullability annotation (e.g. string? vs string).
        return SymbolEqualityComparer.Default.Equals(
            sourceElement.WithNullableAnnotation(NullableAnnotation.NotAnnotated),
            destElement.WithNullableAnnotation(NullableAnnotation.NotAnnotated));
    }

    private static void ReportNullableToNonNullableDiagnostic(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        string destinationPropertyName,
        string sourcePropertyName,
        ITypeSymbol sourcePropertyType,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        ITypeSymbol destinationPropertyType,
        bool isElementNullability = false,
        string? sourceDisplayTypeName = null)
    {
        string sourceTypeName = sourcePropertyType.ToDisplayString();
        string destTypeName = destinationPropertyType.ToDisplayString();

        var properties = ImmutableDictionary.CreateBuilder<string, string?>();
        properties.Add(PropertyNamePropertyName, destinationPropertyName);
        properties.Add(SourcePropertyNamePropertyName, sourcePropertyName);
        properties.Add(SourcePropertyTypePropertyName, sourceTypeName);
        properties.Add(DestinationPropertyTypePropertyName, destTypeName);
        if (isElementNullability)
        {
            properties.Add(ElementNullabilityPropertyName, "true");
        }

        var diagnostic = Diagnostic.Create(
            NullableToNonNullableRule,
            invocation.GetLocation(),
            properties.ToImmutable(),
            destinationPropertyName,
            sourceDisplayTypeName ?? GetDiagnosticTypeName(sourceType),
            sourcePropertyName,
            sourceTypeName,
            GetDiagnosticTypeName(destinationType),
            destTypeName
        );
        context.ReportDiagnostic(diagnostic);
    }

    private static string GetDiagnosticTypeName(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { IsGenericType: true } genericType)
        {
            return genericType.ToDisplayString(new SymbolDisplayFormat(
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes));
        }

        return AutoMapperAnalysisHelpers.GetTypeName(type);
    }

    private static bool IsNullableToNonNullableCompatible(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        if (!IsNullableType(sourceType) || IsNullableType(destinationType))
        {
            return false;
        }

        return AreUnderlyingTypesCompatible(
            AutoMapperAnalysisHelpers.GetUnderlyingType(sourceType),
            AutoMapperAnalysisHelpers.GetUnderlyingType(destinationType));
    }

    private static bool IsNullableType(ITypeSymbol type)
    {
        // Check for nullable reference types (string?, object?, etc.)
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }

        // Check for nullable value types (int?, DateTime?, etc.)
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return true;
        }

        // Check by string representation for cases where annotation might not be detected
        string typeString = type.ToDisplayString();
        return typeString.EndsWith("?");
    }

    private static bool AreUnderlyingTypesCompatible(ITypeSymbol sourceType, ITypeSymbol destType)
    {
        // Use helper for comprehensive compatibility checking
        return AutoMapperAnalysisHelpers.AreTypesCompatible(sourceType, destType);
    }


    private static bool IsAutoMapperCreateMapInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        return MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "CreateMap");
    }


}

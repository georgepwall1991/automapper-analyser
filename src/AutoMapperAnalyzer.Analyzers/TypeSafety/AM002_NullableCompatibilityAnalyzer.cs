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

        InvocationExpressionSyntax? reverseMapInvocation =
            AutoMapperAnalysisHelpers.GetReverseMapInvocation(invocationExpr);
        if (reverseMapInvocation != null)
        {
            AnalyzeNullablePropertyMappings(
                context,
                reverseMapInvocation,
                destinationType,
                sourceType
            );
        }
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
                    out string? explicitNullableSourceName);
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
                    destinationProperty.Type);
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
                        out string? explicitNullableSourceName);
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
        out string? explicitNullableSourceName)
    {
        handlesNullability = false;
        explicitNullableSourceType = null;
        explicitNullableSourceName = null;
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
                    out string? nullableMapFromName))
            {
                if (!ExpressionDereferencesNullableReceiver(mapFromBody, semanticModel) &&
                    ConfigurationCallsSafeNullSubstitute(effectiveMappingCall, destinationPropertyType, semanticModel))
                {
                    handlesNullability = true;
                    return true;
                }

                explicitNullableSourceType = nullableMapFromType;
                explicitNullableSourceName = nullableMapFromName;
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
                AM020MappingConfigurationHelpers.GetSelectedTopLevelMemberNameWithSemanticModel(
                    destinationExpression,
                    semanticModel);
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
        ITypeSymbol? mappedType = GetNullableAwareExpressionType(
            mapFromBody,
            semanticModel,
            includeDeclaredNullability: true);
        if (mappedType == null)
        {
            return false;
        }

        return !ExpressionDereferencesNullableReceiver(mapFromBody, semanticModel) &&
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

    private static bool IsNullableExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        ITypeSymbol? expressionType = GetNullableAwareExpressionType(expression, semanticModel);
        return expressionType != null && IsNullableType(expressionType);
    }

    private static ITypeSymbol? GetNullableAwareExpressionType(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        bool includeDeclaredNullability = false)
    {
        ExpressionSyntax nullableTransparentExpression = RemoveNullForgivingExpression(expression);
        TypeInfo typeInfo = semanticModel.GetTypeInfo(expression);
        ITypeSymbol? expressionType = typeInfo.Type ?? typeInfo.ConvertedType;
        if (expressionType != null && IsNullableType(expressionType))
        {
            return expressionType;
        }

        if (!includeDeclaredNullability && nullableTransparentExpression == expression)
        {
            return expressionType;
        }

        ISymbol? symbol = semanticModel.GetSymbolInfo(nullableTransparentExpression).Symbol;
        return symbol switch
        {
            IPropertySymbol propertySymbol => propertySymbol.Type,
            IFieldSymbol fieldSymbol => fieldSymbol.Type,
            ILocalSymbol localSymbol => localSymbol.Type,
            IParameterSymbol parameterSymbol => parameterSymbol.Type,
            _ => expressionType
        };
    }

    private static ExpressionSyntax RemoveNullForgivingExpression(ExpressionSyntax expression)
    {
        while (expression is PostfixUnaryExpressionSyntax postfix &&
               postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression))
        {
            expression = postfix.Operand;
        }

        return expression;
    }

    private static bool TryGetNullableMapFromType(
        ExpressionSyntax mapFromBody,
        ITypeSymbol destinationPropertyType,
        SemanticModel semanticModel,
        out ITypeSymbol? nullableMapFromType,
        out string? nullableMapFromName)
    {
        nullableMapFromType = null;
        nullableMapFromName = null;
        ExpressionSyntax nullableTransparentBody = RemoveNullForgivingExpression(mapFromBody);
        ITypeSymbol? mappedType = GetNullableAwareExpressionType(
            nullableTransparentBody,
            semanticModel,
            includeDeclaredNullability: true);
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
        nullableMapFromName = TryGetSourceMemberPath(nullableTransparentBody, out string sourceMemberPath)
            ? sourceMemberPath
            : null;
        return true;
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

            ITypeSymbol? receiverType = GetNullableAwareExpressionType(memberAccess.Expression, semanticModel);
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

        if (expression is not MemberAccessExpressionSyntax memberAccess)
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
        bool isElementNullability = false)
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
            GetDiagnosticTypeName(sourceType),
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

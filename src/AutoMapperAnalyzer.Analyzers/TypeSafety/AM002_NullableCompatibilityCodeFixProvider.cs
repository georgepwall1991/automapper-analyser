using System.Collections.Immutable;
using System.Composition;
using System.Text.RegularExpressions;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Analyzers.TypeSafety;

/// <summary>
///     Code fix provider for AM002 Nullable Compatibility diagnostics.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM002_NullableCompatibilityCodeFixProvider))]
[Shared]
public class AM002_NullableCompatibilityCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <summary>
    ///     Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM002");

    /// <summary>
    ///     Registers code fixes for the diagnostics.
    /// </summary>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        CodeFixOperationContext? operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        ImmutableArray<Diagnostic> diagnostics = context.Diagnostics
            .Where(diag =>
                diag.Id == "AM002" &&
                diag.Descriptor == AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule &&
                diag.Location.IsInSource &&
                diag.Location.SourceTree == operationContext.Root.SyntaxTree &&
                diag.Location.SourceSpan.IntersectsWith(context.Span))
            .ToImmutableArray();

        foreach (Diagnostic? diagnostic in diagnostics)
        {
            if (diagnostic == null)
            {
                continue;
            }

            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

            InvocationExpressionSyntax? invocation = GetCreateMapInvocation(operationContext.Root, diagnostic) ??
                operationContext.Root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf()
                    .OfType<InvocationExpressionSyntax>()
                    .FirstOrDefault(i => i.Span.Contains(diagnosticSpan));

            if (invocation == null)
            {
                continue;
            }

            string? propertyName = ExtractPropertyNameFromDiagnostic(diagnostic);
            if (string.IsNullOrEmpty(propertyName))
            {
                continue;
            }

            string sourceMemberName =
                diagnostic.Properties.TryGetValue(
                    AM002_NullableCompatibilityAnalyzer.SourcePropertyNamePropertyName,
                    out string? configuredSourceMemberName) &&
                !string.IsNullOrWhiteSpace(configuredSourceMemberName)
                    ? configuredSourceMemberName!
                    : propertyName!;
            bool sourceMemberRequiresInvocation =
                diagnostic.Properties.TryGetValue(
                    AM002_NullableCompatibilityAnalyzer.SourceMemberRequiresInvocationPropertyName,
                    out string? invocationFlag) &&
                string.Equals(invocationFlag, "true", StringComparison.Ordinal);

            // A "src.X ?? default" coalesce scaffold cannot fix element-level nullability (the elements of
            // List<string?> are still nullable), so the element-nullability diagnostic gets only the
            // manual-review ignore action below.
            bool isElementNullability =
                diagnostic.Properties.TryGetValue(
                    AM002_NullableCompatibilityAnalyzer.ElementNullabilityPropertyName, out string? elementFlag) &&
                string.Equals(elementFlag, "true", StringComparison.Ordinal);

            InvocationExpressionSyntax? existingConfiguration = FindDestinationConfiguration(
                invocation,
                propertyName!,
                operationContext.SemanticModel);
            bool isConstructorParameterConfiguration = existingConfiguration != null &&
                                                       MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                                                           existingConfiguration,
                                                           operationContext.SemanticModel,
                                                           "ForCtorParam");
            (ITypeSymbol? sourceMapType, ITypeSymbol? destinationMapType) =
                ResolveCreateMapTypesWithReverse(invocation, operationContext.SemanticModel);
            bool ignoreWouldExposeUnsafeConstructorAlias =
                sourceMapType != null &&
                destinationMapType != null &&
                AM002_NullableCompatibilityAnalyzer
                    .HasNullableConfiguredConstructorInputForDestinationProperty(
                        invocation,
                        propertyName!,
                        sourceMapType,
                        destinationMapType,
                        operationContext.SemanticModel);
            string? destinationType = ExtractDestinationTypeFromDiagnostic(diagnostic);
            string? defaultValue = GetDefaultValueForDestination(
                invocation,
                propertyName!,
                destinationType,
                existingConfiguration,
                operationContext.SemanticModel);
            if (!isElementNullability &&
                defaultValue != null &&
                (existingConfiguration == null ||
                (!ConfigurationCanVetoAssignment(existingConfiguration, operationContext.SemanticModel) &&
                 ConfigurationCanAcceptDefaultValueFix(existingConfiguration, operationContext.SemanticModel))))
            {
                // Option 1: Map with default value
                context.RegisterCodeFix(
                    CodeAction.Create(
                        $"Scaffold default mapping for '{propertyName}' ({defaultValue})",
                        cancellationToken => AddMapFromAsync(context.Document, invocation, propertyName!,
                            $"src.{CodeFixSyntaxHelper.EscapeIdentifier(sourceMemberName)}{(sourceMemberRequiresInvocation ? "()" : string.Empty)} ?? {defaultValue}",
                            defaultValue,
                            operationContext.SemanticModel,
                            cancellationToken),
                        $"AM002_DefaultValue_{propertyName}"),
                    diagnostic);
            }

            // Option 2: Ignore property
            if (!isConstructorParameterConfiguration &&
                !ignoreWouldExposeUnsafeConstructorAlias)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        $"Ignore property '{propertyName}' (manual review)",
                        cancellationToken => AddIgnoreAsync(
                            context.Document,
                            invocation,
                            propertyName!,
                            operationContext.SemanticModel,
                            cancellationToken),
                        $"AM002_Ignore_{propertyName}"),
                    diagnostic);
            }
        }
    }

    private static string? GetDefaultValueForDestination(
        InvocationExpressionSyntax invocation,
        string propertyName,
        string? destinationType,
        InvocationExpressionSyntax? existingConfiguration,
        SemanticModel semanticModel)
    {
        string defaultValue = TypeConversionHelper.GetDefaultValueForType(destinationType ?? string.Empty);
        if (defaultValue != "default")
        {
            return defaultValue;
        }

        (ITypeSymbol? _, ITypeSymbol? destinationMapType) =
            ResolveCreateMapTypesWithReverse(invocation, semanticModel);
        if (destinationMapType == null)
        {
            return null;
        }

        bool isConstructorParameterConfiguration = existingConfiguration != null &&
                                                   MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                                                       existingConfiguration,
                                                       semanticModel,
                                                       "ForCtorParam");
        if (isConstructorParameterConfiguration)
        {
            if (destinationMapType is not INamedTypeSymbol namedDestinationType ||
                existingConfiguration!.ArgumentList.Arguments.Count == 0)
            {
                return null;
            }

            string? constructorParameterName =
                AM020MappingConfigurationHelpers.GetSelectedTopLevelMemberNameWithSemanticModel(
                    existingConfiguration.ArgumentList.Arguments[0].Expression,
                    semanticModel);
            ITypeSymbol[] matchingParameterTypes = namedDestinationType.InstanceConstructors
                .SelectMany(constructor => constructor.Parameters)
                .Where(parameter => string.Equals(
                    parameter.Name,
                    constructorParameterName,
                    StringComparison.Ordinal))
                .Select(parameter => parameter.Type)
                .ToArray();
            return matchingParameterTypes.Length > 0 &&
                   matchingParameterTypes.All(IsDefinitelyNonNullableValueType)
                ? defaultValue
                : null;
        }

        IPropertySymbol? destinationProperty = AutoMapperAnalysisHelpers
            .GetMappableProperties(destinationMapType, requireSetter: false)
            .FirstOrDefault(property =>
                string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase));
        if (destinationProperty == null)
        {
            return defaultValue;
        }

        return NeedsNullForgivingDefault(destinationProperty.Type) ? "default!" : defaultValue;
    }

    private static bool IsDefinitelyNonNullableValueType(ITypeSymbol type)
    {
        if (type is ITypeParameterSymbol typeParameter)
        {
            return typeParameter.HasValueTypeConstraint;
        }

        return type.IsValueType &&
               !(type is INamedTypeSymbol namedType &&
                 namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);
    }

    private static bool NeedsNullForgivingDefault(ITypeSymbol destinationPropertyType)
    {
        if (destinationPropertyType is ITypeParameterSymbol typeParameter)
        {
            return !typeParameter.HasValueTypeConstraint;
        }

        return destinationPropertyType.IsReferenceType &&
               destinationPropertyType.NullableAnnotation != NullableAnnotation.Annotated;
    }

    private async Task<Document> AddMapFromAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string propertyName,
        string mapFromExpression,
        string defaultValue,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        InvocationExpressionSyntax? existingConfiguration = FindDestinationConfiguration(
            invocation,
            propertyName,
            semanticModel);
        if (existingConfiguration != null)
        {
            if (ConfigurationCanVetoAssignment(existingConfiguration, semanticModel))
            {
                return document;
            }

            if (TryReplaceMapFromExpression(
                    existingConfiguration,
                    defaultValue,
                    semanticModel,
                    out InvocationExpressionSyntax? mapFromReplacement) &&
                mapFromReplacement != null)
            {
                return await ReplaceNodeAsync(document, root, existingConfiguration, mapFromReplacement);
            }

            if (TryAppendMapFromExpression(existingConfiguration, mapFromExpression, out InvocationExpressionSyntax? appendedMapFrom) &&
                appendedMapFrom != null)
            {
                return await ReplaceNodeAsync(document, root, existingConfiguration, appendedMapFrom);
            }

            return document;
        }

        InvocationExpressionSyntax newInvocation =
            CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName, mapFromExpression);
        return await ReplaceNodeAsync(document, root, invocation, newInvocation);
    }

    private async Task<Document> AddIgnoreAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string propertyName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        InvocationExpressionSyntax? existingConfiguration = FindDestinationConfiguration(
            invocation,
            propertyName,
            semanticModel);
        if (existingConfiguration != null)
        {
            InvocationExpressionSyntax replacement =
                CreateReplacementForMemberWithIgnore(existingConfiguration, propertyName);
            return await ReplaceNodeAsync(document, root, existingConfiguration, replacement);
        }

        InvocationExpressionSyntax newInvocation =
            CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
        return await ReplaceNodeAsync(document, root, invocation, newInvocation);
    }

    private static InvocationExpressionSyntax? FindDestinationConfiguration(
        InvocationExpressionSyntax createMapInvocation,
        string propertyName,
        SemanticModel semanticModel)
    {
        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) =
            ResolveCreateMapTypesWithReverse(createMapInvocation, semanticModel);
        var configuredConstructorParameterNames = new HashSet<string>(StringComparer.Ordinal);
        SyntaxNode? configurationNode = createMapInvocation.Parent;
        while (configurationNode is MemberAccessExpressionSyntax configurationMemberAccess &&
               configurationMemberAccess.Parent is InvocationExpressionSyntax configurationInvocation)
        {
            if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                    configurationInvocation,
                    semanticModel,
                    "ReverseMap"))
            {
                break;
            }

            if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                    configurationInvocation,
                    semanticModel,
                    "ForCtorParam") &&
                configurationInvocation.ArgumentList.Arguments.Count > 0)
            {
                string? configuredConstructorParameterName =
                    AM020MappingConfigurationHelpers.GetSelectedTopLevelMemberNameWithSemanticModel(
                        configurationInvocation.ArgumentList.Arguments[0].Expression,
                        semanticModel);
                if (!string.IsNullOrWhiteSpace(configuredConstructorParameterName))
                {
                    configuredConstructorParameterNames.Add(configuredConstructorParameterName!);
                }
            }

            configurationNode = configurationInvocation.Parent;
        }

        SyntaxNode? currentNode = createMapInvocation.Parent;
        InvocationExpressionSyntax? effectiveConfiguration = null;
        InvocationExpressionSyntax? effectiveConstructorParameterConfiguration = null;

        while (currentNode is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                    chainedInvocation,
                    semanticModel,
                    "ReverseMap"))
            {
                break;
            }

            if (chainedInvocation.ArgumentList.Arguments.Count > 0 &&
                DestinationConfigurationTargetsTopLevelMember(
                    chainedInvocation,
                    semanticModel,
                    sourceType,
                    destinationType,
                    configuredConstructorParameterNames,
                    propertyName))
            {
                if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                        chainedInvocation,
                        semanticModel,
                        "ForCtorParam"))
                {
                    effectiveConstructorParameterConfiguration = chainedInvocation;
                }
                else
                {
                    effectiveConfiguration = chainedInvocation;
                }
            }

            currentNode = chainedInvocation.Parent;
        }

        return effectiveConstructorParameterConfiguration ?? effectiveConfiguration;
    }

    private static bool ConfigurationCanVetoAssignment(
        InvocationExpressionSyntax existingConfiguration,
        SemanticModel semanticModel)
    {
        return AM002_NullableCompatibilityAnalyzer.GetDirectlyExecutedOptionMethodInvocations(
                   existingConfiguration,
                   "Condition",
                   semanticModel).Count > 0 ||
               AM002_NullableCompatibilityAnalyzer.GetDirectlyExecutedOptionMethodInvocations(
                   existingConfiguration,
                   "PreCondition",
                   semanticModel).Count > 0;
    }

    private static bool ConfigurationCanAcceptDefaultValueFix(
        InvocationExpressionSyntax existingConfiguration,
        SemanticModel semanticModel)
    {
        IReadOnlyList<InvocationExpressionSyntax> mapFromInvocations =
            AM002_NullableCompatibilityAnalyzer.GetDirectlyExecutedOptionMethodInvocations(
                existingConfiguration,
                "MapFrom",
                semanticModel);
        if (mapFromInvocations.Count == 0)
        {
            return true;
        }

        if (mapFromInvocations.Count != 1 ||
            mapFromInvocations[0].ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        ExpressionSyntax mapFromArgument =
            mapFromInvocations[0].ArgumentList.Arguments[0].Expression;

        return MapFromArgumentCanBeCoalesced(mapFromArgument, semanticModel);
    }

    private static bool DestinationConfigurationTargetsTopLevelMember(
        InvocationExpressionSyntax chainedInvocation,
        SemanticModel semanticModel,
        ITypeSymbol? sourceType,
        ITypeSymbol? destinationType,
        IReadOnlyCollection<string> configuredConstructorParameterNames,
        string propertyName)
    {
        ExpressionSyntax destinationExpression = chainedInvocation.ArgumentList.Arguments[0].Expression;
        if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ForMember"))
        {
            string? selectedMember =
                AM020MappingConfigurationHelpers.GetSelectedTopLevelMemberNameWithSemanticModel(
                    destinationExpression,
                    semanticModel);
            return string.Equals(selectedMember, propertyName, StringComparison.OrdinalIgnoreCase);
        }

        if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ForCtorParam"))
        {
            string? selectedConstructorParameter =
                AM020MappingConfigurationHelpers.GetSelectedTopLevelMemberNameWithSemanticModel(
                    destinationExpression,
                    semanticModel);
            if (sourceType == null || destinationType == null)
            {
                return false;
            }

            if (string.Equals(
                    selectedConstructorParameter,
                    propertyName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return AM020MappingConfigurationHelpers.GetConstructorParameterTypes(
                    destinationType,
                    sourceType,
                    selectedConstructorParameter ?? string.Empty,
                    configuredConstructorParameterNames).Count > 0;
            }

            string? assignedPropertyName =
                AM020MappingConfigurationHelpers.GetDestinationPropertyNameForConstructorParameter(
                    destinationType,
                    sourceType,
                    selectedConstructorParameter ?? string.Empty,
                    configuredConstructorParameterNames,
                    semanticModel);
            return string.Equals(
                       assignedPropertyName,
                       propertyName,
                       StringComparison.OrdinalIgnoreCase) &&
                   !AM020MappingConfigurationHelpers.CanMapDestinationPropertyAfterConstruction(
                       destinationType,
                       propertyName);
        }

        return MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ForPath") &&
               TryGetSelectedMemberPath(destinationExpression, out string memberPath) &&
               !memberPath.Contains('.') &&
               string.Equals(memberPath, propertyName, StringComparison.OrdinalIgnoreCase);
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

    private static bool TryReplaceMapFromExpression(
        InvocationExpressionSyntax existingConfiguration,
        string defaultValue,
        SemanticModel semanticModel,
        out InvocationExpressionSyntax? replacement)
    {
        replacement = null;
        if (!TryGetMapFromArgument(
                existingConfiguration,
                semanticModel,
                out ExpressionSyntax? mapFromArgument) ||
            mapFromArgument == null)
        {
            return false;
        }

        InvocationExpressionSyntax? mapFromInvocation = mapFromArgument
            .AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(invocation =>
                invocation.ArgumentList.Arguments.Any(argument => argument.Expression == mapFromArgument));

        if (mapFromInvocation == null)
        {
            return false;
        }

        if (!MapFromArgumentCanBeCoalesced(mapFromArgument, semanticModel) ||
            !TryCreateCoalescedMapFromArgument(mapFromArgument, defaultValue, out ExpressionSyntax? replacementArgument) ||
            replacementArgument == null)
        {
            return false;
        }

        InvocationExpressionSyntax replacementMapFrom = mapFromInvocation.ReplaceNode(
            mapFromArgument,
            replacementArgument.WithTriviaFrom(mapFromArgument));
        replacement = existingConfiguration.ReplaceNode(mapFromInvocation, replacementMapFrom);
        return true;
    }

    private static bool TryGetMapFromArgument(
        InvocationExpressionSyntax existingConfiguration,
        SemanticModel semanticModel,
        out ExpressionSyntax? mapFromArgument)
    {
        mapFromArgument = null;
        IReadOnlyList<InvocationExpressionSyntax> mapFromInvocations =
            AM002_NullableCompatibilityAnalyzer.GetDirectlyExecutedOptionMethodInvocations(
                existingConfiguration,
                "MapFrom",
                semanticModel);
        if (mapFromInvocations.Count != 1 ||
            mapFromInvocations[0].ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        mapFromArgument = mapFromInvocations[0].ArgumentList.Arguments[0].Expression;
        return mapFromArgument != null;
    }

    private static bool MapFromArgumentCanBeCoalesced(
        ExpressionSyntax mapFromArgument,
        SemanticModel semanticModel)
    {
        ExpressionSyntax? body = mapFromArgument switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Body as ExpressionSyntax,
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda => parenthesizedLambda.Body as ExpressionSyntax,
            _ => null
        };
        if (body == null)
        {
            return false;
        }

        ExpressionSyntax coalescingOperand = UnwrapNullForgivingExpression(
            body,
            out bool removedNullForgivingOperator);
        ITypeSymbol? declaredSymbolType = GetDeclaredSymbolType(
            semanticModel.GetSymbolInfo(coalescingOperand).Symbol);
        bool isNullForgivenNullableSymbol = removedNullForgivingOperator &&
                                             declaredSymbolType != null &&
                                             IsNullableType(declaredSymbolType);
        TypeInfo typeInfo = semanticModel.GetTypeInfo(coalescingOperand);
        ITypeSymbol? bodyType = typeInfo.Type ?? typeInfo.ConvertedType;
        return bodyType != null &&
               (IsNullableType(bodyType) || isNullForgivenNullableSymbol) &&
               !ExpressionDereferencesNullableReceiver(coalescingOperand, semanticModel);
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
        ExpressionSyntax nullableTransparentExpression = UnwrapNullForgivingExpression(
            expression,
            out bool removedNullForgivingOperator);
        TypeInfo typeInfo = semanticModel.GetTypeInfo(nullableTransparentExpression);
        ITypeSymbol? expressionType = typeInfo.Type ?? typeInfo.ConvertedType;
        if (expressionType != null && IsNullableType(expressionType))
        {
            return true;
        }

        ITypeSymbol? declaredSymbolType = GetDeclaredSymbolType(
            semanticModel.GetSymbolInfo(nullableTransparentExpression).Symbol);
        return removedNullForgivingOperator &&
               declaredSymbolType != null &&
               IsNullableType(declaredSymbolType);
    }

    private static ITypeSymbol? GetDeclaredSymbolType(ISymbol? symbol)
    {
        return symbol switch
        {
            IPropertySymbol propertySymbol => propertySymbol.Type,
            IFieldSymbol fieldSymbol => fieldSymbol.Type,
            ILocalSymbol localSymbol => localSymbol.Type,
            IParameterSymbol parameterSymbol => parameterSymbol.Type,
            _ => null
        };
    }

    private static bool IsNullableType(ITypeSymbol type)
    {
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }

        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return true;
        }

        return type.ToDisplayString().EndsWith("?", StringComparison.Ordinal);
    }

    private static bool TryCreateCoalescedMapFromArgument(
        ExpressionSyntax mapFromArgument,
        string defaultValue,
        out ExpressionSyntax? replacementArgument)
    {
        replacementArgument = mapFromArgument switch
        {
            SimpleLambdaExpressionSyntax simpleLambda =>
                CreateLambdaWithCoalescedBody(simpleLambda, simpleLambda.Body as ExpressionSyntax, defaultValue),
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda =>
                CreateLambdaWithCoalescedBody(parenthesizedLambda, parenthesizedLambda.Body as ExpressionSyntax, defaultValue),
            _ => null
        };

        return replacementArgument != null;
    }

    private static SimpleLambdaExpressionSyntax? CreateLambdaWithCoalescedBody(
        SimpleLambdaExpressionSyntax lambda,
        ExpressionSyntax? body,
        string defaultValue)
    {
        return body == null ? null : lambda.WithBody(CreateCoalescedExpression(body, defaultValue));
    }

    private static ParenthesizedLambdaExpressionSyntax? CreateLambdaWithCoalescedBody(
        ParenthesizedLambdaExpressionSyntax lambda,
        ExpressionSyntax? body,
        string defaultValue)
    {
        return body == null ? null : lambda.WithBody(CreateCoalescedExpression(body, defaultValue));
    }

    private static ExpressionSyntax CreateCoalescedExpression(
        ExpressionSyntax body,
        string defaultValue)
    {
        body = UnwrapNullForgivingExpression(body, out _);
        string expressionText = NeedsParenthesesForCoalesce(body)
            ? $"({body}) ?? {defaultValue}"
            : $"{body} ?? {defaultValue}";
        return SyntaxFactory.ParseExpression(expressionText).WithTriviaFrom(body);
    }

    private static ExpressionSyntax UnwrapNullForgivingExpression(
        ExpressionSyntax expression,
        out bool removedNullForgivingOperator)
    {
        if (expression is PostfixUnaryExpressionSyntax postfixUnary &&
            postfixUnary.IsKind(SyntaxKind.SuppressNullableWarningExpression))
        {
            removedNullForgivingOperator = true;
            return UnwrapNullForgivingExpression(postfixUnary.Operand, out _);
        }

        if (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            ExpressionSyntax unwrappedExpression = UnwrapNullForgivingExpression(
                parenthesized.Expression,
                out bool removedInsideParentheses);
            if (removedInsideParentheses)
            {
                removedNullForgivingOperator = true;
                return unwrappedExpression;
            }
        }

        removedNullForgivingOperator = false;
        return expression;
    }

    private static bool NeedsParenthesesForCoalesce(ExpressionSyntax expression)
    {
        return expression is AssignmentExpressionSyntax or ConditionalExpressionSyntax ||
               expression is BinaryExpressionSyntax && !expression.IsKind(SyntaxKind.CoalesceExpression);
    }

    private static bool TryAppendMapFromExpression(
        InvocationExpressionSyntax existingConfiguration,
        string mapFromExpression,
        out InvocationExpressionSyntax? replacement)
    {
        replacement = null;
        if (existingConfiguration.ArgumentList.Arguments.Count <= 1 ||
            existingConfiguration.ArgumentList.Arguments[1].Expression is not LambdaExpressionSyntax optionsLambda)
        {
            return false;
        }

        string? optionParameterName = GetSingleParameterName(optionsLambda);
        if (string.IsNullOrEmpty(optionParameterName))
        {
            return false;
        }

        string sourceParameterName = string.Equals(optionParameterName, "src", StringComparison.Ordinal)
            ? "source"
            : "src";
        if (!string.Equals(sourceParameterName, "src", StringComparison.Ordinal) &&
            mapFromExpression.StartsWith("src.", StringComparison.Ordinal))
        {
            mapFromExpression = sourceParameterName + mapFromExpression.Substring("src".Length);
        }
        StatementSyntax mapFromStatement =
            SyntaxFactory.ParseStatement($"{optionParameterName}.MapFrom({sourceParameterName} => {mapFromExpression});");
        BlockSyntax? replacementBlock = optionsLambda.Body switch
        {
            BlockSyntax block => block.AddStatements(mapFromStatement),
            ExpressionSyntax expression => SyntaxFactory.Block(
                SyntaxFactory.ExpressionStatement(expression.WithoutTrivia()),
                mapFromStatement),
            _ => null
        };

        if (replacementBlock == null)
        {
            return false;
        }

        LambdaExpressionSyntax replacementLambda = optionsLambda switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda
                .WithExpressionBody(null)
                .WithBlock(replacementBlock),
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda => parenthesizedLambda
                .WithExpressionBody(null)
                .WithBlock(replacementBlock),
            _ => optionsLambda
        };

        replacement = existingConfiguration.ReplaceNode(
            optionsLambda,
            replacementLambda.WithTriviaFrom(optionsLambda));
        return true;
    }

    private static string? GetSingleParameterName(LambdaExpressionSyntax lambda)
    {
        return lambda switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesizedLambda =>
                parenthesizedLambda.ParameterList.Parameters[0].Identifier.ValueText,
            _ => null
        };
    }

    private static InvocationExpressionSyntax CreateReplacementForMemberWithIgnore(
        InvocationExpressionSyntax existingConfiguration,
        string propertyName)
    {
        if (existingConfiguration.Expression is MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax receiver })
        {
            return CodeFixSyntaxHelper.CreateForMemberWithIgnore(receiver, propertyName)
                .WithTriviaFrom(existingConfiguration);
        }

        return existingConfiguration;
    }

    private string? ExtractPropertyNameFromDiagnostic(Diagnostic diagnostic)
    {
        // Try to get property name from diagnostic properties
        if (diagnostic.Properties.TryGetValue(AM002_NullableCompatibilityAnalyzer.PropertyNamePropertyName,
                out string? propertyName))
        {
            return propertyName;
        }

        // Fallback: extract from diagnostic message
        string message = diagnostic.GetMessage();
        Match match = Regex.Match(message, @"Property '(\w+)'");
        return match.Success ? match.Groups[1].Value : null;
    }

    private string? ExtractDestinationTypeFromDiagnostic(Diagnostic diagnostic)
    {
        // Try to get from diagnostic properties
        if (diagnostic.Properties.TryGetValue(AM002_NullableCompatibilityAnalyzer.DestinationPropertyTypePropertyName,
                out string? destinationType))
        {
            return destinationType;
        }

        // Backward-compatible fallback for older diagnostics.
        if (diagnostic.Properties.TryGetValue("DestType", out string? destType))
        {
            return destType;
        }

        // Fallback: extract from diagnostic message (e.g., "int?")
        string message = diagnostic.GetMessage();
        // Match pattern like "(...) is nullable" to extract the type before
        Match match = Regex.Match(message, @"\(([^)]+)\)\s+is non-nullable");
        return match.Success ? match.Groups[1].Value : null;
    }

}

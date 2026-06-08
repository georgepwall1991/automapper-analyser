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

            string? destinationType = ExtractDestinationTypeFromDiagnostic(diagnostic);
            string defaultValue = GetDefaultValueForDestination(invocation, propertyName!, destinationType, operationContext.SemanticModel);

            // A "src.X ?? default" coalesce scaffold cannot fix element-level nullability (the elements of
            // List<string?> are still nullable), so the element-nullability diagnostic gets only the
            // manual-review ignore action below.
            bool isElementNullability =
                diagnostic.Properties.TryGetValue(
                    AM002_NullableCompatibilityAnalyzer.ElementNullabilityPropertyName, out string? elementFlag) &&
                string.Equals(elementFlag, "true", StringComparison.Ordinal);

            InvocationExpressionSyntax? existingConfiguration = FindDestinationConfiguration(invocation, propertyName!);
            if (!isElementNullability &&
                (existingConfiguration == null ||
                (!ConfigurationCanVetoAssignment(existingConfiguration) &&
                 ConfigurationCanAcceptDefaultValueFix(existingConfiguration, operationContext.SemanticModel))))
            {
                // Option 1: Map with default value
                context.RegisterCodeFix(
                    CodeAction.Create(
                        $"Scaffold default mapping for '{propertyName}' ({defaultValue})",
                        cancellationToken => AddMapFromAsync(context.Document, invocation, propertyName!,
                            $"src.{CodeFixSyntaxHelper.EscapeIdentifier(propertyName!)} ?? {defaultValue}", defaultValue, operationContext.SemanticModel, cancellationToken),
                        $"AM002_DefaultValue_{propertyName}"),
                    diagnostic);
            }

            // Option 2: Ignore property
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Ignore property '{propertyName}' (manual review)",
                    cancellationToken => AddIgnoreAsync(context.Document, invocation, propertyName!, cancellationToken),
                    $"AM002_Ignore_{propertyName}"),
                diagnostic);
        }
    }

    private static string GetDefaultValueForDestination(
        InvocationExpressionSyntax invocation,
        string propertyName,
        string? destinationType,
        SemanticModel semanticModel)
    {
        string defaultValue = TypeConversionHelper.GetDefaultValueForType(destinationType ?? string.Empty);
        if (defaultValue != "default")
        {
            return defaultValue;
        }

        (ITypeSymbol? _, ITypeSymbol? destinationMapType) =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
        if (destinationMapType == null)
        {
            return defaultValue;
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

        InvocationExpressionSyntax? existingConfiguration = FindDestinationConfiguration(invocation, propertyName);
        if (existingConfiguration != null)
        {
            if (ConfigurationCanVetoAssignment(existingConfiguration))
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

            if (TryAppendMapFromExpression(existingConfiguration, propertyName, defaultValue, out InvocationExpressionSyntax? appendedMapFrom) &&
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
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        InvocationExpressionSyntax? existingConfiguration = FindDestinationConfiguration(invocation, propertyName);
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
        string propertyName)
    {
        SyntaxNode? currentNode = createMapInvocation.Parent;
        InvocationExpressionSyntax? effectiveConfiguration = null;

        while (currentNode is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            string methodName = memberAccess.Name.Identifier.ValueText;
            if (methodName == "ReverseMap")
            {
                break;
            }

            if (chainedInvocation.ArgumentList.Arguments.Count > 0 &&
                DestinationConfigurationTargetsTopLevelMember(chainedInvocation, methodName, propertyName))
            {
                effectiveConfiguration = chainedInvocation;
            }

            currentNode = chainedInvocation.Parent;
        }

        return effectiveConfiguration;
    }

    private static bool ConfigurationCanVetoAssignment(InvocationExpressionSyntax existingConfiguration)
    {
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

        return optionsLambda
            .DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression is IdentifierNameSyntax receiver &&
                string.Equals(receiver.Identifier.ValueText, optionParameterName, StringComparison.Ordinal) &&
                memberAccess.Name.Identifier.ValueText is "Condition" or "PreCondition");
    }

    private static bool ConfigurationCanAcceptDefaultValueFix(
        InvocationExpressionSyntax existingConfiguration,
        SemanticModel semanticModel)
    {
        if (!TryGetMapFromArgument(existingConfiguration, out ExpressionSyntax? mapFromArgument) ||
            mapFromArgument == null)
        {
            return true;
        }

        return MapFromArgumentCanBeCoalesced(mapFromArgument, semanticModel);
    }

    private static bool DestinationConfigurationTargetsTopLevelMember(
        InvocationExpressionSyntax chainedInvocation,
        string methodName,
        string propertyName)
    {
        ExpressionSyntax destinationExpression = chainedInvocation.ArgumentList.Arguments[0].Expression;
        if (methodName == "ForMember")
        {
            string? selectedMember =
                AM020MappingConfigurationHelpers.GetSelectedTopLevelMemberName(destinationExpression);
            return string.Equals(selectedMember, propertyName, StringComparison.OrdinalIgnoreCase);
        }

        return methodName == "ForPath" &&
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
        if (!TryGetMapFromArgument(existingConfiguration, out ExpressionSyntax? mapFromArgument) ||
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
        out ExpressionSyntax? mapFromArgument)
    {
        mapFromArgument = null;
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

        InvocationExpressionSyntax? effectiveMapFromInvocation = null;
        foreach (InvocationExpressionSyntax invocation in optionsLambda
                     .DescendantNodesAndSelf()
                     .OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression is IdentifierNameSyntax receiver &&
                string.Equals(receiver.Identifier.ValueText, optionParameterName, StringComparison.Ordinal) &&
                memberAccess.Name.Identifier.ValueText == "MapFrom" &&
                invocation.ArgumentList.Arguments.Count > 0)
            {
                effectiveMapFromInvocation = invocation;
            }
        }

        mapFromArgument = effectiveMapFromInvocation?.ArgumentList.Arguments[0].Expression;
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

        ExpressionSyntax nullableCandidate = GetNullableCoalescingCandidate(body);
        bool expressionCanProduceNull =
            ExpressionCanProduceNull(nullableCandidate, semanticModel) ||
            ExpressionContainsNullForgivingResultRisk(body, semanticModel);
        return expressionCanProduceNull &&
               CoalescedExpressionCanAcceptFallback(body, semanticModel) &&
               !ExpressionDereferencesSuppressedNullableReceiver(body, semanticModel) &&
               !ExpressionDereferencesNullableReceiver(body, semanticModel);
    }

    private static bool ExpressionContainsNullForgivingResultRisk(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        return expression
            .DescendantNodesAndSelf()
            .OfType<PostfixUnaryExpressionSyntax>()
            .Any(suppression =>
                suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression) &&
                ExpressionCanProduceNull(UnwrapParentheses(suppression.Operand), semanticModel) &&
                SuppressedValueCanFlowToExpressionResult(suppression, expression, semanticModel));
    }

    private static bool SuppressedValueCanFlowToExpressionResult(
        PostfixUnaryExpressionSyntax suppression,
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
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
                    when castExpression.Expression == current &&
                         CoalescedExpressionCanAcceptFallback(castExpression, semanticModel):
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
                    if (!ExpressionCanProduceNull(coalesceExpression.Left, semanticModel) &&
                        !ExpressionContainsNullForgivingResultRisk(coalesceExpression.Left, semanticModel))
                    {
                        return false;
                    }

                    current = coalesceExpression;
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    private static bool CoalescedExpressionCanAcceptFallback(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        TypeInfo typeInfo = semanticModel.GetTypeInfo(expression);
        ITypeSymbol? expressionType = typeInfo.Type ?? typeInfo.ConvertedType;
        return expressionType == null || !IsNonNullableValueType(expressionType);
    }

    private static ExpressionSyntax GetNullableCoalescingCandidate(ExpressionSyntax expression)
    {
        expression = UnwrapParentheses(expression);

        return expression is PostfixUnaryExpressionSyntax suppression &&
               suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression)
            ? suppression.Operand
            : expression;
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesizedExpression)
        {
            expression = parenthesizedExpression.Expression;
        }

        return expression;
    }

    private static bool ExpressionCanProduceNull(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        if (expression is DefaultExpressionSyntax defaultExpression)
        {
            TypeInfo defaultTypeInfo = semanticModel.GetTypeInfo(defaultExpression.Type);
            ITypeSymbol? defaultType = defaultTypeInfo.Type ?? defaultTypeInfo.ConvertedType;
            return defaultType == null || !IsNonNullableValueType(defaultType);
        }

        if (expression.IsKind(SyntaxKind.DefaultLiteralExpression))
        {
            TypeInfo defaultTypeInfo = semanticModel.GetTypeInfo(expression);
            ITypeSymbol? defaultType = defaultTypeInfo.Type ?? defaultTypeInfo.ConvertedType;
            return defaultType == null || !IsNonNullableValueType(defaultType);
        }

        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(expression);
        ISymbol? symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
        ITypeSymbol? symbolType = symbol switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            ILocalSymbol local => local.Type,
            IParameterSymbol parameter => parameter.Type,
            _ => null
        };
        if (symbolType != null)
        {
            return IsNullableType(symbolType);
        }

        TypeInfo typeInfo = semanticModel.GetTypeInfo(expression);
        ITypeSymbol? expressionType = typeInfo.Type ?? typeInfo.ConvertedType;
        return expressionType != null && IsNullableType(expressionType);
    }

    private static bool IsNonNullableValueType(ITypeSymbol type)
    {
        return type.IsValueType && !IsNullableType(type);
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

    private static bool ExpressionDereferencesSuppressedNullableReceiver(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        return expression
            .DescendantNodesAndSelf()
            .OfType<PostfixUnaryExpressionSyntax>()
            .Any(suppression =>
                suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression) &&
                IsNullableExpression(suppression.Operand, semanticModel) &&
                IsSuppressedNullableReceiverDereferenced(suppression, semanticModel));
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
                if (!ExpressionCanProduceNull(coalesceExpression.Left, semanticModel) &&
                    !ExpressionContainsNullForgivingResultRisk(coalesceExpression.Left, semanticModel))
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

        return parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Expression == receiverExpression &&
               !IsSafeNullableValueMemberAccess(memberAccess) &&
               !IsExtensionMethodReceiverAccess(memberAccess, semanticModel);
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
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(expression);
        ISymbol? symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
        ITypeSymbol? symbolType = symbol switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            ILocalSymbol local => local.Type,
            IParameterSymbol parameter => parameter.Type,
            IMethodSymbol method => method.ReturnType,
            _ => null
        };
        if (symbolType != null)
        {
            return IsNullableType(symbolType);
        }

        TypeInfo typeInfo = semanticModel.GetTypeInfo(expression);
        ITypeSymbol? expressionType = typeInfo.Type ?? typeInfo.ConvertedType;
        return expressionType != null && IsNullableType(expressionType);
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
        string expressionText = NeedsParenthesesForCoalesce(body)
            ? $"({body}) ?? {defaultValue}"
            : $"{body} ?? {defaultValue}";
        return SyntaxFactory.ParseExpression(expressionText);
    }

    private static bool NeedsParenthesesForCoalesce(ExpressionSyntax expression)
    {
        return expression is AssignmentExpressionSyntax or ConditionalExpressionSyntax or SwitchExpressionSyntax ||
               expression is BinaryExpressionSyntax;
    }

    private static bool TryAppendMapFromExpression(
        InvocationExpressionSyntax existingConfiguration,
        string propertyName,
        string defaultValue,
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
        string mapFromExpression = $"{sourceParameterName}.{CodeFixSyntaxHelper.EscapeIdentifier(propertyName)} ?? {defaultValue}";
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

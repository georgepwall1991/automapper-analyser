using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Helpers;

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
                MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "ForPath"))
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
            MemberAccessExpressionSyntax memberAccess => GetTopLevelMemberName(memberAccess),
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

    private static bool ShouldStopAtReverseMapBoundary(
        InvocationExpressionSyntax mappingInvocation,
        SemanticModel semanticModel)
    {
        return !MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(mappingInvocation, semanticModel, "ReverseMap");
    }
}

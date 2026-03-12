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

            string? selectedMember =
                GetSelectedTopLevelMemberName(mappingConfigCall.ArgumentList.Arguments[0].Expression);
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
        return expression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => GetSelectedTopLevelMemberName(simpleLambda.Body),
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda =>
                GetSelectedTopLevelMemberName(parenthesizedLambda.Body),
            MemberAccessExpressionSyntax memberAccess => GetTopLevelMemberName(memberAccess),
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) =>
                GetTopLevelMemberName(literal.Token.ValueText),
            _ => null
        };
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

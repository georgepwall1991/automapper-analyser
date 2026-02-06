using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Helpers;

/// <summary>
///     Shared AM020 helpers for identifying whether nested destination members are explicitly configured
///     and whether mapping construction/conversion methods apply to the forward direction.
/// </summary>
internal static class AM020MappingConfigurationHelpers
{
    public static bool IsDestinationPropertyExplicitlyConfigured(
        InvocationExpressionSyntax createMapInvocation,
        string destinationPropertyName,
        InvocationExpressionSyntax? reverseMapInvocation)
    {
        foreach (InvocationExpressionSyntax mappingConfigCall in GetMappingConfigurationCalls(createMapInvocation))
        {
            if (!AppliesToForwardDirection(mappingConfigCall, reverseMapInvocation))
            {
                continue;
            }

            if (mappingConfigCall.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            string? selectedMember =
                GetSelectedTopLevelMemberName(mappingConfigCall.ArgumentList.Arguments[0].Expression);
            if (string.Equals(selectedMember, destinationPropertyName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasCustomConstructionOrConversion(
        InvocationExpressionSyntax createMapInvocation,
        InvocationExpressionSyntax? reverseMapInvocation)
    {
        SyntaxNode? parent = createMapInvocation.Parent;

        while (parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if ((memberAccess.Name.Identifier.ValueText is "ConstructUsing" or "ConvertUsing") &&
                AppliesToForwardDirection(chainedInvocation, reverseMapInvocation))
            {
                return true;
            }

            parent = chainedInvocation.Parent;
        }

        return false;
    }

    private static IEnumerable<InvocationExpressionSyntax> GetMappingConfigurationCalls(
        InvocationExpressionSyntax createMapInvocation)
    {
        var mappingCalls = new List<InvocationExpressionSyntax>();
        SyntaxNode? currentNode = createMapInvocation.Parent;

        while (currentNode is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax invocation)
        {
            if (memberAccess.Name.Identifier.Text is "ForMember" or "ForPath")
            {
                mappingCalls.Add(invocation);
            }

            currentNode = invocation.Parent;
        }

        return mappingCalls;
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
        InvocationExpressionSyntax? reverseMapInvocation)
    {
        if (reverseMapInvocation == null)
        {
            return true;
        }

        return !reverseMapInvocation.Ancestors().Contains(mappingMethod);
    }
}

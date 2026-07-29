using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Helpers;

/// <summary>
///     Answers whether a destination member is explicitly configured on a mapping chain.
///     <para>
///     An analyzer and its code fix must agree on this exactly. When they disagree, the fixer offers
///     actions for members the analyzer never flagged, or withholds them for members it did — and the
///     drift is silent, because each side's tests only exercise its own copy. AM011 carried two
///     independent implementations of these three checks that were identical apart from local variable
///     names, which is the shape that drift takes before anyone notices it.
///     </para>
///     <para>
///     Scoped to the forward direction: traversal stops at <c>ReverseMap()</c>, because configuration
///     after it belongs to the reverse mapping.
///     </para>
/// </summary>
internal static class DestinationMemberConfigurationHelpers
{
    /// <summary>
    ///     True when a <c>ForCtorParam</c> in the forward chain names this member. The parameter name is
    ///     a string constant, so it is compared case-insensitively, matching AutoMapper's own resolution.
    /// </summary>
    public static bool IsConfiguredByCtorParam(
        InvocationExpressionSyntax createMapInvocation,
        string memberName,
        SemanticModel semanticModel
    )
    {
        foreach (
            InvocationExpressionSyntax call in GetForwardChainCalls(
                createMapInvocation,
                "ForCtorParam"
            )
        )
        {
            if (
                !MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                    call,
                    semanticModel,
                    "ForCtorParam"
                )
                || call.ArgumentList.Arguments.Count == 0
            )
            {
                continue;
            }

            Optional<object?> constantValue = semanticModel.GetConstantValue(
                call.ArgumentList.Arguments[0].Expression
            );

            if (
                constantValue.HasValue
                && constantValue.Value is string configuredParameter
                && string.Equals(
                    configuredParameter,
                    memberName,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     True when a <c>ForMember</c> or <c>ForPath</c> in the forward chain selects this member.
    ///     Selectors resolve to their top-level member, so a nested path configures its root rather than
    ///     the member it terminates in.
    /// </summary>
    public static bool IsConfiguredByDestinationSelector(
        InvocationExpressionSyntax createMapInvocation,
        string memberName,
        SemanticModel semanticModel
    )
    {
        foreach (
            InvocationExpressionSyntax call in GetForwardChainCalls(
                createMapInvocation,
                "ForMember",
                "ForPath"
            )
        )
        {
            if (
                !MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                    call,
                    semanticModel,
                    "ForMember"
                )
                && !MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                    call,
                    semanticModel,
                    "ForPath"
                )
            )
            {
                continue;
            }

            if (call.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            string? selectedMember =
                MappingConfigurationHelpers.GetSelectedTopLevelMemberNameWithSemanticModel(
                    call.ArgumentList.Arguments[0].Expression,
                    semanticModel
                );

            if (
                selectedMember != null
                && string.Equals(selectedMember, memberName, StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Walks the fluent chain from a <c>CreateMap</c>, yielding calls whose syntactic name matches one
    ///     of <paramref name="methodNames" /> and stopping at <c>ReverseMap()</c>. The name check here is
    ///     syntactic only; callers confirm the symbol, so an unrelated method borrowing the name is
    ///     filtered by them rather than admitted by this walk.
    /// </summary>
    private static IEnumerable<InvocationExpressionSyntax> GetForwardChainCalls(
        InvocationExpressionSyntax createMapInvocation,
        params string[] methodNames
    )
    {
        SyntaxNode? currentNode = createMapInvocation.Parent;

        while (
            currentNode is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Parent is InvocationExpressionSyntax chainedInvocation
        )
        {
            string methodName = memberAccess.Name.Identifier.ValueText;
            if (methodName == "ReverseMap")
            {
                yield break;
            }

            if (Array.IndexOf(methodNames, methodName) >= 0)
            {
                yield return chainedInvocation;
            }

            currentNode = chainedInvocation.Parent;
        }
    }
}

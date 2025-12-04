using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Performance.Strategies;

/// <summary>
///     Detects complex LINQ operations in mapping expressions that may be expensive.
/// </summary>
public class ComplexLinqOperationDetector : IPerformanceIssueDetector
{
    /// <inheritdoc />
    public string DetectorName => "Complex LINQ Operation Detector";

    /// <inheritdoc />
    public PerformanceIssueResult? Detect(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel)
    {
        string methodName = methodSymbol.Name;

        // SelectMany with nested operations is expensive
        if (methodName == "SelectMany")
        {
            if (HasComplexLambdaArgument(invocation))
            {
                return new PerformanceIssueResult(
                    PerformanceIssueType.ComplexLinqOperation,
                    "SelectMany with complex nested operations",
                    "SelectMany with nested operations can cause performance issues",
                    AM031_PerformanceWarningAnalyzer.ComplexLinqOperationRule);
            }
        }

        // GroupBy followed by multiple operations
        if (methodName == "GroupBy")
        {
            var parent = invocation.Parent;
            if (parent is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Parent is InvocationExpressionSyntax parentInvocation)
            {
                // Check if followed by another expensive operation
                var nextMethodName = memberAccess.Name.Identifier.Text;
                if (nextMethodName == "SelectMany" || nextMethodName == "Select")
                {
                    return new PerformanceIssueResult(
                        PerformanceIssueType.ComplexLinqOperation,
                        "GroupBy with chained operations",
                        "GroupBy followed by projections can be expensive",
                        AM031_PerformanceWarningAnalyzer.ComplexLinqOperationRule);
                }
            }
        }

        return null;
    }

    private static bool HasComplexLambdaArgument(InvocationExpressionSyntax invocation)
    {
        var args = invocation.ArgumentList.Arguments;
        if (args.Count == 0)
        {
            return false;
        }

        if (args[0].Expression is LambdaExpressionSyntax lambda)
        {
            // Count nested invocations in the lambda
            int nestedInvocations = lambda.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Count();

            return nestedInvocations >= 1;
        }

        return false;
    }
}

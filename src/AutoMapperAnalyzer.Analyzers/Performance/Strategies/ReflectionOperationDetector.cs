using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Performance.Strategies;

/// <summary>
///     Detects reflection operations in mapping expressions that may be slow.
/// </summary>
public class ReflectionOperationDetector : IPerformanceIssueDetector
{
    /// <inheritdoc />
    public string DetectorName => "Reflection Operation Detector";

    /// <inheritdoc />
    public PerformanceIssueResult? Detect(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel)
    {
        string methodName = methodSymbol.Name;

        // Check for reflection method patterns
        foreach (var pattern in AutoMapperConstants.ReflectionMethodPatterns)
        {
            if (methodName == pattern)
            {
                return new PerformanceIssueResult(
                    PerformanceIssueType.ReflectionOperation,
                    "reflection operation",
                    $"Method '{methodName}' uses reflection which can be slow",
                    AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule);
            }
        }

        return null;
    }
}

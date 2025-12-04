using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Performance.Strategies;

/// <summary>
///     Detects HTTP/network operations in mapping expressions that should be performed before mapping.
/// </summary>
public class HttpOperationDetector : IPerformanceIssueDetector
{
    /// <inheritdoc />
    public string DetectorName => "HTTP Operation Detector";

    /// <inheritdoc />
    public PerformanceIssueResult? Detect(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel)
    {
        string containingType = methodSymbol.ContainingType?.ToDisplayString() ?? string.Empty;
        string methodName = methodSymbol.Name;

        // Check for HTTP type patterns
        foreach (var pattern in AutoMapperConstants.HttpTypePatterns)
        {
            if (StringUtilities.ContainsOrdinal(containingType, pattern))
            {
                return new PerformanceIssueResult(
                    PerformanceIssueType.HttpOperation,
                    "HTTP request",
                    $"Type '{containingType}' indicates network access",
                    AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule);
            }
        }

        // Check for HTTP method patterns
        foreach (var pattern in AutoMapperConstants.HttpMethodPatterns)
        {
            if (StringUtilities.ContainsOrdinal(methodName, pattern))
            {
                return new PerformanceIssueResult(
                    PerformanceIssueType.HttpOperation,
                    "HTTP request",
                    $"Method '{methodName}' indicates network access",
                    AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule);
            }
        }

        return null;
    }
}

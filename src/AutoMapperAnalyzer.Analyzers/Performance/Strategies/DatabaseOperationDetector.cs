using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Performance.Strategies;

/// <summary>
///     Detects database operations in mapping expressions that should be performed before mapping.
/// </summary>
public class DatabaseOperationDetector : IPerformanceIssueDetector
{
    /// <inheritdoc />
    public string DetectorName => "Database Operation Detector";

    /// <inheritdoc />
    public PerformanceIssueResult? Detect(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel)
    {
        string containingType = methodSymbol.ContainingType?.ToDisplayString() ?? string.Empty;
        string methodName = methodSymbol.Name;

        // Check for database type patterns
        foreach (var pattern in AutoMapperConstants.DatabaseTypePatterns)
        {
            if (StringUtilities.ContainsOrdinal(containingType, pattern))
            {
                return new PerformanceIssueResult(
                    PerformanceIssueType.DatabaseOperation,
                    "database query",
                    $"Type '{containingType}' indicates database access",
                    AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule);
            }
        }

        // Check for database method patterns
        foreach (var pattern in AutoMapperConstants.DatabaseMethodPatterns)
        {
            if (StringUtilities.ContainsOrdinal(methodName, pattern))
            {
                return new PerformanceIssueResult(
                    PerformanceIssueType.DatabaseOperation,
                    "database query",
                    $"Method '{methodName}' indicates database access",
                    AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule);
            }
        }

        return null;
    }
}

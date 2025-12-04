using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Performance.Strategies;

/// <summary>
///     Detects non-deterministic operations in mapping expressions that make testing difficult.
/// </summary>
public class NonDeterministicOperationDetector : IPerformanceIssueDetector
{
    /// <inheritdoc />
    public string DetectorName => "Non-Deterministic Operation Detector";

    /// <inheritdoc />
    public PerformanceIssueResult? Detect(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel)
    {
        string containingType = methodSymbol.ContainingType?.ToDisplayString() ?? string.Empty;
        string methodName = methodSymbol.Name;

        // Check for DateTime.Now/UtcNow (via property getter method)
        if (containingType == "System.DateTime")
        {
            if (methodName == "get_Now" || methodName == "get_UtcNow")
            {
                return new PerformanceIssueResult(
                    PerformanceIssueType.NonDeterministicOperation,
                    "DateTime.Now",
                    "DateTime.Now produces different values on each call, making tests non-deterministic",
                    AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule);
            }
        }

        // Check for Random operations
        if (containingType == "System.Random" || containingType.Contains("Random"))
        {
            return new PerformanceIssueResult(
                PerformanceIssueType.NonDeterministicOperation,
                "Random",
                "Random operations produce non-deterministic results",
                AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule);
        }

        // Check for Guid.NewGuid
        if (containingType == "System.Guid" && methodName == "NewGuid")
        {
            return new PerformanceIssueResult(
                PerformanceIssueType.NonDeterministicOperation,
                "Guid.NewGuid",
                "Guid.NewGuid produces different values on each call",
                AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule);
        }

        return null;
    }
}

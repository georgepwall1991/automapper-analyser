using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Performance.Strategies;

/// <summary>
///     Detects file I/O operations in mapping expressions that should be performed before mapping.
/// </summary>
public class FileIOOperationDetector : IPerformanceIssueDetector
{
    /// <inheritdoc />
    public string DetectorName => "File I/O Operation Detector";

    /// <inheritdoc />
    public PerformanceIssueResult? Detect(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel)
    {
        string containingType = methodSymbol.ContainingType?.ToDisplayString() ?? string.Empty;
        string methodName = methodSymbol.Name;

        // Check for file I/O type patterns
        foreach (var pattern in AutoMapperConstants.FileIOTypePatterns)
        {
            if (containingType == pattern || StringUtilities.ContainsOrdinal(containingType, pattern))
            {
                return new PerformanceIssueResult(
                    PerformanceIssueType.FileIOOperation,
                    "file I/O operation",
                    $"Type '{containingType}' indicates file system access",
                    AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule);
            }
        }

        // Check for read/write operations in System.IO namespace
        if (StringUtilities.ContainsOrdinal(containingType, "System.IO"))
        {
            if (StringUtilities.ContainsOrdinal(methodName, "Read") ||
                StringUtilities.ContainsOrdinal(methodName, "Write"))
            {
                return new PerformanceIssueResult(
                    PerformanceIssueType.FileIOOperation,
                    "file I/O operation",
                    $"Method '{methodName}' in System.IO indicates file access",
                    AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule);
            }
        }

        return null;
    }
}

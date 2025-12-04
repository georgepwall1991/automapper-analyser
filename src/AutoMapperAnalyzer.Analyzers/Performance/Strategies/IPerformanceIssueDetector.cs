using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Performance.Strategies;

/// <summary>
///     Defines a contract for detecting performance issues in AutoMapper mapping expressions.
/// </summary>
public interface IPerformanceIssueDetector
{
    /// <summary>
    ///     Gets the name of this detector for diagnostic purposes.
    /// </summary>
    string DetectorName { get; }

    /// <summary>
    ///     Detects performance issues in a method invocation.
    /// </summary>
    /// <param name="invocation">The method invocation to analyze.</param>
    /// <param name="methodSymbol">The method symbol for the invocation.</param>
    /// <param name="semanticModel">The semantic model for additional analysis.</param>
    /// <returns>A detection result, or null if no issue was detected.</returns>
    PerformanceIssueResult? Detect(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel);
}

/// <summary>
///     Represents the result of a performance issue detection.
/// </summary>
public class PerformanceIssueResult
{
    /// <summary>
    ///     Gets the type of performance issue detected.
    /// </summary>
    public PerformanceIssueType IssueType { get; }

    /// <summary>
    ///     Gets a description of the issue for diagnostic messages.
    /// </summary>
    public string Description { get; }

    /// <summary>
    ///     Gets optional additional information about the issue.
    /// </summary>
    public string? AdditionalInfo { get; }

    /// <summary>
    ///     Gets the diagnostic descriptor to use for this issue.
    /// </summary>
    public DiagnosticDescriptor? DiagnosticRule { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PerformanceIssueResult"/> class.
    /// </summary>
    /// <param name="issueType">The type of issue.</param>
    /// <param name="description">A description of the issue.</param>
    /// <param name="additionalInfo">Optional additional information.</param>
    /// <param name="diagnosticRule">The diagnostic rule to use.</param>
    public PerformanceIssueResult(
        PerformanceIssueType issueType,
        string description,
        string? additionalInfo = null,
        DiagnosticDescriptor? diagnosticRule = null)
    {
        IssueType = issueType;
        Description = description;
        AdditionalInfo = additionalInfo;
        DiagnosticRule = diagnosticRule;
    }
}

/// <summary>
///     Types of performance issues that can be detected.
/// </summary>
public enum PerformanceIssueType
{
    /// <summary>
    ///     A database operation that should be performed before mapping.
    /// </summary>
    DatabaseOperation,

    /// <summary>
    ///     A file I/O operation that should be performed before mapping.
    /// </summary>
    FileIOOperation,

    /// <summary>
    ///     An HTTP/network operation that should be performed before mapping.
    /// </summary>
    HttpOperation,

    /// <summary>
    ///     A reflection operation that may be slow.
    /// </summary>
    ReflectionOperation,

    /// <summary>
    ///     Synchronous access to Task.Result that can cause deadlocks.
    /// </summary>
    TaskResultAccess,

    /// <summary>
    ///     A non-deterministic operation that makes testing difficult.
    /// </summary>
    NonDeterministicOperation,

    /// <summary>
    ///     Multiple enumeration of the same collection.
    /// </summary>
    MultipleEnumeration,

    /// <summary>
    ///     A complex LINQ operation that may be expensive.
    /// </summary>
    ComplexLinqOperation,

    /// <summary>
    ///     An expensive computation in the mapping.
    /// </summary>
    ExpensiveComputation,

    /// <summary>
    ///     An external method call that may be slow.
    /// </summary>
    ExternalMethodCall
}

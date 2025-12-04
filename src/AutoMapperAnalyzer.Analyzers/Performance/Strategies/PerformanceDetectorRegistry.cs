using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Performance.Strategies;

/// <summary>
///     Registry that manages all performance issue detectors and provides
///     a unified interface for detecting performance issues.
/// </summary>
public class PerformanceDetectorRegistry
{
    private static readonly Lazy<PerformanceDetectorRegistry> LazyInstance =
        new(() => new PerformanceDetectorRegistry());

    private readonly ImmutableArray<IPerformanceIssueDetector> _detectors;

    /// <summary>
    ///     Gets the singleton instance of the registry.
    /// </summary>
    public static PerformanceDetectorRegistry Instance => LazyInstance.Value;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PerformanceDetectorRegistry"/> class
    ///     with all registered detectors.
    /// </summary>
    private PerformanceDetectorRegistry()
    {
        _detectors = ImmutableArray.Create<IPerformanceIssueDetector>(
            new DatabaseOperationDetector(),
            new FileIOOperationDetector(),
            new HttpOperationDetector(),
            new ReflectionOperationDetector(),
            new NonDeterministicOperationDetector(),
            new ComplexLinqOperationDetector()
        );
    }

    /// <summary>
    ///     Initializes a new instance with custom detectors (for testing).
    /// </summary>
    /// <param name="detectors">The detectors to use.</param>
    public PerformanceDetectorRegistry(IEnumerable<IPerformanceIssueDetector> detectors)
    {
        _detectors = detectors.ToImmutableArray();
    }

    /// <summary>
    ///     Gets all registered detectors.
    /// </summary>
    public ImmutableArray<IPerformanceIssueDetector> Detectors => _detectors;

    /// <summary>
    ///     Detects all performance issues in a method invocation.
    /// </summary>
    /// <param name="invocation">The method invocation to analyze.</param>
    /// <param name="methodSymbol">The method symbol for the invocation.</param>
    /// <param name="semanticModel">The semantic model for additional analysis.</param>
    /// <returns>All detected performance issues.</returns>
    public IEnumerable<PerformanceIssueResult> DetectAll(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel)
    {
        foreach (var detector in _detectors)
        {
            var result = detector.Detect(invocation, methodSymbol, semanticModel);
            if (result != null)
            {
                yield return result;
            }
        }
    }

    /// <summary>
    ///     Detects the first performance issue in a method invocation.
    /// </summary>
    /// <param name="invocation">The method invocation to analyze.</param>
    /// <param name="methodSymbol">The method symbol for the invocation.</param>
    /// <param name="semanticModel">The semantic model for additional analysis.</param>
    /// <returns>The first detected issue, or null if none found.</returns>
    public PerformanceIssueResult? DetectFirst(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel)
    {
        foreach (var detector in _detectors)
        {
            var result = detector.Detect(invocation, methodSymbol, semanticModel);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    ///     Detects performance issues using a specific detector type.
    /// </summary>
    /// <typeparam name="T">The type of detector to use.</typeparam>
    /// <param name="invocation">The method invocation to analyze.</param>
    /// <param name="methodSymbol">The method symbol for the invocation.</param>
    /// <param name="semanticModel">The semantic model for additional analysis.</param>
    /// <returns>The detection result, or null if no issue was found.</returns>
    public PerformanceIssueResult? DetectUsing<T>(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel) where T : IPerformanceIssueDetector
    {
        var detector = _detectors.OfType<T>().FirstOrDefault();
        return detector?.Detect(invocation, methodSymbol, semanticModel);
    }
}

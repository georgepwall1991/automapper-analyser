using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Performance;

/// <summary>
///     Result of an operation check indicating whether a performance issue was detected.
/// </summary>
internal readonly struct OperationCheckResult
{
    public bool IsMatch { get; }
    public DiagnosticDescriptor? Rule { get; }
    public string[] MessageArgs { get; }
    public bool StopProcessing { get; }

    private OperationCheckResult(bool isMatch, DiagnosticDescriptor? rule, string[] messageArgs, bool stopProcessing)
    {
        IsMatch = isMatch;
        Rule = rule;
        MessageArgs = messageArgs;
        StopProcessing = stopProcessing;
    }

    public static OperationCheckResult NoMatch => new(false, null, [], false);

    public static OperationCheckResult Match(DiagnosticDescriptor rule, params string[] messageArgs) =>
        new(true, rule, messageArgs, stopProcessing: true);

    public static OperationCheckResult MatchContinue(DiagnosticDescriptor rule, params string[] messageArgs) =>
        new(true, rule, messageArgs, stopProcessing: false);
}

/// <summary>
///     Base interface for operation checkers that detect performance issues in mapping expressions.
/// </summary>
internal interface IOperationChecker
{
    OperationCheckResult Check(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel,
        string propertyName);
}

/// <summary>
///     Checks for database operations (Entity Framework, NHibernate, Dapper, etc.).
/// </summary>
internal sealed class DatabaseOperationChecker : IOperationChecker
{
    private static readonly string[] DatabaseTypePatterns =
    [
        "DbSet", "DbContext", "IQueryable", "ISession", "SqlConnection"
    ];

    private static readonly string[] DatabaseMethodPatterns =
    [
        "Query", "Execute", "Load"
    ];

    public OperationCheckResult Check(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel,
        string propertyName)
    {
        string containingType = methodSymbol.ContainingType?.ToDisplayString() ?? "";
        string methodName = methodSymbol.Name;

        bool isDatabaseOp = DatabaseTypePatterns.Any(containingType.Contains) ||
                            DatabaseMethodPatterns.Any(methodName.Contains);

        return isDatabaseOp
            ? OperationCheckResult.Match(
                AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule,
                propertyName, "database query")
            : OperationCheckResult.NoMatch;
    }
}

/// <summary>
///     Checks for file I/O operations.
/// </summary>
internal sealed class FileIOOperationChecker : IOperationChecker
{
    private static readonly HashSet<string> FileIOTypes =
    [
        "System.IO.File", "System.IO.Directory", "System.IO.Path"
    ];

    public OperationCheckResult Check(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel,
        string propertyName)
    {
        string containingType = methodSymbol.ContainingType?.ToDisplayString() ?? "";
        string methodName = methodSymbol.Name;

        bool isFileIO = FileIOTypes.Contains(containingType) ||
                        (containingType.Contains("System.IO") &&
                         (methodName.Contains("Read") || methodName.Contains("Write")));

        return isFileIO
            ? OperationCheckResult.Match(
                AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule,
                propertyName, "file I/O operation")
            : OperationCheckResult.NoMatch;
    }
}

/// <summary>
///     Checks for HTTP operations.
/// </summary>
internal sealed class HttpOperationChecker : IOperationChecker
{
    private static readonly string[] HttpTypePatterns = ["HttpClient", "WebClient"];
    private static readonly string[] HttpMethodPatterns = ["GetAsync", "PostAsync", "GetString"];

    public OperationCheckResult Check(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel,
        string propertyName)
    {
        string containingType = methodSymbol.ContainingType?.ToDisplayString() ?? "";
        string methodName = methodSymbol.Name;

        bool isHttpOp = HttpTypePatterns.Any(containingType.Contains) ||
                        HttpMethodPatterns.Any(methodName.Contains);

        return isHttpOp
            ? OperationCheckResult.Match(
                AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule,
                propertyName, "HTTP request")
            : OperationCheckResult.NoMatch;
    }
}

/// <summary>
///     Checks for reflection operations.
/// </summary>
internal sealed class ReflectionOperationChecker : IOperationChecker
{
    private static readonly HashSet<string> ReflectionMethods =
    [
        "GetType", "GetMethod", "GetProperty", "GetField", "GetCustomAttributes", "Invoke"
    ];

    public OperationCheckResult Check(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel,
        string propertyName)
    {
        return ReflectionMethods.Contains(methodSymbol.Name)
            ? OperationCheckResult.Match(
                AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule,
                propertyName, "reflection operation")
            : OperationCheckResult.NoMatch;
    }
}

/// <summary>
///     Checks for synchronous access of Task.Result.
/// </summary>
internal sealed class TaskResultChecker : IOperationChecker
{
    public OperationCheckResult Check(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel,
        string propertyName)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            memberAccess.Name.Identifier.Text != "Result")
        {
            return OperationCheckResult.NoMatch;
        }

        TypeInfo typeInfo = semanticModel.GetTypeInfo(memberAccess.Expression);
        string typeName = typeInfo.Type?.ToDisplayString() ?? "";

        return typeName.Contains("System.Threading.Tasks.Task")
            ? OperationCheckResult.Match(
                AM031_PerformanceWarningAnalyzer.TaskResultSynchronousAccessRule,
                propertyName)
            : OperationCheckResult.NoMatch;
    }
}

/// <summary>
///     Checks for non-deterministic operations (DateTime.Now, Random, Guid.NewGuid).
/// </summary>
internal sealed class NonDeterministicOperationChecker : IOperationChecker
{
    public OperationCheckResult Check(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel,
        string propertyName)
    {
        string containingType = methodSymbol.ContainingType?.ToDisplayString() ?? "";
        string methodName = methodSymbol.Name;

        string? operationType = GetNonDeterministicOperationType(containingType, methodName);

        return operationType != null
            ? OperationCheckResult.Match(
                AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule,
                propertyName, operationType)
            : OperationCheckResult.NoMatch;
    }

    private static string? GetNonDeterministicOperationType(string containingType, string methodName)
    {
        if (containingType == "System.DateTime" && (methodName == "get_Now" || methodName == "get_UtcNow"))
        {
            return "DateTime.Now";
        }

        if (containingType == "System.Random" || methodName.Contains("Random"))
        {
            return "Random";
        }

        if (containingType == "System.Guid" && methodName == "NewGuid")
        {
            return "Guid.NewGuid";
        }

        return null;
    }
}

/// <summary>
///     Checks for complex LINQ operations.
/// </summary>
internal sealed class ComplexLinqOperationChecker : IOperationChecker
{
    public OperationCheckResult Check(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel,
        string propertyName)
    {
        if (methodSymbol.Name != "SelectMany")
        {
            return OperationCheckResult.NoMatch;
        }

        SeparatedSyntaxList<ArgumentSyntax> args = invocation.ArgumentList.Arguments;
        if (args.Count > 0 && args[0].Expression is LambdaExpressionSyntax lambda)
        {
            int nestedInvocations = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>().Count();
            if (nestedInvocations >= 1)
            {
                return OperationCheckResult.Match(
                    AM031_PerformanceWarningAnalyzer.ComplexLinqOperationRule,
                    propertyName);
            }
        }

        return OperationCheckResult.NoMatch;
    }
}

/// <summary>
///     Checks for external method calls on injected services.
/// </summary>
internal sealed class ExternalMethodCallChecker : IOperationChecker
{
    private static readonly HashSet<string> SimpleLinqMethods =
    [
        "Select", "Where", "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending"
    ];

    public OperationCheckResult Check(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel,
        string propertyName)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return OperationCheckResult.NoMatch;
        }

        // Exclude property getters
        if (methodSymbol.MethodKind == MethodKind.PropertyGet)
        {
            return OperationCheckResult.NoMatch;
        }

        // Exclude string methods (they're generally fast)
        string containingType = methodSymbol.ContainingType?.ToDisplayString() ?? "";
        if (containingType is "string" or "System.String")
        {
            return OperationCheckResult.NoMatch;
        }

        // Exclude simple LINQ extension methods on the source object
        if (SimpleLinqMethods.Contains(methodSymbol.Name) && IsCalledOnSourceProperty(memberAccess))
        {
            return OperationCheckResult.NoMatch;
        }

        // Check if it's called on a field or injected dependency
        ISymbol? expressionSymbol = semanticModel.GetSymbolInfo(memberAccess.Expression).Symbol;
        if (expressionSymbol is IFieldSymbol || expressionSymbol is IPropertySymbol { IsReadOnly: true })
        {
            return OperationCheckResult.Match(
                AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule,
                propertyName, "method call");
        }

        return OperationCheckResult.NoMatch;
    }

    private static bool IsCalledOnSourceProperty(MemberAccessExpressionSyntax memberAccess)
    {
        string expression = memberAccess.Expression.ToString();
        return expression.StartsWith("src.") || expression == "src";
    }
}

/// <summary>
///     Tracks LINQ enumeration methods for multiple enumeration detection.
/// </summary>
internal sealed class LinqEnumerationTracker
{
    private static readonly HashSet<string> EnumerationMethods =
    [
        "ToList", "ToArray", "Sum", "Average", "Count",
        "First", "FirstOrDefault", "Last", "LastOrDefault", "Any", "All"
    ];

    public bool IsEnumerationMethod(string methodName) => EnumerationMethods.Contains(methodName);

    public void TrackAccess(InvocationExpressionSyntax invocation, Dictionary<string, int> collectionAccesses)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        string collectionName = memberAccess.Expression.ToString();

        // Normalize collection name (remove src. prefix if present)
        if (collectionName.StartsWith("src."))
        {
            collectionName = collectionName.Substring(4);
        }

        collectionAccesses.TryGetValue(collectionName, out int count);
        collectionAccesses[collectionName] = count + 1;
    }
}

/// <summary>
///     Checks for expensive computations in lambda expressions.
/// </summary>
internal static class ExpensiveComputationChecker
{
    public static bool IsExpensiveComputation(LambdaExpressionSyntax lambda)
    {
        return lambda.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(IsEnumerableRangeCall);
    }

    private static bool IsEnumerableRangeCall(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Expression.ToString() == "Enumerable" &&
               memberAccess.Name.Identifier.Text == "Range";
    }
}

/// <summary>
///     Checks for non-deterministic property accesses (e.g., DateTime.Now).
/// </summary>
internal static class NonDeterministicPropertyChecker
{
    public static bool IsNonDeterministicProperty(IPropertySymbol propertySymbol)
    {
        string containingType = propertySymbol.ContainingType?.ToDisplayString() ?? "";
        string propertyName = propertySymbol.Name;

        return containingType == "System.DateTime" &&
               propertyName is "Now" or "UtcNow";
    }
}

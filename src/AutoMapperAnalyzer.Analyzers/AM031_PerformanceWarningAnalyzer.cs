using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
/// Analyzer for AM031: Performance warnings in AutoMapper configurations.
/// Detects expensive operations in mapping expressions that should be performed before mapping.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM031_PerformanceWarningAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Diagnostic rule for expensive operations in MapFrom expressions.
    /// </summary>
    public static readonly DiagnosticDescriptor ExpensiveOperationInMapFromRule = new(
        "AM031",
        "Expensive operation in mapping expression",
        "Property '{0}' mapping contains {1} that should be performed before mapping to avoid performance issues",
        "AutoMapper.Performance",
        DiagnosticSeverity.Warning,
        true,
        "Expensive operations (database queries, API calls, file I/O) should be performed before mapping, not during.");

    /// <summary>
    /// Diagnostic rule for multiple enumerations of the same collection.
    /// </summary>
    public static readonly DiagnosticDescriptor MultipleEnumerationRule = new(
        "AM031",
        "Multiple enumeration of collection in mapping",
        "Property '{0}' mapping enumerates collection '{1}' multiple times. Consider caching the result with ToList() or ToArray()",
        "AutoMapper.Performance",
        DiagnosticSeverity.Warning,
        true,
        "Multiple enumerations of IEnumerable can cause performance issues. Cache the result before multiple operations.");

    /// <summary>
    /// Diagnostic rule for expensive computations in mapping.
    /// </summary>
    public static readonly DiagnosticDescriptor ExpensiveComputationRule = new(
        "AM031",
        "Expensive computation in mapping expression",
        "Property '{0}' mapping contains expensive computation that may impact performance. Consider computing before mapping",
        "AutoMapper.Performance",
        DiagnosticSeverity.Warning,
        true,
        "Complex computations in mapping expressions can impact performance. Consider computing values before mapping.");

    /// <summary>
    /// Diagnostic rule for synchronous access of Task.Result.
    /// </summary>
    public static readonly DiagnosticDescriptor TaskResultSynchronousAccessRule = new(
        "AM031",
        "Synchronous access of async operation in mapping",
        "Property '{0}' mapping uses Task.Result which can cause deadlocks. Perform async operations before mapping",
        "AutoMapper.Performance",
        DiagnosticSeverity.Warning,
        true,
        "Using Task.Result or Task.Wait() in mapping can cause deadlocks and performance issues. Await async operations before mapping.");

    /// <summary>
    /// Diagnostic rule for complex LINQ operations.
    /// </summary>
    public static readonly DiagnosticDescriptor ComplexLinqOperationRule = new(
        "AM031",
        "Complex LINQ operation in mapping",
        "Property '{0}' mapping contains complex LINQ operation that may impact performance. Consider simplifying or computing before mapping",
        "AutoMapper.Performance",
        DiagnosticSeverity.Warning,
        true,
        "Complex LINQ operations with SelectMany, multiple Where clauses, or nested queries can impact performance.");

    /// <summary>
    /// Diagnostic rule for non-deterministic operations in mapping.
    /// </summary>
    public static readonly DiagnosticDescriptor NonDeterministicOperationRule = new(
        "AM031",
        "Non-deterministic operation in mapping",
        "Property '{0}' mapping uses {1} which produces non-deterministic results. Consider computing before mapping for testability",
        "AutoMapper.Performance",
        DiagnosticSeverity.Info,
        true,
        "Non-deterministic operations (DateTime.Now, Random, Guid.NewGuid) make mappings harder to test. Consider computing before mapping.");

    /// <summary>
    /// Gets the supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        ExpensiveOperationInMapFromRule,
        MultipleEnumerationRule,
        ExpensiveComputationRule,
        TaskResultSynchronousAccessRule,
        ComplexLinqOperationRule,
        NonDeterministicOperationRule
    ];

    /// <summary>
    /// Initializes the analyzer.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCreateMapInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeCreateMapInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;

        // Check if this is a CreateMap<TSource, TDestination>() call
        if (!AutoMapperAnalysisHelpers.IsCreateMapInvocation(invocationExpr, context.SemanticModel))
        {
            return;
        }

        // Find all ForMember calls in the method chain
        var forMemberCalls = GetForMemberInvocations(invocationExpr);

        foreach (var forMemberCall in forMemberCalls)
        {
            AnalyzeForMemberCall(context, forMemberCall);
        }
    }

    private static List<InvocationExpressionSyntax> GetForMemberInvocations(InvocationExpressionSyntax createMapInvocation)
    {
        var forMemberCalls = new List<InvocationExpressionSyntax>();

        // Find the parent expression statement or any ancestor that might contain the full chain
        var root = createMapInvocation.AncestorsAndSelf().OfType<ExpressionStatementSyntax>().FirstOrDefault()
                  ?? createMapInvocation.AncestorsAndSelf().OfType<VariableDeclaratorSyntax>().FirstOrDefault()?.Parent?.Parent
                  ?? (SyntaxNode)createMapInvocation;

        // Find all ForMember invocations in descendants
        var allInvocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in allInvocations)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.Text == "ForMember")
            {
                // Check if this ForMember is part of the CreateMap chain
                if (IsPartOfCreateMapChain(invocation, createMapInvocation))
                {
                    forMemberCalls.Add(invocation);
                }
            }
        }

        return forMemberCalls;
    }

    private static bool IsPartOfCreateMapChain(InvocationExpressionSyntax forMemberInvocation, InvocationExpressionSyntax createMapInvocation)
    {
        // Check if the ForMember's member access expression contains the CreateMap invocation
        var currentExpr = forMemberInvocation.Expression;
        while (currentExpr is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Expression is InvocationExpressionSyntax invocation)
            {
                if (invocation == createMapInvocation)
                {
                    return true;
                }
                currentExpr = invocation.Expression;
            }
            else
            {
                currentExpr = memberAccess.Expression;
            }
        }

        return false;
    }

    private static void AnalyzeForMemberCall(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax forMemberInvocation)
    {
        // Get the lambda expression from MapFrom
        var mapFromLambda = GetMapFromLambda(forMemberInvocation);
        if (mapFromLambda == null)
        {
            return;
        }

        // Get the destination property name
        var propertyName = GetDestinationPropertyName(forMemberInvocation);
        if (propertyName == null)
        {
            return;
        }

        // Analyze the lambda body for performance issues
        AnalyzeLambdaExpression(context, mapFromLambda, propertyName, forMemberInvocation);
    }

    private static LambdaExpressionSyntax? GetMapFromLambda(InvocationExpressionSyntax forMemberInvocation)
    {
        // Look for the second argument which should contain the MapFrom call
        if (forMemberInvocation.ArgumentList.Arguments.Count < 2)
        {
            return null;
        }

        var optionsArg = forMemberInvocation.ArgumentList.Arguments[1];

        // Navigate through the lambda to find MapFrom
        if (optionsArg.Expression is not SimpleLambdaExpressionSyntax optionsLambda)
        {
            return null;
        }

        // Look for MapFrom invocation in the lambda body
        var mapFromInvocation = optionsLambda.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv => inv.Expression is MemberAccessExpressionSyntax mae &&
                                  mae.Name.Identifier.Text == "MapFrom");

        if (mapFromInvocation == null)
        {
            return null;
        }

        // Get the lambda expression passed to MapFrom
        var mapFromArg = mapFromInvocation.ArgumentList.Arguments.FirstOrDefault();
        return mapFromArg?.Expression as LambdaExpressionSyntax;
    }

    private static string? GetDestinationPropertyName(InvocationExpressionSyntax forMemberInvocation)
    {
        if (forMemberInvocation.ArgumentList.Arguments.Count < 1)
        {
            return null;
        }

        var firstArg = forMemberInvocation.ArgumentList.Arguments[0];
        if (firstArg.Expression is not SimpleLambdaExpressionSyntax destLambda)
        {
            return null;
        }

        if (destLambda.Body is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.Text;
        }

        return null;
    }

    private static void AnalyzeLambdaExpression(
        SyntaxNodeAnalysisContext context,
        LambdaExpressionSyntax lambda,
        string propertyName,
        InvocationExpressionSyntax forMemberInvocation)
    {
        // Check for method invocations
        var invocations = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>().ToList();

        // Check for member access expressions (for properties like DateTime.Now)
        var memberAccesses = lambda.DescendantNodes().OfType<MemberAccessExpressionSyntax>().ToList();

        // Track collection accesses for multiple enumeration detection
        var collectionAccesses = new Dictionary<string, int>();

        foreach (var invocation in invocations)
        {
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            {
                continue;
            }

            var containingType = methodSymbol.ContainingType?.ToDisplayString() ?? "";
            var methodName = methodSymbol.Name;

            // Check for database operations
            if (IsDatabaseOperation(containingType, methodName))
            {
                ReportDiagnostic(context, lambda, ExpensiveOperationInMapFromRule, propertyName, "database query");
                continue;
            }

            // Check for file I/O
            if (IsFileIOOperation(containingType, methodName))
            {
                ReportDiagnostic(context, lambda, ExpensiveOperationInMapFromRule, propertyName, "file I/O operation");
                continue;
            }

            // Check for HTTP requests
            if (IsHttpOperation(containingType, methodName))
            {
                ReportDiagnostic(context, lambda, ExpensiveOperationInMapFromRule, propertyName, "HTTP request");
                continue;
            }

            // Check for reflection
            if (IsReflectionOperation(methodName))
            {
                ReportDiagnostic(context, lambda, ExpensiveOperationInMapFromRule, propertyName, "reflection operation");
                continue;
            }

            // Check for Task.Result
            if (IsTaskResultAccess(invocation, context.SemanticModel))
            {
                ReportDiagnostic(context, lambda, TaskResultSynchronousAccessRule, propertyName);
                continue;
            }

            // Check for non-deterministic operations
            if (IsNonDeterministicOperation(containingType, methodName, out var operationType))
            {
                var diagnostic = Diagnostic.Create(
                    NonDeterministicOperationRule,
                    lambda.GetLocation(),
                    propertyName,
                    operationType);
                context.ReportDiagnostic(diagnostic);
                continue;
            }

            // Track LINQ operations for multiple enumeration detection
            if (IsLinqEnumerationMethod(methodName))
            {
                TrackCollectionAccess(invocation, collectionAccesses);
            }

            // Check for complex LINQ operations
            if (IsComplexLinqOperation(methodName, invocation))
            {
                ReportDiagnostic(context, lambda, ComplexLinqOperationRule, propertyName);
                continue;
            }

            // Check for external method calls (not on source properties)
            if (IsExternalMethodCall(invocation, context.SemanticModel))
            {
                ReportDiagnostic(context, lambda, ExpensiveOperationInMapFromRule, propertyName, "method call");
                continue;
            }
        }

        // Check member access expressions for non-deterministic properties
        foreach (var memberAccess in memberAccesses)
        {
            var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);
            if (symbolInfo.Symbol is IPropertySymbol propertySymbol)
            {
                var containingType = propertySymbol.ContainingType?.ToDisplayString() ?? "";
                var propertyName_member = propertySymbol.Name;

                // Check for DateTime.Now, DateTime.UtcNow
                if (containingType == "System.DateTime" && (propertyName_member == "Now" || propertyName_member == "UtcNow"))
                {
                    var diagnostic = Diagnostic.Create(
                        NonDeterministicOperationRule,
                        lambda.GetLocation(),
                        propertyName,
                        "DateTime.Now");
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        // Check for multiple enumerations
        foreach (var kvp in collectionAccesses)
        {
            if (kvp.Value > 1)
            {
                var diagnostic = Diagnostic.Create(
                    MultipleEnumerationRule,
                    lambda.GetLocation(),
                    propertyName,
                    kvp.Key);
                context.ReportDiagnostic(diagnostic);
            }
        }

        // Check for expensive computations (complex expressions with multiple operations)
        if (IsExpensiveComputation(lambda))
        {
            ReportDiagnostic(context, lambda, ExpensiveComputationRule, propertyName);
        }
    }

    private static void ReportDiagnostic(
        SyntaxNodeAnalysisContext context,
        LambdaExpressionSyntax lambda,
        DiagnosticDescriptor rule,
        params string[] messageArgs)
    {
        var diagnostic = Diagnostic.Create(rule, lambda.GetLocation(), messageArgs);
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsDatabaseOperation(string containingType, string methodName)
    {
        // Check for Entity Framework, NHibernate, Dapper, etc.
        return containingType.Contains("DbSet") ||
               containingType.Contains("DbContext") ||
               containingType.Contains("IQueryable") ||
               containingType.Contains("ISession") ||
               containingType.Contains("SqlConnection") ||
               methodName.Contains("Query") ||
               methodName.Contains("Execute") ||
               methodName.Contains("Load");
    }

    private static bool IsFileIOOperation(string containingType, string methodName)
    {
        return containingType == "System.IO.File" ||
               containingType == "System.IO.Directory" ||
               containingType == "System.IO.Path" ||
               methodName.Contains("Read") && containingType.Contains("System.IO") ||
               methodName.Contains("Write") && containingType.Contains("System.IO");
    }

    private static bool IsHttpOperation(string containingType, string methodName)
    {
        return containingType.Contains("HttpClient") ||
               containingType.Contains("WebClient") ||
               methodName.Contains("GetAsync") ||
               methodName.Contains("PostAsync") ||
               methodName.Contains("GetString");
    }

    private static bool IsReflectionOperation(string methodName)
    {
        return methodName == "GetType" ||
               methodName == "GetMethod" ||
               methodName == "GetProperty" ||
               methodName == "GetField" ||
               methodName == "GetCustomAttributes" ||
               methodName == "Invoke";
    }

    private static bool IsTaskResultAccess(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        // Check if accessing .Result property on a Task
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Name.Identifier.Text == "Result")
            {
                var typeInfo = semanticModel.GetTypeInfo(memberAccess.Expression);
                var typeName = typeInfo.Type?.ToDisplayString() ?? "";
                return typeName.Contains("System.Threading.Tasks.Task");
            }
        }

        return false;
    }

    private static bool IsNonDeterministicOperation(string containingType, string methodName, out string operationType)
    {
        operationType = "";

        if (containingType == "System.DateTime" && (methodName == "get_Now" || methodName == "get_UtcNow"))
        {
            operationType = "DateTime.Now";
            return true;
        }

        if (containingType == "System.Random" || methodName.Contains("Random"))
        {
            operationType = "Random";
            return true;
        }

        if (containingType == "System.Guid" && methodName == "NewGuid")
        {
            operationType = "Guid.NewGuid";
            return true;
        }

        return false;
    }

    private static bool IsLinqEnumerationMethod(string methodName)
    {
        return methodName is "ToList" or "ToArray" or "Sum" or "Average" or "Count" or
               "First" or "FirstOrDefault" or "Last" or "LastOrDefault" or "Any" or "All";
    }

    private static void TrackCollectionAccess(InvocationExpressionSyntax invocation, Dictionary<string, int> collectionAccesses)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var collectionName = memberAccess.Expression.ToString();

            // Normalize collection name (remove src. prefix if present)
            if (collectionName.StartsWith("src."))
            {
                collectionName = collectionName.Substring(4);
            }

            if (!collectionAccesses.ContainsKey(collectionName))
            {
                collectionAccesses[collectionName] = 0;
            }
            collectionAccesses[collectionName]++;
        }
    }

    private static bool IsComplexLinqOperation(string methodName, InvocationExpressionSyntax invocation)
    {
        // SelectMany with nested operations
        if (methodName == "SelectMany")
        {
            // Check if the argument contains complex lambda
            var args = invocation.ArgumentList.Arguments;
            if (args.Count > 0 && args[0].Expression is LambdaExpressionSyntax lambda)
            {
                // Check for nested Where, Select, or other complex operations
                var nestedInvocations = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>().Count();
                return nestedInvocations >= 1;
            }
        }

        return false;
    }

    private static bool IsExternalMethodCall(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
        {
            return false;
        }

        // Exclude simple property access and built-in operators
        if (methodSymbol.MethodKind == MethodKind.PropertyGet)
        {
            return false;
        }

        // Exclude string methods (they're generally fast)
        var containingType = methodSymbol.ContainingType?.ToDisplayString() ?? "";
        if (containingType == "string" || containingType == "System.String")
        {
            return false;
        }

        // Exclude simple LINQ extension methods on the source object
        var simpleLinqMethods = new[] { "Select", "Where", "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending" };
        if (simpleLinqMethods.Contains(methodSymbol.Name) && IsCalledOnSourceProperty(memberAccess))
        {
            return false;
        }

        // Check if it's called on a field or injected dependency
        var expressionSymbol = semanticModel.GetSymbolInfo(memberAccess.Expression).Symbol;
        if (expressionSymbol is IFieldSymbol || expressionSymbol is IPropertySymbol { IsReadOnly: true })
        {
            // This is likely a method call on an injected service
            return true;
        }

        return false;
    }

    private static bool IsCalledOnSourceProperty(MemberAccessExpressionSyntax memberAccess)
    {
        var expression = memberAccess.Expression.ToString();
        return expression.StartsWith("src.") || expression == "src";
    }

    private static bool IsExpensiveComputation(LambdaExpressionSyntax lambda)
    {
        // Look for Enumerable.Range with complex predicates (like prime checking)
        var invocations = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>().ToList();

        foreach (var invocation in invocations)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Expression.ToString() == "Enumerable" && memberAccess.Name.Identifier.Text == "Range")
                {
                    // Found Enumerable.Range - this might be expensive
                    return true;
                }
            }
        }

        return false;
    }
}

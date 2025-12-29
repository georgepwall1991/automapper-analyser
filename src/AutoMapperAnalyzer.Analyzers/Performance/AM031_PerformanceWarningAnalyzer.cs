using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.Performance;

/// <summary>
///     Analyzer for AM031: Performance warnings in AutoMapper configurations.
///     Detects expensive operations in mapping expressions that should be performed before mapping.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM031_PerformanceWarningAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     Diagnostic rule for expensive operations in MapFrom expressions.
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
    ///     Diagnostic rule for multiple enumerations of the same collection.
    /// </summary>
    public static readonly DiagnosticDescriptor MultipleEnumerationRule = new(
        "AM031",
        "Multiple enumeration of collection in mapping",
        "Property '{0}' mapping enumerates collection '{1}' multiple times. Consider caching the result with ToList() or ToArray().",
        "AutoMapper.Performance",
        DiagnosticSeverity.Warning,
        true,
        "Multiple enumerations of IEnumerable can cause performance issues. Cache the result before multiple operations.");

    /// <summary>
    ///     Diagnostic rule for expensive computations in mapping.
    /// </summary>
    public static readonly DiagnosticDescriptor ExpensiveComputationRule = new(
        "AM031",
        "Expensive computation in mapping expression",
        "Property '{0}' mapping contains expensive computation that may impact performance. Consider computing before mapping.",
        "AutoMapper.Performance",
        DiagnosticSeverity.Warning,
        true,
        "Complex computations in mapping expressions can impact performance. Consider computing values before mapping.");

    /// <summary>
    ///     Diagnostic rule for synchronous access of Task.Result.
    /// </summary>
    public static readonly DiagnosticDescriptor TaskResultSynchronousAccessRule = new(
        "AM031",
        "Synchronous access of async operation in mapping",
        "Property '{0}' mapping uses Task.Result which can cause deadlocks. Perform async operations before mapping.",
        "AutoMapper.Performance",
        DiagnosticSeverity.Warning,
        true,
        "Using Task.Result or Task.Wait() in mapping can cause deadlocks and performance issues. Await async operations before mapping.");

    /// <summary>
    ///     Diagnostic rule for complex LINQ operations.
    /// </summary>
    public static readonly DiagnosticDescriptor ComplexLinqOperationRule = new(
        "AM031",
        "Complex LINQ operation in mapping",
        "Property '{0}' mapping contains complex LINQ operation that may impact performance. Consider simplifying or computing before mapping.",
        "AutoMapper.Performance",
        DiagnosticSeverity.Warning,
        true,
        "Complex LINQ operations with SelectMany, multiple Where clauses, or nested queries can impact performance.");

    /// <summary>
    ///     Diagnostic rule for non-deterministic operations in mapping.
    /// </summary>
    public static readonly DiagnosticDescriptor NonDeterministicOperationRule = new(
        "AM031",
        "Non-deterministic operation in mapping",
        "Property '{0}' mapping uses {1} which produces non-deterministic results. Consider computing before mapping for testability.",
        "AutoMapper.Performance",
        DiagnosticSeverity.Info,
        true,
        "Non-deterministic operations (DateTime.Now, Random, Guid.NewGuid) make mappings harder to test. Consider computing before mapping.");

    /// <summary>
    ///     Gets the supported diagnostics for this analyzer.
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
    ///     Initializes the analyzer.
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
        List<InvocationExpressionSyntax> forMemberCalls = GetForMemberInvocations(invocationExpr);

        foreach (InvocationExpressionSyntax? forMemberCall in forMemberCalls)
        {
            AnalyzeForMemberCall(context, forMemberCall);
        }
    }

    private static List<InvocationExpressionSyntax> GetForMemberInvocations(
        InvocationExpressionSyntax createMapInvocation)
    {
        var forMemberCalls = new List<InvocationExpressionSyntax>();

        // Find the parent expression statement or any ancestor that might contain the full chain
        SyntaxNode root = createMapInvocation.AncestorsAndSelf().OfType<ExpressionStatementSyntax>().FirstOrDefault()
                          ?? createMapInvocation.AncestorsAndSelf().OfType<VariableDeclaratorSyntax>().FirstOrDefault()
                              ?.Parent?.Parent
                          ?? createMapInvocation;

        // Find all ForMember invocations in descendants
        IEnumerable<InvocationExpressionSyntax> allInvocations =
            root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (InvocationExpressionSyntax? invocation in allInvocations)
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

    private static bool IsPartOfCreateMapChain(InvocationExpressionSyntax forMemberInvocation,
        InvocationExpressionSyntax createMapInvocation)
    {
        // Check if the ForMember's member access expression contains the CreateMap invocation
        ExpressionSyntax currentExpr = forMemberInvocation.Expression;
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

    private static void AnalyzeForMemberCall(SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax forMemberInvocation)
    {
        // Get the lambda expression from MapFrom
        LambdaExpressionSyntax? mapFromLambda = GetMapFromLambda(forMemberInvocation);
        if (mapFromLambda == null)
        {
            return;
        }

        // Get the destination property name
        string? propertyName = GetDestinationPropertyName(forMemberInvocation);
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

        ArgumentSyntax optionsArg = forMemberInvocation.ArgumentList.Arguments[1];

        // Navigate through the lambda to find MapFrom
        if (optionsArg.Expression is not SimpleLambdaExpressionSyntax optionsLambda)
        {
            return null;
        }

        // Look for MapFrom invocation in the lambda body
        InvocationExpressionSyntax? mapFromInvocation = optionsLambda.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv => inv.Expression is MemberAccessExpressionSyntax mae &&
                                   mae.Name.Identifier.Text == "MapFrom");

        if (mapFromInvocation == null)
        {
            return null;
        }

        // Get the lambda expression passed to MapFrom
        ArgumentSyntax? mapFromArg = mapFromInvocation.ArgumentList.Arguments.FirstOrDefault();
        return mapFromArg?.Expression as LambdaExpressionSyntax;
    }

    private static string? GetDestinationPropertyName(InvocationExpressionSyntax forMemberInvocation)
    {
        if (forMemberInvocation.ArgumentList.Arguments.Count < 1)
        {
            return null;
        }

        ArgumentSyntax firstArg = forMemberInvocation.ArgumentList.Arguments[0];
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

    /// <summary>
    ///     Collection of operation checkers for detecting performance issues.
    ///     Checkers are evaluated in order; processing stops after a match if StopProcessing is true.
    /// </summary>
    private static readonly IOperationChecker[] OperationCheckers =
    [
        new DatabaseOperationChecker(),
        new FileIOOperationChecker(),
        new HttpOperationChecker(),
        new ReflectionOperationChecker(),
        new TaskResultChecker(),
        new NonDeterministicOperationChecker(),
        new ComplexLinqOperationChecker(),
        new ExternalMethodCallChecker()
    ];

    private static readonly LinqEnumerationTracker EnumerationTracker = new();

    private static void AnalyzeLambdaExpression(
        SyntaxNodeAnalysisContext context,
        LambdaExpressionSyntax lambda,
        string propertyName,
        InvocationExpressionSyntax forMemberInvocation)
    {
        var collectionAccesses = new Dictionary<string, int>();

        AnalyzeMethodInvocations(context, lambda, propertyName, collectionAccesses);
        AnalyzeNonDeterministicPropertyAccesses(context, lambda, propertyName);
        ReportMultipleEnumerations(context, lambda, propertyName, collectionAccesses);
        CheckExpensiveComputation(context, lambda, propertyName);
    }

    private static void AnalyzeMethodInvocations(
        SyntaxNodeAnalysisContext context,
        LambdaExpressionSyntax lambda,
        string propertyName,
        Dictionary<string, int> collectionAccesses)
    {
        var invocations = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>().ToList();

        foreach (InvocationExpressionSyntax invocation in invocations)
        {
            SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            {
                continue;
            }

            // Track LINQ operations for multiple enumeration detection
            if (EnumerationTracker.IsEnumerationMethod(methodSymbol.Name))
            {
                EnumerationTracker.TrackAccess(invocation, collectionAccesses);
            }

            // Run through operation checkers
            foreach (IOperationChecker checker in OperationCheckers)
            {
                OperationCheckResult result = checker.Check(invocation, methodSymbol, context.SemanticModel, propertyName);
                if (result.IsMatch)
                {
                    ReportDiagnostic(context, lambda, result.Rule!, result.MessageArgs);
                    if (result.StopProcessing)
                    {
                        break;
                    }
                }
            }
        }
    }

    private static void AnalyzeNonDeterministicPropertyAccesses(
        SyntaxNodeAnalysisContext context,
        LambdaExpressionSyntax lambda,
        string propertyName)
    {
        var memberAccesses = lambda.DescendantNodes().OfType<MemberAccessExpressionSyntax>().ToList();

        foreach (MemberAccessExpressionSyntax memberAccess in memberAccesses)
        {
            SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);
            if (symbolInfo.Symbol is IPropertySymbol propertySymbol &&
                NonDeterministicPropertyChecker.IsNonDeterministicProperty(propertySymbol))
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

    private static void ReportMultipleEnumerations(
        SyntaxNodeAnalysisContext context,
        LambdaExpressionSyntax lambda,
        string propertyName,
        Dictionary<string, int> collectionAccesses)
    {
        foreach (KeyValuePair<string, int> kvp in collectionAccesses.Where(kvp => kvp.Value > 1))
        {
            var diagnostic = Diagnostic.Create(
                MultipleEnumerationRule,
                lambda.GetLocation(),
                propertyName,
                kvp.Key);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void CheckExpensiveComputation(
        SyntaxNodeAnalysisContext context,
        LambdaExpressionSyntax lambda,
        string propertyName)
    {
        if (ExpensiveComputationChecker.IsExpensiveComputation(lambda))
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
}

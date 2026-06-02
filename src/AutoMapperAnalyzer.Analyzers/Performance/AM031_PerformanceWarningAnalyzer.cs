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
    private const string IssueTypePropertyName = "IssueType";
    private const string PropertyNamePropertyName = "PropertyName";
    private const string OperationTypePropertyName = "OperationType";
    private const string CollectionNamePropertyName = "CollectionName";

    private const string ExpensiveOperationIssueType = "ExpensiveOperation";
    private const string MultipleEnumerationIssueType = "MultipleEnumeration";
    private const string ExpensiveComputationIssueType = "ExpensiveComputation";
    private const string TaskResultIssueType = "TaskResult";
    private const string ComplexLinqIssueType = "ComplexLinq";
    private const string NonDeterministicIssueType = "NonDeterministic";

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

        // Ensure strict AutoMapper semantic matching to avoid lookalike false positives.
        if (!MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocationExpr, context.SemanticModel, "CreateMap"))
        {
            return;
        }

        // Find all destination configuration calls in the method chain
        List<InvocationExpressionSyntax> destinationConfigurationCalls =
            GetDestinationConfigurationInvocations(invocationExpr, context.SemanticModel);

        foreach (InvocationExpressionSyntax? destinationConfigurationCall in destinationConfigurationCalls)
        {
            AnalyzeDestinationConfigurationCall(context, destinationConfigurationCall);
        }
    }

    private static List<InvocationExpressionSyntax> GetDestinationConfigurationInvocations(
        InvocationExpressionSyntax createMapInvocation,
        SemanticModel semanticModel)
    {
        var destinationConfigurationCalls = new List<InvocationExpressionSyntax>();

        // Find the parent expression statement or any ancestor that might contain the full chain
        SyntaxNode root = createMapInvocation.AncestorsAndSelf().OfType<ExpressionStatementSyntax>().FirstOrDefault()
                          ?? createMapInvocation.AncestorsAndSelf().OfType<VariableDeclaratorSyntax>().FirstOrDefault()
                              ?.Parent?.Parent
                          ?? createMapInvocation;

        // Find all ForMember/ForPath invocations in descendants
        IEnumerable<InvocationExpressionSyntax> allInvocations =
            root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (InvocationExpressionSyntax? invocation in allInvocations)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.Text is "ForMember" or "ForPath" &&
                MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                    invocation,
                    semanticModel,
                    memberAccess.Name.Identifier.Text))
            {
                // Check if this destination configuration call is part of the CreateMap chain
                if (IsPartOfCreateMapChain(invocation, createMapInvocation))
                {
                    destinationConfigurationCalls.Add(invocation);
                }
            }
        }

        return destinationConfigurationCalls;
    }

    private static bool IsPartOfCreateMapChain(InvocationExpressionSyntax destinationConfigurationInvocation,
        InvocationExpressionSyntax createMapInvocation)
    {
        // Check if the destination configuration's member access expression contains the CreateMap invocation
        ExpressionSyntax currentExpr = destinationConfigurationInvocation.Expression;
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

    private static void AnalyzeDestinationConfigurationCall(SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax destinationConfigurationInvocation)
    {
        // Get the lambda expression from MapFrom
        LambdaExpressionSyntax? mapFromLambda = GetMapFromLambda(destinationConfigurationInvocation, context.SemanticModel);
        if (mapFromLambda == null)
        {
            return;
        }

        // Get the destination member path
        string? propertyName = GetDestinationMemberPath(destinationConfigurationInvocation);
        if (propertyName == null)
        {
            return;
        }

        // Analyze the lambda body for performance issues
        AnalyzeLambdaExpression(context, mapFromLambda, propertyName);
    }

    private static LambdaExpressionSyntax? GetMapFromLambda(
        InvocationExpressionSyntax destinationConfigurationInvocation,
        SemanticModel semanticModel)
    {
        // Look for the second argument which should contain the MapFrom call
        if (destinationConfigurationInvocation.ArgumentList.Arguments.Count < 2)
        {
            return null;
        }

        ArgumentSyntax optionsArg = destinationConfigurationInvocation.ArgumentList.Arguments[1];

        CSharpSyntaxNode? optionsBody = AutoMapperAnalysisHelpers.GetLambdaBody(optionsArg.Expression);
        if (optionsBody == null)
        {
            return null;
        }

        // Look for MapFrom invocation in the options lambda body
        InvocationExpressionSyntax? mapFromInvocation = optionsBody.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv => inv.Expression is MemberAccessExpressionSyntax mae &&
                                   mae.Name.Identifier.Text == "MapFrom" &&
                                   MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(inv, semanticModel, "MapFrom"));

        if (mapFromInvocation == null)
        {
            return null;
        }

        // Get the lambda expression passed to MapFrom
        ArgumentSyntax? mapFromArg = mapFromInvocation.ArgumentList.Arguments.FirstOrDefault();
        return mapFromArg?.Expression as LambdaExpressionSyntax;
    }

    private static string? GetDestinationMemberPath(InvocationExpressionSyntax destinationConfigurationInvocation)
    {
        if (destinationConfigurationInvocation.ArgumentList.Arguments.Count < 1)
        {
            return null;
        }

        ArgumentSyntax firstArg = destinationConfigurationInvocation.ArgumentList.Arguments[0];
        CSharpSyntaxNode? destLambdaBody = AutoMapperAnalysisHelpers.GetLambdaBody(firstArg.Expression);
        if (destLambdaBody == null)
        {
            return null;
        }

        return TryGetMemberPath(destLambdaBody, out string memberPath) ? memberPath : null;
    }

    private static bool TryGetMemberPath(SyntaxNode node, out string memberPath)
    {
        memberPath = string.Empty;
        if (node is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var pathSegments = new Stack<string>();
        ExpressionSyntax currentExpression = memberAccess;
        while (currentExpression is MemberAccessExpressionSyntax currentMemberAccess)
        {
            pathSegments.Push(currentMemberAccess.Name.Identifier.ValueText);
            currentExpression = currentMemberAccess.Expression;
        }

        if (currentExpression is not IdentifierNameSyntax || pathSegments.Count == 0)
        {
            return false;
        }

        memberPath = string.Join(".", pathSegments);
        return true;
    }

    private static void AnalyzeLambdaExpression(
        SyntaxNodeAnalysisContext context,
        LambdaExpressionSyntax lambda,
        string propertyName)
    {
        // Check for method invocations
        var invocations = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>();

        // Check for member access expressions (for properties like DateTime.Now)
        var memberAccesses = lambda.DescendantNodes().OfType<MemberAccessExpressionSyntax>();

        // Track collection accesses for multiple enumeration detection
        var collectionAccesses = new Dictionary<string, int>();
        var reportedIssueTypes = new HashSet<string>(StringComparer.Ordinal);
        string? sourceParameterName = GetSourceParameterName(lambda);

        foreach (InvocationExpressionSyntax? invocation in invocations)
        {
            SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            {
                continue;
            }

            string containingType = methodSymbol.ContainingType?.ToDisplayString() ?? "";
            string methodName = methodSymbol.Name;

            // Check for database operations
            if (IsDatabaseOperation(containingType))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "database query");
                }

                continue;
            }

            // Check for file I/O
            if (IsFileIOOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "file I/O operation");
                }

                continue;
            }

            // Check for HTTP requests
            if (IsHttpOperation(containingType))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "HTTP request");
                }

                continue;
            }

            // Check for reflection
            if (IsReflectionOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "reflection operation");
                }

                continue;
            }

            // Check for Task.Result
            if (IsTaskResultAccess(invocation, context.SemanticModel))
            {
                if (reportedIssueTypes.Add(TaskResultIssueType))
                {
                    ReportTaskResultDiagnostic(context, lambda, propertyName);
                }

                continue;
            }

            if (IsTaskWaitAccess(containingType, methodName))
            {
                if (reportedIssueTypes.Add(TaskResultIssueType))
                {
                    ReportTaskResultDiagnostic(context, lambda, propertyName);
                }

                continue;
            }

            // Check for non-deterministic operations
            if (IsNonDeterministicOperation(containingType, methodName, out string operationType))
            {
                if (reportedIssueTypes.Add(NonDeterministicIssueType))
                {
                    ReportNonDeterministicDiagnostic(context, lambda, propertyName, operationType);
                }

                continue;
            }

            // Track LINQ operations for multiple enumeration detection
            if (IsLinqEnumerationMethod(containingType, methodName))
            {
                TrackCollectionAccess(invocation, collectionAccesses, sourceParameterName, context.SemanticModel);
            }

            // Check for complex LINQ operations
            if (IsComplexLinqOperation(methodName, invocation))
            {
                if (reportedIssueTypes.Add(ComplexLinqIssueType))
                {
                    ReportComplexLinqDiagnostic(context, lambda, propertyName);
                }

                continue;
            }

            // Check for external method calls (not on source properties)
            if (IsExternalMethodCall(invocation, context.SemanticModel))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "method call");
                }
            }
        }

        // Check member access expressions for non-deterministic properties
        foreach (MemberAccessExpressionSyntax? memberAccess in memberAccesses)
        {
            SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);
            if (symbolInfo.Symbol is IPropertySymbol propertySymbol)
            {
                string containingType = propertySymbol.ContainingType?.ToDisplayString() ?? "";
                string propertyName_member = propertySymbol.Name;

                if (IsTaskResultMemberAccess(memberAccess, propertySymbol))
                {
                    if (memberAccess.Expression is InvocationExpressionSyntax &&
                        reportedIssueTypes.Contains(ExpensiveOperationIssueType))
                    {
                        continue;
                    }

                    if (reportedIssueTypes.Add(TaskResultIssueType))
                    {
                        ReportTaskResultDiagnostic(context, lambda, propertyName);
                    }

                    continue;
                }

                // Check for DateTime.Now, DateTime.UtcNow
                if (containingType == "System.DateTime" &&
                    (propertyName_member == "Now" || propertyName_member == "UtcNow"))
                {
                    if (reportedIssueTypes.Add(NonDeterministicIssueType))
                    {
                        string operationType = propertyName_member == "UtcNow" ? "DateTime.UtcNow" : "DateTime.Now";
                        ReportNonDeterministicDiagnostic(context, lambda, propertyName, operationType);
                    }
                }
            }
        }

        // Check for multiple enumerations
        foreach (KeyValuePair<string, int> kvp in collectionAccesses)
        {
            if (kvp.Value > 1 && reportedIssueTypes.Add(MultipleEnumerationIssueType))
            {
                ReportMultipleEnumerationDiagnostic(context, lambda, propertyName, kvp.Key);
            }
        }

        // Check for expensive computations (complex expressions with multiple operations)
        if (IsExpensiveComputation(lambda) && reportedIssueTypes.Add(ExpensiveComputationIssueType))
        {
            ReportExpensiveComputationDiagnostic(context, lambda, propertyName);
        }
    }

    private static void ReportExpensiveOperationDiagnostic(
        SyntaxNodeAnalysisContext context,
        LambdaExpressionSyntax lambda,
        string propertyName,
        string operationType)
    {
        ReportDiagnostic(
            context,
            lambda.GetLocation(),
            ExpensiveOperationInMapFromRule,
            ExpensiveOperationIssueType,
            propertyName,
            messageArgs: [propertyName, operationType],
            operationType: operationType);
    }

    private static void ReportMultipleEnumerationDiagnostic(
        SyntaxNodeAnalysisContext context,
        LambdaExpressionSyntax lambda,
        string propertyName,
        string collectionName)
    {
        ReportDiagnostic(
            context,
            lambda.GetLocation(),
            MultipleEnumerationRule,
            MultipleEnumerationIssueType,
            propertyName,
            messageArgs: [propertyName, collectionName],
            collectionName: collectionName);
    }

    private static void ReportExpensiveComputationDiagnostic(
        SyntaxNodeAnalysisContext context,
        LambdaExpressionSyntax lambda,
        string propertyName)
    {
        ReportDiagnostic(
            context,
            lambda.GetLocation(),
            ExpensiveComputationRule,
            ExpensiveComputationIssueType,
            propertyName,
            messageArgs: [propertyName]);
    }

    private static void ReportTaskResultDiagnostic(
        SyntaxNodeAnalysisContext context,
        LambdaExpressionSyntax lambda,
        string propertyName)
    {
        ReportDiagnostic(
            context,
            lambda.GetLocation(),
            TaskResultSynchronousAccessRule,
            TaskResultIssueType,
            propertyName,
            messageArgs: [propertyName]);
    }

    private static void ReportComplexLinqDiagnostic(
        SyntaxNodeAnalysisContext context,
        LambdaExpressionSyntax lambda,
        string propertyName)
    {
        ReportDiagnostic(
            context,
            lambda.GetLocation(),
            ComplexLinqOperationRule,
            ComplexLinqIssueType,
            propertyName,
            messageArgs: [propertyName]);
    }

    private static void ReportNonDeterministicDiagnostic(
        SyntaxNodeAnalysisContext context,
        LambdaExpressionSyntax lambda,
        string propertyName,
        string operationType)
    {
        ReportDiagnostic(
            context,
            lambda.GetLocation(),
            NonDeterministicOperationRule,
            NonDeterministicIssueType,
            propertyName,
            messageArgs: [propertyName, operationType],
            operationType: operationType);
    }

    private static void ReportDiagnostic(
        SyntaxNodeAnalysisContext context,
        Location location,
        DiagnosticDescriptor rule,
        string issueType,
        string propertyName,
        string[] messageArgs,
        string? operationType = null,
        string? collectionName = null)
    {
        ImmutableDictionary<string, string?> properties =
            CreateDiagnosticProperties(issueType, propertyName, operationType, collectionName);
        var diagnostic = Diagnostic.Create(rule, location, properties, messageArgs);
        context.ReportDiagnostic(diagnostic);
    }

    private static ImmutableDictionary<string, string?> CreateDiagnosticProperties(
        string issueType,
        string propertyName,
        string? operationType,
        string? collectionName)
    {
        ImmutableDictionary<string, string?>.Builder properties =
            ImmutableDictionary.CreateBuilder<string, string?>();
        properties.Add(IssueTypePropertyName, issueType);
        properties.Add(PropertyNamePropertyName, propertyName);

        if (!string.IsNullOrEmpty(operationType))
        {
            properties.Add(OperationTypePropertyName, operationType);
        }

        if (!string.IsNullOrEmpty(collectionName))
        {
            properties.Add(CollectionNamePropertyName, collectionName);
        }

        return properties.ToImmutable();
    }

    private static bool IsDatabaseOperation(string containingType)
    {
        // Restrict to known data-access namespaces/types to avoid method-name false positives.
        return containingType.Contains("DbSet", StringComparison.Ordinal) ||
               containingType.Contains("DbContext", StringComparison.Ordinal) ||
               containingType.Contains("IQueryable", StringComparison.Ordinal) ||
               containingType == "System.Linq.Queryable" ||
               containingType.Contains("EntityFrameworkQueryableExtensions", StringComparison.Ordinal) ||
               containingType == "NHibernate.ISession" ||
               containingType.StartsWith("NHibernate.", StringComparison.Ordinal) ||
               containingType.Contains("SqlConnection", StringComparison.Ordinal) ||
               containingType.Contains("System.Data", StringComparison.Ordinal) ||
               containingType.Contains("Dapper", StringComparison.Ordinal);
    }

    private static bool IsFileIOOperation(string containingType, string methodName)
    {
        return containingType == "System.IO.File" ||
               containingType == "System.IO.Directory" ||
               containingType == "System.IO.Path" ||
               (methodName.Contains("Read") && containingType.Contains("System.IO")) ||
               (methodName.Contains("Write") && containingType.Contains("System.IO"));
    }

    private static bool IsHttpOperation(string containingType)
    {
        return containingType.Contains("HttpClient") ||
               containingType.Contains("WebClient") ||
               containingType.Contains("HttpMessageInvoker");
    }

    private static bool IsReflectionOperation(string containingType, string methodName)
    {
        if (methodName == "GetType" && (containingType == "System.Object" || containingType == "object"))
        {
            return true;
        }

        bool isReflectionType = containingType == "System.Type" ||
                                containingType.StartsWith("System.Reflection.", StringComparison.Ordinal);

        if (!isReflectionType)
        {
            return false;
        }

        return methodName == "GetMethod" ||
               methodName == "GetProperty" ||
               methodName == "GetField" ||
               methodName == "GetCustomAttributes" ||
               methodName == "Invoke";
    }

    private static bool IsTaskResultAccess(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        // Check if this invocation is used with .Result (e.g., SomeTask().Result)
        if (invocation.Parent is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.Text == "Result")
        {
            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
            if (symbolInfo.Symbol is IPropertySymbol propertySymbol &&
                propertySymbol.Name == "Result")
            {
                return IsTaskType(propertySymbol.ContainingType);
            }
        }

        return false;
    }

    private static bool IsTaskResultMemberAccess(
        MemberAccessExpressionSyntax memberAccess,
        IPropertySymbol propertySymbol)
    {
        return memberAccess.Name.Identifier.Text == "Result" &&
               propertySymbol.Name == "Result" &&
               IsTaskType(propertySymbol.ContainingType);
    }

    private static bool IsTaskType(ITypeSymbol? typeSymbol)
    {
        string containingType = typeSymbol?.ToDisplayString() ?? "";
        // ValueTask<T> exposes the same blocking .Result accessor as Task<T>, so it carries the
        // same synchronous-access deadlock/perf risk and must be detected here too.
        return containingType.StartsWith("System.Threading.Tasks.Task", StringComparison.Ordinal) ||
               containingType.StartsWith("System.Threading.Tasks.ValueTask", StringComparison.Ordinal);
    }

    private static bool IsTaskWaitAccess(string containingType, string methodName)
    {
        return methodName == "Wait" &&
               containingType.StartsWith("System.Threading.Tasks.Task", StringComparison.Ordinal);
    }

    private static bool IsNonDeterministicOperation(string containingType, string methodName, out string operationType)
    {
        operationType = "";

        if (containingType == "System.DateTime" && (methodName == "get_Now" || methodName == "get_UtcNow"))
        {
            operationType = methodName == "get_UtcNow" ? "DateTime.UtcNow" : "DateTime.Now";
            return true;
        }

        if (containingType == "System.Random" &&
            methodName is "Next" or "NextDouble" or "NextInt64" or "NextSingle" or "NextBytes")
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

    private static bool IsLinqEnumerationMethod(string containingType, string methodName)
    {
        if (containingType != "System.Linq.Enumerable" && containingType != "System.Linq.Queryable")
        {
            return false;
        }

        return methodName is "ToList" or "ToArray" or "ToHashSet" or "ToDictionary" or "ToLookup" or
            "Sum" or "Average" or "Min" or "Max" or "Aggregate" or
            "Count" or "LongCount" or
            "First" or "FirstOrDefault" or "Last" or "LastOrDefault" or
            "Single" or "SingleOrDefault" or
            "Any" or "All";
    }

    private static void TrackCollectionAccess(
        InvocationExpressionSyntax invocation,
        Dictionary<string, int> collectionAccesses,
        string? sourceParameterName,
        SemanticModel semanticModel)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            ExpressionSyntax collectionRoot = UnwrapChainedLinqReceiver(
                memberAccess.Expression,
                sourceParameterName,
                semanticModel);

            string collectionName = TryGetSourceCollectionPath(
                collectionRoot,
                sourceParameterName,
                out string sourceCollectionPath)
                ? sourceCollectionPath
                : collectionRoot.ToString();

            if (!collectionAccesses.ContainsKey(collectionName))
            {
                collectionAccesses[collectionName] = 0;
            }

            collectionAccesses[collectionName]++;
        }
    }

    private static ExpressionSyntax UnwrapChainedLinqReceiver(
        ExpressionSyntax expression,
        string? sourceParameterName,
        SemanticModel semanticModel)
    {
        // For shapes like src.Items.Where(...).Count() / .Any() / .Sum() the terminal LINQ
        // receiver is a chained invocation. Peel only invocations that resolve to a known lazy
        // System.Linq operator. Even after peeling, only adopt the new root when it normalises
        // to a source-parameter-rooted member path — otherwise the original (un-peeled) receiver
        // string already distinguishes distinct method-call sources such as `src.GetItems()`
        // vs `src.GetOtherItems()`, and adopting the peeled root would collapse them into the
        // same key.
        ExpressionSyntax candidate = expression;
        while (candidate is InvocationExpressionSyntax chainedInvocation &&
               chainedInvocation.Expression is MemberAccessExpressionSyntax chainedMemberAccess &&
               IsLazyLinqInvocation(chainedInvocation, chainedMemberAccess, semanticModel))
        {
            candidate = chainedMemberAccess.Expression;
        }

        return TryGetSourceCollectionPath(candidate, sourceParameterName, out _)
            ? candidate
            : expression;
    }

    private static bool IsLazyLinqInvocation(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        SemanticModel semanticModel)
    {
        if (!IsLazyLinqOperatorName(memberAccess.Name.Identifier.ValueText))
        {
            return false;
        }

        IMethodSymbol? methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol
            ?? semanticModel.GetSymbolInfo(invocation).CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

        string? containingType = methodSymbol?.ContainingType?.ToDisplayString();
        return containingType == "System.Linq.Enumerable" || containingType == "System.Linq.Queryable";
    }

    private static bool IsLazyLinqOperatorName(string methodName)
    {
        return methodName is
            "Where" or "Select" or "SelectMany" or
            "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending" or
            "GroupBy" or "Distinct" or
            "Skip" or "SkipWhile" or "SkipLast" or
            "Take" or "TakeWhile" or "TakeLast" or
            "Reverse" or "Cast" or "OfType" or "DefaultIfEmpty";
    }

    private static string? GetSourceParameterName(LambdaExpressionSyntax lambda)
    {
        return lambda switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda when parenthesizedLambda.ParameterList.Parameters.Count > 0
                => parenthesizedLambda.ParameterList.Parameters[0].Identifier.ValueText,
            _ => null
        };
    }

    private static bool TryGetSourceCollectionPath(
        ExpressionSyntax expression,
        string? sourceParameterName,
        out string collectionPath)
    {
        collectionPath = string.Empty;
        if (string.IsNullOrEmpty(sourceParameterName))
        {
            return false;
        }

        var pathSegments = new Stack<string>();
        ExpressionSyntax currentExpression = expression;
        while (currentExpression is MemberAccessExpressionSyntax memberAccess)
        {
            pathSegments.Push(memberAccess.Name.Identifier.ValueText);
            currentExpression = memberAccess.Expression;
        }

        if (currentExpression is not IdentifierNameSyntax identifier ||
            !string.Equals(identifier.Identifier.ValueText, sourceParameterName, StringComparison.Ordinal) ||
            pathSegments.Count == 0)
        {
            return false;
        }

        collectionPath = string.Join(".", pathSegments);
        return true;
    }

    private static bool IsComplexLinqOperation(string methodName, InvocationExpressionSyntax invocation)
    {
        // SelectMany with nested operations
        if (methodName == "SelectMany")
        {
            // Check if the argument contains complex lambda
            SeparatedSyntaxList<ArgumentSyntax> args = invocation.ArgumentList.Arguments;
            if (args.Count > 0 && args[0].Expression is LambdaExpressionSyntax lambda)
            {
                // Check for nested Where, Select, or other complex operations
                int nestedInvocations = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>().Count();
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

        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);
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
        string containingType = methodSymbol.ContainingType?.ToDisplayString() ?? "";
        if (containingType == "string" || containingType == "System.String")
        {
            return false;
        }

        // Exclude simple LINQ extension methods on the source object
        string[] simpleLinqMethods =
            new[] { "Select", "Where", "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending" };
        if (simpleLinqMethods.Contains(methodSymbol.Name) && IsCalledOnSourceProperty(memberAccess))
        {
            return false;
        }

        // Check if it's called on a field or injected dependency
        ISymbol? expressionSymbol = semanticModel.GetSymbolInfo(memberAccess.Expression).Symbol;
        if (expressionSymbol is IFieldSymbol || expressionSymbol is IPropertySymbol { IsReadOnly: true })
        {
            // This is likely a method call on an injected service
            return true;
        }

        return false;
    }

    private static bool IsCalledOnSourceProperty(MemberAccessExpressionSyntax memberAccess)
    {
        string expression = memberAccess.Expression.ToString();
        return expression.StartsWith("src.") || expression == "src";
    }

    private static bool IsExpensiveComputation(LambdaExpressionSyntax lambda)
    {
        // Look for Enumerable.Range with complex predicates (like prime checking)
        var invocations = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (InvocationExpressionSyntax? invocation in invocations)
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

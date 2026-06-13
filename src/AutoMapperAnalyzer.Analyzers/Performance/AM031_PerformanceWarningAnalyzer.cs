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
    private static readonly string[] LinearCollectionContainsTypes =
    [
        "System.Collections.Generic.List<",
        "System.Collections.ObjectModel.Collection<",
        "System.Collections.ObjectModel.ReadOnlyCollection<"
    ];

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
        "Expensive operations (database queries, API calls, file I/O, blocking calls) should be performed before mapping, not during.");

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
    ///     Diagnostic rule for synchronous access of async operations.
    /// </summary>
    public static readonly DiagnosticDescriptor TaskResultSynchronousAccessRule = new(
        "AM031",
        "Synchronous access of async operation in mapping",
        "Property '{0}' mapping synchronously waits on an async operation, which can cause deadlocks. Perform async operations before mapping.",
        "AutoMapper.Performance",
        DiagnosticSeverity.Warning,
        true,
        "Using Task.Result, Task.Wait(), Task.WaitAll(), Task.WaitAny(), or GetAwaiter().GetResult() in mapping can cause deadlocks and performance issues. Await async operations before mapping.");

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
        "Non-deterministic operations (DateTime.Now/UtcNow, DateTimeOffset.Now/UtcNow, Random, RandomNumberGenerator, Guid.NewGuid, Environment state operations) make mappings harder to test. Consider computing before mapping.");

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
                if (IsSourceRootedDbContextMethodCall(invocation, containingType, sourceParameterName))
                {
                    continue;
                }

                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "database query");
                }

                continue;
            }

            if (IsCompressionOperation(invocation, methodSymbol, context.SemanticModel))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "compression operation");
                }

                continue;
            }

            // Check for file I/O
            if (IsFileIOOperation(containingType, methodName))
            {
                if (IsInMemoryIOInvocation(invocation, containingType, methodName, context.SemanticModel))
                {
                    continue;
                }

                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "file I/O operation");
                }

                continue;
            }

            if (IsConsoleIOOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "console I/O operation");
                }

                continue;
            }

            // Check for HTTP requests
            if (IsHttpOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "HTTP request");
                }

                continue;
            }

            if (IsNetworkLookupOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "network lookup");
                }

                continue;
            }

            if (IsSocketNetworkIOOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "network I/O operation");
                }

                continue;
            }

            if (IsNetworkProbeOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "network I/O operation");
                }

                continue;
            }

            if (IsResourceLookupOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "resource lookup");
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

            if (IsProcessStartOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "process launch");
                }

                continue;
            }

            if (IsProcessControlOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "process control operation");
                }

                continue;
            }

            if (IsProcessTerminationOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "process termination");
                }

                continue;
            }

            if (IsProcessBlockingWaitOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "blocking process operation");
                }

                continue;
            }

            if (IsGarbageCollectionOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "GC operation");
                }

                continue;
            }

            if (IsThreadBlockingOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "blocking thread operation");
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

            if (IsTaskAwaiterGetResultAccess(containingType, methodName))
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

            if (IsBackgroundWorkSchedulingOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "background work scheduling");
                }

                continue;
            }

            if (IsSerializationOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "serialization operation");
                }

                continue;
            }

            if (IsParsingOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "parsing operation");
                }

                continue;
            }

            if (IsRegexOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "regex operation");
                }

                continue;
            }

            if (IsCryptographicOperation(containingType, methodName))
            {
                if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                {
                    ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "cryptographic operation");
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
            bool isLinqEnumerationMethod = IsLinqEnumerationMethod(containingType, methodName);
            if (isLinqEnumerationMethod ||
                IsLinearCollectionContainsMethod(containingType, methodName))
            {
                TrackCollectionAccess(
                    invocation,
                    collectionAccesses,
                    sourceParameterName,
                    context.SemanticModel,
                    isLinqEnumerationMethod,
                    methodName);
            }

            // Check for complex LINQ operations
            if (IsComplexLinqOperation(containingType, methodName, invocation))
            {
                if (reportedIssueTypes.Add(ComplexLinqIssueType))
                {
                    ReportComplexLinqDiagnostic(context, lambda, propertyName);
                }

                continue;
            }

            // Check for external method calls (not on source properties)
            if (IsExternalMethodCall(invocation, context.SemanticModel, sourceParameterName) &&
                !ContainsNestedParsingOperation(invocation, context.SemanticModel) &&
                !ContainsNestedReflectionOperation(invocation, context.SemanticModel))
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

                if (IsFileSystemMetadataProperty(containingType, propertyName_member))
                {
                    if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                    {
                        ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "file I/O operation");
                    }

                    continue;
                }

                if (IsReflectionMetadataProperty(containingType, propertyName_member))
                {
                    if (reportedIssueTypes.Add(ExpensiveOperationIssueType))
                    {
                        ReportExpensiveOperationDiagnostic(context, lambda, propertyName, "reflection operation");
                    }

                    continue;
                }

                if (IsEnvironmentStateProperty(containingType, propertyName_member, out string environmentOperationType))
                {
                    if (reportedIssueTypes.Add(NonDeterministicIssueType))
                    {
                        ReportNonDeterministicDiagnostic(context, lambda, propertyName, environmentOperationType);
                    }

                    continue;
                }

                // Check for DateTime.Now, DateTime.UtcNow, DateTimeOffset.Now, DateTimeOffset.UtcNow
                if ((containingType == "System.DateTime" || containingType == "System.DateTimeOffset") &&
                    (propertyName_member == "Now" || propertyName_member == "UtcNow"))
                {
                    if (reportedIssueTypes.Add(NonDeterministicIssueType))
                    {
                        string typeName = containingType == "System.DateTimeOffset" ? "DateTimeOffset" : "DateTime";
                        string operationType = propertyName_member == "UtcNow"
                            ? $"{typeName}.UtcNow"
                            : $"{typeName}.Now";
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
        if (IsExpensiveComputation(lambda, context.SemanticModel) && reportedIssueTypes.Add(ExpensiveComputationIssueType))
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
        return IsDbSetType(containingType) ||
               IsDbContextType(containingType) ||
               IsIQueryableType(containingType) ||
               containingType == "System.Linq.Queryable" ||
               IsEntityFrameworkQueryableExtensionsType(containingType) ||
               IsNHibernateOperation(containingType) ||
               containingType.StartsWith("System.Data.", StringComparison.Ordinal) ||
               IsSqlConnectionType(containingType) ||
               IsDapperOperation(containingType);
    }

    private static bool IsDbSetType(string containingType)
    {
        return containingType.StartsWith("Microsoft.EntityFrameworkCore.DbSet<", StringComparison.Ordinal) ||
               containingType.StartsWith("System.Data.Entity.DbSet<", StringComparison.Ordinal);
    }

    private static bool IsDbContextType(string containingType)
    {
        return containingType == "DbContext" ||
               containingType.EndsWith("DbContext", StringComparison.Ordinal);
    }

    private static bool IsSourceRootedDbContextMethodCall(
        InvocationExpressionSyntax invocation,
        string containingType,
        string? sourceParameterName)
    {
        if (!IsDbContextType(containingType) ||
            invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        return IsCalledOnSourceExpression(memberAccess, sourceParameterName);
    }

    private static bool IsIQueryableType(string containingType)
    {
        return containingType == "System.Linq.IQueryable" ||
               containingType.StartsWith("System.Linq.IQueryable<", StringComparison.Ordinal);
    }

    private static bool IsSqlConnectionType(string containingType)
    {
        return containingType == "Microsoft.Data.SqlClient.SqlConnection";
    }

    private static bool IsEntityFrameworkQueryableExtensionsType(string containingType)
    {
        return containingType == "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions";
    }

    private static bool IsDapperOperation(string containingType)
    {
        return containingType == "Dapper.SqlMapper";
    }

    private static bool IsNHibernateOperation(string containingType)
    {
        return containingType == "NHibernate.ISession";
    }

    private static bool IsFileIOOperation(string containingType, string methodName)
    {
        if (IsInMemoryIOType(containingType))
        {
            return false;
        }

        return containingType == "System.IO.File" ||
               containingType == "System.IO.Directory" ||
               (containingType == "System.IO.Path" && methodName is "Exists" or "GetTempFileName") ||
               (containingType == "System.IO.Compression.ZipFile" &&
                methodName is "Open" or "OpenRead" or "CreateFromDirectory" or "ExtractToDirectory") ||
               (containingType == "System.IO.Compression.ZipFileExtensions" &&
                methodName is "CreateEntryFromFile" or "ExtractToFile") ||
               (containingType == "System.IO.MemoryMappedFiles.MemoryMappedFile" &&
                methodName is "CreateFromFile" or "OpenExisting" or "CreateNew" or "CreateOrOpen" or
                              "CreateViewAccessor" or "CreateViewStream") ||
               (containingType == "System.IO.MemoryMappedFiles.MemoryMappedViewAccessor" &&
                methodName == "Flush") ||
               (containingType == "System.IO.Stream" &&
                methodName is "CopyTo" or "CopyToAsync") ||
               (containingType == "System.IO.FileStream" &&
                methodName is "Flush" or "SetLength" or "Lock" or "Unlock" or "CopyTo" or "CopyToAsync") ||
               (containingType == "System.IO.FileSystemInfo" &&
                methodName is "Delete" or "Refresh") ||
               (containingType == "System.IO.FileInfo" &&
                methodName is "Open" or "OpenRead" or "OpenText" or "OpenWrite" or
                              "Create" or "CreateText" or "AppendText" or "CopyTo" or
                              "Delete" or "MoveTo" or "Replace" or "Decrypt" or "Encrypt") ||
               (containingType == "System.IO.DirectoryInfo" &&
                methodName is "GetFiles" or "EnumerateFiles" or
                              "GetDirectories" or "EnumerateDirectories" or
                              "GetFileSystemInfos" or "EnumerateFileSystemInfos" or
                              "Create" or "CreateSubdirectory" or "Delete" or "MoveTo") ||
               (methodName.Contains("Read") && containingType.Contains("System.IO")) ||
               (methodName.Contains("Write") && containingType.Contains("System.IO"));
    }

    private static bool IsFileSystemMetadataProperty(string containingType, string propertyName)
    {
        return (containingType == "System.IO.FileInfo" &&
                propertyName is "Length" or "IsReadOnly") ||
               IsFileSystemInfoType(containingType) &&
               propertyName is "Exists" or "Attributes" or
                               "CreationTime" or "CreationTimeUtc" or
                               "LastAccessTime" or "LastAccessTimeUtc" or
                               "LastWriteTime" or "LastWriteTimeUtc";
    }

    private static bool IsFileSystemInfoType(string containingType)
    {
        return containingType is "System.IO.FileSystemInfo" or "System.IO.FileInfo" or "System.IO.DirectoryInfo";
    }

    private static bool IsInMemoryIOType(string containingType)
    {
        return containingType == "System.IO.MemoryStream" ||
               containingType == "System.IO.StringReader" ||
               containingType == "System.IO.StringWriter";
    }

    private static bool IsInMemoryIOInvocation(
        InvocationExpressionSyntax invocation,
        string containingType,
        string methodName,
        SemanticModel semanticModel)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        if (IsMemoryBackedIOHelperMethod(containingType, methodName))
        {
            return IsIOHelperOverMemoryStream(memberAccess.Expression, semanticModel);
        }

        if (IsStringBackedTextHelperMethod(containingType, methodName))
        {
            return IsTextHelperOverStringIO(memberAccess.Expression, semanticModel);
        }

        if (IsMemoryStreamAbstractionMethod(containingType, methodName))
        {
            return IsMemoryStreamExpression(memberAccess.Expression, semanticModel);
        }

        return false;
    }

    private static bool IsMemoryBackedIOHelperMethod(string containingType, string methodName)
    {
        return IsMemoryBackedReaderType(containingType) && methodName.Contains("Read", StringComparison.Ordinal) ||
               IsMemoryBackedWriterType(containingType) && methodName.Contains("Write", StringComparison.Ordinal);
    }

    private static bool IsMemoryStreamAbstractionMethod(string containingType, string methodName)
    {
        return containingType == "System.IO.Stream" &&
               (methodName.Contains("Read", StringComparison.Ordinal) ||
                methodName.Contains("Write", StringComparison.Ordinal) ||
                methodName is "CopyTo" or "CopyToAsync");
    }

    private static bool IsStringBackedTextHelperMethod(string containingType, string methodName)
    {
        return containingType == "System.IO.TextReader" && methodName.Contains("Read", StringComparison.Ordinal) ||
               containingType == "System.IO.TextWriter" && methodName.Contains("Write", StringComparison.Ordinal);
    }

    private static bool IsMemoryBackedReaderType(string containingType)
    {
        return containingType is "System.IO.StreamReader" or "System.IO.BinaryReader";
    }

    private static bool IsMemoryBackedWriterType(string containingType)
    {
        return containingType is "System.IO.BinaryWriter" or "System.IO.StreamWriter";
    }

    private static bool IsMemoryBackedIOHelperType(string containingType)
    {
        return IsMemoryBackedReaderType(containingType) || IsMemoryBackedWriterType(containingType);
    }

    private static bool IsIOHelperOverMemoryStream(
        ExpressionSyntax receiverExpression,
        SemanticModel semanticModel)
    {
        receiverExpression = RemoveParentheses(receiverExpression);
        if (IsIOHelperCreationOverMemoryStream(receiverExpression, semanticModel))
        {
            return true;
        }

        if (semanticModel.GetSymbolInfo(receiverExpression).Symbol is ILocalSymbol localSymbol &&
            TryGetLocalInitializer(localSymbol, out ExpressionSyntax? initializer) &&
            initializer is not null)
        {
            return IsIOHelperCreationOverMemoryStream(initializer, semanticModel);
        }

        return false;
    }

    private static bool IsTextHelperOverStringIO(
        ExpressionSyntax receiverExpression,
        SemanticModel semanticModel)
    {
        receiverExpression = RemoveParentheses(receiverExpression);
        if (semanticModel.GetSymbolInfo(receiverExpression).Symbol is not ILocalSymbol localSymbol ||
            !TryGetLocalInitializer(localSymbol, out ExpressionSyntax? initializer) ||
            initializer is null)
        {
            return false;
        }

        ITypeSymbol? initializerType = semanticModel.GetTypeInfo(RemoveParentheses(initializer)).Type;
        return initializerType?.ToDisplayString() is "System.IO.StringReader" or "System.IO.StringWriter";
    }

    private static bool IsIOHelperCreationOverMemoryStream(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        expression = RemoveParentheses(expression);
        if (expression is not ObjectCreationExpressionSyntax helperCreation ||
            helperCreation.ArgumentList is not { } argumentList ||
            argumentList.Arguments.Count < 1)
        {
            return false;
        }

        ITypeSymbol? helperType = semanticModel.GetTypeInfo(helperCreation).Type;
        string helperTypeName = helperType?.ToDisplayString() ?? "";
        if (!IsMemoryBackedIOHelperType(helperTypeName))
        {
            return false;
        }

        ExpressionSyntax streamExpression = RemoveParentheses(argumentList.Arguments[0].Expression);
        return IsMemoryStreamExpression(streamExpression, semanticModel);
    }

    private static bool IsMemoryStreamExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        expression = RemoveParentheses(expression);
        ITypeSymbol? streamType = semanticModel.GetTypeInfo(expression).Type;
        if (streamType?.ToDisplayString() == "System.IO.MemoryStream")
        {
            return true;
        }

        if (semanticModel.GetSymbolInfo(expression).Symbol is ILocalSymbol localSymbol &&
            TryGetLocalInitializer(localSymbol, out ExpressionSyntax? initializer) &&
            initializer is not null)
        {
            ITypeSymbol? initializerType = semanticModel.GetTypeInfo(RemoveParentheses(initializer)).Type;
            return initializerType?.ToDisplayString() == "System.IO.MemoryStream";
        }

        return false;
    }

    private static bool TryGetLocalInitializer(
        ILocalSymbol local,
        out ExpressionSyntax? initializer)
    {
        foreach (SyntaxReference syntaxReference in local.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is VariableDeclaratorSyntax { Initializer: not null } declarator)
            {
                initializer = declarator.Initializer.Value;
                return true;
            }
        }

        initializer = null;
        return false;
    }

    private static bool IsConsoleIOOperation(string containingType, string methodName)
    {
        return containingType == "System.Console" &&
               methodName is "Read" or "ReadKey" or "ReadLine" or "Write" or "WriteLine" or
                             "OpenStandardInput" or "OpenStandardOutput" or "OpenStandardError" or
                             "SetIn" or "SetOut" or "SetError";
    }

    private static bool IsCompressionOperation(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel)
    {
        if (methodSymbol.Name is not ("Read" or "ReadAsync" or "ReadByte" or
            "Write" or "WriteAsync" or "WriteByte" or
            "CopyTo" or "CopyToAsync"))
        {
            return false;
        }

        string containingType = methodSymbol.ContainingType?.ToDisplayString() ?? "";
        if (IsCompressionStreamType(containingType))
        {
            return true;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            string receiverType = semanticModel.GetTypeInfo(memberAccess.Expression).Type?.ToDisplayString() ?? "";
            return IsCompressionStreamType(receiverType);
        }

        return false;
    }

    private static bool IsCompressionStreamType(string typeName)
    {
        return typeName is "System.IO.Compression.GZipStream" or
                           "System.IO.Compression.DeflateStream" or
                           "System.IO.Compression.BrotliStream" or
                           "System.IO.Compression.ZLibStream";
    }

    private static bool IsHttpOperation(string containingType, string methodName)
    {
        return (containingType == "System.Net.Http.HttpClient" &&
                methodName is "Send" or "SendAsync" or
                              "GetAsync" or "GetByteArrayAsync" or "GetStreamAsync" or "GetStringAsync" or
                              "PostAsync" or "PutAsync" or "PatchAsync" or "DeleteAsync") ||
               (containingType == "System.Net.WebClient" &&
                methodName is "OpenRead" or "OpenReadAsync" or "OpenReadTaskAsync" or
                              "OpenWrite" or "OpenWriteAsync" or "OpenWriteTaskAsync" or
                              "DownloadString" or "DownloadStringAsync" or "DownloadStringTaskAsync" or
                              "DownloadData" or "DownloadDataAsync" or "DownloadDataTaskAsync" or
                              "DownloadFile" or "DownloadFileAsync" or "DownloadFileTaskAsync" or
                              "UploadString" or "UploadStringAsync" or "UploadStringTaskAsync" or
                              "UploadData" or "UploadDataAsync" or "UploadDataTaskAsync" or
                              "UploadFile" or "UploadFileAsync" or "UploadFileTaskAsync" or
                              "UploadValues" or "UploadValuesAsync" or "UploadValuesTaskAsync") ||
               (containingType == "System.Net.Http.HttpMessageInvoker" &&
                methodName is "Send" or "SendAsync") ||
               (containingType == "System.Net.Http.HttpContent" &&
                methodName is "ReadAsStringAsync" or "ReadAsByteArrayAsync" or "ReadAsStreamAsync" or
                              "LoadIntoBufferAsync" or "CopyToAsync") ||
               (containingType == "System.Net.Http.Json.HttpClientJsonExtensions" &&
                methodName is "GetFromJsonAsync" or "PostAsJsonAsync" or "PutAsJsonAsync" or
                              "PatchAsJsonAsync" or "DeleteFromJsonAsync") ||
               (containingType == "System.Net.Http.Json.HttpContentJsonExtensions" &&
                methodName == "ReadFromJsonAsync") ||
               ((containingType == "System.Net.WebRequest" || containingType == "System.Net.HttpWebRequest") &&
                methodName is "GetResponse" or "GetResponseAsync" or
                              "GetRequestStream" or "GetRequestStreamAsync" or
                              "BeginGetResponse" or "EndGetResponse" or
                              "BeginGetRequestStream" or "EndGetRequestStream");
    }

    private static bool IsNetworkLookupOperation(string containingType, string methodName)
    {
        return containingType == "System.Net.Dns" &&
               methodName is "GetHostAddresses" or "GetHostAddressesAsync" or
                             "GetHostEntry" or "GetHostEntryAsync" or
                             "BeginGetHostAddresses" or "EndGetHostAddresses" or
                             "BeginGetHostEntry" or "EndGetHostEntry" or
                             "Resolve" or "GetHostByName" or "GetHostByAddress";
    }

    private static bool IsSocketNetworkIOOperation(string containingType, string methodName)
    {
        return (containingType == "System.Net.Sockets.TcpClient" &&
                methodName is "Connect" or "ConnectAsync" or "BeginConnect" or "EndConnect") ||
               (containingType == "System.Net.Sockets.UdpClient" &&
                methodName is "Connect" or "Send" or "SendAsync" or "Receive" or "ReceiveAsync" or
                              "BeginSend" or "EndSend" or "BeginReceive" or "EndReceive") ||
               (containingType == "System.Net.Sockets.Socket" &&
                methodName is "Connect" or "ConnectAsync" or "Accept" or "AcceptAsync" or
                              "Send" or "SendAsync" or "SendTo" or "SendToAsync" or
                              "Receive" or "ReceiveAsync" or "ReceiveFrom" or "ReceiveFromAsync" or
                              "BeginConnect" or "EndConnect" or "BeginAccept" or "EndAccept" or
                              "BeginSend" or "EndSend" or "BeginReceive" or "EndReceive") ||
               (containingType == "System.Net.Sockets.NetworkStream" &&
                (methodName.Contains("Read") || methodName.Contains("Write")));
    }

    private static bool IsNetworkProbeOperation(string containingType, string methodName)
    {
        return containingType == "System.Net.NetworkInformation.Ping" &&
               methodName is "Send" or "SendAsync" or "SendPingAsync";
    }

    private static bool IsResourceLookupOperation(string containingType, string methodName)
    {
        return containingType == "System.Resources.ResourceManager" &&
               methodName is "GetString" or "GetObject" or "GetStream" or "GetResourceSet";
    }

    private static bool IsReflectionOperation(string containingType, string methodName)
    {
        if (methodName == "GetType" && (containingType == "System.Object" || containingType == "object"))
        {
            return true;
        }

        if (containingType == "System.Activator" &&
            methodName == "CreateInstance")
        {
            return true;
        }

        if ((containingType == "System.Linq.Expressions.LambdaExpression" ||
             containingType.StartsWith("System.Linq.Expressions.Expression<", StringComparison.Ordinal)) &&
            methodName is "Compile" or "CompileToMethod")
        {
            return true;
        }

        if (containingType == "System.Attribute" &&
            methodName is "GetCustomAttribute" or "GetCustomAttributes" or "IsDefined")
        {
            return true;
        }

        if (containingType == "System.Runtime.Loader.AssemblyLoadContext" &&
            methodName is "LoadFromAssemblyName" or "LoadFromAssemblyPath" or "LoadFromStream" or
                          "LoadUnmanagedDllFromPath" or "GetLoadContext")
        {
            return true;
        }

        bool isReflectionType = containingType == "System.Type" ||
                                containingType.StartsWith("System.Reflection.", StringComparison.Ordinal);

        if (!isReflectionType)
        {
            return false;
        }

        if (containingType == "System.Reflection.Assembly" &&
            methodName is "Load" or "LoadFrom" or "LoadFile" or "UnsafeLoadFrom" or
                          "ReflectionOnlyLoad" or "ReflectionOnlyLoadFrom" or
                          "GetAssembly" or "GetCallingAssembly" or "GetEntryAssembly" or "GetExecutingAssembly" or
                          "CreateInstance" or
                          "GetName" or "GetReferencedAssemblies" or
                          "GetManifestResourceNames" or "GetManifestResourceStream" or "GetManifestResourceInfo" or
                          "GetSatelliteAssembly" or
                          "GetFile" or "GetFiles" or
                          "GetModule" or "GetModules" or "GetLoadedModules" or "GetForwardedTypes")
        {
            return true;
        }

        if (containingType == "System.Reflection.AssemblyName" &&
            methodName == "GetAssemblyName")
        {
            return true;
        }

        if (containingType.StartsWith("System.Reflection.Emit.", StringComparison.Ordinal) &&
            methodName is "DefineDynamicAssembly" or "DefineDynamicModule" or
                          "DefineType" or "DefineMethod" or "DefineField" or "DefineProperty" or
                          "DefineConstructor" or "DefineEvent" or "DefineParameter" or
                          "DefineGenericParameters" or "CreateType" or "CreateTypeInfo" or
                          "GetILGenerator" or "Emit" or "EmitCall" or "EmitWriteLine" or
                          "SetCustomAttribute")
        {
            return true;
        }

        return methodName is "GetType" or "GetTypes" or "GetTypeInfo" or
               "GetMethod" or "GetMethods" or
               "GetProperty" or "GetProperties" or
               "GetField" or "GetFields" or
               "GetConstructor" or "GetConstructors" or
               "GetEvent" or "GetEvents" or
               "GetMember" or "GetMembers" or
               "GetNestedType" or "GetNestedTypes" or
               "GetInterface" or "GetInterfaces" or
               "GetRuntimeMethod" or "GetRuntimeMethods" or
               "GetRuntimeProperty" or "GetRuntimeProperties" or
               "GetRuntimeField" or "GetRuntimeFields" or
               "GetRuntimeEvent" or "GetRuntimeEvents" or
               "GetRuntimeBaseDefinition" or
               "GetDeclaredMethod" or "GetDeclaredMethods" or
               "GetDeclaredProperty" or "GetDeclaredProperties" or
               "GetDeclaredField" or "GetDeclaredFields" or
               "GetDeclaredEvent" or "GetDeclaredEvents" or
               "GetDeclaredNestedType" or "GetDeclaredNestedTypes" or
               "GetParameters" or
               "GetGenericArguments" or "GetGenericParameterConstraints" or
               "GetTypeFromHandle" or "GetMethodFromHandle" or "GetFieldFromHandle" or "GetCurrentMethod" or
               "GetElementType" or "GetArrayRank" or
               "MakeGenericType" or "MakeGenericMethod" or
               "MakeArrayType" or "MakeByRefType" or "MakePointerType" or
               "CreateDelegate" or
               "GetCustomAttribute" or "GetCustomAttributes" or "GetCustomAttributesData" or "IsDefined" or
               "Invoke" or "InvokeMember" or
               "ResolveType" or "ResolveMethod" or "ResolveField" or "ResolveMember" or
               "ResolveString" or "ResolveSignature";
    }

    private static bool IsReflectionMetadataProperty(string containingType, string propertyName)
    {
        if (containingType == "System.Type")
        {
            return propertyName is "Assembly" or "BaseType" or "DeclaringType" or "ReflectedType" or
                   "Module" or "TypeHandle" or "GenericTypeArguments";
        }

        if (containingType == "System.Reflection.TypeInfo")
        {
            return propertyName is "DeclaredConstructors" or "DeclaredEvents" or "DeclaredFields" or
                   "DeclaredMembers" or "DeclaredMethods" or "DeclaredNestedTypes" or "DeclaredProperties" or
                   "ImplementedInterfaces" or "GenericTypeParameters";
        }

        if (containingType == "System.Reflection.Assembly")
        {
            return propertyName is "DefinedTypes" or "ExportedTypes" or "Modules" or "EntryPoint" or
                   "FullName" or "Location" or "ImageRuntimeVersion";
        }

        if (containingType.StartsWith("System.Reflection.", StringComparison.Ordinal))
        {
            return propertyName is "CustomAttributes" or "DeclaringType" or "ReflectedType" or
                   "Module" or "MetadataToken" or "Member" or
                   "ParameterType" or "PropertyType" or "FieldType" or "ReturnType";
        }

        return false;
    }

    private static bool IsProcessStartOperation(string containingType, string methodName)
    {
        return containingType == "System.Diagnostics.Process" &&
               methodName == "Start";
    }

    private static bool IsProcessControlOperation(string containingType, string methodName)
    {
        return containingType == "System.Diagnostics.Process" &&
               methodName is "Kill" or "CloseMainWindow";
    }

    private static bool IsProcessTerminationOperation(string containingType, string methodName)
    {
        return containingType == "System.Environment" &&
               methodName is "Exit" or "FailFast";
    }

    private static bool IsProcessBlockingWaitOperation(string containingType, string methodName)
    {
        return containingType == "System.Diagnostics.Process" &&
               methodName is "WaitForExit" or "WaitForInputIdle";
    }

    private static bool IsGarbageCollectionOperation(string containingType, string methodName)
    {
        return containingType == "System.GC" &&
               methodName is "Collect" or
                             "WaitForPendingFinalizers" or
                             "TryStartNoGCRegion" or "EndNoGCRegion" or
                             "AddMemoryPressure" or "RemoveMemoryPressure";
    }

    private static bool IsThreadBlockingOperation(string containingType, string methodName)
    {
        return (containingType == "System.Threading.Thread" &&
                methodName is "Sleep" or "SpinWait" or "Join") ||
               (containingType == "System.Threading.SpinWait" &&
                methodName is "SpinOnce" or "SpinUntil") ||
               (containingType == "System.Threading.WaitHandle" &&
                methodName == "WaitOne") ||
               (containingType == "System.Threading.Monitor" &&
                methodName == "Wait") ||
               (containingType == "System.Threading.SemaphoreSlim" &&
                methodName == "Wait") ||
               (containingType == "System.Threading.ManualResetEventSlim" &&
                methodName == "Wait") ||
               (containingType == "System.Threading.ReaderWriterLockSlim" &&
                methodName is "EnterReadLock" or "EnterWriteLock" or "EnterUpgradeableReadLock");
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
        return (methodName is "Wait" or "WaitAll" or "WaitAny") &&
               containingType.StartsWith("System.Threading.Tasks.Task", StringComparison.Ordinal);
    }

    private static bool IsTaskAwaiterGetResultAccess(string containingType, string methodName)
    {
        return methodName == "GetResult" &&
               (containingType.StartsWith("System.Runtime.CompilerServices.TaskAwaiter", StringComparison.Ordinal) ||
                containingType.StartsWith("System.Runtime.CompilerServices.ValueTaskAwaiter", StringComparison.Ordinal) ||
                containingType.StartsWith("System.Runtime.CompilerServices.ConfiguredTaskAwaitable", StringComparison.Ordinal) ||
                containingType.StartsWith("System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable", StringComparison.Ordinal));
    }

    private static bool IsBackgroundWorkSchedulingOperation(string containingType, string methodName)
    {
        return (containingType == "System.Threading.Tasks.Task" &&
                methodName == "Run") ||
               (containingType == "System.Threading.Tasks.TaskFactory" &&
                methodName == "StartNew") ||
               (containingType == "System.Threading.ThreadPool" &&
                methodName is "QueueUserWorkItem" or "UnsafeQueueUserWorkItem" or "RegisterWaitForSingleObject");
    }

    private static bool IsSerializationOperation(string containingType, string methodName)
    {
        return (containingType == "System.Text.Json.JsonSerializer" &&
                methodName is "Serialize" or "Deserialize" or
                              "SerializeToUtf8Bytes" or "SerializeToDocument" or "SerializeToElement" or
                              "SerializeToNode" or "SerializeAsync" or "DeserializeAsync" or
                              "DeserializeAsyncEnumerable") ||
               (containingType == "System.Xml.Serialization.XmlSerializer" &&
                methodName is "Serialize" or "Deserialize") ||
               IsRuntimeSerializationOperation(containingType, methodName);
    }

    private static bool IsRuntimeSerializationOperation(string containingType, string methodName)
    {
        if (methodName is not ("ReadObject" or "WriteObject" or "WriteStartObject" or "WriteObjectContent" or "WriteEndObject"))
        {
            return false;
        }

        return containingType is "System.Runtime.Serialization.XmlObjectSerializer" or
                                 "System.Runtime.Serialization.DataContractSerializer" or
                                 "System.Runtime.Serialization.Json.DataContractJsonSerializer";
    }

    private static bool IsParsingOperation(string containingType, string methodName)
    {
        return (containingType == "System.Text.Json.JsonDocument" &&
                methodName is "Parse" or "ParseAsync" or "ParseValue") ||
               (containingType == "System.Text.Json.Nodes.JsonNode" &&
                methodName == "Parse") ||
               (containingType is "System.Xml.Linq.XDocument" or "System.Xml.Linq.XElement" &&
                methodName is "Parse" or "Load") ||
               (containingType == "System.Xml.XmlDocument" &&
                methodName is "Load" or "LoadXml");
    }

    private static bool ContainsNestedParsingOperation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        foreach (InvocationExpressionSyntax nestedInvocation in invocation.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (semanticModel.GetSymbolInfo(nestedInvocation).Symbol is not IMethodSymbol methodSymbol)
            {
                continue;
            }

            string containingType = methodSymbol.ContainingType?.ToDisplayString() ?? "";
            if (IsParsingOperation(containingType, methodSymbol.Name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsNestedReflectionOperation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        foreach (InvocationExpressionSyntax nestedInvocation in invocation.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (semanticModel.GetSymbolInfo(nestedInvocation).Symbol is not IMethodSymbol methodSymbol)
            {
                continue;
            }

            string containingType = methodSymbol.ContainingType?.ToDisplayString() ?? "";
            if (IsReflectionOperation(containingType, methodSymbol.Name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRegexOperation(string containingType, string methodName)
    {
        return containingType == "System.Text.RegularExpressions.Regex" &&
               methodName is "IsMatch" or "Match" or "Matches" or "Replace" or "Split" or "Count";
    }

    private static bool IsCryptographicOperation(string containingType, string methodName)
    {
        if (IsCryptographicIncrementalHashOperation(containingType, methodName))
        {
            return true;
        }

        if (IsCryptographicSymmetricOperation(containingType, methodName))
        {
            return true;
        }

        if (IsCryptographicKeyDerivationOperation(containingType, methodName))
        {
            return true;
        }

        if (IsCryptographicPublicKeyOperation(containingType, methodName))
        {
            return true;
        }

        if (methodName is not ("ComputeHash" or "TryComputeHash" or "HashData" or "TryHashData"))
        {
            return false;
        }

        return containingType == "System.Security.Cryptography.HashAlgorithm" ||
               containingType == "System.Security.Cryptography.KeyedHashAlgorithm" ||
               containingType.StartsWith("System.Security.Cryptography.HMAC", StringComparison.Ordinal) ||
               containingType is "System.Security.Cryptography.MD5" or
                                  "System.Security.Cryptography.SHA1" or
                                  "System.Security.Cryptography.SHA256" or
                                  "System.Security.Cryptography.SHA384" or
                                  "System.Security.Cryptography.SHA512";
    }

    private static bool IsCryptographicIncrementalHashOperation(string containingType, string methodName)
    {
        return containingType == "System.Security.Cryptography.IncrementalHash" &&
               methodName is "CreateHash" or "CreateHMAC" or
                             "AppendData" or
                             "GetHashAndReset" or "TryGetHashAndReset" or
                             "GetCurrentHash" or "TryGetCurrentHash";
    }

    private static bool IsCryptographicKeyDerivationOperation(string containingType, string methodName)
    {
        return (containingType == "System.Security.Cryptography.Rfc2898DeriveBytes" &&
                methodName is "GetBytes" or "Pbkdf2") ||
               (containingType == "System.Security.Cryptography.PasswordDeriveBytes" &&
                methodName is "GetBytes" or "CryptDeriveKey");
    }

    private static bool IsCryptographicSymmetricOperation(string containingType, string methodName)
    {
        return (containingType == "System.Security.Cryptography.SymmetricAlgorithm" &&
                methodName is "CreateEncryptor" or "CreateDecryptor") ||
               (containingType == "System.Security.Cryptography.ICryptoTransform" &&
                methodName is "TransformBlock" or "TransformFinalBlock");
    }

    private static bool IsCryptographicPublicKeyOperation(string containingType, string methodName)
    {
        if (containingType == "System.Security.Cryptography.ECDiffieHellman" &&
            methodName is "DeriveKeyMaterial" or "DeriveKeyFromHash" or "DeriveKeyFromHmac" or "DeriveKeyTls")
        {
            return true;
        }

        if (methodName is not ("Encrypt" or "TryEncrypt" or "Decrypt" or "TryDecrypt" or
            "SignData" or "TrySignData" or "SignHash" or "TrySignHash" or
            "VerifyData" or "VerifyHash"))
        {
            return false;
        }

        return containingType is "System.Security.Cryptography.RSA" or
                                  "System.Security.Cryptography.ECDsa" or
                                  "System.Security.Cryptography.DSA";
    }

    private static bool IsNonDeterministicOperation(string containingType, string methodName, out string operationType)
    {
        operationType = "";

        if ((containingType == "System.DateTime" || containingType == "System.DateTimeOffset") &&
            (methodName == "get_Now" || methodName == "get_UtcNow"))
        {
            string typeName = containingType == "System.DateTimeOffset" ? "DateTimeOffset" : "DateTime";
            operationType = methodName == "get_UtcNow" ? $"{typeName}.UtcNow" : $"{typeName}.Now";
            return true;
        }

        if (containingType == "System.Random" &&
            methodName is "Next" or "NextDouble" or "NextInt64" or "NextSingle" or "NextBytes")
        {
            operationType = "Random";
            return true;
        }

        if (containingType == "System.Security.Cryptography.RandomNumberGenerator" &&
            methodName is "GetBytes" or "GetHexString" or "GetInt32" or "GetItems" or "GetString" or "Fill" or "GetNonZeroBytes")
        {
            operationType = "RandomNumberGenerator";
            return true;
        }

        if (containingType == "System.Diagnostics.Stopwatch" &&
            methodName is "GetTimestamp" or "StartNew" or "GetElapsedTime")
        {
            operationType = "Stopwatch";
            return true;
        }

        if (containingType == "System.Guid" && methodName == "NewGuid")
        {
            operationType = "Guid.NewGuid";
            return true;
        }

        if (containingType == "System.Environment" &&
            methodName is "GetCommandLineArgs" or
                          "GetEnvironmentVariable" or "GetEnvironmentVariables" or
                          "ExpandEnvironmentVariables" or "GetFolderPath" or
                          "GetLogicalDrives" or
                          "SetEnvironmentVariable")
        {
            operationType = "Environment";
            return true;
        }

        return false;
    }

    private static bool IsEnvironmentStateProperty(
        string containingType,
        string propertyName,
        out string operationType)
    {
        operationType = "";
        if (containingType != "System.Environment")
        {
            return false;
        }

        if (propertyName is "CommandLine" or
            "CurrentDirectory" or
            "CurrentManagedThreadId" or
            "ExitCode" or
            "HasShutdownStarted" or
            "Is64BitOperatingSystem" or
            "Is64BitProcess" or
            "MachineName" or
            "OSVersion" or
            "ProcessId" or
            "ProcessPath" or
            "ProcessorCount" or
            "StackTrace" or
            "SystemDirectory" or
            "TickCount" or
            "TickCount64" or
            "UserDomainName" or
            "UserInteractive" or
            "UserName" or
            "Version" or
            "WorkingSet")
        {
            operationType = $"Environment.{propertyName}";
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
            "Any" or "All" or "Contains" or "ElementAt" or "ElementAtOrDefault" or
            "SequenceEqual";
    }

    private static bool IsLinearCollectionContainsMethod(string containingType, string methodName)
    {
        return methodName == "Contains" &&
               LinearCollectionContainsTypes.Any(prefix => containingType.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static void TrackCollectionAccess(
        InvocationExpressionSyntax invocation,
        Dictionary<string, int> collectionAccesses,
        string? sourceParameterName,
        SemanticModel semanticModel,
        bool isLinqEnumerationMethod,
        string methodName)
    {
        if (isLinqEnumerationMethod &&
            TryTrackStaticLinqSourceArguments(invocation, collectionAccesses, sourceParameterName, semanticModel, methodName))
        {
            return;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            TrackCollectionExpression(
                memberAccess.Expression,
                collectionAccesses,
                sourceParameterName,
                semanticModel);
        }

        if (isLinqEnumerationMethod && methodName == "SequenceEqual")
        {
            TrackSequenceEqualArgumentAccess(invocation, collectionAccesses, sourceParameterName, semanticModel);
        }
    }

    private static bool TryTrackStaticLinqSourceArguments(
        InvocationExpressionSyntax invocation,
        Dictionary<string, int> collectionAccesses,
        string? sourceParameterName,
        SemanticModel semanticModel,
        string methodName)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            semanticModel.GetSymbolInfo(memberAccess.Expression).Symbol is not INamedTypeSymbol)
        {
            return false;
        }

        int sourceArgumentCount = methodName == "SequenceEqual" ? 2 : 1;
        int argumentsToTrack = Math.Min(sourceArgumentCount, invocation.ArgumentList.Arguments.Count);
        for (int index = 0; index < argumentsToTrack; index++)
        {
            TrackCollectionExpression(
                invocation.ArgumentList.Arguments[index].Expression,
                collectionAccesses,
                sourceParameterName,
                semanticModel);
        }

        return true;
    }

    private static void TrackSequenceEqualArgumentAccess(
        InvocationExpressionSyntax invocation,
        Dictionary<string, int> collectionAccesses,
        string? sourceParameterName,
        SemanticModel semanticModel)
    {
        TrackCollectionExpression(
            invocation.ArgumentList.Arguments[0].Expression,
            collectionAccesses,
            sourceParameterName,
            semanticModel);
    }

    private static void TrackCollectionExpression(
        ExpressionSyntax expression,
        Dictionary<string, int> collectionAccesses,
        string? sourceParameterName,
        SemanticModel semanticModel)
    {
        ExpressionSyntax collectionRoot = UnwrapChainedLinqReceiver(
            expression,
            sourceParameterName,
            semanticModel);

        string collectionName = TryGetSourceCollectionPath(collectionRoot, sourceParameterName, out string sourceCollectionPath)
            ? sourceCollectionPath
            : collectionRoot.ToString();

        if (!collectionAccesses.ContainsKey(collectionName))
        {
            collectionAccesses[collectionName] = 0;
        }

        collectionAccesses[collectionName]++;
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

    private static bool IsComplexLinqOperation(
        string containingType,
        string methodName,
        InvocationExpressionSyntax invocation)
    {
        if (containingType != "System.Linq.Enumerable" && containingType != "System.Linq.Queryable")
        {
            return false;
        }

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

    private static bool IsExternalMethodCall(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string? sourceParameterName)
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

        if (IsHttpControlOperation(containingType, methodSymbol.Name))
        {
            return false;
        }

        if (IsHttpHeaderConfigurationOperation(containingType, methodSymbol.Name))
        {
            return false;
        }

        if (IsCalledOnSourceExpression(memberAccess, sourceParameterName))
        {
            return false;
        }

        if (IsFastFrameworkHelperOperation(containingType, methodSymbol.Name, memberAccess.Expression, semanticModel))
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

    private static bool IsHttpControlOperation(string containingType, string methodName)
    {
        return (containingType == "System.Net.Http.HttpClient" &&
                methodName is "CancelPendingRequests" or "Dispose") ||
               (containingType == "System.Net.WebClient" &&
                methodName is "CancelAsync" or "Dispose") ||
               (containingType == "System.Net.Http.HttpMessageInvoker" &&
                methodName == "Dispose");
    }

    private static bool IsHttpHeaderConfigurationOperation(string containingType, string methodName)
    {
        return containingType.StartsWith("System.Net.Http.Headers.", StringComparison.Ordinal) &&
               methodName is "Add" or "Clear" or "Remove" or "TryAddWithoutValidation" or
                             "ParseAdd" or "TryParseAdd";
    }

    private static bool IsFastFrameworkHelperOperation(
        string containingType,
        string methodName,
        ExpressionSyntax receiverExpression,
        SemanticModel semanticModel)
    {
        return IsFastFrameworkComparerMethod(containingType, methodName) &&
               IsKnownFrameworkComparerReceiver(receiverExpression, semanticModel);
    }

    private static bool IsFastFrameworkComparerMethod(string containingType, string methodName)
    {
        return (containingType == "System.StringComparer" &&
                methodName is "Compare" or "Equals" or "GetHashCode") ||
               (containingType.StartsWith("System.Collections.Generic.EqualityComparer<", StringComparison.Ordinal) &&
                methodName is "Equals" or "GetHashCode") ||
               (containingType == "System.Collections.Generic.ReferenceEqualityComparer" &&
                methodName is "Equals" or "GetHashCode") ||
               (containingType.StartsWith("System.Collections.Generic.Comparer<", StringComparison.Ordinal) &&
                methodName == "Compare");
    }

    private static bool IsKnownFrameworkComparerReceiver(
        ExpressionSyntax receiverExpression,
        SemanticModel semanticModel)
    {
        receiverExpression = RemoveParentheses(receiverExpression);
        ISymbol? receiverSymbol = semanticModel.GetSymbolInfo(receiverExpression).Symbol;

        if (receiverSymbol is IPropertySymbol receiverProperty &&
            IsKnownFrameworkComparerProperty(receiverProperty))
        {
            return true;
        }

        if (receiverSymbol is IFieldSymbol { IsReadOnly: true } receiverField &&
            TryGetFieldInitializer(receiverField, out EqualsValueClauseSyntax? fieldInitializer))
        {
            return fieldInitializer is not null &&
                   IsKnownFrameworkComparerExpression(fieldInitializer.Value, semanticModel);
        }

        if (receiverSymbol is IPropertySymbol { SetMethod: null } fieldBackedProperty &&
            TryGetPropertyFrameworkComparerExpression(fieldBackedProperty, out ExpressionSyntax? propertyExpression))
        {
            return propertyExpression is not null &&
                   IsKnownFrameworkComparerExpression(propertyExpression, semanticModel);
        }

        return false;
    }

    private static bool IsKnownFrameworkComparerExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        expression = RemoveParentheses(expression);
        return semanticModel.GetSymbolInfo(expression).Symbol is IPropertySymbol property &&
               IsKnownFrameworkComparerProperty(property);
    }

    private static bool IsKnownFrameworkComparerProperty(IPropertySymbol property)
    {
        if (!property.IsStatic)
        {
            return false;
        }

        string containingType = property.ContainingType?.ToDisplayString() ?? "";
        return containingType == "System.StringComparer" &&
               property.Name is "Ordinal" or "OrdinalIgnoreCase" or
                                "InvariantCulture" or "InvariantCultureIgnoreCase" ||
               containingType.StartsWith("System.Collections.Generic.EqualityComparer<", StringComparison.Ordinal) &&
               property.Name == "Default" ||
               containingType == "System.Collections.Generic.ReferenceEqualityComparer" &&
               property.Name == "Instance" ||
               containingType.StartsWith("System.Collections.Generic.Comparer<", StringComparison.Ordinal) &&
               property.Name == "Default";
    }

    private static bool TryGetFieldInitializer(
        IFieldSymbol field,
        out EqualsValueClauseSyntax? initializer)
    {
        foreach (SyntaxReference syntaxReference in field.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is VariableDeclaratorSyntax { Initializer: not null } declarator)
            {
                initializer = declarator.Initializer;
                return true;
            }
        }

        initializer = null;
        return false;
    }

    private static bool TryGetPropertyFrameworkComparerExpression(
        IPropertySymbol property,
        out ExpressionSyntax? expression)
    {
        foreach (SyntaxReference syntaxReference in property.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is PropertyDeclarationSyntax propertyDeclaration)
            {
                if (propertyDeclaration.Initializer is not null)
                {
                    expression = propertyDeclaration.Initializer.Value;
                    return true;
                }

                if (propertyDeclaration.ExpressionBody is not null)
                {
                    expression = propertyDeclaration.ExpressionBody.Expression;
                    return true;
                }

                AccessorDeclarationSyntax? getAccessor = propertyDeclaration.AccessorList?.Accessors
                    .FirstOrDefault(accessor => accessor.Kind() == SyntaxKind.GetAccessorDeclaration);
                if (getAccessor?.ExpressionBody is not null)
                {
                    expression = getAccessor.ExpressionBody.Expression;
                    return true;
                }
            }
        }

        expression = null;
        return false;
    }

    private static ExpressionSyntax RemoveParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesizedExpression)
        {
            expression = parenthesizedExpression.Expression;
        }

        return expression;
    }

    private static bool IsCalledOnSourceExpression(
        MemberAccessExpressionSyntax memberAccess,
        string? sourceParameterName)
    {
        if (string.IsNullOrEmpty(sourceParameterName))
        {
            return false;
        }

        ExpressionSyntax currentExpression = memberAccess.Expression;
        while (currentExpression is MemberAccessExpressionSyntax currentMemberAccess)
        {
            currentExpression = currentMemberAccess.Expression;
        }

        return currentExpression is IdentifierNameSyntax identifier &&
               string.Equals(identifier.Identifier.ValueText, sourceParameterName, StringComparison.Ordinal);
    }

    private static bool IsExpensiveComputation(
        LambdaExpressionSyntax lambda,
        SemanticModel semanticModel)
    {
        // Look for Enumerable.Range with complex predicates (like prime checking)
        var invocations = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (InvocationExpressionSyntax? invocation in invocations)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.Text == "Range" &&
                semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol methodSymbol &&
                methodSymbol.ContainingType?.ToDisplayString() == "System.Linq.Enumerable")
            {
                // Found System.Linq.Enumerable.Range - this might be expensive
                return true;
            }
        }

        return false;
    }

}

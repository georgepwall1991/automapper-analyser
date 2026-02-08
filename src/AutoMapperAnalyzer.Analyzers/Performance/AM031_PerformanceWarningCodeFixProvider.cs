using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Analyzers.Performance;

/// <summary>
///     Code fix provider for AM031 Performance Warning diagnostics.
///     Prefers safe, executable fixes over cross-file speculative rewrites.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM031_PerformanceWarningCodeFixProvider))]
[Shared]
public class AM031_PerformanceWarningCodeFixProvider : AutoMapperCodeFixProviderBase
{
    private const string IssueTypePropertyName = "IssueType";
    private const string PropertyNamePropertyName = "PropertyName";

    private const string MultipleEnumerationIssueType = "MultipleEnumeration";
    private const string TaskResultIssueType = "TaskResult";
    private const string NonDeterministicIssueType = "NonDeterministic";
    private const string ExpensiveComputationIssueType = "ExpensiveComputation";
    private const string ComplexLinqIssueType = "ComplexLinq";
    private const string ExpensiveOperationIssueType = "ExpensiveOperation";

    /// <summary>
    ///     Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM031");

    /// <summary>
    ///     Registers code fixes for the diagnostics.
    /// </summary>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        SyntaxTree documentTree = operationContext.Root.SyntaxTree;
        Diagnostic? diagnostic = context.Diagnostics.FirstOrDefault(diag =>
            diag.Id == "AM031" &&
            diag.Location.IsInSource &&
            diag.Location.SourceTree == documentTree &&
            diag.Location.SourceSpan.IntersectsWith(context.Span));

        if (diagnostic == null)
        {
            return;
        }

        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        LambdaExpressionSyntax? lambda = operationContext.Root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf()
            .OfType<LambdaExpressionSyntax>()
            .FirstOrDefault();

        if (lambda == null)
        {
            return;
        }

        InvocationExpressionSyntax? forMemberInvocation = lambda.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv => inv.Expression is MemberAccessExpressionSyntax mae &&
                                   mae.Name.Identifier.Text == "ForMember");

        if (forMemberInvocation == null)
        {
            return;
        }

        string? propertyName = GetPropertyName(diagnostic);
        if (string.IsNullOrEmpty(propertyName))
        {
            return;
        }

        string issueType = diagnostic.Properties.TryGetValue(IssueTypePropertyName, out string? storedIssueType)
            ? storedIssueType ?? string.Empty
            : string.Empty;

        if (string.IsNullOrEmpty(issueType))
        {
            issueType = InferIssueTypeFromDescriptor(diagnostic.Descriptor);
        }

        // Keep targeted caching fix for multiple-enumeration diagnostics.
        if (issueType == MultipleEnumerationIssueType)
        {
            RegisterMultipleEnumerationFix(context, operationContext.Root, lambda, diagnostic);
        }

        RegisterIgnoreMappingFix(context, operationContext.Root, forMemberInvocation, propertyName!, diagnostic);

        if (await CanUseConventionMappingAsync(context.Document, forMemberInvocation, propertyName!, context.CancellationToken))
        {
            RegisterRemoveForMemberFix(context, operationContext.Root, forMemberInvocation, propertyName!, diagnostic);
        }
    }

    private void RegisterMultipleEnumerationFix(
        CodeFixContext context,
        SyntaxNode root,
        LambdaExpressionSyntax lambda,
        Diagnostic diagnostic)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                "Cache collection with ToList() to avoid multiple enumerations",
                cancellationToken => AddCollectionCaching(context.Document, root, lambda, cancellationToken),
                "AM031_CacheCollection"),
            diagnostic);
    }

    private void RegisterIgnoreMappingFix(
        CodeFixContext context,
        SyntaxNode root,
        InvocationExpressionSyntax forMemberInvocation,
        string propertyName,
        Diagnostic diagnostic)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                $"Ignore mapping for '{propertyName}'",
                cancellationToken => ReplaceWithIgnoreAsync(
                    context.Document,
                    root,
                    forMemberInvocation,
                    propertyName,
                    cancellationToken),
                $"AM031_Ignore_{propertyName}"),
            diagnostic);
    }

    private void RegisterRemoveForMemberFix(
        CodeFixContext context,
        SyntaxNode root,
        InvocationExpressionSyntax forMemberInvocation,
        string propertyName,
        Diagnostic diagnostic)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                $"Remove redundant ForMember for '{propertyName}'",
                cancellationToken => RemoveForMemberAsync(context.Document, root, forMemberInvocation, cancellationToken),
                $"AM031_RemoveForMember_{propertyName}"),
            diagnostic);
    }

    private async Task<bool> CanUseConventionMappingAsync(
        Document document,
        InvocationExpressionSyntax forMemberInvocation,
        string propertyName,
        CancellationToken cancellationToken)
    {
        SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel == null)
        {
            return false;
        }

        InvocationExpressionSyntax? createMapInvocation = forMemberInvocation.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv => MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(inv, semanticModel, "CreateMap"));

        if (createMapInvocation == null)
        {
            return false;
        }

        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) =
            MappingChainAnalysisHelper.GetCreateMapTypeArguments(createMapInvocation, semanticModel);
        if (sourceType == null || destinationType == null)
        {
            return false;
        }

        IPropertySymbol? sourceProperty = AutoMapperAnalysisHelpers
            .GetMappableProperties(sourceType, requireSetter: false)
            .FirstOrDefault(prop => string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase));

        IPropertySymbol? destinationProperty = AutoMapperAnalysisHelpers
            .GetMappableProperties(destinationType, requireSetter: true)
            .FirstOrDefault(prop => string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase));

        if (sourceProperty == null || destinationProperty == null)
        {
            return false;
        }

        return AutoMapperAnalysisHelpers.AreTypesCompatible(sourceProperty.Type, destinationProperty.Type);
    }

    private Task<Document> ReplaceWithIgnoreAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax forMemberInvocation,
        string propertyName,
        CancellationToken cancellationToken)
    {
        InvocationExpressionSyntax? previousInvocation = GetInvocationWithoutForMember(forMemberInvocation);
        if (previousInvocation == null)
        {
            return Task.FromResult(document);
        }

        InvocationExpressionSyntax ignoreInvocation =
            CodeFixSyntaxHelper.CreateForMemberWithIgnore(previousInvocation, propertyName)
                .WithTriviaFrom(forMemberInvocation);

        SyntaxNode newRoot = root.ReplaceNode(forMemberInvocation, ignoreInvocation);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private Task<Document> RemoveForMemberAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax forMemberInvocation,
        CancellationToken cancellationToken)
    {
        InvocationExpressionSyntax? previousInvocation = GetInvocationWithoutForMember(forMemberInvocation);
        if (previousInvocation == null)
        {
            return Task.FromResult(document);
        }

        SyntaxNode newRoot = root.ReplaceNode(forMemberInvocation, previousInvocation.WithTriviaFrom(forMemberInvocation));
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private Task<Document> AddCollectionCaching(
        Document document,
        SyntaxNode root,
        LambdaExpressionSyntax lambda,
        CancellationToken cancellationToken)
    {
        string? collectionName = ExtractCollectionNameFromLambda(lambda);
        if (string.IsNullOrEmpty(collectionName))
        {
            return Task.FromResult(document);
        }

        string safeCollectionName = collectionName!;
        string cachedVariableName = $"{safeCollectionName.ToLowerInvariant()}Cache";

        BlockSyntax newLambdaBody = SyntaxFactory.Block(
            SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.IdentifierName("var"))
                    .WithVariables(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.VariableDeclarator(
                                    SyntaxFactory.Identifier(cachedVariableName))
                                .WithInitializer(
                                    SyntaxFactory.EqualsValueClause(
                                        SyntaxFactory.ParseExpression($"src.{safeCollectionName}.ToList()")))))),
            SyntaxFactory.ReturnStatement(
                ReplaceCollectionReferencesWithCache(lambda.Body as ExpressionSyntax, safeCollectionName,
                    cachedVariableName)));

        SimpleLambdaExpressionSyntax newLambda;
        if (lambda is SimpleLambdaExpressionSyntax simpleLambda)
        {
            newLambda = simpleLambda.WithBody(newLambdaBody);
        }
        else
        {
            newLambda = SyntaxFactory.SimpleLambdaExpression(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("src")),
                newLambdaBody);
        }

        SyntaxNode newRoot = root.ReplaceNode(lambda, newLambda);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static InvocationExpressionSyntax? GetInvocationWithoutForMember(InvocationExpressionSyntax forMemberToRemove)
    {
        return forMemberToRemove.Expression is MemberAccessExpressionSyntax
            {
                Expression: InvocationExpressionSyntax previousInvocation
            }
            ? previousInvocation
            : null;
    }

    private string? ExtractCollectionNameFromLambda(LambdaExpressionSyntax lambda)
    {
        var memberAccesses = lambda.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(ma => ma.Expression.ToString().StartsWith("src."))
            .ToList();

        if (memberAccesses.Any())
        {
            return memberAccesses.First().Name.Identifier.Text;
        }

        return null;
    }

    private ExpressionSyntax ReplaceCollectionReferencesWithCache(
        ExpressionSyntax? expression,
        string collectionName,
        string cachedVariableName)
    {
        if (expression == null)
        {
            return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0));
        }

        string expressionString = expression.ToString().Replace($"src.{collectionName}", cachedVariableName);
        return SyntaxFactory.ParseExpression(expressionString);
    }

    private string? GetPropertyName(Diagnostic diagnostic)
    {
        if (diagnostic.Properties.TryGetValue(PropertyNamePropertyName, out string? propertyName) &&
            !string.IsNullOrEmpty(propertyName))
        {
            return propertyName;
        }

        return null;
    }

    private static string InferIssueTypeFromDescriptor(DiagnosticDescriptor descriptor)
    {
        if (descriptor == AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule)
        {
            return MultipleEnumerationIssueType;
        }

        if (descriptor == AM031_PerformanceWarningAnalyzer.TaskResultSynchronousAccessRule)
        {
            return TaskResultIssueType;
        }

        if (descriptor == AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule)
        {
            return NonDeterministicIssueType;
        }

        if (descriptor == AM031_PerformanceWarningAnalyzer.ExpensiveComputationRule)
        {
            return ExpensiveComputationIssueType;
        }

        if (descriptor == AM031_PerformanceWarningAnalyzer.ComplexLinqOperationRule)
        {
            return ComplexLinqIssueType;
        }

        if (descriptor == AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule)
        {
            return ExpensiveOperationIssueType;
        }

        return string.Empty;
    }
}

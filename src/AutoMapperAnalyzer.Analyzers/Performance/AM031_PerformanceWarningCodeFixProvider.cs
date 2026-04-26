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
    private const string CollectionNamePropertyName = "CollectionName";

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
        ImmutableArray<Diagnostic> relatedDiagnostics = context.Diagnostics
            .Where(diag =>
            diag.Id == "AM031" &&
            diag.Location.IsInSource &&
            diag.Location.SourceTree == documentTree &&
            diag.Location.SourceSpan.IntersectsWith(context.Span))
            .ToImmutableArray();

        if (relatedDiagnostics.IsDefaultOrEmpty)
        {
            return;
        }

        Diagnostic diagnostic = relatedDiagnostics[0];
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

        ImmutableHashSet<string> issueTypes = relatedDiagnostics
            .Select(GetIssueType)
            .Where(issueType => !string.IsNullOrEmpty(issueType))
            .ToImmutableHashSet(StringComparer.Ordinal);

        // Keep targeted caching fix for multiple-enumeration diagnostics.
        if (issueTypes.Contains(MultipleEnumerationIssueType))
        {
            string? collectionName = relatedDiagnostics
                .Select(GetCollectionName)
                .FirstOrDefault(name => !string.IsNullOrEmpty(name));

            if (!string.IsNullOrEmpty(collectionName))
            {
                RegisterMultipleEnumerationFix(context, operationContext.Root, lambda, propertyName!, collectionName!,
                    relatedDiagnostics);
            }
        }

        RegisterIgnoreMappingFix(context, operationContext.Root, forMemberInvocation, propertyName!, relatedDiagnostics);

        if (await CanUseConventionMappingAsync(context.Document, forMemberInvocation, propertyName!, context.CancellationToken))
        {
            RegisterRemoveForMemberFix(context, operationContext.Root, forMemberInvocation, propertyName!, relatedDiagnostics);
        }
    }

    private void RegisterMultipleEnumerationFix(
        CodeFixContext context,
        SyntaxNode root,
        LambdaExpressionSyntax lambda,
        string propertyName,
        string collectionName,
        ImmutableArray<Diagnostic> diagnostics)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                $"Cache '{collectionName}' with ToList() for '{propertyName}'",
                cancellationToken => AddCollectionCaching(context.Document, root, lambda, collectionName, cancellationToken),
                $"AM031_CacheCollection_{propertyName}_{collectionName}"),
            diagnostics);
    }

    private void RegisterIgnoreMappingFix(
        CodeFixContext context,
        SyntaxNode root,
        InvocationExpressionSyntax forMemberInvocation,
        string propertyName,
        ImmutableArray<Diagnostic> diagnostics)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                $"Ignore mapping for '{propertyName}' (manual review)",
                cancellationToken => ReplaceWithIgnoreAsync(
                    context.Document,
                    root,
                    forMemberInvocation,
                    propertyName,
                    cancellationToken),
                $"AM031_Ignore_{propertyName}"),
            diagnostics);
    }

    private void RegisterRemoveForMemberFix(
        CodeFixContext context,
        SyntaxNode root,
        InvocationExpressionSyntax forMemberInvocation,
        string propertyName,
        ImmutableArray<Diagnostic> diagnostics)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                $"Remove redundant ForMember for '{propertyName}'",
                cancellationToken => RemoveForMemberAsync(context.Document, root, forMemberInvocation, cancellationToken),
                $"AM031_RemoveForMember_{propertyName}"),
            diagnostics);
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
        string collectionName,
        CancellationToken cancellationToken)
    {
        if (lambda.Body is not ExpressionSyntax lambdaExpression)
        {
            return Task.FromResult(document);
        }

        string? parameterName = GetLambdaParameterName(lambda);
        if (string.IsNullOrEmpty(parameterName))
        {
            return Task.FromResult(document);
        }

        string cachedVariableName = CreateUniqueCacheVariableName(lambda, collectionName);
        ExpressionSyntax cachedCollectionExpression = CreateCachedCollectionExpression(parameterName!, collectionName);
        ExpressionSyntax updatedLambdaExpression =
            ReplaceCollectionReferences(lambdaExpression, parameterName!, collectionName, cachedVariableName);

        BlockSyntax newLambdaBody = SyntaxFactory.Block(
            SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.IdentifierName("var"))
                    .WithVariables(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.VariableDeclarator(
                                SyntaxFactory.Identifier(cachedVariableName))
                                .WithInitializer(
                                    SyntaxFactory.EqualsValueClause(cachedCollectionExpression))))),
            SyntaxFactory.ReturnStatement(updatedLambdaExpression));

        LambdaExpressionSyntax newLambda = lambda switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.WithBody(newLambdaBody),
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda => parenthesizedLambda.WithBody(newLambdaBody),
            _ => lambda
        };

        SyntaxNode newRoot = root.ReplaceNode(lambda, newLambda);

        if (newRoot is CompilationUnitSyntax compilationUnit)
        {
            newRoot = AddUsingIfMissing(compilationUnit, "System.Linq");
        }

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

    private static string? GetLambdaParameterName(LambdaExpressionSyntax lambda)
    {
        return lambda switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda when parenthesizedLambda.ParameterList.Parameters.Count == 1
                => parenthesizedLambda.ParameterList.Parameters[0].Identifier.ValueText,
            _ => null
        };
    }

    private static string CreateUniqueCacheVariableName(LambdaExpressionSyntax lambda, string collectionName)
    {
        string baseName = string.IsNullOrEmpty(collectionName)
            ? "cachedCollection"
            : char.ToLowerInvariant(collectionName[0]) + collectionName.Substring(1) + "Cache";
        var usedNames = lambda.DescendantTokens()
            .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
            .Select(token => token.ValueText)
            .ToImmutableHashSet(StringComparer.Ordinal);

        if (!usedNames.Contains(baseName))
        {
            return baseName;
        }

        int suffix = 2;
        string candidate = $"{baseName}{suffix}";
        while (usedNames.Contains(candidate))
        {
            suffix++;
            candidate = $"{baseName}{suffix}";
        }

        return candidate;
    }

    private static ExpressionSyntax CreateCachedCollectionExpression(string parameterName, string collectionName)
    {
        MemberAccessExpressionSyntax sourceCollection = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.IdentifierName(parameterName),
            SyntaxFactory.IdentifierName(collectionName));

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                sourceCollection,
                SyntaxFactory.IdentifierName("ToList")),
            SyntaxFactory.ArgumentList());
    }

    private static ExpressionSyntax ReplaceCollectionReferences(
        ExpressionSyntax expression,
        string parameterName,
        string collectionName,
        string cachedVariableName)
    {
        return expression.ReplaceNodes(
            expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>()
                .Where(memberAccess => IsTargetCollectionAccess(memberAccess, parameterName, collectionName)),
            (originalNode, _) => SyntaxFactory.IdentifierName(cachedVariableName).WithTriviaFrom(originalNode));
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

    private static string GetIssueType(Diagnostic diagnostic)
    {
        if (diagnostic.Properties.TryGetValue(IssueTypePropertyName, out string? storedIssueType) &&
            !string.IsNullOrEmpty(storedIssueType))
        {
            return storedIssueType!;
        }

        return InferIssueTypeFromDescriptor(diagnostic.Descriptor);
    }

    private static string? GetCollectionName(Diagnostic diagnostic)
    {
        if (diagnostic.Properties.TryGetValue(CollectionNamePropertyName, out string? collectionName) &&
            !string.IsNullOrEmpty(collectionName))
        {
            return collectionName;
        }

        return null;
    }

    private static bool IsTargetCollectionAccess(
        MemberAccessExpressionSyntax memberAccess,
        string parameterName,
        string collectionName)
    {
        return memberAccess.Expression is IdentifierNameSyntax identifier &&
               string.Equals(identifier.Identifier.ValueText, parameterName, StringComparison.Ordinal) &&
               string.Equals(memberAccess.Name.Identifier.ValueText, collectionName, StringComparison.Ordinal);
    }

    private static CompilationUnitSyntax AddUsingIfMissing(CompilationUnitSyntax root, string namespaceName)
    {
        if (root.Usings.Any(u =>
                u.Name != null &&
                string.Equals(u.Name.ToString(), namespaceName, StringComparison.Ordinal)))
        {
            return root;
        }

        UsingDirectiveSyntax usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName))
            .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed);

        return root.AddUsings(usingDirective);
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

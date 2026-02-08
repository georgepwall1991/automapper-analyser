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
///     Provides fixes for expensive operations in mapping expressions.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM031_PerformanceWarningCodeFixProvider))]
[Shared]
public class AM031_PerformanceWarningCodeFixProvider : AutoMapperCodeFixProviderBase
{
    private const string IssueTypePropertyName = "IssueType";
    private const string PropertyNamePropertyName = "PropertyName";
    private const string OperationTypePropertyName = "OperationType";

    private const string ExpensiveOperationIssueType = "ExpensiveOperation";
    private const string MultipleEnumerationIssueType = "MultipleEnumeration";
    private const string ExpensiveComputationIssueType = "ExpensiveComputation";
    private const string TaskResultIssueType = "TaskResult";
    private const string ComplexLinqIssueType = "ComplexLinq";
    private const string NonDeterministicIssueType = "NonDeterministic";

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

        string safePropertyName = propertyName!;
        string issueType = diagnostic.Properties.TryGetValue(IssueTypePropertyName, out string? storedIssueType)
            ? storedIssueType ?? string.Empty
            : string.Empty;

        switch (issueType)
        {
            case ExpensiveOperationIssueType:
            case ExpensiveComputationIssueType:
            case ComplexLinqIssueType:
                RegisterExpensiveOperationFix(context, forMemberInvocation, safePropertyName, diagnostic);
                break;
            case MultipleEnumerationIssueType:
                RegisterMultipleEnumerationFix(context, operationContext.Root, lambda, diagnostic);
                break;
            case TaskResultIssueType:
                RegisterTaskResultFix(context, forMemberInvocation, safePropertyName, diagnostic);
                break;
            case NonDeterministicIssueType:
                RegisterNonDeterministicOperationFix(context, forMemberInvocation, safePropertyName, diagnostic);
                break;
        }
    }

    private void RegisterExpensiveOperationFix(
        CodeFixContext context,
        InvocationExpressionSyntax forMemberInvocation,
        string propertyName,
        Diagnostic diagnostic)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                $"Move computation to source property '{propertyName}'",
                cancellationToken =>
                    MoveComputationToSourcePropertyAsync(
                        context.Document,
                        forMemberInvocation,
                        propertyName,
                        "TODO: Populate this property before mapping",
                        cancellationToken),
                $"AM031_MoveToSource_{propertyName}"),
            diagnostic);
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
                cancellationToken =>
                    AddCollectionCaching(context.Document, root, lambda, cancellationToken),
                "AM031_CacheCollection"),
            diagnostic);
    }

    private void RegisterTaskResultFix(
        CodeFixContext context,
        InvocationExpressionSyntax forMemberInvocation,
        string propertyName,
        Diagnostic diagnostic)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                $"Move async operation to source property '{propertyName}'",
                cancellationToken =>
                    MoveComputationToSourcePropertyAsync(
                        context.Document,
                        forMemberInvocation,
                        propertyName,
                        "TODO: Await async operation before mapping",
                        cancellationToken),
                $"AM031_MoveAsync_{propertyName}"),
            diagnostic);
    }

    private void RegisterNonDeterministicOperationFix(
        CodeFixContext context,
        InvocationExpressionSyntax forMemberInvocation,
        string propertyName,
        Diagnostic diagnostic)
    {
        string operationType = ExtractOperationTypeFromDiagnostic(diagnostic);

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Move {operationType} calculation to source property '{propertyName}'",
                cancellationToken =>
                    MoveComputationToSourcePropertyAsync(
                        context.Document,
                        forMemberInvocation,
                        propertyName,
                        $"TODO: Calculate before mapping using {operationType}",
                        cancellationToken),
                $"AM031_MoveNonDeterministic_{propertyName}"),
            diagnostic);
    }

    private async Task<Solution> MoveComputationToSourcePropertyAsync(
        Document document,
        InvocationExpressionSyntax forMemberInvocation,
        string propertyName,
        string todoComment,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document.Project.Solution;

        // Find the CreateMap invocation
        InvocationExpressionSyntax? createMapInvocation = forMemberInvocation.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv =>
            {
                if (inv.Expression is GenericNameSyntax genericName)
                {
                    return genericName.Identifier.Text == "CreateMap";
                }

                if (inv.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    return memberAccess.Name.Identifier.Text == "CreateMap";
                }

                return false;
            });

        if (createMapInvocation == null)
        {
            return document.Project.Solution;
        }
        
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel == null) return document.Project.Solution;

        // Find the Source/Destination types via CreateMap<TSource, TDestination>
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(createMapInvocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol || methodSymbol.TypeArguments.Length < 2)
        {
            return document.Project.Solution;
        }

        ITypeSymbol sourceType = methodSymbol.TypeArguments[0];
        ITypeSymbol destinationType = methodSymbol.TypeArguments[1];

        // Get Syntax Reference for Source Class
        var syntaxRef = sourceType.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null) return document.Project.Solution;

        string propertyTypeName = GetDestinationPropertyTypeName(destinationType, propertyName);

        Document? sourceDocument = document.Project.Solution.GetDocument(syntaxRef.SyntaxTree);
        if (sourceDocument == null)
        {
            return document.Project.Solution;
        }

        // Determine if Source Class is in the same document as the diagnostic.
        bool sameDocument = sourceDocument.Id == document.Id;

        InvocationExpressionSyntax? updatedMappingInvocation = GetInvocationWithoutForMember(forMemberInvocation);
        if (updatedMappingInvocation == null)
        {
            return document.Project.Solution;
        }

        if (sameDocument)
        {
            var sourceClass = root.FindNode(syntaxRef.Span) as ClassDeclarationSyntax;
            if (sourceClass == null) return document.Project.Solution;

            ClassDeclarationSyntax newSourceClass = AddPropertyIfMissing(
                sourceClass,
                propertyName,
                propertyTypeName,
                todoComment);

            SyntaxNode newRoot = root.ReplaceNodes(
                new SyntaxNode[] { sourceClass, forMemberInvocation },
                (original, _) =>
                {
                    if (original == sourceClass) return newSourceClass;
                    if (original == forMemberInvocation) return updatedMappingInvocation;
                    return original;
                });

            return document.Project.Solution.WithDocumentSyntaxRoot(document.Id, newRoot);
        }
        else
        {
            // Multi-document change

            // 1. Update Source Document
            var sourceRoot = await syntaxRef.SyntaxTree.GetRootAsync(cancellationToken);
            var sourceClass = sourceRoot.FindNode(syntaxRef.Span) as ClassDeclarationSyntax;
            if (sourceClass == null) return document.Project.Solution; // Should check if editable

            ClassDeclarationSyntax newSourceClass = AddPropertyIfMissing(
                sourceClass,
                propertyName,
                propertyTypeName,
                todoComment);
            SyntaxNode newSourceRoot = sourceRoot.ReplaceNode(sourceClass, newSourceClass);

            // 2. Update Profile Document
            SyntaxNode newProfileRoot = root.ReplaceNode(forMemberInvocation, updatedMappingInvocation);

            // Apply changes
            var solution = document.Project.Solution;
            solution = solution.WithDocumentSyntaxRoot(sourceDocument.Id, newSourceRoot);
            solution = solution.WithDocumentSyntaxRoot(document.Id, newProfileRoot);

            return solution;
        }
    }

    private Task<Document> AddCollectionCaching(
        Document document,
        SyntaxNode root,
        LambdaExpressionSyntax lambda,
        CancellationToken cancellationToken)
    {
        // Find the collection name that's being enumerated multiple times
        string? collectionName = ExtractCollectionNameFromLambda(lambda);
        if (string.IsNullOrEmpty(collectionName))
        {
            return Task.FromResult(document);
        }

        // After null check, we know collectionName is not null
        string safeCollectionName = collectionName!;

        // Create a new lambda body with caching
        string cachedVariableName = $"{safeCollectionName.ToLowerInvariant()}Cache";

        // Create the new lambda with block body
        BlockSyntax newLambdaBody = SyntaxFactory.Block(
            // var collectionCache = src.Collection.ToList();
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
            // return cachedVariable.Sum() + cachedVariable.Average();
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
            // For ParenthesizedLambdaExpressionSyntax, create a simple lambda
            newLambda = SyntaxFactory.SimpleLambdaExpression(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("src")),
                newLambdaBody);
        }

        // Replace the lambda in the tree
        SyntaxNode newRoot = root.ReplaceNode(lambda, newLambda);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static ClassDeclarationSyntax AddPropertyIfMissing(
        ClassDeclarationSyntax sourceClass,
        string propertyName,
        string propertyTypeName,
        string comment)
    {
        bool hasProperty = sourceClass.Members
            .OfType<PropertyDeclarationSyntax>()
            .Any(property => string.Equals(property.Identifier.ValueText, propertyName, StringComparison.OrdinalIgnoreCase));

        if (hasProperty)
        {
            return sourceClass;
        }

        PropertyDeclarationSyntax newProperty = CreatePropertyWithComment(propertyTypeName, propertyName, comment);
        return sourceClass.AddMembers(newProperty);
    }

    private static PropertyDeclarationSyntax CreatePropertyWithComment(string propertyTypeName, string propertyName, string comment)
    {
        return SyntaxFactory.PropertyDeclaration(
                SyntaxFactory.ParseTypeName(propertyTypeName),
                SyntaxFactory.Identifier(propertyName))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithAccessorList(
                SyntaxFactory.AccessorList(
                    SyntaxFactory.List(new[]
                    {
                        SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                        SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                    })))
            .WithTrailingTrivia(
                SyntaxFactory.Comment($" // {comment}"));
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
        // Look for src.CollectionName patterns
        var memberAccesses = lambda.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(ma => ma.Expression.ToString().StartsWith("src."))
            .ToList();

        if (memberAccesses.Any())
        {
            string first = memberAccesses.First().Name.Identifier.Text;
            return first;
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

        // Replace all occurrences of src.CollectionName with cachedVariableName
        string expressionString = expression.ToString().Replace($"src.{collectionName}", cachedVariableName);
        return SyntaxFactory.ParseExpression(expressionString);
    }

    private string GetDestinationPropertyTypeName(ITypeSymbol destinationType, string propertyName)
    {
        IPropertySymbol? destinationProperty = AutoMapperAnalysisHelpers
            .GetMappableProperties(destinationType, false)
            .FirstOrDefault(property => string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase));

        if (destinationProperty == null)
        {
            return "string";
        }

        return destinationProperty.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
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

    private string ExtractOperationTypeFromDiagnostic(Diagnostic diagnostic)
    {
        if (diagnostic.Properties.TryGetValue(OperationTypePropertyName, out string? operationType) &&
            !string.IsNullOrEmpty(operationType))
        {
            return operationType!;
        }

        string message = diagnostic.GetMessage();
        if (message.Contains("DateTime.Now"))
        {
            return "DateTime.Now";
        }

        if (message.Contains("Random"))
        {
            return "Random";
        }

        if (message.Contains("Guid.NewGuid"))
        {
            return "Guid.NewGuid";
        }

        return "non-deterministic operation";
    }
}

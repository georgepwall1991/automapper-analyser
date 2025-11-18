using System.Collections.Immutable;
using System.Composition;
using System.Text.RegularExpressions;
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
public class AM031_PerformanceWarningCodeFixProvider : CodeFixProvider
{
    /// <summary>
    ///     Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM031");

    /// <summary>
    ///     Gets whether this provider can fix multiple diagnostics in a single code action.
    /// </summary>
    public sealed override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <summary>
    ///     Registers code fixes for the diagnostics.
    /// </summary>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return;
        }

        Diagnostic diagnostic = context.Diagnostics.First();
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the lambda expression that triggered the diagnostic
        LambdaExpressionSyntax? lambda = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf()
            .OfType<LambdaExpressionSyntax>()
            .FirstOrDefault();

        if (lambda == null)
        {
            return;
        }

        // Find the ForMember invocation
        InvocationExpressionSyntax? forMemberInvocation = lambda.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv => inv.Expression is MemberAccessExpressionSyntax mae &&
                                   mae.Name.Identifier.Text == "ForMember");

        if (forMemberInvocation == null)
        {
            return;
        }

        // Extract property name from diagnostic message
        string? propertyName = ExtractPropertyNameFromDiagnostic(diagnostic);
        if (string.IsNullOrEmpty(propertyName))
        {
            return;
        }

        SemanticModel? semanticModel =
            await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel == null)
        {
            return;
        }

        // After null check, we know propertyName is not null
        string safePropertyName = propertyName!;

        // Register fixes based on diagnostic descriptor
        DiagnosticDescriptor descriptor = diagnostic.Descriptor;

        if (descriptor == AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule)
        {
            RegisterExpensiveOperationFix(context, root, forMemberInvocation, safePropertyName, diagnostic,
                semanticModel);
        }
        else if (descriptor == AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule)
        {
            RegisterMultipleEnumerationFix(context, root, forMemberInvocation, lambda, diagnostic);
        }
        else if (descriptor == AM031_PerformanceWarningAnalyzer.TaskResultSynchronousAccessRule)
        {
            RegisterTaskResultFix(context, root, forMemberInvocation, safePropertyName, diagnostic, semanticModel);
        }
        else if (descriptor == AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule)
        {
            RegisterNonDeterministicOperationFix(context, root, forMemberInvocation, safePropertyName, diagnostic,
                semanticModel);
        }
    }

    private void RegisterExpensiveOperationFix(
        CodeFixContext context,
        SyntaxNode root,
        InvocationExpressionSyntax forMemberInvocation,
        string propertyName,
        Diagnostic diagnostic,
        SemanticModel semanticModel)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                $"Move computation to source property '{propertyName}'",
                cancellationToken =>
                    MoveComputationToSourceProperty(
                        context.Document,
                        root,
                        forMemberInvocation,
                        propertyName,
                        "TODO: Populate this property before mapping",
                        semanticModel,
                        cancellationToken),
                $"AM031_MoveToSource_{propertyName}"),
            diagnostic);
    }

    private void RegisterMultipleEnumerationFix(
        CodeFixContext context,
        SyntaxNode root,
        InvocationExpressionSyntax forMemberInvocation,
        LambdaExpressionSyntax lambda,
        Diagnostic diagnostic)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                "Cache collection with ToList() to avoid multiple enumerations",
                cancellationToken =>
                    AddCollectionCaching(context.Document, root, lambda, forMemberInvocation, cancellationToken),
                "AM031_CacheCollection"),
            diagnostic);
    }

    private void RegisterTaskResultFix(
        CodeFixContext context,
        SyntaxNode root,
        InvocationExpressionSyntax forMemberInvocation,
        string propertyName,
        Diagnostic diagnostic,
        SemanticModel semanticModel)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                $"Move async operation to source property '{propertyName}'",
                cancellationToken =>
                    MoveComputationToSourceProperty(
                        context.Document,
                        root,
                        forMemberInvocation,
                        propertyName,
                        "TODO: Await async operation before mapping",
                        semanticModel,
                        cancellationToken),
                $"AM031_MoveAsync_{propertyName}"),
            diagnostic);
    }

    private void RegisterNonDeterministicOperationFix(
        CodeFixContext context,
        SyntaxNode root,
        InvocationExpressionSyntax forMemberInvocation,
        string propertyName,
        Diagnostic diagnostic,
        SemanticModel semanticModel)
    {
        string operationType = ExtractOperationTypeFromDiagnostic(diagnostic);

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Move {operationType} calculation to source property '{propertyName}'",
                cancellationToken =>
                    MoveComputationToSourceProperty(
                        context.Document,
                        root,
                        forMemberInvocation,
                        propertyName,
                        $"TODO: Calculate before mapping using {operationType}",
                        semanticModel,
                        cancellationToken),
                $"AM031_MoveNonDeterministic_{propertyName}"),
            diagnostic);
    }

    private Task<Document> MoveComputationToSourceProperty(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax forMemberInvocation,
        string propertyName,
        string todoComment,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
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
            return Task.FromResult(document);
        }

        // Find the Source class
        ClassDeclarationSyntax? sourceClass = FindSourceClass(root, createMapInvocation, semanticModel);
        if (sourceClass == null)
        {
            return Task.FromResult(document);
        }

        // Add property to source class with TODO comment
        PropertyDeclarationSyntax newProperty = CreatePropertyWithComment(propertyName, todoComment);
        ClassDeclarationSyntax newSourceClass = sourceClass.AddMembers(newProperty);

        // Remove the ForMember call by finding the chain and removing this link
        InvocationExpressionSyntax newCreateMapInvocation =
            RemoveForMemberFromChain(createMapInvocation, forMemberInvocation);

        // Replace both nodes
        SyntaxNode newRoot = root.ReplaceNodes(
            new SyntaxNode[] { sourceClass, createMapInvocation },
            (original, _) =>
            {
                if (original == sourceClass)
                {
                    return newSourceClass;
                }

                if (original == createMapInvocation)
                {
                    return newCreateMapInvocation;
                }

                return original;
            });

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private Task<Document> AddCollectionCaching(
        Document document,
        SyntaxNode root,
        LambdaExpressionSyntax lambda,
        InvocationExpressionSyntax forMemberInvocation,
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

    private ClassDeclarationSyntax? FindSourceClass(SyntaxNode root, InvocationExpressionSyntax createMapInvocation,
        SemanticModel semanticModel)
    {
        // Get the source type from CreateMap<TSource, TDest>()
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(createMapInvocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol || methodSymbol.TypeArguments.Length < 1)
        {
            return null;
        }

        ITypeSymbol sourceType = methodSymbol.TypeArguments[0];
        string sourceTypeName = sourceType.Name;

        // Find the class declaration
        return root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == sourceTypeName);
    }

    private PropertyDeclarationSyntax CreatePropertyWithComment(string propertyName, string comment)
    {
        return SyntaxFactory.PropertyDeclaration(
                SyntaxFactory.IdentifierName("string"),
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

    private InvocationExpressionSyntax RemoveForMemberFromChain(
        InvocationExpressionSyntax createMapInvocation,
        InvocationExpressionSyntax forMemberToRemove)
    {
        // If the ForMember is directly on CreateMap, just return CreateMap
        if (createMapInvocation ==
            forMemberToRemove.DescendantNodes().OfType<InvocationExpressionSyntax>().FirstOrDefault())
        {
            return createMapInvocation;
        }

        // Navigate through the chain and find the node to remove
        InvocationExpressionSyntax? current = createMapInvocation;
        InvocationExpressionSyntax? previous = null;

        while (current != null)
        {
            if (current == forMemberToRemove)
            {
                // Found it - return the previous node (or CreateMap if this is the first ForMember)
                return previous ?? createMapInvocation;
            }

            // Move to the next node in the chain
            previous = current;
            if (current.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression is InvocationExpressionSyntax nextInvocation)
            {
                current = nextInvocation;
            }
            else
            {
                break;
            }
        }

        // If we didn't find it, just return CreateMap
        return createMapInvocation;
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

    private string? ExtractPropertyNameFromDiagnostic(Diagnostic diagnostic)
    {
        // Extract from diagnostic message
        string message = diagnostic.GetMessage();
        Match match = Regex.Match(message, @"Property '(\w+)'");
        return match.Success ? match.Groups[1].Value : null;
    }

    private string ExtractOperationTypeFromDiagnostic(Diagnostic diagnostic)
    {
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

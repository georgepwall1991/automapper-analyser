using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Configuration;

/// <summary>
///     Code fix provider for removing duplicate mapping registrations.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM041_DuplicateMappingCodeFixProvider))]
[Shared]
public class AM041_DuplicateMappingCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM041");

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        foreach (Diagnostic? diagnostic in context.Diagnostics)
        {
            SyntaxNode? node = operationContext.Root.FindNode(diagnostic.Location.SourceSpan);

            // Find the invocation expression (could be CreateMap or ReverseMap)
            InvocationExpressionSyntax? invocation = node as InvocationExpressionSyntax ??
                                                     node?.AncestorsAndSelf().OfType<InvocationExpressionSyntax>()
                                                         .FirstOrDefault();

            if (invocation != null)
            {
                string mappingLabel = GetMappingLabel(invocation);
                context.RegisterCodeFix(
                    CodeAction.Create(
                        $"Remove duplicate mapping for '{mappingLabel}'",
                        c => RemoveDuplicateMapping(
                            context.Document,
                            operationContext.Root,
                            invocation,
                            c),
                        $"AM041_RemoveDuplicateMapping_{mappingLabel}"),
                    diagnostic);
            }
        }
    }

    private static string GetMappingLabel(InvocationExpressionSyntax invocation)
    {
        if (TryGetDuplicateMappingTypes(invocation, out string sourceTypeName, out string destinationTypeName))
        {
            return $"{sourceTypeName} -> {destinationTypeName}";
        }

        return "current mapping";
    }

    private static bool TryGetDuplicateMappingTypes(
        InvocationExpressionSyntax invocation,
        out string sourceTypeName,
        out string destinationTypeName)
    {
        sourceTypeName = string.Empty;
        destinationTypeName = string.Empty;

        if (IsReverseMapInvocation(invocation))
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax
                {
                    Expression: InvocationExpressionSyntax createMapInvocation
                })
            {
                return false;
            }

            if (!TryGetCreateMapTypeNames(createMapInvocation, out destinationTypeName, out sourceTypeName))
            {
                return false;
            }

            return true;
        }

        return TryGetCreateMapTypeNames(invocation, out sourceTypeName, out destinationTypeName);
    }

    private static bool TryGetCreateMapTypeNames(
        InvocationExpressionSyntax invocation,
        out string sourceTypeName,
        out string destinationTypeName)
    {
        sourceTypeName = string.Empty;
        destinationTypeName = string.Empty;

        if (!TryGetCreateMapGenericName(invocation, out GenericNameSyntax genericName) ||
            genericName.TypeArgumentList.Arguments.Count != 2)
        {
            return false;
        }

        sourceTypeName = genericName.TypeArgumentList.Arguments[0].ToString();
        destinationTypeName = genericName.TypeArgumentList.Arguments[1].ToString();
        return true;
    }

    private Task<Document> RemoveDuplicateMapping(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        // Check if it is ReverseMap()
        if (IsReverseMapInvocation(invocation))
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                // Replace the whole ReverseMap() invocation with the expression it was called on
                // e.g. CreateMap<A,B>().ReverseMap() -> CreateMap<A,B>()
                return ReplaceNodeAsync(document, root, invocation, memberAccess.Expression);
            }
        }

        // Preserve reverse direction mapping for simple CreateMap<TSource, TDestination>().ReverseMap() duplicates
        if (TryCreateReverseCreateMapReplacement(invocation, out ExpressionStatementSyntax oldStatement,
                out ExpressionStatementSyntax replacementStatement))
        {
            return ReplaceNodeAsync(document, root, oldStatement, replacementStatement);
        }

        // Otherwise, assume it's CreateMap and remove the whole statement
        ExpressionStatementSyntax? statement =
            invocation.AncestorsAndSelf().OfType<ExpressionStatementSyntax>().FirstOrDefault();
        if (statement != null)
        {
            SyntaxNode? newRoot = root.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
            return Task.FromResult(document.WithSyntaxRoot(newRoot!));
        }

        return Task.FromResult(document);
    }

    private static bool TryCreateReverseCreateMapReplacement(
        InvocationExpressionSyntax createMapInvocation,
        out ExpressionStatementSyntax oldStatement,
        out ExpressionStatementSyntax replacementStatement)
    {
        oldStatement = null!;
        replacementStatement = null!;

        if (!TryGetCreateMapGenericName(createMapInvocation, out GenericNameSyntax genericName))
        {
            return false;
        }

        if (genericName.TypeArgumentList.Arguments.Count != 2)
        {
            return false;
        }

        if (createMapInvocation.Parent is not MemberAccessExpressionSyntax reverseMapMemberAccess ||
            reverseMapMemberAccess.Name.Identifier.Text != "ReverseMap" ||
            reverseMapMemberAccess.Parent is not InvocationExpressionSyntax reverseMapInvocation ||
            reverseMapInvocation.ArgumentList.Arguments.Count != 0 ||
            reverseMapInvocation.Parent is not ExpressionStatementSyntax reverseMapStatement)
        {
            return false;
        }

        TypeSyntax sourceType = genericName.TypeArgumentList.Arguments[0];
        TypeSyntax destinationType = genericName.TypeArgumentList.Arguments[1];
        GenericNameSyntax swappedGenericName = genericName.WithTypeArgumentList(
            SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SeparatedList<TypeSyntax>(new SyntaxNodeOrToken[]
                {
                    destinationType, SyntaxFactory.Token(SyntaxKind.CommaToken), sourceType
                })));

        ExpressionSyntax swappedExpression = createMapInvocation.Expression switch
        {
            GenericNameSyntax => swappedGenericName,
            MemberAccessExpressionSyntax memberAccess =>
                memberAccess.WithName(swappedGenericName),
            _ => createMapInvocation.Expression
        };

        InvocationExpressionSyntax swappedCreateMapInvocation = createMapInvocation
            .WithExpression(swappedExpression)
            .WithTriviaFrom(createMapInvocation);

        oldStatement = reverseMapStatement;
        replacementStatement = SyntaxFactory.ExpressionStatement(swappedCreateMapInvocation)
            .WithTriviaFrom(reverseMapStatement);
        return true;
    }

    private static bool TryGetCreateMapGenericName(
        InvocationExpressionSyntax invocation,
        out GenericNameSyntax genericName)
    {
        genericName = invocation.Expression switch
        {
            GenericNameSyntax directGenericName => directGenericName,
            MemberAccessExpressionSyntax { Name: GenericNameSyntax memberGenericName } => memberGenericName,
            _ => null!
        };

        return genericName != null && genericName.Identifier.Text == "CreateMap";
    }

    private static bool IsReverseMapInvocation(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.Text == "ReverseMap";
        }

        return false;
    }
}

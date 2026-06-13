using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

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
            InvocationExpressionSyntax? invocation = FindDuplicateMappingInvocation(
                operationContext.Root.FindNode(diagnostic.Location.SourceSpan),
                diagnostic.Location.SourceSpan);

            if (invocation != null)
            {
                if (IsChainedReverseMapConfiguration(invocation))
                {
                    continue;
                }

                if (IsCreateMapWithUnsafeChainedConfiguration(invocation))
                {
                    continue;
                }

                // Only offer removal when the duplicate registration is a standalone statement we can
                // safely delete. A CreateMap stored in a variable or passed as an argument has no
                // ExpressionStatement ancestor, so the removal would silently do nothing — don't offer it.
                if (!HasRemovableStatement(invocation))
                {
                    continue;
                }

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

    private static bool HasRemovableStatement(InvocationExpressionSyntax invocation)
    {
        ExpressionStatementSyntax? statement =
            invocation.AncestorsAndSelf().OfType<ExpressionStatementSyntax>().FirstOrDefault();
        if (statement == null)
        {
            return false;
        }

        if (statement.Expression == invocation)
        {
            return true;
        }

        return TryCreateReverseCreateMapReplacement(invocation, out _, out _);
    }

    private static InvocationExpressionSyntax? FindDuplicateMappingInvocation(
        SyntaxNode? node,
        TextSpan diagnosticSpan)
    {
        if (node == null)
        {
            return null;
        }

        return node.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Concat(node.AncestorsAndSelf().OfType<InvocationExpressionSyntax>())
            .Where(invocation => IsDuplicateMappingInvocation(invocation) &&
                                 (invocation.Span.IntersectsWith(diagnosticSpan) ||
                                  diagnosticSpan.Contains(invocation.Span)))
            .Distinct()
            .OrderBy(invocation => invocation.Span.Contains(diagnosticSpan) ? 0 : 1)
            .ThenBy(invocation => invocation.Span.Length)
            .FirstOrDefault();
    }

    private static bool IsDuplicateMappingInvocation(InvocationExpressionSyntax invocation)
    {
        return IsReverseMapInvocation(invocation) || TryGetCreateMapGenericName(invocation, out _);
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
            if (IsChainedReverseMapConfiguration(invocation))
            {
                return Task.FromResult(document);
            }

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

        SyntaxNode? reverseMapReceiver = createMapInvocation.Parent;
        while (reverseMapReceiver is ParenthesizedExpressionSyntax parenthesizedReceiver)
        {
            reverseMapReceiver = parenthesizedReceiver.Parent;
        }

        if (reverseMapReceiver is not MemberAccessExpressionSyntax reverseMapMemberAccess ||
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

    private static bool IsChainedReverseMapConfiguration(InvocationExpressionSyntax invocation)
    {
        return IsReverseMapInvocation(invocation) &&
               (invocation.Parent is MemberAccessExpressionSyntax ||
                invocation.Parent is ParenthesizedExpressionSyntax { Parent: MemberAccessExpressionSyntax });
    }

    private static bool IsCreateMapWithUnsafeChainedConfiguration(InvocationExpressionSyntax invocation)
    {
        if (!TryGetCreateMapGenericName(invocation, out _))
        {
            return false;
        }

        SyntaxNode? current = invocation.Parent;
        while (current is ParenthesizedExpressionSyntax paren)
        {
            current = paren.Parent;
        }

        if (current is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        // The bare `CreateMap<S, D>().ReverseMap()` shape is reversed safely by
        // TryCreateReverseCreateMapReplacement. Any further chaining beyond that — or
        // any other chained configuration — would silently drop policy if removed.
        if (memberAccess.Name.Identifier.Text == "ReverseMap" &&
            memberAccess.Parent is InvocationExpressionSyntax reverseMapInvocation &&
            reverseMapInvocation.ArgumentList.Arguments.Count == 0 &&
            reverseMapInvocation.Parent is ExpressionStatementSyntax)
        {
            return false;
        }

        return true;
    }
}

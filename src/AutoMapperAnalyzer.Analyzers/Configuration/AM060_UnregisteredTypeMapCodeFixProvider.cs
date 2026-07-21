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
///     Code fix provider for AM060. Scaffolds the missing CreateMap registration into a Profile
///     constructor in the same document. Cross-document profiles are deliberately withheld so the
///     fix never edits a file the diagnostic does not point at.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM060_UnregisteredTypeMapCodeFixProvider))]
[Shared]
public class AM060_UnregisteredTypeMapCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM060");

    /// <summary>
    ///     Batch FixAll is disabled: independent scaffold actions would append overlapping
    ///     registrations to the same profile constructor, and solution-wide FixAll could add the
    ///     same pair to several profiles (creating AM041 duplicate registrations).
    /// </summary>
    public override FixAllProvider GetFixAllProvider() => null!;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        CodeFixOperationContext? operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            Dictionary<string, string>? properties = TryGetDiagnosticProperties(
                diagnostic,
                "SourceTypeName",
                "DestinationTypeName");
            if (properties == null)
            {
                continue;
            }

            if (!TryFindProfileConstructor(
                    operationContext,
                    out ConstructorDeclarationSyntax? constructor,
                    out string? profileName))
            {
                continue;
            }

            string sourceName = properties["SourceTypeName"];
            string destinationName = properties["DestinationTypeName"];

            // Re-resolve the reported pair semantically so the inserted type names are the minimal
            // names valid at the profile constructor (diagnostic properties carry display names
            // relative to the call site, which may differ from the profile's scope).
            (string Source, string Destination)? resolvedNames = ResolveTypeNamesAtConstructor(
                operationContext,
                diagnostic,
                constructor!);
            if (resolvedNames != null)
            {
                sourceName = resolvedNames.Value.Source;
                destinationName = resolvedNames.Value.Destination;
            }

            string sealedSourceName = sourceName;
            string sealedDestinationName = destinationName;

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Create CreateMap<{sealedSourceName}, {sealedDestinationName}> in {profileName}",
                    _ => Task.FromResult(AddCreateMapToProfile(
                        context.Document,
                        operationContext,
                        constructor!,
                        sealedSourceName,
                        sealedDestinationName)),
                    $"AM060_CreateMap_{sealedSourceName}_{sealedDestinationName}"),
                diagnostic);
        }
    }

    private static (string Source, string Destination)? ResolveTypeNamesAtConstructor(
        CodeFixOperationContext operationContext,
        Diagnostic diagnostic,
        ConstructorDeclarationSyntax constructor)
    {
        SyntaxNode node = operationContext.Root.FindNode(diagnostic.Location.SourceSpan);
        InvocationExpressionSyntax? invocation =
            node as InvocationExpressionSyntax ?? node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation == null ||
            !AM060_UnregisteredTypeMapAnalyzer.TryGetReportedPair(
                invocation,
                operationContext.SemanticModel,
                out ITypeSymbol source,
                out ITypeSymbol destination))
        {
            return null;
        }

        int position = constructor.Body?.SpanStart ?? constructor.SpanStart;
        return (
            source.ToMinimalDisplayString(operationContext.SemanticModel, position),
            destination.ToMinimalDisplayString(operationContext.SemanticModel, position));
    }

    private static bool TryFindProfileConstructor(
        CodeFixOperationContext operationContext,
        out ConstructorDeclarationSyntax? constructor,
        out string? profileName)
    {
        foreach (ClassDeclarationSyntax classDeclaration in operationContext.Root
                     .DescendantNodes()
                     .OfType<ClassDeclarationSyntax>())
        {
            if (operationContext.SemanticModel.GetDeclaredSymbol(classDeclaration) is not { } classSymbol ||
                !DerivesFromAutoMapperProfile(classSymbol))
            {
                continue;
            }

            ConstructorDeclarationSyntax? candidate = classDeclaration.Members
                .OfType<ConstructorDeclarationSyntax>()
                .FirstOrDefault(candidate => candidate.Body != null &&
                                             !candidate.Modifiers.Any(SyntaxKind.StaticKeyword));
            if (candidate == null)
            {
                continue;
            }

            constructor = candidate;
            profileName = classSymbol.Name;
            return true;
        }

        constructor = null;
        profileName = null;
        return false;
    }

    private static bool DerivesFromAutoMapperProfile(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
        {
            if (baseType.Name == "Profile" &&
                baseType.ContainingNamespace?.ToDisplayString() == "AutoMapper")
            {
                return true;
            }
        }

        return false;
    }

    private static Document AddCreateMapToProfile(
        Document document,
        CodeFixOperationContext operationContext,
        ConstructorDeclarationSyntax constructor,
        string sourceName,
        string destinationName)
    {
        InvocationExpressionSyntax createMapCall = SyntaxFactory.InvocationExpression(
                SyntaxFactory.GenericName(SyntaxFactory.Identifier("CreateMap"))
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SeparatedList(new[]
                            {
                                SyntaxFactory.ParseTypeName(sourceName),
                                SyntaxFactory.ParseTypeName(destinationName)
                            }))))
            .WithArgumentList(SyntaxFactory.ArgumentList());

        ExpressionStatementSyntax statement = SyntaxFactory.ExpressionStatement(createMapCall);

        BlockSyntax newBody = constructor.Body!.WithStatements(
            constructor.Body.Statements.Add(statement));

        ConstructorDeclarationSyntax newConstructor = constructor.WithBody(newBody);
        SyntaxNode newRoot = operationContext.Root.ReplaceNode(constructor, newConstructor);
        return document.WithSyntaxRoot(newRoot);
    }
}

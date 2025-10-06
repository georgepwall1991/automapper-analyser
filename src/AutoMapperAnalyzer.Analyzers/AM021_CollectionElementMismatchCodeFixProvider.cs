using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
/// Code fix provider for AM021 Collection Element Mismatch diagnostics.
/// Provides fixes for incompatible collection element types.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM021_CollectionElementMismatchCodeFixProvider)), Shared]
public class AM021_CollectionElementMismatchCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM021");

    /// <summary>
    /// Gets whether this provider can fix multiple diagnostics in a single code action.
    /// </summary>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>
    /// Registers code fixes for the diagnostics.
    /// </summary>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        foreach (var diagnostic in context.Diagnostics)
        {
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var invocation = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(i => i.Span.Contains(diagnosticSpan));

            if (invocation == null)
            {
                continue;
            }

            // Extract diagnostic properties
            if (!diagnostic.Properties.TryGetValue("PropertyName", out var propertyName) || string.IsNullOrEmpty(propertyName))
            {
                // Fall back to parsing from diagnostic message
                propertyName = ExtractPropertyNameFromDiagnostic(diagnostic);
                if (string.IsNullOrEmpty(propertyName))
                {
                    continue;
                }
            }

            var sourceElementType = diagnostic.Properties.TryGetValue("SourceElementType", out var sourceElemType)
                ? sourceElemType
                : ExtractSourceElementTypeFromDiagnostic(diagnostic);
            var destElementType = diagnostic.Properties.TryGetValue("DestElementType", out var destElemType)
                ? destElemType
                : ExtractDestElementTypeFromDiagnostic(diagnostic);

            if (string.IsNullOrEmpty(sourceElementType) || string.IsNullOrEmpty(destElementType))
            {
                continue;
            }

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (semanticModel == null)
            {
                continue;
            }

            // Determine if this is a simple type conversion or complex mapping
            bool isSimpleConversion = IsSimpleTypeConversion(sourceElementType!, destElementType!);

            if (isSimpleConversion)
            {
                // Offer simple conversion with Parse/Convert
                RegisterSimpleConversionFix(context, invocation, propertyName!, sourceElementType!, destElementType!, diagnostic, semanticModel);
            }
            else
            {
                // Offer complex mapping with mapper.Map<T>
                RegisterComplexMappingFix(context, invocation, propertyName!, sourceElementType!, destElementType!, diagnostic, semanticModel);
            }

            // Always offer ignore option
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Ignore property '{propertyName}'",
                    createChangedDocument: cancellationToken =>
                        AddIgnoreAsync(context.Document, invocation, propertyName!, cancellationToken),
                    equivalenceKey: $"AM021_Ignore_{propertyName}"),
                diagnostic);
        }
    }

    private void RegisterSimpleConversionFix(
        CodeFixContext context,
        InvocationExpressionSyntax invocation,
        string propertyName,
        string sourceElementType,
        string destElementType,
        Diagnostic diagnostic,
        SemanticModel semanticModel)
    {
        var conversionMethod = GetConversionMethod(sourceElementType, destElementType);
        var collectionMethod = GetCollectionMaterializationMethod(invocation, propertyName, semanticModel);

        var mapFromExpression = $"src.{propertyName}.Select(x => {conversionMethod}(x)).{collectionMethod}()";

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Add Select with {conversionMethod} for '{propertyName}'",
                createChangedDocument: cancellationToken =>
                    AddMapFromWithLinqAsync(context.Document, invocation, propertyName, mapFromExpression, cancellationToken),
                equivalenceKey: $"AM021_SimpleConversion_{propertyName}"),
            diagnostic);
    }

    private void RegisterComplexMappingFix(
        CodeFixContext context,
        InvocationExpressionSyntax invocation,
        string propertyName,
        string sourceElementType,
        string destElementType,
        Diagnostic diagnostic,
        SemanticModel semanticModel)
    {
        var sourceTypeShortName = GetShortTypeName(sourceElementType);
        var destTypeShortName = GetShortTypeName(destElementType);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Add CreateMap<{sourceTypeShortName}, {destTypeShortName}>() for element mapping",
                createChangedDocument: cancellationToken =>
                    AddElementCreateMapAsync(context.Document, invocation,
                        sourceTypeShortName, destTypeShortName, cancellationToken),
                equivalenceKey: $"AM021_ComplexMapping_{propertyName}"),
            diagnostic);
    }

    private static async Task<Document> AddMapFromWithLinqAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string propertyName,
        string mapFromExpression,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Add ForMember with MapFrom
        var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
            invocation,
            propertyName,
            mapFromExpression);

        var newRoot = root.ReplaceNode(invocation, newInvocation);

        // Add using System.Linq if not present
        if (newRoot is CompilationUnitSyntax compilationUnit)
        {
            newRoot = AddUsingIfMissing(compilationUnit, "System.Linq");
        }

        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> AddElementCreateMapAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string sourceTypeName,
        string destTypeName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Find the class/profile containing this CreateMap
        var classDeclaration = invocation.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDeclaration != null)
        {
            // Add CreateMap for element types after the current CreateMap
            var createMapStatement = CreateElementCreateMapStatement(sourceTypeName, destTypeName);

            var constructor = classDeclaration.DescendantNodes()
                .OfType<ConstructorDeclarationSyntax>()
                .FirstOrDefault(c => c.Body != null && c.Body.Statements.Any(s => s.DescendantNodes().Contains(invocation)));

            if (constructor?.Body != null)
            {
                var statementWithInvocation = constructor.Body.Statements
                    .FirstOrDefault(s => s.DescendantNodes().Contains(invocation));

                if (statementWithInvocation != null)
                {
                    var indexOfStatement = constructor.Body.Statements.IndexOf(statementWithInvocation);

                    // Insert the new CreateMap statement after the existing one
                    var newStatements = constructor.Body.Statements.ToList();
                    newStatements.Insert(indexOfStatement + 1, createMapStatement);

                    var newBody = constructor.Body.WithStatements(SyntaxFactory.List(newStatements));
                    var newConstructor = constructor.WithBody(newBody);
                    var newClass = classDeclaration.ReplaceNode(constructor, newConstructor);
                    root = root!.ReplaceNode(classDeclaration, newClass);
                }
            }
        }

        return document.WithSyntaxRoot(root);
    }

    private static async Task<Document> AddIgnoreAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        var newInvocation = CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
        var newRoot = root.ReplaceNode(invocation, newInvocation);

        return document.WithSyntaxRoot(newRoot);
    }

    private static ExpressionStatementSyntax CreateElementCreateMapStatement(string sourceType, string destType)
    {
        return SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.GenericName(
                    SyntaxFactory.Identifier("CreateMap"))
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SeparatedList<TypeSyntax>(new[]
                        {
                            SyntaxFactory.ParseTypeName(sourceType),
                            SyntaxFactory.ParseTypeName(destType)
                        }))))
            .WithArgumentList(SyntaxFactory.ArgumentList())
        );
    }

    private static string GetCollectionMaterializationMethod(InvocationExpressionSyntax invocation, string propertyName, SemanticModel semanticModel)
    {
        // Get the destination property type to determine the appropriate collection method
        var createMapTypes = AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
        if (createMapTypes.Item2 == null)
        {
            return "ToList"; // Default
        }

        var destProperty = createMapTypes.Item2.GetMembers()
            .OfType<IPropertySymbol>()
            .FirstOrDefault(p => p.Name == propertyName);

        if (destProperty == null)
        {
            return "ToList";
        }

        var destTypeName = destProperty.Type.ToDisplayString();

        if (destTypeName.Contains("HashSet"))
            return "ToHashSet";
        if (destTypeName.Contains("[]"))
            return "ToArray";
        if (destTypeName.Contains("Stack"))
            return "ToList"; // Stack doesn't have ToStack, use ToList + new Stack<T>
        if (destTypeName.Contains("Queue"))
            return "ToList"; // Queue doesn't have ToQueue, use ToList + new Queue<T>

        return "ToList";
    }

    private static bool IsSimpleTypeConversion(string sourceElementType, string destElementType)
    {
        // Check if both are primitive/built-in types
        var primitiveTypes = new[] { "string", "int", "long", "double", "float", "decimal", "bool", "byte", "short", "char",
            "String", "Int32", "Int64", "Double", "Single", "Decimal", "Boolean", "Byte", "Int16", "Char" };

        var sourceIsPrimitive = primitiveTypes.Any(p => sourceElementType.Contains(p));
        var destIsPrimitive = primitiveTypes.Any(p => destElementType.Contains(p));

        return sourceIsPrimitive && destIsPrimitive;
    }

    private static string GetConversionMethod(string sourceElementType, string destElementType)
    {
        // Determine the conversion method based on destination type
        if (destElementType.Contains("int") || destElementType.Contains("Int32"))
            return "int.Parse";
        if (destElementType.Contains("long") || destElementType.Contains("Int64"))
            return "long.Parse";
        if (destElementType.Contains("double") || destElementType.Contains("Double"))
            return "double.Parse";
        if (destElementType.Contains("float") || destElementType.Contains("Single"))
            return "float.Parse";
        if (destElementType.Contains("decimal") || destElementType.Contains("Decimal"))
            return "decimal.Parse";
        if (destElementType.Contains("bool") || destElementType.Contains("Boolean"))
            return "bool.Parse";

        return "Convert.ToString"; // Default fallback
    }

    private static string GetShortTypeName(string fullTypeName)
    {
        // Extract the simple type name from fully qualified name
        var lastDot = fullTypeName.LastIndexOf('.');
        if (lastDot >= 0)
        {
            return fullTypeName.Substring(lastDot + 1);
        }
        return fullTypeName;
    }

    private static string? ExtractPropertyNameFromDiagnostic(Diagnostic diagnostic)
    {
        var message = diagnostic.GetMessage();
        var startIndex = message.IndexOf("Property '");
        if (startIndex < 0) return null;

        startIndex += "Property '".Length;
        var endIndex = message.IndexOf("'", startIndex);
        if (endIndex < 0) return null;

        return message.Substring(startIndex, endIndex - startIndex);
    }

    private static string? ExtractSourceElementTypeFromDiagnostic(Diagnostic diagnostic)
    {
        var message = diagnostic.GetMessage();
        var pattern = @"\(([^)]+)\) elements cannot";
        var match = System.Text.RegularExpressions.Regex.Match(message, pattern);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractDestElementTypeFromDiagnostic(Diagnostic diagnostic)
    {
        var message = diagnostic.GetMessage();
        var pattern = @"to [^(]+\(([^)]+)\) elements";
        var match = System.Text.RegularExpressions.Regex.Match(message, pattern);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static CompilationUnitSyntax AddUsingIfMissing(CompilationUnitSyntax root, string namespaceName)
    {
        if (root.Usings.Any(u => string.Equals(u.Name.ToString(), namespaceName, StringComparison.Ordinal)))
        {
            return root;
        }

        var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName))
            .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed);

        return root.AddUsings(usingDirective);
    }
}

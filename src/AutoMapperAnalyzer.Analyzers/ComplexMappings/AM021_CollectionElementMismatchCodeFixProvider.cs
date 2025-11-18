using System.Collections.Immutable;
using System.Composition;
using System.Text.RegularExpressions;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Analyzers.ComplexMappings;

/// <summary>
///     Code fix provider for AM021 Collection Element Mismatch diagnostics.
///     Provides fixes for incompatible collection element types.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM021_CollectionElementMismatchCodeFixProvider))]
[Shared]
public class AM021_CollectionElementMismatchCodeFixProvider : CodeFixProvider
{
    /// <summary>
    ///     Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM021");

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

        foreach (Diagnostic? diagnostic in context.Diagnostics)
        {
            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

            InvocationExpressionSyntax? invocation = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(i => i.Span.Contains(diagnosticSpan));

            if (invocation == null)
            {
                continue;
            }

            // Extract diagnostic properties
            if (!diagnostic.Properties.TryGetValue("PropertyName", out string? propertyName) ||
                string.IsNullOrEmpty(propertyName))
            {
                // Fall back to parsing from diagnostic message
                propertyName = ExtractPropertyNameFromDiagnostic(diagnostic);
                if (string.IsNullOrEmpty(propertyName))
                {
                    continue;
                }
            }

            string? sourceElementType =
                diagnostic.Properties.TryGetValue("SourceElementType", out string? sourceElemType)
                    ? sourceElemType
                    : ExtractSourceElementTypeFromDiagnostic(diagnostic);
            string? destElementType = diagnostic.Properties.TryGetValue("DestElementType", out string? destElemType)
                ? destElemType
                : ExtractDestElementTypeFromDiagnostic(diagnostic);

            if (string.IsNullOrEmpty(sourceElementType) || string.IsNullOrEmpty(destElementType))
            {
                continue;
            }

            SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken)
                .ConfigureAwait(false);
            if (semanticModel == null)
            {
                continue;
            }

            // Determine if this is a simple type conversion or complex mapping
            bool isSimpleConversion = IsSimpleTypeConversion(sourceElementType!, destElementType!);

            if (isSimpleConversion)
            {
                // Offer simple conversion with Parse/Convert
                RegisterSimpleConversionFix(context, invocation, propertyName!, sourceElementType!, destElementType!,
                    diagnostic, semanticModel);
            }
            else
            {
                // Offer complex mapping with mapper.Map<T>
                RegisterComplexMappingFix(context, invocation, propertyName!, sourceElementType!, destElementType!,
                    diagnostic, semanticModel);
            }

            // Always offer ignore option
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Ignore property '{propertyName}'",
                    cancellationToken =>
                        AddIgnoreAsync(context.Document, invocation, propertyName!, cancellationToken),
                    $"AM021_Ignore_{propertyName}"),
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
        string conversionMethod = GetConversionMethod(sourceElementType, destElementType);
        string collectionMethod = GetCollectionMaterializationMethod(invocation, propertyName, semanticModel);

        string mapFromExpression = $"src.{propertyName}.Select(x => {conversionMethod}(x)).{collectionMethod}()";

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Add Select with {conversionMethod} for '{propertyName}'",
                cancellationToken =>
                    AddMapFromWithLinqAsync(context.Document, invocation, propertyName, mapFromExpression,
                        cancellationToken),
                $"AM021_SimpleConversion_{propertyName}"),
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
        string sourceTypeShortName = GetShortTypeName(sourceElementType);
        string destTypeShortName = GetShortTypeName(destElementType);

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Add CreateMap<{sourceTypeShortName}, {destTypeShortName}>() for element mapping",
                cancellationToken =>
                    AddElementCreateMapAsync(context.Document, invocation,
                        sourceTypeShortName, destTypeShortName, cancellationToken),
                $"AM021_ComplexMapping_{propertyName}"),
            diagnostic);
    }

    private static async Task<Document> AddMapFromWithLinqAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string propertyName,
        string mapFromExpression,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return document;
        }

        // Add ForMember with MapFrom
        InvocationExpressionSyntax newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
            invocation,
            propertyName,
            mapFromExpression);

        SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);

        // Add using System.Linq if not present
        if (newRoot is CompilationUnitSyntax compilationUnit)
        {
            newRoot = AddUsingIfMissing(compilationUnit, "System.Linq");

            // Add using System if Convert is used
            if (mapFromExpression.Contains("Convert."))
            {
                newRoot = AddUsingIfMissing((CompilationUnitSyntax)newRoot, "System");
            }
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
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return document;
        }

        // Find the class/profile containing this CreateMap
        ClassDeclarationSyntax? classDeclaration = invocation.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDeclaration != null)
        {
            // Add CreateMap for element types after the current CreateMap
            ExpressionStatementSyntax createMapStatement =
                CreateElementCreateMapStatement(sourceTypeName, destTypeName);

            ConstructorDeclarationSyntax? constructor = classDeclaration.DescendantNodes()
                .OfType<ConstructorDeclarationSyntax>()
                .FirstOrDefault(c =>
                    c.Body != null && c.Body.Statements.Any(s => s.DescendantNodes().Contains(invocation)));

            if (constructor?.Body != null)
            {
                StatementSyntax? statementWithInvocation = constructor.Body.Statements
                    .FirstOrDefault(s => s.DescendantNodes().Contains(invocation));

                if (statementWithInvocation != null)
                {
                    int indexOfStatement = constructor.Body.Statements.IndexOf(statementWithInvocation);

                    // Insert the new CreateMap statement after the existing one
                    var newStatements = constructor.Body.Statements.ToList();
                    newStatements.Insert(indexOfStatement + 1, createMapStatement);

                    BlockSyntax newBody = constructor.Body.WithStatements(SyntaxFactory.List(newStatements));
                    ConstructorDeclarationSyntax newConstructor = constructor.WithBody(newBody);
                    ClassDeclarationSyntax newClass = classDeclaration.ReplaceNode(constructor, newConstructor);
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
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return document;
        }

        InvocationExpressionSyntax newInvocation =
            CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
        SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);

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
                                    SyntaxFactory.ParseTypeName(sourceType), SyntaxFactory.ParseTypeName(destType)
                                }))))
                .WithArgumentList(SyntaxFactory.ArgumentList())
        );
    }

    private static string GetCollectionMaterializationMethod(InvocationExpressionSyntax invocation, string propertyName,
        SemanticModel semanticModel)
    {
        // Get the destination property type to determine the appropriate collection method
        (ITypeSymbol? sourceType, ITypeSymbol? destType) createMapTypes =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
        if (createMapTypes.Item2 == null)
        {
            return "ToList"; // Default
        }

        IPropertySymbol? destProperty = createMapTypes.Item2.GetMembers()
            .OfType<IPropertySymbol>()
            .FirstOrDefault(p => p.Name == propertyName);

        if (destProperty == null)
        {
            return "ToList";
        }

        string destTypeName = destProperty.Type.ToDisplayString();

        if (destTypeName.Contains("HashSet"))
        {
            return "ToHashSet";
        }

        if (destTypeName.Contains("[]"))
        {
            return "ToArray";
        }

        if (destTypeName.Contains("Stack"))
        {
            return "ToList"; // Stack doesn't have ToStack, use ToList + new Stack<T>
        }

        if (destTypeName.Contains("Queue"))
        {
            return "ToList"; // Queue doesn't have ToQueue, use ToList + new Queue<T>
        }

        return "ToList";
    }

    private static bool IsSimpleTypeConversion(string sourceElementType, string destElementType)
    {
        // Check if both are primitive/built-in types
        string[] primitiveTypes = new[]
        {
            "string", "int", "long", "double", "float", "decimal", "bool", "byte", "short", "char", "String",
            "Int32", "Int64", "Double", "Single", "Decimal", "Boolean", "Byte", "Int16", "Char"
        };

        bool sourceIsPrimitive = primitiveTypes.Any(p => sourceElementType.Contains(p));
        bool destIsPrimitive = primitiveTypes.Any(p => destElementType.Contains(p));

        return sourceIsPrimitive && destIsPrimitive;
    }

    private static string GetConversionMethod(string sourceElementType, string destElementType)
    {
        // Determine the conversion method based on destination type
        // using Convert class for better null handling compared to Parse
        if (destElementType.Contains("int") || destElementType.Contains("Int32"))
        {
            return "Convert.ToInt32";
        }

        if (destElementType.Contains("long") || destElementType.Contains("Int64"))
        {
            return "Convert.ToInt64";
        }

        if (destElementType.Contains("double") || destElementType.Contains("Double"))
        {
            return "Convert.ToDouble";
        }

        if (destElementType.Contains("float") || destElementType.Contains("Single"))
        {
            return "Convert.ToSingle";
        }

        if (destElementType.Contains("decimal") || destElementType.Contains("Decimal"))
        {
            return "Convert.ToDecimal";
        }

        if (destElementType.Contains("bool") || destElementType.Contains("Boolean"))
        {
            return "Convert.ToBoolean";
        }

        if (destElementType.Contains("byte") || destElementType.Contains("Byte"))
        {
            return "Convert.ToByte";
        }

        if (destElementType.Contains("short") || destElementType.Contains("Int16"))
        {
            return "Convert.ToInt16";
        }

        // Types not supported by Convert directly or needing Parse
        if (destElementType.Contains("DateTime"))
        {
            return "DateTime.Parse";
        }

        if (destElementType.Contains("Guid"))
        {
            return "Guid.Parse";
        }

        return "Convert.ToString"; // Default fallback
    }

    private static string GetShortTypeName(string fullTypeName)
    {
        // Extract the simple type name from fully qualified name
        int lastDot = fullTypeName.LastIndexOf('.');
        if (lastDot >= 0)
        {
            return fullTypeName.Substring(lastDot + 1);
        }

        return fullTypeName;
    }

    private static string? ExtractPropertyNameFromDiagnostic(Diagnostic diagnostic)
    {
        string message = diagnostic.GetMessage();
        int startIndex = message.IndexOf("Property '");
        if (startIndex < 0)
        {
            return null;
        }

        startIndex += "Property '".Length;
        int endIndex = message.IndexOf("'", startIndex);
        if (endIndex < 0)
        {
            return null;
        }

        return message.Substring(startIndex, endIndex - startIndex);
    }

    private static string? ExtractSourceElementTypeFromDiagnostic(Diagnostic diagnostic)
    {
        string message = diagnostic.GetMessage();
        string pattern = @"\(([^)]+)\) elements cannot";
        Match match = Regex.Match(message, pattern);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractDestElementTypeFromDiagnostic(Diagnostic diagnostic)
    {
        string message = diagnostic.GetMessage();
        string pattern = @"to [^(]+\(([^)]+)\) elements";
        Match match = Regex.Match(message, pattern);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static CompilationUnitSyntax AddUsingIfMissing(CompilationUnitSyntax root, string namespaceName)
    {
        if (root.Usings.Any(u =>
                u.Name != null && string.Equals(u.Name.ToString(), namespaceName, StringComparison.Ordinal)))
        {
            return root;
        }

        UsingDirectiveSyntax usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName))
            .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed);

        return root.AddUsings(usingDirective);
    }
}

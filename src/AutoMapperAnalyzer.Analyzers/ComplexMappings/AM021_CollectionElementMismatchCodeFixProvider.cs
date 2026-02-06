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
public class AM021_CollectionElementMismatchCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <summary>
    ///     Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM021");

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

        foreach (Diagnostic? diagnostic in context.Diagnostics)
        {
            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

            InvocationExpressionSyntax? invocation = operationContext.Root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf()
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

            InvocationExpressionSyntax? reverseMapInvocation =
                AutoMapperAnalysisHelpers.GetReverseMapInvocation(invocation);
            if (AM020MappingConfigurationHelpers.HasCustomConstructionOrConversion(invocation, reverseMapInvocation) ||
                AM020MappingConfigurationHelpers.IsDestinationPropertyExplicitlyConfigured(
                    invocation,
                    propertyName!,
                    reverseMapInvocation))
            {
                continue;
            }

            // Determine if this is a simple type conversion or complex mapping
            bool isSimpleConversion = IsSimpleTypeConversion(sourceElementType!, destElementType!);

            if (isSimpleConversion)
            {
                // Offer simple conversion with Parse/Convert
                RegisterSimpleConversionFix(context, operationContext.Root, invocation, propertyName!, sourceElementType!, destElementType!,
                    diagnostic, operationContext.SemanticModel);
            }
            else
            {
                // Offer complex mapping with mapper.Map<T>
                RegisterComplexMappingFix(context, operationContext.Root, invocation, propertyName!, sourceElementType!, destElementType!,
                    diagnostic, operationContext.SemanticModel);
            }

            // Always offer ignore option
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Ignore property '{propertyName}'",
                    cancellationToken =>
                        AddIgnoreAsync(context.Document, operationContext.Root, invocation, propertyName!),
                    $"AM021_Ignore_{propertyName}"),
                diagnostic);
        }
    }

    private void RegisterSimpleConversionFix(
        CodeFixContext context,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        string propertyName,
        string sourceElementType,
        string destElementType,
        Diagnostic diagnostic,
        SemanticModel semanticModel)
    {
        string conversionMethod = GetConversionMethod(destElementType);
        string collectionMethod = GetCollectionMaterializationMethod(invocation, propertyName, semanticModel);

        string mapFromExpression = $"src.{propertyName}.Select(x => {conversionMethod}(x)).{collectionMethod}()";

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Add Select with {conversionMethod} for '{propertyName}'",
                cancellationToken =>
                    AddMapFromWithLinqAsync(context.Document, root, invocation, propertyName, mapFromExpression),
                $"AM021_SimpleConversion_{propertyName}"),
            diagnostic);
    }

    private void RegisterComplexMappingFix(
        CodeFixContext context,
        SyntaxNode root,
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
                        sourceElementType, destElementType, cancellationToken),
                $"AM021_ComplexMapping_{propertyName}"),
            diagnostic);
    }

    private async Task<Document> AddMapFromWithLinqAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        string propertyName,
        string mapFromExpression)
    {
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

        return await Task.FromResult(document.WithSyntaxRoot(newRoot));
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

    private Task<Document> AddIgnoreAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        string propertyName)
    {
        InvocationExpressionSyntax newInvocation =
            CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
        return ReplaceNodeAsync(document, root, invocation, newInvocation);
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
        return IsSimpleConversionType(sourceElementType) && IsSimpleConversionType(destElementType);
    }

    private static string GetConversionMethod(string destElementType)
    {
        // Determine the conversion method based on destination type
        // using Convert class for better null handling compared to Parse
        string normalizedDestType = NormalizeTypeName(destElementType);
        return normalizedDestType switch
        {
            "int" or "System.Int32" => "Convert.ToInt32",
            "long" or "System.Int64" => "Convert.ToInt64",
            "double" or "System.Double" => "Convert.ToDouble",
            "float" or "System.Single" => "Convert.ToSingle",
            "decimal" or "System.Decimal" => "Convert.ToDecimal",
            "bool" or "System.Boolean" => "Convert.ToBoolean",
            "byte" or "System.Byte" => "Convert.ToByte",
            "short" or "System.Int16" => "Convert.ToInt16",
            "char" or "System.Char" => "Convert.ToChar",
            "string" or "System.String" => "Convert.ToString",
            "DateTime" or "System.DateTime" => "DateTime.Parse",
            "Guid" or "System.Guid" => "Guid.Parse",
            _ => "Convert.ToString"
        };
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

    private static bool IsSimpleConversionType(string typeName)
    {
        string normalizedType = NormalizeTypeName(typeName);
        return normalizedType is "string" or "System.String" or
               "int" or "System.Int32" or
               "long" or "System.Int64" or
               "double" or "System.Double" or
               "float" or "System.Single" or
               "decimal" or "System.Decimal" or
               "bool" or "System.Boolean" or
               "byte" or "System.Byte" or
               "short" or "System.Int16" or
               "char" or "System.Char" or
               "DateTime" or "System.DateTime" or
               "Guid" or "System.Guid";
    }

    private static string NormalizeTypeName(string typeName)
    {
        string normalized = typeName.Trim();

        if (normalized.StartsWith("global::", StringComparison.Ordinal))
        {
            normalized = normalized.Substring("global::".Length);
        }

        while (normalized.EndsWith("[]", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(0, normalized.Length - 2);
        }

        if (normalized.StartsWith("System.Nullable<", StringComparison.Ordinal) &&
            normalized.EndsWith(">", StringComparison.Ordinal))
        {
            string innerType = normalized.Substring("System.Nullable<".Length,
                normalized.Length - "System.Nullable<".Length - 1);
            return NormalizeTypeName(innerType);
        }

        if (normalized.StartsWith("Nullable<", StringComparison.Ordinal) &&
            normalized.EndsWith(">", StringComparison.Ordinal))
        {
            string innerType = normalized.Substring("Nullable<".Length, normalized.Length - "Nullable<".Length - 1);
            return NormalizeTypeName(innerType);
        }

        if (normalized.EndsWith("?", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(0, normalized.Length - 1);
        }

        int genericStart = normalized.IndexOf('<');
        if (genericStart >= 0)
        {
            normalized = normalized.Substring(0, genericStart);
        }

        return normalized switch
        {
            "String" => "System.String",
            "Int32" => "System.Int32",
            "Int64" => "System.Int64",
            "Double" => "System.Double",
            "Single" => "System.Single",
            "Decimal" => "System.Decimal",
            "Boolean" => "System.Boolean",
            "Byte" => "System.Byte",
            "Int16" => "System.Int16",
            "Char" => "System.Char",
            _ => normalized
        };
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

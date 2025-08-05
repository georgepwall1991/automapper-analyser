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

namespace AutoMapperAnalyzer.CodeFixes;

/// <summary>
/// Code fix provider for AM020: Missing nested object mapping configurations
/// Automatically adds CreateMap statements for missing nested type mappings
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM020_NestedObjectMappingCodeFixProvider)), Shared]
public class AM020_NestedObjectMappingCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("AM020");

    /// <summary>
    /// Gets the fix all provider for batch fixes.
    /// </summary>
    /// <returns>The batch fixer provider.</returns>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>
    /// Registers code fixes for the specified context.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel == null) return;

        foreach (var diagnostic in context.Diagnostics.Where(d => FixableDiagnosticIds.Contains(d.Id)))
        {
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var node = root.FindNode(diagnosticSpan);

            if (node is InvocationExpressionSyntax invocation)
            {
                var action = CodeAction.Create(
                    title: "Add missing nested object mapping",
                    createChangedDocument: c => AddMissingNestedMappingAsync(context.Document, invocation, semanticModel, c),
                    equivalenceKey: "AddMissingNestedMapping");

                context.RegisterCodeFix(action, diagnostic);
            }
        }
    }

    private static async Task<Document> AddMissingNestedMappingAsync(
        Document document,
        InvocationExpressionSyntax createMapInvocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Get the type arguments from the CreateMap call
        var typeArguments = GetCreateMapTypeArguments(createMapInvocation, semanticModel);
        if (typeArguments.sourceType == null || typeArguments.destinationType == null)
        {
            return document;
        }

        // Find all missing nested mappings
        var missingMappings = GetMissingNestedMappings(
            createMapInvocation, 
            typeArguments.sourceType, 
            typeArguments.destinationType, 
            semanticModel);

        if (!missingMappings.Any())
        {
            return document;
        }

        // Find the constructor containing the CreateMap call
        var constructor = createMapInvocation.Ancestors().OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
        if (constructor?.Body == null)
        {
            return document;
        }

        // Create new CreateMap statements
        var newStatements = missingMappings.Select(mapping => 
            CreateCreateMapStatement(mapping.sourceType, mapping.destinationType)).ToArray();

        // Insert the new statements after the original CreateMap call
        var originalStatement = createMapInvocation.Ancestors().OfType<ExpressionStatementSyntax>().FirstOrDefault();
        if (originalStatement == null)
        {
            return document;
        }

        var originalIndex = constructor.Body.Statements.IndexOf(originalStatement);
        var newBody = constructor.Body.WithStatements(
            constructor.Body.Statements.InsertRange(originalIndex + 1, newStatements));

        var newConstructor = constructor.WithBody(newBody);
        var newRoot = root.ReplaceNode(constructor, newConstructor);

        return document.WithSyntaxRoot(newRoot);
    }

    private static (INamedTypeSymbol? sourceType, INamedTypeSymbol? destinationType) GetCreateMapTypeArguments(
        InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is IMethodSymbol method && method.IsGenericMethod && method.TypeArguments.Length == 2)
        {
            var sourceType = method.TypeArguments[0] as INamedTypeSymbol;
            var destinationType = method.TypeArguments[1] as INamedTypeSymbol;
            return (sourceType, destinationType);
        }

        return (null, null);
    }

    private static List<(INamedTypeSymbol sourceType, INamedTypeSymbol destinationType)> GetMissingNestedMappings(
        InvocationExpressionSyntax createMapInvocation,
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType,
        SemanticModel semanticModel)
    {
        var missingMappings = new List<(INamedTypeSymbol, INamedTypeSymbol)>();
        var existingMappings = GetExistingMappings(createMapInvocation, semanticModel);

        var sourceProperties = GetMappableProperties(sourceType);
        var destinationProperties = GetMappableProperties(destinationType);

        foreach (var sourceProperty in sourceProperties)
        {
            var destinationProperty = destinationProperties
                .FirstOrDefault(p => string.Equals(p.Name, sourceProperty.Name, System.StringComparison.OrdinalIgnoreCase));

            if (destinationProperty == null) continue;

            if (RequiresNestedObjectMapping(sourceProperty.Type, destinationProperty.Type))
            {
                var sourceNestedType = GetUnderlyingType(sourceProperty.Type) as INamedTypeSymbol;
                var destNestedType = GetUnderlyingType(destinationProperty.Type) as INamedTypeSymbol;

                if (sourceNestedType != null && destNestedType != null)
                {
                    var mappingKey = (sourceNestedType.Name, destNestedType.Name);
                    
                    if (!existingMappings.Contains(mappingKey))
                    {
                        missingMappings.Add((sourceNestedType, destNestedType));
                    }
                }
            }
        }

        return missingMappings.Distinct().ToList();
    }

    private static HashSet<(string sourceType, string destType)> GetExistingMappings(
        InvocationExpressionSyntax currentInvocation, SemanticModel semanticModel)
    {
        var mappings = new HashSet<(string, string)>();

        var containingClass = currentInvocation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass == null) return mappings;

        var createMapInvocations = containingClass.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => IsCreateMapInvocation(inv, semanticModel));

        foreach (var invocation in createMapInvocations)
        {
            var typeArgs = GetCreateMapTypeArguments(invocation, semanticModel);
            if (typeArgs.sourceType != null && typeArgs.destinationType != null)
            {
                mappings.Add((typeArgs.sourceType.Name, typeArgs.destinationType.Name));
            }
        }

        return mappings;
    }

    private static bool IsCreateMapInvocation(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        return symbolInfo.Symbol is IMethodSymbol method &&
               method.Name == "CreateMap" &&
               (method.ContainingType?.Name == "IMappingExpression" ||
                method.ContainingType?.Name == "Profile");
    }

    private static IPropertySymbol[] GetMappableProperties(INamedTypeSymbol type)
    {
        var properties = new List<IPropertySymbol>();
        
        var currentType = type;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var typeProperties = currentType.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                            p.CanBeReferencedByName &&
                            !p.IsStatic &&
                            p.GetMethod != null &&
                            p.SetMethod != null &&
                            p.ContainingType.Equals(currentType, SymbolEqualityComparer.Default))
                .ToArray();

            properties.AddRange(typeProperties);
            currentType = currentType.BaseType;
        }

        return properties.ToArray();
    }

    private static bool RequiresNestedObjectMapping(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        var sourceUnderlyingType = GetUnderlyingType(sourceType);
        var destUnderlyingType = GetUnderlyingType(destinationType);

        if (SymbolEqualityComparer.Default.Equals(sourceUnderlyingType, destUnderlyingType))
        {
            return false;
        }

        if (IsBuiltInType(sourceUnderlyingType) || IsBuiltInType(destUnderlyingType))
        {
            return false;
        }

        if (IsCollectionType(sourceUnderlyingType) || IsCollectionType(destUnderlyingType))
        {
            return false;
        }

        return sourceUnderlyingType.TypeKind == TypeKind.Class && 
               destUnderlyingType.TypeKind == TypeKind.Class;
    }

    private static ITypeSymbol GetUnderlyingType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return namedType.TypeArguments[0];
        }

        if (type.CanBeReferencedByName && type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        }

        return type;
    }

    private static bool IsBuiltInType(ITypeSymbol type)
    {
        return type.SpecialType != SpecialType.None || 
               type.Name == "String" ||
               type.Name == "DateTime" ||
               type.Name == "DateTimeOffset" ||
               type.Name == "TimeSpan" ||
               type.Name == "Guid" ||
               type.Name == "Decimal";
    }

    private static bool IsCollectionType(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        return type.AllInterfaces.Any(i => 
            i.Name == "IEnumerable" || 
            i.Name == "ICollection" || 
            i.Name == "IList");
    }

    private static ExpressionStatementSyntax CreateCreateMapStatement(INamedTypeSymbol sourceType, INamedTypeSymbol destinationType)
    {
        var createMapCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.GenericName(
                SyntaxFactory.Identifier("CreateMap"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(new[]
                    {
                        SyntaxFactory.IdentifierName(sourceType.Name),
                        SyntaxFactory.IdentifierName(destinationType.Name)
                    }))))
            .WithArgumentList(SyntaxFactory.ArgumentList());

        return SyntaxFactory.ExpressionStatement(createMapCall);
    }
}
using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.ComplexMappings;

/// <summary>
///     Code fix provider for AM020: Missing nested object mapping configurations.
///     Automatically adds CreateMap statements for missing nested type mappings.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM020_NestedObjectMappingCodeFixProvider))]
[Shared]
public class AM020_NestedObjectMappingCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <summary>
    ///     Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM020");

    /// <summary>
    ///     Registers code fixes for AM020 diagnostics.
    /// </summary>
    /// <param name="context">The code fix context containing diagnostic information.</param>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        foreach (Diagnostic? diagnostic in context.Diagnostics.Where(d => FixableDiagnosticIds.Contains(d.Id)))
        {
            SyntaxNode node = operationContext.Root.FindNode(diagnostic.Location.SourceSpan);

            if (node is InvocationExpressionSyntax invocation)
            {
                var action = CodeAction.Create(
                    "Add missing nested object mapping",
                    c => AddMissingNestedMappingAsync(context.Document, invocation, operationContext.SemanticModel, c),
                    "AddMissingNestedMapping");

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
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return document;
        }

        (INamedTypeSymbol? sourceType, INamedTypeSymbol? destinationType) typeArguments =
            GetCreateMapTypeArguments(createMapInvocation, semanticModel);
        if (typeArguments.sourceType == null || typeArguments.destinationType == null)
        {
            return document;
        }

        InvocationExpressionSyntax? reverseMapInvocation =
            AutoMapperAnalysisHelpers.GetReverseMapInvocation(createMapInvocation);
        List<(INamedTypeSymbol sourceType, INamedTypeSymbol destinationType)> missingMappings =
            GetMissingNestedMappings(
                createMapInvocation,
                typeArguments.sourceType,
                typeArguments.destinationType,
                semanticModel,
                reverseMapInvocation);

        if (!missingMappings.Any())
        {
            return document;
        }

        ConstructorDeclarationSyntax? constructor =
            createMapInvocation.Ancestors().OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
        if (constructor?.Body == null)
        {
            return document;
        }

        ExpressionStatementSyntax[] newStatements = missingMappings.Select(m =>
            CreateCreateMapStatement(m.sourceType, m.destinationType)).ToArray();

        ExpressionStatementSyntax? originalStatement =
            createMapInvocation.Ancestors().OfType<ExpressionStatementSyntax>().FirstOrDefault();
        if (originalStatement == null)
        {
            return document;
        }

        int originalIndex = constructor.Body.Statements.IndexOf(originalStatement);
        BlockSyntax newBody = constructor.Body.WithStatements(
            constructor.Body.Statements.InsertRange(originalIndex + 1, newStatements));

        ConstructorDeclarationSyntax newConstructor = constructor.WithBody(newBody);
        SyntaxNode newRoot = root.ReplaceNode(constructor, newConstructor);

        return document.WithSyntaxRoot(newRoot);
    }

    private static (INamedTypeSymbol? sourceType, INamedTypeSymbol? destinationType) GetCreateMapTypeArguments(
        InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is IMethodSymbol { IsGenericMethod: true, TypeArguments.Length: 2 } method)
        {
            return (method.TypeArguments[0] as INamedTypeSymbol, method.TypeArguments[1] as INamedTypeSymbol);
        }

        return (null, null);
    }

    private static List<(INamedTypeSymbol sourceType, INamedTypeSymbol destinationType)> GetMissingNestedMappings(
        InvocationExpressionSyntax createMapInvocation,
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType,
        SemanticModel semanticModel,
        InvocationExpressionSyntax? reverseMapInvocation)
    {
        var missingMappings = new List<(INamedTypeSymbol Source, INamedTypeSymbol Destination)>();
        var registry = CreateMapRegistry.FromCompilation(semanticModel.Compilation);

        if (AM020MappingConfigurationHelpers.HasCustomConstructionOrConversion(createMapInvocation, reverseMapInvocation))
        {
            return missingMappings;
        }

        IPropertySymbol[] sourceProperties = GetMappableProperties(sourceType, requireSetter: false);
        IPropertySymbol[] destinationProperties = GetMappableProperties(destinationType, false);

        foreach (IPropertySymbol sourceProp in sourceProperties)
        {
            IPropertySymbol? destProp = destinationProperties
                .FirstOrDefault(p => string.Equals(p.Name, sourceProp.Name, StringComparison.OrdinalIgnoreCase));

            if (destProp == null || !RequiresNestedObjectMapping(sourceProp.Type, destProp.Type))
            {
                continue;
            }

            if (AM020MappingConfigurationHelpers.IsDestinationPropertyExplicitlyConfigured(
                    createMapInvocation,
                    destProp.Name,
                    reverseMapInvocation))
            {
                continue;
            }

            var sourceNestedType = GetUnderlyingType(sourceProp.Type) as INamedTypeSymbol;
            var destNestedType = GetUnderlyingType(destProp.Type) as INamedTypeSymbol;

            if (sourceNestedType != null && destNestedType != null)
            {
                if (registry.Contains(sourceNestedType, destNestedType))
                {
                    continue;
                }

                if (!missingMappings.Any(m =>
                        SymbolEqualityComparer.Default.Equals(m.Source, sourceNestedType) &&
                        SymbolEqualityComparer.Default.Equals(m.Destination, destNestedType)))
                {
                    missingMappings.Add((sourceNestedType, destNestedType));
                }
            }
        }

        return missingMappings;
    }

    private static IPropertySymbol[] GetMappableProperties(
        INamedTypeSymbol type,
        bool requireGetter = true,
        bool requireSetter = true)
    {
        var properties = new List<IPropertySymbol>();
        INamedTypeSymbol? currentType = type;

        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            properties.AddRange(currentType.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                            p.CanBeReferencedByName &&
                            !p.IsStatic &&
                            (!requireGetter || p.GetMethod != null) &&
                            (!requireSetter || p.SetMethod != null) &&
                            p.ContainingType.Equals(currentType, SymbolEqualityComparer.Default)));

            currentType = currentType.BaseType;
        }

        return properties.ToArray();
    }

    private static bool RequiresNestedObjectMapping(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        ITypeSymbol sourceUnderlying = GetUnderlyingType(sourceType);
        ITypeSymbol destUnderlying = GetUnderlyingType(destinationType);

        if (SymbolEqualityComparer.Default.Equals(sourceUnderlying, destUnderlying))
        {
            return false;
        }

        if (IsBuiltInType(sourceUnderlying) || IsBuiltInType(destUnderlying))
        {
            return false;
        }

        if (IsCollectionType(sourceUnderlying) || IsCollectionType(destUnderlying))
        {
            return false;
        }

        return (sourceUnderlying.TypeKind == TypeKind.Class || sourceUnderlying.TypeKind == TypeKind.Struct || sourceUnderlying.TypeKind == TypeKind.Interface) &&
               (destUnderlying.TypeKind == TypeKind.Class || destUnderlying.TypeKind == TypeKind.Struct || destUnderlying.TypeKind == TypeKind.Interface);
    }

    private static ITypeSymbol GetUnderlyingType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } namedType)
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
               type.Name is "String" or "DateTime" or "DateTimeOffset" or "TimeSpan" or "Guid" or "Decimal";
    }

    private static bool IsCollectionType(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        return type.AllInterfaces.Any(i => i.Name is "IEnumerable" or "ICollection" or "IList");
    }

    private static ExpressionStatementSyntax CreateCreateMapStatement(INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType)
    {
        InvocationExpressionSyntax createMapCall = SyntaxFactory.InvocationExpression(
                SyntaxFactory.GenericName(SyntaxFactory.Identifier("CreateMap"))
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

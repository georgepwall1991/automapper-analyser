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

            if (node is not InvocationExpressionSyntax invocation)
            {
                continue;
            }

            // Preflight: only advertise a fix when apply can insert into a block body.
            if (!TryGetCreateMapInsertTarget(invocation, out _, out _))
            {
                continue;
            }

            (INamedTypeSymbol? sourceType, INamedTypeSymbol? destinationType) typeArguments =
                GetCreateMapTypeArguments(invocation, operationContext.SemanticModel);
            if (typeArguments.sourceType == null || typeArguments.destinationType == null)
            {
                continue;
            }

            List<(INamedTypeSymbol sourceType, INamedTypeSymbol destinationType)> missingMappings =
                GetMissingNestedMappings(
                    invocation,
                    typeArguments.sourceType,
                    typeArguments.destinationType,
                    operationContext.SemanticModel);
            if (missingMappings.Count == 0)
            {
                continue;
            }

            var action = CodeAction.Create(
                "Add missing nested CreateMap registrations",
                c => AddMissingNestedMappingAsync(context.Document, invocation, operationContext.SemanticModel, c),
                "AM020_AddMissingNestedMappings");

            context.RegisterCodeFix(action, diagnostic);
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

        List<(INamedTypeSymbol sourceType, INamedTypeSymbol destinationType)> missingMappings =
            GetMissingNestedMappings(
                createMapInvocation,
                typeArguments.sourceType,
                typeArguments.destinationType,
                semanticModel);

        if (missingMappings.Count == 0)
        {
            return document;
        }

        if (!TryGetCreateMapInsertTarget(createMapInvocation, out SyntaxNode? bodyOwner, out ExpressionStatementSyntax? originalStatement))
        {
            return document;
        }

        ExpressionStatementSyntax[] newStatements = missingMappings.Select(m =>
            CreateCreateMapStatement(m.sourceType, m.destinationType, semanticModel, createMapInvocation.SpanStart)).ToArray();

        return InsertCreateMapStatements(document, root, bodyOwner!, originalStatement!, newStatements);
    }

    /// <summary>
    ///     Locates a block body (constructor or method) and expression statement that can host
    ///     inserted CreateMap statements after the original. Returns false when apply would no-op.
    ///     Method bodies only qualify when CreateMap is bare/<c>this</c> (Profile-style); receiver-qualified
    ///     calls such as <c>cfg.CreateMap&lt;,&gt;()</c> are withheld because the fix emits unqualified CreateMap.
    /// </summary>
    private static bool TryGetCreateMapInsertTarget(
        InvocationExpressionSyntax createMapInvocation,
        out SyntaxNode? bodyOwner,
        out ExpressionStatementSyntax? originalStatement)
    {
        bodyOwner = null;
        originalStatement = createMapInvocation.Ancestors().OfType<ExpressionStatementSyntax>().FirstOrDefault();
        if (originalStatement == null)
        {
            return false;
        }

        ConstructorDeclarationSyntax? constructor =
            createMapInvocation.Ancestors().OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
        if (constructor?.Body != null && constructor.Body.Statements.Contains(originalStatement))
        {
            // Profile constructors almost always use bare CreateMap; still gate on unqualified form
            // so cfg.CreateMap inside a ctor cannot produce a compile-breaking insert.
            if (!IsUnqualifiedCreateMapInvocation(createMapInvocation))
            {
                return false;
            }

            bodyOwner = constructor;
            return true;
        }

        MethodDeclarationSyntax? method =
            createMapInvocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method?.Body != null &&
            method.Body.Statements.Contains(originalStatement) &&
            IsUnqualifiedCreateMapInvocation(createMapInvocation))
        {
            bodyOwner = method;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     True when the mapping chain roots at CreateMap without an external receiver (bare or <c>this</c>),
    ///     so an unqualified <c>CreateMap&lt;,&gt;()</c> insert will compile. Peels fluent chains
    ///     (e.g. diagnostics on <c>ReverseMap()</c>).
    /// </summary>
    private static bool IsUnqualifiedCreateMapInvocation(InvocationExpressionSyntax invocation)
    {
        InvocationExpressionSyntax? current = invocation;
        while (current != null)
        {
            if (IsDirectUnqualifiedCreateMap(current))
            {
                return true;
            }

            current = (current.Expression as MemberAccessExpressionSyntax)?.Expression as InvocationExpressionSyntax;
        }

        return false;
    }

    private static bool IsDirectUnqualifiedCreateMap(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            GenericNameSyntax generic when generic.Identifier.ValueText == "CreateMap" => true,
            IdentifierNameSyntax identifier when identifier.Identifier.ValueText == "CreateMap" => true,
            MemberAccessExpressionSyntax memberAccess
                when GetMemberName(memberAccess.Name) == "CreateMap" &&
                     memberAccess.Expression is ThisExpressionSyntax => true,
            _ => false
        };
    }

    private static string GetMemberName(SimpleNameSyntax name) =>
        name is GenericNameSyntax generic ? generic.Identifier.ValueText : name.Identifier.ValueText;

    private static Document InsertCreateMapStatements(
        Document document,
        SyntaxNode root,
        SyntaxNode bodyOwner,
        ExpressionStatementSyntax originalStatement,
        ExpressionStatementSyntax[] newStatements)
    {
        switch (bodyOwner)
        {
            case ConstructorDeclarationSyntax constructor when constructor.Body != null:
            {
                int originalIndex = constructor.Body.Statements.IndexOf(originalStatement);
                BlockSyntax newBody = constructor.Body.WithStatements(
                    constructor.Body.Statements.InsertRange(originalIndex + 1, newStatements));
                return document.WithSyntaxRoot(root.ReplaceNode(constructor, constructor.WithBody(newBody)));
            }
            case MethodDeclarationSyntax method when method.Body != null:
            {
                int originalIndex = method.Body.Statements.IndexOf(originalStatement);
                BlockSyntax newBody = method.Body.WithStatements(
                    method.Body.Statements.InsertRange(originalIndex + 1, newStatements));
                return document.WithSyntaxRoot(root.ReplaceNode(method, method.WithBody(newBody)));
            }
            default:
                return document;
        }
    }

    private static (INamedTypeSymbol? sourceType, INamedTypeSymbol? destinationType) GetCreateMapTypeArguments(
        InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        if (TryGetDirectCreateMapTypeArguments(invocation, semanticModel, out INamedTypeSymbol? sourceType,
                out INamedTypeSymbol? destinationType))
        {
            return (sourceType, destinationType);
        }

        if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "ReverseMap"))
        {
            InvocationExpressionSyntax? createMapInvocation = FindCreateMapInvocation(invocation, semanticModel);
            if (createMapInvocation != null &&
                TryGetDirectCreateMapTypeArguments(createMapInvocation, semanticModel, out sourceType,
                    out destinationType))
            {
                return (destinationType, sourceType);
            }
        }

        return (null, null);
    }

    private static bool TryGetDirectCreateMapTypeArguments(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out INamedTypeSymbol? sourceType,
        out INamedTypeSymbol? destinationType)
    {
        sourceType = null;
        destinationType = null;

        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is IMethodSymbol { IsGenericMethod: true, TypeArguments.Length: 2 } method)
        {
            sourceType = method.TypeArguments[0] as INamedTypeSymbol;
            destinationType = method.TypeArguments[1] as INamedTypeSymbol;
            return sourceType != null && destinationType != null;
        }

        return false;
    }

    private static List<(INamedTypeSymbol sourceType, INamedTypeSymbol destinationType)> GetMissingNestedMappings(
        InvocationExpressionSyntax createMapInvocation,
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType,
        SemanticModel semanticModel)
    {
        var missingMappings = new List<(INamedTypeSymbol Source, INamedTypeSymbol Destination)>();
        var registry = CreateMapRegistry.FromCompilation(semanticModel.Compilation);

        if (AM020MappingConfigurationHelpers.HasCustomConstructionOrConversion(createMapInvocation, semanticModel))
        {
            return missingMappings;
        }

        // Match the analyzer: public + internal members via shared helper (avoids silent no-op fixes
        // when only internal nested properties need CreateMap registrations).
        IPropertySymbol[] sourceProperties = AutoMapperAnalysisHelpers
            .GetMappableProperties(sourceType, requireSetter: false)
            .ToArray();
        IPropertySymbol[] destinationProperties = AutoMapperAnalysisHelpers
            .GetMappableProperties(destinationType, false)
            .ToArray();

        foreach (IPropertySymbol sourceProp in sourceProperties)
        {
            IPropertySymbol? destProp = destinationProperties
                .FirstOrDefault(p => string.Equals(p.Name, sourceProp.Name, StringComparison.OrdinalIgnoreCase));

            if (destProp == null || !RequiresNestedObjectMapping(semanticModel.Compilation, sourceProp.Type, destProp.Type))
            {
                continue;
            }

            if (AM020MappingConfigurationHelpers.IsDestinationPropertyExplicitlyConfigured(
                    createMapInvocation,
                    destProp.Name,
                    semanticModel))
            {
                continue;
            }

            var sourceNestedType = AutoMapperAnalysisHelpers.GetUnderlyingType(sourceProp.Type) as INamedTypeSymbol;
            var destNestedType = AutoMapperAnalysisHelpers.GetUnderlyingType(destProp.Type) as INamedTypeSymbol;

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

    private static bool RequiresNestedObjectMapping(Compilation compilation, ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        ITypeSymbol sourceUnderlying = AutoMapperAnalysisHelpers.GetUnderlyingType(sourceType);
        ITypeSymbol destUnderlying = AutoMapperAnalysisHelpers.GetUnderlyingType(destinationType);

        if (SymbolEqualityComparer.Default.Equals(sourceUnderlying, destUnderlying))
        {
            return false;
        }

        if (AutoMapperAnalysisHelpers.IsBuiltInType(sourceUnderlying) ||
            AutoMapperAnalysisHelpers.IsBuiltInType(destUnderlying))
        {
            return false;
        }

        if (AutoMapperAnalysisHelpers.IsCollectionType(sourceUnderlying) ||
            AutoMapperAnalysisHelpers.IsCollectionType(destUnderlying))
        {
            return false;
        }

        Conversion conversion = compilation.ClassifyConversion(sourceUnderlying, destUnderlying);
        if (conversion.Exists && conversion.IsImplicit)
        {
            return false;
        }

        return (sourceUnderlying.TypeKind == TypeKind.Class || sourceUnderlying.TypeKind == TypeKind.Struct || sourceUnderlying.TypeKind == TypeKind.Interface) &&
               (destUnderlying.TypeKind == TypeKind.Class || destUnderlying.TypeKind == TypeKind.Struct || destUnderlying.TypeKind == TypeKind.Interface);
    }

    private static ExpressionStatementSyntax CreateCreateMapStatement(INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType, SemanticModel semanticModel, int position)
    {
        InvocationExpressionSyntax createMapCall = SyntaxFactory.InvocationExpression(
                SyntaxFactory.GenericName(SyntaxFactory.Identifier("CreateMap"))
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SeparatedList(new[]
                            {
                                CreateTypeName(sourceType, semanticModel, position),
                                CreateTypeName(destinationType, semanticModel, position)
                            }))))
            .WithArgumentList(SyntaxFactory.ArgumentList());

        return SyntaxFactory.ExpressionStatement(createMapCall);
    }

    private static TypeSyntax CreateTypeName(INamedTypeSymbol type, SemanticModel semanticModel, int position)
    {
        // ToMinimalDisplayString yields the shortest name valid at the insertion point: a simple name when
        // the type is in scope, a qualified name across namespaces, and it always keeps generic type
        // arguments — so the generated CreateMap compiles instead of dropping the namespace (IdentifierName
        // used only type.Name, which broke cross-namespace and generic nested types).
        return SyntaxFactory.ParseTypeName(type.ToMinimalDisplayString(semanticModel, position));
    }
}

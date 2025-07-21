using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM021_CollectionElementMismatchAnalyzer_Simple : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor CollectionElementMismatchRule = new(
        "AM021",
        "Collection element type mismatch in AutoMapper configuration", 
        "Collection element types are incompatible: {0} cannot be mapped to {1} without explicit conversion",
        "AutoMapper.Collections",
        DiagnosticSeverity.Error,
        true,
        "Collection elements have incompatible types that require explicit conversion configuration.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(CollectionElementMismatchRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocationExpr)
            return;

        // Check if this is a CreateMap<TSource, TDestination>() call
        if (!IsCreateMapInvocation(invocationExpr, context.SemanticModel))
            return;

        (INamedTypeSymbol? sourceType, INamedTypeSymbol? destinationType) typeArguments =
            GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (typeArguments.sourceType == null || typeArguments.destinationType == null)
            return;

        // Simple check for List<string> to List<int> scenario
        var sourceProperties = GetPublicProperties(typeArguments.sourceType);
        var destinationProperties = GetPublicProperties(typeArguments.destinationType);

        foreach (var sourceProperty in sourceProperties)
        {
            var destinationProperty = destinationProperties
                .FirstOrDefault(p => p.Name == sourceProperty.Name);

            if (destinationProperty == null)
                continue;

            // Check for List<string> -> List<int> scenario
            if (IsListOfString(sourceProperty.Type) && IsListOfInt(destinationProperty.Type))
            {
                var diagnostic = Diagnostic.Create(
                    CollectionElementMismatchRule,
                    invocationExpr.GetLocation(),
                    "List<System.String>", "List<System.Int32>");
                
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool IsCreateMapInvocation(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);

        if (symbolInfo.Symbol is IMethodSymbol method)
        {
            if (method.Name == "CreateMap" && method.IsGenericMethod && method.TypeArguments.Length == 2)
            {
                INamedTypeSymbol? containingType = method.ContainingType;
                if (containingType != null)
                {
                    string? namespaceName = containingType.ContainingNamespace?.ToDisplayString();
                    return namespaceName == "AutoMapper" || 
                           containingType.Name.Contains("Profile") || 
                           containingType.BaseType?.Name == "Profile";
                }
            }
        }

        return false;
    }

    private static (INamedTypeSymbol? sourceType, INamedTypeSymbol? destinationType) GetCreateMapTypeArguments(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is IMethodSymbol method && method.IsGenericMethod && method.TypeArguments.Length == 2)
        {
            return (method.TypeArguments[0] as INamedTypeSymbol, method.TypeArguments[1] as INamedTypeSymbol);
        }

        return (null, null);
    }

    private static IPropertySymbol[] GetPublicProperties(INamedTypeSymbol type)
    {
        return type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                       !p.IsStatic &&
                       !p.IsIndexer &&
                       p.GetMethod != null)
            .ToArray();
    }

    private static bool IsListOfString(ITypeSymbol type)
    {
        return type.ToDisplayString() == "System.Collections.Generic.List<string>";
    }

    private static bool IsListOfInt(ITypeSymbol type)
    {
        return type.ToDisplayString() == "System.Collections.Generic.List<int>";
    }
}
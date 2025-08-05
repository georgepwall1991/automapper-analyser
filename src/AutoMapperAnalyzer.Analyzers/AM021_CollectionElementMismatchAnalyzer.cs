using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
///     Analyzer for detecting collection element type mismatch issues in AutoMapper configurations
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM021_CollectionElementMismatchAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     AM021: Collection element type incompatibility
    /// </summary>
    public static readonly DiagnosticDescriptor CollectionElementIncompatibilityRule = new(
        "AM021",
        "Collection element type incompatibility in AutoMapper configuration", 
        "Property '{0}' has incompatible collection element types: {1}.{0} ({2}) elements cannot be mapped to {3}.{0} ({4}) elements without explicit conversion",
        "AutoMapper.Collections",
        DiagnosticSeverity.Warning,
        true,
        "Collection properties have compatible collection types but incompatible element types that may require custom mapping.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(CollectionElementIncompatibilityRule);

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
        if (!AutoMapperAnalysisHelpers.IsCreateMapInvocation(invocationExpr, context.SemanticModel))
            return;

        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) typeArguments =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (typeArguments.sourceType == null || typeArguments.destinationType == null)
            return;

        // Analyze collection element compatibility for property mappings
        AnalyzeCollectionElementCompatibility(context, invocationExpr, typeArguments.sourceType,
            typeArguments.destinationType);
    }

    private static void AnalyzeCollectionElementCompatibility(SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation, ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType);
        var destinationProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destinationType);

        foreach (IPropertySymbol sourceProperty in sourceProperties)
        {
            IPropertySymbol? destinationProperty = destinationProperties
                .FirstOrDefault(p => p.Name == sourceProperty.Name);

            if (destinationProperty == null)
                continue;

            // Check for explicit property mapping that might handle collection conversion
            if (AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember(invocation, sourceProperty.Name, context.SemanticModel))
                continue;

            // Check if both properties are collections and analyze element types
            if (AutoMapperAnalysisHelpers.IsCollectionType(sourceProperty.Type) && AutoMapperAnalysisHelpers.IsCollectionType(destinationProperty.Type))
            {
                AnalyzeCollectionElementTypes(context, invocation, sourceProperty, destinationProperty,
                    sourceType, destinationType);
            }
        }
    }

    private static void AnalyzeCollectionElementTypes(SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IPropertySymbol sourceProperty,
        IPropertySymbol destinationProperty,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        // Get element types from collections
        ITypeSymbol? sourceElementType = AutoMapperAnalysisHelpers.GetCollectionElementType(sourceProperty.Type);
        ITypeSymbol? destElementType = AutoMapperAnalysisHelpers.GetCollectionElementType(destinationProperty.Type);

        if (sourceElementType == null || destElementType == null)
            return;

        // Check if element types are compatible
        if (!AutoMapperAnalysisHelpers.AreTypesCompatible(sourceElementType, destElementType))
        {
            var diagnostic = Diagnostic.Create(
                CollectionElementIncompatibilityRule,
                invocation.GetLocation(),
                sourceProperty.Name,
                GetTypeName(sourceType),
                sourceElementType.ToDisplayString(),
                GetTypeName(destinationType),
                destElementType.ToDisplayString());

            context.ReportDiagnostic(diagnostic);
        }
    }


    private static bool HasExplicitPropertyMapping(InvocationExpressionSyntax invocation, string propertyName)
    {
        // Look for chained ForMember calls starting from this invocation
        var current = invocation.Parent;
        while (current != null)
        {
            if (current is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.ValueText == "ForMember" &&
                memberAccess.Parent is InvocationExpressionSyntax forMemberCall)
            {
                var args = forMemberCall.ArgumentList.Arguments;
                if (args.Count > 0)
                {
                    var firstArg = args[0].Expression.ToString();
                    // Look for patterns like "dest => dest.PropertyName" or "x => x.PropertyName"
                    if (firstArg.Contains($".{propertyName}"))
                        return true;
                }
            }
            current = current.Parent;
        }

        // Also check within the same method for ForMember calls
        var method = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method == null) return false;

        var forMemberCalls = method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax member &&
                         member.Name.Identifier.ValueText == "ForMember");

        foreach (var call in forMemberCalls)
        {
            var args = call.ArgumentList.Arguments;
            if (args.Count > 0)
            {
                var firstArg = args[0].Expression.ToString();
                // Look for patterns like "dest => dest.PropertyName"
                if (firstArg.Contains($".{propertyName}"))
                    return true;
            }
        }

        return false;
    }

    private static bool HasElementTypeMapping(InvocationExpressionSyntax invocation, ITypeSymbol sourceElementType, ITypeSymbol destElementType)
    {
        // Look for CreateMap<SourceElementType, DestElementType> calls in the same method
        var method = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method == null) return false;

        // Get the full type names for comparison
        var sourceElementTypeName = sourceElementType.ToDisplayString();
        var destElementTypeName = destElementType.ToDisplayString();

        // Find all CreateMap calls in the method by looking at the entire method text
        var methodText = method.ToString();
        
        // Simple check - look for CreateMap<SourceElementType, DestElementType> pattern in the text
        var createMapPattern = $"CreateMap<{sourceElementTypeName}, {destElementTypeName}>";
        if (methodText.Contains(createMapPattern))
            return true;
            
        // Also check with just the type names (without namespace)
        var sourceSimpleName = sourceElementType.Name;
        var destSimpleName = destElementType.Name;
        var simpleCreateMapPattern = $"CreateMap<{sourceSimpleName}, {destSimpleName}>";
        if (methodText.Contains(simpleCreateMapPattern))
            return true;

        return false;
    }



    /// <summary>
    /// Gets the type name from an ITypeSymbol.
    /// </summary>
    /// <param name="type">The type symbol.</param>
    /// <returns>The type name.</returns>
    private static string GetTypeName(ITypeSymbol type)
    {
        return type.Name;
    }
}
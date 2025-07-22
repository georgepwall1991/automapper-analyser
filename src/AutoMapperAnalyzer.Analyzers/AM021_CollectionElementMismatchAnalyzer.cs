using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

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
        if (!IsCreateMapInvocation(invocationExpr, context.SemanticModel))
            return;

        (INamedTypeSymbol? sourceType, INamedTypeSymbol? destinationType) typeArguments =
            GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (typeArguments.sourceType == null || typeArguments.destinationType == null)
            return;

        // Analyze collection element compatibility for property mappings
        AnalyzeCollectionElementCompatibility(context, invocationExpr, typeArguments.sourceType,
            typeArguments.destinationType);
    }

    private static void AnalyzeCollectionElementCompatibility(SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation, INamedTypeSymbol sourceType, INamedTypeSymbol destinationType)
    {
        IPropertySymbol[] sourceProperties = GetPublicProperties(sourceType);
        IPropertySymbol[] destinationProperties = GetPublicProperties(destinationType);

        foreach (IPropertySymbol sourceProperty in sourceProperties)
        {
            IPropertySymbol? destinationProperty = destinationProperties
                .FirstOrDefault(p => p.Name == sourceProperty.Name);

            if (destinationProperty == null)
                continue;

            // Check for explicit property mapping that might handle collection conversion
            if (HasExplicitPropertyMapping(invocation, sourceProperty.Name))
                continue;

            // Check if both properties are collections and analyze element types
            if (IsCollectionType(sourceProperty.Type) && IsCollectionType(destinationProperty.Type))
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
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType)
    {
        // Get element types from collections
        ITypeSymbol? sourceElementType = GetCollectionElementType(sourceProperty.Type);
        ITypeSymbol? destElementType = GetCollectionElementType(destinationProperty.Type);

        if (sourceElementType == null || destElementType == null)
            return;

        // Check if element types are compatible
        if (!AreElementTypesCompatible(sourceElementType, destElementType))
        {
            var diagnostic = Diagnostic.Create(
                CollectionElementIncompatibilityRule,
                invocation.GetLocation(),
                sourceProperty.Name,
                sourceType.Name,
                sourceElementType.ToDisplayString(),
                destinationType.Name,
                destElementType.ToDisplayString());

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsCreateMapInvocation(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        // Get the symbol info to check if this is really AutoMapper's CreateMap method
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);

        if (symbolInfo.Symbol is IMethodSymbol method)
        {
            // Check if method name is CreateMap and it's generic with 2 type parameters
            if (method.Name == "CreateMap" && method.IsGenericMethod && method.TypeArguments.Length == 2)
            {
                // Additional check: see if it's from AutoMapper (check containing type or namespace)
                INamedTypeSymbol? containingType = method.ContainingType;
                if (containingType != null)
                {
                    // Check if the containing type is likely from AutoMapper
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

    private static IPropertySymbol[] GetPublicProperties(ITypeSymbol type)
    {
        return type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && 
                        p.GetMethod != null && 
                        p.SetMethod != null)
            .ToArray();
    }

    private static bool HasExplicitPropertyMapping(InvocationExpressionSyntax invocation, string propertyName)
    {
        // Simple check - look for ForMember calls in the same method
        var method = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method == null) return false;

        // Look for ForMember calls that mention this property
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
                if (firstArg.Contains(propertyName))
                    return true;
            }
        }

        return false;
    }

    private static ITypeSymbol? GetCollectionElementType(ITypeSymbol type)
    {
        // Handle arrays
        if (type is IArrayTypeSymbol arrayType)
            return arrayType.ElementType;

        // Handle generic collections
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            // For List<T>, IEnumerable<T>, etc., get the first type argument
            if (namedType.TypeArguments.Length > 0)
                return namedType.TypeArguments[0];
        }

        return null;
    }


    private static bool AreElementTypesCompatible(ITypeSymbol sourceType, ITypeSymbol destType)
    {
        // Same type
        if (SymbolEqualityComparer.Default.Equals(sourceType, destType))
            return true;

        // Check for basic type compatibility (e.g., numeric conversions)
        if (AreNumericTypesCompatible(sourceType.Name, destType.Name))
            return true;

        return false;
    }

    private static bool AreNumericTypesCompatible(string from, string to)
    {
        string[] numericTypes = { "Byte", "SByte", "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64", "Single", "Double", "Decimal" };
        bool fromIsNumeric = numericTypes.Contains(from);
        bool toIsNumeric = numericTypes.Contains(to);
        
        return fromIsNumeric && toIsNumeric;
    }

    private static bool IsCollectionType(ITypeSymbol type)
    {
        // Check for arrays
        if (type.TypeKind == TypeKind.Array)
            return true;

        if (type is INamedTypeSymbol namedType)
        {
            // Check for generic collection types
            if (namedType.IsGenericType)
            {
                string genericTypeName = namedType.ConstructedFrom.ToDisplayString();
                return genericTypeName.Contains("List") ||
                       genericTypeName.Contains("IEnumerable") ||
                       genericTypeName.Contains("ICollection") ||
                       genericTypeName.Contains("HashSet") ||
                       genericTypeName.Contains("Queue") ||
                       genericTypeName.Contains("Stack");
            }
        }

        return false;
    }
}
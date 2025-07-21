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
    ///     AM021: Collection element types are incompatible
    /// </summary>
    public static readonly DiagnosticDescriptor CollectionElementMismatchRule = new(
        "AM021",
        "Collection element type mismatch in AutoMapper configuration",
        "Collection element types are incompatible: {0} cannot be mapped to {1} without explicit conversion",
        "AutoMapper.Collections",
        DiagnosticSeverity.Error,
        true,
        "Collection elements have incompatible types that require explicit conversion configuration.");

    /// <summary>
    ///     AM021: Collection element mapping missing for complex types
    /// </summary>
    public static readonly DiagnosticDescriptor CollectionElementMappingMissingRule = new(
        "AM021",
        "Collection element mapping missing in AutoMapper configuration",
        "Collection element mapping missing: {0} to {1}. Consider adding CreateMap<{0}, {1}>()",
        "AutoMapper.Collections",
        DiagnosticSeverity.Warning,
        true,
        "Complex collection element types require explicit mapping configuration.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(CollectionElementMismatchRule, CollectionElementMappingMissingRule);

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
        // Get all accessible properties from source and destination types
        var sourceProperties = GetAccessibleProperties(sourceType);
        var destinationProperties = GetAccessibleProperties(destinationType);

        // Get explicit property mappings from ForMember calls
        var explicitMappings = GetExplicitPropertyMappings(invocation);

        foreach (var sourceProperty in sourceProperties)
        {
            // Skip if this property has explicit mapping configuration
            if (explicitMappings.Contains(sourceProperty.Name))
                continue;

            // Find corresponding destination property (case-insensitive)
            var destinationProperty = destinationProperties.FirstOrDefault(dp =>
                string.Equals(dp.Name, sourceProperty.Name, StringComparison.OrdinalIgnoreCase));

            if (destinationProperty == null)
                continue;

            AnalyzeCollectionElementTypes(context, invocation, sourceProperty, destinationProperty,
                sourceType, destinationType);
        }
    }

    private static void AnalyzeCollectionElementTypes(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IPropertySymbol sourceProperty,
        IPropertySymbol destinationProperty,
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType)
    {
        var sourceElementType = GetCollectionElementType(sourceProperty.Type);
        var destElementType = GetCollectionElementType(destinationProperty.Type);

        // Skip if either property is not a collection
        if (sourceElementType == null || destElementType == null)
            return;

        // Check element type compatibility
        if (!AreTypesCompatible(sourceElementType, destElementType, context.SemanticModel))
        {
            // Check if it's a simple type mismatch vs complex type mapping missing
            if (IsSimpleType(sourceElementType) || IsSimpleType(destElementType))
            {
                // Simple type mismatch - error
                var sourceTypeName = GetCollectionTypeName(sourceProperty.Type);
                var destTypeName = GetCollectionTypeName(destinationProperty.Type);
                
                var diagnostic = Diagnostic.Create(
                    CollectionElementMismatchRule,
                    invocation.GetLocation(),
                    sourceTypeName,
                    destTypeName);
                
                context.ReportDiagnostic(diagnostic);
            }
            else
            {
                // Complex type mapping missing - warning
                var diagnostic = Diagnostic.Create(
                    CollectionElementMappingMissingRule,
                    invocation.GetLocation(),
                    sourceElementType.Name,
                    destElementType.Name);
                
                context.ReportDiagnostic(diagnostic);
            }
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

    private static IEnumerable<IPropertySymbol> GetAccessibleProperties(INamedTypeSymbol type)
    {
        var properties = new List<IPropertySymbol>();
        var currentType = type;

        // Walk up the inheritance chain to get all properties including inherited ones
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var typeProperties = currentType.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                           !p.IsStatic &&
                           !p.IsIndexer &&
                           p.GetMethod != null);

            properties.AddRange(typeProperties);
            currentType = currentType.BaseType;
        }

        return properties;
    }

    private static HashSet<string> GetExplicitPropertyMappings(InvocationExpressionSyntax invocation)
    {
        var mappings = new HashSet<string>();
        
        // Look for chained ForMember calls in the same statement
        var parent = invocation.Parent;
        while (parent != null)
        {
            if (parent is InvocationExpressionSyntax chainedCall &&
                chainedCall.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.ValueText == "ForMember")
            {
                // Extract property name from ForMember lambda
                var propertyName = ExtractPropertyNameFromForMember(chainedCall);
                if (!string.IsNullOrEmpty(propertyName))
                    mappings.Add(propertyName);
            }
            parent = parent.Parent;
        }

        return mappings;
    }

    private static string? ExtractPropertyNameFromForMember(InvocationExpressionSyntax forMemberCall)
    {
        if (forMemberCall.ArgumentList.Arguments.Count == 0)
            return null;

        var firstArg = forMemberCall.ArgumentList.Arguments[0];
        if (firstArg.Expression is SimpleLambdaExpressionSyntax lambda &&
            lambda.Body is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.ValueText;
        }

        return null;
    }

    private static ITypeSymbol? GetCollectionElementType(ITypeSymbol type)
    {
        // Handle arrays
        if (type is IArrayTypeSymbol arrayType)
            return arrayType.ElementType;

        // Handle generic collections (IEnumerable<T>, List<T>, etc.)
        if (type is INamedTypeSymbol namedType)
        {
            // Check if it implements IEnumerable<T>
            var enumerableInterface = namedType.AllInterfaces.FirstOrDefault(i =>
                i.IsGenericType &&
                i.ConstructedFrom.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);

            if (enumerableInterface != null)
                return enumerableInterface.TypeArguments[0];

            // Direct generic type check
            if (namedType.IsGenericType && namedType.TypeArguments.Length > 0)
                return namedType.TypeArguments[0];
        }

        return null;
    }

    private static string GetCollectionTypeName(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arrayType)
            return $"{arrayType.ElementType.Name}[]";

        return type.Name;
    }

    private static bool AreTypesCompatible(ITypeSymbol sourceType, ITypeSymbol destType, SemanticModel semanticModel)
    {
        // Same type
        if (SymbolEqualityComparer.Default.Equals(sourceType, destType))
            return true;

        // Check if conversion exists
        var conversion = semanticModel.Compilation.ClassifyConversion(sourceType, destType);
        if (conversion.IsImplicit)
            return true;

        // Special cases for numeric types
        if (IsNumericType(sourceType) && IsNumericType(destType))
            return true;

        return false;
    }

    private static bool IsSimpleType(ITypeSymbol type)
    {
        return type.SpecialType != SpecialType.None ||
               type.TypeKind == TypeKind.Enum ||
               IsNumericType(type) ||
               type.Name == "String" ||
               type.Name == "DateTime" ||
               type.Name == "Guid";
    }

    private static bool IsNumericType(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Byte or
            SpecialType.System_SByte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Int32 or
            SpecialType.System_UInt32 or
            SpecialType.System_Int64 or
            SpecialType.System_UInt64 or
            SpecialType.System_Single or
            SpecialType.System_Double or
            SpecialType.System_Decimal => true,
            _ => false
        };
    }
}
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
///     Analyzer for detecting infinite recursion risks in AutoMapper configurations
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM022_InfiniteRecursionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     AM022: Infinite recursion risk detected
    /// </summary>
    public static readonly DiagnosticDescriptor InfiniteRecursionRiskRule = new(
        "AM022",
        "Infinite recursion risk in AutoMapper configuration",
        "Potential infinite recursion detected: {0} to {1} mapping may cause stack overflow due to circular references",
        "AutoMapper.Recursion",
        DiagnosticSeverity.Warning,
        true,
        "Circular object references can cause infinite recursion during mapping. Consider using MaxDepth() or ignoring circular properties.");

    /// <summary>
    ///     AM022: Self-referencing type detected
    /// </summary>
    public static readonly DiagnosticDescriptor SelfReferencingTypeRule = new(
        "AM022",
        "Self-referencing type in AutoMapper configuration",
        "Self-referencing type detected: {0} contains properties of its own type, which may cause infinite recursion",
        "AutoMapper.Recursion",
        DiagnosticSeverity.Warning,
        true,
        "Self-referencing types can cause infinite recursion during mapping. Consider using MaxDepth() or ignoring self-referencing properties.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(InfiniteRecursionRiskRule, SelfReferencingTypeRule);

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

        // Check if MaxDepth is configured or circular properties are ignored
        if (HasMaxDepthConfiguration(invocationExpr) || HasCircularPropertyIgnored(invocationExpr, typeArguments.sourceType, typeArguments.destinationType))
            return;

        // Analyze for infinite recursion risks
        AnalyzeRecursionRisk(context, invocationExpr, typeArguments.sourceType, typeArguments.destinationType);
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

    private static bool HasMaxDepthConfiguration(InvocationExpressionSyntax invocation)
    {
        // Look for chained MaxDepth calls
        var parent = invocation.Parent;
        while (parent != null)
        {
            if (parent is InvocationExpressionSyntax chainedCall &&
                chainedCall.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.ValueText == "MaxDepth")
            {
                return true;
            }
            parent = parent.Parent;
        }

        return false;
    }

    private static bool HasCircularPropertyIgnored(InvocationExpressionSyntax invocation, 
        INamedTypeSymbol sourceType, INamedTypeSymbol destinationType)
    {
        var circularProperties = FindCircularProperties(sourceType, destinationType);
        var ignoredProperties = GetIgnoredProperties(invocation);

        // Check if any circular property is explicitly ignored
        return circularProperties.Any(prop => ignoredProperties.Contains(prop));
    }

    private static HashSet<string> GetIgnoredProperties(InvocationExpressionSyntax invocation)
    {
        var ignoredProperties = new HashSet<string>();
        
        // Look for chained ForMember calls with Ignore()
        var parent = invocation.Parent;
        while (parent != null)
        {
            if (parent is InvocationExpressionSyntax chainedCall &&
                chainedCall.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.ValueText == "ForMember")
            {
                // Check if this ForMember call has Ignore()
                if (HasIgnoreConfiguration(chainedCall))
                {
                    var propertyName = ExtractPropertyNameFromForMember(chainedCall);
                    if (!string.IsNullOrEmpty(propertyName))
                        ignoredProperties.Add(propertyName);
                }
            }
            parent = parent.Parent;
        }

        return ignoredProperties;
    }

    private static bool HasIgnoreConfiguration(InvocationExpressionSyntax forMemberCall)
    {
        // Look for lambda expression that contains opt.Ignore() call
        if (forMemberCall.ArgumentList.Arguments.Count >= 2)
        {
            var secondArg = forMemberCall.ArgumentList.Arguments[1];
            // Simple check for "Ignore" text in the argument
            return secondArg.ToString().Contains("Ignore");
        }

        return false;
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

    private static void AnalyzeRecursionRisk(SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation, INamedTypeSymbol sourceType, INamedTypeSymbol destinationType)
    {
        // Check for self-referencing types
        if (IsSelfReferencing(sourceType) || IsSelfReferencing(destinationType))
        {
            var diagnostic = Diagnostic.Create(
                SelfReferencingTypeRule,
                invocation.GetLocation(),
                sourceType.Name,
                destinationType.Name);
            
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // Check for circular references between types
        if (HasCircularReference(sourceType, destinationType))
        {
            var diagnostic = Diagnostic.Create(
                InfiniteRecursionRiskRule,
                invocation.GetLocation(),
                sourceType.Name,
                destinationType.Name);
            
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsSelfReferencing(INamedTypeSymbol type)
    {
        var properties = GetAccessibleProperties(type);
        
        foreach (var property in properties)
        {
            // Check direct self-reference
            if (SymbolEqualityComparer.Default.Equals(property.Type, type))
                return true;

            // Check collection of self-type
            var elementType = GetCollectionElementType(property.Type);
            if (elementType != null && SymbolEqualityComparer.Default.Equals(elementType, type))
                return true;
        }

        return false;
    }

    private static bool HasCircularReference(INamedTypeSymbol sourceType, INamedTypeSymbol destinationType)
    {
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        return HasCircularReferenceRecursive(sourceType, destinationType, visited, 0, maxDepth: 10);
    }

    private static bool HasCircularReferenceRecursive(INamedTypeSymbol currentSourceType, 
        INamedTypeSymbol targetDestType, HashSet<INamedTypeSymbol> visited, int depth, int maxDepth)
    {
        // Prevent stack overflow by limiting recursion depth
        if (depth > maxDepth)
            return false;

        // If we've already visited this type, we have a cycle
        if (visited.Contains(currentSourceType))
            return true;

        visited.Add(currentSourceType);

        var properties = GetAccessibleProperties(currentSourceType);
        
        foreach (var property in properties)
        {
            var propertyType = property.Type;
            
            // Skip value types and system types
            if (IsSimpleType(propertyType))
                continue;

            // Check collection element types
            var elementType = GetCollectionElementType(propertyType);
            if (elementType != null)
                propertyType = elementType;

            if (propertyType is INamedTypeSymbol namedPropertyType)
            {
                // If this property references back to our target destination type
                if (SymbolEqualityComparer.Default.Equals(namedPropertyType, targetDestType))
                    return true;

                // Recursively check this property's type
                if (HasCircularReferenceRecursive(namedPropertyType, targetDestType, new HashSet<INamedTypeSymbol>(visited), depth + 1, maxDepth))
                    return true;
            }
        }

        visited.Remove(currentSourceType);
        return false;
    }

    private static HashSet<string> FindCircularProperties(INamedTypeSymbol sourceType, INamedTypeSymbol destinationType)
    {
        var circularProperties = new HashSet<string>();
        var sourceProperties = GetAccessibleProperties(sourceType);

        foreach (var property in sourceProperties)
        {
            // Check if property type references back to destination type
            if (SymbolEqualityComparer.Default.Equals(property.Type, destinationType))
            {
                circularProperties.Add(property.Name);
                continue;
            }

            // Check collection element types
            var elementType = GetCollectionElementType(property.Type);
            if (elementType != null && SymbolEqualityComparer.Default.Equals(elementType, destinationType))
            {
                circularProperties.Add(property.Name);
            }
        }

        return circularProperties;
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
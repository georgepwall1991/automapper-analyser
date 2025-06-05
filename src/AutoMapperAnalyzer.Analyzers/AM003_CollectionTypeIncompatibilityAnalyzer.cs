using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
///     Analyzer for detecting collection type incompatibility issues in AutoMapper configurations
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM003_CollectionTypeIncompatibilityAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     AM003: Collection type incompatibility without proper conversion
    /// </summary>
    public static readonly DiagnosticDescriptor CollectionTypeIncompatibilityRule = new(
        "AM003",
        "Collection type incompatibility in AutoMapper configuration",
        "Property '{0}' has incompatible collection types: {1}.{0} ({2}) cannot be mapped to {3}.{0} ({4}) without explicit collection conversion",
        "AutoMapper.Collections",
        DiagnosticSeverity.Error,
        true,
        "Source and destination properties have incompatible collection types that require explicit conversion configuration.");

    /// <summary>
    ///     AM003: Collection element type incompatibility
    /// </summary>
    public static readonly DiagnosticDescriptor CollectionElementIncompatibilityRule = new(
        "AM003",
        "Collection element type incompatibility in AutoMapper configuration",
        "Property '{0}' has incompatible collection element types: {1}.{0} ({2}) elements cannot be mapped to {3}.{0} ({4}) elements without explicit conversion",
        "AutoMapper.Collections",
        DiagnosticSeverity.Warning,
        true,
        "Collection properties have compatible collection types but incompatible element types that may require custom mapping.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(CollectionTypeIncompatibilityRule, CollectionElementIncompatibilityRule);

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

        // Analyze collection compatibility for property mappings
        AnalyzeCollectionCompatibility(context, invocationExpr, typeArguments.sourceType,
            typeArguments.destinationType);
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

    private static void AnalyzeCollectionCompatibility(SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType)
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

            // Check if both properties are collections
            if (IsCollectionType(sourceProperty.Type) && IsCollectionType(destinationProperty.Type))
            {
                AnalyzeCollectionPropertyCompatibility(context, invocation, sourceProperty, destinationProperty,
                    sourceType, destinationType);
            }
        }
    }

    private static bool IsCollectionType(ITypeSymbol type)
    {
        // Check for common collection interfaces and types
        if (type.TypeKind == TypeKind.Array)
            return true;

        if (type is INamedTypeSymbol namedType)
        {
            string typeName = namedType.ToDisplayString();
            
            // Check for generic collection types
            if (namedType.IsGenericType)
            {
                string genericTypeName = namedType.ConstructedFrom.ToDisplayString();
                return genericTypeName.StartsWith("System.Collections.Generic.List<") ||
                       genericTypeName.StartsWith("System.Collections.Generic.IList<") ||
                       genericTypeName.StartsWith("System.Collections.Generic.ICollection<") ||
                       genericTypeName.StartsWith("System.Collections.Generic.IEnumerable<") ||
                       genericTypeName.StartsWith("System.Collections.Generic.HashSet<") ||
                       genericTypeName.StartsWith("System.Collections.Generic.Queue<") ||
                       genericTypeName.StartsWith("System.Collections.Generic.Stack<") ||
                       genericTypeName.StartsWith("System.Collections.ObjectModel.Collection<") ||
                       genericTypeName.StartsWith("System.Collections.ObjectModel.ObservableCollection<");
            }

            // Check for non-generic collection types
            return typeName == "System.Collections.ArrayList" ||
                   typeName == "System.Collections.Queue" ||
                   typeName == "System.Collections.Stack" ||
                   typeName.StartsWith("System.Collections");
        }

        return false;
    }

    private static void AnalyzeCollectionPropertyCompatibility(SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IPropertySymbol sourceProperty,
        IPropertySymbol destinationProperty,
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType)
    {
        string sourceTypeName = sourceProperty.Type.ToDisplayString();
        string destTypeName = destinationProperty.Type.ToDisplayString();

        // Get element types for comparison
        ITypeSymbol? sourceElementType = GetCollectionElementType(sourceProperty.Type);
        ITypeSymbol? destElementType = GetCollectionElementType(destinationProperty.Type);

        if (sourceElementType == null || destElementType == null)
            return;

        // Check if collection types are fundamentally incompatible
        if (AreCollectionTypesIncompatible(sourceProperty.Type, destinationProperty.Type))
        {
            Diagnostic diagnostic = Diagnostic.Create(
                CollectionTypeIncompatibilityRule,
                invocation.GetLocation(),
                sourceProperty.Name,
                sourceType.Name,
                sourceTypeName,
                destinationType.Name,
                destTypeName);

            context.ReportDiagnostic(diagnostic);
        }
        // Check if element types are incompatible
        else if (!AreElementTypesCompatible(sourceElementType, destElementType))
        {
            Diagnostic diagnostic = Diagnostic.Create(
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

    private static ITypeSymbol? GetCollectionElementType(ITypeSymbol collectionType)
    {
        // Handle arrays
        if (collectionType is IArrayTypeSymbol arrayType)
            return arrayType.ElementType;

        // Handle generic collections
        if (collectionType is INamedTypeSymbol { IsGenericType: true } namedType)
        {
            // Most generic collections have the element type as the first type argument
            if (namedType.TypeArguments.Length > 0)
                return namedType.TypeArguments[0];
        }

        return null;
    }

    private static bool AreCollectionTypesIncompatible(ITypeSymbol sourceType, ITypeSymbol destType)
    {
        // Arrays and lists are generally compatible
        bool sourceIsArray = sourceType.TypeKind == TypeKind.Array;
        bool destIsArray = destType.TypeKind == TypeKind.Array;

        if (sourceIsArray && destIsArray)
            return false; // Arrays to arrays are compatible

        // Check for specific incompatible combinations
        string sourceTypeName = sourceType.ToDisplayString();
        string destTypeName = destType.ToDisplayString();

        // HashSet to List is generally incompatible without custom handling
        if (sourceTypeName.Contains("HashSet") && destTypeName.Contains("List"))
            return true;

        // Queue/Stack to other collections need special handling
        if ((sourceTypeName.Contains("Queue") || sourceTypeName.Contains("Stack")) &&
            !destTypeName.Contains("Queue") && !destTypeName.Contains("Stack"))
            return true;

        // Non-generic to generic collections
        if (!IsGenericCollection(sourceType) && IsGenericCollection(destType))
            return true;

        return false;
    }

    private static bool IsGenericCollection(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { IsGenericType: true } namedType &&
               namedType.TypeArguments.Length > 0;
    }

    private static bool AreElementTypesCompatible(ITypeSymbol sourceElementType, ITypeSymbol destElementType)
    {
        // Same types are compatible
        if (SymbolEqualityComparer.Default.Equals(sourceElementType, destElementType))
            return true;

        // Check for implicit conversions
        if (HasImplicitConversion(sourceElementType, destElementType))
            return true;

        // String representations for additional checks
        string sourceTypeName = sourceElementType.ToDisplayString();
        string destTypeName = destElementType.ToDisplayString();

        // Numeric type compatibility
        if (AreNumericTypesCompatible(sourceTypeName, destTypeName))
            return true;

        return false;
    }

    private static bool HasImplicitConversion(ITypeSymbol from, ITypeSymbol to)
    {
        // Check if there's an implicit conversion between the types
        // This is a simplified check - in a full implementation, we'd use Compilation.HasImplicitConversion
        
        // Only check for numeric implicit conversions between numeric types
        if (from.SpecialType == SpecialType.None || to.SpecialType == SpecialType.None)
            return false;

        // Both must be numeric types for implicit numeric conversion
        int fromLevel = GetNumericConversionLevel(from.SpecialType);
        int toLevel = GetNumericConversionLevel(to.SpecialType);
        
        // If either is not a numeric type (returns int.MaxValue), no implicit conversion
        if (fromLevel == int.MaxValue || toLevel == int.MaxValue)
            return false;
            
        return fromLevel <= toLevel;
    }

    private static int GetNumericConversionLevel(SpecialType type)
    {
        return type switch
        {
            SpecialType.System_Byte => 1,
            SpecialType.System_SByte => 1,
            SpecialType.System_Int16 => 2,
            SpecialType.System_UInt16 => 2,
            SpecialType.System_Int32 => 3,
            SpecialType.System_UInt32 => 3,
            SpecialType.System_Int64 => 4,
            SpecialType.System_UInt64 => 4,
            SpecialType.System_Single => 5,
            SpecialType.System_Double => 6,
            SpecialType.System_Decimal => 7,
            _ => int.MaxValue
        };
    }

    private static bool AreNumericTypesCompatible(string from, string to)
    {
        string[] numericTypes = { "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong", "float", "double", "decimal" };
        bool fromIsNumeric = numericTypes.Contains(from);
        bool toIsNumeric = numericTypes.Contains(to);
        
        // Both must be numeric types for this to be true
        return fromIsNumeric && toIsNumeric;
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

    private static bool HasExplicitPropertyMapping(InvocationExpressionSyntax createMapInvocation, string propertyName)
    {
        // Look for chained ForMember calls
        SyntaxNode? parent = createMapInvocation.Parent;

        while (parent is MemberAccessExpressionSyntax memberAccess &&
               parent.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if (memberAccess.Name.Identifier.ValueText == "ForMember")
            {
                // Check if this ForMember call is for the property we're analyzing
                if (IsForMemberOfProperty(chainedInvocation, propertyName))
                    return true;
            }

            parent = chainedInvocation.Parent;
        }

        return false;
    }

    private static bool IsForMemberOfProperty(InvocationExpressionSyntax forMemberInvocation, string propertyName)
    {
        // This is a simplified check - in a full implementation, we'd need to analyze the lambda expression
        // to determine which property is being configured
        SeparatedSyntaxList<ArgumentSyntax>? arguments = forMemberInvocation.ArgumentList?.Arguments;
        if (arguments?.Count > 0)
        {
            // Look for property name in the first argument (lambda expression)
            ArgumentSyntax firstArg = arguments.Value[0];
            return firstArg.ToString().Contains(propertyName);
        }

        return false;
    }
} 
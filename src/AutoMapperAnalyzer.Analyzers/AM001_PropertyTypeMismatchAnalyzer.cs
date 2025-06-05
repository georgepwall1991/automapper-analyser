using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM001_PropertyTypeMismatchAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor PropertyTypeMismatchRule = new(
        "AM001",
        "Property type mismatch in AutoMapper configuration",
        "Property '{0}' has incompatible types: {1}.{0} ({2}) cannot be mapped to {3}.{0} ({4}) without explicit conversion",
        "AutoMapper.TypeSafety",
        DiagnosticSeverity.Error,
        true,
        "Source and destination properties have incompatible types that require explicit conversion configuration."
    );

    public static readonly DiagnosticDescriptor NullableCompatibilityRule = new(
        "AM001",
        "Nullable compatibility issue in AutoMapper configuration",
        "Property '{0}' has nullable compatibility issue: {1}.{0} ({2}) can be null but {3}.{0} ({4}) is non-nullable",
        "AutoMapper.TypeSafety",
        DiagnosticSeverity.Warning,
        true,
        "Nullable source property mapped to non-nullable destination property may cause null reference exceptions."
    );

    public static readonly DiagnosticDescriptor GenericTypeMismatchRule = new(
        "AM001",
        "Generic type mismatch in AutoMapper configuration",
        "Property '{0}' has incompatible generic types: {1}.{0} ({2}) cannot be mapped to {3}.{0} ({4}) without explicit conversion",
        "AutoMapper.TypeSafety",
        DiagnosticSeverity.Error,
        true,
        "Generic type parameters are incompatible and require explicit conversion configuration."
    );

    public static readonly DiagnosticDescriptor ComplexTypeMappingMissingRule = new(
        "AM001",
        "Complex type mapping configuration missing",
        "Property '{0}' requires mapping configuration: {1}.{0} ({2}) to {3}.{0} ({4}). Consider adding CreateMap<{2}, {4}>().",
        "AutoMapper.TypeSafety",
        DiagnosticSeverity.Warning,
        true,
        "Complex types require explicit mapping configuration to ensure proper property mapping."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        PropertyTypeMismatchRule,
            NullableCompatibilityRule,
            GenericTypeMismatchRule,
            ComplexTypeMappingMissingRule
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCreateMapInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeCreateMapInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;

        // Check if this is a CreateMap<TSource, TDestination>() call
        if (!IsCreateMapInvocation(invocationExpr, context.SemanticModel))
        {
            return;
        }

        (INamedTypeSymbol? sourceType, INamedTypeSymbol? destinationType) typeArguments =
            GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (typeArguments.sourceType == null || typeArguments.destinationType == null)
        {
            return;
        }

        // Analyze property mappings between source and destination types
        AnalyzePropertyMappings(
            context,
            invocationExpr,
            typeArguments.sourceType,
            typeArguments.destinationType
        );
    }

    private static bool IsCreateMapInvocation(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        // Get the symbol info to check if this is really AutoMapper's CreateMap method
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);

        if (symbolInfo.Symbol is IMethodSymbol method)
        {
            // Check if it's a CreateMap method and it's a generic method
            if (method.Name == "CreateMap" && method.IsGenericMethod && method.TypeArguments.Length == 2)
            {
                // Additional check: see if it's from AutoMapper (check containing type or namespace)
                INamedTypeSymbol? containingType = method.ContainingType;
                if (containingType != null)
                {
                    // Allow any CreateMap method for now, we'll refine this later if needed
                    return true;
                }
            }
        }

        // Fallback: check syntax if symbol resolution fails
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.ValueText == "CreateMap";
        }

        if (invocation.Expression is IdentifierNameSyntax identifier)
        {
            return identifier.Identifier.ValueText == "CreateMap";
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
            var sourceType = method.TypeArguments[0] as INamedTypeSymbol;
            var destinationType = method.TypeArguments[1] as INamedTypeSymbol;
            return (sourceType, destinationType);
        }

        return (null, null);
    }

    private static void AnalyzePropertyMappings(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType)
    {
        IPropertySymbol[] sourceProperties = GetPublicProperties(sourceType);
        IPropertySymbol[] destinationProperties = GetPublicProperties(destinationType);

        // Check each source property for mapping compatibility
        foreach (IPropertySymbol sourceProp in sourceProperties)
        {
            IPropertySymbol? destProp = destinationProperties.FirstOrDefault(p =>
                string.Equals(p.Name, sourceProp.Name, StringComparison.OrdinalIgnoreCase));

            if (destProp == null)
            {
                continue; // Missing property will be handled by AM010
            }

            // Check if explicit mapping is configured for this property
            if (HasExplicitPropertyMapping(invocation, sourceProp.Name))
            {
                continue;
            }

            AnalyzePropertyTypeCompatibility(
                context,
                invocation,
                sourceProp,
                destProp,
                sourceType,
                destinationType
            );
        }
    }

    private static IPropertySymbol[] GetPublicProperties(INamedTypeSymbol type)
    {
        return type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                        p.CanBeReferencedByName &&
                        !p.IsStatic)
            .ToArray();
    }

    private static bool HasExplicitPropertyMapping(InvocationExpressionSyntax createMapInvocation, string propertyName)
    {
        // Look for chained ForMember calls
        SyntaxNode? parent = createMapInvocation.Parent;

        while (parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if (memberAccess.Name.Identifier.ValueText == "ForMember")
            {
                // Check if this ForMember call is for the property we're analyzing
                if (IsForMemberOfProperty(chainedInvocation, propertyName))
                {
                    return true;
                }
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

    private static void AnalyzePropertyTypeCompatibility(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IPropertySymbol sourceProperty,
        IPropertySymbol destinationProperty,
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType)
    {
        string sourceTypeName = sourceProperty.Type.ToDisplayString();
        string destTypeName = destinationProperty.Type.ToDisplayString();

        // Check for exact type match
        if (SymbolEqualityComparer.Default.Equals(sourceProperty.Type, destinationProperty.Type))
        {
            return;
        }

        // Check for nullable compatibility issues
        if (IsNullableCompatibilityIssue(sourceProperty.Type, destinationProperty.Type))
        {
            var diagnostic = Diagnostic.Create(
                NullableCompatibilityRule,
                invocation.GetLocation(),
                sourceProperty.Name,
                sourceType.Name,
                sourceTypeName,
                destinationType.Name,
                destTypeName
            );
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // Check for generic type mismatches
        if (IsGenericTypeMismatch(sourceProperty.Type, destinationProperty.Type))
        {
            var diagnostic = Diagnostic.Create(
                GenericTypeMismatchRule,
                invocation.GetLocation(),
                sourceProperty.Name,
                sourceType.Name,
                sourceTypeName,
                destinationType.Name,
                destTypeName
            );
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // Check for complex type mapping requirements
        if (IsComplexTypeMappingRequired(sourceProperty.Type, destinationProperty.Type))
        {
            // Check if mapping already exists for these complex types
            if (!HasExistingCreateMapForTypes(context, sourceProperty.Type, destinationProperty.Type))
            {
                var diagnostic = Diagnostic.Create(
                    ComplexTypeMappingMissingRule,
                    invocation.GetLocation(),
                    sourceProperty.Name,
                    sourceType.Name,
                    sourceTypeName,
                    destinationType.Name,
                    destTypeName
                );
                context.ReportDiagnostic(diagnostic);
            }

            return;
        }

        // Check for basic type incompatibilities
        if (!AreTypesCompatible(sourceProperty.Type, destinationProperty.Type))
        {
            var diagnostic = Diagnostic.Create(
                PropertyTypeMismatchRule,
                invocation.GetLocation(),
                sourceProperty.Name,
                sourceType.Name,
                sourceTypeName,
                destinationType.Name,
                destTypeName
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsNullableCompatibilityIssue(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        // Check if source is nullable reference type and destination is non-nullable
        return sourceType.CanBeReferencedByName &&
               sourceType.NullableAnnotation == NullableAnnotation.Annotated &&
               destinationType.NullableAnnotation == NullableAnnotation.NotAnnotated;
    }

    private static bool IsGenericTypeMismatch(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        if (sourceType is INamedTypeSymbol sourceNamed && destinationType is INamedTypeSymbol destNamed)
        {
            // Both are generic types with same generic definition but different type arguments
            if (sourceNamed.IsGenericType && destNamed.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(sourceNamed.OriginalDefinition, destNamed.OriginalDefinition))
            {
                // Check if type arguments are different
                for (int i = 0; i < sourceNamed.TypeArguments.Length; i++)
                {
                    if (!SymbolEqualityComparer.Default.Equals(
                            sourceNamed.TypeArguments[i],
                            destNamed.TypeArguments[i]))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsComplexTypeMappingRequired(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        // Both are named types (classes/structs) but not the same type
        if (sourceType is INamedTypeSymbol sourceNamed && destinationType is INamedTypeSymbol destNamed)
        {
            // Skip primitive types and common framework types
            if (IsPrimitiveOrFrameworkType(sourceType) || IsPrimitiveOrFrameworkType(destinationType))
            {
                return false;
            }

            // Different named types that aren't generic collections
            return !SymbolEqualityComparer.Default.Equals(sourceType, destinationType) &&
                   sourceNamed.TypeKind == TypeKind.Class &&
                   destNamed.TypeKind == TypeKind.Class;
        }

        return false;
    }

    private static bool HasExistingCreateMapForTypes(
        SyntaxNodeAnalysisContext context,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        // Get the root syntax node to search for all CreateMap invocations
        SyntaxNode root = context.Node.SyntaxTree.GetRoot();

        // Find all invocation expressions in the syntax tree
        IEnumerable<InvocationExpressionSyntax> allInvocations =
            root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (InvocationExpressionSyntax? invocation in allInvocations)
        {
            if (IsCreateMapInvocation(invocation, context.SemanticModel))
            {
                (INamedTypeSymbol? sourceType, INamedTypeSymbol? destinationType) typeArgs =
                    GetCreateMapTypeArguments(invocation, context.SemanticModel);
                if (typeArgs.sourceType != null && typeArgs.destinationType != null)
                {
                    // Check if this CreateMap matches the types we're looking for
                    if (SymbolEqualityComparer.Default.Equals(typeArgs.sourceType, sourceType) &&
                        SymbolEqualityComparer.Default.Equals(typeArgs.destinationType, destinationType))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool AreTypesCompatible(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        // Types are compatible if they're the same or have implicit conversions
        if (SymbolEqualityComparer.Default.Equals(sourceType, destinationType))
        {
            return true;
        }

        // Check for implicit numeric conversions only (string -> int is NOT implicit)
        return HasImplicitConversion(sourceType, destinationType);
    }

    private static bool HasImplicitConversion(ITypeSymbol from, ITypeSymbol to)
    {
        // Simplified implicit conversion checks for common scenarios
        string fromTypeName = from.ToDisplayString();
        string toTypeName = to.ToDisplayString();

        // Numeric conversions
        (string, string)[] numericConversions = new[]
        {
            ("byte", "short"), ("byte", "int"), ("byte", "long"), ("byte", "float"), ("byte", "double"),
            ("byte", "decimal"), ("short", "int"), ("short", "long"), ("short", "float"), ("short", "double"),
            ("short", "decimal"), ("int", "long"), ("int", "float"), ("int", "double"), ("int", "decimal"),
            ("long", "float"), ("long", "double"), ("long", "decimal"), ("float", "double")
        };

        return numericConversions.Any(conversion =>
            conversion.Item1 == fromTypeName && conversion.Item2 == toTypeName);
    }

    private static bool IsPrimitiveOrFrameworkType(ITypeSymbol type)
    {
        string typeName = type.ToDisplayString();
        string[] primitiveAndFrameworkTypes = new[]
        {
            "string", "int", "long", "short", "byte", "bool", "char", "float", "double", "decimal",
            "System.DateTime", "System.DateTimeOffset", "System.TimeSpan", "System.Guid"
        };

        return primitiveAndFrameworkTypes.Contains(typeName);
    }
}

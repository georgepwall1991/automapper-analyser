using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
///     Analyzer for detecting nullable reference type compatibility issues in AutoMapper configurations
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM002_NullableCompatibilityAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     AM002: Nullable to non-nullable assignment without proper handling
    /// </summary>
    public static readonly DiagnosticDescriptor NullableToNonNullableRule = new(
        "AM002",
        "Nullable to non-nullable mapping issue in AutoMapper configuration",
        "Property '{0}' has nullable compatibility issue: {1}.{0} ({2}) can be null but {3}.{0} ({4}) is non-nullable",
        "AutoMapper.NullSafety",
        DiagnosticSeverity.Error,
        true,
        "Source property is nullable but destination property is non-nullable, which could cause null reference exceptions at runtime.");

    /// <summary>
    ///     AM002: Non-nullable to nullable assignment (informational)
    /// </summary>
    public static readonly DiagnosticDescriptor NonNullableToNullableRule = new(
        "AM002",
        "Non-nullable to nullable mapping in AutoMapper configuration",
        "Property '{0}' mapping: {1}.{0} ({2}) is non-nullable but {3}.{0} ({4}) is nullable",
        "AutoMapper.NullSafety",
        DiagnosticSeverity.Info,
        true,
        "Non-nullable source property is being mapped to nullable destination property.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [NullableToNonNullableRule, NonNullableToNullableRule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
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

        // Analyze nullable compatibility for property mappings
        AnalyzeNullablePropertyMappings(
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

    private static void AnalyzeNullablePropertyMappings(
        SyntaxNodeAnalysisContext context,
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

            if (destinationProperty != null)
            {
                // Check for explicit property mapping that might handle nullability
                if (HasExplicitPropertyMapping(invocation, sourceProperty.Name))
                {
                    continue;
                }

                AnalyzeNullableCompatibility(
                    context,
                    invocation,
                    sourceProperty,
                    destinationProperty,
                    sourceType,
                    destinationType
                );
            }
        }
    }

    private static IPropertySymbol[] GetPublicProperties(INamedTypeSymbol type)
    {
        return type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public)
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

    private static void AnalyzeNullableCompatibility(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IPropertySymbol sourceProperty,
        IPropertySymbol destinationProperty,
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType)
    {
        string sourceTypeName = sourceProperty.Type.ToDisplayString();
        string destTypeName = destinationProperty.Type.ToDisplayString();

        // Case 1: Nullable source -> Non-nullable destination (WARNING)
        if (IsNullableType(sourceProperty.Type) && !IsNullableType(destinationProperty.Type))
        {
            // Check if the underlying types are compatible
            ITypeSymbol sourceUnderlyingType = GetUnderlyingType(sourceProperty.Type);
            ITypeSymbol destUnderlyingType = GetUnderlyingType(destinationProperty.Type);

            if (AreUnderlyingTypesCompatible(sourceUnderlyingType, destUnderlyingType))
            {
                var diagnostic = Diagnostic.Create(
                    NullableToNonNullableRule,
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
        // Case 2: Non-nullable source -> Nullable destination (INFO)
        else if (!IsNullableType(sourceProperty.Type) && IsNullableType(destinationProperty.Type))
        {
            ITypeSymbol sourceUnderlyingType = GetUnderlyingType(sourceProperty.Type);
            ITypeSymbol destUnderlyingType = GetUnderlyingType(destinationProperty.Type);

            if (AreUnderlyingTypesCompatible(sourceUnderlyingType, destUnderlyingType))
            {
                var diagnostic = Diagnostic.Create(
                    NonNullableToNullableRule,
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
    }

    private static bool IsNullableType(ITypeSymbol type)
    {
        // Check for nullable reference types (string?, object?, etc.)
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }

        // Check for nullable value types (int?, DateTime?, etc.)
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return true;
        }

        // Check by string representation for cases where annotation might not be detected
        string typeString = type.ToDisplayString();
        return typeString.EndsWith("?");
    }

    private static ITypeSymbol GetUnderlyingType(ITypeSymbol type)
    {
        // For nullable value types (int?, DateTime?), get the underlying type
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            type is INamedTypeSymbol namedType && namedType.TypeArguments.Length == 1)
        {
            return namedType.TypeArguments[0];
        }

        // For nullable reference types (string?, object?), the type itself is the underlying type
        return type;
    }

    private static bool AreUnderlyingTypesCompatible(ITypeSymbol sourceType, ITypeSymbol destType)
    {
        // Check if underlying types are the same
        if (SymbolEqualityComparer.Default.Equals(sourceType, destType))
        {
            return true;
        }

        // Check if there's an implicit conversion between the underlying types
        return HasImplicitConversion(sourceType, destType);
    }

    private static bool HasImplicitConversion(ITypeSymbol from, ITypeSymbol to)
    {
        // Simplified implicit conversion checks for common scenarios
        string fromTypeName = from.ToDisplayString();
        string toTypeName = to.ToDisplayString();

        // Numeric conversions
        (string, string)[] numericConversions =
        [
            ("byte", "short"), ("byte", "int"), ("byte", "long"), ("byte", "float"), ("byte", "double"),
            ("byte", "decimal"), ("short", "int"), ("short", "long"), ("short", "float"), ("short", "double"),
            ("short", "decimal"), ("int", "long"), ("int", "float"), ("int", "double"), ("int", "decimal"),
            ("long", "float"), ("long", "double"), ("long", "decimal"), ("float", "double")
        ];

        return numericConversions.Any(conversion =>
            conversion.Item1 == fromTypeName && conversion.Item2 == toTypeName);
    }
}

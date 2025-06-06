using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
///     Analyzer for detecting unmapped required properties in destination types that will cause runtime exceptions
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM011_UnmappedRequiredPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     AM011: Unmapped required property - will cause runtime exception
    /// </summary>
    public static readonly DiagnosticDescriptor UnmappedRequiredPropertyRule = new(
        "AM011",
        "Required destination property is not mapped from source",
        "Required property '{0}' in destination is not mapped from any source property and will cause a runtime exception",
        "AutoMapper.RequiredProperties",
        DiagnosticSeverity.Error,
        true,
        "Required destination properties must be mapped from source properties or explicitly configured to prevent runtime exceptions during mapping.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(UnmappedRequiredPropertyRule);

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

        // Analyze unmapped required properties in destination
        AnalyzeUnmappedRequiredProperties(
            context,
            invocationExpr,
            typeArguments.sourceType,
            typeArguments.destinationType
        );
    }

    private static bool IsCreateMapInvocation(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);
        return symbolInfo.Symbol is IMethodSymbol method &&
               method.Name == "CreateMap" &&
               (method.ContainingType?.Name == "IMappingExpression" ||
                method.ContainingType?.Name == "Profile");
    }

    private static (INamedTypeSymbol? sourceType, INamedTypeSymbol? destinationType) GetCreateMapTypeArguments(
        InvocationExpressionSyntax invocation, SemanticModel semanticModel)
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

    private static void AnalyzeUnmappedRequiredProperties(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType)
    {
        IPropertySymbol[] sourceProperties = GetMappableProperties(sourceType);
        IPropertySymbol[] destinationProperties = GetMappableProperties(destinationType);

        // Check each required destination property to see if it's mapped from source
        foreach (IPropertySymbol destinationProperty in destinationProperties)
        {
            // Only analyze required properties
            if (!IsRequiredProperty(destinationProperty))
            {
                continue;
            }

            // Check if source has a property with the same name (case-insensitive, like AutoMapper)
            IPropertySymbol? sourceProperty = sourceProperties
                .FirstOrDefault(p =>
                    string.Equals(p.Name, destinationProperty.Name, StringComparison.OrdinalIgnoreCase));

            if (sourceProperty != null)
            {
                continue; // Property is mapped from source, no issue
            }

            // Check if this destination property is explicitly mapped via ForMember
            if (IsDestinationPropertyExplicitlyMapped(invocation, destinationProperty.Name))
            {
                continue; // Property is explicitly mapped, no issue
            }

            // Report diagnostic for unmapped required property
            var diagnostic = Diagnostic.Create(
                UnmappedRequiredPropertyRule,
                invocation.GetLocation(),
                destinationProperty.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static IPropertySymbol[] GetMappableProperties(INamedTypeSymbol type)
    {
        var properties = new List<IPropertySymbol>();

        // Get properties from the type and all its base types
        INamedTypeSymbol? currentType = type;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            // Get declared properties (not inherited ones to avoid duplicates)
            IPropertySymbol[] typeProperties = currentType.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                            p.CanBeReferencedByName &&
                            !p.IsStatic &&
                            p.GetMethod != null && // Must be readable
                            p.SetMethod != null && // Must be writable
                            p.ContainingType.Equals(currentType, SymbolEqualityComparer.Default)) // Only direct members
                .ToArray();

            properties.AddRange(typeProperties);
            currentType = currentType.BaseType;
        }

        return properties.ToArray();
    }

    private static bool IsRequiredProperty(IPropertySymbol property)
    {
        // Check for required modifier in the property
        // In Roslyn, required properties have a RequiredMemberAttribute or the required keyword
        return property.IsRequired;
    }

    private static bool IsDestinationPropertyExplicitlyMapped(InvocationExpressionSyntax createMapInvocation,
        string destinationPropertyName)
    {
        // Look for chained ForMember calls that map to this destination property
        SyntaxNode? parent = createMapInvocation.Parent;

        while (parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if (memberAccess.Name.Identifier.ValueText == "ForMember")
            {
                // Check if this ForMember call targets the destination property we're analyzing
                if (IsForMemberTargetingProperty(chainedInvocation, destinationPropertyName))
                {
                    return true;
                }
            }

            parent = chainedInvocation.Parent;
        }

        return false;
    }

    private static bool IsForMemberTargetingProperty(InvocationExpressionSyntax forMemberInvocation,
        string destinationPropertyName)
    {
        // This is a simplified check - in a full implementation, we'd need to analyze the lambda expressions
        // to determine exact property targets
        SeparatedSyntaxList<ArgumentSyntax>? arguments = forMemberInvocation.ArgumentList?.Arguments;
        if (arguments?.Count >= 1)
        {
            // Check if the destination property is referenced in the first argument (destination selector)
            string firstArg = arguments.Value[0].ToString();
            if (firstArg.Contains(destinationPropertyName))
            {
                return true;
            }
        }

        return false;
    }
}

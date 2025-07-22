using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
///     Analyzer for detecting case sensitivity mismatches between source and destination properties in AutoMapper
///     configurations
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM005_CaseSensitivityMismatchAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     AM005: Case sensitivity mismatch - properties differ only in casing
    /// </summary>
    public static readonly DiagnosticDescriptor CaseSensitivityMismatchRule = new(
        "AM005",
        "Property names differ only in casing",
        "Property '{0}' in source differs only in casing from destination property '{1}' - consider explicit mapping or case-insensitive configuration",
        "AutoMapper.PropertyMapping",
        DiagnosticSeverity.Warning,
        true,
        "Properties that differ only in casing may cause mapping issues depending on AutoMapper configuration. " +
        "Consider using explicit mapping or configure case-insensitive property matching.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [CaseSensitivityMismatchRule];

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

        // Analyze case sensitivity mismatches between source and destination properties
        AnalyzeCaseSensitivityMismatches(
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

    private static void AnalyzeCaseSensitivityMismatches(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType)
    {
        IPropertySymbol[] sourceProperties = GetMappableProperties(sourceType);
        IPropertySymbol[] destinationProperties = GetMappableProperties(destinationType);

        // Check each source property for case sensitivity mismatches
        foreach (IPropertySymbol sourceProperty in sourceProperties)
        {
            // Find destination property with same name but different case
            IPropertySymbol? exactMatch = destinationProperties
                .FirstOrDefault(p => string.Equals(p.Name, sourceProperty.Name, StringComparison.Ordinal));

            if (exactMatch != null)
            {
                continue; // Exact match, no case sensitivity issue
            }

            // Find case-insensitive match
            IPropertySymbol? caseInsensitiveMatch = destinationProperties
                .FirstOrDefault(p => string.Equals(p.Name, sourceProperty.Name, StringComparison.OrdinalIgnoreCase));

            if (caseInsensitiveMatch == null)
            {
                continue; // No match at all - this would be handled by AM004 (missing destination property)
            }

            // Check if types are compatible (only report case sensitivity if types match or are compatible)
            if (!AreTypesCompatibleForCaseSensitivityCheck(sourceProperty.Type, caseInsensitiveMatch.Type))
            {
                continue; // Type mismatch - this would be handled by AM001 (type mismatch)
            }

            // Check if explicit mapping is configured for this property
            if (IsPropertyExplicitlyMapped(invocation, sourceProperty.Name, caseInsensitiveMatch.Name))
            {
                continue; // Explicit mapping handles the case sensitivity issue
            }

            // Report diagnostic for case sensitivity mismatch
            var diagnostic = Diagnostic.Create(
                CaseSensitivityMismatchRule,
                invocation.GetLocation(),
                sourceProperty.Name,
                caseInsensitiveMatch.Name);

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

    private static bool AreTypesCompatibleForCaseSensitivityCheck(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        // For case sensitivity checks, we only care about properties where the types are the same or compatible
        // If types are incompatible, that's a different issue (AM001)

        // Exact type match
        if (SymbolEqualityComparer.Default.Equals(sourceType, destinationType))
        {
            return true;
        }

        // Check for implicit conversions between compatible types
        return AreTypesImplicitlyCompatible(sourceType, destinationType);
    }

    private static bool AreTypesImplicitlyCompatible(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        string sourceTypeName = sourceType.ToDisplayString();
        string destTypeName = destinationType.ToDisplayString();

        // Numeric conversions
        (string, string)[] numericConversions =
        [
            ("byte", "short"), ("byte", "int"), ("byte", "long"), ("byte", "float"), ("byte", "double"),
            ("byte", "decimal"), ("short", "int"), ("short", "long"), ("short", "float"), ("short", "double"),
            ("short", "decimal"), ("int", "long"), ("int", "float"), ("int", "double"), ("int", "decimal"),
            ("long", "float"), ("long", "double"), ("long", "decimal"), ("float", "double")
        ];

        return numericConversions.Any(conversion =>
            conversion.Item1 == sourceTypeName && conversion.Item2 == destTypeName);
    }

    private static bool IsPropertyExplicitlyMapped(InvocationExpressionSyntax createMapInvocation,
        string sourcePropertyName, string destinationPropertyName)
    {
        // Look for chained ForMember calls that handle this property mapping
        SyntaxNode? parent = createMapInvocation.Parent;

        while (parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if (memberAccess.Name.Identifier.ValueText == "ForMember")
            {
                // Check if this ForMember call maps the source property to the destination property
                if (IsForMemberMappingProperty(chainedInvocation, sourcePropertyName, destinationPropertyName))
                {
                    return true;
                }
            }

            parent = chainedInvocation.Parent;
        }

        return false;
    }

    private static bool IsForMemberMappingProperty(InvocationExpressionSyntax forMemberInvocation,
        string sourcePropertyName, string destinationPropertyName)
    {
        // This is a simplified check - in a full implementation, we'd need to analyze the lambda expressions
        // to determine exact property mappings
        SeparatedSyntaxList<ArgumentSyntax>? arguments = forMemberInvocation.ArgumentList?.Arguments;
        if (arguments?.Count >= 2)
        {
            // Check if the destination property is referenced in the first argument
            string firstArg = arguments.Value[0].ToString();
            if (firstArg.Contains(destinationPropertyName))
            {
                // Check if the source property is referenced in the second argument  
                string secondArg = arguments.Value[1].ToString();
                if (secondArg.Contains(sourcePropertyName))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

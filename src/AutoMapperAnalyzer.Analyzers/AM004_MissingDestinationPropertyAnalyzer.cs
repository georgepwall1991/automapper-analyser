using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
///     Analyzer for detecting missing destination properties that could lead to data loss in AutoMapper configurations
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM004_MissingDestinationPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     AM004: Missing destination property - potential data loss
    /// </summary>
    public static readonly DiagnosticDescriptor MissingDestinationPropertyRule = new(
        "AM004",
        "Source property has no corresponding destination property",
        "Source property '{0}' will not be mapped - potential data loss",
        "AutoMapper.MissingProperty",
        DiagnosticSeverity.Warning,
        true,
        "Source property exists but no corresponding destination property found, which may result in data loss during mapping.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [MissingDestinationPropertyRule];

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

        // Analyze missing properties between source and destination types
        AnalyzeMissingDestinationProperties(
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

    private static void AnalyzeMissingDestinationProperties(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType)
    {
        IPropertySymbol[] sourceProperties = GetMappableProperties(sourceType);
        IPropertySymbol[] destinationProperties = GetMappableProperties(destinationType);

        // Check each source property to see if it has a corresponding destination property
        foreach (IPropertySymbol sourceProperty in sourceProperties)
        {
            // Check if destination has a property with the same name (case-insensitive, like AutoMapper)
            IPropertySymbol? destinationProperty = destinationProperties
                .FirstOrDefault(p => string.Equals(p.Name, sourceProperty.Name, StringComparison.OrdinalIgnoreCase));

            if (destinationProperty != null)
            {
                continue; // Property exists in destination, no data loss
            }

            // Check if this source property is handled by custom mapping configuration
            if (IsSourcePropertyHandledByCustomMapping(invocation, sourceProperty.Name))
            {
                continue; // Property is handled by custom mapping, no data loss
            }

            // Check if this source property is explicitly ignored
            if (IsSourcePropertyExplicitlyIgnored(invocation, sourceProperty.Name))
            {
                continue; // Property is explicitly ignored, no diagnostic needed
            }

            // Report diagnostic for missing destination property
            var diagnostic = Diagnostic.Create(
                MissingDestinationPropertyRule,
                invocation.GetLocation(),
                sourceProperty.Name);

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
            IPropertySymbol[] currentProperties = currentType.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                            p.CanBeReferencedByName &&
                            !p.IsStatic &&
                            p.GetMethod != null && // Must be readable
                            p.SetMethod != null) // Must be writable (for destination) or readable (for source)
                .ToArray();

            properties.AddRange(currentProperties);
            currentType = currentType.BaseType;
        }

        // Remove duplicates (in case of overridden properties)
        return properties
            .GroupBy(p => p.Name)
            .Select(g => g.First())
            .ToArray();
    }

    private static bool IsSourcePropertyHandledByCustomMapping(InvocationExpressionSyntax createMapInvocation,
        string sourcePropertyName)
    {
        // Look for chained ForMember calls that might use this source property
        SyntaxNode? parent = createMapInvocation.Parent;

        while (parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if (memberAccess.Name.Identifier.ValueText == "ForMember")
            {
                // Check if this ForMember call references the source property
                if (ForMemberReferencesSourceProperty(chainedInvocation, sourcePropertyName))
                {
                    return true;
                }
            }

            // Move up the chain
            parent = chainedInvocation.Parent;
        }

        return false;
    }

    private static bool ForMemberReferencesSourceProperty(InvocationExpressionSyntax forMemberInvocation,
        string sourcePropertyName)
    {
        // Look for lambda expressions in ForMember arguments that reference the source property
        foreach (ArgumentSyntax arg in forMemberInvocation.ArgumentList.Arguments)
        {
            if (ContainsPropertyReference(arg.Expression, sourcePropertyName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsPropertyReference(SyntaxNode node, string propertyName)
    {
        // Recursively search for property access expressions that match the property name
        return node.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
            .Any(memberAccess => memberAccess.Name.Identifier.ValueText == propertyName);
    }

    private static bool IsSourcePropertyExplicitlyIgnored(InvocationExpressionSyntax createMapInvocation,
        string sourcePropertyName)
    {
        // Look for ForSourceMember calls with DoNotValidate
        SyntaxNode? parent = createMapInvocation.Parent;

        while (parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if (memberAccess.Name.Identifier.ValueText == "ForSourceMember")
            {
                // Check if this ForSourceMember call is for the property we're analyzing
                if (IsForSourceMemberOfProperty(chainedInvocation, sourcePropertyName))
                {
                    // Check if it has DoNotValidate
                    if (HasDoNotValidateCall(chainedInvocation))
                    {
                        return true;
                    }
                }
            }

            // Move up the chain
            parent = chainedInvocation.Parent;
        }

        return false;
    }

    private static bool IsForSourceMemberOfProperty(InvocationExpressionSyntax forSourceMemberInvocation,
        string propertyName)
    {
        // Check the first argument of ForSourceMember to see if it references the property
        if (forSourceMemberInvocation.ArgumentList.Arguments.Count > 0)
        {
            ExpressionSyntax firstArg = forSourceMemberInvocation.ArgumentList.Arguments[0].Expression;
            return ContainsPropertyReference(firstArg, propertyName);
        }

        return false;
    }

    private static bool HasDoNotValidateCall(InvocationExpressionSyntax forSourceMemberInvocation)
    {
        // Look for DoNotValidate call in the second argument (options)
        if (forSourceMemberInvocation.ArgumentList.Arguments.Count > 1)
        {
            ExpressionSyntax secondArg = forSourceMemberInvocation.ArgumentList.Arguments[1].Expression;

            // Look for lambda expressions that contain DoNotValidate calls
            return secondArg.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation =>
                    invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name.Identifier.ValueText == "DoNotValidate");
        }

        return false;
    }
}

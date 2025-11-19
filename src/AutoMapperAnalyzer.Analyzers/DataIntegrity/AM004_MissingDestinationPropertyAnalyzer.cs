using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.DataIntegrity;

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

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [MissingDestinationPropertyRule];

    /// <inheritdoc />
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
        if (!AutoMapperAnalysisHelpers.IsCreateMapInvocation(invocationExpr, context.SemanticModel))
        {
            return;
        }

        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) typeArguments =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (typeArguments.sourceType == null || typeArguments.destinationType == null)
        {
            return;
        }

        // Analyze missing properties for Forward Map (Source -> Destination)
        AnalyzeMissingDestinationProperties(
            context,
            invocationExpr,
            typeArguments.sourceType,
            typeArguments.destinationType,
            false
        );

        // Check for ReverseMap() and analyze Destination -> Source
        InvocationExpressionSyntax? reverseMapInvocation =
            AutoMapperAnalysisHelpers.GetReverseMapInvocation(invocationExpr);
        if (reverseMapInvocation != null)
        {
            AnalyzeMissingDestinationProperties(
                context,
                invocationExpr, // Use the original invocation for location, or maybe reverseMapInvocation?
                typeArguments.destinationType, // Source is now Destination
                typeArguments.sourceType, // Destination is now Source
                true,
                reverseMapInvocation
            );
        }
    }

    private static void AnalyzeMissingDestinationProperties(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        bool isReverseMap,
        InvocationExpressionSyntax? reverseMapInvocation = null)
    {
        IEnumerable<IPropertySymbol> sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        IEnumerable<IPropertySymbol> destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, false);

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

            // Check for flattening: if source property is complex and destination has properties starting with source name
            // AutoMapper flattens complex properties (e.g. Customer.Name -> CustomerName)
            if (!AutoMapperAnalysisHelpers.IsBuiltInType(sourceProperty.Type))
            {
                bool isUsedInFlattening = destinationProperties
                    .Any(p => p.Name.StartsWith(sourceProperty.Name, StringComparison.OrdinalIgnoreCase));

                if (isUsedInFlattening)
                {
                    continue; // Property is likely used in flattening
                }
            }

            // Check if this source property is handled by custom mapping configuration
            if (IsSourcePropertyHandledByCustomMapping(invocation, sourceProperty.Name, isReverseMap,
                    reverseMapInvocation))
            {
                continue; // Property is handled by custom mapping, no data loss
            }

            // Check if this source property is explicitly ignored
            if (IsSourcePropertyExplicitlyIgnored(invocation, sourceProperty.Name, isReverseMap, reverseMapInvocation))
            {
                continue; // Property is explicitly ignored, no diagnostic needed
            }

            // Report diagnostic for missing destination property
            ImmutableDictionary<string, string?>.Builder properties =
                ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("PropertyName", sourceProperty.Name);
            properties.Add("PropertyType", sourceProperty.Type.ToDisplayString());
            properties.Add("SourceTypeName", GetTypeName(sourceType));
            properties.Add("DestinationTypeName", GetTypeName(destinationType));

            // Use location of ReverseMap call if this is a reverse map issue, otherwise CreateMap
            InvocationExpressionSyntax locationNode =
                isReverseMap && reverseMapInvocation != null ? reverseMapInvocation : invocation;

            var diagnostic = Diagnostic.Create(
                MissingDestinationPropertyRule,
                locationNode.GetLocation(),
                properties.ToImmutable(),
                sourceProperty.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    ///     Gets the type name from an ITypeSymbol.
    /// </summary>
    /// <param name="type">The type symbol.</param>
    /// <returns>The type name.</returns>
    private static string GetTypeName(ITypeSymbol type)
    {
        return type.Name;
    }

    private static bool IsSourcePropertyHandledByCustomMapping(
        InvocationExpressionSyntax createMapInvocation,
        string sourcePropertyName,
        bool isReverseMap,
        InvocationExpressionSyntax? reverseMapInvocation)
    {
        // Get all ForMember calls in the chain
        IEnumerable<InvocationExpressionSyntax> forMemberCalls =
            AutoMapperAnalysisHelpers.GetForMemberCalls(createMapInvocation);

        foreach (InvocationExpressionSyntax? forMember in forMemberCalls)
        {
            // Check if this ForMember call applies to the current direction
            if (!AppliesToDirection(forMember, isReverseMap, reverseMapInvocation))
            {
                continue;
            }

            // Check if this ForMember call references the source property
            if (ForMemberReferencesSourceProperty(forMember, sourcePropertyName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AppliesToDirection(
        InvocationExpressionSyntax mappingMethod,
        bool isReverseMap,
        InvocationExpressionSyntax? reverseMapInvocation)
    {
        if (reverseMapInvocation == null)
        {
            // No ReverseMap, so all calls apply to Forward (which is the only direction being analyzed if isReverseMap is false)
            // If isReverseMap is true but no reverseMapInvocation, something is wrong, but we'll assume false.
            return !isReverseMap;
        }

        // If we have ReverseMap, we need to split the chain
        // Forward Map: Methods that are DESCENDANTS of ReverseMap (inside its expression)
        // Reverse Map: Methods that are ANCESTORS of ReverseMap (contain it)

        // Check if mappingMethod contains ReverseMap (Ancestor)
        bool isAncestor = reverseMapInvocation.Ancestors().Contains(mappingMethod);

        if (isReverseMap)
        {
            // For Reverse Map, we want calls that come AFTER ReverseMap() in the chain
            // In syntax tree, these are parents/ancestors of ReverseMap
            return isAncestor;
        }

        // For Forward Map, we want calls that come BEFORE ReverseMap() in the chain
        // In syntax tree, these are descendants (or simply NOT ancestors)
        // Actually, strictly speaking, they should be descendants.
        // If mappingMethod is neither ancestor nor descendant, it might be on a separate branch? 
        // But usually it's a linear chain.
        // If it's NOT an ancestor, it must be "inside" the expression of ReverseMap.
        return !isAncestor;
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

    private static bool IsSourcePropertyExplicitlyIgnored(
        InvocationExpressionSyntax createMapInvocation,
        string sourcePropertyName,
        bool isReverseMap,
        InvocationExpressionSyntax? reverseMapInvocation)
    {
        // Look for ForSourceMember calls
        SyntaxNode? parent = createMapInvocation.Parent;

        while (parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if (memberAccess.Name.Identifier.ValueText == "ForSourceMember")
            {
                // Check direction
                if (AppliesToDirection(chainedInvocation, isReverseMap, reverseMapInvocation))
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

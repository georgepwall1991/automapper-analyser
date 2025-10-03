using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using AutoMapperAnalyzer.Analyzers.Helpers;

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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [UnmappedRequiredPropertyRule];

    /// <inheritdoc/>
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

        // Analyze unmapped required properties in destination
        AnalyzeUnmappedRequiredProperties(
            context,
            invocationExpr,
            typeArguments.sourceType,
            typeArguments.destinationType
        );
    }


    private static void AnalyzeUnmappedRequiredProperties(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        var destinationProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, requireGetter: false);

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
            var properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("PropertyName", destinationProperty.Name);
            properties.Add("PropertyType", destinationProperty.Type.ToDisplayString());
            properties.Add("SourceTypeName", GetTypeName(sourceType));
            properties.Add("DestinationTypeName", GetTypeName(destinationType));

            var diagnostic = Diagnostic.Create(
                UnmappedRequiredPropertyRule,
                invocation.GetLocation(),
                properties.ToImmutable(),
                destinationProperty.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// Gets the type name from an ITypeSymbol.
    /// </summary>
    /// <param name="type">The type symbol.</param>
    /// <returns>The type name.</returns>
    private static string GetTypeName(ITypeSymbol type)
    {
        return type.Name;
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

using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.DataIntegrity;

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

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [UnmappedRequiredPropertyRule];

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

        // Custom construction/conversion logic can satisfy required members in ways this analyzer
        // cannot reliably infer. Skip to avoid noisy false positives.
        if (HasCustomConstructionOrConversion(invocationExpr))
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
        IEnumerable<IPropertySymbol> sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        IEnumerable<IPropertySymbol> destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, false);

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
            if (AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember(invocation, destinationProperty.Name,
                    context.SemanticModel))
            {
                continue; // Property is explicitly mapped, no issue
            }

            // Check if this destination property is configured via constructor parameter mapping
            if (IsPropertyConfiguredWithForCtorParam(invocation, destinationProperty.Name, context.SemanticModel))
            {
                continue; // Property is mapped via ForCtorParam, no issue
            }

            // Report diagnostic for unmapped required property
            ImmutableDictionary<string, string?>.Builder properties =
                ImmutableDictionary.CreateBuilder<string, string?>();
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
    ///     Gets the type name from an ITypeSymbol.
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

    private static bool HasCustomConstructionOrConversion(InvocationExpressionSyntax createMapInvocation)
    {
        SyntaxNode? currentNode = createMapInvocation.Parent;

        while (currentNode is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax invocation)
        {
            string methodName = memberAccess.Name.Identifier.ValueText;
            if (methodName is "ConstructUsing" or "ConvertUsing")
            {
                return true;
            }

            currentNode = invocation.Parent;
        }

        return false;
    }

    private static bool IsPropertyConfiguredWithForCtorParam(
        InvocationExpressionSyntax createMapInvocation,
        string propertyName,
        SemanticModel semanticModel)
    {
        SyntaxNode? currentNode = createMapInvocation.Parent;

        while (currentNode is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax invocation)
        {
            if (memberAccess.Name.Identifier.ValueText == "ForCtorParam" &&
                invocation.ArgumentList.Arguments.Count > 0)
            {
                ArgumentSyntax firstArg = invocation.ArgumentList.Arguments[0];
                Optional<object?> constantValue = semanticModel.GetConstantValue(firstArg.Expression);

                if (constantValue.HasValue &&
                    constantValue.Value is string configuredParam &&
                    string.Equals(configuredParam, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            currentNode = invocation.Parent;
        }

        return false;
    }
}

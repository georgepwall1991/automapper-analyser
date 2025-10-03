using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using AutoMapperAnalyzer.Analyzers.Helpers;

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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [CaseSensitivityMismatchRule];

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

        // Analyze case sensitivity mismatches between source and destination properties
        AnalyzeCaseSensitivityMismatches(
            context,
            invocationExpr,
            typeArguments.sourceType,
            typeArguments.destinationType
        );
    }


    private static void AnalyzeCaseSensitivityMismatches(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        var destinationProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, requireGetter: false);

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
            if (!AutoMapperAnalysisHelpers.AreTypesCompatible(sourceProperty.Type, caseInsensitiveMatch.Type))
            {
                continue; // Type mismatch - this would be handled by AM001 (type mismatch)
            }

            // Check if explicit mapping is configured for this property
            if (IsPropertyExplicitlyMapped(invocation, sourceProperty.Name, caseInsensitiveMatch.Name))
            {
                continue; // Explicit mapping handles the case sensitivity issue
            }

            // Report diagnostic for case sensitivity mismatch
            var properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("SourcePropertyName", sourceProperty.Name);
            properties.Add("DestinationPropertyName", caseInsensitiveMatch.Name);
            properties.Add("PropertyType", sourceProperty.Type.ToDisplayString());
            properties.Add("SourceTypeName", GetTypeName(sourceType));
            properties.Add("DestinationTypeName", GetTypeName(destinationType));

            var diagnostic = Diagnostic.Create(
                CaseSensitivityMismatchRule,
                invocation.GetLocation(),
                properties.ToImmutable(),
                sourceProperty.Name,
                caseInsensitiveMatch.Name);

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

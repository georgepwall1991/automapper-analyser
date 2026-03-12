using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.DataIntegrity;

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

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [CaseSensitivityMismatchRule];

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

        // Ensure strict AutoMapper semantic matching to avoid lookalike false positives.
        if (!MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocationExpr, context.SemanticModel, "CreateMap"))
        {
            return;
        }

        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) typeArguments =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (typeArguments.sourceType == null || typeArguments.destinationType == null)
        {
            return;
        }

        var reportedMismatches = new HashSet<string>(StringComparer.Ordinal);

        // Analyze case sensitivity mismatches between source and destination properties
        AnalyzeCaseSensitivityMismatches(
            context,
            invocationExpr,
            typeArguments.sourceType,
            typeArguments.destinationType,
            reportedMismatches
        );

        InvocationExpressionSyntax? reverseMapInvocation =
            AutoMapperAnalysisHelpers.GetReverseMapInvocation(invocationExpr);
        if (reverseMapInvocation != null)
        {
            AnalyzeCaseSensitivityMismatches(
                context,
                reverseMapInvocation,
                typeArguments.destinationType,
                typeArguments.sourceType,
                reportedMismatches
            );
        }
    }


    private static void AnalyzeCaseSensitivityMismatches(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax mappingInvocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        HashSet<string> reportedMismatches)
    {
        if (AM020MappingConfigurationHelpers.HasCustomConstructionOrConversion(mappingInvocation, context.SemanticModel))
        {
            return;
        }

        IEnumerable<IPropertySymbol> sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        IEnumerable<IPropertySymbol> destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, false);

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
            if (AM020MappingConfigurationHelpers.IsDestinationPropertyExplicitlyConfigured(
                    mappingInvocation,
                    caseInsensitiveMatch.Name,
                    context.SemanticModel))
            {
                continue; // Explicit mapping handles the case sensitivity issue
            }

            // Check if source property is explicitly ignored
            if (MappingChainAnalysisHelper.IsSourcePropertyExplicitlyIgnored(
                    mappingInvocation,
                    sourceProperty.Name,
                    context.SemanticModel,
                    ShouldStopAtReverseMapBoundary(mappingInvocation, context.SemanticModel)))
            {
                continue;
            }

            string mismatchKey =
                CreateMismatchKey(sourceType, destinationType, sourceProperty.Name, caseInsensitiveMatch.Name);
            if (!reportedMismatches.Add(mismatchKey))
            {
                continue;
            }

            // Report diagnostic for case sensitivity mismatch
            ImmutableDictionary<string, string?>.Builder properties =
                ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("SourcePropertyName", sourceProperty.Name);
            properties.Add("DestinationPropertyName", caseInsensitiveMatch.Name);
            properties.Add("PropertyType", sourceProperty.Type.ToDisplayString());
            properties.Add("SourceTypeName", AutoMapperAnalysisHelpers.GetTypeName(sourceType));
            properties.Add("DestinationTypeName", AutoMapperAnalysisHelpers.GetTypeName(destinationType));

            var diagnostic = Diagnostic.Create(
                CaseSensitivityMismatchRule,
                mappingInvocation.GetLocation(),
                properties.ToImmutable(),
                sourceProperty.Name,
                caseInsensitiveMatch.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static string CreateMismatchKey(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        string sourcePropertyName,
        string destinationPropertyName)
    {
        string[] typeNames = [sourceType.ToDisplayString(), destinationType.ToDisplayString()];
        Array.Sort(typeNames, StringComparer.Ordinal);

        string[] propertyNames = [sourcePropertyName.ToUpperInvariant(), destinationPropertyName.ToUpperInvariant()];
        Array.Sort(propertyNames, StringComparer.Ordinal);

        return $"{typeNames[0]}|{typeNames[1]}::{propertyNames[0]}|{propertyNames[1]}";
    }

    private static bool ShouldStopAtReverseMapBoundary(
        InvocationExpressionSyntax mappingInvocation,
        SemanticModel semanticModel)
    {
        return !MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(mappingInvocation, semanticModel, "ReverseMap");
    }
}

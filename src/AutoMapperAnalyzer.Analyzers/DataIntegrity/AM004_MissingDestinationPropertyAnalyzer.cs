using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.DataIntegrity;

/// <summary>
///     Analyzer for detecting missing destination properties that could lead to data loss in AutoMapper configurations.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM004_MissingDestinationPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     AM004: Missing destination property - potential data loss.
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

        if (!MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocationExpr, context.SemanticModel, "CreateMap"))
        {
            return;
        }

        var typeArguments = MappingChainAnalysisHelper.GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (typeArguments.sourceType == null || typeArguments.destinationType == null)
        {
            return;
        }

        // Forward direction: Source -> Destination
        ReportUnmappedProperties(context, invocationExpr, typeArguments.sourceType, typeArguments.destinationType,
            context.SemanticModel, stopAtReverseMapBoundary: true, isReverseMap: false);

        // Reverse direction: find ReverseMap and analyze Destination -> Source
        foreach (InvocationExpressionSyntax chainedInvocation in MappingChainAnalysisHelper.GetScopedChainInvocations(
                     invocationExpr, context.SemanticModel, stopAtReverseMapBoundary: false))
        {
            if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(chainedInvocation, context.SemanticModel, "ReverseMap"))
            {
                ReportUnmappedProperties(context, chainedInvocation, typeArguments.destinationType,
                    typeArguments.sourceType, context.SemanticModel, stopAtReverseMapBoundary: false, isReverseMap: true);
                break;
            }
        }
    }

    private static void ReportUnmappedProperties(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax mappingInvocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary,
        bool isReverseMap)
    {
        if (MappingChainAnalysisHelper.HasCustomConstructionOrConversion(mappingInvocation, semanticModel, stopAtReverseMapBoundary))
        {
            return;
        }

        List<IPropertySymbol> unmapped = MappingChainAnalysisHelper.GetUnmappedSourceProperties(
            mappingInvocation, sourceType, destinationType, semanticModel, stopAtReverseMapBoundary);

        foreach (IPropertySymbol sourceProperty in unmapped)
        {
            ImmutableDictionary<string, string?>.Builder properties =
                ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("PropertyName", sourceProperty.Name);
            properties.Add("PropertyType", sourceProperty.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            properties.Add("SourceTypeName", sourceType.Name);
            properties.Add("DestinationTypeName", destinationType.Name);
            properties.Add("IsReverseMap", isReverseMap ? "true" : "false");

            context.ReportDiagnostic(Diagnostic.Create(
                MissingDestinationPropertyRule,
                mappingInvocation.GetLocation(),
                properties.ToImmutable(),
                sourceProperty.Name));
        }
    }
}

using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.DataIntegrity;

/// <summary>
///     Analyzer for detecting destination properties that are not mapped from source
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM006_UnmappedDestinationPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     AM006: Unmapped destination property
    /// </summary>
    public static readonly DiagnosticDescriptor UnmappedDestinationPropertyRule = new(
        "AM006",
        "Destination property is not mapped",
        "Destination property '{0}' is not mapped from source '{1}'",
        "AutoMapper.DataIntegrity",
        DiagnosticSeverity.Info,
        true,
        "Destination property exists but has no corresponding source property or explicit mapping.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [UnmappedDestinationPropertyRule];

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

        // Analyze forward map
        AnalyzeUnmappedDestinationProperties(
            context,
            invocationExpr,
            typeArguments.sourceType,
            typeArguments.destinationType,
            false
        );

        // Analyze reverse map
        InvocationExpressionSyntax? reverseMapInvocation =
            AutoMapperAnalysisHelpers.GetReverseMapInvocation(invocationExpr);
        if (reverseMapInvocation != null)
        {
            AnalyzeUnmappedDestinationProperties(
                context,
                invocationExpr,
                typeArguments.destinationType, // Source is now Destination
                typeArguments.sourceType, // Destination is now Source
                true,
                reverseMapInvocation
            );
        }
    }

    private static void AnalyzeUnmappedDestinationProperties(
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

        foreach (IPropertySymbol destProperty in destinationProperties)
        {
            // 1. Check for direct mapping (same name)
            if (sourceProperties.Any(p => string.Equals(p.Name, destProperty.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // 2. Check for flattening
            // If Dest is "CustomerName" and Source has "Customer" (complex), and "Customer" has "Name".
            // Heuristic: Check if any source property is a prefix of Dest property
            bool matchesFlattening = false;
            foreach (var srcProp in sourceProperties)
            {
                if (!AutoMapperAnalysisHelpers.IsBuiltInType(srcProp.Type) &&
                    destProperty.Name.StartsWith(srcProp.Name, StringComparison.OrdinalIgnoreCase) &&
                    destProperty.Name.Length > srcProp.Name.Length)
                {
                    // Potential flattening. e.g. Src=Customer, Dest=CustomerName.
                    // Ideally check if Customer has Name property.
                    // For now, assume if prefix matches and it's complex, it might be flattening.
                    matchesFlattening = true; 
                    break;
                }
            }
            if (matchesFlattening) continue;

            // 3. Check for explicit configuration (ForMember)
            if (AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember(invocation, destProperty.Name, context.SemanticModel))
            {
                continue;
            }
            
            // 4. Check for Ignore? 
            // IsPropertyConfiguredWithForMember checks if ForMember exists. 
            // If ForMember exists (even if Ignore), it is "mapped" (or explicitly handled).
            // So check 3 covers Ignore.

            // Report diagnostic
            ImmutableDictionary<string, string?>.Builder properties =
                ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("PropertyName", destProperty.Name);
            properties.Add("DestinationTypeName", destinationType.Name);
            properties.Add("SourceTypeName", sourceType.Name);

            InvocationExpressionSyntax locationNode =
                isReverseMap && reverseMapInvocation != null ? reverseMapInvocation : invocation;

            var diagnostic = Diagnostic.Create(
                UnmappedDestinationPropertyRule,
                locationNode.GetLocation(),
                properties.ToImmutable(),
                destProperty.Name,
                sourceType.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }
}


using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.Configuration;

/// <summary>
///     Analyzer that detects redundant MapFrom configurations where the source and destination property names match.
///     AutoMapper automatically maps matching property names, so explicit configuration is unnecessary.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM050_RedundantMapFromAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     Diagnostic descriptor for redundant MapFrom detection.
    /// </summary>
    public static readonly DiagnosticDescriptor RedundantMapFromRule = new(
        "AM050",
        "Redundant MapFrom configuration",
        "Explicit mapping for '{0}' is redundant because the property name matches the source",
        "AutoMapper.Configuration",
        DiagnosticSeverity.Info,
        true,
        "AutoMapper automatically maps properties with the same name. Explicit MapFrom configuration is unnecessary.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [RedundantMapFromRule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Check if it is a MapFrom call
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.Text == "MapFrom")
        {
            if (!MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, context.SemanticModel, "MapFrom"))
            {
                return;
            }

            // Check if it's inside ForMember
            InvocationExpressionSyntax? forMemberInvocation = invocation.Ancestors()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(inv =>
                    inv.Expression is MemberAccessExpressionSyntax ma &&
                    ma.Name.Identifier.Text == "ForMember" &&
                    MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(inv, context.SemanticModel, "ForMember"));

            if (forMemberInvocation == null)
            {
                return;
            }

            // Get destination property name
            string? destPropName = GetDestinationPropertyName(forMemberInvocation);
            if (destPropName == null)
            {
                return;
            }

            // Get source property name
            string? sourcePropName = GetMapFromSourcePropertyName(invocation);
            if (sourcePropName == null)
            {
                return;
            }

            // Check if source property access has same name
            if (string.Equals(sourcePropName, destPropName, StringComparison.Ordinal))
            {
                // Check if types are compatible (same type including nullability)
                if (!AreTypesCompatibleForAutoMapping(forMemberInvocation, invocation, context))
                {
                    return; // Not redundant if types differ (e.g., int? to int)
                }

                var diagnostic = Diagnostic.Create(
                    RedundantMapFromRule,
                    invocation.GetLocation(),
                    destPropName);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private bool AreTypesCompatibleForAutoMapping(
        InvocationExpressionSyntax forMemberInvocation,
        InvocationExpressionSyntax mapFromInvocation,
        SyntaxNodeAnalysisContext context)
    {
        // Get source and destination property types
        ITypeSymbol? sourceType = GetSourcePropertyType(mapFromInvocation, context);
        ITypeSymbol? destType = GetDestinationPropertyType(forMemberInvocation, context);

        if (sourceType == null || destType == null)
        {
            return true; // Can't determine, assume compatible
        }

        // Check if types are exactly the same (including nullability)
        return SymbolEqualityComparer.Default.Equals(sourceType, destType);
    }

    private ITypeSymbol? GetSourcePropertyType(
        InvocationExpressionSyntax mapFromInvocation,
        SyntaxNodeAnalysisContext context)
    {
        if (mapFromInvocation.ArgumentList.Arguments.Count < 1)
        {
            return null;
        }

        ExpressionSyntax arg = mapFromInvocation.ArgumentList.Arguments[0].Expression;

        // Expecting src => src.Name
        if (arg is SimpleLambdaExpressionSyntax lambda &&
            lambda.Body is MemberAccessExpressionSyntax memberAccess)
        {
            SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);
            if (symbolInfo.Symbol is IPropertySymbol propertySymbol)
            {
                return propertySymbol.Type;
            }
        }

        return null;
    }

    private ITypeSymbol? GetDestinationPropertyType(
        InvocationExpressionSyntax forMemberInvocation,
        SyntaxNodeAnalysisContext context)
    {
        if (forMemberInvocation.ArgumentList.Arguments.Count < 1)
        {
            return null;
        }

        ExpressionSyntax arg = forMemberInvocation.ArgumentList.Arguments[0].Expression;

        // Expecting dest => dest.Name
        if (arg is SimpleLambdaExpressionSyntax lambda &&
            lambda.Body is MemberAccessExpressionSyntax memberAccess)
        {
            SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);
            if (symbolInfo.Symbol is IPropertySymbol propertySymbol)
            {
                return propertySymbol.Type;
            }
        }

        return null;
    }

    private string? GetDestinationPropertyName(InvocationExpressionSyntax forMemberInvocation)
    {
        if (forMemberInvocation.ArgumentList.Arguments.Count < 1)
        {
            return null;
        }

        ExpressionSyntax arg = forMemberInvocation.ArgumentList.Arguments[0].Expression;

        // Expecting dest => dest.Name
        if (arg is SimpleLambdaExpressionSyntax lambda &&
            lambda.Body is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Expression is IdentifierNameSyntax identifier &&
            identifier.Identifier.Text == lambda.Parameter.Identifier.Text)
        {
            return memberAccess.Name.Identifier.Text;
        }

        // Handle quoted string "Name"
        if (arg is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }

        return null;
    }

    private string? GetMapFromSourcePropertyName(InvocationExpressionSyntax mapFromInvocation)
    {
        if (mapFromInvocation.ArgumentList.Arguments.Count < 1)
        {
            return null;
        }

        ExpressionSyntax arg = mapFromInvocation.ArgumentList.Arguments[0].Expression;

        // Expecting src => src.Name
        if (arg is SimpleLambdaExpressionSyntax lambda &&
            lambda.Body is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Expression is IdentifierNameSyntax identifier &&
            identifier.Identifier.Text == lambda.Parameter.Identifier.Text)
        {
            return memberAccess.Name.Identifier.Text;
        }

        return null;
    }

}

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers.Configuration;

/// <summary>
/// Analyzer that detects redundant MapFrom configurations where the source and destination property names match.
/// AutoMapper automatically maps matching property names, so explicit configuration is unnecessary.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM050_RedundantMapFromAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Diagnostic descriptor for redundant MapFrom detection.
    /// </summary>
    public static readonly DiagnosticDescriptor RedundantMapFromRule = new(
        "AM050",
        "Redundant MapFrom configuration",
        "Explicit mapping for '{0}' is redundant because the property name matches the source",
        "AutoMapper.Configuration",
        DiagnosticSeverity.Info,
        true,
        "AutoMapper automatically maps properties with the same name. Explicit MapFrom configuration is unnecessary.");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [RedundantMapFromRule];

    /// <inheritdoc/>
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
            // Check if it's inside ForMember
            var forMemberInvocation = invocation.Ancestors()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(inv => 
                    inv.Expression is MemberAccessExpressionSyntax ma && 
                    ma.Name.Identifier.Text == "ForMember");

            if (forMemberInvocation == null) return;

            // Get destination property name
            var destPropName = GetDestinationPropertyName(forMemberInvocation);
            if (destPropName == null) return;

            // Get source expression
            var sourceExpression = GetMapFromExpression(invocation);
            if (sourceExpression == null) return;

            // Check if source expression is simple property access with same name
            if (IsSimplePropertyAccess(sourceExpression, out var sourcePropName) &&
                string.Equals(sourcePropName, destPropName, System.StringComparison.Ordinal))
            {
                var diagnostic = Diagnostic.Create(
                    RedundantMapFromRule,
                    invocation.GetLocation(),
                    destPropName);
                
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private string? GetDestinationPropertyName(InvocationExpressionSyntax forMemberInvocation)
    {
        if (forMemberInvocation.ArgumentList.Arguments.Count < 1) return null;
        
        var arg = forMemberInvocation.ArgumentList.Arguments[0].Expression;
        
        // Expecting dest => dest.Name
        if (arg is SimpleLambdaExpressionSyntax lambda &&
            lambda.Body is MemberAccessExpressionSyntax memberAccess)
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

    private ExpressionSyntax? GetMapFromExpression(InvocationExpressionSyntax mapFromInvocation)
    {
        if (mapFromInvocation.ArgumentList.Arguments.Count < 1) return null;
        
        var arg = mapFromInvocation.ArgumentList.Arguments[0].Expression;
        
        // Expecting src => src.Name
        if (arg is SimpleLambdaExpressionSyntax lambda)
        {
            return lambda.Body as ExpressionSyntax;
        }
        
        return null;
    }

    private bool IsSimplePropertyAccess(ExpressionSyntax expression, out string? propertyName)
    {
        propertyName = null;
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            // Ensure it's a simple property access on the lambda parameter (not nested or complex)
            // e.g. src.Name is simple. src.Child.Name is not simple (in context of redundancy check 1-to-1).
            
            if (memberAccess.Expression is IdentifierNameSyntax)
            {
                propertyName = memberAccess.Name.Identifier.Text;
                return true;
            }
        }
        return false;
    }
}

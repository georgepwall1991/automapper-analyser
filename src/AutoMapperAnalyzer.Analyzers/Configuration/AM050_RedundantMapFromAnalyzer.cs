using System.Collections.Immutable;
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
            if (!IsAutoMapperMethodInvocation(invocation, context.SemanticModel, "MapFrom"))
            {
                return;
            }

            // Check if it's inside ForMember
            InvocationExpressionSyntax? forMemberInvocation = invocation.Ancestors()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(inv =>
                    inv.Expression is MemberAccessExpressionSyntax ma &&
                    ma.Name.Identifier.Text == "ForMember" &&
                    IsAutoMapperMethodInvocation(inv, context.SemanticModel, "ForMember"));

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

    private static bool IsAutoMapperMethodInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string methodName)
    {
        IMethodSymbol? methodSymbol = GetMethodSymbol(invocation, semanticModel);
        if (methodSymbol == null || methodSymbol.Name != methodName)
        {
            return false;
        }

        string? namespaceName = methodSymbol.ContainingNamespace?.ToDisplayString();
        return namespaceName == "AutoMapper" ||
               (namespaceName?.StartsWith("AutoMapper.", StringComparison.Ordinal) ?? false);
    }

    private static IMethodSymbol? GetMethodSymbol(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);

        if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
        {
            return methodSymbol;
        }

        foreach (ISymbol candidateSymbol in symbolInfo.CandidateSymbols)
        {
            if (candidateSymbol is IMethodSymbol candidateMethod)
            {
                return candidateMethod;
            }
        }

        return null;
    }
}

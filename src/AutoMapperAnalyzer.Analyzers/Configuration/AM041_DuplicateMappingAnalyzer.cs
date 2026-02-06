using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.Configuration;

/// <summary>
///     Analyzer that detects duplicate mapping registrations in AutoMapper configuration.
///     Duplicates can cause ambiguous behavior and runtime errors.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM041_DuplicateMappingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     Diagnostic descriptor for duplicate mapping detection.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateMappingRule = new(
        "AM041",
        "Duplicate mapping registration",
        "Mapping from '{0}' to '{1}' is already registered",
        "AutoMapper.Configuration",
        DiagnosticSeverity.Warning,
        true,
        "Duplicate mapping registrations can cause ambiguous behavior and should be removed.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [DuplicateMappingRule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            // Use the shared registry which now handles duplicate detection
            var registry = CreateMapRegistry.FromCompilation(compilationContext.Compilation);
            Dictionary<InvocationExpressionSyntax, (string Source, string Dest, Location Location)> duplicates =
                registry.GetDuplicateMappings();

            compilationContext.RegisterSyntaxNodeAction(ctx =>
            {
                var invocation = (InvocationExpressionSyntax)ctx.Node;
                if (duplicates.TryGetValue(invocation, out (string Source, string Dest, Location Location) info))
                {
                    if (!IsAutoMapperMappingInvocation(invocation, ctx.SemanticModel))
                    {
                        return;
                    }

                    var diagnostic = Diagnostic.Create(
                        DuplicateMappingRule,
                        info.Location,
                        info.Source,
                        info.Dest);
                    ctx.ReportDiagnostic(diagnostic);
                }
            }, SyntaxKind.InvocationExpression);
        });
    }

    private static bool IsAutoMapperMappingInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);
        IMethodSymbol? methodSymbol = symbolInfo.Symbol as IMethodSymbol;
        if (methodSymbol == null && symbolInfo.CandidateSymbols.Length > 0)
        {
            methodSymbol = symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        }

        if (methodSymbol == null)
        {
            return false;
        }

        if (methodSymbol.Name is not ("CreateMap" or "ReverseMap"))
        {
            return false;
        }

        string? namespaceName = methodSymbol.ContainingNamespace?.ToDisplayString();
        return namespaceName == "AutoMapper" || (namespaceName?.StartsWith("AutoMapper.") ?? false);
    }
}

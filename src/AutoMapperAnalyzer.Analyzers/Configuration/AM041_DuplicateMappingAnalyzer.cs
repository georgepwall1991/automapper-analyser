using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers.Configuration;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM041_DuplicateMappingAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor DuplicateMappingRule = new(
        "AM041",
        "Duplicate mapping registration",
        "Mapping from '{0}' to '{1}' is already registered",
        "AutoMapper.Configuration",
        DiagnosticSeverity.Warning,
        true,
        "Duplicate mapping registrations can cause ambiguous behavior and should be removed.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [DuplicateMappingRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        
        context.RegisterCompilationStartAction(compilationContext =>
        {
            // Use the shared registry which now handles duplicate detection
            var registry = CreateMapRegistry.FromCompilation(compilationContext.Compilation);
            var duplicates = registry.GetDuplicateMappings();
            
            compilationContext.RegisterSyntaxNodeAction(ctx =>
            {
                var invocation = (InvocationExpressionSyntax)ctx.Node;
                if (duplicates.TryGetValue(invocation, out var info))
                {
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
}

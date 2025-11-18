using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
            var duplicates = FindDuplicateMappings(compilationContext.Compilation);
            
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

    private struct DuplicateInfo
    {
        public string Source;
        public string Dest;
        public Location Location;
    }

    private Dictionary<InvocationExpressionSyntax, DuplicateInfo> FindDuplicateMappings(Compilation compilation)
    {
        var mappings = new List<(ITypeSymbol Source, ITypeSymbol Dest, Location Location, InvocationExpressionSyntax Node, bool IsReverseMap)>();

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var root = syntaxTree.GetRoot();
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                if (!AutoMapperAnalysisHelpers.IsCreateMapInvocation(invocation, semanticModel))
                {
                    continue;
                }

                var (sourceType, destType) = AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
                if (sourceType != null && destType != null)
                {
                    mappings.Add((sourceType, destType, invocation.GetLocation(), invocation, false));

                    if (AutoMapperAnalysisHelpers.HasReverseMap(invocation))
                    {
                        var reverseMapInvocation = AutoMapperAnalysisHelpers.GetReverseMapInvocation(invocation);
                        if (reverseMapInvocation != null)
                        {
                            Location loc;
                            if (reverseMapInvocation.Expression is MemberAccessExpressionSyntax ma)
                            {
                                loc = ma.Name.GetLocation();
                            }
                            else
                            {
                                loc = reverseMapInvocation.GetLocation();
                            }
                            
                            mappings.Add((destType, sourceType, loc, reverseMapInvocation, true));
                        }
                    }
                }
            }
        }

        var duplicates = new Dictionary<InvocationExpressionSyntax, DuplicateInfo>();
        var groups = mappings.GroupBy(m => (m.Source, m.Dest), new MappingComparer());

        foreach (var group in groups)
        {
            if (group.Count() > 1)
            {
                // Sort by location to have deterministic reporting
                var sorted = group.OrderBy(x => x.Location.SourceTree?.FilePath)
                                  .ThenBy(x => x.Location.SourceSpan.Start)
                                  .ToList();

                // Report on all except the first one
                for (int i = 1; i < sorted.Count; i++)
                {
                    var duplicate = sorted[i];
                    duplicates[duplicate.Node] = new DuplicateInfo
                    {
                        Source = duplicate.Source.Name,
                        Dest = duplicate.Dest.Name,
                        Location = duplicate.Location
                    };
                }
            }
        }

        return duplicates;
    }

    private class MappingComparer : IEqualityComparer<(ITypeSymbol Source, ITypeSymbol Dest)>
    {
        public bool Equals((ITypeSymbol Source, ITypeSymbol Dest) x, (ITypeSymbol Source, ITypeSymbol Dest) y)
        {
            return SymbolEqualityComparer.Default.Equals(x.Source, y.Source) &&
                   SymbolEqualityComparer.Default.Equals(x.Dest, y.Dest);
        }

        public int GetHashCode((ITypeSymbol Source, ITypeSymbol Dest) obj)
        {
            int h1 = SymbolEqualityComparer.Default.GetHashCode(obj.Source);
            int h2 = SymbolEqualityComparer.Default.GetHashCode(obj.Dest);
            return h1 ^ h2;
        }
    }
}

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;
using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Analyzers.TypeSafety;
using AutoMapperAnalyzer.Analyzers.ComplexMappings;
using AutoMapperAnalyzer.Analyzers.Configuration;
using AutoMapperAnalyzer.Analyzers.Performance;

using System.Globalization;

namespace AnalyzerVerifier;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🔍 AutoMapper Analyzer Verifier");
        Console.WriteLine("===============================");

        // Register MSBuild
        MSBuildLocator.RegisterDefaults();

        using var workspace = MSBuildWorkspace.Create();
        
        // Locate the samples solution
        // Assuming we run from project root or tools/AnalyzerVerifier
        // Better to find it relative to current execution
        var currentDir = Directory.GetCurrentDirectory();
        var repoRoot = FindRepoRoot(currentDir);
        
        if (repoRoot == null)
        {
            Console.Error.WriteLine("Could not find repository root.");
            return;
        }
        
        var solutionPath = Path.Combine(repoRoot, "automapper-analyser.sln");
        // We can also just target the samples project directly to save time/issues loading the whole sln
        var projectPath = Path.Combine(repoRoot, "samples/AutoMapperAnalyzer.Samples/AutoMapperAnalyzer.Samples.csproj");

        Console.WriteLine($"Opening project: {projectPath}");
        
        // Hook up workspace failure event
        workspace.WorkspaceFailed += (o, e) => Console.WriteLine($"Workspace failed: {e.Diagnostic.Message}");

        var project = await workspace.OpenProjectAsync(projectPath);
        var compilation = await project.GetCompilationAsync();

        if (compilation == null)
        {
            Console.Error.WriteLine("Compilation failed.");
            return;
        }

        Console.WriteLine("Compilation succeeded. Running analyzers...");

        // Gather all analyzers from the referenced project
        var analyzers = GetAnalyzers();
        
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

        // Filter diagnostics to only those from our analyzers
        var ourDiagnostics = diagnostics
            .Where(d => d.Id.StartsWith("AM", StringComparison.Ordinal))
            .OrderBy(d => d.Id)
            .ThenBy(d => d.Location.SourceTree?.FilePath)
            .ThenBy(d => d.Location.SourceSpan.Start)
            .ToList();

        Console.WriteLine($"Found {ourDiagnostics.Count} diagnostics.");

        // Generate Report
        var reportPath = Path.Combine(repoRoot, "docs/ANALYZER_REPORT.md");
        GenerateReport(ourDiagnostics, reportPath, analyzers);
        
        Console.WriteLine($"Report generated at: {reportPath}");
    }

    static ImmutableArray<DiagnosticAnalyzer> GetAnalyzers()
    {
        // We manually instantiate them to ensure we have the exact list we expect
        return ImmutableArray.Create<DiagnosticAnalyzer>(
            // Type Safety
            new AM001_PropertyTypeMismatchAnalyzer(),
            new AM002_NullableCompatibilityAnalyzer(),
            new AM003_CollectionTypeIncompatibilityAnalyzer(),
            
            // Data Integrity
            new AM004_MissingDestinationPropertyAnalyzer(),
            new AM005_CaseSensitivityMismatchAnalyzer(),
            new AM006_UnmappedDestinationPropertyAnalyzer(),
            new AM011_UnmappedRequiredPropertyAnalyzer(),
            
            // Complex Mappings
            new AM020_NestedObjectMappingAnalyzer(),
            new AM021_CollectionElementMismatchAnalyzer(),
            new AM022_InfiniteRecursionAnalyzer(),
            
            // Custom Conversions
            new AM030_CustomTypeConverterAnalyzer(),
            
            // Performance
            new AM031_PerformanceWarningAnalyzer(),
            
            // Configuration
            new AM041_DuplicateMappingAnalyzer(),
            new AM050_RedundantMapFromAnalyzer()
        );
    }

    static void GenerateReport(List<Diagnostic> diagnostics, string reportPath, ImmutableArray<DiagnosticAnalyzer> analyzers)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# AutoMapper Analyzer Diagnostic Report");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Generated:** {DateTime.Now}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Total Issues Found:** {diagnostics.Count}");
        sb.AppendLine();
        
        sb.AppendLine("## Analyzer Coverage Status");
        sb.AppendLine("| Analyzer | Name | Issues Found |");
        sb.AppendLine("|----------|------|--------------|");
        
        foreach (var analyzer in analyzers)
        {
            foreach (var descriptor in analyzer.SupportedDiagnostics)
            {
                var count = diagnostics.Count(d => d.Id == descriptor.Id);
                sb.AppendLine(CultureInfo.InvariantCulture, $"| {descriptor.Id} | {descriptor.Title} | {count} |");
            }
        }
        
        sb.AppendLine();
        sb.AppendLine("## Detailed Findings");
        sb.AppendLine();

        var groupedDiagnostics = diagnostics.GroupBy(d => d.Id);

        foreach (var group in groupedDiagnostics)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"### {group.Key}: {group.First().Descriptor.Title}");
            sb.AppendLine();
            
            foreach (var diagnostic in group)
            {
                var location = diagnostic.Location;
                var filePath = location.SourceTree?.FilePath;
                var fileName = Path.GetFileName(filePath);
                var lineSpan = location.GetLineSpan();
                var line = lineSpan.StartLinePosition.Line + 1;
                
                sb.AppendLine(CultureInfo.InvariantCulture, $"- **{fileName}:{line}**: {diagnostic.GetMessage(CultureInfo.InvariantCulture)}");
            }
            sb.AppendLine();
        }

        File.WriteAllText(reportPath, sb.ToString());
    }

    static string? FindRepoRoot(string currentPath)
    {
        var dir = new DirectoryInfo(currentPath);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}

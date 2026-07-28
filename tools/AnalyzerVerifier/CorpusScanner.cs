using System.Collections.Immutable;
using System.Text.Json;
using AutoMapperAnalyzer.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;

namespace AnalyzerVerifier;

/// <summary>
///     Runs the shipped analyzers over third-party code the project did not author.
///     <para>
///     Every other verification path in this repository reads code written by the same people who wrote
///     the rules: the samples project, the test suite, the snapshot baselines. That cannot surface a
///     false positive nobody imagined. The <c>IncludeMembers</c> defect fixed in 2.30.88 sat unnoticed
///     through more than thirty releases for exactly this reason — no third-party mapping profile had
///     ever been compiled against the analyzers.
///     </para>
///     <para>
///     Output is a diagnostics report, not a pass/fail gate. A corpus finding is a lead to triage, and
///     promoting a confirmed one into a permanent regression test is the point.
///     </para>
/// </summary>
internal static class CorpusScanner
{
    public sealed record CorpusFinding(
        string RuleId,
        string Severity,
        string File,
        int Line,
        string Message
    );

    public sealed record CorpusReport(
        string Target,
        int ProjectsScanned,
        int ProjectsFailed,
        int TotalDiagnostics,
        Dictionary<string, int> CountsByRule,
        List<CorpusFinding> Findings,
        List<string> LoadFailures
    );

    /// <summary>
    ///     Opens a project or solution, runs every catalogued analyzer over it, and returns what the
    ///     analyzers reported. Projects that fail to load are recorded rather than aborting the scan:
    ///     partial coverage of a real codebase is still worth more than none.
    /// </summary>
    public static async Task<CorpusReport> ScanAsync(string targetPath, int maxSamplesPerRule)
    {
        using MSBuildWorkspace workspace = MSBuildWorkspace.Create();
        var projects = new List<Project>();

        if (
            targetPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
            || targetPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
        )
        {
            Solution solution = await workspace.OpenSolutionAsync(targetPath);
            projects.AddRange(solution.Projects);
        }
        else
        {
            projects.Add(await workspace.OpenProjectAsync(targetPath));
        }

        ImmutableArray<DiagnosticAnalyzer> analyzers = RuleCatalog
            .Rules.Select(rule => rule.AnalyzerType)
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type)!)
            .ToImmutableArray();

        var findings = new List<CorpusFinding>();
        var countsByRule = new Dictionary<string, int>(StringComparer.Ordinal);
        var loadFailures = new List<string>();
        var scanned = 0;
        var failed = 0;

        foreach (
            Project project in projects.Where(project => project.Language == LanguageNames.CSharp)
        )
        {
            Compilation? compilation;
            try
            {
                compilation = await project.GetCompilationAsync();
            }
            catch (Exception exception)
            {
                failed++;
                loadFailures.Add(
                    $"{project.Name}: {exception.GetType().Name}: {exception.Message}"
                );
                continue;
            }

            if (compilation == null)
            {
                failed++;
                loadFailures.Add($"{project.Name}: compilation could not be created");
                continue;
            }

            // A project that does not reference AutoMapper cannot produce a meaningful AM diagnostic,
            // and scanning it only adds noise and time.
            if (
                !compilation.ReferencedAssemblyNames.Any(assembly =>
                    assembly.Name.StartsWith("AutoMapper", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                continue;
            }

            scanned++;

            ImmutableArray<Diagnostic> diagnostics = await compilation
                .WithAnalyzers(analyzers)
                .GetAnalyzerDiagnosticsAsync();

            foreach (
                Diagnostic diagnostic in diagnostics.Where(diagnostic =>
                    diagnostic.Id.StartsWith("AM", StringComparison.Ordinal)
                )
            )
            {
                countsByRule.TryGetValue(diagnostic.Id, out int existing);
                countsByRule[diagnostic.Id] = existing + 1;

                if (existing >= maxSamplesPerRule)
                {
                    continue;
                }

                FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
                findings.Add(
                    new CorpusFinding(
                        diagnostic.Id,
                        diagnostic.Severity.ToString(),
                        span.Path,
                        span.StartLinePosition.Line + 1,
                        diagnostic.GetMessage()
                    )
                );
            }
        }

        foreach (
            WorkspaceDiagnostic diagnostic in workspace.Diagnostics.Where(diagnostic =>
                diagnostic.Kind == WorkspaceDiagnosticKind.Failure
            )
        )
        {
            loadFailures.Add(diagnostic.Message);
        }

        return new CorpusReport(
            targetPath,
            scanned,
            failed,
            countsByRule.Values.Sum(),
            countsByRule
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            findings
                .OrderBy(finding => finding.RuleId, StringComparer.Ordinal)
                .ThenBy(finding => finding.File, StringComparer.Ordinal)
                .ThenBy(finding => finding.Line)
                .ToList(),
            loadFailures
        );
    }

    public static string Render(CorpusReport report)
    {
        var lines = new List<string>
        {
            $"Corpus scan: {report.Target}",
            $"  projects scanned: {report.ProjectsScanned} (failed to load: {report.ProjectsFailed})",
            $"  AM diagnostics:   {report.TotalDiagnostics}",
            string.Empty,
        };

        if (report.CountsByRule.Count == 0)
        {
            lines.Add("  No AM diagnostics reported.");
        }
        else
        {
            lines.Add("  Counts by rule:");
            foreach ((string ruleId, int count) in report.CountsByRule)
            {
                lines.Add($"    {ruleId}  {count}");
            }

            lines.Add(string.Empty);
            lines.Add("  Samples:");
            foreach (CorpusFinding finding in report.Findings)
            {
                lines.Add($"    {finding.RuleId} {finding.Severity} {finding.File}:{finding.Line}");
                lines.Add($"      {finding.Message}");
            }
        }

        if (report.LoadFailures.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("  Load failures (scan continued):");
            foreach (string failure in report.LoadFailures.Distinct().Take(20))
            {
                lines.Add($"    {failure}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string ToJson(CorpusReport report)
    {
        return JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    }
}

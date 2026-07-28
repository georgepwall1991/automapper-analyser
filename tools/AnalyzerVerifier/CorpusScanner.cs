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
        int ProjectsSkippedWithCompilerErrors,
        int AnalyzerCrashes,
        int TotalDiagnostics,
        Dictionary<string, int> CountsByRule,
        List<CorpusFinding> Findings,
        List<string> ProjectFailures,
        List<string> WorkspaceFailures
    );

    /// <summary>
    ///     Opens a project or solution, runs every catalogued analyzer over it, and returns what the
    ///     analyzers reported. Projects that cannot be loaded or do not compile are recorded rather than
    ///     aborting the scan: partial coverage of a real codebase beats none, provided the report is
    ///     honest about what was actually covered.
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
        var projectFailures = new List<string>();
        var scanned = 0;
        var failed = 0;
        var skippedWithCompilerErrors = 0;
        var analyzerCrashes = 0;

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
                projectFailures.Add(
                    $"{project.Name}: {exception.GetType().Name}: {exception.Message}"
                );
                continue;
            }

            if (compilation == null)
            {
                failed++;
                projectFailures.Add($"{project.Name}: compilation could not be created");
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

            // A project that evaluates but does not compile yields an incomplete semantic model, and
            // analyzers reading error types either invent findings or fall silent. Either way the result
            // is not evidence, so record it rather than counting it as covered.
            ImmutableArray<Diagnostic> compilerErrors = compilation
                .GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToImmutableArray();

            if (compilerErrors.Length > 0)
            {
                skippedWithCompilerErrors++;
                projectFailures.Add(
                    $"{project.Name}: skipped, {compilerErrors.Length} compiler error(s), first: {compilerErrors[0]}"
                );
                continue;
            }

            ImmutableArray<Diagnostic> diagnostics = await compilation
                .WithAnalyzers(analyzers)
                .GetAnalyzerDiagnosticsAsync();

            // AD0001 means an analyzer threw on this code. Dropping it because the ID is not "AM" would
            // let a crash read as a clean scan - the exact outcome this tool exists to prevent.
            ImmutableArray<Diagnostic> crashes = diagnostics
                .Where(diagnostic => string.Equals(diagnostic.Id, "AD0001", StringComparison.Ordinal))
                .ToImmutableArray();

            foreach (Diagnostic crash in crashes)
            {
                analyzerCrashes++;
                projectFailures.Add($"{project.Name}: analyzer crash (AD0001): {crash.GetMessage()}");
            }

            // Counting a project whose analyzers threw as scanned would overstate coverage: its findings
            // are missing whatever the crashed analyzer would have reported. Diagnostics gathered before
            // the crash are still worth recording, so they are kept below.
            if (crashes.Length == 0)
            {
                scanned++;
            }

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

        // Workspace-level failures are not per-project outcomes - a solution referencing a missing
        // project never becomes a Project at all - so they are counted separately. They are still
        // failures, and callers must treat them as incomplete coverage rather than noise.
        List<string> workspaceFailures = workspace
            .Diagnostics.Where(diagnostic => diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
            .Select(diagnostic => diagnostic.Message)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new CorpusReport(
            targetPath,
            scanned,
            failed,
            skippedWithCompilerErrors,
            analyzerCrashes,
            countsByRule.Values.Sum(),
            countsByRule
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            findings
                .OrderBy(finding => finding.RuleId, StringComparer.Ordinal)
                .ThenBy(finding => finding.File, StringComparer.Ordinal)
                .ThenBy(finding => finding.Line)
                .ToList(),
            projectFailures,
            workspaceFailures
        );
    }

    public static string Render(CorpusReport report)
    {
        var lines = new List<string>
        {
            $"Corpus scan: {report.Target}",
            $"  projects scanned: {report.ProjectsScanned} (failed to load: {report.ProjectsFailed}, "
                + $"skipped for compiler errors: {report.ProjectsSkippedWithCompilerErrors})",
            $"  AM diagnostics:   {report.TotalDiagnostics}",
            $"  analyzer crashes: {report.AnalyzerCrashes}",
            $"  workspace failures: {report.WorkspaceFailures.Count}",
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

        if (report.ProjectFailures.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("  Projects not scanned (scan continued):");
            foreach (
                string failure in report.ProjectFailures.Distinct(StringComparer.Ordinal).Take(20)
            )
            {
                lines.Add($"    {failure}");
            }
        }

        if (report.WorkspaceFailures.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("  Workspace failures:");
            foreach (string warning in report.WorkspaceFailures.Take(20))
            {
                lines.Add($"    {warning}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string ToJson(CorpusReport report)
    {
        return JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    }
}

using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AutoMapperAnalyzer.Analyzers;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;

namespace AnalyzerVerifier;

internal static class Program
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
        {
            PrintUsage();
            return args.Length == 0 ? 2 : 0;
        }

        string? repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
        if (repoRoot == null)
        {
            Console.Error.WriteLine("Could not find repository root.");
            return 1;
        }

        bool checkCatalog = args.Contains("--check-catalog", StringComparer.Ordinal);
        bool updateCatalog = args.Contains("--update-catalog", StringComparer.Ordinal);
        bool checkSnapshots = args.Contains("--check-snapshots", StringComparer.Ordinal);
        bool updateSnapshots = args.Contains("--update-snapshots", StringComparer.Ordinal);

        if ((!checkCatalog && !updateCatalog && !checkSnapshots && !updateSnapshots) ||
            (checkCatalog && updateCatalog) ||
            (checkSnapshots && updateSnapshots))
        {
            PrintUsage();
            return 2;
        }

        int failures = 0;

        if (checkCatalog || updateCatalog)
        {
            string catalogPath = Path.Combine(repoRoot, "docs", "RULE_CATALOG.md");
            string content = GenerateRuleCatalogMarkdown();
            failures += WriteOrCheckFile(catalogPath, content, updateCatalog, "rule catalog");
        }

        if (checkSnapshots || updateSnapshots)
        {
            string snapshotPath = Path.Combine(
                repoRoot,
                "tests",
                "AutoMapperAnalyzer.Tests",
                "Snapshots",
                "sample-diagnostics.json");
            IReadOnlyList<DiagnosticSnapshot> snapshots = await GenerateSampleDiagnosticSnapshotsAsync(repoRoot);
            string content = JsonSerializer.Serialize(snapshots, JsonOptions) + "\n";
            failures += WriteOrCheckFile(snapshotPath, content, updateSnapshots, "sample diagnostics snapshot");
        }

        return failures == 0 ? 0 : 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("AutoMapper Analyzer trust verifier");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project tools/AnalyzerVerifier -- --check-catalog");
        Console.WriteLine("  dotnet run --project tools/AnalyzerVerifier -- --update-catalog");
        Console.WriteLine("  dotnet run --project tools/AnalyzerVerifier -- --check-snapshots");
        Console.WriteLine("  dotnet run --project tools/AnalyzerVerifier -- --update-snapshots");
        Console.WriteLine();
        Console.WriteLine("Modes can be combined, for example: --check-catalog --check-snapshots.");
    }

    private static int WriteOrCheckFile(string path, string expectedContent, bool update, string description)
    {
        if (update)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, expectedContent, Utf8NoBom);
            Console.WriteLine($"Updated {description}: {path}");
            return 0;
        }

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Missing {description}: {path}");
            return 1;
        }

        string actualContent = File.ReadAllText(path, Utf8NoBom);
        if (string.Equals(actualContent, expectedContent, StringComparison.Ordinal))
        {
            Console.WriteLine($"{description} is up to date.");
            return 0;
        }

        Console.Error.WriteLine($"Stale {description}: {path}");
        Console.Error.WriteLine("Run the matching --update mode and review the generated diff.");
        return 1;
    }

    private static string GenerateRuleCatalogMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Rule Catalog");
        sb.AppendLine();
        sb.AppendLine("This file is generated from `RuleCatalog`.");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Package version: `{RuleCatalog.CurrentPackageVersion}`");
        sb.AppendLine();
        sb.AppendLine("| Rule ID | Descriptor Title | Severity | Category | Analyzer | Code Fix | Trust | Sample | Docs |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- |");

        foreach (RuleCatalogEntry rule in RuleCatalog.Rules.OrderBy(rule => rule.RuleId, StringComparer.Ordinal))
        {
            foreach (DiagnosticDescriptor descriptor in rule.Descriptors)
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"| `{rule.RuleId}` | {EscapeMarkdownTableCell(descriptor.Title.ToString(CultureInfo.InvariantCulture))} | `{descriptor.DefaultSeverity}` | `{descriptor.Category}` | `{rule.AnalyzerType.Name}` | `{rule.CodeFixProviderType.Name}` | `{rule.FixTrustLevel}` | [`{rule.SamplePath}`](../{rule.SamplePath}) | [docs]({BuildDocsLink(rule.DocumentationAnchor)}) |");
            }
        }

        return sb.ToString();
    }

    private static async Task<IReadOnlyList<DiagnosticSnapshot>> GenerateSampleDiagnosticSnapshotsAsync(string repoRoot)
    {
        RegisterMsBuild();

        string projectPath = Path.Combine(
            repoRoot,
            "samples",
            "AutoMapperAnalyzer.Samples",
            "AutoMapperAnalyzer.Samples.csproj");

        using var workspace = MSBuildWorkspace.Create();
        Project project = await workspace.OpenProjectAsync(projectPath);
        Compilation? compilation = await project.GetCompilationAsync();
        if (compilation == null)
        {
            throw new InvalidOperationException("Sample project compilation could not be created.");
        }

        ImmutableArray<DiagnosticAnalyzer> analyzers = RuleCatalog.Rules
            .Select(rule => rule.AnalyzerType)
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type)!)
            .ToImmutableArray();

        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers(analyzers)
            .GetAnalyzerDiagnosticsAsync();

        Dictionary<string, CodeFixTrustLevel> trustByRuleId = RuleCatalog.Rules.ToDictionary(
            rule => rule.RuleId,
            rule => rule.FixTrustLevel,
            StringComparer.Ordinal);

        return diagnostics
            .Where(diagnostic => diagnostic.Id.StartsWith("AM", StringComparison.Ordinal))
            .Select(diagnostic => ToSnapshot(repoRoot, diagnostic, trustByRuleId))
            .OrderBy(snapshot => snapshot.RuleId, StringComparer.Ordinal)
            .ThenBy(snapshot => snapshot.FilePath, StringComparer.Ordinal)
            .ThenBy(snapshot => snapshot.Line)
            .ThenBy(snapshot => snapshot.Column)
            .ThenBy(snapshot => snapshot.Message, StringComparer.Ordinal)
            .ToArray();
    }

    private static DiagnosticSnapshot ToSnapshot(
        string repoRoot,
        Diagnostic diagnostic,
        Dictionary<string, CodeFixTrustLevel> trustByRuleId)
    {
        FileLinePositionSpan lineSpan = diagnostic.Location.GetLineSpan();
        string filePath = lineSpan.Path.Length == 0
            ? string.Empty
            : Path.GetRelativePath(repoRoot, lineSpan.Path).Replace(Path.DirectorySeparatorChar, '/');

        return new DiagnosticSnapshot(
            diagnostic.Id,
            diagnostic.Severity.ToString(),
            diagnostic.Descriptor.Title.ToString(CultureInfo.InvariantCulture),
            diagnostic.Descriptor.Category,
            filePath,
            lineSpan.StartLinePosition.Line + 1,
            lineSpan.StartLinePosition.Character + 1,
            diagnostic.GetMessage(CultureInfo.InvariantCulture),
            trustByRuleId.TryGetValue(diagnostic.Id, out CodeFixTrustLevel trustLevel)
                ? trustLevel.ToString()
                : string.Empty);
    }

    private static void RegisterMsBuild()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            EnsureDotNetHostPath();
            MSBuildLocator.RegisterDefaults();
        }
    }

    private static void EnsureDotNetHostPath()
    {
        string? currentHostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(currentHostPath) && File.Exists(currentHostPath))
        {
            return;
        }

        string? pathHost = FindDotNetOnPath();
        string[] candidates =
        [
            pathHost ?? string.Empty,
            "/opt/homebrew/bin/dotnet",
            "/usr/local/bin/dotnet",
            "/usr/bin/dotnet"
        ];

        string? dotnetPath = candidates.FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(dotnetPath))
        {
            Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", dotnetPath);
            string? dotnetDirectory = Path.GetDirectoryName(dotnetPath);
            if (!string.IsNullOrWhiteSpace(dotnetDirectory))
            {
                Environment.SetEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", dotnetDirectory);
                string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                if (!currentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Contains(dotnetDirectory, StringComparer.Ordinal))
                {
                    Environment.SetEnvironmentVariable("PATH", dotnetDirectory + Path.PathSeparator + currentPath);
                }
            }
        }
    }

    private static string? FindDotNetOnPath()
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, "dotnet");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string BuildDocsLink(string documentationAnchor)
    {
        return "DIAGNOSTIC_RULES.md#" + ToMarkdownAnchor(documentationAnchor);
    }

    private static string ToMarkdownAnchor(string heading)
    {
        string text = heading.TrimStart('#').Trim().ToLowerInvariant();
        var sb = new StringBuilder();
        bool previousDash = false;

        foreach (char c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                previousDash = false;
            }
            else if (char.IsWhiteSpace(c) || c == '-')
            {
                if (!previousDash)
                {
                    sb.Append('-');
                    previousDash = true;
                }
            }
        }

        return sb.ToString().Trim('-');
    }

    private static string EscapeMarkdownTableCell(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static string? FindRepoRoot(string currentPath)
    {
        var directory = new DirectoryInfo(currentPath);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "automapper-analyser.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private sealed record DiagnosticSnapshot(
        string RuleId,
        string Severity,
        string Title,
        string Category,
        string FilePath,
        int Line,
        int Column,
        string Message,
        string FixTrustLevel);
}

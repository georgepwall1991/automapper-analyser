using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AutoMapperAnalyzer.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Tests.Trust;

public partial class RuleCatalogTests
{
    [Fact]
    public void RuleCatalog_ShouldMatchAnalyzerDescriptorsAndFixers()
    {
        Assert.Equal(RuleCatalog.Rules.Length, RuleCatalog.Rules.Select(rule => rule.RuleId).Distinct().Count());

        foreach (RuleCatalogEntry rule in RuleCatalog.Rules)
        {
            Assert.All(rule.Descriptors, descriptor => Assert.Equal(rule.RuleId, descriptor.Id));
            Assert.True(Enum.IsDefined(rule.FixTrustLevel));

            var analyzer = Assert.IsAssignableFrom<DiagnosticAnalyzer>(
                Activator.CreateInstance(rule.AnalyzerType));
            ImmutableArray<DiagnosticDescriptor> supportedDiagnostics = analyzer.SupportedDiagnostics;

            Assert.Equal(
                rule.Descriptors.Select(ToDescriptorSnapshot),
                supportedDiagnostics.Select(ToDescriptorSnapshot));

            var fixer = Assert.IsAssignableFrom<CodeFixProvider>(
                Activator.CreateInstance(rule.CodeFixProviderType));
            Assert.Contains(rule.RuleId, fixer.FixableDiagnosticIds);
        }
    }

    [Fact]
    public void RuleCatalog_ShouldCoverEveryShippedAnalyzerAndFixProvider()
    {
        Type[] analyzerTypes = GetShippedTypes<DiagnosticAnalyzer>()
            .Where(type => type.GetCustomAttributes<DiagnosticAnalyzerAttribute>().Any())
            .ToArray();
        Type[] codeFixTypes = GetShippedTypes<CodeFixProvider>()
            .Where(type => type.GetCustomAttributes<ExportCodeFixProviderAttribute>().Any())
            .ToArray();

        Type[] catalogAnalyzerTypes = RuleCatalog.Rules
            .Select(rule => rule.AnalyzerType)
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        Type[] catalogCodeFixTypes = RuleCatalog.Rules
            .Select(rule => rule.CodeFixProviderType)
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            analyzerTypes.Select(type => type.FullName),
            catalogAnalyzerTypes.Select(type => type.FullName));
        Assert.Equal(
            codeFixTypes.Select(type => type.FullName),
            catalogCodeFixTypes.Select(type => type.FullName));
    }

    [Fact]
    public void RuleCatalog_ShouldPointToDocumentedRulesAndSamples()
    {
        string repoRoot = GetRepositoryRoot();
        string ruleDocs = File.ReadAllText(Path.Combine(repoRoot, "docs", "DIAGNOSTIC_RULES.md"));

        foreach (RuleCatalogEntry rule in RuleCatalog.Rules)
        {
            Assert.Contains(rule.DocumentationAnchor, ruleDocs, StringComparison.Ordinal);
            Assert.True(
                File.Exists(Path.Combine(repoRoot, rule.SamplePath)),
                $"{rule.RuleId} sample path does not exist: {rule.SamplePath}");
        }
    }

    [Fact]
    public void VersionReferences_ShouldStayAlignedWithPackageMetadata()
    {
        string repoRoot = GetRepositoryRoot();
        string currentVersion = GetPackageVersion(repoRoot);
        Assert.Equal(RuleCatalog.CurrentPackageVersion, currentVersion);

        string readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        Assert.Contains($"Latest Release: v{currentVersion}", readme, StringComparison.Ordinal);

        string changelog = File.ReadAllText(Path.Combine(repoRoot, "CHANGELOG.md"));
        Assert.Contains($"## [{currentVersion}]", changelog, StringComparison.Ordinal);

        var analyzerProject = XDocument.Load(Path.Combine(repoRoot, "src", "AutoMapperAnalyzer.Analyzers", "AutoMapperAnalyzer.Analyzers.csproj"));
        string packageReleaseNotes = analyzerProject.Root?.Element("PropertyGroup")?.Element("PackageReleaseNotes")?.Value ?? string.Empty;
        Assert.Contains($"Version {currentVersion} -", packageReleaseNotes, StringComparison.Ordinal);

        string[] markdownFiles =
        [
            Path.Combine(repoRoot, "README.md"),
            Path.Combine(repoRoot, "docs", "ARCHITECTURE.md"),
            Path.Combine(repoRoot, "docs", "CI-CD.md"),
            Path.Combine(repoRoot, "docs", "COMPATIBILITY.md"),
            Path.Combine(repoRoot, "docs", "DIAGNOSTIC_RULES.md")
        ];

        foreach (string markdownFile in markdownFiles)
        {
            string content = File.ReadAllText(markdownFile);
            foreach (Match match in AnalyzerPackageVersionRegex().Matches(content))
            {
                Assert.Equal(currentVersion, match.Groups["version"].Value);
            }

            foreach (Match match in DotnetAddPackageVersionRegex().Matches(content))
            {
                Assert.StartsWith(currentVersion, match.Groups["version"].Value, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void ToolingAndPackageMetadata_ShouldHaveTrustChecks()
    {
        string repoRoot = GetRepositoryRoot();

        var analyzerProject = XDocument.Load(Path.Combine(repoRoot, "src", "AutoMapperAnalyzer.Analyzers", "AutoMapperAnalyzer.Analyzers.csproj"));
        XElement propertyGroup = analyzerProject.Root!.Element("PropertyGroup")!;
        Assert.Equal(
            "README.md",
            propertyGroup.Element("PackageReadmeFile")?.Value);
        Assert.Equal(
            "icon.png",
            propertyGroup.Element("PackageIcon")?.Value);
        string[] productionSuppressions = (propertyGroup.Element("NoWarn")?.Value ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.DoesNotContain("RS2008", productionSuppressions);
        Assert.Contains("RS2001", productionSuppressions);

        string projectXml = File.ReadAllText(Path.Combine(repoRoot, "src", "AutoMapperAnalyzer.Analyzers", "AutoMapperAnalyzer.Analyzers.csproj"));
        Assert.Contains(@"PackagePath=""analyzers\dotnet\cs\", projectXml, StringComparison.Ordinal);
        Assert.Contains(@"PackagePath=""analyzers\dotnet\", projectXml, StringComparison.Ordinal);

        using JsonDocument globalJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(repoRoot, "global.json")));
        string sdkVersion = globalJson.RootElement.GetProperty("sdk").GetProperty("version").GetString()!;
        Assert.Matches(@"^\d+\.\d+\.[1-9]\d{2}$", sdkVersion);

        string ci = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "ci.yml"));
        Assert.Contains("DOTNET_VERSION: '10.0.x'", ci, StringComparison.Ordinal);
        Assert.Contains("-warnaserror", ci, StringComparison.Ordinal);
        Assert.Contains("Verify package contents", ci, StringComparison.Ordinal);
        Assert.Contains("--check-catalog --check-snapshots", ci, StringComparison.Ordinal);
        Assert.Contains("tools/package-smoke.sh", ci, StringComparison.Ordinal);

        string shippedReleases = File.ReadAllText(Path.Combine(repoRoot, "src", "AutoMapperAnalyzer.Analyzers", "AnalyzerReleases.Shipped.md"));
        string unshippedReleases = File.ReadAllText(Path.Combine(repoRoot, "src", "AutoMapperAnalyzer.Analyzers", "AnalyzerReleases.Unshipped.md"));
        Assert.Contains($"## Release {RuleCatalog.CurrentPackageVersion}", shippedReleases, StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(unshippedReleases), "Unshipped release tracking should be empty between releases.");
        foreach (string ruleId in RuleCatalog.Rules.Select(rule => rule.RuleId))
        {
            Assert.Contains($"{ruleId} |", shippedReleases, StringComparison.Ordinal);
        }
    }

    private static DescriptorSnapshot ToDescriptorSnapshot(
        DiagnosticDescriptor descriptor)
    {
        return new DescriptorSnapshot(
            descriptor.Id,
            descriptor.Title.ToString(),
            descriptor.MessageFormat.ToString(),
            descriptor.Category,
            descriptor.DefaultSeverity,
            descriptor.IsEnabledByDefault,
            descriptor.Description.ToString(),
            descriptor.HelpLinkUri,
            string.Join(",", descriptor.CustomTags.OrderBy(tag => tag, StringComparer.Ordinal)));
    }

    private static Type[] GetShippedTypes<TBase>()
    {
        return typeof(RuleCatalog).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract)
            .Where(type => type.Namespace?.StartsWith("AutoMapperAnalyzer.Analyzers", StringComparison.Ordinal) == true)
            .Where(type => typeof(TBase).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetPackageVersion(string repoRoot)
    {
        XDocument project = XDocument.Load(
            Path.Combine(repoRoot, "src", "AutoMapperAnalyzer.Analyzers", "AutoMapperAnalyzer.Analyzers.csproj"));
        XElement propertyGroup = project.Root!.Element("PropertyGroup")!;
        return string.Join(
            ".",
            propertyGroup.Element("MajorVersion")!.Value,
            propertyGroup.Element("MinorVersion")!.Value,
            propertyGroup.Element("PatchVersion")!.Value);
    }

    private static string GetRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "automapper-analyser.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }

    [GeneratedRegex(@"AutoMapperAnalyzer\.Analyzers""\s+Version=""(?<version>\d+\.\d+\.\d+)""")]
    private static partial Regex AnalyzerPackageVersionRegex();

    [GeneratedRegex(@"dotnet add package AutoMapperAnalyzer\.Analyzers --version (?<version>\d+\.\d+\.\d+(?:-[\w.-]+)?)")]
    private static partial Regex DotnetAddPackageVersionRegex();

    private sealed record DescriptorSnapshot(
        string Id,
        string Title,
        string MessageFormat,
        string Category,
        DiagnosticSeverity Severity,
        bool IsEnabledByDefault,
        string Description,
        string HelpLinkUri,
        string CustomTags);
}

using System.Collections.Immutable;
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
        Assert.Equal(14, RuleCatalog.Rules.Length);
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
        Assert.Equal(
            "README.md",
            analyzerProject.Root?.Element("PropertyGroup")?.Element("PackageReadmeFile")?.Value);
        Assert.Equal(
            "icon.png",
            analyzerProject.Root?.Element("PropertyGroup")?.Element("PackageIcon")?.Value);

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
    }

    private static (string Id, string Title, DiagnosticSeverity Severity, string Category) ToDescriptorSnapshot(
        DiagnosticDescriptor descriptor)
    {
        return (descriptor.Id, descriptor.Title.ToString(), descriptor.DefaultSeverity, descriptor.Category);
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
}

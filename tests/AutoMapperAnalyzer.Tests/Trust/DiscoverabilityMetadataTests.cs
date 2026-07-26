using System.Text.RegularExpressions;
using System.Xml.Linq;
using AutoMapperAnalyzer.Analyzers;

namespace AutoMapperAnalyzer.Tests.Trust;

/// <summary>
/// Guards NuGet/GitHub discoverability assets: package description/tags, README funnel,
/// and product-flow visuals that ship with PackageReadmeFile.
/// </summary>
public sealed class DiscoverabilityMetadataTests
{
    [Fact]
    public void Analyzer_package_description_and_tags_include_high_intent_automapper_terms()
    {
        string repoRoot = GetRepositoryRoot();
        var csproj = XDocument.Load(
            Path.Combine(repoRoot, "src", "AutoMapperAnalyzer.Analyzers", "AutoMapperAnalyzer.Analyzers.csproj"));

        XElement propertyGroup = csproj.Root!.Element("PropertyGroup")!;
        string description = propertyGroup.Element("Description")?.Value ?? string.Empty;
        string tags = propertyGroup.Element("PackageTags")?.Value ?? string.Empty;
        string title = propertyGroup.Element("Title")?.Value ?? string.Empty;
        string readmeFile = propertyGroup.Element("PackageReadmeFile")?.Value ?? string.Empty;

        Assert.Equal("README.md", readmeFile);
        Assert.Contains("CreateMap", title, StringComparison.Ordinal);
        Assert.Contains("mapping validation", title, StringComparison.OrdinalIgnoreCase);

        foreach (string term in new[]
                 {
                     "CreateMap",
                     "Profile",
                     "ForMember",
                     "MapFrom",
                     "ProjectTo",
                     "AutoMapperMappingException",
                     "nullable",
                     "unmapped",
                     "Roslyn",
                 })
        {
            Assert.True(
                description.Contains(term, StringComparison.Ordinal),
                $"Analyzer Description must contain '{term}' for NuGet search discoverability.");
        }

        foreach (string tag in new[]
                 {
                     "CreateMap",
                     "Profile",
                     "ForMember",
                     "MapFrom",
                     "ProjectTo",
                     "ReverseMap",
                     "nullable",
                     "unmapped",
                     "roslyn-analyzer",
                     "analyzers",
                     "AutoMapperMappingException",
                     "code-fix",
                 })
        {
            Assert.True(
                tags.Contains(tag, StringComparison.Ordinal),
                $"Analyzer PackageTags must include '{tag}'.");
        }
    }

    [Fact]
    public void Readme_conversion_funnel_and_product_visuals_exist_with_resolvable_paths()
    {
        string repoRoot = GetRepositoryRoot();
        string readmePath = Path.Combine(repoRoot, "README.md");
        string readme = File.ReadAllText(readmePath);

        foreach (string section in new[]
                 {
                     "## The problem",
                     "## What it catches",
                     "## Install",
                     "## See it work",
                     "## 30-second path",
                     "## Feature snapshot",
                     "## Complete Analyzer Coverage",
                 })
        {
            Assert.Contains(section, readme, StringComparison.Ordinal);
        }

        Assert.Contains("PrivateAssets=\"all\"", readme, StringComparison.Ordinal);
        Assert.Contains($"Version=\"{RuleCatalog.CurrentPackageVersion}\"", readme, StringComparison.Ordinal);
        Assert.Contains($"Latest Release: v{RuleCatalog.CurrentPackageVersion}", readme, StringComparison.Ordinal);
        Assert.Contains("AM001", readme, StringComparison.Ordinal);
        Assert.Contains("AM060", readme, StringComparison.Ordinal);
        Assert.Contains("stays quiet", readme, StringComparison.OrdinalIgnoreCase);

        // NuGet.org requires absolute HTTPS image URLs in PackageReadmeFile content.
        const string rawBase =
            "https://raw.githubusercontent.com/georgepwall1991/automapper-analyser/main/";

        string[] visualAssets =
        [
            "assets/flow-ide-diagnostics.svg",
            "assets/flow-before-after-fix.svg",
            "assets/flow-analyzer-ci-loop.svg",
        ];

        foreach (string asset in visualAssets)
        {
            Assert.Contains(rawBase + asset, readme, StringComparison.Ordinal);
            string fullPath = Path.Combine(repoRoot, asset);
            Assert.True(File.Exists(fullPath), $"Missing README visual: {asset}");
            Assert.True(new FileInfo(fullPath).Length > 0, $"Empty README visual: {asset}");
        }

        // Relative image paths break NuGet.org README rendering — require HTTPS.
        IEnumerable<string> imageRefs = Regex.Matches(readme, @"!\[[^\]]*\]\(([^)]+)\)")
            .Select(m => m.Groups[1].Value)
            .Concat(Regex.Matches(readme, @"<img[^>]+src=""([^""]+)""")
                .Select(m => m.Groups[1].Value))
            .Distinct(StringComparer.Ordinal);

        foreach (string imageRef in imageRefs)
        {
            Assert.True(
                imageRef.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
                $"README image must use absolute HTTPS for NuGet rendering: {imageRef}");
        }
    }

    [Fact]
    public void Analyzer_packs_all_assets_for_nuget_readme_rendering()
    {
        string repoRoot = GetRepositoryRoot();
        var analyzer = XDocument.Load(
            Path.Combine(repoRoot, "src", "AutoMapperAnalyzer.Analyzers", "AutoMapperAnalyzer.Analyzers.csproj"));

        Assert.Contains(
            analyzer.Descendants("None"),
            n => (n.Attribute("Include")?.Value ?? string.Empty).Contains("assets", StringComparison.Ordinal)
                && string.Equals(n.Attribute("Pack")?.Value, "true", StringComparison.OrdinalIgnoreCase)
                && (n.Attribute("PackagePath")?.Value ?? string.Empty).Contains("assets", StringComparison.Ordinal));
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
}

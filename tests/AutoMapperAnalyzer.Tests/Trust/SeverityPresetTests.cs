using System.Text.RegularExpressions;
using AutoMapperAnalyzer.Analyzers;
using Microsoft.CodeAnalysis;

namespace AutoMapperAnalyzer.Tests.Trust;

/// <summary>
///     The shipped severity presets are a published contract: a consumer inherits them and expects every
///     rule to be covered and every severity to be real. These tests keep them in lockstep with
///     <see cref="RuleCatalog" /> so a new rule cannot ship with no entry in either preset.
/// </summary>
public class SeverityPresetTests
{
    private const string RecommendedPreset = "AutoMapperAnalyzer.Recommended.globalconfig";
    private const string MinimalPreset = "AutoMapperAnalyzer.Minimal.globalconfig";

    private static readonly string[] ValidSeverities =
    [
        "none",
        "silent",
        "suggestion",
        "warning",
        "error",
    ];

    [Theory]
    [InlineData(RecommendedPreset)]
    [InlineData(MinimalPreset)]
    public void Preset_ShouldCoverEveryCatalogRuleExactlyOnce(string presetFileName)
    {
        Dictionary<string, string> severities = ReadPreset(presetFileName);

        string[] catalogRuleIds = RuleCatalog
            .Rules.Select(rule => rule.RuleId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            catalogRuleIds,
            severities.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray()
        );
    }

    [Theory]
    [InlineData(RecommendedPreset)]
    [InlineData(MinimalPreset)]
    public void Preset_ShouldUseOnlyValidSeverities(string presetFileName)
    {
        foreach ((string ruleId, string severity) in ReadPreset(presetFileName))
        {
            Assert.True(
                ValidSeverities.Contains(severity, StringComparer.Ordinal),
                $"{presetFileName} sets {ruleId} to '{severity}', which is not a valid analyzer severity."
            );
        }
    }

    [Theory]
    [InlineData(RecommendedPreset)]
    [InlineData(MinimalPreset)]
    public void Preset_ShouldDeclareGlobalScope(string presetFileName)
    {
        string text = File.ReadAllText(ResolvePresetPath(presetFileName));
        Assert.Contains("is_global = true", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The Recommended preset advertises itself as matching shipped defaults, so it must actually do
    ///     so. A descriptor whose default severity changes without the preset following would publish a
    ///     silent behaviour change to anyone inheriting it.
    /// </summary>
    [Fact]
    public void RecommendedPreset_ShouldMatchShippedDescriptorDefaults()
    {
        Dictionary<string, string> severities = ReadPreset(RecommendedPreset);

        foreach (RuleCatalogEntry rule in RuleCatalog.Rules)
        {
            // A rule ID may expose several descriptors (AM002 is Error + Info). The preset speaks for the
            // rule ID as a consumer configures it, so compare against the most severe shipped descriptor.
            DiagnosticSeverity shipped = rule.Descriptors.Max(descriptor =>
                descriptor.DefaultSeverity
            );
            string expected = shipped switch
            {
                DiagnosticSeverity.Error => "error",
                DiagnosticSeverity.Warning => "warning",
                DiagnosticSeverity.Info => "suggestion",
                DiagnosticSeverity.Hidden => "silent",
                _ => throw new InvalidOperationException($"Unhandled severity {shipped}"),
            };

            Assert.True(
                string.Equals(severities[rule.RuleId], expected, StringComparison.Ordinal),
                $"Recommended preset sets {rule.RuleId} to '{severities[rule.RuleId]}' but its shipped "
                    + $"default is '{expected}'. The preset advertises parity with shipped defaults."
            );
        }
    }

    /// <summary>
    ///     The Minimal preset exists so a large existing codebase can enable the analyzer without a wall
    ///     of build errors. If any rule in it is an error, it does not do the one job it claims.
    /// </summary>
    [Fact]
    public void MinimalPreset_ShouldNeverBreakTheBuild()
    {
        foreach ((string ruleId, string severity) in ReadPreset(MinimalPreset))
        {
            Assert.False(
                string.Equals(severity, "error", StringComparison.Ordinal),
                $"Minimal preset sets {ruleId} to error, which defeats its purpose as an adoption ramp."
            );
        }
    }

    /// <summary>
    ///     Minimal is an adoption ramp, not a different product: it may only relax severities relative to
    ///     Recommended, never tighten them.
    /// </summary>
    [Fact]
    public void MinimalPreset_ShouldNotBeStricterThanRecommended()
    {
        Dictionary<string, string> recommended = ReadPreset(RecommendedPreset);
        Dictionary<string, string> minimal = ReadPreset(MinimalPreset);

        foreach ((string ruleId, string minimalSeverity) in minimal)
        {
            int minimalRank = Array.IndexOf(ValidSeverities, minimalSeverity);
            int recommendedRank = Array.IndexOf(ValidSeverities, recommended[ruleId]);

            Assert.True(
                minimalRank <= recommendedRank,
                $"Minimal preset sets {ruleId} to '{minimalSeverity}', stricter than Recommended's "
                    + $"'{recommended[ruleId]}'."
            );
        }
    }

    [Fact]
    public void Presets_ShouldBePackedIntoTheAnalyzerPackage()
    {
        string projectPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "AutoMapperAnalyzer.Analyzers",
            "AutoMapperAnalyzer.Analyzers.csproj"
        );
        string project = File.ReadAllText(projectPath);

        Assert.Contains(".globalconfig", project, StringComparison.Ordinal);
        Assert.Contains("PackagePath=\"config\"", project, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> ReadPreset(string presetFileName)
    {
        var severities = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (string line in File.ReadAllLines(ResolvePresetPath(presetFileName)))
        {
            Match match = Regex.Match(
                line.Trim(),
                @"^dotnet_diagnostic\.(?<rule>AM\d+)\.severity\s*=\s*(?<severity>[a-z]+)$"
            );

            if (!match.Success)
            {
                continue;
            }

            string ruleId = match.Groups["rule"].Value;
            Assert.False(
                severities.ContainsKey(ruleId),
                $"{presetFileName} configures {ruleId} more than once; the last entry would silently win."
            );

            severities[ruleId] = match.Groups["severity"].Value;
        }

        return severities;
    }

    private static string ResolvePresetPath(string presetFileName)
    {
        return Path.Combine(RepositoryRoot(), "config", presetFileName);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (
            directory != null
            && !File.Exists(Path.Combine(directory.FullName, "automapper-analyser.sln"))
        )
        {
            directory = directory.Parent;
        }

        Assert.True(
            directory != null,
            "Could not locate the repository root from the test output directory."
        );
        return directory!.FullName;
    }
}

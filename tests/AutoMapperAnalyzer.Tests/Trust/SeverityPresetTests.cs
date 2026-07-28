using System.Text.RegularExpressions;
using AutoMapperAnalyzer.Analyzers;
using Microsoft.CodeAnalysis;

namespace AutoMapperAnalyzer.Tests.Trust;

/// <summary>
///     The shipped severity presets are a published contract: a consumer inherits them and expects every
///     rule to be covered and every severity to be real. These tests keep them in lockstep with
///     <see cref="RuleCatalog" /> so a new rule cannot ship with no entry, and so neither preset can
///     quietly stop honouring what it advertises.
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
    public void Preset_ShouldOnlyConfigureCatalogRules(string presetFileName)
    {
        HashSet<string> catalogRuleIds = RuleCatalog
            .Rules.Select(rule => rule.RuleId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string ruleId in ReadPreset(presetFileName).Keys)
        {
            Assert.True(
                catalogRuleIds.Contains(ruleId),
                $"{presetFileName} configures unknown rule {ruleId}."
            );
        }
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
        Assert.Contains(
            "is_global = true",
            File.ReadAllText(ResolvePresetPath(presetFileName)),
            StringComparison.Ordinal
        );
    }

    /// <summary>
    ///     A severity setting is keyed by rule ID, but a rule ID can expose several descriptors at
    ///     different shipped severities — AM002 ships nullable-to-non-nullable as Error and
    ///     non-nullable-to-nullable as Info. One ID-level override applies to every descriptor, so it
    ///     cannot reproduce a mixed default: it would silently promote the informational descriptor and
    ///     fail builds on mappings that are safe today. Recommended must leave those rule IDs alone.
    /// </summary>
    [Fact]
    public void RecommendedPreset_ShouldOmitRuleIdsWithMixedDescriptorSeverities()
    {
        Dictionary<string, string> severities = ReadPreset(RecommendedPreset);

        foreach (RuleCatalogEntry rule in RuleCatalog.Rules.Where(HasMixedDescriptorSeverities))
        {
            Assert.False(
                severities.ContainsKey(rule.RuleId),
                $"Recommended preset overrides {rule.RuleId}, whose descriptors ship at different "
                    + "severities. A single ID-level override cannot preserve that and would change behaviour."
            );
        }
    }

    [Fact]
    public void RecommendedPreset_ShouldCoverEverySingleSeverityRule()
    {
        Dictionary<string, string> severities = ReadPreset(RecommendedPreset);

        foreach (
            RuleCatalogEntry rule in RuleCatalog.Rules.Where(rule =>
                !HasMixedDescriptorSeverities(rule)
            )
        )
        {
            Assert.True(
                severities.ContainsKey(rule.RuleId),
                $"Recommended preset does not configure {rule.RuleId}."
            );
        }
    }

    /// <summary>
    ///     Minimal must cover every rule, including mixed-severity IDs: leaving one out would let its
    ///     Error descriptor keep breaking the build, which is the single thing this preset exists to
    ///     prevent.
    /// </summary>
    [Fact]
    public void MinimalPreset_ShouldCoverEveryCatalogRule()
    {
        Dictionary<string, string> severities = ReadPreset(MinimalPreset);

        foreach (RuleCatalogEntry rule in RuleCatalog.Rules)
        {
            Assert.True(
                severities.ContainsKey(rule.RuleId),
                $"Minimal preset does not configure {rule.RuleId}; an error descriptor would still break the build."
            );
        }
    }

    /// <summary>
    ///     Recommended advertises parity with shipped defaults, so it must actually have it. A descriptor
    ///     whose default severity changes without the preset following would publish a silent behaviour
    ///     change to everyone inheriting it.
    /// </summary>
    [Fact]
    public void RecommendedPreset_ShouldMatchShippedDescriptorDefaults()
    {
        Dictionary<string, string> severities = ReadPreset(RecommendedPreset);

        foreach (
            RuleCatalogEntry rule in RuleCatalog.Rules.Where(rule =>
                !HasMixedDescriptorSeverities(rule)
            )
        )
        {
            // Every descriptor for this ID ships at the same severity, so one override reproduces it
            // exactly. Mixed-severity IDs are asserted separately and must be omitted entirely.
            string expected = ToEditorConfigSeverity(rule.Descriptors[0].DefaultSeverity);

            Assert.True(
                string.Equals(severities[rule.RuleId], expected, StringComparison.Ordinal),
                $"Recommended preset sets {rule.RuleId} to '{severities[rule.RuleId]}' but its shipped "
                    + $"default is '{expected}'. The preset advertises parity with shipped defaults."
            );
        }
    }

    /// <summary>
    ///     Minimal exists so a large existing codebase can enable the analyzer without a wall of build
    ///     errors, so no rule may be reported at error severity. It cannot control a consumer's
    ///     <c>TreatWarningsAsErrors</c>, which promotes warnings independently of analyzer configuration
    ///     — the preset documents <c>WarningsNotAsErrors</c> for that case rather than pretending to
    ///     override it.
    /// </summary>
    [Fact]
    public void MinimalPreset_ShouldNeverUseErrorSeverity()
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
    ///     Minimal documents a <c>WarningsNotAsErrors</c> escape hatch for projects that build with
    ///     warnings-as-errors. That list must name every rule the preset actually sets to warning,
    ///     otherwise the documented mitigation silently misses rules.
    /// </summary>
    [Fact]
    public void MinimalPreset_ShouldDocumentEveryWarningInItsWarningsNotAsErrorsGuidance()
    {
        Dictionary<string, string> severities = ReadPreset(MinimalPreset);
        string text = File.ReadAllText(ResolvePresetPath(MinimalPreset));

        Match guidance = Regex.Match(
            text,
            @"<WarningsNotAsErrors>(?<value>[^<]*)</WarningsNotAsErrors>"
        );
        Assert.True(
            guidance.Success,
            "Minimal preset does not document a WarningsNotAsErrors escape hatch."
        );

        HashSet<string> documented = guidance
            .Groups["value"]
            .Value.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .Where(entry => entry.StartsWith("AM", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        foreach (
            (string ruleId, string severity) in severities.Where(entry => entry.Value == "warning")
        )
        {
            Assert.True(
                documented.Contains(ruleId),
                $"Minimal preset sets {ruleId} to warning but omits it from the documented "
                    + "WarningsNotAsErrors list, so the mitigation would not cover it."
            );
        }
    }

    /// <summary>
    ///     Minimal is an adoption ramp, not a different product: it may only relax severities relative to
    ///     Recommended, never tighten them. Rules Recommended omits are compared against their shipped
    ///     default instead.
    /// </summary>
    [Fact]
    public void MinimalPreset_ShouldNotBeStricterThanRecommended()
    {
        Dictionary<string, string> recommended = ReadPreset(RecommendedPreset);
        Dictionary<string, string> minimal = ReadPreset(MinimalPreset);

        foreach (RuleCatalogEntry rule in RuleCatalog.Rules)
        {
            string baseline = recommended.TryGetValue(rule.RuleId, out string? configured)
                ? configured
                : ToEditorConfigSeverity(
                    rule.Descriptors.Min(descriptor => descriptor.DefaultSeverity)
                );

            int minimalRank = Array.IndexOf(ValidSeverities, minimal[rule.RuleId]);
            int baselineRank = Array.IndexOf(ValidSeverities, baseline);

            Assert.True(
                minimalRank <= baselineRank,
                $"Minimal preset sets {rule.RuleId} to '{minimal[rule.RuleId]}', stricter than the "
                    + $"Recommended/shipped baseline '{baseline}'."
            );
        }
    }

    [Fact]
    public void Presets_ShouldBePackedIntoTheAnalyzerPackage()
    {
        string project = File.ReadAllText(
            Path.Combine(
                RepositoryRoot(),
                "src",
                "AutoMapperAnalyzer.Analyzers",
                "AutoMapperAnalyzer.Analyzers.csproj"
            )
        );

        Assert.Contains(".globalconfig", project, StringComparison.Ordinal);
        Assert.Contains("PackagePath=\"config\"", project, StringComparison.Ordinal);
    }

    private static bool HasMixedDescriptorSeverities(RuleCatalogEntry rule)
    {
        return rule.Descriptors.Select(descriptor => descriptor.DefaultSeverity).Distinct().Count()
            > 1;
    }

    private static string ToEditorConfigSeverity(DiagnosticSeverity severity)
    {
        return severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            DiagnosticSeverity.Info => "suggestion",
            DiagnosticSeverity.Hidden => "silent",
            _ => throw new InvalidOperationException($"Unhandled severity {severity}"),
        };
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

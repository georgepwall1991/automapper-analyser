using System.Text.RegularExpressions;
using AutoMapperAnalyzer.Analyzers;
using Microsoft.CodeAnalysis;

namespace AutoMapperAnalyzer.Tests.Trust;

/// <summary>
///     Help links are what an IDE offers behind "learn more" on a diagnostic. A missing one costs the
///     user a search; a wrong one sends them to a page that does not explain their diagnostic, which is
///     worse than offering nothing. These tests hold the links to the documentation that actually exists.
/// </summary>
public class RuleHelpLinkTests
{
    [Fact]
    public void EveryDescriptor_ShouldOfferAHelpLink()
    {
        foreach (RuleCatalogEntry rule in RuleCatalog.Rules)
        {
            foreach (DiagnosticDescriptor descriptor in rule.Descriptors)
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(descriptor.HelpLinkUri),
                    $"{rule.RuleId} descriptor '{descriptor.Id}' has no HelpLinkUri, so the IDE offers no "
                        + "way to read what the diagnostic means."
                );
            }
        }
    }

    /// <summary>
    ///     The anchor each link points at must exist as a heading in the documentation. A link to a
    ///     missing anchor silently lands at the top of a 77KB file, which reads as documentation that
    ///     does not cover the rule.
    /// </summary>
    [Fact]
    public void EveryHelpLink_ShouldResolveToARealDocumentationHeading()
    {
        string documentation = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "docs", "DIAGNOSTIC_RULES.md")
        );

        HashSet<string> availableAnchors = documentation
            .Split('\n')
            .Where(line => line.StartsWith("#", StringComparison.Ordinal))
            .Select(ToGitHubSlug)
            .ToHashSet(StringComparer.Ordinal);

        foreach (RuleCatalogEntry rule in RuleCatalog.Rules)
        {
            foreach (DiagnosticDescriptor descriptor in rule.Descriptors)
            {
                string anchor = descriptor.HelpLinkUri.Split('#').Last();

                Assert.True(
                    availableAnchors.Contains(anchor),
                    $"{rule.RuleId} links to '#{anchor}', which is not a heading in "
                        + "docs/DIAGNOSTIC_RULES.md. The link would land at the top of the document."
                );
            }
        }
    }

    [Fact]
    public void EveryHelpLink_ShouldBeAnAbsoluteHttpsUrl()
    {
        foreach (RuleCatalogEntry rule in RuleCatalog.Rules)
        {
            foreach (DiagnosticDescriptor descriptor in rule.Descriptors)
            {
                Assert.True(
                    Uri.TryCreate(descriptor.HelpLinkUri, UriKind.Absolute, out Uri? uri)
                        && uri.Scheme == Uri.UriSchemeHttps,
                    $"{rule.RuleId} help link '{descriptor.HelpLinkUri}' is not an absolute https URL. "
                        + "IDEs will not open a relative or non-https link."
                );
            }
        }
    }

    /// <summary>
    ///     Descriptors sharing a rule ID document the same rule, so they must point at the same section.
    ///     AM002 ships two descriptors and would otherwise be free to disagree.
    /// </summary>
    [Fact]
    public void DescriptorsSharingARuleId_ShouldShareAHelpLink()
    {
        foreach (
            RuleCatalogEntry rule in RuleCatalog.Rules.Where(rule => rule.Descriptors.Length > 1)
        )
        {
            string[] distinctLinks = rule
                .Descriptors.Select(descriptor => descriptor.HelpLinkUri)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            Assert.True(
                distinctLinks.Length == 1,
                $"{rule.RuleId} descriptors point at different documentation: "
                    + string.Join(", ", distinctLinks)
            );
        }
    }

    private static string ToGitHubSlug(string heading)
    {
        string text = heading.TrimStart('#').Trim().ToLowerInvariant();
        return new string(
            text.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-')
                .Select(c => c == ' ' ? '-' : c)
                .ToArray()
        );
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

        Assert.True(directory != null, "Could not locate the repository root.");
        return directory!.FullName;
    }
}

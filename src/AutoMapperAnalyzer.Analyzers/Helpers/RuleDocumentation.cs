using Microsoft.CodeAnalysis;

namespace AutoMapperAnalyzer.Analyzers.Helpers;

/// <summary>
///     The single source of the documentation anchor for each rule, and the help link built from it.
///     <para>
///     Diagnostic descriptors cannot read <c>RuleCatalog</c> — the catalog is built from the descriptors,
///     so the dependency runs the other way. Holding the anchors here lets both use one definition
///     instead of the descriptors repeating what the catalog already knows, where the two would drift
///     silently and the drift would only ever be visible to a user clicking a dead link.
///     </para>
/// </summary>
internal static class RuleDocumentation
{
    private const string DocumentationUrl =
        "https://github.com/georgepwall1991/automapper-analyser/blob/main/docs/DIAGNOSTIC_RULES.md";

    private static readonly Dictionary<string, string> AnchorsByRuleId = new(StringComparer.Ordinal)
    {
        ["AM001"] = "### AM001: Property Type Mismatch",
        ["AM002"] = "### AM002: Nullable Compatibility Issue",
        ["AM003"] = "### AM003: Collection Type Incompatibility",
        ["AM004"] = "### AM004: Missing Destination Property",
        ["AM005"] = "### AM005: Case Sensitivity Mismatch",
        ["AM006"] = "### AM006: Unmapped Destination Property",
        ["AM011"] = "### AM011: Unmapped Required Property",
        ["AM020"] = "### AM020: Nested Object Mapping Issue",
        ["AM021"] = "### AM021: Collection Element Mismatch",
        ["AM022"] = "### AM022: Infinite Recursion Risk",
        ["AM030"] = "### AM030: Invalid Type Converter Implementation",
        ["AM032"] = "### AM032: Type Converter Null Handling",
        ["AM033"] = "### AM033: Unused Type Converter",
        ["AM031"] = "### AM031: Multiple Enumeration",
        ["AM034"] = "### AM034: Expensive Operation",
        ["AM035"] = "### AM035: Expensive Computation",
        ["AM036"] = "### AM036: Sync-Over-Async",
        ["AM037"] = "### AM037: Complex LINQ",
        ["AM038"] = "### AM038: Non-Deterministic Operation",
        ["AM041"] = "### AM041: Duplicate Mapping Registration",
        ["AM050"] = "### AM050: Redundant MapFrom Configuration",
        ["AM060"] = "### AM060: Unregistered Type Map",
        ["AM061"] = "### AM061: Enum Member Mismatch",
    };

    /// <summary>
    ///     The markdown heading documenting a rule, as it appears in <c>docs/DIAGNOSTIC_RULES.md</c>.
    /// </summary>
    public static string AnchorFor(string ruleId)
    {
        return AnchorsByRuleId[ruleId];
    }

    /// <summary>
    ///     The URL an IDE offers behind "learn more" for a diagnostic. Points at the rule's section
    ///     rather than the top of the document, because a user who clicked through from a specific
    ///     diagnostic has already told you which rule they care about.
    /// </summary>
    public static string LinkFor(string ruleId)
    {
        return DocumentationUrl + "#" + ToGitHubSlug(AnchorsByRuleId[ruleId]);
    }

    /// <summary>
    ///     GitHub's heading-anchor rules: drop the leading hashes, lowercase, remove punctuation, and
    ///     replace spaces with hyphens.
    /// </summary>
    private static string ToGitHubSlug(string heading)
    {
        string text = heading.TrimStart('#').Trim().ToLowerInvariant();
        var slug = new System.Text.StringBuilder(text.Length);

        foreach (char character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                slug.Append(character);
            }
            else if (character is ' ' or '-')
            {
                slug.Append('-');
            }
        }

        return slug.ToString();
    }
}

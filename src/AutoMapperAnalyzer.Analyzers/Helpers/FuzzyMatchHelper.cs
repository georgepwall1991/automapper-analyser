using Microsoft.CodeAnalysis;

namespace AutoMapperAnalyzer.Analyzers.Helpers;

/// <summary>
///     Provides fuzzy string matching utilities for property name comparison in code fix providers.
/// </summary>
public static class FuzzyMatchHelper
{
    /// <summary>
    ///     Computes the Levenshtein distance between two strings.
    /// </summary>
    /// <param name="s">The first string.</param>
    /// <param name="t">The second string.</param>
    /// <returns>The edit distance between the two strings.</returns>
    public static int ComputeLevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.IsNullOrEmpty(t) ? 0 : t.Length;
        }

        if (string.IsNullOrEmpty(t))
        {
            return s.Length;
        }

        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++)
        {
            d[i, 0] = i;
        }

        for (int j = 0; j <= m; j++)
        {
            d[0, j] = j;
        }

        for (int j = 1; j <= m; j++)
        {
            for (int i = 1; i <= n; i++)
            {
                if (s[i - 1] == t[j - 1])
                {
                    d[i, j] = d[i - 1, j - 1];
                }
                else
                {
                    d[i, j] = Math.Min(Math.Min(
                            d[i - 1, j] + 1, // deletion
                            d[i, j - 1] + 1), // insertion
                        d[i - 1, j - 1] + 1 // substitution
                    );
                }
            }
        }

        return d[n, m];
    }

    /// <summary>
    ///     Determines whether a property is a fuzzy match candidate based on name similarity and type compatibility.
    ///     Returns true when the Levenshtein distance is 1 or 2, length difference at most 2, and types are compatible.
    ///     Exact matches (distance 0) return false since the analyzer wouldn't flag them.
    /// </summary>
    /// <param name="nameA">The first property name to compare.</param>
    /// <param name="propertyB">The second property symbol to compare against.</param>
    /// <param name="typeA">The type of the first property, used for compatibility checking.</param>
    /// <returns>True if the properties are fuzzy match candidates; otherwise, false.</returns>
    public static bool IsFuzzyMatchCandidate(string nameA, IPropertySymbol propertyB, ITypeSymbol typeA)
    {
        int distance = ComputeLevenshteinDistance(nameA, propertyB.Name);
        if (distance > 2 || Math.Abs(nameA.Length - propertyB.Name.Length) > 2)
        {
            return false;
        }

        // distance == 0 means exact match — the analyzer wouldn't flag it, so skip
        if (distance == 0)
        {
            return false;
        }

        return AutoMapperAnalysisHelpers.AreTypesCompatible(typeA, propertyB.Type);
    }

    /// <summary>
    ///     Finds all fuzzy match candidates from a collection of properties for a given target property name and type.
    /// </summary>
    /// <param name="targetPropertyName">The property name to find fuzzy matches for.</param>
    /// <param name="candidateProperties">The properties to search for matches.</param>
    /// <param name="targetPropertyType">The type of the target property, used for compatibility checking.</param>
    /// <returns>Properties that are fuzzy match candidates.</returns>
    public static IEnumerable<IPropertySymbol> FindFuzzyMatches(
        string targetPropertyName,
        IEnumerable<IPropertySymbol> candidateProperties,
        ITypeSymbol targetPropertyType)
    {
        return candidateProperties.Where(p => IsFuzzyMatchCandidate(targetPropertyName, p, targetPropertyType));
    }
}

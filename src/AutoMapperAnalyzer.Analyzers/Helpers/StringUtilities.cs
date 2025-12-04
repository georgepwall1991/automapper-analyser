namespace AutoMapperAnalyzer.Analyzers.Helpers;

/// <summary>
///     Provides string utility methods for AutoMapper analyzers.
/// </summary>
public static class StringUtilities
{
    /// <summary>
    ///     Computes the Levenshtein distance between two strings.
    ///     The Levenshtein distance is the minimum number of single-character edits
    ///     (insertions, deletions, or substitutions) required to change one word into the other.
    /// </summary>
    /// <param name="source">The source string.</param>
    /// <param name="target">The target string.</param>
    /// <returns>The edit distance between the two strings.</returns>
    public static int ComputeLevenshteinDistance(string? source, string? target)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.IsNullOrEmpty(target) ? 0 : target!.Length;
        }

        if (string.IsNullOrEmpty(target))
        {
            return source.Length;
        }

        int sourceLength = source.Length;
        int targetLength = target.Length;
        int[,] distance = new int[sourceLength + 1, targetLength + 1];

        // Initialize the first column
        for (int i = 0; i <= sourceLength; i++)
        {
            distance[i, 0] = i;
        }

        // Initialize the first row
        for (int j = 0; j <= targetLength; j++)
        {
            distance[0, j] = j;
        }

        // Fill in the rest of the matrix
        for (int j = 1; j <= targetLength; j++)
        {
            for (int i = 1; i <= sourceLength; i++)
            {
                int cost = source[i - 1] == target[j - 1] ? 0 : 1;

                distance[i, j] = Math.Min(
                    Math.Min(
                        distance[i - 1, j] + 1,     // Deletion
                        distance[i, j - 1] + 1),    // Insertion
                    distance[i - 1, j - 1] + cost); // Substitution
            }
        }

        return distance[sourceLength, targetLength];
    }

    /// <summary>
    ///     Determines if two property names are similar based on Levenshtein distance.
    /// </summary>
    /// <param name="name1">The first property name.</param>
    /// <param name="name2">The second property name.</param>
    /// <param name="maxDistance">The maximum allowed edit distance (default: 2).</param>
    /// <param name="maxLengthDifference">The maximum allowed length difference (default: 2).</param>
    /// <returns>True if the names are considered similar.</returns>
    public static bool AreNamesSimilar(
        string? name1,
        string? name2,
        int maxDistance = 2,
        int maxLengthDifference = 2)
    {
        if (string.IsNullOrEmpty(name1) || string.IsNullOrEmpty(name2))
        {
            return false;
        }

        // Check length difference first (cheaper than computing distance)
        if (Math.Abs(name1.Length - name2.Length) > maxLengthDifference)
        {
            return false;
        }

        int distance = ComputeLevenshteinDistance(name1, name2);
        return distance <= maxDistance;
    }

    /// <summary>
    ///     Normalizes a property accessor expression by removing common prefixes.
    /// </summary>
    /// <param name="expression">The expression to normalize.</param>
    /// <returns>The normalized expression.</returns>
    public static string NormalizePropertyExpression(string? expression)
    {
        if (string.IsNullOrEmpty(expression))
        {
            return string.Empty;
        }

        // Remove common source parameter prefixes
        foreach (var prefix in AutoMapperConstants.SourceParameterPrefixes)
        {
            if (expression.StartsWith(prefix, StringComparison.Ordinal))
            {
                return expression.Substring(prefix.Length);
            }
        }

        return expression;
    }

    /// <summary>
    ///     Checks if a string represents an AutoMapper source parameter reference.
    /// </summary>
    /// <param name="expression">The expression to check.</param>
    /// <returns>True if the expression is a source parameter reference.</returns>
    public static bool IsSourceParameterReference(string? expression)
    {
        if (string.IsNullOrEmpty(expression))
        {
            return false;
        }

        return expression == AutoMapperConstants.SourceParameterName ||
               expression.StartsWith(AutoMapperConstants.SourceParameterPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Creates a source property access expression.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The full source property access expression (e.g., "src.PropertyName").</returns>
    public static string CreateSourcePropertyAccess(string propertyName)
    {
        return $"{AutoMapperConstants.SourceParameterPrefix}{propertyName}";
    }

    /// <summary>
    ///     Computes the similarity ratio between two strings (0.0 to 1.0).
    /// </summary>
    /// <param name="source">The source string.</param>
    /// <param name="target">The target string.</param>
    /// <returns>The similarity ratio, where 1.0 means identical.</returns>
    public static double ComputeSimilarityRatio(string? source, string? target)
    {
        if (string.IsNullOrEmpty(source) && string.IsNullOrEmpty(target))
        {
            return 1.0;
        }

        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
        {
            return 0.0;
        }

        int maxLength = Math.Max(source.Length, target.Length);
        if (maxLength == 0)
        {
            return 1.0;
        }

        int distance = ComputeLevenshteinDistance(source, target);
        return 1.0 - ((double)distance / maxLength);
    }

    /// <summary>
    ///     Finds the best matching property name from a collection of candidates.
    /// </summary>
    /// <param name="targetName">The target property name to match.</param>
    /// <param name="candidates">The collection of candidate names.</param>
    /// <param name="minSimilarity">The minimum similarity ratio required (default: 0.6).</param>
    /// <returns>The best matching name, or null if no suitable match found.</returns>
    public static string? FindBestMatch(
        string targetName,
        IEnumerable<string> candidates,
        double minSimilarity = 0.6)
    {
        if (string.IsNullOrEmpty(targetName) || candidates == null)
        {
            return null;
        }

        string? bestMatch = null;
        double bestSimilarity = minSimilarity;

        foreach (var candidate in candidates)
        {
            double similarity = ComputeSimilarityRatio(targetName, candidate);
            if (similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestMatch = candidate;
            }
        }

        return bestMatch;
    }

    /// <summary>
    ///     Checks if two strings are equal ignoring case.
    /// </summary>
    /// <param name="str1">The first string.</param>
    /// <param name="str2">The second string.</param>
    /// <returns>True if the strings are equal ignoring case.</returns>
    public static bool EqualsIgnoreCase(string? str1, string? str2)
    {
        return string.Equals(str1, str2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Checks if a string contains another string using ordinal comparison.
    /// </summary>
    /// <param name="source">The source string to search in.</param>
    /// <param name="value">The value to search for.</param>
    /// <returns>True if the source contains the value.</returns>
    public static bool ContainsOrdinal(string? source, string value)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        return source.IndexOf(value, StringComparison.Ordinal) >= 0;
    }

    /// <summary>
    ///     Checks if a string contains another string using ordinal case-insensitive comparison.
    /// </summary>
    /// <param name="source">The source string to search in.</param>
    /// <param name="value">The value to search for.</param>
    /// <returns>True if the source contains the value.</returns>
    public static bool ContainsIgnoreCase(string? source, string value)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

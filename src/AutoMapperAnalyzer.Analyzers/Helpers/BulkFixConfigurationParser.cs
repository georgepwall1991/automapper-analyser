using System.Text.RegularExpressions;

namespace AutoMapperAnalyzer.Analyzers.Helpers;

/// <summary>
///     Represents the action to take for a property in bulk fix configuration.
/// </summary>
public enum BulkFixAction
{
    /// <summary>
    ///     Map to default value (0, "", null, false, etc.)
    /// </summary>
    Default,

    /// <summary>
    ///     Ignore the property (won't be mapped)
    /// </summary>
    Ignore,

    /// <summary>
    ///     Map to a similar property using fuzzy matching
    /// </summary>
    FuzzyMatch,

    /// <summary>
    ///     Add TODO comment for manual implementation
    /// </summary>
    Todo,

    /// <summary>
    ///     Add custom mapping logic placeholder
    /// </summary>
    Custom,

    /// <summary>
    ///     Make destination property nullable (cross-file edit)
    /// </summary>
    Nullable
}

/// <summary>
///     Configuration for a single property fix.
/// </summary>
public class PropertyFixAction
{
    /// <summary>
    ///     Gets the property name.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    ///     Gets the property type.
    /// </summary>
    public string PropertyType { get; }

    /// <summary>
    ///     Gets the action to perform.
    /// </summary>
    public BulkFixAction Action { get; }

    /// <summary>
    ///     Gets the parameter for the action (e.g., source property name for fuzzy match).
    /// </summary>
    public string? Parameter { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PropertyFixAction"/> class.
    /// </summary>
    public PropertyFixAction(string propertyName, string propertyType, BulkFixAction action, string? parameter = null)
    {
        PropertyName = propertyName;
        PropertyType = propertyType;
        Action = action;
        Parameter = parameter;
    }
}

/// <summary>
///     Bulk fix configuration parsed from comment.
/// </summary>
public class BulkFixConfiguration
{
    /// <summary>
    ///     Gets the list of property fix actions.
    /// </summary>
    public List<PropertyFixAction> PropertyActions { get; }

    /// <summary>
    ///     Gets a value indicating whether chunking is enabled.
    /// </summary>
    public bool EnableChunking { get; }

    /// <summary>
    ///     Gets the chunk size (number of properties per method).
    /// </summary>
    public int ChunkSize { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="BulkFixConfiguration"/> class.
    /// </summary>
    public BulkFixConfiguration(List<PropertyFixAction> propertyActions, bool enableChunking, int chunkSize)
    {
        PropertyActions = propertyActions;
        EnableChunking = enableChunking;
        ChunkSize = chunkSize;
    }
}

/// <summary>
///     Parses bulk fix configuration from comment text.
/// </summary>
public class BulkFixConfigurationParser
{
    private const string ConfigMarker = "BULK-FIX-CONFIG:";
    private const string ChunkingMarker = "CHUNKING:";
    private const string ChunkSizeMarker = "Chunk size:";

    /// <summary>
    ///     Checks if a comment contains bulk fix configuration.
    /// </summary>
    /// <param name="commentText">The comment text to check.</param>
    /// <returns>True if the comment contains configuration marker.</returns>
    public static bool IsConfigurationComment(string commentText)
    {
        return commentText.Contains(ConfigMarker);
    }

    /// <summary>
    ///     Parses bulk fix configuration from comment text.
    /// </summary>
    /// <param name="commentText">The comment text containing the configuration table.</param>
    /// <returns>The parsed configuration, or null if parsing fails.</returns>
    public static BulkFixConfiguration? Parse(string commentText)
    {
        if (!IsConfigurationComment(commentText))
        {
            return null;
        }

        var propertyActions = new List<PropertyFixAction>();
        bool enableChunking = false;
        int chunkSize = 15; // default

        var lines = commentText.Split('\n');
        bool inTableData = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim().TrimStart('*', '/', ' ').Trim();

            // Check for chunking configuration
            if (trimmed.StartsWith(ChunkingMarker))
            {
                enableChunking = ParseChunkingEnabled(trimmed);
                continue;
            }

            if (trimmed.StartsWith(ChunkSizeMarker))
            {
                chunkSize = ParseChunkSize(trimmed);
                continue;
            }

            // Skip header lines
            if (trimmed.StartsWith("Property Name") || trimmed.StartsWith("---") ||
                trimmed.StartsWith("Instructions:") || trimmed.StartsWith("Valid actions:") ||
                string.IsNullOrWhiteSpace(trimmed))
            {
                if (trimmed.StartsWith("---"))
                {
                    inTableData = true; // Start reading data after separator
                }
                continue;
            }

            // Parse property configuration line
            if (inTableData && trimmed.Contains("|"))
            {
                var propertyAction = ParsePropertyLine(trimmed);
                if (propertyAction != null)
                {
                    propertyActions.Add(propertyAction);
                }
            }
        }

        if (propertyActions.Count == 0)
        {
            return null;
        }

        return new BulkFixConfiguration(propertyActions, enableChunking, chunkSize);
    }

    private static PropertyFixAction? ParsePropertyLine(string line)
    {
        // Format: "PropertyName | Type | Action | Parameter"
        var parts = line.Split('|');
        if (parts.Length < 3)
        {
            return null;
        }

        var propertyName = parts[0].Trim();
        var propertyType = parts[1].Trim();
        var actionText = parts[2].Trim();
        var parameter = parts.Length > 3 ? parts[3].Trim() : null;

        if (string.IsNullOrEmpty(propertyName) || string.IsNullOrEmpty(propertyType))
        {
            return null;
        }

        var action = ParseAction(actionText);
        if (action == null)
        {
            return null;
        }

        return new PropertyFixAction(propertyName, propertyType, action.Value, parameter);
    }

    private static BulkFixAction? ParseAction(string actionText)
    {
        return actionText.ToUpperInvariant() switch
        {
            "DEFAULT" => BulkFixAction.Default,
            "IGNORE" => BulkFixAction.Ignore,
            "FUZZY-MATCH" or "FUZZY" => BulkFixAction.FuzzyMatch,
            // Legacy compatibility: map retired placeholder actions to executable default behavior.
            "TODO" => BulkFixAction.Default,
            "CUSTOM" => BulkFixAction.Default,
            "NULLABLE" => BulkFixAction.Default,
            _ => null
        };
    }

    private static bool ParseChunkingEnabled(string line)
    {
        // Format: "CHUNKING: [YES/NO]: YES"
        var match = Regex.Match(line, @"\[YES/NO\]:\s*(YES|NO)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Equals("YES", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static int ParseChunkSize(string line)
    {
        // Format: "Chunk size: 15"
        var match = Regex.Match(line, @"Chunk size:\s*(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int size))
        {
            return Math.Max(5, Math.Min(size, 50)); // Clamp between 5 and 50
        }

        return 15; // default
    }

    /// <summary>
    ///     Generates a configuration comment template for bulk fix.
    /// </summary>
    /// <param name="properties">The properties to configure.</param>
    /// <returns>The configuration comment text.</returns>
    public static string GenerateConfigurationComment(IEnumerable<(string Name, string Type, BulkFixAction DefaultAction, string? Parameter)> properties)
    {
        var lines = new List<string>
        {
            "/* ===== BULK FIX CONFIGURATION =====",
            " * INSTRUCTIONS: Edit actions below, then use Ctrl+. to apply",
            " *",
            " * Property Name           | Type              | Action        | Parameter",
            " * -------------------------------------------------------------------------------"
        };

        foreach (var (name, type, defaultAction, parameter) in properties)
        {
            var actionStr = ActionToString(defaultAction);
            var paramStr = parameter ?? "";
            var line = $" * {name,-24} | {type,-17} | {actionStr,-13} | {paramStr}";
            lines.Add(line);
        }

        lines.Add(" *");
        lines.Add(" * === AVAILABLE ACTIONS ===");
        lines.Add(" * DEFAULT       - Map to default value (0, \"\", false, null, etc.)");
        lines.Add(" * FUZZY         - Map to similar source property (specify in Parameter)");
        lines.Add(" * IGNORE        - Add .Ignore() - property not needed");
        lines.Add(" *");
        lines.Add(" * === CHUNKING ===");
        lines.Add(" * CHUNKING: [YES/NO]: NO");
        lines.Add(" * Chunk size: 15");
        lines.Add(" *");
        lines.Add(" * Press Ctrl+. on this comment to apply configuration");
        lines.Add(" */");

        return string.Join("\n", lines);
    }

    private static string ActionToString(BulkFixAction action)
    {
        return action switch
        {
            BulkFixAction.Default => "DEFAULT",
            BulkFixAction.Ignore => "IGNORE",
            BulkFixAction.FuzzyMatch => "FUZZY-MATCH",
            BulkFixAction.Todo => "DEFAULT",
            BulkFixAction.Custom => "DEFAULT",
            BulkFixAction.Nullable => "DEFAULT",
            _ => "DEFAULT"
        };
    }
}

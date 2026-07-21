using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AnalyzerVerifier;

public sealed record CompatibilityCase(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("runner")] string Runner,
    [property: JsonPropertyName("targetFramework")] string TargetFramework,
    [property: JsonPropertyName("autoMapperVersion")] string AutoMapperVersion);

public sealed partial class CompatibilityContract
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = false
    };

    private static readonly HashSet<string> SupportedRunners =
        new(StringComparer.Ordinal) { "ubuntu-latest", "windows-latest" };

    private CompatibilityContract(IReadOnlyList<CompatibilityCase> cases)
    {
        Cases = cases;
    }

    public IReadOnlyList<CompatibilityCase> Cases { get; }

    public static CompatibilityContract Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(File.ReadAllText(path));
    }

    public static CompatibilityContract Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        CompatibilityManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<CompatibilityManifest>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Compatibility manifest is not valid JSON.", exception);
        }

        if (manifest?.Include is not { Count: > 0 } cases)
        {
            throw new InvalidDataException("Compatibility manifest field 'include' must contain at least one case.");
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (CompatibilityCase compatibilityCase in cases)
        {
            ValidateCase(compatibilityCase);
            if (!seenIds.Add(compatibilityCase.Id))
            {
                throw new InvalidDataException($"Duplicate compatibility case id '{compatibilityCase.Id}'.");
            }
        }

        return new CompatibilityContract(cases.AsReadOnly());
    }

    public CompatibilityCase GetCase(string id) =>
        Cases.SingleOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal))
        ?? throw new InvalidDataException($"Compatibility case '{id}' does not exist in the manifest.");

    public string ToGitHubMatrixJson() =>
        JsonSerializer.Serialize(new CompatibilityManifest(Cases.ToList()), JsonOptions);

    public string ToMarkdownTable()
    {
        var table = new StringBuilder();
        table.AppendLine("| Case | Runner | Target | AutoMapper |");
        table.AppendLine("| --- | --- | --- | --- |");
        foreach (CompatibilityCase compatibilityCase in Cases)
        {
            table.Append("| `")
                .Append(compatibilityCase.Id)
                .Append("` | `")
                .Append(compatibilityCase.Runner)
                .Append("` | `")
                .Append(compatibilityCase.TargetFramework)
                .Append("` | `")
                .Append(compatibilityCase.AutoMapperVersion)
                .AppendLine("` |");
        }

        return table.ToString().TrimEnd();
    }

    private static void ValidateCase(CompatibilityCase compatibilityCase)
    {
        if (string.IsNullOrWhiteSpace(compatibilityCase.Id) || !CaseIdPattern().IsMatch(compatibilityCase.Id))
        {
            throw new InvalidDataException($"Compatibility case field 'id' is invalid: '{compatibilityCase.Id}'.");
        }

        if (string.IsNullOrWhiteSpace(compatibilityCase.Runner) || !SupportedRunners.Contains(compatibilityCase.Runner))
        {
            throw new InvalidDataException(
                $"Compatibility case '{compatibilityCase.Id}' field 'runner' is invalid: '{compatibilityCase.Runner}'.");
        }

        if (string.IsNullOrWhiteSpace(compatibilityCase.TargetFramework) ||
            !TargetFrameworkPattern().IsMatch(compatibilityCase.TargetFramework))
        {
            throw new InvalidDataException(
                $"Compatibility case '{compatibilityCase.Id}' field 'targetFramework' is invalid: "
                + $"'{compatibilityCase.TargetFramework}'.");
        }

        if (string.IsNullOrWhiteSpace(compatibilityCase.AutoMapperVersion) ||
            !VersionPattern().IsMatch(compatibilityCase.AutoMapperVersion))
        {
            throw new InvalidDataException(
                $"Compatibility case '{compatibilityCase.Id}' field 'autoMapperVersion' is invalid: "
                + $"'{compatibilityCase.AutoMapperVersion}'.");
        }
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CaseIdPattern();

    [GeneratedRegex("^net(?:48|[6-9]\\.0|10\\.0)$", RegexOptions.CultureInvariant)]
    private static partial Regex TargetFrameworkPattern();

    [GeneratedRegex(
        "^[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    private sealed record CompatibilityManifest(
        [property: JsonPropertyName("include")] List<CompatibilityCase> Include);
}

public static class CompatibilityDocumentation
{
    public const string StartMarker = "<!-- compatibility-matrix:start -->";
    public const string EndMarker = "<!-- compatibility-matrix:end -->";

    public static string ExtractGeneratedTable(string documentation)
    {
        ArgumentNullException.ThrowIfNull(documentation);
        (int contentStart, int contentLength) = FindGeneratedRange(documentation);
        return documentation.Substring(contentStart, contentLength).Trim('\r', '\n');
    }

    public static string ReplaceGeneratedTable(string documentation, string table)
    {
        ArgumentNullException.ThrowIfNull(documentation);
        ArgumentNullException.ThrowIfNull(table);
        (int contentStart, int contentLength) = FindGeneratedRange(documentation);
        return documentation[..contentStart]
               + "\n"
               + table.Trim('\r', '\n')
               + "\n"
               + documentation[(contentStart + contentLength)..];
    }

    private static (int ContentStart, int ContentLength) FindGeneratedRange(string documentation)
    {
        int startMarker = documentation.IndexOf(StartMarker, StringComparison.Ordinal);
        int endMarker = documentation.IndexOf(EndMarker, StringComparison.Ordinal);
        if (startMarker < 0 || endMarker < 0 || endMarker <= startMarker)
        {
            throw new InvalidDataException(
                $"Compatibility documentation must contain '{StartMarker}' before '{EndMarker}'.");
        }

        int contentStart = startMarker + StartMarker.Length;
        return (contentStart, endMarker - contentStart);
    }
}

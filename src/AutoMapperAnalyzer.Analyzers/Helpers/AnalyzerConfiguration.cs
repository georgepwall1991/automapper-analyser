using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.Helpers;

/// <summary>
///     Provides analyzer configuration options that can be customized via .editorconfig.
///
///     Supported options:
///     - automapper_analyzer.fuzzy_match_distance = 2 (default)
///     - automapper_analyzer.fuzzy_match_length_difference = 2 (default)
///     - automapper_analyzer.min_similarity_ratio = 0.6 (default)
///     - automapper_analyzer.bulk_fix_chunk_size = 10 (default)
///     - automapper_analyzer.report_non_deterministic_as = info|warning|error (default: info)
/// </summary>
public static class AnalyzerConfiguration
{
    #region Option Keys

    /// <summary>
    ///     The prefix for all AutoMapper analyzer options.
    /// </summary>
    private const string OptionPrefix = "automapper_analyzer.";

    /// <summary>
    ///     Key for fuzzy match distance configuration.
    /// </summary>
    public const string FuzzyMatchDistanceKey = OptionPrefix + "fuzzy_match_distance";

    /// <summary>
    ///     Key for fuzzy match length difference configuration.
    /// </summary>
    public const string FuzzyMatchLengthDifferenceKey = OptionPrefix + "fuzzy_match_length_difference";

    /// <summary>
    ///     Key for minimum similarity ratio configuration.
    /// </summary>
    public const string MinSimilarityRatioKey = OptionPrefix + "min_similarity_ratio";

    /// <summary>
    ///     Key for bulk fix chunk size configuration.
    /// </summary>
    public const string BulkFixChunkSizeKey = OptionPrefix + "bulk_fix_chunk_size";

    /// <summary>
    ///     Key for non-deterministic operation severity configuration.
    /// </summary>
    public const string NonDeterministicSeverityKey = OptionPrefix + "report_non_deterministic_as";

    /// <summary>
    ///     Key for enabling/disabling performance warnings.
    /// </summary>
    public const string EnablePerformanceWarningsKey = OptionPrefix + "enable_performance_warnings";

    /// <summary>
    ///     Key for enabling/disabling nullable compatibility warnings.
    /// </summary>
    public const string EnableNullableWarningsKey = OptionPrefix + "enable_nullable_warnings";

    #endregion

    #region Default Values

    /// <summary>
    ///     Default fuzzy match distance.
    /// </summary>
    public const int DefaultFuzzyMatchDistance = 2;

    /// <summary>
    ///     Default fuzzy match length difference.
    /// </summary>
    public const int DefaultFuzzyMatchLengthDifference = 2;

    /// <summary>
    ///     Default minimum similarity ratio.
    /// </summary>
    public const double DefaultMinSimilarityRatio = 0.6;

    /// <summary>
    ///     Default bulk fix chunk size.
    /// </summary>
    public const int DefaultBulkFixChunkSize = 10;

    #endregion

    /// <summary>
    ///     Gets the fuzzy match distance from analyzer options.
    /// </summary>
    /// <param name="options">The analyzer options provider.</param>
    /// <param name="syntaxTree">The syntax tree for context.</param>
    /// <returns>The configured fuzzy match distance.</returns>
    public static int GetFuzzyMatchDistance(AnalyzerConfigOptionsProvider options, SyntaxTree syntaxTree)
    {
        return GetIntOption(options, syntaxTree, FuzzyMatchDistanceKey, DefaultFuzzyMatchDistance);
    }

    /// <summary>
    ///     Gets the fuzzy match length difference from analyzer options.
    /// </summary>
    /// <param name="options">The analyzer options provider.</param>
    /// <param name="syntaxTree">The syntax tree for context.</param>
    /// <returns>The configured fuzzy match length difference.</returns>
    public static int GetFuzzyMatchLengthDifference(AnalyzerConfigOptionsProvider options, SyntaxTree syntaxTree)
    {
        return GetIntOption(options, syntaxTree, FuzzyMatchLengthDifferenceKey, DefaultFuzzyMatchLengthDifference);
    }

    /// <summary>
    ///     Gets the minimum similarity ratio from analyzer options.
    /// </summary>
    /// <param name="options">The analyzer options provider.</param>
    /// <param name="syntaxTree">The syntax tree for context.</param>
    /// <returns>The configured minimum similarity ratio.</returns>
    public static double GetMinSimilarityRatio(AnalyzerConfigOptionsProvider options, SyntaxTree syntaxTree)
    {
        return GetDoubleOption(options, syntaxTree, MinSimilarityRatioKey, DefaultMinSimilarityRatio);
    }

    /// <summary>
    ///     Gets the bulk fix chunk size from analyzer options.
    /// </summary>
    /// <param name="options">The analyzer options provider.</param>
    /// <param name="syntaxTree">The syntax tree for context.</param>
    /// <returns>The configured bulk fix chunk size.</returns>
    public static int GetBulkFixChunkSize(AnalyzerConfigOptionsProvider options, SyntaxTree syntaxTree)
    {
        return GetIntOption(options, syntaxTree, BulkFixChunkSizeKey, DefaultBulkFixChunkSize);
    }

    /// <summary>
    ///     Gets whether performance warnings are enabled.
    /// </summary>
    /// <param name="options">The analyzer options provider.</param>
    /// <param name="syntaxTree">The syntax tree for context.</param>
    /// <returns>True if performance warnings are enabled.</returns>
    public static bool ArePerformanceWarningsEnabled(AnalyzerConfigOptionsProvider options, SyntaxTree syntaxTree)
    {
        return GetBoolOption(options, syntaxTree, EnablePerformanceWarningsKey, true);
    }

    /// <summary>
    ///     Gets whether nullable warnings are enabled.
    /// </summary>
    /// <param name="options">The analyzer options provider.</param>
    /// <param name="syntaxTree">The syntax tree for context.</param>
    /// <returns>True if nullable warnings are enabled.</returns>
    public static bool AreNullableWarningsEnabled(AnalyzerConfigOptionsProvider options, SyntaxTree syntaxTree)
    {
        return GetBoolOption(options, syntaxTree, EnableNullableWarningsKey, true);
    }

    /// <summary>
    ///     Gets the severity for non-deterministic operation warnings.
    /// </summary>
    /// <param name="options">The analyzer options provider.</param>
    /// <param name="syntaxTree">The syntax tree for context.</param>
    /// <returns>The configured severity.</returns>
    public static DiagnosticSeverity GetNonDeterministicSeverity(AnalyzerConfigOptionsProvider options, SyntaxTree syntaxTree)
    {
        var analyzerOptions = options.GetOptions(syntaxTree);
        if (analyzerOptions.TryGetValue(NonDeterministicSeverityKey, out var value))
        {
            return value?.ToLowerInvariant() switch
            {
                "error" => DiagnosticSeverity.Error,
                "warning" => DiagnosticSeverity.Warning,
                "info" => DiagnosticSeverity.Info,
                "hidden" => DiagnosticSeverity.Hidden,
                _ => DiagnosticSeverity.Info
            };
        }

        return DiagnosticSeverity.Info;
    }

    #region Private Helpers

    private static int GetIntOption(
        AnalyzerConfigOptionsProvider options,
        SyntaxTree syntaxTree,
        string key,
        int defaultValue)
    {
        var analyzerOptions = options.GetOptions(syntaxTree);
        if (analyzerOptions.TryGetValue(key, out var value) && int.TryParse(value, out var result))
        {
            return result;
        }

        return defaultValue;
    }

    private static double GetDoubleOption(
        AnalyzerConfigOptionsProvider options,
        SyntaxTree syntaxTree,
        string key,
        double defaultValue)
    {
        var analyzerOptions = options.GetOptions(syntaxTree);
        if (analyzerOptions.TryGetValue(key, out var value) && double.TryParse(value, out var result))
        {
            return result;
        }

        return defaultValue;
    }

    private static bool GetBoolOption(
        AnalyzerConfigOptionsProvider options,
        SyntaxTree syntaxTree,
        string key,
        bool defaultValue)
    {
        var analyzerOptions = options.GetOptions(syntaxTree);
        if (analyzerOptions.TryGetValue(key, out var value))
        {
            return value?.ToLowerInvariant() switch
            {
                "true" or "yes" or "1" => true,
                "false" or "no" or "0" => false,
                _ => defaultValue
            };
        }

        return defaultValue;
    }

    #endregion
}

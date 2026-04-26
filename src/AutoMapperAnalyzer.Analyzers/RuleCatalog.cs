using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.ComplexMappings;
using AutoMapperAnalyzer.Analyzers.Configuration;
using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Analyzers.Performance;
using AutoMapperAnalyzer.Analyzers.TypeSafety;
using Microsoft.CodeAnalysis;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
///     Classifies how much review a generated code fix normally needs before a developer accepts it.
/// </summary>
public enum CodeFixTrustLevel
{
    /// <summary>
    ///     The fix is behavior-preserving or removes configuration AutoMapper already handles by convention.
    /// </summary>
    SafeRewrite,

    /// <summary>
    ///     The fix is semantically reasonable, but a developer should verify the chosen mapping policy.
    /// </summary>
    LikelyRewrite,

    /// <summary>
    ///     The fix is compile-safe starter code or an explicit suppression that requires manual review.
    /// </summary>
    Scaffold
}

/// <summary>
///     Rule metadata used by tests, documentation validation, and release hygiene checks.
/// </summary>
public sealed class RuleCatalogEntry
{
    /// <summary>
    ///     Creates a rule catalog entry.
    /// </summary>
    public RuleCatalogEntry(
        string ruleId,
        string documentationAnchor,
        string samplePath,
        Type analyzerType,
        Type codeFixProviderType,
        CodeFixTrustLevel fixTrustLevel,
        ImmutableArray<DiagnosticDescriptor> descriptors)
    {
        RuleId = ruleId;
        DocumentationAnchor = documentationAnchor;
        SamplePath = samplePath;
        AnalyzerType = analyzerType;
        CodeFixProviderType = codeFixProviderType;
        FixTrustLevel = fixTrustLevel;
        Descriptors = descriptors;
    }

    /// <summary>
    ///     The public diagnostic rule ID.
    /// </summary>
    public string RuleId { get; }

    /// <summary>
    ///     Anchor in docs/DIAGNOSTIC_RULES.md for this rule.
    /// </summary>
    public string DocumentationAnchor { get; }

    /// <summary>
    ///     Relative path to a checked-in sample that exercises or documents the rule.
    /// </summary>
    public string SamplePath { get; }

    /// <summary>
    ///     Analyzer type that owns this rule.
    /// </summary>
    public Type AnalyzerType { get; }

    /// <summary>
    ///     Code fix provider type for this rule.
    /// </summary>
    public Type CodeFixProviderType { get; }

    /// <summary>
    ///     Trust classification for the rule's generated fixes.
    /// </summary>
    public CodeFixTrustLevel FixTrustLevel { get; }

    /// <summary>
    ///     Descriptors exposed under this public rule ID.
    /// </summary>
    public ImmutableArray<DiagnosticDescriptor> Descriptors { get; }
}

/// <summary>
///     Central catalog for implemented AutoMapper analyzer rules.
/// </summary>
public static class RuleCatalog
{
    /// <summary>
    ///     Current package version used by docs/package drift tests.
    /// </summary>
    public const string CurrentPackageVersion = "2.30.8";

    /// <summary>
    ///     Implemented rules, grouped by public diagnostic ID.
    /// </summary>
    public static ImmutableArray<RuleCatalogEntry> Rules { get; } =
    [
        new(
            "AM001",
            "### AM001: Property Type Mismatch",
            "samples/AutoMapperAnalyzer.Samples/TypeSafety/TypeSafetyExamples.cs",
            typeof(AM001_PropertyTypeMismatchAnalyzer),
            typeof(AM001_PropertyTypeMismatchCodeFixProvider),
            CodeFixTrustLevel.LikelyRewrite,
            [AM001_PropertyTypeMismatchAnalyzer.PropertyTypeMismatchRule]),
        new(
            "AM002",
            "### AM002: Nullable Compatibility Issue",
            "samples/AutoMapperAnalyzer.Samples/TypeSafety/ManyNullableMismatches.cs",
            typeof(AM002_NullableCompatibilityAnalyzer),
            typeof(AM002_NullableCompatibilityCodeFixProvider),
            CodeFixTrustLevel.Scaffold,
            [
                AM002_NullableCompatibilityAnalyzer.NullableToNonNullableRule,
                AM002_NullableCompatibilityAnalyzer.NonNullableToNullableRule
            ]),
        new(
            "AM003",
            "### AM003: Collection Type Incompatibility",
            "samples/AutoMapperAnalyzer.Samples/TypeSafety/TypeSafetyExamples.cs",
            typeof(AM003_CollectionTypeIncompatibilityAnalyzer),
            typeof(AM003_CollectionTypeIncompatibilityCodeFixProvider),
            CodeFixTrustLevel.LikelyRewrite,
            [AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule]),
        new(
            "AM004",
            "### AM004: Missing Destination Property",
            "samples/AutoMapperAnalyzer.Samples/MissingProperties/MissingPropertyExamples.cs",
            typeof(AM004_MissingDestinationPropertyAnalyzer),
            typeof(AM004_MissingDestinationPropertyCodeFixProvider),
            CodeFixTrustLevel.LikelyRewrite,
            [AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule]),
        new(
            "AM005",
            "### AM005: Case Sensitivity Mismatch",
            "samples/AutoMapperAnalyzer.Samples/MissingProperties/MissingPropertyExamples.cs",
            typeof(AM005_CaseSensitivityMismatchAnalyzer),
            typeof(AM005_CaseSensitivityMismatchCodeFixProvider),
            CodeFixTrustLevel.LikelyRewrite,
            [AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule]),
        new(
            "AM006",
            "### AM006: Unmapped Destination Property",
            "samples/AutoMapperAnalyzer.Samples/UnmappedDestination/UnmappedDestinationExamples.cs",
            typeof(AM006_UnmappedDestinationPropertyAnalyzer),
            typeof(AM006_UnmappedDestinationPropertyCodeFixProvider),
            CodeFixTrustLevel.Scaffold,
            [AM006_UnmappedDestinationPropertyAnalyzer.UnmappedDestinationPropertyRule]),
        new(
            "AM011",
            "### AM011: Unmapped Required Property",
            "samples/AutoMapperAnalyzer.Samples/DataIntegrity/ManyUnmappedRequiredProps.cs",
            typeof(AM011_UnmappedRequiredPropertyAnalyzer),
            typeof(AM011_UnmappedRequiredPropertyCodeFixProvider),
            CodeFixTrustLevel.Scaffold,
            [AM011_UnmappedRequiredPropertyAnalyzer.UnmappedRequiredPropertyRule]),
        new(
            "AM020",
            "### AM020: Nested Object Mapping Issue",
            "samples/AutoMapperAnalyzer.Samples/CodeFixDemo.cs",
            typeof(AM020_NestedObjectMappingAnalyzer),
            typeof(AM020_NestedObjectMappingCodeFixProvider),
            CodeFixTrustLevel.SafeRewrite,
            [AM020_NestedObjectMappingAnalyzer.NestedObjectMappingMissingRule]),
        new(
            "AM021",
            "### AM021: Collection Element Mismatch",
            "samples/AutoMapperAnalyzer.Samples/ComplexTypes/ComplexTypeMappingExamples.cs",
            typeof(AM021_CollectionElementMismatchAnalyzer),
            typeof(AM021_CollectionElementMismatchCodeFixProvider),
            CodeFixTrustLevel.LikelyRewrite,
            [AM021_CollectionElementMismatchAnalyzer.CollectionElementIncompatibilityRule]),
        new(
            "AM022",
            "### AM022: Infinite Recursion Risk",
            "samples/AutoMapperAnalyzer.Samples/ComplexTypes/ComplexTypeMappingExamples.cs",
            typeof(AM022_InfiniteRecursionAnalyzer),
            typeof(AM022_InfiniteRecursionCodeFixProvider),
            CodeFixTrustLevel.LikelyRewrite,
            [
                AM022_InfiniteRecursionAnalyzer.InfiniteRecursionRiskRule,
                AM022_InfiniteRecursionAnalyzer.SelfReferencingTypeRule
            ]),
        new(
            "AM030",
            "### AM030: Custom Type Converter Issues",
            "samples/AutoMapperAnalyzer.Samples/Conversions/TypeConverterExamples.cs",
            typeof(AM030_CustomTypeConverterAnalyzer),
            typeof(AM030_CustomTypeConverterCodeFixProvider),
            CodeFixTrustLevel.LikelyRewrite,
            [
                AM030_CustomTypeConverterAnalyzer.InvalidConverterImplementationRule,
                AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule,
                AM030_CustomTypeConverterAnalyzer.UnusedTypeConverterRule
            ]),
        new(
            "AM031",
            "### AM031: Performance Warning",
            "samples/AutoMapperAnalyzer.Samples/Performance/AM031_PerformanceExamples.cs",
            typeof(AM031_PerformanceWarningAnalyzer),
            typeof(AM031_PerformanceWarningCodeFixProvider),
            CodeFixTrustLevel.Scaffold,
            [
                AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule,
                AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule,
                AM031_PerformanceWarningAnalyzer.ExpensiveComputationRule,
                AM031_PerformanceWarningAnalyzer.TaskResultSynchronousAccessRule,
                AM031_PerformanceWarningAnalyzer.ComplexLinqOperationRule,
                AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule
            ]),
        new(
            "AM041",
            "### AM041: Duplicate Mapping Registration",
            "samples/AutoMapperAnalyzer.Samples/Configuration/AM041_DuplicateMappingExamples.cs",
            typeof(AM041_DuplicateMappingAnalyzer),
            typeof(AM041_DuplicateMappingCodeFixProvider),
            CodeFixTrustLevel.SafeRewrite,
            [AM041_DuplicateMappingAnalyzer.DuplicateMappingRule]),
        new(
            "AM050",
            "### AM050: Redundant MapFrom Configuration",
            "samples/AutoMapperAnalyzer.Samples/Configuration/AM050_RedundantMapFromExamples.cs",
            typeof(AM050_RedundantMapFromAnalyzer),
            typeof(AM050_RedundantMapFromCodeFixProvider),
            CodeFixTrustLevel.SafeRewrite,
            [AM050_RedundantMapFromAnalyzer.RedundantMapFromRule])
    ];
}

namespace AutoMapperAnalyzer.Analyzers.Helpers;

/// <summary>
///     Contains constant values used throughout the AutoMapper analyzers.
/// </summary>
public static class AutoMapperConstants
{
    #region Method Names

    /// <summary>
    ///     The CreateMap method name.
    /// </summary>
    public const string CreateMapMethodName = "CreateMap";

    /// <summary>
    ///     The ForMember method name.
    /// </summary>
    public const string ForMemberMethodName = "ForMember";

    /// <summary>
    ///     The MapFrom method name.
    /// </summary>
    public const string MapFromMethodName = "MapFrom";

    /// <summary>
    ///     The Ignore method name.
    /// </summary>
    public const string IgnoreMethodName = "Ignore";

    /// <summary>
    ///     The ReverseMap method name.
    /// </summary>
    public const string ReverseMapMethodName = "ReverseMap";

    /// <summary>
    ///     The ConvertUsing method name.
    /// </summary>
    public const string ConvertUsingMethodName = "ConvertUsing";

    /// <summary>
    ///     The ForAllMembers method name.
    /// </summary>
    public const string ForAllMembersMethodName = "ForAllMembers";

    /// <summary>
    ///     The IncludeBase method name.
    /// </summary>
    public const string IncludeBaseMethodName = "IncludeBase";

    /// <summary>
    ///     The Include method name.
    /// </summary>
    public const string IncludeMethodName = "Include";

    #endregion

    #region Type Names

    /// <summary>
    ///     The AutoMapper Profile base class name.
    /// </summary>
    public const string ProfileClassName = "Profile";

    /// <summary>
    ///     The fully qualified AutoMapper Profile class name.
    /// </summary>
    public const string FullyQualifiedProfileClassName = "AutoMapper.Profile";

    /// <summary>
    ///     The AutoMapper namespace.
    /// </summary>
    public const string AutoMapperNamespace = "AutoMapper";

    /// <summary>
    ///     Common naming patterns for mapping profile classes.
    /// </summary>
    public const string MappingProfileSuffix = "MappingProfile";

    /// <summary>
    ///     Common naming patterns for mapper configuration classes.
    /// </summary>
    public const string MapperConfigurationSuffix = "MapperConfiguration";

    /// <summary>
    ///     The ITypeConverter interface name.
    /// </summary>
    public const string TypeConverterInterfaceName = "ITypeConverter";

    /// <summary>
    ///     The IValueResolver interface name.
    /// </summary>
    public const string ValueResolverInterfaceName = "IValueResolver";

    #endregion

    #region Lambda Parameters

    /// <summary>
    ///     The common source parameter name in lambda expressions.
    /// </summary>
    public const string SourceParameterName = "src";

    /// <summary>
    ///     The source parameter prefix for property access.
    /// </summary>
    public const string SourceParameterPrefix = "src.";

    /// <summary>
    ///     Common source parameter prefixes in AutoMapper configurations.
    /// </summary>
    public static readonly string[] SourceParameterPrefixes =
    [
        "src.",
        "s.",
        "source.",
        "x."
    ];

    /// <summary>
    ///     The common destination parameter name in lambda expressions.
    /// </summary>
    public const string DestinationParameterName = "dest";

    /// <summary>
    ///     The destination parameter prefix for property access.
    /// </summary>
    public const string DestinationParameterPrefix = "dest.";

    /// <summary>
    ///     Common destination parameter prefixes in AutoMapper configurations.
    /// </summary>
    public static readonly string[] DestinationParameterPrefixes =
    [
        "dest.",
        "d.",
        "destination.",
        "y."
    ];

    /// <summary>
    ///     The common options parameter name in ForMember callbacks.
    /// </summary>
    public const string OptionsParameterName = "opt";

    #endregion

    #region Variable Naming Patterns

    /// <summary>
    ///     Common variable names for mapper instances.
    /// </summary>
    public static readonly string[] MapperVariableNames =
    [
        "mapper",
        "Mapper",
        "_mapper"
    ];

    /// <summary>
    ///     Common variable names for configuration instances.
    /// </summary>
    public static readonly string[] ConfigurationVariableNames =
    [
        "cfg",
        "config",
        "configuration"
    ];

    #endregion

    #region Diagnostic Categories

    /// <summary>
    ///     The category for type safety diagnostics.
    /// </summary>
    public const string CategoryTypeSafety = "AutoMapper.TypeSafety";

    /// <summary>
    ///     The category for data integrity diagnostics.
    /// </summary>
    public const string CategoryDataIntegrity = "AutoMapper.DataIntegrity";

    /// <summary>
    ///     The category for complex mapping diagnostics.
    /// </summary>
    public const string CategoryComplexMappings = "AutoMapper.ComplexMappings";

    /// <summary>
    ///     The category for performance diagnostics.
    /// </summary>
    public const string CategoryPerformance = "AutoMapper.Performance";

    /// <summary>
    ///     The category for configuration diagnostics.
    /// </summary>
    public const string CategoryConfiguration = "AutoMapper.Configuration";

    #endregion

    #region Diagnostic Property Keys

    /// <summary>
    ///     The property key for property name.
    /// </summary>
    public const string PropertyKeyPropertyName = "PropertyName";

    /// <summary>
    ///     The property key for property type.
    /// </summary>
    public const string PropertyKeyPropertyType = "PropertyType";

    /// <summary>
    ///     The property key for source type.
    /// </summary>
    public const string PropertyKeySourceType = "SourceType";

    /// <summary>
    ///     The property key for destination type.
    /// </summary>
    public const string PropertyKeyDestinationType = "DestinationType";

    /// <summary>
    ///     The property key for source property name.
    /// </summary>
    public const string PropertyKeySourcePropertyName = "SourcePropertyName";

    /// <summary>
    ///     The property key for destination property name.
    /// </summary>
    public const string PropertyKeyDestinationPropertyName = "DestinationPropertyName";

    /// <summary>
    ///     The property key for operation type (used in performance warnings).
    /// </summary>
    public const string PropertyKeyOperationType = "OperationType";

    #endregion

    #region Performance Detection Patterns

    /// <summary>
    ///     Type names that indicate database operations.
    /// </summary>
    public static readonly string[] DatabaseTypePatterns =
    [
        "DbSet",
        "DbContext",
        "IQueryable",
        "ISession",
        "SqlConnection",
        "NpgsqlConnection",
        "MySqlConnection"
    ];

    /// <summary>
    ///     Method names that indicate database operations.
    /// </summary>
    public static readonly string[] DatabaseMethodPatterns =
    [
        "Query",
        "Execute",
        "Load",
        "Find",
        "SaveChanges"
    ];

    /// <summary>
    ///     Type names that indicate file I/O operations.
    /// </summary>
    public static readonly string[] FileIOTypePatterns =
    [
        "System.IO.File",
        "System.IO.Directory",
        "System.IO.Path",
        "StreamReader",
        "StreamWriter"
    ];

    /// <summary>
    ///     Type names that indicate HTTP operations.
    /// </summary>
    public static readonly string[] HttpTypePatterns =
    [
        "HttpClient",
        "WebClient",
        "HttpWebRequest"
    ];

    /// <summary>
    ///     Method names that indicate HTTP operations.
    /// </summary>
    public static readonly string[] HttpMethodPatterns =
    [
        "GetAsync",
        "PostAsync",
        "PutAsync",
        "DeleteAsync",
        "GetString",
        "SendAsync"
    ];

    /// <summary>
    ///     Method names that indicate reflection operations.
    /// </summary>
    public static readonly string[] ReflectionMethodPatterns =
    [
        "GetType",
        "GetMethod",
        "GetProperty",
        "GetField",
        "GetCustomAttributes",
        "Invoke"
    ];

    /// <summary>
    ///     Simple LINQ methods that are generally acceptable in mappings.
    /// </summary>
    public static readonly string[] SimpleLinqMethods =
    [
        "Select",
        "Where",
        "OrderBy",
        "OrderByDescending",
        "ThenBy",
        "ThenByDescending",
        "Take",
        "Skip"
    ];

    /// <summary>
    ///     LINQ methods that enumerate collections.
    /// </summary>
    public static readonly string[] EnumerationLinqMethods =
    [
        "ToList",
        "ToArray",
        "Sum",
        "Average",
        "Count",
        "First",
        "FirstOrDefault",
        "Last",
        "LastOrDefault",
        "Any",
        "All",
        "Single",
        "SingleOrDefault"
    ];

    #endregion

    #region Fuzzy Matching Thresholds

    /// <summary>
    ///     The default maximum Levenshtein distance for fuzzy matching.
    /// </summary>
    public const int DefaultFuzzyMatchDistance = 2;

    /// <summary>
    ///     The default maximum length difference for fuzzy matching.
    /// </summary>
    public const int DefaultFuzzyMatchLengthDifference = 2;

    /// <summary>
    ///     The default minimum similarity ratio for property matching.
    /// </summary>
    public const double DefaultMinSimilarityRatio = 0.6;

    #endregion

    #region Bulk Fix Configuration

    /// <summary>
    ///     The default chunk size for bulk fix operations.
    /// </summary>
    public const int DefaultBulkFixChunkSize = 10;

    /// <summary>
    ///     The threshold for when chunking should be enabled.
    /// </summary>
    public const int BulkFixChunkingThreshold = 30;

    #endregion
}

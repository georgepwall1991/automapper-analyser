namespace AutoMapperAnalyzer.Analyzers.Helpers;

/// <summary>
/// Helper class for type conversion and default value generation in CodeFix providers.
/// Centralizes logic for determining default and sample values for common types.
/// </summary>
public static class TypeConversionHelper
{
    /// <summary>
    /// Gets a default value expression for the specified type.
    /// Returns expressions like "string.Empty", "0", "false", etc.
    /// </summary>
    /// <param name="propertyType">The type name (e.g., "string", "int", "DateTime").</param>
    /// <returns>A C# expression representing the default value for the type.</returns>
    public static string GetDefaultValueForType(string propertyType)
    {
        return propertyType.ToLower() switch
        {
            "string" => "string.Empty",
            "int" => "0",
            "long" => "0L",
            "double" => "0.0",
            "float" => "0.0f",
            "decimal" => "0m",
            "bool" => "false",
            "datetime" => "DateTime.MinValue",
            "guid" => "Guid.Empty",
            _ => "default"
        };
    }

    /// <summary>
    /// Gets a sample/example value expression for the specified type.
    /// Returns expressions like "1", "true", "DateTime.Now", etc.
    /// </summary>
    /// <param name="propertyType">The type name (e.g., "string", "int", "DateTime").</param>
    /// <returns>A C# expression representing a sample value for the type.</returns>
    public static string GetSampleValueForType(string propertyType)
    {
        return propertyType.ToLower() switch
        {
            "string" => "\"DefaultValue\"",
            "int" => "1",
            "long" => "1L",
            "double" => "1.0",
            "float" => "1.0f",
            "decimal" => "1.0m",
            "bool" => "true",
            "datetime" => "DateTime.Now",
            "guid" => "Guid.NewGuid()",
            _ => $"new {propertyType}()"
        };
    }

    /// <summary>
    /// Checks if the specified type is a string type.
    /// </summary>
    /// <param name="propertyType">The type name to check.</param>
    /// <returns>True if the type is string; otherwise, false.</returns>
    public static bool IsStringType(string propertyType)
    {
        return propertyType.Equals("string", StringComparison.OrdinalIgnoreCase) ||
               propertyType.Equals("System.String", StringComparison.OrdinalIgnoreCase);
    }
}

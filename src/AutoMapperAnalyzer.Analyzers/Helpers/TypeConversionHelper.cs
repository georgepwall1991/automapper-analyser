namespace AutoMapperAnalyzer.Analyzers.Helpers;

/// <summary>
///     Helper class for type conversion and default value generation in CodeFix providers.
///     Centralizes logic for determining default and sample values for common types.
/// </summary>
public static class TypeConversionHelper
{
    /// <summary>
    ///     Gets a default value expression for the specified type.
    ///     Returns expressions like "string.Empty", "0", "false", etc.
    /// </summary>
    /// <param name="propertyType">The type name (e.g., "string", "int", "DateTime").</param>
    /// <returns>A C# expression representing the default value for the type.</returns>
    public static string GetDefaultValueForType(string propertyType)
    {
        string normalized = NormalizeTypeName(propertyType);

        return normalized switch
        {
            "string" => "string.Empty",
            "char" => "'\0'",
            "int" => "0",
            "int16" => "0",
            "int32" => "0",
            "int64" => "0L",
            "uint16" => "0",
            "uint32" => "0u",
            "uint64" => "0UL",
            "long" => "0L",
            "short" => "0",
            "ushort" => "0",
            "byte" => "0",
            "sbyte" => "0",
            "double" => "0.0",
            "float" => "0.0f",
            "single" => "0.0f",
            "decimal" => "0m",
            "bool" => "false",
            "datetime" => "DateTime.MinValue",
            "datetimeoffset" => "DateTimeOffset.MinValue",
            "guid" => "Guid.Empty",
            _ => "default"
        };
    }

    /// <summary>
    ///     Gets a sample/example value expression for the specified type.
    ///     Returns expressions like "1", "true", "DateTime.Now", etc.
    /// </summary>
    /// <param name="propertyType">The type name (e.g., "string", "int", "DateTime").</param>
    /// <returns>A C# expression representing a sample value for the type.</returns>
    public static string GetSampleValueForType(string propertyType)
    {
        string normalized = NormalizeTypeName(propertyType);

        return normalized switch
        {
            "string" => "\"DefaultValue\"",
            "char" => "'a'",
            "int" or "int16" or "int32" => "1",
            "int64" or "long" => "1L",
            "uint16" or "uint32" => "1u",
            "uint64" => "1UL",
            "double" => "1.0",
            "float" or "single" => "1.0f",
            "decimal" => "1.0m",
            "bool" => "true",
            "datetime" => "DateTime.Now",
            "datetimeoffset" => "DateTimeOffset.UtcNow",
            "guid" => "Guid.NewGuid()",
            _ => $"new {propertyType.Trim()}()"
        };
    }

    /// <summary>
    ///     Checks if the specified type is a string type.
    /// </summary>
    /// <param name="propertyType">The type name to check.</param>
    /// <returns>True if the type is string; otherwise, false.</returns>
    public static bool IsStringType(string propertyType)
    {
        string normalized = NormalizeTypeName(propertyType);
        return normalized.Equals("string", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Normalizes a type name to a simple invariant for comparison.
    /// </summary>
    public static string NormalizeTypeName(string propertyType)
    {
        string value = propertyType?.Trim() ?? string.Empty;

        if (value.EndsWith("?", StringComparison.Ordinal))
        {
            value = value.Substring(0, value.Length - 1);
        }

        // Strip namespace prefixes we know about
        if (value.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring("System.".Length);
        }

        if (value.StartsWith("Collections.Generic.", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring("Collections.Generic.".Length);
        }

        // Handle fully qualified System.Collections.Generic.* that may remain
        if (value.StartsWith("Collections.", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring("Collections.".Length);
        }

        return value.ToLowerInvariant();
    }
}

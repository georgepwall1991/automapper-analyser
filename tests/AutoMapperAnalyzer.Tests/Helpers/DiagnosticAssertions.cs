using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.Helpers;

/// <summary>
/// Fluent assertion helpers for diagnostic results
/// </summary>
public static class DiagnosticAssertions
{
    /// <summary>
    /// Creates a diagnostic result builder for fluent assertions
    /// </summary>
    public static DiagnosticResultBuilder Diagnostic(DiagnosticDescriptor descriptor)
    {
        return new DiagnosticResultBuilder(descriptor);
    }

    /// <summary>
    /// Creates a diagnostic result builder for AutoMapper analyzer rules
    /// </summary>
    public static DiagnosticResultBuilder AutoMapperDiagnostic(string ruleId, DiagnosticSeverity severity, string messageFormat)
    {
        var descriptor = new DiagnosticDescriptor(
            ruleId,
            "Test Rule",
            messageFormat,
            "AutoMapper",
            severity,
            isEnabledByDefault: true);
            
        return new DiagnosticResultBuilder(descriptor);
    }

    /// <summary>
    /// Builder for creating diagnostic results with fluent API
    /// </summary>
    public class DiagnosticResultBuilder
    {
        private readonly DiagnosticDescriptor _descriptor;
        private int? _line;
        private int? _column;
        private int? _endLine;
        private int? _endColumn;
        private object[]? _messageArgs;

        internal DiagnosticResultBuilder(DiagnosticDescriptor descriptor)
        {
            _descriptor = descriptor;
        }

        /// <summary>
        /// Sets the location of the diagnostic
        /// </summary>
        public DiagnosticResultBuilder AtLocation(int line, int column)
        {
            _line = line;
            _column = column;
            return this;
        }

        /// <summary>
        /// Sets the span of the diagnostic
        /// </summary>
        public DiagnosticResultBuilder AtSpan(int startLine, int startColumn, int endLine, int endColumn)
        {
            _line = startLine;
            _column = startColumn;
            _endLine = endLine;
            _endColumn = endColumn;
            return this;
        }

        /// <summary>
        /// Sets the message arguments for the diagnostic
        /// </summary>
        public DiagnosticResultBuilder WithArguments(params object[] args)
        {
            _messageArgs = args;
            return this;
        }

        /// <summary>
        /// Builds the diagnostic result
        /// </summary>
        public DiagnosticResult Build()
        {
            var result = new DiagnosticResult(_descriptor);

            if (_line.HasValue && _column.HasValue)
            {
                if (_endLine.HasValue && _endColumn.HasValue)
                {
                    result = result.WithSpan(_line.Value, _column.Value, _endLine.Value, _endColumn.Value);
                }
                else
                {
                    result = result.WithLocation(_line.Value, _column.Value);
                }
            }

            if (_messageArgs != null)
            {
                result = result.WithArguments(_messageArgs);
            }

            return result;
        }

        /// <summary>
        /// Implicit conversion to DiagnosticResult for convenience
        /// </summary>
        public static implicit operator DiagnosticResult(DiagnosticResultBuilder builder)
        {
            return builder.Build();
        }
    }
}

/// <summary>
/// Common diagnostic descriptors for AutoMapper analyzer
/// </summary>
public static class AutoMapperDiagnostics
{
    /// <summary>
    /// AM001: Property Type Mismatch
    /// </summary>
    public static readonly DiagnosticDescriptor PropertyTypeMismatch = new(
        "AM001",
        "Property type mismatch between source and destination",
        "Property '{0}' type mismatch: source is '{1}' but destination is '{2}'",
        "AutoMapper.TypeSafety",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Source and destination properties have incompatible types without explicit conversion.");

    /// <summary>
    /// AM002: Nullable to Non-Nullable Assignment
    /// </summary>
    public static readonly DiagnosticDescriptor NullableToNonNullable = new(
        "AM002",
        "Nullable to non-nullable assignment without null handling",
        "Property '{0}' maps nullable source '{1}' to non-nullable destination '{2}' without null handling",
        "AutoMapper.TypeSafety",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Mapping nullable source to non-nullable destination without null handling could cause NullReferenceException.");

    /// <summary>
    /// AM003: Collection Type Incompatibility
    /// </summary>
    public static readonly DiagnosticDescriptor CollectionTypeIncompatibility = new(
        "AM003",
        "Collection types are incompatible",
        "Collection property '{0}' has incompatible types: source '{1}' and destination '{2}'",
        "AutoMapper.TypeSafety",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Collection types are incompatible and require explicit conversion.");

    /// <summary>
    /// AM010: Missing Destination Property
    /// </summary>
    public static readonly DiagnosticDescriptor MissingDestinationProperty = new(
        "AM010",
        "Source property has no corresponding destination property",
        "Source property '{0}' will not be mapped - potential data loss",
        "AutoMapper.MissingProperty",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Source property exists but no corresponding destination property - potential data loss.");

    /// <summary>
    /// AM011: Unmapped Required Property
    /// </summary>
    public static readonly DiagnosticDescriptor UnmappedRequiredProperty = new(
        "AM011",
        "Required destination property is not mapped",
        "Required destination property '{0}' is not mapped from any source property",
        "AutoMapper.MissingProperty",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Required destination property has no mapping configuration and will cause runtime exception.");
} 
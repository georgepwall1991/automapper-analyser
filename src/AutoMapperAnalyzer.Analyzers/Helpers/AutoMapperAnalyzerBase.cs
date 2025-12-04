using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.Helpers;

/// <summary>
///     Base class for AutoMapper analyzers that provides common functionality
///     and reduces boilerplate code across analyzer implementations.
/// </summary>
public abstract class AutoMapperAnalyzerBase : DiagnosticAnalyzer
{
    /// <summary>
    ///     Gets the supported diagnostics for this analyzer.
    ///     Derived classes must implement this to specify their diagnostic rules.
    /// </summary>
    public abstract override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }

    /// <summary>
    ///     Initializes the analyzer with standard configuration.
    ///     Derived classes can override to add additional registrations.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    public override void Initialize(AnalysisContext context)
    {
        // Standard analyzer configuration
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Register for CreateMap invocation analysis by default
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);
    }

    /// <summary>
    ///     Analyzes a syntax node. This method filters for CreateMap invocations
    ///     and delegates to the derived class implementation.
    /// </summary>
    /// <param name="context">The syntax node analysis context.</param>
    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;

        // Check if this is a CreateMap invocation
        if (!AutoMapperAnalysisHelpers.IsCreateMapInvocation(invocationExpr, context.SemanticModel))
        {
            return;
        }

        // Get type arguments
        var (sourceType, destinationType) = AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(
            invocationExpr, context.SemanticModel);

        if (sourceType == null || destinationType == null)
        {
            return;
        }

        // Delegate to derived class
        AnalyzeCreateMapInvocation(context, invocationExpr, sourceType, destinationType);
    }

    /// <summary>
    ///     Analyzes a CreateMap invocation. Derived classes implement this to provide
    ///     specific diagnostic logic.
    /// </summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The CreateMap invocation expression.</param>
    /// <param name="sourceType">The source type being mapped from.</param>
    /// <param name="destinationType">The destination type being mapped to.</param>
    protected abstract void AnalyzeCreateMapInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType);

    /// <summary>
    ///     Gets mappable properties from a type using standard AutoMapper conventions.
    /// </summary>
    /// <param name="typeSymbol">The type to analyze.</param>
    /// <param name="requireGetter">Whether properties must have a getter.</param>
    /// <param name="requireSetter">Whether properties must have a setter.</param>
    /// <returns>Collection of mappable properties.</returns>
    protected static IReadOnlyList<IPropertySymbol> GetMappableProperties(
        ITypeSymbol typeSymbol,
        bool requireGetter = true,
        bool requireSetter = true)
    {
        return AutoMapperAnalysisHelpers.GetMappableProperties(typeSymbol, requireGetter, requireSetter).ToList();
    }

    /// <summary>
    ///     Checks if a property has an explicit ForMember configuration.
    /// </summary>
    /// <param name="invocation">The CreateMap invocation.</param>
    /// <param name="propertyName">The property name to check.</param>
    /// <param name="semanticModel">The semantic model.</param>
    /// <returns>True if the property is explicitly configured.</returns>
    protected static bool IsPropertyConfigured(
        InvocationExpressionSyntax invocation,
        string propertyName,
        SemanticModel semanticModel)
    {
        return AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember(invocation, propertyName, semanticModel);
    }

    /// <summary>
    ///     Creates a diagnostic with the specified rule and arguments.
    /// </summary>
    /// <param name="rule">The diagnostic rule.</param>
    /// <param name="location">The location for the diagnostic.</param>
    /// <param name="messageArgs">The message format arguments.</param>
    /// <returns>The created diagnostic.</returns>
    protected static Diagnostic CreateDiagnostic(
        DiagnosticDescriptor rule,
        Location location,
        params object[] messageArgs)
    {
        return Diagnostic.Create(rule, location, messageArgs);
    }

    /// <summary>
    ///     Creates a diagnostic with additional properties for code fix providers.
    /// </summary>
    /// <param name="rule">The diagnostic rule.</param>
    /// <param name="location">The location for the diagnostic.</param>
    /// <param name="properties">Additional properties to include.</param>
    /// <param name="messageArgs">The message format arguments.</param>
    /// <returns>The created diagnostic.</returns>
    protected static Diagnostic CreateDiagnosticWithProperties(
        DiagnosticDescriptor rule,
        Location location,
        ImmutableDictionary<string, string?> properties,
        params object[] messageArgs)
    {
        return Diagnostic.Create(rule, location, properties, messageArgs);
    }

    /// <summary>
    ///     Builds an immutable dictionary of diagnostic properties.
    /// </summary>
    /// <param name="properties">Key-value pairs to include.</param>
    /// <returns>An immutable dictionary of properties.</returns>
    protected static ImmutableDictionary<string, string?> BuildProperties(
        params (string Key, string? Value)[] properties)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string?>();
        foreach (var (key, value) in properties)
        {
            builder.Add(key, value);
        }
        return builder.ToImmutable();
    }

    /// <summary>
    ///     Gets the display name of a type, suitable for diagnostic messages.
    /// </summary>
    /// <param name="type">The type symbol.</param>
    /// <returns>The display name.</returns>
    protected static string GetTypeName(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType ? namedType.Name : type.Name;
    }

    /// <summary>
    ///     Checks if two types are compatible for AutoMapper mapping.
    /// </summary>
    /// <param name="sourceType">The source type.</param>
    /// <param name="destinationType">The destination type.</param>
    /// <returns>True if the types are compatible.</returns>
    protected static bool AreTypesCompatible(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        return AutoMapperAnalysisHelpers.AreTypesCompatible(sourceType, destinationType);
    }

    /// <summary>
    ///     Checks if a type is a collection type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is a collection.</returns>
    protected static bool IsCollectionType(ITypeSymbol type)
    {
        return AutoMapperAnalysisHelpers.IsCollectionType(type);
    }

    /// <summary>
    ///     Gets the element type of a collection.
    /// </summary>
    /// <param name="collectionType">The collection type.</param>
    /// <returns>The element type, or null if not a collection.</returns>
    protected static ITypeSymbol? GetCollectionElementType(ITypeSymbol collectionType)
    {
        return AutoMapperAnalysisHelpers.GetCollectionElementType(collectionType);
    }

    /// <summary>
    ///     Checks if the mapping chain contains a ReverseMap call.
    /// </summary>
    /// <param name="createMapInvocation">The CreateMap invocation.</param>
    /// <returns>True if ReverseMap is present.</returns>
    protected static bool HasReverseMap(InvocationExpressionSyntax createMapInvocation)
    {
        return AutoMapperAnalysisHelpers.HasReverseMap(createMapInvocation);
    }
}

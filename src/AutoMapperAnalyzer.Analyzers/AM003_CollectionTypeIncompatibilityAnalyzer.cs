using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
///     Analyzer for detecting collection type incompatibility issues in AutoMapper configurations
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM003_CollectionTypeIncompatibilityAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     AM003: Collection type incompatibility without proper conversion
    /// </summary>
    public static readonly DiagnosticDescriptor CollectionTypeIncompatibilityRule = new(
        "AM003",
        "Collection type incompatibility in AutoMapper configuration",
        "Property '{0}' has incompatible collection types: {1}.{0} ({2}) cannot be mapped to {3}.{0} ({4}) without explicit collection conversion",
        "AutoMapper.Collections",
        DiagnosticSeverity.Error,
        true,
        "Source and destination properties have incompatible collection types that require explicit conversion configuration.");

    /// <summary>
    ///     AM003: Collection element type incompatibility
    /// </summary>
    public static readonly DiagnosticDescriptor CollectionElementIncompatibilityRule = new(
        "AM003",
        "Collection element type incompatibility in AutoMapper configuration",
        "Property '{0}' has incompatible collection element types: {1}.{0} ({2}) elements cannot be mapped to {3}.{0} ({4}) elements without explicit conversion",
        "AutoMapper.Collections",
        DiagnosticSeverity.Warning,
        true,
        "Collection properties have compatible collection types but incompatible element types that may require custom mapping.");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [CollectionTypeIncompatibilityRule, CollectionElementIncompatibilityRule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocationExpr)
        {
            return;
        }

        // Check if this is a CreateMap<TSource, TDestination>() call
        if (!AutoMapperAnalysisHelpers.IsCreateMapInvocation(invocationExpr, context.SemanticModel))
        {
            return;
        }

        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) typeArguments =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (typeArguments.sourceType == null || typeArguments.destinationType == null)
        {
            return;
        }

        // Analyze collection compatibility for property mappings
        AnalyzeCollectionCompatibility(context, invocationExpr, typeArguments.sourceType,
            typeArguments.destinationType);
    }


    private static void AnalyzeCollectionCompatibility(SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType);
        var destinationProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destinationType);

        foreach (IPropertySymbol sourceProperty in sourceProperties)
        {
            IPropertySymbol? destinationProperty = destinationProperties
                .FirstOrDefault(p => p.Name == sourceProperty.Name);

            if (destinationProperty == null)
            {
                continue;
            }

            // Check for explicit property mapping that might handle collection conversion
            if (AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember(invocation, sourceProperty.Name, context.SemanticModel))
            {
                continue;
            }

            // Check if both properties are collections
            if (AutoMapperAnalysisHelpers.IsCollectionType(sourceProperty.Type) && AutoMapperAnalysisHelpers.IsCollectionType(destinationProperty.Type))
            {
                AnalyzeCollectionPropertyCompatibility(context, invocation, sourceProperty, destinationProperty,
                    sourceType, destinationType);
            }
        }
    }


    private static void AnalyzeCollectionPropertyCompatibility(SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IPropertySymbol sourceProperty,
        IPropertySymbol destinationProperty,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        string sourceTypeName = sourceProperty.Type.ToDisplayString();
        string destTypeName = destinationProperty.Type.ToDisplayString();

        // Get element types for comparison
        ITypeSymbol? sourceElementType = AutoMapperAnalysisHelpers.GetCollectionElementType(sourceProperty.Type);
        ITypeSymbol? destElementType = AutoMapperAnalysisHelpers.GetCollectionElementType(destinationProperty.Type);

        if (sourceElementType == null || destElementType == null)
        {
            return;
        }

        // Check if collection types are fundamentally incompatible
        if (AreCollectionTypesIncompatible(sourceProperty.Type, destinationProperty.Type))
        {
            var properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("PropertyName", sourceProperty.Name);
            properties.Add("SourceType", sourceTypeName);
            properties.Add("DestType", destTypeName);
            properties.Add("SourceTypeName", GetTypeName(sourceType));
            properties.Add("DestTypeName", GetTypeName(destinationType));
            properties.Add("SourceElementType", sourceElementType.ToDisplayString());
            properties.Add("DestElementType", destElementType.ToDisplayString());

            var diagnostic = Diagnostic.Create(
                CollectionTypeIncompatibilityRule,
                invocation.GetLocation(),
                properties.ToImmutable(),
                sourceProperty.Name,
                GetTypeName(sourceType),
                sourceTypeName,
                GetTypeName(destinationType),
                destTypeName);

            context.ReportDiagnostic(diagnostic);
        }
        // Check if element types are incompatible
        else if (!AutoMapperAnalysisHelpers.AreTypesCompatible(sourceElementType, destElementType))
        {
            var properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("PropertyName", sourceProperty.Name);
            properties.Add("SourceType", sourceTypeName);
            properties.Add("DestType", destTypeName);
            properties.Add("SourceTypeName", GetTypeName(sourceType));
            properties.Add("DestTypeName", GetTypeName(destinationType));
            properties.Add("SourceElementType", sourceElementType.ToDisplayString());
            properties.Add("DestElementType", destElementType.ToDisplayString());

            var diagnostic = Diagnostic.Create(
                CollectionElementIncompatibilityRule,
                invocation.GetLocation(),
                properties.ToImmutable(),
                sourceProperty.Name,
                sourceType.Name,
                sourceElementType.ToDisplayString(),
                destinationType.Name,
                destElementType.ToDisplayString());

            context.ReportDiagnostic(diagnostic);
        }
    }


    private static bool AreCollectionTypesIncompatible(ITypeSymbol sourceType, ITypeSymbol destType)
    {
        // Arrays and lists are generally compatible
        bool sourceIsArray = sourceType.TypeKind == TypeKind.Array;
        bool destIsArray = destType.TypeKind == TypeKind.Array;

        if (sourceIsArray && destIsArray)
        {
            return false; // Arrays to arrays are compatible
        }

        // Check for specific incompatible combinations
        string sourceTypeName = sourceType.ToDisplayString();
        string destTypeName = destType.ToDisplayString();

        // HashSet to List is generally incompatible without custom handling
        if (sourceTypeName.Contains("HashSet") && destTypeName.Contains("List"))
        {
            return true;
        }

        // Queue/Stack to other collections need special handling
        if ((sourceTypeName.Contains("Queue") || sourceTypeName.Contains("Stack")) &&
            !destTypeName.Contains("Queue") && !destTypeName.Contains("Stack"))
        {
            return true;
        }

        // Non-generic to generic collections
        if (!IsGenericCollection(sourceType) && IsGenericCollection(destType))
        {
            return true;
        }

        return false;
    }

    private static bool IsGenericCollection(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { IsGenericType: true } namedType &&
               namedType.TypeArguments.Length > 0;
    }



    /// <summary>
    /// Gets the type name from an ITypeSymbol.
    /// </summary>
    /// <param name="type">The type symbol.</param>
    /// <returns>The type name.</returns>
    private static string GetTypeName(ITypeSymbol type)
    {
        return type.Name;
    }
}

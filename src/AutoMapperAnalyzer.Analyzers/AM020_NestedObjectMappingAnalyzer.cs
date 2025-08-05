using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
///     Analyzer for detecting nested object mapping issues where complex types require explicit mapping configuration
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM020_NestedObjectMappingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     AM020: Nested object mapping missing - requires explicit mapping configuration
    /// </summary>
    public static readonly DiagnosticDescriptor NestedObjectMappingMissingRule = new(
        "AM020",
        "Nested object mapping configuration missing",
        "Property '{0}' requires mapping configuration between '{1}' and '{2}'. Consider adding CreateMap<{1}, {2}>() or explicit ForMember configuration",
        "AutoMapper.NestedObjects",
        DiagnosticSeverity.Warning,
        true,
        "Complex nested objects require explicit mapping configuration to ensure proper data transformation and avoid runtime exceptions.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [NestedObjectMappingMissingRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCreateMapInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeCreateMapInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;

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

        // Analyze nested object mapping requirements
        AnalyzeNestedObjectMappingRequirements(
            context,
            invocationExpr,
            typeArguments.sourceType,
            typeArguments.destinationType
        );
    }


    private static void AnalyzeNestedObjectMappingRequirements(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType);
        var destinationProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destinationType);

        // Get all CreateMap configurations in the same profile
        HashSet<(string sourceType, string destType)> existingMappings = GetExistingMappings(context, invocation);

        // Check each property pair for nested object mapping requirements
        foreach (IPropertySymbol sourceProperty in sourceProperties)
        {
            // Find corresponding destination property
            IPropertySymbol? destinationProperty = destinationProperties
                .FirstOrDefault(p => string.Equals(p.Name, sourceProperty.Name, StringComparison.OrdinalIgnoreCase));

            if (destinationProperty == null)
            {
                continue; // No corresponding property, handled by other analyzers
            }

            // Check if this property requires nested object mapping
            if (RequiresNestedObjectMapping(sourceProperty.Type, destinationProperty.Type))
            {
                // Check if mapping already exists
                string sourceTypeName = GetTypeNameWithoutNullability(sourceProperty.Type);
                string destTypeName = GetTypeNameWithoutNullability(destinationProperty.Type);

                if (HasExistingMapping(existingMappings, sourceTypeName, destTypeName))
                {
                    continue; // Mapping already configured
                }

                // Check if property is explicitly mapped via ForMember
                if (AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember(invocation, destinationProperty.Name, context.SemanticModel))
                {
                    continue; // Property is explicitly handled
                }

                // Report diagnostic for missing nested object mapping
                var diagnostic = Diagnostic.Create(
                    NestedObjectMappingMissingRule,
                    invocation.GetLocation(),
                    sourceProperty.Name,
                    sourceTypeName,
                    destTypeName);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static IPropertySymbol[] GetMappableProperties(INamedTypeSymbol type)
    {
        var properties = new List<IPropertySymbol>();

        // Get properties from the type and all its base types
        INamedTypeSymbol? currentType = type;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            // Get declared properties (not inherited ones to avoid duplicates)
            IPropertySymbol[] typeProperties = currentType.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                            p.CanBeReferencedByName &&
                            !p.IsStatic &&
                            p.GetMethod != null && // Must be readable
                            p.SetMethod != null && // Must be writable
                            p.ContainingType.Equals(currentType, SymbolEqualityComparer.Default)) // Only direct members
                .ToArray();

            properties.AddRange(typeProperties);
            currentType = currentType.BaseType;
        }

        return properties.ToArray();
    }

    private static bool RequiresNestedObjectMapping(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        // Get the underlying types (remove nullable wrappers)
        ITypeSymbol sourceUnderlyingType = GetUnderlyingType(sourceType);
        ITypeSymbol destUnderlyingType = GetUnderlyingType(destinationType);

        // Same types don't need mapping
        if (SymbolEqualityComparer.Default.Equals(sourceUnderlyingType, destUnderlyingType))
        {
            return false;
        }

        // Built-in value types and strings don't need nested object mapping
        if (IsBuiltInType(sourceUnderlyingType) || IsBuiltInType(destUnderlyingType))
        {
            return false;
        }

        // Collections should be handled by AM021, not AM020
        if (AutoMapperAnalysisHelpers.IsCollectionType(sourceUnderlyingType) || AutoMapperAnalysisHelpers.IsCollectionType(destUnderlyingType))
        {
            return false;
        }

        // Both must be reference types (classes) that are different
        return sourceUnderlyingType.TypeKind == TypeKind.Class &&
               destUnderlyingType.TypeKind == TypeKind.Class;
    }

    private static ITypeSymbol GetUnderlyingType(ITypeSymbol type)
    {
        // Handle nullable reference types (T?)
        if (type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return namedType.TypeArguments[0];
        }

        // Handle nullable reference types with annotations
        if (type.CanBeReferencedByName && type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        }

        return type;
    }

    private static bool IsBuiltInType(ITypeSymbol type)
    {
        // Check for built-in value types and common reference types that don't need mapping
        return type.SpecialType != SpecialType.None ||
               type.Name == "String" ||
               type.Name == "DateTime" ||
               type.Name == "DateTimeOffset" ||
               type.Name == "TimeSpan" ||
               type.Name == "Guid" ||
               type.Name == "Decimal";
    }

    private static bool IsCollectionType(ITypeSymbol type)
    {
        // Check if type implements IEnumerable (but not string)
        if (type.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        return type.AllInterfaces.Any(i =>
            i.Name == "IEnumerable" ||
            i.Name == "ICollection" ||
            i.Name == "IList");
    }

    private static string GetTypeNameWithoutNullability(ITypeSymbol type)
    {
        ITypeSymbol underlyingType = GetUnderlyingType(type);
        return underlyingType.Name;
    }

    private static HashSet<(string sourceType, string destType)> GetExistingMappings(
        SyntaxNodeAnalysisContext context, InvocationExpressionSyntax currentInvocation)
    {
        var mappings = new HashSet<(string, string)>();

        // Find the containing class (Profile)
        ClassDeclarationSyntax? containingClass =
            currentInvocation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass == null)
        {
            return mappings;
        }

        // Find all CreateMap invocations in the same class
        IEnumerable<InvocationExpressionSyntax> createMapInvocations = containingClass.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => AutoMapperAnalysisHelpers.IsCreateMapInvocation(inv, context.SemanticModel));

        foreach (InvocationExpressionSyntax? invocation in createMapInvocations)
        {
            (ITypeSymbol? sourceType, ITypeSymbol? destinationType) typeArgs =
                AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, context.SemanticModel);
            if (typeArgs.sourceType != null && typeArgs.destinationType != null)
            {
                mappings.Add((typeArgs.sourceType.Name, typeArgs.destinationType.Name));
            }
        }

        return mappings;
    }

    private static bool HasExistingMapping(HashSet<(string sourceType, string destType)> existingMappings,
        string sourceTypeName, string destTypeName)
    {
        return existingMappings.Contains((sourceTypeName, destTypeName));
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

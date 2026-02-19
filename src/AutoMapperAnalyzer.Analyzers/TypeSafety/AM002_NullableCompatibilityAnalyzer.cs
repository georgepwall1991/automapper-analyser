using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.TypeSafety;

/// <summary>
///     Analyzer for detecting nullable reference type compatibility issues in AutoMapper configurations.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM002_NullableCompatibilityAnalyzer : DiagnosticAnalyzer
{
    internal const string PropertyNamePropertyName = "PropertyName";
    internal const string SourcePropertyTypePropertyName = "SourcePropertyType";
    internal const string DestinationPropertyTypePropertyName = "DestinationPropertyType";

    /// <summary>
    ///     AM002: Nullable to non-nullable assignment without proper handling.
    /// </summary>
    public static readonly DiagnosticDescriptor NullableToNonNullableRule = new(
        "AM002",
        "Nullable to non-nullable mapping issue in AutoMapper configuration",
        "Property '{0}' has nullable compatibility issue: {1}.{0} ({2}) can be null but {3}.{0} ({4}) is non-nullable",
        "AutoMapper.NullSafety",
        DiagnosticSeverity.Error,
        true,
        "Source property is nullable but destination property is non-nullable, which could cause null reference exceptions at runtime.");

    /// <summary>
    ///     AM002: Non-nullable to nullable assignment (informational).
    /// </summary>
    public static readonly DiagnosticDescriptor NonNullableToNullableRule = new(
        "AM002",
        "Non-nullable to nullable mapping in AutoMapper configuration",
        "Property '{0}' mapping: {1}.{0} ({2}) is non-nullable but {3}.{0} ({4}) is nullable",
        "AutoMapper.NullSafety",
        DiagnosticSeverity.Info,
        true,
        "Non-nullable source property is being mapped to nullable destination property.");

    /// <summary>
    ///     Gets the supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [NullableToNonNullableRule, NonNullableToNullableRule];

    /// <summary>
    ///     Initializes the analyzer.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeCreateMapInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeCreateMapInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;

        // Ensure this resolves to AutoMapper CreateMap to avoid lookalike API false positives.
        if (!IsAutoMapperCreateMapInvocation(invocationExpr, context.SemanticModel))
        {
            return;
        }

        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (sourceType == null || destinationType == null)
        {
            return;
        }

        // Analyze nullable compatibility for property mappings
        AnalyzeNullablePropertyMappings(
            context,
            invocationExpr,
            sourceType,
            destinationType
        );
    }

    private static void AnalyzeNullablePropertyMappings(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        IPropertySymbol[] sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false).ToArray();
        IPropertySymbol[] destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, false).ToArray();

        foreach (IPropertySymbol sourceProperty in sourceProperties)
        {
            IPropertySymbol? destinationProperty = destinationProperties.FirstOrDefault(p => p.Name == sourceProperty.Name) ??
                                                   destinationProperties.FirstOrDefault(p =>
                                                       string.Equals(p.Name, sourceProperty.Name,
                                                           StringComparison.OrdinalIgnoreCase));

            if (destinationProperty != null)
            {
                // Check for explicit property mapping that might handle nullability
                if (IsPropertyConfiguredWithForMember(invocation, sourceProperty.Name, context.SemanticModel))
                {
                    continue;
                }

                AnalyzeNullableCompatibility(
                    context,
                    invocation,
                    sourceProperty,
                    destinationProperty,
                    sourceType,
                    destinationType
                );
            }
        }
    }

    private static void AnalyzeNullableCompatibility(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IPropertySymbol sourceProperty,
        IPropertySymbol destinationProperty,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        string sourceTypeName = sourceProperty.Type.ToDisplayString();
        string destTypeName = destinationProperty.Type.ToDisplayString();

        // Case 1: Nullable source -> Non-nullable destination (WARNING)
        if (IsNullableType(sourceProperty.Type) && !IsNullableType(destinationProperty.Type))
        {
            // Check if the underlying types are compatible
            ITypeSymbol sourceUnderlyingType = AutoMapperAnalysisHelpers.GetUnderlyingType(sourceProperty.Type);
            ITypeSymbol destUnderlyingType = AutoMapperAnalysisHelpers.GetUnderlyingType(destinationProperty.Type);

            if (AreUnderlyingTypesCompatible(sourceUnderlyingType, destUnderlyingType))
            {
                var properties = ImmutableDictionary.CreateBuilder<string, string?>();
                properties.Add(PropertyNamePropertyName, sourceProperty.Name);
                properties.Add(SourcePropertyTypePropertyName, sourceTypeName);
                properties.Add(DestinationPropertyTypePropertyName, destTypeName);

                var diagnostic = Diagnostic.Create(
                    NullableToNonNullableRule,
                    invocation.GetLocation(),
                    properties.ToImmutable(),
                    sourceProperty.Name,
                    AutoMapperAnalysisHelpers.GetTypeName(sourceType),
                    sourceTypeName,
                    AutoMapperAnalysisHelpers.GetTypeName(destinationType),
                    destTypeName
                );
                context.ReportDiagnostic(diagnostic);
            }
        }
        // Case 2: Non-nullable source -> Nullable destination (INFO)
        else if (!IsNullableType(sourceProperty.Type) && IsNullableType(destinationProperty.Type))
        {
            ITypeSymbol sourceUnderlyingType = AutoMapperAnalysisHelpers.GetUnderlyingType(sourceProperty.Type);
            ITypeSymbol destUnderlyingType = AutoMapperAnalysisHelpers.GetUnderlyingType(destinationProperty.Type);

            if (AreUnderlyingTypesCompatible(sourceUnderlyingType, destUnderlyingType))
            {
                var properties = ImmutableDictionary.CreateBuilder<string, string?>();
                properties.Add(PropertyNamePropertyName, sourceProperty.Name);
                properties.Add(SourcePropertyTypePropertyName, sourceTypeName);
                properties.Add(DestinationPropertyTypePropertyName, destTypeName);

                var diagnostic = Diagnostic.Create(
                    NonNullableToNullableRule,
                    invocation.GetLocation(),
                    properties.ToImmutable(),
                    sourceProperty.Name,
                    AutoMapperAnalysisHelpers.GetTypeName(sourceType),
                    sourceTypeName,
                    AutoMapperAnalysisHelpers.GetTypeName(destinationType),
                    destTypeName
                );
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool IsNullableType(ITypeSymbol type)
    {
        // Check for nullable reference types (string?, object?, etc.)
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }

        // Check for nullable value types (int?, DateTime?, etc.)
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return true;
        }

        // Check by string representation for cases where annotation might not be detected
        string typeString = type.ToDisplayString();
        return typeString.EndsWith("?");
    }

    private static bool AreUnderlyingTypesCompatible(ITypeSymbol sourceType, ITypeSymbol destType)
    {
        // Use helper for comprehensive compatibility checking
        return AutoMapperAnalysisHelpers.AreTypesCompatible(sourceType, destType);
    }


    private static bool IsPropertyConfiguredWithForMember(
        InvocationExpressionSyntax createMapInvocation,
        string propertyName,
        SemanticModel semanticModel)
    {
        IEnumerable<InvocationExpressionSyntax> forMemberCalls = AutoMapperAnalysisHelpers.GetForMemberCalls(createMapInvocation);

        foreach (InvocationExpressionSyntax forMemberCall in forMemberCalls)
        {
            if (!MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(forMemberCall, semanticModel, "ForMember"))
            {
                continue;
            }

            if (forMemberCall.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            ArgumentSyntax destinationArgument = forMemberCall.ArgumentList.Arguments[0];
            CSharpSyntaxNode? lambdaBody = AutoMapperAnalysisHelpers.GetLambdaBody(destinationArgument.Expression);
            if (lambdaBody is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            if (string.Equals(memberAccess.Name.Identifier.Text, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }


    private static bool IsAutoMapperCreateMapInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        return MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "CreateMap");
    }


}

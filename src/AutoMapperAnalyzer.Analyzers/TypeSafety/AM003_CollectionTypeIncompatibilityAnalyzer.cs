using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.TypeSafety;

/// <summary>
///     Analyzer for detecting collection type incompatibility issues in AutoMapper configurations
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM003_CollectionTypeIncompatibilityAnalyzer : DiagnosticAnalyzer
{
    internal const string PropertyNamePropertyName = "PropertyName";
    internal const string SourcePropertyTypePropertyName = "SourcePropertyType";
    internal const string DestinationPropertyTypePropertyName = "DestinationPropertyType";
    internal const string SourceElementTypePropertyName = "SourceElementType";
    internal const string DestinationElementTypePropertyName = "DestinationElementType";

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

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [CollectionTypeIncompatibilityRule, CollectionElementIncompatibilityRule];

    /// <inheritdoc />
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

        // Ensure this invocation resolves to AutoMapper CreateMap to avoid lookalike false positives.
        if (!IsAutoMapperCreateMapInvocation(invocationExpr, context.SemanticModel))
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
        IEnumerable<IPropertySymbol> sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        IEnumerable<IPropertySymbol> destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, false);

        foreach (IPropertySymbol sourceProperty in sourceProperties)
        {
            IPropertySymbol? destinationProperty =
                destinationProperties.FirstOrDefault(p => p.Name == sourceProperty.Name) ??
                destinationProperties.FirstOrDefault(p =>
                    string.Equals(p.Name, sourceProperty.Name, StringComparison.OrdinalIgnoreCase));

            if (destinationProperty == null)
            {
                continue;
            }

            // Check for explicit property mapping that might handle collection conversion
            if (IsPropertyConfiguredWithForMember(invocation, sourceProperty.Name, context.SemanticModel))
            {
                continue;
            }

            // Check if both properties are collections
            if (AutoMapperAnalysisHelpers.IsCollectionType(sourceProperty.Type) &&
                AutoMapperAnalysisHelpers.IsCollectionType(destinationProperty.Type))
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
            ImmutableDictionary<string, string?>.Builder properties =
                ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add(PropertyNamePropertyName, sourceProperty.Name);
            properties.Add(SourcePropertyTypePropertyName, sourceTypeName);
            properties.Add(DestinationPropertyTypePropertyName, destTypeName);
            properties.Add(SourceElementTypePropertyName, sourceElementType.ToDisplayString());
            properties.Add(DestinationElementTypePropertyName, destElementType.ToDisplayString());
            // Backward-compatible aliases for existing fixers/consumers.
            properties.Add("SourceType", sourceTypeName);
            properties.Add("DestType", destTypeName);
            properties.Add("SourceTypeName", GetTypeName(sourceType));
            properties.Add("DestTypeName", GetTypeName(destinationType));

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
            ImmutableDictionary<string, string?>.Builder properties =
                ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add(PropertyNamePropertyName, sourceProperty.Name);
            properties.Add(SourcePropertyTypePropertyName, sourceTypeName);
            properties.Add(DestinationPropertyTypePropertyName, destTypeName);
            properties.Add(SourceElementTypePropertyName, sourceElementType.ToDisplayString());
            properties.Add(DestinationElementTypePropertyName, destElementType.ToDisplayString());
            // Backward-compatible aliases for existing fixers/consumers.
            properties.Add("SourceType", sourceTypeName);
            properties.Add("DestType", destTypeName);
            properties.Add("SourceTypeName", GetTypeName(sourceType));
            properties.Add("DestTypeName", GetTypeName(destinationType));

            var diagnostic = Diagnostic.Create(
                CollectionElementIncompatibilityRule,
                invocation.GetLocation(),
                properties.ToImmutable(),
                sourceProperty.Name,
                GetTypeName(sourceType),
                sourceElementType.ToDisplayString(),
                GetTypeName(destinationType),
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
        bool sourceIsHashSet = IsConstructedFromType(sourceType, "System.Collections.Generic.HashSet<T>");
        bool destIsHashSet = IsConstructedFromType(destType, "System.Collections.Generic.HashSet<T>");
        bool sourceIsQueue = IsConstructedFromType(sourceType, "System.Collections.Generic.Queue<T>");
        bool destIsQueue = IsConstructedFromType(destType, "System.Collections.Generic.Queue<T>");
        bool sourceIsStack = IsConstructedFromType(sourceType, "System.Collections.Generic.Stack<T>");
        bool destIsStack = IsConstructedFromType(destType, "System.Collections.Generic.Stack<T>");

        // HashSet ↔ List/Array/IEnumerable bidirectional incompatibility
        if (sourceIsHashSet && !destIsHashSet)
        {
            return true;
        }

        if (!sourceIsHashSet && destIsHashSet)
        {
            return true;
        }

        // Queue ↔ other collections bidirectional incompatibility
        if (sourceIsQueue && !destIsQueue)
        {
            return true;
        }

        if (!sourceIsQueue && destIsQueue)
        {
            return true;
        }

        // Stack ↔ other collections bidirectional incompatibility
        if (sourceIsStack && !destIsStack)
        {
            return true;
        }

        if (!sourceIsStack && destIsStack)
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

    private static bool IsConstructedFromType(ITypeSymbol type, string genericDefinitionName)
    {
        return type is INamedTypeSymbol namedType &&
               namedType.OriginalDefinition.ToDisplayString() == genericDefinitionName;
    }


    /// <summary>
    ///     Gets the type name from an ITypeSymbol.
    /// </summary>
    /// <param name="type">The type symbol.</param>
    /// <returns>The type name.</returns>
    private static string GetTypeName(ITypeSymbol type)
    {
        return type.Name;
    }

    private static bool IsPropertyConfiguredWithForMember(
        InvocationExpressionSyntax createMapInvocation,
        string propertyName,
        SemanticModel semanticModel)
    {
        IEnumerable<InvocationExpressionSyntax> forMemberCalls =
            AutoMapperAnalysisHelpers.GetForMemberCalls(createMapInvocation);

        foreach (InvocationExpressionSyntax forMemberCall in forMemberCalls)
        {
            if (!IsAutoMapperMethodInvocation(forMemberCall, semanticModel, "ForMember"))
            {
                continue;
            }

            if (forMemberCall.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            ArgumentSyntax destinationArgument = forMemberCall.ArgumentList.Arguments[0];
            CSharpSyntaxNode? lambdaBody = GetLambdaBody(destinationArgument.Expression);
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

    private static CSharpSyntaxNode? GetLambdaBody(ExpressionSyntax expression)
    {
        return expression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Body,
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda => parenthesizedLambda.Body,
            _ => null
        };
    }

    private static bool IsAutoMapperCreateMapInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        return IsAutoMapperMethodInvocation(invocation, semanticModel, "CreateMap");
    }

    private static bool IsAutoMapperMethodInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string methodName)
    {
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);

        if (IsAutoMapperMethod(symbolInfo.Symbol as IMethodSymbol, methodName))
        {
            return true;
        }

        foreach (ISymbol candidateSymbol in symbolInfo.CandidateSymbols)
        {
            if (IsAutoMapperMethod(candidateSymbol as IMethodSymbol, methodName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAutoMapperMethod(IMethodSymbol? methodSymbol, string methodName)
    {
        if (methodSymbol == null || methodSymbol.Name != methodName)
        {
            return false;
        }

        string? namespaceName = methodSymbol.ContainingNamespace?.ToDisplayString();
        return namespaceName == "AutoMapper" ||
               (namespaceName?.StartsWith("AutoMapper.", StringComparison.Ordinal) ?? false);
    }
}

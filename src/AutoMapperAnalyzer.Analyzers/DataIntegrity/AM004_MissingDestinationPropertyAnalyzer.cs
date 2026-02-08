using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.DataIntegrity;

/// <summary>
///     Analyzer for detecting missing destination properties that could lead to data loss in AutoMapper configurations
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM004_MissingDestinationPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     AM004: Missing destination property - potential data loss
    /// </summary>
    public static readonly DiagnosticDescriptor MissingDestinationPropertyRule = new(
        "AM004",
        "Source property has no corresponding destination property",
        "Source property '{0}' will not be mapped - potential data loss",
        "AutoMapper.MissingProperty",
        DiagnosticSeverity.Warning,
        true,
        "Source property exists but no corresponding destination property found, which may result in data loss during mapping.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [MissingDestinationPropertyRule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCreateMapInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeCreateMapInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;

        // Check if this is an AutoMapper CreateMap<TSource, TDestination>() call
        if (!IsAutoMapperCreateMapInvocation(invocationExpr, context.SemanticModel))
        {
            return;
        }

        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) typeArguments =
            GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (typeArguments.sourceType == null || typeArguments.destinationType == null)
        {
            return;
        }

        // Analyze missing properties for Forward Map (Source -> Destination)
        AnalyzeMissingDestinationProperties(
            context,
            invocationExpr,
            typeArguments.sourceType,
            typeArguments.destinationType,
            context.SemanticModel,
            stopAtReverseMapBoundary: true
        );

        // Check for ReverseMap() and analyze Destination -> Source
        InvocationExpressionSyntax? reverseMapInvocation =
            GetAutoMapperReverseMapInvocation(invocationExpr, context.SemanticModel);
        if (reverseMapInvocation != null)
        {
            AnalyzeMissingDestinationProperties(
                context,
                reverseMapInvocation,
                typeArguments.destinationType, // Source is now Destination
                typeArguments.sourceType, // Destination is now Source
                context.SemanticModel,
                stopAtReverseMapBoundary: false
            );
        }
    }

    private static InvocationExpressionSyntax? GetAutoMapperReverseMapInvocation(
        InvocationExpressionSyntax createMapInvocation,
        SemanticModel semanticModel)
    {
        foreach (InvocationExpressionSyntax chainedInvocation in GetScopedChainInvocations(
                     createMapInvocation,
                     semanticModel,
                     stopAtReverseMapBoundary: false))
        {
            if (IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ReverseMap"))
            {
                return chainedInvocation;
            }
        }

        return null;
    }

    private static void AnalyzeMissingDestinationProperties(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax mappingInvocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        // Custom construction/conversion can legitimately consume source members without
        // one-to-one destination properties. Skip this direction to avoid noisy false positives.
        if (HasCustomConstructionOrConversion(mappingInvocation, semanticModel, stopAtReverseMapBoundary))
        {
            return;
        }

        IEnumerable<IPropertySymbol> sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        IEnumerable<IPropertySymbol> destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, false);

        // Check each source property to see if it has a corresponding destination property
        foreach (IPropertySymbol sourceProperty in sourceProperties)
        {
            // Check if destination has a property with the same name (case-insensitive, like AutoMapper)
            IPropertySymbol? destinationProperty = destinationProperties
                .FirstOrDefault(p => string.Equals(p.Name, sourceProperty.Name, StringComparison.OrdinalIgnoreCase));

            if (destinationProperty != null)
            {
                continue; // Property exists in destination, no data loss
            }

            // Check for flattening: if source property is complex and destination has properties starting with source name
            // AutoMapper flattens complex properties (e.g. Customer.Name -> CustomerName)
            if (IsFlatteningMatch(sourceProperty, destinationProperties))
            {
                continue; // Property is used in flattening
            }

            // Check if this source property is handled by custom mapping configuration
            if (IsSourcePropertyHandledByCustomMapping(
                    mappingInvocation,
                    sourceProperty.Name,
                    semanticModel,
                    stopAtReverseMapBoundary))
            {
                continue; // Property is handled by custom mapping, no data loss
            }

            // Check if this source property is consumed by constructor parameter mapping
            if (IsSourcePropertyHandledByCtorParamMapping(
                    mappingInvocation,
                    sourceProperty.Name,
                    semanticModel,
                    stopAtReverseMapBoundary))
            {
                continue; // Property is mapped to ctor parameter, no data loss
            }

            // Check if this source property is explicitly ignored
            if (IsSourcePropertyExplicitlyIgnored(
                    mappingInvocation,
                    sourceProperty.Name,
                    semanticModel,
                    stopAtReverseMapBoundary))
            {
                continue; // Property is explicitly ignored, no diagnostic needed
            }

            // Report diagnostic for missing destination property
            ImmutableDictionary<string, string?>.Builder properties =
                ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("PropertyName", sourceProperty.Name);
            properties.Add("PropertyType", sourceProperty.Type.ToDisplayString());
            properties.Add("SourceTypeName", GetTypeName(sourceType));
            properties.Add("DestinationTypeName", GetTypeName(destinationType));

            var diagnostic = Diagnostic.Create(
                MissingDestinationPropertyRule,
                mappingInvocation.GetLocation(),
                properties.ToImmutable(),
                sourceProperty.Name);

            context.ReportDiagnostic(diagnostic);
        }
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

    private static bool IsFlatteningMatch(
        IPropertySymbol sourceProperty,
        IEnumerable<IPropertySymbol> destinationProperties)
    {
        if (AutoMapperAnalysisHelpers.IsBuiltInType(sourceProperty.Type))
        {
            return false;
        }

        IEnumerable<IPropertySymbol> nestedProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceProperty.Type, requireSetter: false);

        foreach (IPropertySymbol destinationProperty in destinationProperties)
        {
            if (!destinationProperty.Name.StartsWith(sourceProperty.Name, StringComparison.OrdinalIgnoreCase) ||
                destinationProperty.Name.Length <= sourceProperty.Name.Length)
            {
                continue;
            }

            string flattenedMemberName = destinationProperty.Name.Substring(sourceProperty.Name.Length);
            if (nestedProperties.Any(p => string.Equals(p.Name, flattenedMemberName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSourcePropertyHandledByCustomMapping(
        InvocationExpressionSyntax mappingInvocation,
        string sourcePropertyName,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        foreach (InvocationExpressionSyntax chainedInvocation in GetScopedChainInvocations(
                     mappingInvocation,
                     semanticModel,
                     stopAtReverseMapBoundary))
        {
            if (!IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ForMember"))
            {
                continue;
            }

            if (ForMemberReferencesSourceProperty(chainedInvocation, sourcePropertyName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ForMemberReferencesSourceProperty(
        InvocationExpressionSyntax forMemberInvocation,
        string sourcePropertyName)
    {
        // ForMember's second argument contains the mapping lambda where source usage appears.
        if (forMemberInvocation.ArgumentList.Arguments.Count > 1)
        {
            return ContainsPropertyReference(forMemberInvocation.ArgumentList.Arguments[1].Expression, sourcePropertyName);
        }

        return false;
    }

    private static bool HasCustomConstructionOrConversion(
        InvocationExpressionSyntax mappingInvocation,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        foreach (InvocationExpressionSyntax chainedInvocation in GetScopedChainInvocations(
                     mappingInvocation,
                     semanticModel,
                     stopAtReverseMapBoundary))
        {
            if (IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ConstructUsing") ||
                IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ConvertUsing"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSourcePropertyHandledByCtorParamMapping(
        InvocationExpressionSyntax mappingInvocation,
        string sourcePropertyName,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        foreach (InvocationExpressionSyntax chainedInvocation in GetScopedChainInvocations(
                     mappingInvocation,
                     semanticModel,
                     stopAtReverseMapBoundary))
        {
            if (!IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ForCtorParam") ||
                chainedInvocation.ArgumentList.Arguments.Count <= 1)
            {
                continue;
            }

            ExpressionSyntax ctorMappingArg = chainedInvocation.ArgumentList.Arguments[1].Expression;
            if (ContainsPropertyReference(ctorMappingArg, sourcePropertyName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSourcePropertyExplicitlyIgnored(
        InvocationExpressionSyntax mappingInvocation,
        string sourcePropertyName,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        foreach (InvocationExpressionSyntax chainedInvocation in GetScopedChainInvocations(
                     mappingInvocation,
                     semanticModel,
                     stopAtReverseMapBoundary))
        {
            if (!IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ForSourceMember"))
            {
                continue;
            }

            if (IsForSourceMemberOfProperty(chainedInvocation, sourcePropertyName) &&
                HasDoNotValidateCall(chainedInvocation))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsForSourceMemberOfProperty(
        InvocationExpressionSyntax forSourceMemberInvocation,
        string propertyName)
    {
        if (forSourceMemberInvocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        string? selectedMember = GetSelectedMemberName(forSourceMemberInvocation.ArgumentList.Arguments[0].Expression);
        return string.Equals(selectedMember, propertyName, StringComparison.Ordinal);
    }

    private static bool HasDoNotValidateCall(InvocationExpressionSyntax forSourceMemberInvocation)
    {
        if (forSourceMemberInvocation.ArgumentList.Arguments.Count <= 1)
        {
            return false;
        }

        ExpressionSyntax secondArg = forSourceMemberInvocation.ArgumentList.Arguments[1].Expression;
        return secondArg.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.ValueText == "DoNotValidate");
    }

    private static bool ContainsPropertyReference(SyntaxNode node, string propertyName)
    {
        if (node is MemberAccessExpressionSyntax rootMemberAccess &&
            rootMemberAccess.Name.Identifier.ValueText == propertyName)
        {
            return true;
        }

        return node.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
            .Any(memberAccess => memberAccess.Name.Identifier.ValueText == propertyName);
    }

    private static string? GetSelectedMemberName(ExpressionSyntax expression)
    {
        return expression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda when simpleLambda.Body is MemberAccessExpressionSyntax memberAccess =>
                memberAccess.Name.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda
                when parenthesizedLambda.Body is MemberAccessExpressionSyntax memberAccess =>
                memberAccess.Name.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => null
        };
    }

    private static IEnumerable<InvocationExpressionSyntax> GetScopedChainInvocations(
        InvocationExpressionSyntax mappingInvocation,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        SyntaxNode? currentNode = mappingInvocation.Parent;

        while (currentNode is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if (stopAtReverseMapBoundary &&
                IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ReverseMap"))
            {
                break;
            }

            yield return chainedInvocation;
            currentNode = chainedInvocation.Parent;
        }
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

    private static (ITypeSymbol? sourceType, ITypeSymbol? destinationType) GetCreateMapTypeArguments(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);

        if (TryGetCreateMapTypeArgumentsFromMethod(symbolInfo.Symbol as IMethodSymbol, out ITypeSymbol? sourceType,
                out ITypeSymbol? destinationType))
        {
            return (sourceType, destinationType);
        }

        foreach (ISymbol candidateSymbol in symbolInfo.CandidateSymbols)
        {
            if (TryGetCreateMapTypeArgumentsFromMethod(candidateSymbol as IMethodSymbol, out sourceType,
                    out destinationType))
            {
                return (sourceType, destinationType);
            }
        }

        return AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
    }

    private static bool TryGetCreateMapTypeArgumentsFromMethod(
        IMethodSymbol? methodSymbol,
        out ITypeSymbol? sourceType,
        out ITypeSymbol? destinationType)
    {
        sourceType = null;
        destinationType = null;

        if (methodSymbol?.TypeArguments.Length != 2)
        {
            return false;
        }

        sourceType = methodSymbol.TypeArguments[0];
        destinationType = methodSymbol.TypeArguments[1];
        return true;
    }
}

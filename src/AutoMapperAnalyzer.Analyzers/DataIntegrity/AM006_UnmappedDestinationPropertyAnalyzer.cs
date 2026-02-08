using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.DataIntegrity;

/// <summary>
///     Analyzer for detecting destination properties that are not mapped from source
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM006_UnmappedDestinationPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     AM006: Unmapped destination property
    /// </summary>
    public static readonly DiagnosticDescriptor UnmappedDestinationPropertyRule = new(
        "AM006",
        "Destination property is not mapped",
        "Destination property '{0}' is not mapped from source '{1}'",
        "AutoMapper.DataIntegrity",
        DiagnosticSeverity.Info,
        true,
        "Destination property exists but has no corresponding source property or explicit mapping.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [UnmappedDestinationPropertyRule];

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

        if (!IsAutoMapperCreateMapInvocation(invocationExpr, context.SemanticModel))
        {
            return;
        }

        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) typeArguments =
            MappingChainAnalysisHelper.GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (typeArguments.sourceType == null || typeArguments.destinationType == null)
        {
            return;
        }

        // Analyze forward map
        AnalyzeUnmappedDestinationProperties(
            context,
            invocationExpr,
            typeArguments.sourceType,
            typeArguments.destinationType,
            false
        );

        // Analyze reverse map
        InvocationExpressionSyntax? reverseMapInvocation =
            AutoMapperAnalysisHelpers.GetReverseMapInvocation(invocationExpr);
        if (reverseMapInvocation != null)
        {
            AnalyzeUnmappedDestinationProperties(
                context,
                invocationExpr,
                typeArguments.destinationType, // Source is now Destination
                typeArguments.sourceType, // Destination is now Source
                true,
                reverseMapInvocation
            );
        }
    }

    private static void AnalyzeUnmappedDestinationProperties(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        bool isReverseMap,
        InvocationExpressionSyntax? reverseMapInvocation = null)
    {
        IEnumerable<IPropertySymbol> sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        IEnumerable<IPropertySymbol> destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, false);
        InvocationExpressionSyntax mappingInvocation =
            isReverseMap && reverseMapInvocation != null ? reverseMapInvocation : invocation;

        foreach (IPropertySymbol destProperty in destinationProperties)
        {
            // Required members are enforced by AM011 (error). Skip here to avoid duplicate,
            // contradictory diagnostics for the same property.
            if (destProperty.IsRequired)
            {
                continue;
            }

            // 1. Check for direct mapping (same name)
            if (sourceProperties.Any(p => string.Equals(p.Name, destProperty.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // 2. Check for flattening
            // If Dest is "CustomerName" and Source has "Customer" (complex), and "Customer" has "Name".
            bool matchesFlattening = sourceProperties.Any(srcProp => IsFlatteningMatch(srcProp, destProperty));
            if (matchesFlattening) continue;

            // 3. Check for explicit configuration (ForMember)
            if (IsPropertyConfiguredWithForMember(
                    mappingInvocation,
                    destProperty.Name,
                    context.SemanticModel,
                    stopAtReverseMapBoundary: !isReverseMap))
            {
                continue;
            }
            
            // 4. Check for Ignore? 
            // IsPropertyConfiguredWithForMember checks if ForMember exists. 
            // If ForMember exists (even if Ignore), it is "mapped" (or explicitly handled).
            // So check 3 covers Ignore.

            // Report diagnostic
            ImmutableDictionary<string, string?>.Builder properties =
                ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("PropertyName", destProperty.Name);
            properties.Add("PropertyType", destProperty.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            properties.Add("DestinationTypeName", destinationType.Name);
            properties.Add("SourceTypeName", sourceType.Name);

            var diagnostic = Diagnostic.Create(
                UnmappedDestinationPropertyRule,
                mappingInvocation.GetLocation(),
                properties.ToImmutable(),
                destProperty.Name,
                sourceType.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsFlatteningMatch(IPropertySymbol sourceProperty, IPropertySymbol destinationProperty)
    {
        if (AutoMapperAnalysisHelpers.IsBuiltInType(sourceProperty.Type))
        {
            return false;
        }

        if (!destinationProperty.Name.StartsWith(sourceProperty.Name, StringComparison.OrdinalIgnoreCase) ||
            destinationProperty.Name.Length <= sourceProperty.Name.Length)
        {
            return false;
        }

        string flattenedMemberName = destinationProperty.Name.Substring(sourceProperty.Name.Length);
        if (string.IsNullOrWhiteSpace(flattenedMemberName))
        {
            return false;
        }

        IEnumerable<IPropertySymbol> nestedProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceProperty.Type, requireSetter: false);
        return nestedProperties.Any(p => string.Equals(p.Name, flattenedMemberName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPropertyConfiguredWithForMember(
        InvocationExpressionSyntax createMapInvocation,
        string propertyName,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        IEnumerable<InvocationExpressionSyntax> forMemberCalls =
            GetScopedForMemberCalls(createMapInvocation, stopAtReverseMapBoundary);

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

    private static IEnumerable<InvocationExpressionSyntax> GetScopedForMemberCalls(
        InvocationExpressionSyntax mappingInvocation,
        bool stopAtReverseMapBoundary)
    {
        var forMemberCalls = new List<InvocationExpressionSyntax>();
        SyntaxNode? currentNode = mappingInvocation.Parent;

        while (currentNode is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax invocation)
        {
            string methodName = memberAccess.Name.Identifier.Text;
            if (stopAtReverseMapBoundary && methodName == "ReverseMap")
            {
                break;
            }

            if (methodName == "ForMember")
            {
                forMemberCalls.Add(invocation);
            }

            currentNode = invocation.Parent;
        }

        return forMemberCalls;
    }

    private static bool IsAutoMapperCreateMapInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        return MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "CreateMap");
    }
}

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
        InvocationExpressionSyntax mappingInvocation =
            isReverseMap && reverseMapInvocation != null ? reverseMapInvocation : invocation;

        List<IPropertySymbol> unmapped = GetUnmappedDestinationProperties(
            mappingInvocation,
            sourceType,
            destinationType,
            context.SemanticModel,
            stopAtReverseMapBoundary: !isReverseMap);

        foreach (IPropertySymbol destProperty in unmapped)
        {
            ImmutableDictionary<string, string?>.Builder properties =
                ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("PropertyName", destProperty.Name);
            properties.Add("PropertyType", destProperty.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            properties.Add("DestinationTypeName", destinationType.Name);
            properties.Add("SourceTypeName", sourceType.Name);
            properties.Add("MappingInvocationStart", mappingInvocation.SpanStart.ToString());
            properties.Add("MappingInvocationLength", mappingInvocation.Span.Length.ToString());

            var diagnostic = Diagnostic.Create(
                UnmappedDestinationPropertyRule,
                GetPropertyLocation(destProperty) ?? mappingInvocation.GetLocation(),
                properties.ToImmutable(),
                destProperty.Name,
                sourceType.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    ///     Live unmapped non-required destination properties for a mapping direction.
    ///     Shared with the code fix so sibling recompute matches analyzer ownership.
    /// </summary>
    internal static List<IPropertySymbol> GetUnmappedDestinationProperties(
        InvocationExpressionSyntax mappingInvocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        if (HasCustomConversion(mappingInvocation, semanticModel, stopAtReverseMapBoundary))
        {
            return [];
        }

        List<IPropertySymbol> sourcePropertiesList =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false).ToList();
        var sourcePropertyNames = new HashSet<string>(
            sourcePropertiesList.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);

        var unmapped = new List<IPropertySymbol>();
        foreach (IPropertySymbol destProperty in AutoMapperAnalysisHelpers.GetMappableProperties(
                     destinationType, requireGetter: false, requireSetter: true))
        {
            if (destProperty.IsRequired)
            {
                continue;
            }

            if (sourcePropertyNames.Contains(destProperty.Name))
            {
                continue;
            }

            if (sourcePropertiesList.Any(srcProp => IsFlatteningMatch(srcProp, destProperty)))
            {
                continue;
            }

            if (IsPropertyConfiguredWithForMember(
                    mappingInvocation,
                    destProperty.Name,
                    semanticModel,
                    stopAtReverseMapBoundary))
            {
                continue;
            }

            if (IsDestinationPropertyInitializedByConstructUsing(
                    mappingInvocation,
                    destProperty.Name,
                    semanticModel,
                    stopAtReverseMapBoundary))
            {
                continue;
            }

            unmapped.Add(destProperty);
        }

        return unmapped;
    }

    private static bool HasCustomConversion(
        InvocationExpressionSyntax mappingInvocation,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        foreach (InvocationExpressionSyntax chainedInvocation in MappingChainAnalysisHelper.GetScopedChainInvocations(
                     mappingInvocation,
                     semanticModel,
                     stopAtReverseMapBoundary))
        {
            if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ConvertUsing"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDestinationPropertyInitializedByConstructUsing(
        InvocationExpressionSyntax mappingInvocation,
        string propertyName,
        SemanticModel semanticModel,
        bool stopAtReverseMapBoundary)
    {
        bool? effectiveConstructUsingInitializesProperty = null;

        foreach (InvocationExpressionSyntax chainedInvocation in MappingChainAnalysisHelper.GetScopedChainInvocations(
                     mappingInvocation,
                     semanticModel,
                     stopAtReverseMapBoundary))
        {
            if (!MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(chainedInvocation, semanticModel, "ConstructUsing") ||
                chainedInvocation.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            IReadOnlyList<BaseObjectCreationExpressionSyntax> objectCreations =
                GetConstructUsingObjectCreations(chainedInvocation.ArgumentList.Arguments[0].Expression);
            if (objectCreations.Count == 0)
            {
                effectiveConstructUsingInitializesProperty = false;
                continue;
            }

            effectiveConstructUsingInitializesProperty = objectCreations.All(objectCreation =>
                objectCreation.Initializer != null &&
                objectCreation.Initializer.Expressions
                    .OfType<AssignmentExpressionSyntax>()
                    .Any(assignment => AssignmentTargetsProperty(assignment.Left, propertyName)));
        }

        return effectiveConstructUsingInitializesProperty == true;
    }

    private static IReadOnlyList<BaseObjectCreationExpressionSyntax> GetConstructUsingObjectCreations(ExpressionSyntax constructExpression)
    {
        return constructExpression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => GetObjectCreationFromLambdaBody(simpleLambda.Body),
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda =>
                GetObjectCreationFromLambdaBody(parenthesizedLambda.Body),
            BaseObjectCreationExpressionSyntax creation => [creation],
            _ => []
        };
    }

    private static IReadOnlyList<BaseObjectCreationExpressionSyntax> GetObjectCreationFromLambdaBody(CSharpSyntaxNode lambdaBody)
    {
        if (lambdaBody is BaseObjectCreationExpressionSyntax creation)
        {
            return [creation];
        }

        if (lambdaBody is not BlockSyntax block)
        {
            return [];
        }

        ReturnStatementSyntax[] returnStatements = block
            .DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .ToArray();
        if (returnStatements.Length == 0)
        {
            return [];
        }

        var objectCreations = new List<BaseObjectCreationExpressionSyntax>();
        foreach (ReturnStatementSyntax returnStatement in returnStatements)
        {
            if (returnStatement.Expression is not BaseObjectCreationExpressionSyntax returnedCreation)
            {
                return [];
            }

            objectCreations.Add(returnedCreation);
        }

        return objectCreations;
    }

    private static bool AssignmentTargetsProperty(ExpressionSyntax assignmentTarget, string propertyName)
    {
        string? assignedPropertyName = assignmentTarget switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => null
        };

        return string.Equals(assignedPropertyName, propertyName, StringComparison.OrdinalIgnoreCase);
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
        IEnumerable<InvocationExpressionSyntax> mappingCalls =
            GetScopedDestinationConfigurationCalls(createMapInvocation, stopAtReverseMapBoundary);

        foreach (InvocationExpressionSyntax mappingCall in mappingCalls)
        {
            if (!MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(mappingCall, semanticModel, "ForMember") &&
                !MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(mappingCall, semanticModel, "ForPath"))
            {
                continue;
            }

            if (mappingCall.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            ArgumentSyntax destinationArgument = mappingCall.ArgumentList.Arguments[0];
            string? selectedMember = AM020MappingConfigurationHelpers.GetSelectedTopLevelMemberNameWithSemanticModel(
                destinationArgument.Expression,
                semanticModel);
            if (selectedMember == null)
            {
                continue;
            }

            if (string.Equals(selectedMember, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<InvocationExpressionSyntax> GetScopedDestinationConfigurationCalls(
        InvocationExpressionSyntax mappingInvocation,
        bool stopAtReverseMapBoundary)
    {
        var mappingCalls = new List<InvocationExpressionSyntax>();
        SyntaxNode? currentNode = mappingInvocation.Parent;

        while (currentNode is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax invocation)
        {
            string methodName = memberAccess.Name.Identifier.Text;
            if (stopAtReverseMapBoundary && methodName == "ReverseMap")
            {
                break;
            }

            if (methodName is "ForMember" or "ForPath")
            {
                mappingCalls.Add(invocation);
            }

            currentNode = invocation.Parent;
        }

        return mappingCalls;
    }

    private static bool IsAutoMapperCreateMapInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        return MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "CreateMap");
    }

    private static Location? GetPropertyLocation(IPropertySymbol property)
    {
        foreach (SyntaxReference syntaxReference in property.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is PropertyDeclarationSyntax propertyDeclaration)
            {
                return propertyDeclaration.Identifier.GetLocation();
            }

            if (syntaxReference.GetSyntax() is ParameterSyntax parameter)
            {
                return parameter.Identifier.GetLocation();
            }
        }

        return null;
    }
}

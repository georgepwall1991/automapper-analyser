using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.DataIntegrity;

/// <summary>
///     Analyzer for detecting case sensitivity mismatches between source and destination properties in AutoMapper
///     configurations
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM005_CaseSensitivityMismatchAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     AM005: Case sensitivity mismatch - properties differ only in casing
    /// </summary>
    public static readonly DiagnosticDescriptor CaseSensitivityMismatchRule = new(
        "AM005",
        "Property names differ only in casing",
        "Property '{0}' in source differs only in casing from destination property '{1}' - consider explicit mapping or case-insensitive configuration",
        "AutoMapper.PropertyMapping",
        DiagnosticSeverity.Warning,
        true,
        "Properties that differ only in casing may cause mapping issues depending on AutoMapper configuration. " +
        "Consider using explicit mapping or configure case-insensitive property matching.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [CaseSensitivityMismatchRule];

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

        // Ensure strict AutoMapper semantic matching to avoid lookalike false positives.
        if (!MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocationExpr, context.SemanticModel, "CreateMap"))
        {
            return;
        }

        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) typeArguments =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (typeArguments.sourceType == null || typeArguments.destinationType == null)
        {
            return;
        }

        var reportedMismatches = new HashSet<string>(StringComparer.Ordinal);

        // Analyze case sensitivity mismatches between source and destination properties
        AnalyzeCaseSensitivityMismatches(
            context,
            invocationExpr,
            typeArguments.sourceType,
            typeArguments.destinationType,
            false,
            null,
            reportedMismatches
        );

        InvocationExpressionSyntax? reverseMapInvocation =
            AutoMapperAnalysisHelpers.GetReverseMapInvocation(invocationExpr);
        if (reverseMapInvocation != null)
        {
            AnalyzeCaseSensitivityMismatches(
                context,
                invocationExpr,
                typeArguments.destinationType,
                typeArguments.sourceType,
                true,
                reverseMapInvocation,
                reportedMismatches
            );
        }
    }


    private static void AnalyzeCaseSensitivityMismatches(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        bool isReverseMap,
        InvocationExpressionSyntax? reverseMapInvocation,
        HashSet<string> reportedMismatches)
    {
        if (HasCustomConstructionOrConversion(invocation, isReverseMap, reverseMapInvocation))
        {
            return;
        }

        IEnumerable<IPropertySymbol> sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        IEnumerable<IPropertySymbol> destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, false);

        // Check each source property for case sensitivity mismatches
        foreach (IPropertySymbol sourceProperty in sourceProperties)
        {
            // Find destination property with same name but different case
            IPropertySymbol? exactMatch = destinationProperties
                .FirstOrDefault(p => string.Equals(p.Name, sourceProperty.Name, StringComparison.Ordinal));

            if (exactMatch != null)
            {
                continue; // Exact match, no case sensitivity issue
            }

            // Find case-insensitive match
            IPropertySymbol? caseInsensitiveMatch = destinationProperties
                .FirstOrDefault(p => string.Equals(p.Name, sourceProperty.Name, StringComparison.OrdinalIgnoreCase));

            if (caseInsensitiveMatch == null)
            {
                continue; // No match at all - this would be handled by AM004 (missing destination property)
            }

            // Check if types are compatible (only report case sensitivity if types match or are compatible)
            if (!AutoMapperAnalysisHelpers.AreTypesCompatible(sourceProperty.Type, caseInsensitiveMatch.Type))
            {
                continue; // Type mismatch - this would be handled by AM001 (type mismatch)
            }

            // Check if explicit mapping is configured for this property
            if (IsDestinationPropertyConfiguredWithForMember(invocation, caseInsensitiveMatch.Name, isReverseMap,
                    reverseMapInvocation))
            {
                continue; // Explicit mapping handles the case sensitivity issue
            }

            // Check if source property is explicitly ignored
            if (IsSourcePropertyExplicitlyIgnored(invocation, sourceProperty.Name, isReverseMap, reverseMapInvocation))
            {
                continue;
            }

            string mismatchKey =
                CreateMismatchKey(sourceType, destinationType, sourceProperty.Name, caseInsensitiveMatch.Name);
            if (!reportedMismatches.Add(mismatchKey))
            {
                continue;
            }

            // Report diagnostic for case sensitivity mismatch
            ImmutableDictionary<string, string?>.Builder properties =
                ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("SourcePropertyName", sourceProperty.Name);
            properties.Add("DestinationPropertyName", caseInsensitiveMatch.Name);
            properties.Add("PropertyType", sourceProperty.Type.ToDisplayString());
            properties.Add("SourceTypeName", AutoMapperAnalysisHelpers.GetTypeName(sourceType));
            properties.Add("DestinationTypeName", AutoMapperAnalysisHelpers.GetTypeName(destinationType));

            InvocationExpressionSyntax locationNode =
                isReverseMap && reverseMapInvocation != null ? reverseMapInvocation : invocation;

            var diagnostic = Diagnostic.Create(
                CaseSensitivityMismatchRule,
                locationNode.GetLocation(),
                properties.ToImmutable(),
                sourceProperty.Name,
                caseInsensitiveMatch.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static string CreateMismatchKey(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        string sourcePropertyName,
        string destinationPropertyName)
    {
        string[] typeNames = [sourceType.ToDisplayString(), destinationType.ToDisplayString()];
        Array.Sort(typeNames, StringComparer.Ordinal);

        string[] propertyNames = [sourcePropertyName.ToUpperInvariant(), destinationPropertyName.ToUpperInvariant()];
        Array.Sort(propertyNames, StringComparer.Ordinal);

        return $"{typeNames[0]}|{typeNames[1]}::{propertyNames[0]}|{propertyNames[1]}";
    }

    private static bool IsDestinationPropertyConfiguredWithForMember(
        InvocationExpressionSyntax createMapInvocation,
        string destinationPropertyName,
        bool isReverseMap,
        InvocationExpressionSyntax? reverseMapInvocation)
    {
        IEnumerable<InvocationExpressionSyntax> forMemberCalls =
            AutoMapperAnalysisHelpers.GetForMemberCalls(createMapInvocation);

        foreach (InvocationExpressionSyntax forMember in forMemberCalls)
        {
            if (!AppliesToDirection(forMember, isReverseMap, reverseMapInvocation))
            {
                continue;
            }

            if (IsForMemberOfProperty(forMember, destinationPropertyName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsForMemberOfProperty(InvocationExpressionSyntax forMemberInvocation, string propertyName)
    {
        if (forMemberInvocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        string? selectedMember = GetSelectedMemberName(forMemberInvocation.ArgumentList.Arguments[0].Expression);
        return string.Equals(selectedMember, propertyName, StringComparison.Ordinal);
    }

    private static bool IsSourcePropertyExplicitlyIgnored(
        InvocationExpressionSyntax createMapInvocation,
        string sourcePropertyName,
        bool isReverseMap,
        InvocationExpressionSyntax? reverseMapInvocation)
    {
        SyntaxNode? parent = createMapInvocation.Parent;

        while (parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if (memberAccess.Name.Identifier.ValueText == "ForSourceMember" &&
                AppliesToDirection(chainedInvocation, isReverseMap, reverseMapInvocation) &&
                IsForSourceMemberOfProperty(chainedInvocation, sourcePropertyName) &&
                HasDoNotValidateCall(chainedInvocation))
            {
                return true;
            }

            parent = chainedInvocation.Parent;
        }

        return false;
    }

    private static bool IsForSourceMemberOfProperty(InvocationExpressionSyntax forSourceMemberInvocation,
        string propertyName)
    {
        if (forSourceMemberInvocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        string? selectedMember = GetSelectedMemberName(forSourceMemberInvocation.ArgumentList.Arguments[0].Expression);
        return string.Equals(selectedMember, propertyName, StringComparison.Ordinal);
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

    private static bool HasDoNotValidateCall(InvocationExpressionSyntax forSourceMemberInvocation)
    {
        if (forSourceMemberInvocation.ArgumentList.Arguments.Count < 2)
        {
            return false;
        }

        ExpressionSyntax optionsArg = forSourceMemberInvocation.ArgumentList.Arguments[1].Expression;
        return optionsArg.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.ValueText == "DoNotValidate");
    }

    private static bool HasCustomConstructionOrConversion(
        InvocationExpressionSyntax createMapInvocation,
        bool isReverseMap,
        InvocationExpressionSyntax? reverseMapInvocation)
    {
        SyntaxNode? parent = createMapInvocation.Parent;

        while (parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if ((memberAccess.Name.Identifier.ValueText is "ConstructUsing" or "ConvertUsing") &&
                AppliesToDirection(chainedInvocation, isReverseMap, reverseMapInvocation))
            {
                return true;
            }

            parent = chainedInvocation.Parent;
        }

        return false;
    }

    private static bool AppliesToDirection(
        InvocationExpressionSyntax mappingMethod,
        bool isReverseMap,
        InvocationExpressionSyntax? reverseMapInvocation)
    {
        if (reverseMapInvocation == null)
        {
            return !isReverseMap;
        }

        bool isAncestor = reverseMapInvocation.Ancestors().Contains(mappingMethod);
        return isReverseMap ? isAncestor : !isAncestor;
    }
}

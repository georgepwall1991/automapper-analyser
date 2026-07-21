using System.Collections.Immutable;
using System.Globalization;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.TypeSafety;

/// <summary>
///     Analyzer that detects enum-to-enum property mappings where source and destination enum
///     members do not align by name and numeric value. AutoMapper maps enums by numeric value by
///     default, so misaligned enums silently produce wrong data instead of failing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM061_EnumMemberMismatchAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     Diagnostic descriptor for enum member misalignment in a configured mapping.
    /// </summary>
    public static readonly DiagnosticDescriptor EnumMemberMismatchRule = new(
        "AM061",
        "Enum member mismatch in mapping",
        "Enum property '{0}' maps '{1}' to '{2}' by numeric value, but source member '{3}' (value {4}) has no same-named destination member with that value",
        "AutoMapper.TypeSafety",
        DiagnosticSeverity.Warning,
        true,
        "AutoMapper maps enums by numeric value by default. When a source enum member's numeric value "
        + "corresponds to a differently named (or nonexistent) destination member, the mapping silently "
        + "produces wrong data. Add an explicit name-based conversion, align the enum definitions, or "
        + "ignore the member deliberately.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [EnumMemberMismatchRule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            CreateMapRegistry registry = CreateMapRegistry.FromCompilation(compilationContext.Compilation);
            if (registry.IsEmpty)
            {
                return;
            }

            // Key registrations by their invocation node so the syntax node action only does work
            // on CreateMap/ReverseMap call sites (the AM041 pattern).
            var mappingsByNode = new Dictionary<InvocationExpressionSyntax, List<CreateMapRegistry.MappingInfo>>();
            foreach (CreateMapRegistry.MappingInfo mapping in registry.AllMappings)
            {
                if (!mappingsByNode.TryGetValue(mapping.Node, out List<CreateMapRegistry.MappingInfo>? mappings))
                {
                    mappings = new List<CreateMapRegistry.MappingInfo>();
                    mappingsByNode[mapping.Node] = mappings;
                }

                mappings.Add(mapping);
            }

            compilationContext.RegisterSyntaxNodeAction(ctx =>
            {
                var invocation = (InvocationExpressionSyntax)ctx.Node;
                if (!mappingsByNode.TryGetValue(invocation, out List<CreateMapRegistry.MappingInfo>? mappings))
                {
                    return;
                }

                foreach (CreateMapRegistry.MappingInfo mapping in mappings)
                {
                    AnalyzeMapping(ctx.ReportDiagnostic, registry, mapping);
                }
            }, SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeMapping(
        Action<Diagnostic> reportDiagnostic,
        CreateMapRegistry registry,
        CreateMapRegistry.MappingInfo mapping)
    {
        ITypeSymbol source = mapping.Source;
        ITypeSymbol destination = mapping.Destination;

        if (source.TypeKind == TypeKind.Error || destination.TypeKind == TypeKind.Error)
        {
            return;
        }

        if (ContainsTypeParameter(source) || ContainsTypeParameter(destination))
        {
            return;
        }

        // Deferred configuration (var map = CreateMap<S, D>(); map.ForMember(...);) is not visible
        // from the fluent chain, so the full configuration cannot be proven — fail closed. This
        // includes deferred ReverseMap() calls whose receiver is a local mapping expression.
        if (mapping.Node.Ancestors().OfType<VariableDeclaratorSyntax>().Any() ||
            (mapping.IsReverseMap && ReceiverIsLocalMappingExpression(mapping.Node, mapping.SemanticModel)))
        {
            return;
        }

        SemanticModel semanticModel = mapping.SemanticModel;

        // Forward registrations own the chain segment before ReverseMap(); reverse-generated
        // registrations own the segment after it.
        List<InvocationExpressionSyntax> scopedCalls = MappingChainAnalysisHelper
            .GetScopedChainInvocations(mapping.Node, semanticModel, stopAtReverseMapBoundary: !mapping.IsReverseMap)
            .ToList();

        if (scopedCalls.Any(call => IsConverterCall(call, semanticModel)))
        {
            return;
        }

        var ignoredMembers = new HashSet<string>(StringComparer.Ordinal);
        var customConfiguredMembers = new HashSet<string>(StringComparer.Ordinal);
        var explicitPairs = new List<(string DestinationName, string SourceName, InvocationExpressionSyntax? ForMemberCall)>();

        CollectMemberConfiguration(scopedCalls, semanticModel, ignoredMembers, customConfiguredMembers, explicitPairs);

        if (mapping.IsReverseMap)
        {
            // AutoMapper reverses proven direct forward MapFrom paths, so the reverse direction
            // inherits them inverted unless the reverse-scoped configuration overrides the member.
            // Non-trivial forward expressions are not reversible and silence the member instead.
            foreach ((string forwardDest, string forwardSource, InvocationExpressionSyntax? _) in
                     GetForwardDirectPairs(mapping, semanticModel))
            {
                if (ignoredMembers.Contains(forwardSource) ||
                    explicitPairs.Any(pair => pair.DestinationName == forwardSource))
                {
                    continue;
                }

                if (customConfiguredMembers.Contains(forwardSource))
                {
                    continue;
                }

                explicitPairs.Add((forwardSource, forwardDest, null));
            }
        }

        var reportedPairs = new HashSet<(string DestinationName, string SourceMember, string Value)>();

        Dictionary<string, IPropertySymbol> sourcePropertiesByName =
            AutoMapperAnalysisHelpers.GetMappableProperties(source, requireSetter: false)
                .GroupBy(property => property.Name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        Dictionary<string, IPropertySymbol> destinationPropertiesByName =
            AutoMapperAnalysisHelpers.GetMappableProperties(destination)
                .GroupBy(property => property.Name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (IPropertySymbol destinationProperty in destinationPropertiesByName.Values)
        {
            if (ignoredMembers.Contains(destinationProperty.Name) ||
                customConfiguredMembers.Contains(destinationProperty.Name) ||
                explicitPairs.Any(pair => pair.DestinationName == destinationProperty.Name))
            {
                continue;
            }

            if (!sourcePropertiesByName.TryGetValue(destinationProperty.Name, out IPropertySymbol? sourceProperty))
            {
                continue;
            }

            ReportMisalignedEnumMembers(
                reportDiagnostic,
                registry,
                mapping,
                destinationProperty.Name,
                sourceProperty.Name,
                sourceProperty.Type,
                destinationProperty.Type,
                forMemberCall: null,
                reportedPairs);
        }

        foreach ((string destinationName, string sourceName, InvocationExpressionSyntax? forMemberCall) in explicitPairs)
        {
            if (ignoredMembers.Contains(destinationName) || customConfiguredMembers.Contains(destinationName))
            {
                continue;
            }

            ITypeSymbol? sourceMemberType = ResolveMemberType(source, sourceName, sourcePropertiesByName);
            ITypeSymbol? destinationMemberType = ResolveMemberType(destination, destinationName, destinationPropertiesByName);
            if (sourceMemberType == null || destinationMemberType == null)
            {
                continue;
            }

            ReportMisalignedEnumMembers(
                reportDiagnostic,
                registry,
                mapping,
                destinationName,
                sourceName,
                sourceMemberType,
                destinationMemberType,
                forMemberCall,
                reportedPairs);
        }
    }

    private static void CollectMemberConfiguration(
        List<InvocationExpressionSyntax> scopedCalls,
        SemanticModel semanticModel,
        HashSet<string> ignoredMembers,
        HashSet<string> customConfiguredMembers,
        List<(string DestinationName, string SourceName, InvocationExpressionSyntax? ForMemberCall)> explicitPairs)
    {
        foreach (InvocationExpressionSyntax call in scopedCalls)
        {
            bool isMemberConfiguration =
                MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(call, semanticModel, "ForMember") ||
                MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(call, semanticModel, "ForPath");
            if (!isMemberConfiguration)
            {
                continue;
            }

            string? destinationMember = ExtractDestinationMemberName(call);
            if (destinationMember == null)
            {
                // Nested ForPath(d => d.Nested.Member, ...) and unresolvable selectors cannot be
                // attributed to a top-level property; leave convention analysis untouched.
                continue;
            }

            if (HasOptionsInvocation(call, semanticModel, "Ignore"))
            {
                ignoredMembers.Add(destinationMember);
                continue;
            }

            if (HasOptionsInvocation(call, semanticModel, "ConvertUsing"))
            {
                customConfiguredMembers.Add(destinationMember);
                continue;
            }

            if (!HasOptionsInvocation(call, semanticModel, "MapFrom"))
            {
                // Condition/NullSubstitute-style policy keeps the convention source member.
                continue;
            }

            if (TryGetDirectMapFromSourceMember(call, semanticModel, out string? sourceMember))
            {
                explicitPairs.Add((destinationMember, sourceMember!, call));
            }
            else
            {
                customConfiguredMembers.Add(destinationMember);
            }
        }
    }

    /// <summary>
    ///     Extracts direct member pairs from the forward chain that roots a reverse registration,
    ///     so the reverse direction can inherit them inverted.
    /// </summary>
    private static List<(string DestinationName, string SourceName, InvocationExpressionSyntax? ForMemberCall)>
        GetForwardDirectPairs(CreateMapRegistry.MappingInfo reverseMapping, SemanticModel semanticModel)
    {
        InvocationExpressionSyntax? createMap = FindReceiverCreateMap(reverseMapping.Node, semanticModel);
        if (createMap == null)
        {
            return new List<(string, string, InvocationExpressionSyntax?)>();
        }

        List<InvocationExpressionSyntax> forwardCalls = MappingChainAnalysisHelper
            .GetScopedChainInvocations(createMap, semanticModel, stopAtReverseMapBoundary: true)
            .ToList();

        var ignored = new HashSet<string>(StringComparer.Ordinal);
        var custom = new HashSet<string>(StringComparer.Ordinal);
        var pairs = new List<(string DestinationName, string SourceName, InvocationExpressionSyntax? ForMemberCall)>();
        CollectMemberConfiguration(forwardCalls, semanticModel, ignored, custom, pairs);
        return pairs;
    }

    /// <summary>
    ///     True when a ReverseMap() call's receiver is a local variable initialized with a mapping
    ///     expression (var map = CreateMap&lt;S,D&gt;(); ... map.ReverseMap();), whose deferred
    ///     configuration cannot be proven from the fluent chain.
    /// </summary>
    private static bool ReceiverIsLocalMappingExpression(
        InvocationExpressionSyntax reverseMapInvocation,
        SemanticModel semanticModel)
    {
        if (reverseMapInvocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            memberAccess.Expression is not IdentifierNameSyntax receiver ||
            semanticModel.GetSymbolInfo(receiver).Symbol is not ILocalSymbol localSymbol)
        {
            return false;
        }

        foreach (SyntaxReference syntaxReference in localSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is VariableDeclaratorSyntax declarator &&
                declarator.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Any(invocation => MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                        invocation,
                        semanticModel,
                        "CreateMap")))
            {
                return true;
            }
        }

        return false;
    }

    private static InvocationExpressionSyntax? FindReceiverCreateMap(
        InvocationExpressionSyntax reverseMapInvocation,
        SemanticModel semanticModel)
    {
        ExpressionSyntax? current = (reverseMapInvocation.Expression as MemberAccessExpressionSyntax)?.Expression;
        while (current is ParenthesizedExpressionSyntax parenthesized)
        {
            current = parenthesized.Expression;
        }

        while (current is InvocationExpressionSyntax invocation)
        {
            if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "CreateMap"))
            {
                return invocation;
            }

            current = (invocation.Expression as MemberAccessExpressionSyntax)?.Expression;
            while (current is ParenthesizedExpressionSyntax innerParenthesized)
            {
                current = innerParenthesized.Expression;
            }
        }

        return null;
    }

    private static void ReportMisalignedEnumMembers(
        Action<Diagnostic> reportDiagnostic,
        CreateMapRegistry registry,
        CreateMapRegistry.MappingInfo mapping,
        string destinationPropertyName,
        string sourcePropertyName,
        ITypeSymbol sourceMemberType,
        ITypeSymbol destinationMemberType,
        InvocationExpressionSyntax? forMemberCall,
        HashSet<(string DestinationName, string SourceMember, string Value)> reportedPairs)
    {
        ITypeSymbol sourceEnum = AutoMapperAnalysisHelpers.GetUnderlyingType(sourceMemberType);
        ITypeSymbol destinationEnum = AutoMapperAnalysisHelpers.GetUnderlyingType(destinationMemberType);

        if (sourceEnum.TypeKind != TypeKind.Enum || destinationEnum.TypeKind != TypeKind.Enum)
        {
            return;
        }

        if (SymbolEqualityComparer.Default.Equals(sourceEnum, destinationEnum))
        {
            return;
        }

        // [Flags] enums legitimately combine values that never appear as named members.
        if (HasFlagsAttribute(sourceEnum) || HasFlagsAttribute(destinationEnum))
        {
            return;
        }

        // A dedicated enum-pair registration with a converter owns the conversion globally
        // (including ConvertUsingEnumMapping from the enum-mapping extension package).
        if (HasDedicatedEnumConverter(registry, sourceEnum, destinationEnum))
        {
            return;
        }

        // Constant values are normalized to decimal so enums with different underlying types
        // (byte vs int, etc.) compare by numeric value, and every alias sharing a value is kept
        // so identical aliased enums stay aligned.
        var destinationMembersByValue = new Dictionary<decimal, HashSet<string>>();
        var destinationMemberNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (IFieldSymbol field in GetEnumFields(destinationEnum))
        {
            destinationMemberNames.Add(field.Name);
            decimal value = NormalizeEnumValue(field.ConstantValue!);
            if (!destinationMembersByValue.TryGetValue(value, out HashSet<string>? names))
            {
                names = new HashSet<string>(StringComparer.Ordinal);
                destinationMembersByValue[value] = names;
            }

            names.Add(field.Name);
        }

        string sourceEnumName = AutoMapperAnalysisHelpers.GetTypeNameWithoutNullability(sourceEnum);
        string destinationEnumName = AutoMapperAnalysisHelpers.GetTypeNameWithoutNullability(destinationEnum);
        string destinationEnumDisplayName =
            destinationEnum.ToMinimalDisplayString(mapping.SemanticModel, mapping.Node.SpanStart);

        // The name-based Enum.Parse fix is only safe when every possible source member name
        // exists in the destination enum, the source cannot be null (null would parse ""), and
        // the source enum has no duplicate-valued aliases (ToString() picks an unspecified alias
        // at runtime, so name-based mapping would be non-deterministic).
        IFieldSymbol[] sourceFields = GetEnumFields(sourceEnum).ToArray();
        bool mapByNameSafe = !IsNullableValueType(sourceMemberType) &&
                             sourceFields.All(field => destinationMemberNames.Contains(field.Name)) &&
                             sourceFields
                                 .GroupBy(field => NormalizeEnumValue(field.ConstantValue!))
                                 .All(group => group.Count() == 1);

        foreach (IFieldSymbol sourceField in GetEnumFields(sourceEnum))
        {
            if (sourceField.ConstantValue == null)
            {
                continue;
            }

            bool aligned = destinationMembersByValue.TryGetValue(
                NormalizeEnumValue(sourceField.ConstantValue),
                out HashSet<string>? destinationMembers) &&
                destinationMembers.Contains(sourceField.Name);
            if (aligned)
            {
                continue;
            }

            string valueText = Convert.ToString(sourceField.ConstantValue, CultureInfo.InvariantCulture) ?? "?";
            if (!reportedPairs.Add((destinationPropertyName, sourceField.Name, valueText)))
            {
                continue;
            }

            ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
                .Add("PropertyName", destinationPropertyName)
                .Add("SourcePropertyName", sourcePropertyName)
                .Add("DestinationEnumName", destinationEnumDisplayName)
                .Add("MapByNameSafe", mapByNameSafe ? "true" : "false");

            if (forMemberCall != null)
            {
                properties = properties
                    .Add("MappingInvocationStart", forMemberCall.Span.Start.ToString(CultureInfo.InvariantCulture))
                    .Add("MappingInvocationLength", forMemberCall.Span.Length.ToString(CultureInfo.InvariantCulture));
            }

            reportDiagnostic(Diagnostic.Create(
                EnumMemberMismatchRule,
                mapping.Location,
                properties,
                destinationPropertyName,
                sourceEnumName,
                destinationEnumName,
                sourceField.Name,
                valueText));
        }
    }

    private static bool HasDedicatedEnumConverter(
        CreateMapRegistry registry,
        ITypeSymbol sourceEnum,
        ITypeSymbol destinationEnum)
    {
        foreach (CreateMapRegistry.MappingInfo mapping in registry.AllMappings)
        {
            if (!SymbolEqualityComparer.Default.Equals(mapping.Source, sourceEnum) ||
                !SymbolEqualityComparer.Default.Equals(mapping.Destination, destinationEnum))
            {
                continue;
            }

            // Deferred configuration on a local enum-pair map (var enumMap = CreateMap<SE, DE>();
            // enumMap.ConvertUsing(...);) is invisible from the stored registration's chain; assume
            // the conversion may be owned rather than risk a false positive.
            if (mapping.Node.Ancestors().OfType<VariableDeclaratorSyntax>().Any())
            {
                return true;
            }

            List<InvocationExpressionSyntax> scopedCalls = MappingChainAnalysisHelper
                .GetScopedChainInvocations(
                    mapping.Node,
                    mapping.SemanticModel,
                    stopAtReverseMapBoundary: !mapping.IsReverseMap)
                .ToList();

            if (scopedCalls.Any(call => IsConverterCall(call, mapping.SemanticModel)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsConverterCall(InvocationExpressionSyntax call, SemanticModel semanticModel)
    {
        return MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(call, semanticModel, "ConvertUsing") ||
               MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                   call,
                   semanticModel,
                   "ConvertUsingEnumMapping");
    }

    private static bool IsNullableValueType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
               namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
    }

    private static IEnumerable<IFieldSymbol> GetEnumFields(ITypeSymbol enumType)
    {
        return enumType.GetMembers().OfType<IFieldSymbol>().Where(field => field.HasConstantValue);
    }

    private static decimal NormalizeEnumValue(object constantValue)
    {
        // Decimal represents every integral enum underlying type (sbyte..ulong) exactly, so
        // cross-underlying-type comparisons work by numeric value rather than boxed equality.
        return Convert.ToDecimal(constantValue, CultureInfo.InvariantCulture);
    }

    private static bool HasFlagsAttribute(ITypeSymbol type)
    {
        return type.GetAttributes().Any(attribute =>
            attribute.AttributeClass is { Name: "FlagsAttribute" } attributeClass &&
            attributeClass.ContainingNamespace?.ToDisplayString() == "System");
    }

    private static ITypeSymbol? ResolveMemberType(
        ITypeSymbol type,
        string memberName,
        Dictionary<string, IPropertySymbol> propertiesByName)
    {
        if (propertiesByName.TryGetValue(memberName, out IPropertySymbol? property))
        {
            return property.Type;
        }

        // Fields are not convention-mapped but explicit MapFrom can target them; walk the
        // inheritance chain explicitly so inherited fields resolve like inherited properties.
        for (ITypeSymbol? current = type; current != null; current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers(memberName))
            {
                if (member is IFieldSymbol { IsStatic: false } field)
                {
                    return field.Type;
                }
            }
        }

        return null;
    }

    private static string? ExtractDestinationMemberName(InvocationExpressionSyntax memberConfigurationCall)
    {
        if (memberConfigurationCall.ArgumentList.Arguments.Count == 0)
        {
            return null;
        }

        ExpressionSyntax argument = memberConfigurationCall.ArgumentList.Arguments[0].Expression;
        while (argument is ParenthesizedExpressionSyntax parenthesized)
        {
            argument = parenthesized.Expression;
        }

        switch (argument)
        {
            case SimpleLambdaExpressionSyntax simpleLambda:
                return GetDirectMemberName(simpleLambda.ExpressionBody, simpleLambda.Parameter.Identifier.Text);
            case ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesizedLambda:
                return GetDirectMemberName(
                    parenthesizedLambda.ExpressionBody,
                    parenthesizedLambda.ParameterList.Parameters[0].Identifier.Text);
            case LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal:
                return literal.Token.ValueText;
            case InvocationExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.Text: "nameof" },
                ArgumentList.Arguments.Count: 1
            } nameofCall:
                return nameofCall.ArgumentList.Arguments[0].Expression switch
                {
                    MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
                    IdentifierNameSyntax identifier => identifier.Identifier.Text,
                    _ => null
                };
            default:
                return null;
        }
    }

    /// <summary>
    ///     Returns the member name only for a direct <c>param =&gt; param.Member</c> selector. Nested
    ///     paths such as <c>dest =&gt; dest.Nested.Member</c> return null so they are never confused
    ///     with a top-level property of the same leaf name.
    /// </summary>
    private static string? GetDirectMemberName(ExpressionSyntax? body, string parameterName)
    {
        while (body is ParenthesizedExpressionSyntax parenthesized)
        {
            body = parenthesized.Expression;
        }

        return body is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax receiver
        } memberAccess && receiver.Identifier.Text == parameterName
            ? memberAccess.Name.Identifier.Text
            : null;
    }

    private static bool HasOptionsInvocation(
        InvocationExpressionSyntax memberConfigurationCall,
        SemanticModel semanticModel,
        string optionsMethodName)
    {
        return memberConfigurationCall.ArgumentList.Arguments.Count >= 2 &&
               memberConfigurationCall.ArgumentList.Arguments[1].Expression
                   .DescendantNodesAndSelf()
                   .OfType<InvocationExpressionSyntax>()
                   .Any(invocation => MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                       invocation,
                       semanticModel,
                       optionsMethodName));
    }

    private static bool TryGetDirectMapFromSourceMember(
        InvocationExpressionSyntax memberConfigurationCall,
        SemanticModel semanticModel,
        out string? sourceMemberName)
    {
        sourceMemberName = null;

        if (memberConfigurationCall.ArgumentList.Arguments.Count < 2)
        {
            return false;
        }

        InvocationExpressionSyntax? mapFromCall = memberConfigurationCall.ArgumentList.Arguments[1].Expression
            .DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(invocation => MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                invocation,
                semanticModel,
                "MapFrom"));

        if (mapFromCall == null || mapFromCall.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        ExpressionSyntax mapFromArgument = mapFromCall.ArgumentList.Arguments[0].Expression;
        while (mapFromArgument is ParenthesizedExpressionSyntax parenthesized)
        {
            mapFromArgument = parenthesized.Expression;
        }

        if (mapFromArgument is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal)
        {
            sourceMemberName = literal.Token.ValueText;
            return true;
        }

        switch (mapFromArgument)
        {
            case SimpleLambdaExpressionSyntax simpleLambda:
                sourceMemberName = GetDirectMemberName(
                    simpleLambda.ExpressionBody,
                    simpleLambda.Parameter.Identifier.Text);
                return sourceMemberName != null;
            case ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesizedLambda:
                sourceMemberName = GetDirectMemberName(
                    parenthesizedLambda.ExpressionBody,
                    parenthesizedLambda.ParameterList.Parameters[0].Identifier.Text);
                return sourceMemberName != null;
            default:
                return false;
        }
    }

    private static bool ContainsTypeParameter(ITypeSymbol type)
    {
        return type.TypeKind == TypeKind.TypeParameter ||
               (type is INamedTypeSymbol namedType &&
                (namedType.IsUnboundGenericType || namedType.TypeArguments.Any(ContainsTypeParameter))) ||
               (type is IArrayTypeSymbol arrayType && ContainsTypeParameter(arrayType.ElementType));
    }
}

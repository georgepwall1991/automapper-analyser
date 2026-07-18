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
    private enum ReceiverStatus
    {
        None,
        Conditional,
        Definite
    }

    private readonly struct ReceiverMutation
    {
        public ReceiverMutation(int position, ISymbol symbol, ReceiverStatus status)
        {
            Position = position;
            Symbol = symbol;
            Status = status;
        }

        public int Position { get; }

        public ISymbol Symbol { get; }

        public ReceiverStatus Status { get; }
    }

    internal const string PropertyNamePropertyName = "PropertyName";
    internal const string SourcePropertyNamePropertyName = "SourcePropertyName";
    internal const string SourcePropertyTypePropertyName = "SourcePropertyType";
    internal const string DestinationPropertyTypePropertyName = "DestinationPropertyType";
    internal const string SourceMemberRequiresInvocationPropertyName = "SourceMemberRequiresInvocation";

    /// <summary>
    ///     Marker property set when the diagnostic is a collection-element nullability mismatch
    ///     (e.g. <c>List&lt;string?&gt;</c> → <c>List&lt;string&gt;</c>) rather than a top-level one. The
    ///     code-fix provider uses it to withhold the <c>?? default</c> scaffold (which cannot fix element
    ///     nullability) and offer only the manual-review ignore action.
    /// </summary>
    internal const string ElementNullabilityPropertyName = "ElementNullability";

    /// <summary>
    ///     AM002: Nullable to non-nullable assignment without proper handling.
    /// </summary>
    public static readonly DiagnosticDescriptor NullableToNonNullableRule = new(
        "AM002",
        "Nullable to non-nullable mapping issue in AutoMapper configuration",
        "Property '{0}' has nullable compatibility issue: {1}.{2} ({3}) can be null but {4}.{0} ({5}) is non-nullable",
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

        InvocationExpressionSyntax? reverseMapInvocation =
            AutoMapperAnalysisHelpers.GetReverseMapInvocation(invocationExpr);
        if (reverseMapInvocation != null)
        {
            AnalyzeNullablePropertyMappings(
                context,
                reverseMapInvocation,
                destinationType,
                sourceType
            );
        }
    }

    private static void AnalyzeNullablePropertyMappings(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        if (AM020MappingConfigurationHelpers.HasCustomConstructionOrConversion(invocation, context.SemanticModel))
        {
            return;
        }

        var destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, false).ToList();
        foreach (IPropertySymbol constructorOwnedProperty in GetConstructorOwnedDestinationProperties(
                     invocation,
                     sourceType,
                     destinationType,
                     context.SemanticModel))
        {
            if (!destinationProperties.Any(property =>
                    SymbolEqualityComparer.Default.Equals(property, constructorOwnedProperty)))
            {
                destinationProperties.Add(constructorOwnedProperty);
            }
        }

        var explicitlyHandledDestinationProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AnalyzeConfiguredConstructorParametersWithoutDestinationProperties(
            context,
            invocation,
            sourceType,
            destinationType,
            destinationProperties);

        foreach (IPropertySymbol destinationProperty in destinationProperties)
        {
            bool hasExplicitNullabilityConfiguration = TryGetExplicitNullabilityConfiguration(
                invocation,
                destinationProperty.Name,
                destinationProperty.Type,
                sourceType,
                destinationType,
                context.SemanticModel,
                    out bool configurationHandlesNullability,
                    out ITypeSymbol? explicitNullableSourceType,
                    out string? explicitNullableSourceName,
                    out ITypeSymbol? explicitDestinationType,
                    out bool explicitElementNullability);
            if (!hasExplicitNullabilityConfiguration)
            {
                continue;
            }

            if (configurationHandlesNullability)
            {
                explicitlyHandledDestinationProperties.Add(destinationProperty.Name);
                continue;
            }

            if (explicitNullableSourceType != null)
            {
                ReportNullableToNonNullableDiagnostic(
                    context,
                    invocation,
                    destinationProperty.Name,
                    explicitNullableSourceName ?? destinationProperty.Name,
                    explicitNullableSourceType,
                    sourceType,
                    destinationType,
                    explicitDestinationType ?? destinationProperty.Type,
                    isElementNullability: explicitElementNullability);
                explicitlyHandledDestinationProperties.Add(destinationProperty.Name);
            }
        }

        foreach (IPropertySymbol destinationProperty in destinationProperties)
        {
            if (explicitlyHandledDestinationProperties.Contains(destinationProperty.Name))
            {
                continue;
            }

            MappableSourceMember? sourceMember =
                AM020MappingConfigurationHelpers.GetMappableSourceMember(
                    sourceType,
                    destinationProperty.Name);
            if (sourceMember == null)
            {
                continue;
            }

            bool hasExplicitNullabilityConfiguration = TryGetExplicitNullabilityConfiguration(
                    invocation,
                    destinationProperty.Name,
                    destinationProperty.Type,
                    sourceType,
                    destinationType,
                    context.SemanticModel,
                    out bool configurationHandlesNullability,
                    out ITypeSymbol? explicitNullableSourceType,
                    out string? explicitNullableSourceName,
                    out ITypeSymbol? explicitDestinationType,
                    out bool explicitElementNullability);
            if (hasExplicitNullabilityConfiguration &&
                configurationHandlesNullability)
            {
                continue;
            }

            if (explicitNullableSourceType != null)
            {
                ReportNullableToNonNullableDiagnostic(
                    context,
                    invocation,
                    destinationProperty.Name,
                    explicitNullableSourceName ?? destinationProperty.Name,
                    explicitNullableSourceType,
                    sourceType,
                    destinationType,
                    explicitDestinationType ?? destinationProperty.Type,
                    isElementNullability: explicitElementNullability);
                continue;
            }

            if (hasExplicitNullabilityConfiguration &&
                !IsNullableToNonNullableCompatible(sourceMember.Value.Type, destinationProperty.Type))
            {
                continue;
            }

            AnalyzeNullableCompatibility(
                context,
                invocation,
                sourceMember.Value.SourceName,
                sourceMember.Value.Type,
                sourceMember.Value.RequiresInvocation,
                destinationProperty,
                sourceType,
                destinationType);
        }
    }

    private static bool TryGetExplicitNullabilityConfiguration(
        InvocationExpressionSyntax createMapInvocation,
        string destinationPropertyName,
        ITypeSymbol destinationPropertyType,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        SemanticModel semanticModel,
        out bool handlesNullability,
        out ITypeSymbol? explicitNullableSourceType,
        out string? explicitNullableSourceName,
        out ITypeSymbol? explicitDestinationType,
        out bool explicitElementNullability)
    {
        handlesNullability = false;
        explicitNullableSourceType = null;
        explicitNullableSourceName = null;
        explicitDestinationType = null;
        explicitElementNullability = false;
        InvocationExpressionSyntax? effectiveMappingCall = null;
        InvocationExpressionSyntax? effectiveConstructorParameterCall = null;
        InvocationExpressionSyntax[] mappingCalls =
            GetDestinationConfigurationCalls(createMapInvocation, semanticModel).ToArray();
        HashSet<string> configuredConstructorParameterNames =
            GetConfiguredConstructorParameterNames(mappingCalls, semanticModel);
        foreach (InvocationExpressionSyntax mappingCall in mappingCalls)
        {
            bool isConstructorParameterCall =
                MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                    mappingCall,
                    semanticModel,
                    "ForCtorParam");

            if (!ConfigurationTargetsTopLevelDestinationMember(
                    mappingCall,
                    semanticModel,
                    sourceType,
                    destinationType,
                    configuredConstructorParameterNames,
                    destinationPropertyName))
            {
                continue;
            }

            if (isConstructorParameterCall)
            {
                effectiveConstructorParameterCall = mappingCall;
            }
            else
            {
                effectiveMappingCall = mappingCall;
            }
        }

        effectiveMappingCall = effectiveConstructorParameterCall ?? effectiveMappingCall;
        if (effectiveMappingCall == null)
        {
            return false;
        }
        bool isConstructorParameterConfiguration =
            MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                effectiveMappingCall,
                semanticModel,
                "ForCtorParam");
        IReadOnlyList<ITypeSymbol> nullabilityTargetTypes = new[] { destinationPropertyType };
        if (isConstructorParameterConfiguration)
        {
            string? selectedConstructorParameter =
                AM020MappingConfigurationHelpers.GetSelectedTopLevelMemberNameWithSemanticModel(
                    effectiveMappingCall.ArgumentList.Arguments[0].Expression,
                    semanticModel);
            IReadOnlyList<ITypeSymbol> constructorParameterTypes =
                AM020MappingConfigurationHelpers.GetConstructorParameterTypes(
                    destinationType,
                    sourceType,
                    selectedConstructorParameter ?? string.Empty,
                    configuredConstructorParameterNames);
            if (constructorParameterTypes.Count == 0)
            {
                return false;
            }

            nullabilityTargetTypes = constructorParameterTypes;
        }

        if (ConfigurationCallsMethod(
                effectiveMappingCall,
                "Ignore",
                semanticModel,
                directExecutionOnly: true) ||
            ConfigurationCallsMethod(
                effectiveMappingCall,
                "ConvertUsing",
                semanticModel,
                directExecutionOnly: true))
        {
            handlesNullability = true;
            return true;
        }

        if (TryGetMapFromBodies(
                effectiveMappingCall,
                semanticModel,
                out IReadOnlyList<ExpressionSyntax?> mapFromBodies,
                out bool hasUnconfiguredAlternative))
        {
            ExpressionSyntax? mapFromBody = null;
            foreach (ExpressionSyntax? candidateBody in mapFromBodies)
            {
                if (MapFromBodyHandlesNullability(
                        candidateBody,
                        nullabilityTargetTypes,
                        isConstructorParameterConfiguration,
                        semanticModel))
                {
                    continue;
                }

                mapFromBody = candidateBody;
                break;
            }

            if (mapFromBody == null)
            {
                if (!hasUnconfiguredAlternative)
                {
                    handlesNullability = true;
                    return true;
                }
            }
            else
            {
                ITypeSymbol? mappedType = GetNullableAwareExpressionType(
                    mapFromBody,
                    semanticModel,
                    includeDeclaredNullability: true);
                ITypeSymbol? elementMismatchTargetType = mappedType == null
                    ? null
                    : nullabilityTargetTypes.FirstOrDefault(targetType =>
                        HasNullableElementMismatch(mappedType, targetType));
                if (mappedType != null && elementMismatchTargetType != null)
                {
                    explicitNullableSourceType = mappedType;
                    explicitNullableSourceName =
                        TryGetSourceMemberPath(
                            RemoveNullForgivingExpression(mapFromBody),
                            out string sourceMemberPath)
                            ? sourceMemberPath
                            : null;
                    explicitDestinationType = elementMismatchTargetType;
                    explicitElementNullability = true;
                    return true;
                }

                foreach (ITypeSymbol nullabilityTargetType in nullabilityTargetTypes)
                {
                    if (!TryGetNullableMapFromType(
                            mapFromBody,
                            nullabilityTargetType,
                            semanticModel,
                            out ITypeSymbol? nullableMapFromType,
                            out string? nullableMapFromName))
                    {
                        continue;
                    }

                    if (!ExpressionDereferencesNullableReceiver(mapFromBody, semanticModel) &&
                        nullabilityTargetTypes.All(targetType =>
                            ConfigurationCallsSafeNullSubstitute(
                                effectiveMappingCall,
                                targetType,
                                semanticModel)))
                    {
                        handlesNullability = true;
                        return true;
                    }

                    explicitNullableSourceType = nullableMapFromType;
                    explicitNullableSourceName = nullableMapFromName;
                    explicitDestinationType = nullabilityTargetType;
                    return true;
                }
            }
        }

        if (nullabilityTargetTypes.All(targetType =>
                ConfigurationCallsSafeNullSubstitute(effectiveMappingCall, targetType, semanticModel)))
        {
            handlesNullability = true;
            return true;
        }

        return true;
    }

    private static IEnumerable<InvocationExpressionSyntax> GetDestinationConfigurationCalls(
        InvocationExpressionSyntax createMapInvocation,
        SemanticModel semanticModel)
    {
        foreach (InvocationExpressionSyntax invocation in MappingChainAnalysisHelper.GetScopedChainInvocations(
                     createMapInvocation,
                     semanticModel,
                     stopAtReverseMapBoundary: true))
        {
            if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "ForMember") ||
                MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "ForPath") ||
                MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "ForCtorParam") ||
                MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "ForAllMembers"))
            {
                yield return invocation;
            }
        }
    }

    private static HashSet<string> GetConfiguredConstructorParameterNames(
        IEnumerable<InvocationExpressionSyntax> mappingCalls,
        SemanticModel semanticModel)
    {
        var configuredConstructorParameterNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (InvocationExpressionSyntax mappingCall in mappingCalls)
        {
            if (!MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                    mappingCall,
                    semanticModel,
                    "ForCtorParam") ||
                mappingCall.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            string? configuredConstructorParameterName =
                AM020MappingConfigurationHelpers.GetSelectedTopLevelMemberNameWithSemanticModel(
                    mappingCall.ArgumentList.Arguments[0].Expression,
                    semanticModel);
            if (!string.IsNullOrWhiteSpace(configuredConstructorParameterName))
            {
                configuredConstructorParameterNames.Add(configuredConstructorParameterName!);
            }
        }

        return configuredConstructorParameterNames;
    }

    private static bool HasGuaranteedPostConstructionAssignment(
        IEnumerable<InvocationExpressionSyntax> mappingCalls,
        ITypeSymbol sourceType,
        string destinationPropertyName,
        SemanticModel semanticModel)
    {
        if (mappingCalls.Any(mappingCall =>
                MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                    mappingCall,
                    semanticModel,
                    "ForAllMembers") &&
                (ConfigurationCallsMethod(
                     mappingCall,
                     "Ignore",
                     semanticModel,
                     optionsArgumentIndex: 0,
                     directExecutionOnly: true) ||
                 ConfigurationCallsMethod(
                     mappingCall,
                     "Condition",
                     semanticModel,
                     optionsArgumentIndex: 0,
                     directExecutionOnly: true) ||
                 ConfigurationCallsMethod(
                     mappingCall,
                     "PreCondition",
                     semanticModel,
                     optionsArgumentIndex: 0,
                     directExecutionOnly: true))))
        {
            return false;
        }

        InvocationExpressionSyntax? effectiveMemberConfiguration = null;
        foreach (InvocationExpressionSyntax mappingCall in mappingCalls)
        {
            if (!TargetsTopLevelMemberAssignmentConfiguration(
                    mappingCall,
                    destinationPropertyName,
                    semanticModel))
            {
                continue;
            }

            effectiveMemberConfiguration = mappingCall;
        }

        if (effectiveMemberConfiguration == null)
        {
            return AM020MappingConfigurationHelpers.HasMappableSourceMember(
                sourceType,
                destinationPropertyName);
        }

        if (ConfigurationCallsMethod(
                effectiveMemberConfiguration,
                "Ignore",
                semanticModel,
                directExecutionOnly: true) ||
            ConfigurationCallsMethod(
                effectiveMemberConfiguration,
                "Condition",
                semanticModel,
                directExecutionOnly: true) ||
            ConfigurationCallsMethod(
                effectiveMemberConfiguration,
                "PreCondition",
                semanticModel,
                directExecutionOnly: true))
        {
            return false;
        }

        return ConfigurationCallsMethod(
                   effectiveMemberConfiguration,
                   "MapFrom",
                   semanticModel,
                   directExecutionOnly: true) ||
               ConfigurationCallsMethod(
                   effectiveMemberConfiguration,
                   "ConvertUsing",
                   semanticModel,
                   directExecutionOnly: true) ||
               AM020MappingConfigurationHelpers.HasMappableSourceMember(
                   sourceType,
                   destinationPropertyName);
    }

    private static bool TargetsTopLevelMemberAssignmentConfiguration(
        InvocationExpressionSyntax mappingCall,
        string destinationPropertyName,
        SemanticModel semanticModel)
    {
        if (mappingCall.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        ExpressionSyntax destinationExpression = mappingCall.ArgumentList.Arguments[0].Expression;
        if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                mappingCall,
                semanticModel,
                "ForMember"))
        {
            string? selectedMember =
                AM020MappingConfigurationHelpers.GetSelectedTopLevelMemberNameWithSemanticModel(
                    destinationExpression,
                    semanticModel);
            return string.Equals(
                selectedMember,
                destinationPropertyName,
                StringComparison.OrdinalIgnoreCase);
        }

        return MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                   mappingCall,
                   semanticModel,
                   "ForPath") &&
               TryGetSelectedMemberPath(destinationExpression, out string memberPath) &&
               !memberPath.Contains('.') &&
               string.Equals(
                   memberPath,
                   destinationPropertyName,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void AnalyzeConfiguredConstructorParametersWithoutDestinationProperties(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax createMapInvocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        IReadOnlyCollection<IPropertySymbol> destinationProperties)
    {
        InvocationExpressionSyntax[] mappingCalls =
            GetDestinationConfigurationCalls(createMapInvocation, context.SemanticModel).ToArray();
        HashSet<string> configuredConstructorParameterNames =
            GetConfiguredConstructorParameterNames(mappingCalls, context.SemanticModel);

        foreach (string constructorParameterName in configuredConstructorParameterNames)
        {
            bool targetsDestinationProperty =
                AM020MappingConfigurationHelpers.GetConstructorParameterTypes(
                    destinationType,
                    sourceType,
                    constructorParameterName,
                    configuredConstructorParameterNames).Count > 0 &&
                destinationProperties.Any(destinationProperty =>
                    string.Equals(
                        destinationProperty.Name,
                        constructorParameterName,
                        StringComparison.OrdinalIgnoreCase));
            if (!targetsDestinationProperty)
            {
                string? assignedPropertyName =
                    AM020MappingConfigurationHelpers.GetDestinationPropertyNameForConstructorParameter(
                        destinationType,
                        sourceType,
                        constructorParameterName,
                        configuredConstructorParameterNames,
                        context.SemanticModel);
                targetsDestinationProperty = destinationProperties.Any(destinationProperty =>
                {
                    if (!string.Equals(
                            destinationProperty.Name,
                            assignedPropertyName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    if (!AM020MappingConfigurationHelpers.CanMapDestinationPropertyAfterConstruction(
                            destinationType,
                            destinationProperty.Name))
                    {
                        return true;
                    }

                    return HasGuaranteedPostConstructionAssignment(
                        mappingCalls,
                        sourceType,
                        destinationProperty.Name,
                        context.SemanticModel);
                });
            }

            if (targetsDestinationProperty)
            {
                continue;
            }

            IReadOnlyList<ITypeSymbol> constructorParameterTypes =
                AM020MappingConfigurationHelpers.GetConstructorParameterTypes(
                    destinationType,
                    sourceType,
                    constructorParameterName,
                    configuredConstructorParameterNames);
            if (constructorParameterTypes.Count == 0 ||
                !TryGetExplicitNullabilityConfiguration(
                    createMapInvocation,
                    constructorParameterName,
                    constructorParameterTypes[0],
                    sourceType,
                    destinationType,
                    context.SemanticModel,
                    out bool configurationHandlesNullability,
                    out ITypeSymbol? explicitNullableSourceType,
                    out string? explicitNullableSourceName,
                    out ITypeSymbol? explicitDestinationType,
                    out bool explicitElementNullability) ||
                configurationHandlesNullability ||
                explicitNullableSourceType == null)
            {
                continue;
            }

            ReportNullableToNonNullableDiagnostic(
                context,
                createMapInvocation,
                constructorParameterName,
                explicitNullableSourceName ?? constructorParameterName,
                explicitNullableSourceType,
                sourceType,
                destinationType,
                explicitDestinationType ?? constructorParameterTypes[0],
                isElementNullability: explicitElementNullability);
        }
    }

    internal static bool HasNullableConfiguredConstructorInputForDestinationProperty(
        InvocationExpressionSyntax createMapInvocation,
        string destinationPropertyName,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        SemanticModel semanticModel)
    {
        InvocationExpressionSyntax[] mappingCalls =
            GetDestinationConfigurationCalls(createMapInvocation, semanticModel).ToArray();
        HashSet<string> configuredConstructorParameterNames =
            GetConfiguredConstructorParameterNames(mappingCalls, semanticModel);

        foreach (string constructorParameterName in configuredConstructorParameterNames)
        {
            string? assignedPropertyName =
                AM020MappingConfigurationHelpers.GetDestinationPropertyNameForConstructorParameter(
                    destinationType,
                    sourceType,
                    constructorParameterName,
                    configuredConstructorParameterNames,
                    semanticModel);
            if (!string.Equals(
                    assignedPropertyName,
                    destinationPropertyName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            IReadOnlyList<ITypeSymbol> constructorParameterTypes =
                AM020MappingConfigurationHelpers.GetConstructorParameterTypes(
                    destinationType,
                    sourceType,
                    constructorParameterName,
                    configuredConstructorParameterNames);
            if (constructorParameterTypes.Count == 0)
            {
                continue;
            }

            if (TryGetExplicitNullabilityConfiguration(
                    createMapInvocation,
                    constructorParameterName,
                    constructorParameterTypes[0],
                    sourceType,
                    destinationType,
                    semanticModel,
                    out bool configurationHandlesNullability,
                    out ITypeSymbol? explicitNullableSourceType,
                    out _,
                    out _,
                    out _) &&
                !configurationHandlesNullability &&
                explicitNullableSourceType != null)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<IPropertySymbol> GetConstructorOwnedDestinationProperties(
        InvocationExpressionSyntax createMapInvocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        SemanticModel semanticModel)
    {
        IPropertySymbol[] allDestinationProperties = AutoMapperAnalysisHelpers
            .GetMappableProperties(destinationType, requireGetter: false, requireSetter: false)
            .ToArray();

        InvocationExpressionSyntax[] mappingCalls =
            GetDestinationConfigurationCalls(createMapInvocation, semanticModel).ToArray();
        HashSet<string> configuredConstructorParameterNames =
            GetConfiguredConstructorParameterNames(mappingCalls, semanticModel);
        foreach (InvocationExpressionSyntax mappingCall in mappingCalls)
        {
            if (!MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                    mappingCall,
                    semanticModel,
                    "ForCtorParam") ||
                mappingCall.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            string? selectedConstructorParameter =
                AM020MappingConfigurationHelpers.GetSelectedTopLevelMemberNameWithSemanticModel(
                    mappingCall.ArgumentList.Arguments[0].Expression,
                    semanticModel);
            if (string.IsNullOrWhiteSpace(selectedConstructorParameter))
            {
                continue;
            }

            if (AM020MappingConfigurationHelpers.GetConstructorParameterTypes(
                    destinationType,
                    sourceType,
                    selectedConstructorParameter!,
                    configuredConstructorParameterNames).Count == 0)
            {
                continue;
            }

            IPropertySymbol? destinationProperty = allDestinationProperties.FirstOrDefault(property =>
                string.Equals(
                    property.Name,
                    selectedConstructorParameter,
                    StringComparison.OrdinalIgnoreCase));
            if (destinationProperty == null)
            {
                string? assignedPropertyName =
                    AM020MappingConfigurationHelpers.GetDestinationPropertyNameForConstructorParameter(
                        destinationType,
                        sourceType,
                        selectedConstructorParameter!,
                        configuredConstructorParameterNames,
                        semanticModel);
                destinationProperty = allDestinationProperties.FirstOrDefault(property =>
                    string.Equals(
                        property.Name,
                        assignedPropertyName,
                        StringComparison.OrdinalIgnoreCase));
            }

            if (destinationProperty != null &&
                !AM020MappingConfigurationHelpers.CanMapDestinationPropertyAfterConstruction(
                    destinationType,
                    destinationProperty.Name))
            {
                yield return destinationProperty;
            }
        }
    }

    private static bool ConfigurationTargetsTopLevelDestinationMember(
        InvocationExpressionSyntax mappingCall,
        SemanticModel semanticModel,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        IReadOnlyCollection<string> configuredConstructorParameterNames,
        string destinationPropertyName)
    {
        if (mappingCall.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        ExpressionSyntax destinationExpression = mappingCall.ArgumentList.Arguments[0].Expression;
        if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(mappingCall, semanticModel, "ForMember"))
        {
            string? selectedMember =
                AM020MappingConfigurationHelpers.GetSelectedTopLevelMemberNameWithSemanticModel(
                    destinationExpression,
                    semanticModel);
            return string.Equals(selectedMember, destinationPropertyName, StringComparison.OrdinalIgnoreCase);
        }

        if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(mappingCall, semanticModel, "ForCtorParam"))
        {
            string? selectedConstructorParameter =
                AM020MappingConfigurationHelpers.GetSelectedTopLevelMemberNameWithSemanticModel(
                    destinationExpression,
                    semanticModel);
            if (string.Equals(
                    selectedConstructorParameter,
                    destinationPropertyName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return AM020MappingConfigurationHelpers.GetConstructorParameterTypes(
                    destinationType,
                    sourceType,
                    selectedConstructorParameter ?? string.Empty,
                    configuredConstructorParameterNames).Count > 0;
            }

            string? assignedPropertyName =
                AM020MappingConfigurationHelpers.GetDestinationPropertyNameForConstructorParameter(
                    destinationType,
                    sourceType,
                    selectedConstructorParameter ?? string.Empty,
                    configuredConstructorParameterNames,
                    semanticModel);
            return string.Equals(
                       assignedPropertyName,
                       destinationPropertyName,
                       StringComparison.OrdinalIgnoreCase) &&
                   !AM020MappingConfigurationHelpers.CanMapDestinationPropertyAfterConstruction(
                       destinationType,
                       destinationPropertyName);
        }

        return MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(mappingCall, semanticModel, "ForPath") &&
               TryGetSelectedMemberPath(destinationExpression, out string memberPath) &&
               !memberPath.Contains('.') &&
               string.Equals(memberPath, destinationPropertyName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetSelectedMemberPath(ExpressionSyntax expression, out string memberPath)
    {
        memberPath = string.Empty;
        if (expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            memberPath = literal.Token.ValueText.Trim();
            return !string.IsNullOrEmpty(memberPath);
        }

        CSharpSyntaxNode? body = AutoMapperAnalysisHelpers.GetLambdaBody(expression);
        if (body is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var pathSegments = new Stack<string>();
        ExpressionSyntax currentExpression = memberAccess;
        while (currentExpression is MemberAccessExpressionSyntax currentMemberAccess)
        {
            pathSegments.Push(currentMemberAccess.Name.Identifier.ValueText);
            currentExpression = currentMemberAccess.Expression;
        }

        if (currentExpression is not IdentifierNameSyntax || pathSegments.Count == 0)
        {
            return false;
        }

        memberPath = string.Join(".", pathSegments);
        return true;
    }

    private static bool ConfigurationCallsMethod(
        InvocationExpressionSyntax mappingCall,
        string methodName,
        SemanticModel semanticModel,
        int optionsArgumentIndex = 1,
        bool directExecutionOnly = false)
    {
        if (mappingCall.ArgumentList.Arguments.Count <= optionsArgumentIndex)
        {
            return false;
        }

        ExpressionSyntax optionsExpression = mappingCall.ArgumentList.Arguments[optionsArgumentIndex].Expression;
        string? optionsParameterName = GetSingleParameterName(optionsExpression);
        if (string.IsNullOrEmpty(optionsParameterName))
        {
            return false;
        }

        IParameterSymbol? optionsParameterSymbol = GetSingleParameterSymbol(optionsExpression, semanticModel);

        CSharpSyntaxNode? optionsBody = AutoMapperAnalysisHelpers.GetLambdaBody(optionsExpression);
        if (optionsBody == null)
        {
            return false;
        }

        if (directExecutionOnly)
        {
            return GetDirectlyExecutedOptionMethodInvocations(
                    mappingCall,
                    methodName,
                    semanticModel,
                    optionsArgumentIndex)
                .Count > 0;
        }

        return optionsBody
            .DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                GetIdentifierReceiver(memberAccess) is IdentifierNameSyntax receiver &&
                (optionsParameterSymbol != null
                    ? SymbolEqualityComparer.Default.Equals(
                        semanticModel.GetSymbolInfo(receiver).Symbol,
                        optionsParameterSymbol)
                    : string.Equals(
                        receiver.Identifier.ValueText,
                        optionsParameterName,
                        StringComparison.Ordinal)) &&
                memberAccess.Name.Identifier.ValueText == methodName);
    }

    internal static IReadOnlyList<InvocationExpressionSyntax>
        GetDirectlyExecutedOptionMethodInvocations(
            InvocationExpressionSyntax mappingCall,
            string methodName,
            SemanticModel semanticModel,
            int optionsArgumentIndex = 1,
            bool requireUnconditional = false)
    {
        if (mappingCall.ArgumentList.Arguments.Count <= optionsArgumentIndex)
        {
            return Array.Empty<InvocationExpressionSyntax>();
        }

        ExpressionSyntax optionsExpression =
            mappingCall.ArgumentList.Arguments[optionsArgumentIndex].Expression;
        string? optionsParameterName = GetSingleParameterName(optionsExpression);
        if (string.IsNullOrEmpty(optionsParameterName))
        {
            return Array.Empty<InvocationExpressionSyntax>();
        }

        IParameterSymbol? optionsParameterSymbol = GetSingleParameterSymbol(
            optionsExpression,
            semanticModel);
        CSharpSyntaxNode? optionsBody = AutoMapperAnalysisHelpers.GetLambdaBody(optionsExpression);
        if (optionsBody == null)
        {
            return Array.Empty<InvocationExpressionSyntax>();
        }

        return GetDirectlyExecutedInvocations(optionsBody, semanticModel, optionsParameterSymbol)
            .Where(executedInvocation =>
                (!requireUnconditional || !executedInvocation.IsConditional) &&
                executedInvocation.Invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                GetIdentifierReceiver(memberAccess) is IdentifierNameSyntax receiver &&
                (optionsParameterSymbol != null
                    ? executedInvocation.ReceiverSymbols.Contains(
                        semanticModel.GetSymbolInfo(receiver).Symbol!)
                    : string.Equals(
                        receiver.Identifier.ValueText,
                        optionsParameterName,
                        StringComparison.Ordinal)) &&
                memberAccess.Name.Identifier.ValueText == methodName &&
                MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                    executedInvocation.Invocation,
                    semanticModel,
                    methodName))
            .Select(executedInvocation => executedInvocation.Invocation)
            .ToArray();
    }

    private static IReadOnlyList<(
        InvocationExpressionSyntax Invocation,
        ISet<ISymbol> ReceiverSymbols,
        bool IsConditional)>
        GetDirectlyExecutedInvocations(
        CSharpSyntaxNode optionsBody,
        SemanticModel semanticModel,
        IParameterSymbol? optionsParameter)
    {
        var localFunctions = new Dictionary<IMethodSymbol, LocalFunctionStatementSyntax>(
            SymbolEqualityComparer.Default);
        foreach (LocalFunctionStatementSyntax localFunction in optionsBody
                     .DescendantNodesAndSelf(node => node is not AnonymousFunctionExpressionSyntax)
                     .OfType<LocalFunctionStatementSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(localFunction) is IMethodSymbol symbol)
            {
                localFunctions[symbol] = localFunction;
            }
        }

        var activeLocalFunctions = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        var invocations = new List<(
            InvocationExpressionSyntax Invocation,
            ISet<ISymbol> ReceiverSymbols,
            bool IsConditional)>();
        var receiverSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var conditionalReceiverSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        if (optionsParameter != null)
        {
            receiverSymbols.Add(optionsParameter);
        }

        CollectDirectlyExecutedInvocations(
            optionsBody,
            semanticModel,
            localFunctions,
            activeLocalFunctions,
            receiverSymbols,
            conditionalReceiverSymbols,
            invocations,
            executionIsConditional: false);
        return invocations;
    }

    private static (
        ISet<ISymbol> ReceiverSymbols,
        ISet<ISymbol> ConditionalReceiverSymbols) CollectDirectlyExecutedInvocations(
        CSharpSyntaxNode body,
        SemanticModel semanticModel,
        IReadOnlyDictionary<IMethodSymbol, LocalFunctionStatementSyntax> localFunctions,
        ISet<IMethodSymbol> activeLocalFunctions,
        ISet<ISymbol> initialReceiverSymbols,
        ISet<ISymbol> initialConditionalReceiverSymbols,
        ICollection<(
            InvocationExpressionSyntax Invocation,
            ISet<ISymbol> ReceiverSymbols,
            bool IsConditional)> invocations,
        bool executionIsConditional)
    {
        var receiverMutations = new List<ReceiverMutation>();
        foreach (InvocationExpressionSyntax invocation in body
                     .DescendantNodesAndSelf(node =>
                         node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax)
                     .OfType<InvocationExpressionSyntax>()
                     .OrderBy(candidate => candidate.Span.End))
        {
            if (InvocationMayRunAfterAwait(invocation, body))
            {
                continue;
            }

            ISet<ISymbol> receiverSymbols = GetReceiverSymbolsAtPosition(
                body,
                invocation.SpanStart,
                invocation,
                initialReceiverSymbols,
                initialConditionalReceiverSymbols,
                receiverMutations,
                semanticModel,
                out ISet<ISymbol> conditionalReceiverSymbols);
            bool receiverIsConditional =
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                GetIdentifierReceiver(memberAccess) is IdentifierNameSyntax receiver &&
                semanticModel.GetSymbolInfo(receiver).Symbol is ISymbol receiverSymbol &&
                conditionalReceiverSymbols.Contains(receiverSymbol);
            bool invocationIsConditional = executionIsConditional ||
                                           receiverIsConditional ||
                                           IsConditionallyExecutedWithinBody(invocation, body);
            invocations.Add((invocation, receiverSymbols, invocationIsConditional));

            if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol
                {
                    MethodKind: MethodKind.LocalFunction
                } invokedLocalFunction ||
                !localFunctions.TryGetValue(invokedLocalFunction, out LocalFunctionStatementSyntax? localFunction) ||
                invokedLocalFunction.IsAsync ||
                invokedLocalFunction.IsIterator &&
                !IsSynchronouslyEnumeratedIteratorInvocation(invocation, body, semanticModel) ||
                !activeLocalFunctions.Add(invokedLocalFunction))
            {
                continue;
            }

            CSharpSyntaxNode? localFunctionBody = localFunction.Body;
            if (localFunctionBody == null)
            {
                localFunctionBody = localFunction.ExpressionBody?.Expression;
            }

            if (localFunctionBody == null)
            {
                activeLocalFunctions.Remove(invokedLocalFunction);
                continue;
            }

            ISet<ISymbol> receiverSymbolsAfterArguments = GetReceiverSymbolsAtPosition(
                body,
                invocation.ArgumentList.Span.End,
                invocation,
                initialReceiverSymbols,
                initialConditionalReceiverSymbols,
                receiverMutations,
                semanticModel,
                out ISet<ISymbol> conditionalReceiverSymbolsAfterArguments);
            ISet<ISymbol> localReceiverSymbols = GetReceiverSymbolsForLocalFunctionCall(
                invocation,
                invokedLocalFunction,
                receiverSymbolsAfterArguments,
                conditionalReceiverSymbolsAfterArguments,
                semanticModel,
                out ISet<ISymbol> localConditionalReceiverSymbols,
                out IReadOnlyDictionary<IParameterSymbol, ISymbol> byRefArguments);
            (
                ISet<ISymbol> finalLocalReceiverSymbols,
                ISet<ISymbol> finalLocalConditionalReceiverSymbols) = CollectDirectlyExecutedInvocations(
                localFunctionBody,
                semanticModel,
                localFunctions,
                activeLocalFunctions,
                localReceiverSymbols,
                localConditionalReceiverSymbols,
                invocations,
                invocationIsConditional);
            RecordCapturedReceiverMutations(
                invocation,
                invokedLocalFunction,
                localFunction,
                receiverSymbolsAfterArguments,
                conditionalReceiverSymbolsAfterArguments,
                finalLocalReceiverSymbols,
                finalLocalConditionalReceiverSymbols,
                invocationIsConditional,
                byRefArguments,
                receiverMutations);
            activeLocalFunctions.Remove(invokedLocalFunction);
        }

        ISet<ISymbol> finalReceiverSymbols = GetReceiverSymbolsAtPosition(
            body,
            body.Span.End,
            evaluationPoint: null,
            initialReceiverSymbols,
            initialConditionalReceiverSymbols,
            receiverMutations,
            semanticModel,
            out ISet<ISymbol> finalConditionalReceiverSymbols);
        return (finalReceiverSymbols, finalConditionalReceiverSymbols);
    }

    private static bool InvocationMayRunAfterAwait(
        InvocationExpressionSyntax invocation,
        CSharpSyntaxNode body)
    {
        return body
            .DescendantNodesAndSelf(node =>
                node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax)
            .OfType<AwaitExpressionSyntax>()
            .Any(awaitExpression => awaitExpression.Span.End <= invocation.SpanStart);
    }

    private static void RecordCapturedReceiverMutations(
        InvocationExpressionSyntax invocation,
        IMethodSymbol invokedLocalFunction,
        LocalFunctionStatementSyntax localFunction,
        ISet<ISymbol> receiverSymbols,
        ISet<ISymbol> conditionalReceiverSymbols,
        ISet<ISymbol> finalLocalReceiverSymbols,
        ISet<ISymbol> finalLocalConditionalReceiverSymbols,
        bool invocationIsConditional,
        IReadOnlyDictionary<IParameterSymbol, ISymbol> byRefArguments,
        ICollection<ReceiverMutation> receiverMutations)
    {
        var candidateSymbols = new HashSet<ISymbol>(
            receiverSymbols,
            SymbolEqualityComparer.Default);
        candidateSymbols.UnionWith(conditionalReceiverSymbols);
        candidateSymbols.UnionWith(finalLocalReceiverSymbols);
        candidateSymbols.UnionWith(finalLocalConditionalReceiverSymbols);

        foreach (ISymbol symbol in candidateSymbols.Where(candidate =>
                     IsCapturedByLocalFunction(candidate, invokedLocalFunction, localFunction)))
        {
            ReceiverStatus before = GetReceiverStatus(
                symbol,
                receiverSymbols,
                conditionalReceiverSymbols);
            ReceiverStatus after = GetReceiverStatus(
                symbol,
                finalLocalReceiverSymbols,
                finalLocalConditionalReceiverSymbols);
            ReceiverStatus resultingStatus = invocationIsConditional
                ? MergeConditionalReceiverStatuses(before, after)
                : after;
            if (resultingStatus != before)
            {
                receiverMutations.Add(new ReceiverMutation(
                    invocation.Span.End,
                    symbol,
                    resultingStatus));
            }
        }

        foreach (KeyValuePair<IParameterSymbol, ISymbol> byRefArgument in byRefArguments)
        {
            ReceiverStatus before = GetReceiverStatus(
                byRefArgument.Value,
                receiverSymbols,
                conditionalReceiverSymbols);
            ReceiverStatus after = GetReceiverStatus(
                byRefArgument.Key,
                finalLocalReceiverSymbols,
                finalLocalConditionalReceiverSymbols);
            ReceiverStatus resultingStatus = invocationIsConditional
                ? MergeConditionalReceiverStatuses(before, after)
                : after;
            if (resultingStatus != before)
            {
                receiverMutations.Add(new ReceiverMutation(
                    invocation.Span.End,
                    byRefArgument.Value,
                    resultingStatus));
            }
        }
    }

    private static bool IsCapturedByLocalFunction(
        ISymbol symbol,
        IMethodSymbol invokedLocalFunction,
        LocalFunctionStatementSyntax localFunction)
    {
        if (symbol is IParameterSymbol parameter &&
            SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, invokedLocalFunction))
        {
            return false;
        }

        return symbol is not ILocalSymbol local ||
               local.DeclaringSyntaxReferences.All(reference =>
                   !localFunction.Span.Contains(reference.Span));
    }

    private static ReceiverStatus GetReceiverStatus(
        ISymbol symbol,
        ISet<ISymbol> receiverSymbols,
        ISet<ISymbol> conditionalReceiverSymbols)
    {
        if (!receiverSymbols.Contains(symbol))
        {
            return ReceiverStatus.None;
        }

        return conditionalReceiverSymbols.Contains(symbol)
            ? ReceiverStatus.Conditional
            : ReceiverStatus.Definite;
    }

    private static ReceiverStatus MergeConditionalReceiverStatuses(
        ReceiverStatus before,
        ReceiverStatus after)
    {
        if (before == after)
        {
            return before;
        }

        return before == ReceiverStatus.None && after == ReceiverStatus.None
            ? ReceiverStatus.None
            : ReceiverStatus.Conditional;
    }

    private static ISet<ISymbol> GetReceiverSymbolsForLocalFunctionCall(
        InvocationExpressionSyntax invocation,
        IMethodSymbol localFunction,
        ISet<ISymbol> receiverSymbols,
        ISet<ISymbol> conditionalReceiverSymbols,
        SemanticModel semanticModel,
        out ISet<ISymbol> localConditionalReceiverSymbols,
        out IReadOnlyDictionary<IParameterSymbol, ISymbol> byRefArguments)
    {
        var localReceiverSymbols = new HashSet<ISymbol>(
            receiverSymbols,
            SymbolEqualityComparer.Default);
        localConditionalReceiverSymbols = new HashSet<ISymbol>(
            conditionalReceiverSymbols,
            SymbolEqualityComparer.Default);
        var mutableByRefArguments = new Dictionary<IParameterSymbol, ISymbol>(
            SymbolEqualityComparer.Default);
        for (int argumentIndex = 0;
             argumentIndex < invocation.ArgumentList.Arguments.Count;
             argumentIndex++)
        {
            ArgumentSyntax argument = invocation.ArgumentList.Arguments[argumentIndex];
            IParameterSymbol? parameter = null;
            if (argument.NameColon != null)
            {
                string parameterName = argument.NameColon.Name.Identifier.ValueText;
                parameter = localFunction.Parameters.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, parameterName, StringComparison.Ordinal));
            }
            else if (argumentIndex < localFunction.Parameters.Length)
            {
                parameter = localFunction.Parameters[argumentIndex];
            }

            if (parameter == null)
            {
                continue;
            }

            if (parameter.RefKind is RefKind.Ref or RefKind.Out &&
                semanticModel.GetSymbolInfo(argument.Expression).Symbol is ISymbol argumentSymbol)
            {
                mutableByRefArguments[parameter] = argumentSymbol;
            }

            if (ReferencesReceiverSymbol(argument.Expression, receiverSymbols, semanticModel))
            {
                localReceiverSymbols.Add(parameter);
                if (ReferencesReceiverSymbol(
                        argument.Expression,
                        conditionalReceiverSymbols,
                        semanticModel))
                {
                    localConditionalReceiverSymbols.Add(parameter);
                }
                else
                {
                    localConditionalReceiverSymbols.Remove(parameter);
                }
            }
        }

        byRefArguments = mutableByRefArguments;
        return localReceiverSymbols;
    }

    private static ISet<ISymbol> GetReceiverSymbolsAtPosition(
        CSharpSyntaxNode body,
        int position,
        SyntaxNode? evaluationPoint,
        ISet<ISymbol> initialReceiverSymbols,
        ISet<ISymbol> initialConditionalReceiverSymbols,
        IEnumerable<ReceiverMutation> receiverMutations,
        SemanticModel semanticModel,
        out ISet<ISymbol> conditionalReceiverSymbols)
    {
        var receiverSymbols = new HashSet<ISymbol>(
            initialReceiverSymbols,
            SymbolEqualityComparer.Default);
        conditionalReceiverSymbols = new HashSet<ISymbol>(
            initialConditionalReceiverSymbols,
            SymbolEqualityComparer.Default);
        IEnumerable<(int Position, int Start, SyntaxNode? Node, ReceiverMutation? Mutation)> syntaxChanges = body
            .DescendantNodesAndSelf(candidate =>
                candidate is not AnonymousFunctionExpressionSyntax and
                not LocalFunctionStatementSyntax)
            .Where(candidate =>
                candidate.Span.End <= position &&
                candidate is VariableDeclaratorSyntax or AssignmentExpressionSyntax)
            .Select(candidate => (
                candidate.Span.End,
                candidate.SpanStart,
                (SyntaxNode?)candidate,
                (ReceiverMutation?)null));
        IEnumerable<(int Position, int Start, SyntaxNode? Node, ReceiverMutation? Mutation)> helperChanges =
            receiverMutations
                .Where(mutation => mutation.Position <= position)
                .Select(mutation => (
                    mutation.Position,
                    mutation.Position,
                    (SyntaxNode?)null,
                    (ReceiverMutation?)mutation));

        foreach ((int _, int _, SyntaxNode? node, ReceiverMutation? mutation) in syntaxChanges
                     .Concat(helperChanges)
                     .OrderBy(change => change.Position)
                     .ThenBy(change => change.Start))
        {
            if (mutation is { } receiverMutation)
            {
                ApplyReceiverStatus(
                    receiverMutation.Symbol,
                    receiverMutation.Status,
                    receiverSymbols,
                    conditionalReceiverSymbols);
                continue;
            }

            if (node is VariableDeclaratorSyntax declarator &&
                semanticModel.GetDeclaredSymbol(declarator) is ILocalSymbol localSymbol)
            {
                if (declarator.Initializer != null &&
                    ReferencesReceiverSymbol(
                        declarator.Initializer.Value,
                        receiverSymbols,
                        semanticModel))
                {
                    receiverSymbols.Add(localSymbol);
                    if (ReferencesReceiverSymbol(
                            declarator.Initializer.Value,
                            conditionalReceiverSymbols,
                            semanticModel))
                    {
                        conditionalReceiverSymbols.Add(localSymbol);
                    }
                    else
                    {
                        conditionalReceiverSymbols.Remove(localSymbol);
                    }
                }
                else
                {
                    receiverSymbols.Remove(localSymbol);
                    conditionalReceiverSymbols.Remove(localSymbol);
                }

                continue;
            }

            if (node is not AssignmentExpressionSyntax assignment)
            {
                continue;
            }

            ISymbol? assignedSymbol = semanticModel.GetSymbolInfo(assignment.Left).Symbol;
            if (assignedSymbol == null)
            {
                continue;
            }

            bool assignmentDefinitelyApplies = AssignmentDefinitelyAppliesToNode(
                assignment,
                evaluationPoint,
                body,
                semanticModel);
            if (assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
                ReferencesReceiverSymbol(assignment.Right, receiverSymbols, semanticModel))
            {
                receiverSymbols.Add(assignedSymbol);
                if (!assignmentDefinitelyApplies ||
                    ReferencesReceiverSymbol(
                        assignment.Right,
                        conditionalReceiverSymbols,
                        semanticModel))
                {
                    conditionalReceiverSymbols.Add(assignedSymbol);
                }
                else
                {
                    conditionalReceiverSymbols.Remove(assignedSymbol);
                }
            }
            else if (assignmentDefinitelyApplies)
            {
                receiverSymbols.Remove(assignedSymbol);
                conditionalReceiverSymbols.Remove(assignedSymbol);
            }
            else if (receiverSymbols.Contains(assignedSymbol))
            {
                conditionalReceiverSymbols.Add(assignedSymbol);
            }
        }

        return receiverSymbols;
    }

    private static void ApplyReceiverStatus(
        ISymbol symbol,
        ReceiverStatus status,
        ISet<ISymbol> receiverSymbols,
        ISet<ISymbol> conditionalReceiverSymbols)
    {
        if (status == ReceiverStatus.None)
        {
            receiverSymbols.Remove(symbol);
            conditionalReceiverSymbols.Remove(symbol);
            return;
        }

        receiverSymbols.Add(symbol);
        if (status == ReceiverStatus.Conditional)
        {
            conditionalReceiverSymbols.Add(symbol);
        }
        else
        {
            conditionalReceiverSymbols.Remove(symbol);
        }
    }

    private static bool AssignmentDefinitelyAppliesToNode(
        AssignmentExpressionSyntax assignment,
        SyntaxNode? evaluationPoint,
        CSharpSyntaxNode body,
        SemanticModel semanticModel)
    {
        for (SyntaxNode? ancestor = assignment.Parent;
             ancestor != null && ancestor != body;
             ancestor = ancestor.Parent)
        {
            SyntaxNode? conditionalRegion = GetConditionalExecutionRegion(ancestor, assignment);

            if (conditionalRegion != null &&
                (evaluationPoint == null ||
                 !conditionalRegion.Span.Contains(evaluationPoint.Span)) &&
                (evaluationPoint == null ||
                 !ConditionalRegionDominatesLaterNode(
                     ancestor,
                     conditionalRegion,
                     evaluationPoint,
                     semanticModel)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ConditionalRegionDominatesLaterNode(
        SyntaxNode ancestor,
        SyntaxNode conditionalRegion,
        SyntaxNode evaluationPoint,
        SemanticModel semanticModel)
    {
        if (ancestor.Span.End >= evaluationPoint.SpanStart)
        {
            return false;
        }

        StatementSyntax? alternative = ancestor switch
        {
            IfStatementSyntax ifStatement
                when ifStatement.Statement == conditionalRegion => ifStatement.Else?.Statement,
            ElseClauseSyntax elseClause
                when elseClause.Statement == conditionalRegion &&
                     elseClause.Parent is IfStatementSyntax ifStatement => ifStatement.Statement,
            _ => null
        };
        if (alternative == null)
        {
            return false;
        }

        ControlFlowAnalysis? controlFlow = semanticModel.AnalyzeControlFlow(alternative);
        return controlFlow is { Succeeded: true, EndPointIsReachable: false };
    }

    private static bool IsConditionallyExecutedWithinBody(
        SyntaxNode node,
        CSharpSyntaxNode body)
    {
        for (SyntaxNode? ancestor = node.Parent;
             ancestor != null && ancestor != body;
             ancestor = ancestor.Parent)
        {
            if (GetConditionalExecutionRegion(ancestor, node) != null)
            {
                return true;
            }
        }

        return false;
    }

    private static SyntaxNode? GetConditionalExecutionRegion(
        SyntaxNode ancestor,
        SyntaxNode node)
    {
        return ancestor switch
        {
            IfStatementSyntax ifStatement
                when ifStatement.Statement.Span.Contains(node.Span) => ifStatement.Statement,
            ElseClauseSyntax elseClause => elseClause.Statement,
            ForStatementSyntax forStatement
                when forStatement.Statement.Span.Contains(node.Span) => forStatement.Statement,
            ForEachStatementSyntax forEachStatement
                when forEachStatement.Statement.Span.Contains(node.Span) => forEachStatement.Statement,
            ForEachVariableStatementSyntax forEachVariableStatement
                when forEachVariableStatement.Statement.Span.Contains(node.Span) =>
                forEachVariableStatement.Statement,
            WhileStatementSyntax whileStatement
                when whileStatement.Statement.Span.Contains(node.Span) => whileStatement.Statement,
            SwitchSectionSyntax switchSection => switchSection,
            ConditionalExpressionSyntax conditionalExpression
                when conditionalExpression.WhenTrue.Span.Contains(node.Span) => conditionalExpression.WhenTrue,
            ConditionalExpressionSyntax conditionalExpression
                when conditionalExpression.WhenFalse.Span.Contains(node.Span) => conditionalExpression.WhenFalse,
            BinaryExpressionSyntax binaryExpression
                when binaryExpression.Right.Span.Contains(node.Span) &&
                     (binaryExpression.IsKind(SyntaxKind.LogicalAndExpression) ||
                      binaryExpression.IsKind(SyntaxKind.LogicalOrExpression) ||
                      binaryExpression.IsKind(SyntaxKind.CoalesceExpression)) => binaryExpression.Right,
            SwitchExpressionArmSyntax switchExpressionArm => switchExpressionArm,
            CatchClauseSyntax catchClause
                when catchClause.Block.Span.Contains(node.Span) => catchClause.Block,
            TryStatementSyntax tryStatement
                when tryStatement.Block.Span.Contains(node.Span) => tryStatement.Block,
            _ => null
        };
    }

    private static bool IsSynchronouslyEnumeratedIteratorInvocation(
        InvocationExpressionSyntax invocation,
        CSharpSyntaxNode body,
        SemanticModel semanticModel)
    {
        ExpressionSyntax expression = invocation;
        while (expression.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized;
        }

        if (expression.Parent switch
            {
                ForEachStatementSyntax forEachStatement => forEachStatement.Expression == expression,
                ForEachVariableStatementSyntax forEachVariableStatement =>
                    forEachVariableStatement.Expression == expression,
                _ => false
            })
        {
            return true;
        }

        if (IsSynchronouslyMaterializedIteratorInvocation(expression, semanticModel))
        {
            return true;
        }

        var iteratorResults = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        while (true)
        {
            while (expression.Parent is ParenthesizedExpressionSyntax parenthesized)
            {
                expression = parenthesized;
            }

            if (expression.Parent is AssignmentExpressionSyntax assignment &&
                assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
                assignment.Right == expression &&
                semanticModel.GetSymbolInfo(assignment.Left).Symbol is ILocalSymbol assignedLocal)
            {
                iteratorResults.Add(assignedLocal);
                expression = assignment;
                continue;
            }

            if (expression.Parent is EqualsValueClauseSyntax
                {
                    Parent: VariableDeclaratorSyntax declarator
                } equalsValue &&
                equalsValue.Value == expression &&
                semanticModel.GetDeclaredSymbol(declarator) is ILocalSymbol declaredLocal)
            {
                iteratorResults.Add(declaredLocal);
            }

            break;
        }

        if (iteratorResults.Count == 0)
        {
            return false;
        }

        IReadOnlyList<SyntaxNode> synchronousNodes = body
            .DescendantNodesAndSelf(node =>
                node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax)
            .ToArray();

        foreach (ForEachStatementSyntax forEachStatement in synchronousNodes
                     .OfType<ForEachStatementSyntax>())
        {
            if (IteratorResultReachesEnumeration(
                    iteratorResults,
                    forEachStatement.Expression,
                    forEachStatement.SpanStart,
                    invocation,
                    synchronousNodes,
                    body,
                    semanticModel))
            {
                return true;
            }
        }

        return synchronousNodes
            .OfType<ForEachVariableStatementSyntax>()
            .Any(forEachStatement =>
                IteratorResultReachesEnumeration(
                    iteratorResults,
                    forEachStatement.Expression,
                    forEachStatement.SpanStart,
                    invocation,
                    synchronousNodes,
                    body,
                    semanticModel));
    }

    private static bool IsSynchronouslyMaterializedIteratorInvocation(
        ExpressionSyntax iteratorExpression,
        SemanticModel semanticModel)
    {
        InvocationExpressionSyntax? terminalInvocation = null;
        if (iteratorExpression.Parent is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Expression == iteratorExpression &&
            memberAccess.Parent is InvocationExpressionSyntax extensionInvocation &&
            extensionInvocation.Expression == memberAccess)
        {
            terminalInvocation = extensionInvocation;
        }
        else if (iteratorExpression.Parent is ArgumentSyntax
                 {
                     Parent.Parent: InvocationExpressionSyntax staticInvocation
                 })
        {
            terminalInvocation = staticInvocation;
        }

        if (terminalInvocation == null)
        {
            return false;
        }

        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(terminalInvocation);
        IMethodSymbol? method = symbolInfo.Symbol as IMethodSymbol ??
                                symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        IMethodSymbol? definition = method?.ReducedFrom ?? method;
        if (definition?.ContainingType.Name != "Enumerable" ||
            definition.ContainingNamespace.ToDisplayString() != "System.Linq")
        {
            return false;
        }

        return definition.Name is "ToList" or "ToArray";
    }

    private static bool IteratorResultReachesEnumeration(
        ISet<ISymbol> iteratorResults,
        ExpressionSyntax enumerableExpression,
        int enumerationStart,
        InvocationExpressionSyntax iteratorInvocation,
        IEnumerable<SyntaxNode> synchronousNodes,
        CSharpSyntaxNode body,
        SemanticModel semanticModel)
    {
        if (enumerationStart <= iteratorInvocation.SpanStart)
        {
            return false;
        }

        var symbolsHoldingIterator = new HashSet<ISymbol>(
            iteratorResults,
            SymbolEqualityComparer.Default);
        foreach (SyntaxNode node in synchronousNodes
                     .Where(node =>
                         node.SpanStart > iteratorInvocation.Span.End &&
                         node.SpanStart < enumerationStart &&
                         node is VariableDeclaratorSyntax or AssignmentExpressionSyntax)
                     .OrderBy(node => node.SpanStart))
        {
            if (node is VariableDeclaratorSyntax
                {
                    Initializer: { } initializer
                } declarator &&
                semanticModel.GetDeclaredSymbol(declarator) is ILocalSymbol localSymbol)
            {
                if (ReferencesReceiverSymbol(initializer.Value, symbolsHoldingIterator, semanticModel))
                {
                    symbolsHoldingIterator.Add(localSymbol);
                }

                continue;
            }

            if (node is not AssignmentExpressionSyntax assignment)
            {
                continue;
            }

            ISymbol? assignedSymbol = semanticModel.GetSymbolInfo(assignment.Left).Symbol;
            if (assignedSymbol == null)
            {
                continue;
            }

            if (assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
                ReferencesReceiverSymbol(assignment.Right, symbolsHoldingIterator, semanticModel))
            {
                symbolsHoldingIterator.Add(assignedSymbol);
            }
            else if (AssignmentDefinitelyAppliesToNode(
                         assignment,
                         enumerableExpression,
                         body,
                         semanticModel))
            {
                symbolsHoldingIterator.Remove(assignedSymbol);
            }
        }

        return ReferencesReceiverSymbol(
            enumerableExpression,
            symbolsHoldingIterator,
            semanticModel);
    }

    private static bool ReferencesReceiverSymbol(
        ExpressionSyntax expression,
        ISet<ISymbol> receiverSymbols,
        SemanticModel semanticModel)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        if (expression is AssignmentExpressionSyntax assignment &&
            assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
        {
            return ReferencesReceiverSymbol(assignment.Right, receiverSymbols, semanticModel);
        }

        ISymbol? symbol = semanticModel.GetSymbolInfo(expression).Symbol;
        return symbol != null && receiverSymbols.Contains(symbol);
    }

    private static bool ConfigurationCallsSafeNullSubstitute(
        InvocationExpressionSyntax mappingCall,
        ITypeSymbol destinationPropertyType,
        SemanticModel semanticModel)
    {
        return GetDirectlyExecutedOptionMethodInvocations(
                mappingCall,
                "NullSubstitute",
                semanticModel,
                requireUnconditional: true)
            .Any(invocation =>
                invocation.ArgumentList.Arguments.Count > 0 &&
                NullSubstituteValueIsSafe(
                    invocation.ArgumentList.Arguments[0].Expression,
                    destinationPropertyType,
                    semanticModel));
    }

    private static bool NullSubstituteValueIsSafe(
        ExpressionSyntax substituteExpression,
        ITypeSymbol destinationPropertyType,
        SemanticModel semanticModel)
    {
        if (substituteExpression.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return false;
        }

        TypeInfo typeInfo = semanticModel.GetTypeInfo(substituteExpression);
        ITypeSymbol? substituteType = typeInfo.Type ?? typeInfo.ConvertedType;
        if (substituteType == null)
        {
            return false;
        }

        if ((substituteExpression.IsKind(SyntaxKind.DefaultLiteralExpression) ||
             substituteExpression is DefaultExpressionSyntax) &&
            !IsNonNullableValueType(substituteType))
        {
            return false;
        }

        if (IsNullableType(substituteType))
        {
            return false;
        }

        Conversion conversion = semanticModel.ClassifyConversion(substituteExpression, destinationPropertyType);
        if (conversion.Exists && conversion.IsImplicit)
        {
            return true;
        }

        return AreUnderlyingTypesCompatible(
            AutoMapperAnalysisHelpers.GetUnderlyingType(substituteType),
            AutoMapperAnalysisHelpers.GetUnderlyingType(destinationPropertyType));
    }

    private static bool IsNonNullableValueType(ITypeSymbol type)
    {
        return type.IsValueType && !IsNullableType(type);
    }

    private static string? GetSingleParameterName(ExpressionSyntax expression)
    {
        return expression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesizedLambda =>
                parenthesizedLambda.ParameterList.Parameters[0].Identifier.ValueText,
            _ => null
        };
    }

    private static IParameterSymbol? GetSingleParameterSymbol(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        ParameterSyntax? parameter = expression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Parameter,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesizedLambda =>
                parenthesizedLambda.ParameterList.Parameters[0],
            _ => null
        };

        return parameter == null ? null : semanticModel.GetDeclaredSymbol(parameter);
    }

    private static IdentifierNameSyntax? GetIdentifierReceiver(
        MemberAccessExpressionSyntax memberAccess)
    {
        ExpressionSyntax receiver = memberAccess.Expression;
        while (receiver is ParenthesizedExpressionSyntax parenthesized)
        {
            receiver = parenthesized.Expression;
        }

        return receiver as IdentifierNameSyntax;
    }

    private static bool TryGetMapFromBodies(
        InvocationExpressionSyntax mappingCall,
        SemanticModel semanticModel,
        out IReadOnlyList<ExpressionSyntax?> mapFromBodies,
        out bool hasUnconfiguredAlternative)
    {
        var bodies = new List<ExpressionSyntax?>();
        mapFromBodies = bodies;
        hasUnconfiguredAlternative = true;
        if (mappingCall.ArgumentList.Arguments.Count <= 1)
        {
            return false;
        }

        ExpressionSyntax optionsExpression = mappingCall.ArgumentList.Arguments[1].Expression;
        string? optionsParameterName = GetSingleParameterName(optionsExpression);
        if (string.IsNullOrEmpty(optionsParameterName))
        {
            return false;
        }

        IParameterSymbol? optionsParameterSymbol = GetSingleParameterSymbol(
            optionsExpression,
            semanticModel);

        CSharpSyntaxNode? optionsBody = AutoMapperAnalysisHelpers.GetLambdaBody(optionsExpression);
        if (optionsBody == null)
        {
            return false;
        }

        foreach ((
                     InvocationExpressionSyntax Invocation,
                     ISet<ISymbol> ReceiverSymbols,
                     bool IsConditional) executedInvocation in
                 GetDirectlyExecutedInvocations(optionsBody, semanticModel, optionsParameterSymbol))
        {
            InvocationExpressionSyntax invocation = executedInvocation.Invocation;
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                GetIdentifierReceiver(memberAccess) is not IdentifierNameSyntax receiver ||
                (optionsParameterSymbol != null
                    ? !executedInvocation.ReceiverSymbols.Contains(
                        semanticModel.GetSymbolInfo(receiver).Symbol!)
                    : !string.Equals(
                        receiver.Identifier.ValueText,
                        optionsParameterName,
                        StringComparison.Ordinal)) ||
                memberAccess.Name.Identifier.ValueText != "MapFrom")
            {
                continue;
            }

            if (!MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                    invocation,
                    semanticModel,
                    "MapFrom"))
            {
                continue;
            }

            ExpressionSyntax? mapFromBody;
            if (invocation.ArgumentList.Arguments.Count == 0)
            {
                mapFromBody = null;
            }
            else if (memberAccess.Name is GenericNameSyntax
                {
                    TypeArgumentList.Arguments.Count: > 1
                })
            {
                mapFromBody = null;
            }
            else
            {
                ExpressionSyntax mapFromArgument = invocation.ArgumentList.Arguments[0].Expression;
                mapFromBody = mapFromArgument switch
                {
                    SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Body as ExpressionSyntax,
                    ParenthesizedLambdaExpressionSyntax parenthesizedLambda =>
                        parenthesizedLambda.Body as ExpressionSyntax,
                    _ => null
                };
            }

            if (!executedInvocation.IsConditional)
            {
                bodies.Clear();
                hasUnconfiguredAlternative = false;
            }

            bodies.Add(mapFromBody);
        }

        return bodies.Count > 0;
    }

    private static bool MapFromBodyHandlesNullability(
        ExpressionSyntax? mapFromBody,
        IReadOnlyList<ITypeSymbol> nullabilityTargetTypes,
        bool isConstructorParameterConfiguration,
        SemanticModel semanticModel)
    {
        if (mapFromBody == null)
        {
            return true;
        }

        ITypeSymbol? mappedType = GetNullableAwareExpressionType(
            mapFromBody,
            semanticModel,
            includeDeclaredNullability: true);
        if (mappedType != null &&
            nullabilityTargetTypes.Any(targetType =>
                HasNullableElementMismatch(mappedType, targetType)))
        {
            return false;
        }

        if (isConstructorParameterConfiguration &&
            nullabilityTargetTypes.All(IsNullableType) &&
            !ExpressionDereferencesNullableReceiver(mapFromBody, semanticModel))
        {
            return true;
        }

        return nullabilityTargetTypes.All(targetType =>
            MapFromExpressionProducesNonNullableValue(mapFromBody, targetType, semanticModel));
    }

    private static bool MapFromExpressionProducesNonNullableValue(
        ExpressionSyntax mapFromBody,
        ITypeSymbol destinationPropertyType,
        SemanticModel semanticModel)
    {
        ITypeSymbol? mappedType = GetNullableAwareExpressionType(
            mapFromBody,
            semanticModel,
            includeDeclaredNullability: true);
        if (mappedType == null)
        {
            return false;
        }

        return !ExpressionDereferencesNullableReceiver(mapFromBody, semanticModel) &&
               !IsNullableType(mappedType) &&
               AreUnderlyingTypesCompatible(
                   AutoMapperAnalysisHelpers.GetUnderlyingType(mappedType),
                   AutoMapperAnalysisHelpers.GetUnderlyingType(destinationPropertyType));
    }

    private static bool ExpressionDereferencesNullableReceiver(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        return expression
            .DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(memberAccess =>
                !IsSafeNullableValueMemberAccess(memberAccess) &&
                !IsExtensionMethodReceiverAccess(memberAccess, semanticModel) &&
                IsNullableExpression(memberAccess.Expression, semanticModel));
    }

    private static bool IsExtensionMethodReceiverAccess(
        MemberAccessExpressionSyntax memberAccess,
        SemanticModel semanticModel)
    {
        if (memberAccess.Parent is not InvocationExpressionSyntax invocation ||
            invocation.Expression != memberAccess)
        {
            return false;
        }

        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);
        IMethodSymbol? methodSymbol = symbolInfo.Symbol as IMethodSymbol ??
                                      symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        return methodSymbol is { IsExtensionMethod: true } or { ReducedFrom: not null };
    }

    private static bool IsSafeNullableValueMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        return memberAccess.Name.Identifier.ValueText == "GetValueOrDefault" &&
               memberAccess.Parent is InvocationExpressionSyntax invocation &&
               invocation.Expression == memberAccess;
    }

    private static bool IsNullableExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        ITypeSymbol? expressionType = GetNullableAwareExpressionType(expression, semanticModel);
        return expressionType != null && IsNullableType(expressionType);
    }

    private static ITypeSymbol? GetNullableAwareExpressionType(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        bool includeDeclaredNullability = false)
    {
        ExpressionSyntax nullableTransparentExpression = RemoveNullForgivingExpression(expression);
        TypeInfo typeInfo = semanticModel.GetTypeInfo(expression);
        ITypeSymbol? expressionType = typeInfo.Type ?? typeInfo.ConvertedType;
        if (expressionType != null && IsNullableType(expressionType))
        {
            return expressionType;
        }

        if (!includeDeclaredNullability && nullableTransparentExpression == expression)
        {
            return expressionType;
        }

        ISymbol? symbol = semanticModel.GetSymbolInfo(nullableTransparentExpression).Symbol;
        return symbol switch
        {
            IPropertySymbol propertySymbol => propertySymbol.Type,
            IFieldSymbol fieldSymbol => fieldSymbol.Type,
            ILocalSymbol localSymbol => localSymbol.Type,
            IParameterSymbol parameterSymbol => parameterSymbol.Type,
            _ => expressionType
        };
    }

    private static ExpressionSyntax RemoveNullForgivingExpression(ExpressionSyntax expression)
    {
        while (expression is PostfixUnaryExpressionSyntax postfix &&
               postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression))
        {
            expression = postfix.Operand;
        }

        return expression;
    }

    private static bool TryGetNullableMapFromType(
        ExpressionSyntax mapFromBody,
        ITypeSymbol destinationPropertyType,
        SemanticModel semanticModel,
        out ITypeSymbol? nullableMapFromType,
        out string? nullableMapFromName)
    {
        nullableMapFromType = null;
        nullableMapFromName = null;
        ExpressionSyntax nullableTransparentBody = RemoveNullForgivingExpression(mapFromBody);
        ITypeSymbol? mappedType = GetNullableAwareExpressionType(
            nullableTransparentBody,
            semanticModel,
            includeDeclaredNullability: true);
        if (TryGetNullableDereferencedReceiver(
                mapFromBody,
                destinationPropertyType,
                semanticModel,
                out nullableMapFromType,
                out nullableMapFromName))
        {
            return true;
        }

        if (mappedType == null ||
            !IsNullableType(mappedType) ||
            IsNullableType(destinationPropertyType) ||
            !AreUnderlyingTypesCompatible(
                AutoMapperAnalysisHelpers.GetUnderlyingType(mappedType),
                AutoMapperAnalysisHelpers.GetUnderlyingType(destinationPropertyType)))
        {
            return false;
        }

        nullableMapFromType = mappedType;
        nullableMapFromName = TryGetSourceMemberPath(nullableTransparentBody, out string sourceMemberPath)
            ? sourceMemberPath
            : null;
        return true;
    }

    private static bool TryGetNullableDereferencedReceiver(
        ExpressionSyntax expression,
        ITypeSymbol destinationPropertyType,
        SemanticModel semanticModel,
        out ITypeSymbol? nullableReceiverType,
        out string? nullableReceiverName)
    {
        nullableReceiverType = null;
        nullableReceiverName = null;
        foreach (MemberAccessExpressionSyntax memberAccess in expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
        {
            if (IsSafeNullableValueMemberAccess(memberAccess))
            {
                continue;
            }

            ITypeSymbol? receiverType = GetNullableAwareExpressionType(memberAccess.Expression, semanticModel);
            if (receiverType == null ||
                !IsNullableType(receiverType) ||
                IsNullableType(destinationPropertyType))
            {
                continue;
            }

            nullableReceiverType = receiverType;
            nullableReceiverName = TryGetSourceMemberPath(memberAccess.Expression, out string sourceMemberPath)
                ? sourceMemberPath
                : null;
            return true;
        }

        return false;
    }

    private static bool TryGetSourceMemberPath(ExpressionSyntax expression, out string sourceMemberPath)
    {
        sourceMemberPath = string.Empty;
        if (TryGetSourceMemberPathFromExpression(expression, out sourceMemberPath))
        {
            return true;
        }

        foreach (MemberAccessExpressionSyntax memberAccess in expression.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (TryGetSourceMemberPathFromExpression(memberAccess, out sourceMemberPath))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetSourceMemberPathFromExpression(ExpressionSyntax expression, out string sourceMemberPath)
    {
        sourceMemberPath = string.Empty;
        if (expression is InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax invocationMemberAccess
            })
        {
            return TryGetSourceMemberPathFromExpression(invocationMemberAccess.Expression, out sourceMemberPath);
        }

        if (expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var pathSegments = new Stack<string>();
        ExpressionSyntax currentExpression = memberAccess;
        while (currentExpression is MemberAccessExpressionSyntax currentMemberAccess)
        {
            pathSegments.Push(currentMemberAccess.Name.Identifier.ValueText);
            currentExpression = currentMemberAccess.Expression;
        }

        if (currentExpression is not IdentifierNameSyntax || pathSegments.Count == 0)
        {
            return false;
        }

        sourceMemberPath = string.Join(".", pathSegments);
        return true;
    }

    private static void AnalyzeNullableCompatibility(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        string sourceMemberName,
        ITypeSymbol sourceMemberType,
        bool sourceMemberRequiresInvocation,
        IPropertySymbol destinationProperty,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        string sourceTypeName = sourceMemberType.ToDisplayString();
        string destTypeName = destinationProperty.Type.ToDisplayString();

        // Case 1: Nullable source -> Non-nullable destination (ERROR)
        if (IsNullableType(sourceMemberType) && !IsNullableType(destinationProperty.Type))
        {
            // Check if the underlying types are compatible
            ITypeSymbol sourceUnderlyingType = AutoMapperAnalysisHelpers.GetUnderlyingType(sourceMemberType);
            ITypeSymbol destUnderlyingType = AutoMapperAnalysisHelpers.GetUnderlyingType(destinationProperty.Type);

            if (AreUnderlyingTypesCompatible(sourceUnderlyingType, destUnderlyingType))
            {
                ReportNullableToNonNullableDiagnostic(
                    context,
                    invocation,
                    destinationProperty.Name,
                    sourceMemberName,
                    sourceMemberType,
                    sourceType,
                    destinationType,
                    destinationProperty.Type,
                    sourceMemberRequiresInvocation: sourceMemberRequiresInvocation);
            }
        }
        // Case 2: Non-nullable source -> Nullable destination (INFO)
        else if (!IsNullableType(sourceMemberType) && IsNullableType(destinationProperty.Type))
        {
            ITypeSymbol sourceUnderlyingType = AutoMapperAnalysisHelpers.GetUnderlyingType(sourceMemberType);
            ITypeSymbol destUnderlyingType = AutoMapperAnalysisHelpers.GetUnderlyingType(destinationProperty.Type);

            if (AreUnderlyingTypesCompatible(sourceUnderlyingType, destUnderlyingType))
            {
                var properties = ImmutableDictionary.CreateBuilder<string, string?>();
                properties.Add(PropertyNamePropertyName, destinationProperty.Name);
                properties.Add(SourcePropertyTypePropertyName, sourceTypeName);
                properties.Add(DestinationPropertyTypePropertyName, destTypeName);

                var diagnostic = Diagnostic.Create(
                    NonNullableToNullableRule,
                    invocation.GetLocation(),
                    properties.ToImmutable(),
                    destinationProperty.Name,
                    GetDiagnosticTypeName(sourceType),
                    sourceTypeName,
                    GetDiagnosticTypeName(destinationType),
                    destTypeName
                );
                context.ReportDiagnostic(diagnostic);
            }
        }
        // Case 3: Collection whose element nullability is lost (e.g. List<string?> -> List<string>).
        // The container is non-nullable on both sides (so Cases 1/2 do not apply), but a nullable source
        // element flows into a non-nullable destination element — the same NRE risk one level down.
        else if (HasNullableElementMismatch(sourceMemberType, destinationProperty.Type))
        {
            ReportNullableToNonNullableDiagnostic(
                context,
                invocation,
                destinationProperty.Name,
                sourceMemberName,
                sourceMemberType,
                sourceType,
                destinationType,
                destinationProperty.Type,
                isElementNullability: true,
                sourceMemberRequiresInvocation: sourceMemberRequiresInvocation);
        }
    }

    /// <summary>
    ///     Determines whether a nested reference type in the source is nullable while the matching
    ///     generic argument or array element in the destination is non-nullable. Value-type nullable
    ///     elements and genuine nested type mismatches are intentionally excluded — those stay with
    ///     AM001/AM021 so the rules never double-report.
    /// </summary>
    private static bool HasNullableElementMismatch(ITypeSymbol sourceType, ITypeSymbol destType)
    {
        var visitedOutputTypePairs = new Dictionary<ITypeSymbol, HashSet<ITypeSymbol>>(
            SymbolEqualityComparer.Default);
        var visitedInputTypePairs = new Dictionary<ITypeSymbol, HashSet<ITypeSymbol>>(
            SymbolEqualityComparer.Default);
        return HasNullableElementMismatch(
            sourceType,
            destType,
            isOutputPosition: true,
            visitedOutputTypePairs,
            visitedInputTypePairs);
    }

    private static bool HasNullableElementMismatch(
        ITypeSymbol sourceType,
        ITypeSymbol destType,
        bool isOutputPosition,
        Dictionary<ITypeSymbol, HashSet<ITypeSymbol>> visitedOutputTypePairs,
        Dictionary<ITypeSymbol, HashSet<ITypeSymbol>> visitedInputTypePairs)
    {
        Dictionary<ITypeSymbol, HashSet<ITypeSymbol>> visitedTypePairs =
            isOutputPosition ? visitedOutputTypePairs : visitedInputTypePairs;
        if (!AutoMapperAnalysisHelpers.TryVisitTypePair(sourceType, destType, visitedTypePairs))
        {
            return false;
        }

        bool hasSameRuntimeType = SymbolEqualityComparer.Default.Equals(sourceType, destType);

        if (hasSameRuntimeType &&
            sourceType is IArrayTypeSymbol sourceArray &&
            destType is IArrayTypeSymbol destinationArray)
        {
            return (isOutputPosition &&
                    HasNullableReferenceMismatch(sourceArray.ElementType, destinationArray.ElementType)) ||
                   HasNullableElementMismatch(
                       sourceArray.ElementType,
                       destinationArray.ElementType,
                       isOutputPosition,
                       visitedOutputTypePairs,
                       visitedInputTypePairs);
        }

        if (hasSameRuntimeType &&
            sourceType is INamedTypeSymbol sourceNamedType &&
            destType is INamedTypeSymbol destinationNamedType)
        {
            for (int index = 0; index < sourceNamedType.TypeArguments.Length; index++)
            {
                ITypeSymbol sourceArgument = sourceNamedType.TypeArguments[index];
                ITypeSymbol destinationArgument = destinationNamedType.TypeArguments[index];
                bool argumentIsOutputPosition =
                    sourceNamedType.OriginalDefinition.TypeParameters[index].Variance == VarianceKind.In
                        ? !isOutputPosition
                        : isOutputPosition;
                if ((argumentIsOutputPosition &&
                     HasNullableReferenceMismatch(sourceArgument, destinationArgument)) ||
                    HasNullableElementMismatch(
                        sourceArgument,
                        destinationArgument,
                        argumentIsOutputPosition,
                        visitedOutputTypePairs,
                        visitedInputTypePairs))
                {
                    return true;
                }
            }
        }

        if (isOutputPosition &&
            AutoMapperAnalysisHelpers.IsCollectionType(sourceType) &&
            AutoMapperAnalysisHelpers.IsCollectionType(destType))
        {
            ITypeSymbol? sourceElement = AutoMapperAnalysisHelpers.GetCollectionElementType(sourceType);
            ITypeSymbol? destinationElement = AutoMapperAnalysisHelpers.GetCollectionElementType(destType);
            return sourceElement != null &&
                   destinationElement != null &&
                   (HasNullableReferenceMismatch(sourceElement, destinationElement) ||
                    HasNullableElementMismatch(
                        sourceElement,
                        destinationElement,
                        isOutputPosition: true,
                        visitedOutputTypePairs,
                        visitedInputTypePairs));
        }

        return false;
    }

    private static bool HasNullableReferenceMismatch(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        return sourceType.IsReferenceType &&
               destinationType.IsReferenceType &&
               IsNullableType(sourceType) &&
               !IsNullableType(destinationType) &&
               (SymbolEqualityComparer.Default.Equals(sourceType, destinationType) ||
                HaveCompatibleCollectionShapes(sourceType, destinationType));
    }

    private static bool HaveCompatibleCollectionShapes(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        var visitedTypePairs = new Dictionary<ITypeSymbol, HashSet<ITypeSymbol>>(
            SymbolEqualityComparer.Default);
        return HaveCompatibleCollectionShapes(sourceType, destinationType, visitedTypePairs);
    }

    private static bool HaveCompatibleCollectionShapes(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        Dictionary<ITypeSymbol, HashSet<ITypeSymbol>> visitedTypePairs)
    {
        if (!AutoMapperAnalysisHelpers.TryVisitTypePair(sourceType, destinationType, visitedTypePairs))
        {
            return true;
        }

        if (!AutoMapperAnalysisHelpers.IsCollectionType(sourceType) ||
            !AutoMapperAnalysisHelpers.IsCollectionType(destinationType))
        {
            return false;
        }

        ITypeSymbol? sourceElement = AutoMapperAnalysisHelpers.GetCollectionElementType(sourceType);
        ITypeSymbol? destinationElement = AutoMapperAnalysisHelpers.GetCollectionElementType(destinationType);
        return sourceElement != null &&
               destinationElement != null &&
               (SymbolEqualityComparer.Default.Equals(sourceElement, destinationElement) ||
                HaveCompatibleCollectionShapes(sourceElement, destinationElement, visitedTypePairs));
    }

    private static void ReportNullableToNonNullableDiagnostic(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        string destinationPropertyName,
        string sourcePropertyName,
        ITypeSymbol sourcePropertyType,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        ITypeSymbol destinationPropertyType,
        bool isElementNullability = false,
        bool sourceMemberRequiresInvocation = false)
    {
        string sourceTypeName = sourcePropertyType.ToDisplayString();
        string destTypeName = destinationPropertyType.ToDisplayString();

        var properties = ImmutableDictionary.CreateBuilder<string, string?>();
        properties.Add(PropertyNamePropertyName, destinationPropertyName);
        properties.Add(SourcePropertyNamePropertyName, sourcePropertyName);
        properties.Add(SourcePropertyTypePropertyName, sourceTypeName);
        properties.Add(DestinationPropertyTypePropertyName, destTypeName);
        if (isElementNullability)
        {
            properties.Add(ElementNullabilityPropertyName, "true");
        }
        if (sourceMemberRequiresInvocation)
        {
            properties.Add(SourceMemberRequiresInvocationPropertyName, "true");
        }

        var diagnostic = Diagnostic.Create(
            NullableToNonNullableRule,
            invocation.GetLocation(),
            properties.ToImmutable(),
            destinationPropertyName,
            GetDiagnosticTypeName(sourceType),
            sourcePropertyName,
            sourceTypeName,
            GetDiagnosticTypeName(destinationType),
            destTypeName
        );
        context.ReportDiagnostic(diagnostic);
    }

    private static string GetDiagnosticTypeName(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { IsGenericType: true } genericType)
        {
            return genericType.ToDisplayString(new SymbolDisplayFormat(
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes));
        }

        return AutoMapperAnalysisHelpers.GetTypeName(type);
    }

    private static bool IsNullableToNonNullableCompatible(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        if (!IsNullableType(sourceType) || IsNullableType(destinationType))
        {
            return false;
        }

        return AreUnderlyingTypesCompatible(
            AutoMapperAnalysisHelpers.GetUnderlyingType(sourceType),
            AutoMapperAnalysisHelpers.GetUnderlyingType(destinationType));
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


    private static bool IsAutoMapperCreateMapInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        return MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "CreateMap");
    }


}

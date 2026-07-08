using System.Collections.Immutable;
using System.Composition;
using System.Text.RegularExpressions;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Analyzers.TypeSafety;

/// <summary>
///     Code fix provider for AM001 Property Type Mismatch diagnostics.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM001_PropertyTypeMismatchCodeFixProvider))]
[Shared]
public class AM001_PropertyTypeMismatchCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <summary>
    ///     Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM001");

    /// <summary>
    ///     Registers code fixes for the diagnostics. Multi-property CreateMaps get Convert-all /
    ///     Ignore-all aggregates (when every property has a conversion) plus a nested submenu.
    ///     Sibling mismatches are recomputed from the CreateMap (AM011-style) so property-token
    ///     diagnostics still offer aggregates when the IDE context holds only one diagnostic.
    /// </summary>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        CodeFixOperationContext? operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        foreach (DiagnosticInvocationGroup group in GroupDiagnosticsByInvocation(
                     operationContext.Root,
                     context.Diagnostics,
                     AM001_PropertyTypeMismatchAnalyzer.PropertyNamePropertyName))
        {
            // Expand to every convention type-mismatch on this map (not only diags in the caret context).
            List<string> allMismatched = GetMismatchedPropertyNames(operationContext, group.Invocation);
            if (allMismatched.Count == 0)
            {
                allMismatched = group.PropertyNames.ToList();
            }

            var expandedGroup = new DiagnosticInvocationGroup(
                group.Invocation,
                group.Diagnostics,
                allMismatched);

            bool isBatch = expandedGroup.PropertyNames.Count >= 2;
            var perPropertySubMenus = new List<CodeAction>();

            foreach (string propertyName in expandedGroup.PropertyNames)
            {
                (string SubMenuTitle, ImmutableArray<CodeAction> Actions)? built =
                    BuildPerPropertyActions(context, operationContext, expandedGroup, propertyName);
                if (built == null || built.Value.Actions.IsDefaultOrEmpty)
                {
                    continue;
                }

                if (isBatch)
                {
                    perPropertySubMenus.Add(
                        CodeAction.Create(built.Value.SubMenuTitle, built.Value.Actions, isInlinable: false));
                }
                else
                {
                    foreach (CodeAction action in built.Value.Actions)
                    {
                        // Register against every diagnostic in the context (typically one property token).
                        context.RegisterCodeFix(action, group.Diagnostics);
                    }
                }
            }

            if (!isBatch)
            {
                continue;
            }

            foreach (CodeAction aggregateAction in BuildAggregateActions(context, operationContext, expandedGroup))
            {
                context.RegisterCodeFix(aggregateAction, group.Diagnostics);
            }

            if (perPropertySubMenus.Count > 0)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "Fix individual type mismatch…",
                        perPropertySubMenus.ToImmutableArray(),
                        isInlinable: false),
                    group.Diagnostics);
            }
        }
    }

    private static List<string> GetMismatchedPropertyNames(
        CodeFixOperationContext operationContext,
        InvocationExpressionSyntax invocation)
    {
        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) =
            ResolveCreateMapTypesWithReverse(invocation, operationContext.SemanticModel);
        if (sourceType == null || destinationType == null)
        {
            return [];
        }

        var names = new List<string>();
        foreach (IPropertySymbol sourceProperty in
                 AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false))
        {
            IPropertySymbol? destProperty = FindProperty(destinationType, sourceProperty.Name, true);
            if (destProperty == null)
            {
                continue;
            }

            if (AM020MappingConfigurationHelpers.IsDestinationPropertyExplicitlyConfigured(
                    invocation, destProperty.Name, operationContext.SemanticModel))
            {
                continue;
            }

            if (AM001_PropertyTypeMismatchAnalyzer.WouldReportPropertyTypeMismatch(
                    operationContext.SemanticModel.Compilation,
                    sourceProperty.Type,
                    destProperty.Type))
            {
                names.Add(sourceProperty.Name);
            }
        }

        return names;
    }

    private (string SubMenuTitle, ImmutableArray<CodeAction> Actions)? BuildPerPropertyActions(
        CodeFixContext context,
        CodeFixOperationContext operationContext,
        DiagnosticInvocationGroup group,
        string propertyName)
    {
        if (!TryBuildConversion(operationContext, group.Invocation, propertyName, out string? conversionExpression))
        {
            return null;
        }

        var actions = ImmutableArray.CreateBuilder<CodeAction>();
        if (conversionExpression != null)
        {
            string expression = conversionExpression;
            actions.Add(CodeAction.Create(
                $"Map '{propertyName}' with conversion",
                _ => ReplaceNodeAsync(
                    context.Document,
                    operationContext.Root,
                    group.Invocation,
                    CodeFixSyntaxHelper.CreateForMemberWithMapFrom(group.Invocation, propertyName, expression)),
                $"AM001_MapWithConversion_{propertyName}"));
        }

        actions.Add(CodeAction.Create(
            $"Ignore property '{propertyName}' (manual review)",
            _ => ReplaceNodeAsync(
                context.Document,
                operationContext.Root,
                group.Invocation,
                CodeFixSyntaxHelper.CreateForMemberWithIgnore(group.Invocation, propertyName)),
            $"AM001_Ignore_{propertyName}"));

        return ($"Property '{propertyName}'", actions.ToImmutable());
    }

    private ImmutableArray<CodeAction> BuildAggregateActions(
        CodeFixContext context,
        CodeFixOperationContext operationContext,
        DiagnosticInvocationGroup group)
    {
        if (group.PropertyNames.Count < 2)
        {
            return ImmutableArray<CodeAction>.Empty;
        }

        InvocationExpressionSyntax invocation = group.Invocation;
        SyntaxNode root = operationContext.Root;
        int count = group.PropertyNames.Count;
        var actions = ImmutableArray.CreateBuilder<CodeAction>();

        var conversions = group.PropertyNames
            .Select(name =>
            {
                TryBuildConversion(operationContext, invocation, name, out string? expression);
                return (Name: name, Expression: expression);
            })
            .ToList();

        // Convert-all only when every flagged property has a known conversion recipe (honest title).
        if (conversions.All(c => c.Expression != null))
        {
            List<PropertyFixSpec> mapSpecs = conversions
                .Select(c => PropertyFixSpec.MapFrom(c.Name, c.Expression!))
                .ToList();
            actions.Add(CodeAction.Create(
                $"Convert all {count} type mismatches",
                _ => ReplaceNodeAsync(
                    context.Document, root, invocation, FoldAggregateForMembers(invocation, mapSpecs)),
                "AM001_ConvertAll"));
        }

        List<PropertyFixSpec> ignoreSpecs = group.PropertyNames
            .Select(PropertyFixSpec.Ignore)
            .ToList();
        actions.Add(CodeAction.Create(
            $"Ignore all {count} type mismatches (manual review)",
            _ => ReplaceNodeAsync(
                context.Document, root, invocation, FoldAggregateForMembers(invocation, ignoreSpecs)),
            "AM001_IgnoreAll"));

        return actions.ToImmutable();
    }

    private static bool TryBuildConversion(
        CodeFixOperationContext operationContext,
        InvocationExpressionSyntax invocation,
        string propertyName,
        out string? conversionExpression)
    {
        conversionExpression = null;
        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) =
            ResolveCreateMapTypesWithReverse(invocation, operationContext.SemanticModel);
        if (sourceType == null || destinationType == null)
        {
            return false;
        }

        IPropertySymbol? sourceProperty = FindProperty(sourceType, propertyName, false);
        IPropertySymbol? destinationProperty = FindProperty(destinationType, propertyName, true);
        if (sourceProperty == null || destinationProperty == null)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(sourceProperty.Type, destinationProperty.Type))
        {
            return false;
        }

        conversionExpression =
            CreateConversionExpression(sourceProperty.Type, destinationProperty.Type, propertyName);
        // Still offer Ignore even when no conversion is known.
        return true;
    }

    private static IPropertySymbol? FindProperty(ITypeSymbol typeSymbol, string name, bool expectSetter)
    {
        if (typeSymbol is not INamedTypeSymbol namedType)
        {
            return null;
        }

        return AutoMapperAnalysisHelpers
            .GetMappableProperties(namedType, requireSetter: expectSetter)
            .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string? CreateConversionExpression(ITypeSymbol sourceType, ITypeSymbol destinationType,
        string propertyName)
    {
        if (SymbolEqualityComparer.Default.Equals(sourceType, destinationType))
        {
            return null;
        }

        string escapedPropertyName = CodeFixSyntaxHelper.EscapeIdentifier(propertyName);
        string srcMember = $"src.{escapedPropertyName}";

        // Peel Nullable<T> / nullable annotations so conversion recipes key off underlying SpecialTypes.
        ITypeSymbol sourceUnderlying = AutoMapperAnalysisHelpers.GetUnderlyingType(sourceType);
        ITypeSymbol destinationUnderlying = AutoMapperAnalysisHelpers.GetUnderlyingType(destinationType);
        bool sourceIsNullable = IsNullableType(sourceType);
        bool destinationIsNullable = IsNullableType(destinationType);

        string destinationTypeName =
            destinationUnderlying.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        string destinationDisplayForCast =
            destinationType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        const string invariantCulture = "global::System.Globalization.CultureInfo.InvariantCulture";

        // Numeric conversions: add cast (after peeling nullable wrappers).
        if (IsNumericConversion(sourceUnderlying.SpecialType) &&
            IsNumericConversion(destinationUnderlying.SpecialType))
        {
            if (!SymbolEqualityComparer.Default.Equals(sourceUnderlying, destinationUnderlying))
            {
                if (sourceIsNullable && !destinationIsNullable)
                {
                    // Coalesce must match the source nullable's underlying type so `src.X ?? fallback`
                    // compiles before the outer cast (e.g. double?→decimal uses 0.0, not 0m).
                    string sourceTypeName =
                        sourceUnderlying.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    string fallback = TypeConversionHelper.GetDefaultValueForType(sourceTypeName);
                    return $"({destinationTypeName})({srcMember} ?? {fallback})";
                }

                if (sourceIsNullable)
                {
                    return $"{srcMember}.HasValue ? ({destinationTypeName}){srcMember}.Value : null";
                }

                return $"({destinationDisplayForCast}){srcMember}";
            }

            // Same underlying numeric type, nullable → non-nullable.
            if (sourceIsNullable && !destinationIsNullable)
            {
                string fallback = TypeConversionHelper.GetDefaultValueForType(destinationTypeName);
                return $"{srcMember} ?? {fallback}";
            }
        }

        // String -> numeric: Parse with invariant culture.
        if (IsString(sourceUnderlying) && IsNumericConversion(destinationUnderlying.SpecialType))
        {
            string fallback = GetNullSourceFallback(destinationTypeName, destinationIsNullable);
            return
                $"{srcMember} != null ? {destinationTypeName}.Parse({srcMember}, {invariantCulture}) : {fallback}";
        }

        // Numeric -> string: invariant culture ToString.
        if (IsNumericConversion(sourceUnderlying.SpecialType) && IsString(destinationUnderlying))
        {
            if (sourceIsNullable)
            {
                return $"{srcMember}.HasValue ? {srcMember}.Value.ToString({invariantCulture}) : string.Empty";
            }

            return $"{srcMember}.ToString({invariantCulture})";
        }

        // Enum -> string
        if (sourceUnderlying.TypeKind == TypeKind.Enum && IsString(destinationUnderlying))
        {
            if (sourceIsNullable)
            {
                return $"{srcMember}.HasValue ? {srcMember}.Value.ToString() : string.Empty";
            }

            return $"{srcMember}.ToString()";
        }

        // String -> enum
        if (IsString(sourceUnderlying) && destinationUnderlying.TypeKind == TypeKind.Enum)
        {
            string fullyQualifiedDestinationTypeName =
                destinationUnderlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string fallback = destinationIsNullable
                ? $"default({fullyQualifiedDestinationTypeName}?)"
                : "default";
            return
                $"{srcMember} != null ? global::System.Enum.Parse<{fullyQualifiedDestinationTypeName}>({srcMember}) : {fallback}";
        }

        // bool <-> string
        if (sourceUnderlying.SpecialType == SpecialType.System_Boolean && IsString(destinationUnderlying))
        {
            if (sourceIsNullable)
            {
                return $"{srcMember}.HasValue ? {srcMember}.Value.ToString() : string.Empty";
            }

            return $"{srcMember}.ToString()";
        }

        if (IsString(sourceUnderlying) && destinationUnderlying.SpecialType == SpecialType.System_Boolean)
        {
            string fallback = GetNullSourceFallback("bool", destinationIsNullable);
            return $"{srcMember} != null ? bool.Parse({srcMember}) : {fallback}";
        }

        // char -> string
        if (sourceUnderlying.SpecialType == SpecialType.System_Char && IsString(destinationUnderlying))
        {
            if (sourceIsNullable)
            {
                return $"{srcMember}.HasValue ? {srcMember}.Value.ToString() : string.Empty";
            }

            return $"{srcMember}.ToString()";
        }

        // Framework scalar <-> string (DateTime, Guid, Uri, DateOnly, TimeOnly, ...)
        if (TryCreateFrameworkStringConversion(
                sourceUnderlying,
                destinationUnderlying,
                srcMember,
                sourceIsNullable,
                destinationIsNullable,
                out string? frameworkConversion))
        {
            return frameworkConversion;
        }

        // Nullable source to non-nullable destination where underlying types are compatible.
        if (sourceIsNullable &&
            !destinationIsNullable &&
            AutoMapperAnalysisHelpers.AreTypesCompatible(sourceUnderlying, destinationUnderlying))
        {
            string fallback = TypeConversionHelper.GetDefaultValueForType(destinationTypeName);
            return $"{srcMember} ?? {fallback}";
        }

        return null;
    }

    private static bool TryCreateFrameworkStringConversion(
        ITypeSymbol sourceUnderlying,
        ITypeSymbol destinationUnderlying,
        string srcMember,
        bool sourceIsNullable,
        bool destinationIsNullable,
        out string? expression)
    {
        expression = null;

        if (IsString(destinationUnderlying) && IsFrameworkToStringType(sourceUnderlying))
        {
            bool needsInvariant = RequiresInvariantCultureFormat(sourceUnderlying);
            const string invariantCulture = "global::System.Globalization.CultureInfo.InvariantCulture";
            if (sourceIsNullable)
            {
                // TimeSpan/Guid/Uri value types use HasValue; reference Uri uses null check via IsNullableType.
                bool isNullableValueType = sourceUnderlying.IsValueType;
                if (needsInvariant && isNullableValueType)
                {
                    expression =
                        $"{srcMember}.HasValue ? {srcMember}.Value.ToString({invariantCulture}) : string.Empty";
                }
                else if (isNullableValueType)
                {
                    expression =
                        $"{srcMember}.HasValue ? {srcMember}.Value.ToString() : string.Empty";
                }
                else
                {
                    expression = $"{srcMember} != null ? {srcMember}.ToString() : string.Empty";
                }
            }
            else
            {
                expression = needsInvariant
                    ? $"{srcMember}.ToString({invariantCulture})"
                    : $"{srcMember}.ToString()";
            }

            return true;
        }

        if (IsString(sourceUnderlying) &&
            IsFrameworkParseType(destinationUnderlying, out string parseCall, out bool parseNeedsInvariantCulture))
        {
            string underlyingName =
                destinationUnderlying.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            string fallback = GetNullSourceFallback(underlyingName, destinationIsNullable);
            string parseArgs = parseNeedsInvariantCulture
                ? $"{srcMember}, global::System.Globalization.CultureInfo.InvariantCulture"
                : srcMember;
            expression = $"{srcMember} != null ? {parseCall}({parseArgs}) : {fallback}";
            return true;
        }

        return false;
    }

    private static string GetNullSourceFallback(string destinationTypeName, bool destinationIsNullable)
    {
        if (!destinationIsNullable)
        {
            return TypeConversionHelper.GetDefaultValueForType(destinationTypeName);
        }

        // Bare `null` breaks AutoMapper expression-tree MapFrom type inference for nullable
        // value destinations (CS1660). `default(T?)` preserves null without losing type.
        return $"default({destinationTypeName}?)";
    }

    private static bool IsFrameworkToStringType(ITypeSymbol type)
    {
        if (type.SpecialType is SpecialType.System_DateTime or SpecialType.System_Boolean or SpecialType.System_Char)
        {
            return true;
        }

        return type.ContainingNamespace?.ToDisplayString() == "System" &&
               type.Name is "DateTimeOffset" or "DateOnly" or "TimeOnly" or "TimeSpan" or "Guid" or "Uri";
    }

    private static bool RequiresInvariantCultureFormat(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_DateTime)
        {
            return true;
        }

        return type.ContainingNamespace?.ToDisplayString() == "System" &&
               type.Name is "DateTimeOffset" or "DateOnly" or "TimeOnly" or "Decimal";
    }

    private static bool IsFrameworkParseType(
        ITypeSymbol type,
        out string parseCall,
        out bool needsInvariantCulture)
    {
        parseCall = string.Empty;
        needsInvariantCulture = false;

        if (type.SpecialType == SpecialType.System_DateTime)
        {
            parseCall = "global::System.DateTime.Parse";
            needsInvariantCulture = true;
            return true;
        }

        if (type.ContainingNamespace?.ToDisplayString() == "System")
        {
            switch (type.Name)
            {
                case "Guid":
                    parseCall = "global::System.Guid.Parse";
                    return true;
                case "DateTimeOffset":
                    parseCall = "global::System.DateTimeOffset.Parse";
                    needsInvariantCulture = true;
                    return true;
                case "DateOnly":
                    parseCall = "global::System.DateOnly.Parse";
                    needsInvariantCulture = true;
                    return true;
                case "TimeOnly":
                    parseCall = "global::System.TimeOnly.Parse";
                    needsInvariantCulture = true;
                    return true;
                case "Uri":
                    parseCall = "new global::System.Uri";
                    return true;
            }
        }

        return false;
    }

    private static bool IsNumericConversion(SpecialType specialType)
    {
        return specialType is SpecialType.System_Byte or SpecialType.System_SByte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 or
            SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int64 or SpecialType.System_UInt64 or
            SpecialType.System_Single or SpecialType.System_Double or
            SpecialType.System_Decimal;
    }

    private static bool IsString(ITypeSymbol typeSymbol)
    {
        return typeSymbol.SpecialType == SpecialType.System_String;
    }

    private static bool IsNullableType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return true;
        }

        return type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.Annotated;
    }

    private static string? ExtractPropertyNameFromDiagnostic(Diagnostic diagnostic)
    {
        if (diagnostic.Properties.TryGetValue(AM001_PropertyTypeMismatchAnalyzer.PropertyNamePropertyName,
                out string? propertyName))
        {
            return propertyName;
        }

        // Fallback: extract from diagnostic message
        string message = diagnostic.GetMessage();
        Match match = Regex.Match(message, @"Property '(\w+)'");
        return match.Success ? match.Groups[1].Value : null;
    }
}

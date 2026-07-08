using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.TypeSafety;

/// <summary>
///     Code fix provider for AM003 diagnostic - Collection Type Incompatibility.
///     Provides fixes for collection type mismatches and element type conversions.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM003_CollectionTypeIncompatibilityCodeFixProvider))]
[Shared]
public class AM003_CollectionTypeIncompatibilityCodeFixProvider : AutoMapperCodeFixProviderBase
{
    private const string LegacySourceTypePropertyName = "SourceType";
    private const string LegacyDestinationTypePropertyName = "DestType";

    /// <summary>
    ///     Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ["AM003"];

    /// <summary>
    ///     Registers code fixes for AM003 diagnostics.
    /// </summary>
    /// <param name="context">The code fix context containing diagnostic information.</param>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        ImmutableArray<Diagnostic> diagnostics = context.Diagnostics
            .Where(diag =>
                diag.Id == "AM003" &&
                diag.Location.IsInSource &&
                diag.Location.SourceTree == operationContext.Root.SyntaxTree &&
                diag.Location.SourceSpan.IntersectsWith(context.Span))
            .ToImmutableArray();

        foreach (Diagnostic diagnostic in diagnostics)
        {
            if (!TryGetStringProperty(diagnostic, AM003_CollectionTypeIncompatibilityAnalyzer.PropertyNamePropertyName,
                    out string? safePropertyName))
            {
                continue;
            }

            InvocationExpressionSyntax? invocation = GetCreateMapInvocation(operationContext.Root, diagnostic);
            if (invocation == null)
            {
                continue;
            }

            string? safeSourceType = TryGetStringProperty(diagnostic,
                    AM003_CollectionTypeIncompatibilityAnalyzer.SourcePropertyTypePropertyName, out string? sourceType)
                ? sourceType
                : TryGetStringProperty(diagnostic, LegacySourceTypePropertyName, out string? legacySourceType)
                    ? legacySourceType
                    : null;
            string? safeDestType = TryGetStringProperty(diagnostic,
                    AM003_CollectionTypeIncompatibilityAnalyzer.DestinationPropertyTypePropertyName,
                    out string? destinationType)
                ? destinationType
                : TryGetStringProperty(diagnostic, LegacyDestinationTypePropertyName, out string? legacyDestinationType)
                    ? legacyDestinationType
                    : null;

            if (!TryGetStringProperty(diagnostic, AM003_CollectionTypeIncompatibilityAnalyzer.SourceElementTypePropertyName,
                    out string? safeSourceElementType) ||
                !TryGetStringProperty(diagnostic,
                    AM003_CollectionTypeIncompatibilityAnalyzer.DestinationElementTypePropertyName,
                    out string? safeDestElementType))
            {
                continue;
            }

            if (string.IsNullOrEmpty(safeSourceType) || string.IsNullOrEmpty(safeDestType))
            {
                continue;
            }

            string propertyName = safePropertyName!;
            string sourceCollectionType = safeSourceType!;
            string destinationCollectionType = safeDestType!;
            string sourceElementType = safeSourceElementType!;
            string destinationElementType = safeDestElementType!;

            bool elementImplicitlyConvertible = TryGetStringProperty(diagnostic,
                    AM003_CollectionTypeIncompatibilityAnalyzer.ElementImplicitlyConvertiblePropertyName,
                    out string? convertibleFlag) &&
                string.Equals(convertibleFlag, "true", StringComparison.Ordinal);

            if (diagnostic.Descriptor == AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule)
            {
                (string Title, string Expression, bool RequiresLinq, string EquivalenceKey)[] fixes =
                    CreateCollectionFixes(propertyName, sourceCollectionType, destinationCollectionType, sourceElementType,
                        destinationElementType, elementImplicitlyConvertible)
                    .ToArray();

                if (fixes.Length == 0)
                {
                    RegisterIgnoreFix(context, operationContext.Root, invocation, propertyName, diagnostic);
                    continue;
                }

                foreach ((string Title, string Expression, bool RequiresLinq, string EquivalenceKey) fix in fixes)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            fix.Title,
                            ct => AddMapFromAsync(context.Document, operationContext.Root, invocation, propertyName,
                                fix.Expression, fix.RequiresLinq, ct),
                            fix.EquivalenceKey),
                        diagnostic);
                }

                RegisterIgnoreFix(context, operationContext.Root, invocation, propertyName, diagnostic);
            }
            else
            {
                string conversionLambda = GetElementConversion(sourceElementType, destinationElementType);
                if (!string.IsNullOrEmpty(conversionLambda))
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            $"Convert {propertyName} elements using Select()",
                            ct => AddMapFromAsync(
                                context.Document,
                                operationContext.Root,
                                invocation,
                                propertyName,
                                $"src.{propertyName}.Select({conversionLambda})",
                                true,
                                ct),
                            $"AM003_Select_{propertyName}"),
                        diagnostic);
                }

                RegisterIgnoreFix(context, operationContext.Root, invocation, propertyName, diagnostic);
            }
        }
    }

    private void RegisterIgnoreFix(
        CodeFixContext context,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        string propertyName,
        Diagnostic diagnostic)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                $"Ignore property '{propertyName}' (manual review)",
                ct => AddIgnoreAsync(context.Document, root, invocation, propertyName),
                $"AM003_Ignore_{propertyName}"),
            diagnostic);
    }

    private static bool TryGetStringProperty(Diagnostic diagnostic, string key, out string? value)
    {
        if (diagnostic.Properties.TryGetValue(key, out string? propertyValue) &&
            !string.IsNullOrWhiteSpace(propertyValue))
        {
            value = propertyValue;
            return true;
        }

        value = null;
        return false;
    }

    private async Task<Document> AddMapFromAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        string propertyName,
        string mapFromExpression,
        bool requiresLinq,
        CancellationToken cancellationToken)
    {
        InvocationExpressionSyntax newInvocation =
            CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName, mapFromExpression);
        SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);

        if (requiresLinq && newRoot is CompilationUnitSyntax updatedCompilationUnit)
        {
            newRoot = CodeFixSyntaxHelper.AddUsingIfMissing(updatedCompilationUnit, "System.Linq");
        }

        return await Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private Task<Document> AddIgnoreAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        string propertyName)
    {
        InvocationExpressionSyntax newInvocation =
            CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
        return ReplaceNodeAsync(document, root, invocation, newInvocation);
    }

    private static IEnumerable<(string Title, string Expression, bool RequiresLinq, string EquivalenceKey)>
        CreateCollectionFixes(
            string propertyName,
            string sourceType,
            string destType,
            string sourceElementType,
            string destElementType,
            bool elementImplicitlyConvertible)
    {
        var fixes = new List<(string Title, string Expression, bool RequiresLinq, string EquivalenceKey)>();
        string simplifiedDestType = SimplifyCollectionType(destType);

        // Determine if element conversion is needed
        string elementConversionLambda = GetElementConversion(sourceElementType, destElementType);
        bool elementsDiffer = !string.Equals(
            TypeConversionHelper.NormalizeTypeName(sourceElementType),
            TypeConversionHelper.NormalizeTypeName(destElementType),
            StringComparison.Ordinal);

        if (elementsDiffer && string.IsNullOrEmpty(elementConversionLambda))
        {
            // No named conversion. A cast is only safe when the source element is implicitly convertible
            // to the destination (e.g. a derived-to-base upcast); for unrelated types a speculative
            // (Dest)x cast would fail to compile or throw, so withhold and let the caller offer manual
            // review.
            if (!elementImplicitlyConvertible)
            {
                return fixes;
            }

            elementConversionLambda = $"x => ({destElementType})x";
        }

        bool needsElementConversion =
            !string.IsNullOrEmpty(elementConversionLambda) && elementConversionLambda != "x => x";

        // Helper to build expression with optional element conversion
        string BuildExpression(string collectionConversion, bool isConstructor = false)
        {
            if (needsElementConversion)
            {
                if (isConstructor)
                {
                    // new List<T>(src.Prop.Select(x => ...))
                    return collectionConversion.Replace($"src.{propertyName}",
                        $"src.{propertyName}.Select({elementConversionLambda})");
                }

                // src.Prop.Select(x => ...).ToList()
                return $"src.{propertyName}.Select({elementConversionLambda}).{collectionConversion.Split('.').Last()}";
            }

            return collectionConversion;
        }

        if (TryCreateDestinationInterfaceFix(
                propertyName,
                destType,
                needsElementConversion,
                elementConversionLambda,
                out (string Title, string Expression, bool RequiresLinq, string EquivalenceKey) interfaceFix))
        {
            fixes.Add(interfaceFix);
            return fixes;
        }

        if (Contains(sourceType, "HashSet") && IsKnownConcreteCollectionType(destType, "List"))
        {
            fixes.Add((
                $"Convert {propertyName} using ToList()",
                BuildExpression($"src.{propertyName}.ToList()"),
                true, // Always true if using ToList or Select
                $"AM003_ToList_{propertyName}"));
        }

        if (Contains(sourceType, "Queue") && IsKnownConcreteCollectionType(destType, "List"))
        {
            (string title, string expr, _, string key) = CreateConstructorFix(propertyName, simplifiedDestType);
            fixes.Add((title, BuildExpression(expr, true), needsElementConversion, key));
        }

        if (Contains(sourceType, "Stack") && IsKnownConcreteCollectionType(destType, "List"))
        {
            (string title, string expr, _, string key) = CreateConstructorFix(propertyName, simplifiedDestType);
            fixes.Add((title, BuildExpression(expr, true), needsElementConversion, key));
        }

        if (Contains(sourceType, "List") && IsKnownConcreteCollectionType(destType, "Queue"))
        {
            (string title, string expr, _, string key) = CreateConstructorFix(propertyName, simplifiedDestType);
            fixes.Add((title, BuildExpression(expr, true), needsElementConversion, key));
        }

        if (Contains(sourceType, "IEnumerable") &&
            (IsKnownConcreteCollectionType(destType, "Stack") || IsKnownConcreteCollectionType(destType, "HashSet")))
        {
            (string title, string expr, _, string key) = CreateConstructorFix(propertyName, simplifiedDestType);
            fixes.Add((title, BuildExpression(expr, true), needsElementConversion, key));
        }

        if (TryCreateImmutableOrFrozenDestinationFix(
                propertyName,
                destType,
                needsElementConversion,
                elementConversionLambda,
                out (string Title, string Expression, bool RequiresLinq, string EquivalenceKey) immutableFix))
        {
            fixes.Add(immutableFix);
            return fixes;
        }

        if (IsArrayType(sourceType) && Contains(destType, "IEnumerable"))
        {
            // Arrays are IEnumerable, so AsEnumerable is fine, but if we need element conversion, we need Select
            string expr = needsElementConversion
                ? $"src.{propertyName}.Select({elementConversionLambda})"
                : $"src.{propertyName}.AsEnumerable()";

            fixes.Add((
                $"Convert {propertyName} using {(needsElementConversion ? "Select" : "AsEnumerable")}()",
                expr,
                true,
                $"AM003_AsEnumerable_{propertyName}"));
        }

        if (!fixes.Any() && IsKnownConstructibleCollectionType(destType))
        {
            (string title, string expr, _, string key) = CreateConstructorFix(propertyName, simplifiedDestType);
            fixes.Add((title, BuildExpression(expr, true), needsElementConversion, key));
        }

        return fixes;

        static (string Title, string Expression, bool RequiresLinq, string EquivalenceKey) CreateConstructorFix(
            string propertyName, string simplifiedDestType)
        {
            string targetType = string.IsNullOrWhiteSpace(simplifiedDestType)
                ? "System.Collections.Generic.List<object>"
                : simplifiedDestType;
            return ($"Convert {propertyName} using collection constructor", $"new {targetType}(src.{propertyName})",
                false, $"AM003_Constructor_{propertyName}");
        }
    }

    private static bool TryCreateDestinationInterfaceFix(
        string propertyName,
        string destinationType,
        bool needsElementConversion,
        string elementConversionLambda,
        out (string Title, string Expression, bool RequiresLinq, string EquivalenceKey) fix)
    {
        if (IsListLikeInterface(destinationType))
        {
            string expression = needsElementConversion
                ? $"src.{propertyName}.Select({elementConversionLambda}).ToList()"
                : $"src.{propertyName}.ToList()";
            fix = (
                $"Convert {propertyName} using ToList()",
                expression,
                true,
                $"AM003_ToList_{propertyName}");
            return true;
        }

        if (IsSetLikeInterface(destinationType) && TryGetGenericArgument(destinationType, out string? elementType))
        {
            string sourceExpression = needsElementConversion
                ? $"src.{propertyName}.Select({elementConversionLambda})"
                : $"src.{propertyName}";
            fix = (
                $"Convert {propertyName} using HashSet constructor",
                $"new global::System.Collections.Generic.HashSet<{elementType}>({sourceExpression})",
                needsElementConversion,
                $"AM003_HashSet_{propertyName}");
            return true;
        }

        fix = default;
        return false;
    }

    private static bool TryCreateImmutableOrFrozenDestinationFix(
        string propertyName,
        string destinationType,
        bool needsElementConversion,
        string elementConversionLambda,
        out (string Title, string Expression, bool RequiresLinq, string EquivalenceKey) fix)
    {
        string sourceExpression = needsElementConversion
            ? $"src.{propertyName}.Select({elementConversionLambda})"
            : $"src.{propertyName}";

        if (IsKnownConcreteCollectionType(destinationType, "ImmutableList", "System.Collections.Immutable."))
        {
            fix = (
                $"Convert {propertyName} using ImmutableList.CreateRange()",
                $"global::System.Collections.Immutable.ImmutableList.CreateRange({sourceExpression})",
                needsElementConversion,
                $"AM003_ImmutableList_{propertyName}");
            return true;
        }

        if (IsKnownConcreteCollectionType(destinationType, "ImmutableArray", "System.Collections.Immutable."))
        {
            fix = (
                $"Convert {propertyName} using ImmutableArray.CreateRange()",
                $"global::System.Collections.Immutable.ImmutableArray.CreateRange({sourceExpression})",
                needsElementConversion,
                $"AM003_ImmutableArray_{propertyName}");
            return true;
        }

        if (IsKnownConcreteCollectionType(destinationType, "ImmutableHashSet", "System.Collections.Immutable."))
        {
            fix = (
                $"Convert {propertyName} using ImmutableHashSet.CreateRange()",
                $"global::System.Collections.Immutable.ImmutableHashSet.CreateRange({sourceExpression})",
                needsElementConversion,
                $"AM003_ImmutableHashSet_{propertyName}");
            return true;
        }

        if (IsKnownConcreteCollectionType(destinationType, "FrozenSet", "System.Collections.Frozen."))
        {
            fix = (
                $"Convert {propertyName} using FrozenSet.ToFrozenSet()",
                $"global::System.Collections.Frozen.FrozenSet.ToFrozenSet({sourceExpression})",
                needsElementConversion,
                $"AM003_FrozenSet_{propertyName}");
            return true;
        }

        fix = default;
        return false;
    }

    private static bool IsListLikeInterface(string typeName)
    {
        return IsGenericCollectionInterface(typeName, "IList") ||
               IsGenericCollectionInterface(typeName, "ICollection") ||
               IsGenericCollectionInterface(typeName, "IReadOnlyList") ||
               IsGenericCollectionInterface(typeName, "IReadOnlyCollection");
    }

    private static bool IsSetLikeInterface(string typeName)
    {
        return IsGenericCollectionInterface(typeName, "ISet") ||
               IsGenericCollectionInterface(typeName, "IReadOnlySet");
    }

    private static bool IsKnownConstructibleCollectionType(string typeName)
    {
        return IsKnownConcreteCollectionType(typeName, "List") ||
               IsKnownConcreteCollectionType(typeName, "HashSet") ||
               IsKnownConcreteCollectionType(typeName, "Queue") ||
               IsKnownConcreteCollectionType(typeName, "Stack") ||
               IsKnownConcreteCollectionType(typeName, "SortedSet") ||
               IsKnownConcreteCollectionType(typeName, "LinkedList");
    }

    private static bool IsKnownConcreteCollectionType(string typeName, string collectionName)
    {
        return IsKnownConcreteCollectionType(typeName, collectionName, "System.Collections.Generic.");
    }

    private static bool IsKnownConcreteCollectionType(string typeName, string collectionName, string namespacePrefix)
    {
        return typeName.StartsWith($"{namespacePrefix}{collectionName}<", StringComparison.Ordinal) ||
               typeName.StartsWith($"{collectionName}<", StringComparison.Ordinal);
    }

    private static bool IsGenericCollectionInterface(string typeName, string interfaceName)
    {
        return typeName.StartsWith($"System.Collections.Generic.{interfaceName}<", StringComparison.Ordinal) ||
               typeName.StartsWith($"{interfaceName}<", StringComparison.Ordinal);
    }

    private static bool TryGetGenericArgument(string typeName, out string? genericArgument)
    {
        int start = typeName.IndexOf('<');
        int end = typeName.LastIndexOf('>');
        if (start < 0 || end <= start + 1)
        {
            genericArgument = null;
            return false;
        }

        genericArgument = typeName.Substring(start + 1, end - start - 1);
        return true;
    }

    private static string GetElementConversion(string sourceElementType, string destElementType)
    {
        string source = TypeConversionHelper.NormalizeTypeName(sourceElementType);
        string destination = TypeConversionHelper.NormalizeTypeName(destElementType);

        if (string.Equals(source, destination, StringComparison.Ordinal))
        {
            return "x => x";
        }

        return (source, destination) switch
        {
            ("string", "int") or ("string", "int32") => "x => int.Parse(x)",
            ("string", "long") or ("string", "int64") => "x => long.Parse(x)",
            ("string", "double") => "x => double.Parse(x)",
            ("string", "decimal") => "x => decimal.Parse(x)",
            ("string", "bool") => "x => bool.Parse(x)",
            ("string", "datetime") => "x => global::System.DateTime.Parse(x)",
            ("string", "guid") => "x => global::System.Guid.Parse(x)",
            ("object", "string") => "x => x != null ? x.ToString() : string.Empty",
            (_, "string") => "x => x != null ? x.ToString() : string.Empty",
            ("double", "int") or ("double", "int32") => "x => global::System.Convert.ToInt32(x)",
            ("double", "long") or ("double", "int64") => "x => global::System.Convert.ToInt64(x)",
            ("float", "int") or ("single", "int") => "x => global::System.Convert.ToInt32(x)",
            // No known safe element conversion. Return empty rather than a speculative ($"({dest})x") cast,
            // which compiles for unrelated types but throws InvalidCastException at runtime; the caller
            // withholds the automatic fix and offers manual review instead.
            _ => string.Empty
        };
    }

    private static bool Contains(string typeName, string value)
    {
        return typeName?.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsArrayType(string typeName)
    {
        return typeName?.EndsWith("[]", StringComparison.Ordinal) == true;
    }

    private static string SimplifyCollectionType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return typeName;
        }

        const string collectionsPrefix = "System.Collections.Generic.";
        const string systemPrefix = "System.";

        if (typeName.StartsWith(collectionsPrefix, StringComparison.Ordinal))
        {
            return typeName.Substring(collectionsPrefix.Length);
        }

        if (typeName.StartsWith(systemPrefix, StringComparison.Ordinal))
        {
            return typeName.Substring(systemPrefix.Length);
        }

        return typeName;
    }

}

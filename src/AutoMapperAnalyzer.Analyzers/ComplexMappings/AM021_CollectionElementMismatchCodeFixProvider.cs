using System.Collections.Immutable;
using System.Composition;
using System.Text.RegularExpressions;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Analyzers.ComplexMappings;

/// <summary>
///     Code fix provider for AM021 Collection Element Mismatch diagnostics.
///     Provides fixes for incompatible collection element types.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM021_CollectionElementMismatchCodeFixProvider))]
[Shared]
public class AM021_CollectionElementMismatchCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <summary>
    ///     Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM021");

    /// <summary>
    ///     Registers code fixes for the diagnostics.
    /// </summary>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        foreach (Diagnostic? diagnostic in context.Diagnostics)
        {
            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

            InvocationExpressionSyntax? invocation = operationContext.Root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(i => i.Span.Contains(diagnosticSpan));

            if (invocation == null)
            {
                continue;
            }

            if (!diagnostic.Properties.TryGetValue("DestinationPropertyName", out string? destinationPropertyName) ||
                string.IsNullOrEmpty(destinationPropertyName))
            {
                diagnostic.Properties.TryGetValue("PropertyName", out destinationPropertyName);
            }

            if (string.IsNullOrEmpty(destinationPropertyName))
            {
                destinationPropertyName = ExtractPropertyNameFromDiagnostic(diagnostic);
                if (string.IsNullOrEmpty(destinationPropertyName))
                {
                    continue;
                }
            }

            string destinationPropertyNameValue = destinationPropertyName!;
            string sourcePropertyName;
            if (diagnostic.Properties.TryGetValue("SourcePropertyName", out string? sourceProperty) &&
                !string.IsNullOrEmpty(sourceProperty))
            {
                sourcePropertyName = sourceProperty!;
            }
            else
            {
                sourcePropertyName = destinationPropertyNameValue;
            }

            string? sourceElementType =
                diagnostic.Properties.TryGetValue("SourceElementType", out string? sourceElemType)
                    ? sourceElemType
                    : ExtractSourceElementTypeFromDiagnostic(diagnostic);
            string? destElementType = diagnostic.Properties.TryGetValue("DestElementType", out string? destElemType)
                ? destElemType
                : ExtractDestElementTypeFromDiagnostic(diagnostic);

            if (string.IsNullOrEmpty(sourceElementType) || string.IsNullOrEmpty(destElementType))
            {
                continue;
            }

            if (AM020MappingConfigurationHelpers.HasCustomConstructionOrConversion(invocation, operationContext.SemanticModel) ||
                AM020MappingConfigurationHelpers.IsDestinationPropertyExplicitlyConfigured(
                    invocation,
                    destinationPropertyNameValue,
                    operationContext.SemanticModel))
            {
                continue;
            }

            // Dictionary destinations carry decomposed key/value axes; offer a ToDictionary rewrite (simple
            // axes) or a decomposed element CreateMap (complex value), instead of the previous ignore-only
            // dead end. The KeyValuePair-guarded simple/complex paths below remain no-ops for dictionaries.
            if (diagnostic.Properties.TryGetValue("IsDictionary", out string? isDictionary) &&
                string.Equals(isDictionary, "true", StringComparison.Ordinal))
            {
                RegisterDictionaryFixes(context, operationContext.Root, invocation, sourcePropertyName,
                    destinationPropertyNameValue, diagnostic, operationContext.SemanticModel);
            }

            // Determine if this is a simple type conversion or complex mapping
            bool isSimpleConversion = IsSimpleTypeConversion(sourceElementType!, destElementType!);

            if (isSimpleConversion)
            {
                RegisterSimpleConversionFix(context, operationContext.Root, invocation, sourcePropertyName, destinationPropertyNameValue,
                    sourceElementType!, destElementType!,
                    diagnostic, operationContext.SemanticModel);
            }
            else
            {
                if (!IsKeyValuePairType(sourceElementType!) && !IsKeyValuePairType(destElementType!))
                {
                    RegisterComplexMappingFix(context, operationContext.Root, invocation, destinationPropertyNameValue, sourceElementType!,
                        destElementType!, diagnostic, operationContext.SemanticModel);
                }
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Ignore property '{destinationPropertyNameValue}' (manual review)",
                    cancellationToken =>
                        AddIgnoreAsync(context.Document, operationContext.Root, invocation, destinationPropertyNameValue),
                    $"AM021_Ignore_{destinationPropertyNameValue}"),
                diagnostic);
        }
    }

    private void RegisterSimpleConversionFix(
        CodeFixContext context,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        string sourcePropertyName,
        string destinationPropertyName,
        string sourceElementType,
        string destElementType,
        Diagnostic diagnostic,
        SemanticModel semanticModel)
    {
        // Same compile-safety gate as dictionary axes: DateTime/Guid.Parse require a string source.
        if (!IsSafeAxisConversion(sourceElementType, destElementType))
        {
            return;
        }

        string conversionMethod = GetConversionMethod(destElementType);
        string? mapFromExpression = CreateSimpleConversionExpression(
            invocation,
            sourcePropertyName,
            destinationPropertyName,
            conversionMethod,
            semanticModel);
        if (mapFromExpression == null)
        {
            return;
        }

        string shortConversion = GetShortConversionLabel(conversionMethod);
        context.RegisterCodeFix(
            CodeAction.Create(
                $"Convert '{destinationPropertyName}' elements with {shortConversion}",
                cancellationToken =>
                    AddMapFromWithLinqAsync(context.Document, root, invocation, destinationPropertyName, mapFromExpression),
                $"AM021_SimpleConversion_{destinationPropertyName}"),
            diagnostic);
    }

    private static string GetShortConversionLabel(string conversionMethod)
    {
        // e.g. global::System.Convert.ToInt32 → ToInt32; global::System.DateTime.Parse → DateTime.Parse
        const string convertPrefix = "global::System.Convert.";
        if (conversionMethod.StartsWith(convertPrefix, StringComparison.Ordinal))
        {
            return conversionMethod.Substring(convertPrefix.Length);
        }

        const string systemPrefix = "global::System.";
        if (conversionMethod.StartsWith(systemPrefix, StringComparison.Ordinal))
        {
            return conversionMethod.Substring(systemPrefix.Length);
        }

        return conversionMethod;
    }

    private void RegisterDictionaryFixes(
        CodeFixContext context,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        string sourcePropertyName,
        string destinationPropertyName,
        Diagnostic diagnostic,
        SemanticModel semanticModel)
    {
        if (!diagnostic.Properties.TryGetValue("KeySourceType", out string? keySource) ||
            !diagnostic.Properties.TryGetValue("KeyDestType", out string? keyDest) ||
            !diagnostic.Properties.TryGetValue("ValueSourceType", out string? valueSource) ||
            !diagnostic.Properties.TryGetValue("ValueDestType", out string? valueDest) ||
            string.IsNullOrEmpty(keySource) || string.IsNullOrEmpty(keyDest) ||
            string.IsNullOrEmpty(valueSource) || string.IsNullOrEmpty(valueDest))
        {
            return;
        }

        bool keyNeedsConversion = !TypeNamesEquivalent(keySource!, keyDest!);
        bool valueNeedsConversion = !TypeNamesEquivalent(valueSource!, valueDest!);
        bool keyAxisSimple = !keyNeedsConversion || IsSafeAxisConversion(keySource!, keyDest!);
        bool valueAxisSimple = !valueNeedsConversion || IsSafeAxisConversion(valueSource!, valueDest!);

        // Both axes are pass-through or simple primitive conversions -> executable ToDictionary rewrite.
        if ((keyNeedsConversion || valueNeedsConversion) && keyAxisSimple && valueAxisSimple)
        {
            string keySelector = keyNeedsConversion
                ? $"{GetConversionMethod(keyDest!)}(kvp.Key)"
                : "kvp.Key";
            string valueSelector = valueNeedsConversion
                ? $"{GetConversionMethod(valueDest!)}(kvp.Value)"
                : "kvp.Value";
            string mapFromExpression =
                $"src.{CodeFixSyntaxHelper.EscapeIdentifier(sourcePropertyName)}.ToDictionary(kvp => {keySelector}, kvp => {valueSelector})";

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Convert {destinationPropertyName} entries using ToDictionary()",
                    cancellationToken =>
                        AddMapFromWithLinqAsync(context.Document, root, invocation, destinationPropertyName,
                            mapFromExpression),
                    $"AM021_DictionaryConversion_{destinationPropertyName}"),
                diagnostic);
            return;
        }

        // The value axis is a complex object type with no registered CreateMap -> offer the element map.
        // Restricted to plain named types (generic collections/arrays such as List<int> vs List<string> are
        // not a CreateMap target) AND a pass-through key axis: when the key also needs conversion (e.g.
        // Dictionary<string, Foo> -> Dictionary<int, FooDto>), a value-only CreateMap would leave the key
        // mismatch unresolved, so the diagnostic falls through to the manual-review ignore action only.
        if (valueNeedsConversion && !valueAxisSimple && !keyNeedsConversion &&
            IsPlainNamedTypeName(valueSource!) && IsPlainNamedTypeName(valueDest!))
        {
            RegisterComplexMappingFix(context, root, invocation, destinationPropertyName, valueSource!, valueDest!,
                diagnostic, semanticModel);
        }
    }

    /// <summary>
    ///     Determines whether a dictionary key/value axis conversion is one the generated ToDictionary
    ///     selector can actually compile. Both sides must be simple types, and a <c>DateTime</c>/<c>Guid</c>
    ///     destination (which only exposes a <c>Parse(string)</c> conversion) requires a string source —
    ///     otherwise a call such as <c>DateTime.Parse(intValue)</c> would not compile.
    /// </summary>
    private static bool IsSafeAxisConversion(string sourceType, string destType)
    {
        if (!IsSimpleConversionType(sourceType) || !IsSimpleConversionType(destType))
        {
            return false;
        }

        if (GetConversionMethod(destType).EndsWith(".Parse", StringComparison.Ordinal))
        {
            string normalizedSource = NormalizeTypeName(sourceType);
            return normalizedSource is "string" or "System.String";
        }

        return true;
    }

    private static bool TypeNamesEquivalent(string left, string right)
    {
        // Preserve generic arguments here: List<int> and List<string> must be treated as DIFFERENT axes so a
        // ToDictionary pass-through is not generated for a genuine inner-element mismatch. NormalizeTypeName
        // intentionally strips generics for the simple-primitive check, which is the wrong comparison for
        // deciding whether an axis needs conversion.
        return string.Equals(StripTypeQualifiers(left), StripTypeQualifiers(right), StringComparison.Ordinal);
    }

    private static string StripTypeQualifiers(string typeName)
    {
        string value = typeName.Trim();
        if (value.StartsWith("global::", StringComparison.Ordinal))
        {
            value = value.Substring("global::".Length);
        }

        return value;
    }

    /// <summary>
    ///     A plain named type is a candidate for a decomposed element <c>CreateMap</c>: not a generic type,
    ///     array, or simple primitive (those are handled by ToDictionary or left to manual review).
    /// </summary>
    private static bool IsPlainNamedTypeName(string typeName)
    {
        string value = typeName.Trim();
        return !value.Contains('<') &&
               !value.Contains('[') &&
               !IsSimpleConversionType(value);
    }

    private void RegisterComplexMappingFix(
        CodeFixContext context,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        string propertyName,
        string sourceElementType,
        string destElementType,
        Diagnostic diagnostic,
        SemanticModel semanticModel)
    {
        string sourceTypeShortName = GetShortTypeName(sourceElementType);
        string destTypeShortName = GetShortTypeName(destElementType);

        // Only advertise when apply can insert into a constructor/method block statement list.
        if (!TryGetElementCreateMapInsertTarget(invocation, out _, out _))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Add CreateMap<{sourceTypeShortName}, {destTypeShortName}>() for element mapping",
                cancellationToken =>
                    AddElementCreateMapAsync(context.Document, invocation,
                        sourceElementType, destElementType, cancellationToken),
                $"AM021_ComplexMapping_{propertyName}"),
            diagnostic);
    }

    private static bool TryGetElementCreateMapInsertTarget(
        InvocationExpressionSyntax invocation,
        out SyntaxNode? bodyOwner,
        out StatementSyntax? statementWithInvocation)
    {
        bodyOwner = null;
        statementWithInvocation = null;

        // Inserted CreateMap is unqualified; only Profile-style bare/this CreateMap hosts compile.
        if (!IsUnqualifiedCreateMapInvocation(invocation))
        {
            return false;
        }

        ConstructorDeclarationSyntax? constructor = invocation.Ancestors()
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault(c => c.Body != null);
        if (constructor?.Body != null)
        {
            StatementSyntax? statement = constructor.Body.Statements
                .FirstOrDefault(s => s.DescendantNodes().Contains(invocation));
            if (statement != null)
            {
                bodyOwner = constructor;
                statementWithInvocation = statement;
                return true;
            }
        }

        MethodDeclarationSyntax? method = invocation.Ancestors()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Body != null);
        if (method?.Body != null)
        {
            StatementSyntax? statement = method.Body.Statements
                .FirstOrDefault(s => s.DescendantNodes().Contains(invocation));
            if (statement != null)
            {
                bodyOwner = method;
                statementWithInvocation = statement;
                return true;
            }
        }

        return false;
    }

    private static bool IsUnqualifiedCreateMapInvocation(InvocationExpressionSyntax invocation)
    {
        // Diagnostics may land on ForMember chains; peel to CreateMap when present.
        InvocationExpressionSyntax? createMap = invocation;
        while (createMap != null)
        {
            if (IsDirectUnqualifiedCreateMap(createMap))
            {
                return true;
            }

            createMap = (createMap.Expression as MemberAccessExpressionSyntax)?.Expression as InvocationExpressionSyntax;
        }

        return IsDirectUnqualifiedCreateMap(invocation);
    }

    private static bool IsDirectUnqualifiedCreateMap(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            GenericNameSyntax generic when generic.Identifier.ValueText == "CreateMap" => true,
            IdentifierNameSyntax identifier when identifier.Identifier.ValueText == "CreateMap" => true,
            MemberAccessExpressionSyntax memberAccess
                when GetMemberName(memberAccess.Name) == "CreateMap" &&
                     memberAccess.Expression is ThisExpressionSyntax => true,
            _ => false
        };
    }

    private static string GetMemberName(SimpleNameSyntax name) =>
        name is GenericNameSyntax generic ? generic.Identifier.ValueText : name.Identifier.ValueText;

    private async Task<Document> AddMapFromWithLinqAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        string propertyName,
        string mapFromExpression)
    {
        // Add ForMember with MapFrom
        InvocationExpressionSyntax newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
            invocation,
            propertyName,
            mapFromExpression);

        SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);

        // Add using System.Linq if not present
        if (newRoot is CompilationUnitSyntax compilationUnit)
        {
            newRoot = CodeFixSyntaxHelper.AddUsingIfMissing(compilationUnit, "System.Linq");
        }

        return await Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static async Task<Document> AddElementCreateMapAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string sourceTypeName,
        string destTypeName,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return document;
        }

        if (!TryGetElementCreateMapInsertTarget(invocation, out SyntaxNode? bodyOwner, out StatementSyntax? statementWithInvocation))
        {
            return document;
        }

        ExpressionStatementSyntax createMapStatement =
            CreateElementCreateMapStatement(sourceTypeName, destTypeName);

        switch (bodyOwner)
        {
            case ConstructorDeclarationSyntax constructor when constructor.Body != null && statementWithInvocation != null:
            {
                int indexOfStatement = constructor.Body.Statements.IndexOf(statementWithInvocation);
                var newStatements = constructor.Body.Statements.ToList();
                newStatements.Insert(indexOfStatement + 1, createMapStatement);
                BlockSyntax newBody = constructor.Body.WithStatements(SyntaxFactory.List(newStatements));
                root = root.ReplaceNode(constructor, constructor.WithBody(newBody));
                break;
            }
            case MethodDeclarationSyntax method when method.Body != null && statementWithInvocation != null:
            {
                int indexOfStatement = method.Body.Statements.IndexOf(statementWithInvocation);
                var newStatements = method.Body.Statements.ToList();
                newStatements.Insert(indexOfStatement + 1, createMapStatement);
                BlockSyntax newBody = method.Body.WithStatements(SyntaxFactory.List(newStatements));
                root = root.ReplaceNode(method, method.WithBody(newBody));
                break;
            }
        }

        return document.WithSyntaxRoot(root);
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

    private static ExpressionStatementSyntax CreateElementCreateMapStatement(string sourceType, string destType)
    {
        return SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                    SyntaxFactory.GenericName(
                            SyntaxFactory.Identifier("CreateMap"))
                        .WithTypeArgumentList(
                            SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SeparatedList<TypeSyntax>(new[]
                                {
                                    SyntaxFactory.ParseTypeName(sourceType), SyntaxFactory.ParseTypeName(destType)
                                }))))
                .WithArgumentList(SyntaxFactory.ArgumentList())
        );
    }

    private static string? CreateSimpleConversionExpression(
        InvocationExpressionSyntax invocation,
        string sourcePropertyName,
        string destinationPropertyName,
        string conversionMethod,
        SemanticModel semanticModel)
    {
        (ITypeSymbol? sourceType, ITypeSymbol? destType) createMapTypes =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
        if (createMapTypes.Item2 == null)
        {
            return null;
        }

        IPropertySymbol? destProperty = createMapTypes.Item2.GetMembers()
            .OfType<IPropertySymbol>()
            .FirstOrDefault(p => string.Equals(p.Name, destinationPropertyName, StringComparison.OrdinalIgnoreCase));

        if (destProperty == null)
        {
            return null;
        }

        string selectExpression =
            $"src.{CodeFixSyntaxHelper.EscapeIdentifier(sourcePropertyName)}.Select(x => {conversionMethod}(x))";
        string destinationTypeName = destProperty.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        if (destProperty.Type.TypeKind == TypeKind.Array)
        {
            return $"{selectExpression}.ToArray()";
        }

        if (IsConstructedFromType(destProperty.Type, "System.Collections.Generic.List<T>"))
        {
            return $"{selectExpression}.ToList()";
        }

        if (IsConstructedFromType(destProperty.Type, "System.Collections.Generic.IEnumerable<T>") ||
            IsConstructedFromType(destProperty.Type, "System.Collections.Generic.ICollection<T>") ||
            IsConstructedFromType(destProperty.Type, "System.Collections.Generic.IList<T>") ||
            IsConstructedFromType(destProperty.Type, "System.Collections.Generic.IReadOnlyCollection<T>") ||
            IsConstructedFromType(destProperty.Type, "System.Collections.Generic.IReadOnlyList<T>"))
        {
            return $"{selectExpression}.ToList()";
        }

        if (IsConstructedFromType(destProperty.Type, "System.Collections.Generic.HashSet<T>") ||
            IsConstructedFromType(destProperty.Type, "System.Collections.Generic.ISet<T>") ||
            IsConstructedFromType(destProperty.Type, "System.Collections.Generic.IReadOnlySet<T>"))
        {
            string destinationElementTypeName = GetCollectionElementTypeName(destProperty.Type);
            return $"new global::System.Collections.Generic.HashSet<{destinationElementTypeName}>({selectExpression})";
        }

        if (IsConstructedFromType(destProperty.Type, "System.Collections.Generic.Queue<T>"))
        {
            return $"new {destinationTypeName}({selectExpression})";
        }

        if (IsConstructedFromType(destProperty.Type, "System.Collections.Generic.Stack<T>"))
        {
            return $"new {destinationTypeName}({selectExpression})";
        }

        if (IsConstructedFromType(destProperty.Type, "System.Collections.Immutable.ImmutableList<T>"))
        {
            return $"global::System.Collections.Immutable.ImmutableList.CreateRange({selectExpression})";
        }

        if (IsConstructedFromType(destProperty.Type, "System.Collections.Immutable.ImmutableArray<T>"))
        {
            return $"global::System.Collections.Immutable.ImmutableArray.CreateRange({selectExpression})";
        }

        if (IsConstructedFromType(destProperty.Type, "System.Collections.Immutable.ImmutableHashSet<T>"))
        {
            return $"global::System.Collections.Immutable.ImmutableHashSet.CreateRange({selectExpression})";
        }

        if (IsConstructedFromType(destProperty.Type, "System.Collections.Frozen.FrozenSet<T>"))
        {
            return $"global::System.Collections.Frozen.FrozenSet.ToFrozenSet({selectExpression})";
        }

        return null;
    }

    private static bool IsSimpleTypeConversion(string sourceElementType, string destElementType)
    {
        return IsSimpleConversionType(sourceElementType) && IsSimpleConversionType(destElementType);
    }

    private static bool IsKeyValuePairType(string typeName)
    {
        string normalizedType = typeName.Trim();
        return normalizedType.StartsWith("System.Collections.Generic.KeyValuePair<", StringComparison.Ordinal) ||
               normalizedType.StartsWith("global::System.Collections.Generic.KeyValuePair<", StringComparison.Ordinal) ||
               normalizedType.StartsWith("KeyValuePair<", StringComparison.Ordinal);
    }

    private static string GetConversionMethod(string destElementType)
    {
        // Determine the conversion method based on destination type
        // using Convert class for better null handling compared to Parse
        string normalizedDestType = NormalizeTypeName(destElementType);
        return normalizedDestType switch
        {
            "int" or "System.Int32" => "global::System.Convert.ToInt32",
            "long" or "System.Int64" => "global::System.Convert.ToInt64",
            "double" or "System.Double" => "global::System.Convert.ToDouble",
            "float" or "System.Single" => "global::System.Convert.ToSingle",
            "decimal" or "System.Decimal" => "global::System.Convert.ToDecimal",
            "bool" or "System.Boolean" => "global::System.Convert.ToBoolean",
            "byte" or "System.Byte" => "global::System.Convert.ToByte",
            "short" or "System.Int16" => "global::System.Convert.ToInt16",
            "char" or "System.Char" => "global::System.Convert.ToChar",
            "string" or "System.String" => "global::System.Convert.ToString",
            "DateTime" or "System.DateTime" => "global::System.DateTime.Parse",
            "Guid" or "System.Guid" => "global::System.Guid.Parse",
            _ => "global::System.Convert.ToString"
        };
    }

    private static string GetShortTypeName(string fullTypeName)
    {
        // Extract the simple type name from fully qualified name
        int lastDot = fullTypeName.LastIndexOf('.');
        if (lastDot >= 0)
        {
            return fullTypeName.Substring(lastDot + 1);
        }

        return fullTypeName;
    }

    private static bool IsSimpleConversionType(string typeName)
    {
        string normalizedType = NormalizeTypeName(typeName);
        return normalizedType is "string" or "System.String" or
               "int" or "System.Int32" or
               "long" or "System.Int64" or
               "double" or "System.Double" or
               "float" or "System.Single" or
               "decimal" or "System.Decimal" or
               "bool" or "System.Boolean" or
               "byte" or "System.Byte" or
               "short" or "System.Int16" or
               "char" or "System.Char" or
               "DateTime" or "System.DateTime" or
               "Guid" or "System.Guid";
    }

    private static string NormalizeTypeName(string typeName)
    {
        string normalized = typeName.Trim();

        if (normalized.StartsWith("global::", StringComparison.Ordinal))
        {
            normalized = normalized.Substring("global::".Length);
        }

        while (normalized.EndsWith("[]", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(0, normalized.Length - 2);
        }

        if (normalized.StartsWith("System.Nullable<", StringComparison.Ordinal) &&
            normalized.EndsWith(">", StringComparison.Ordinal))
        {
            string innerType = normalized.Substring("System.Nullable<".Length,
                normalized.Length - "System.Nullable<".Length - 1);
            return NormalizeTypeName(innerType);
        }

        if (normalized.StartsWith("Nullable<", StringComparison.Ordinal) &&
            normalized.EndsWith(">", StringComparison.Ordinal))
        {
            string innerType = normalized.Substring("Nullable<".Length, normalized.Length - "Nullable<".Length - 1);
            return NormalizeTypeName(innerType);
        }

        if (normalized.EndsWith("?", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(0, normalized.Length - 1);
        }

        int genericStart = normalized.IndexOf('<');
        if (genericStart >= 0)
        {
            normalized = normalized.Substring(0, genericStart);
        }

        return normalized switch
        {
            "String" => "System.String",
            "Int32" => "System.Int32",
            "Int64" => "System.Int64",
            "Double" => "System.Double",
            "Single" => "System.Single",
            "Decimal" => "System.Decimal",
            "Boolean" => "System.Boolean",
            "Byte" => "System.Byte",
            "Int16" => "System.Int16",
            "Char" => "System.Char",
            _ => normalized
        };
    }

    private static bool IsConstructedFromType(ITypeSymbol type, string genericDefinitionName)
    {
        return type is INamedTypeSymbol namedType &&
               namedType.OriginalDefinition.ToDisplayString() == genericDefinitionName;
    }

    private static string GetCollectionElementTypeName(ITypeSymbol collectionType)
    {
        return AutoMapperAnalysisHelpers.GetCollectionElementType(collectionType)
            ?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "object";
    }

    private static string? ExtractPropertyNameFromDiagnostic(Diagnostic diagnostic)
    {
        string message = diagnostic.GetMessage();
        int startIndex = message.IndexOf("Property '");
        if (startIndex < 0)
        {
            return null;
        }

        startIndex += "Property '".Length;
        int endIndex = message.IndexOf("'", startIndex);
        if (endIndex < 0)
        {
            return null;
        }

        return message.Substring(startIndex, endIndex - startIndex);
    }

    private static string? ExtractSourceElementTypeFromDiagnostic(Diagnostic diagnostic)
    {
        string message = diagnostic.GetMessage();
        string pattern = @"\(([^)]+)\) elements cannot";
        Match match = Regex.Match(message, pattern);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractDestElementTypeFromDiagnostic(Diagnostic diagnostic)
    {
        string message = diagnostic.GetMessage();
        string pattern = @"to [^(]+\(([^)]+)\) elements";
        Match match = Regex.Match(message, pattern);
        return match.Success ? match.Groups[1].Value : null;
    }

}

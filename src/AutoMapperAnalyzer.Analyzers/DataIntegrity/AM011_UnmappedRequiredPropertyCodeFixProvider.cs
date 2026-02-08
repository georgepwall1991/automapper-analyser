using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.DataIntegrity;

/// <summary>
///     Code fix provider for AM011 diagnostic - Unmapped Required Property.
///     Provides fixes for required properties that are not mapped from source.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM011_UnmappedRequiredPropertyCodeFixProvider))]
[Shared]
public class AM011_UnmappedRequiredPropertyCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <summary>
    ///     Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ["AM011"];

    /// <summary>
    ///     Registers code fixes for the specified context.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        await ProcessDiagnosticsAsync(
            context,
            propertyNames: ["PropertyName", "PropertyType"],
            registerBulkFixes: RegisterBulkFixes,
            registerPerPropertyFixes: (ctx, diagnostic, invocation, properties, semanticModel, root) =>
            {
                RegisterPerPropertyFixes(ctx, diagnostic, invocation, properties["PropertyName"],
                    properties["PropertyType"], semanticModel, root);
            });
    }

    private void RegisterPerPropertyFixes(
        CodeFixContext context,
        Diagnostic diagnostic,
        InvocationExpressionSyntax invocation,
        string propertyName,
        string propertyType,
        SemanticModel semanticModel,
        SyntaxNode root)
    {
        var nestedActions = ImmutableArray.CreateBuilder<CodeAction>();

        // Fix 0: Fuzzy Matching - Find similar source properties
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            MappingChainAnalysisHelper.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType != null)
        {
            var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
            IPropertySymbol? destinationProperty = destType == null
                ? null
                : AutoMapperAnalysisHelpers.GetMappableProperties(destType, false)
                    .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

            var fuzzyMatches = destinationProperty != null
                ? FuzzyMatchHelper.FindFuzzyMatches(propertyName, sourceProperties, destinationProperty.Type)
                : sourceProperties;

            foreach (var sourceProp in fuzzyMatches)
            {
                var matchAction = CodeAction.Create(
                    $"Map from similar property '{sourceProp.Name}'",
                    cancellationToken =>
                    {
                        InvocationExpressionSyntax newInvocation =
                            CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName, $"src.{sourceProp.Name}");
                        return ReplaceNodeAsync(context.Document, root, invocation, newInvocation);
                    },
                    $"FuzzyMatch_{propertyName}_{sourceProp.Name}");
                nestedActions.Add(matchAction);
            }
        }

        // Fix 1: Add ForMember mapping with default value
        var defaultValueAction = CodeAction.Create(
            "Map to default value",
            cancellationToken =>
            {
                string defaultValue = TypeConversionHelper.GetDefaultValueForType(propertyType);
                InvocationExpressionSyntax newInvocation =
                    CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName, defaultValue);
                return ReplaceNodeAsync(context.Document, root, invocation, newInvocation);
            },
            $"DefaultValue_{propertyName}");
        nestedActions.Add(defaultValueAction);

        // Fix 2: Add ForMember mapping with constant value
        var constantValueAction = CodeAction.Create(
            "Map to constant value",
            cancellationToken =>
            {
                string constantValue = TypeConversionHelper.GetSampleValueForType(propertyType);
                InvocationExpressionSyntax newInvocation =
                    CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, propertyName, constantValue);
                return ReplaceNodeAsync(context.Document, root, invocation, newInvocation);
            },
            $"ConstantValue_{propertyName}");
        nestedActions.Add(constantValueAction);

        // Fix 3: Explicitly ignore required destination property.
        var ignoreAction = CodeAction.Create(
            "Ignore required property",
            cancellationToken =>
            {
                InvocationExpressionSyntax newInvocation =
                    CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
                return ReplaceNodeAsync(context.Document, root, invocation, newInvocation);
            },
            $"Ignore_{propertyName}");
        nestedActions.Add(ignoreAction);

        // Fix 4: Create missing source property so convention mapping succeeds.
        if (sourceType != null &&
            !AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false)
                .Any(property => string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)))
        {
            var createSourcePropertyAction = CodeAction.Create(
                "Create property in source type",
                cancellationToken =>
                    CreateSourcePropertyAsync(context.Document, sourceType, propertyName, propertyType),
                $"CreateSourceProperty_{propertyName}");
            nestedActions.Add(createSourcePropertyAction);
        }

        // Register the grouped action using base class helper
        RegisterGroupedPropertyFix(context, diagnostic, propertyName, nestedActions);
    }

    private void RegisterBulkFixes(CodeFixContext context, InvocationExpressionSyntax invocation,
        SemanticModel semanticModel, SyntaxNode root)
    {
        // Check if there's already a configuration comment
        string? existingConfig = ExtractConfigurationComment(invocation);

        if (existingConfig != null && BulkFixConfigurationParser.IsConfigurationComment(existingConfig))
        {
            // Step 2: Apply configuration from existing comment
            var applyConfigAction = CodeAction.Create(
                "✅ Apply bulk fix configuration",
                cancellationToken => ApplyConfigurationAsync(context.Document, root, invocation, semanticModel, existingConfig),
                "AM011_Apply_Config"
            );

            RegisterBulkFixes(context, applyConfigAction);
            return; // Only show "Apply" option when config comment exists
        }

        // Step 1: Generate configuration comment (no existing config)
        var configureAction = CodeAction.Create(
            "📝 Configure bulk fix (interactive)...",
            cancellationToken => GenerateConfigurationCommentAsync(context.Document, root, invocation, semanticModel),
            "AM011_Bulk_Configure"
        );

        // Quick bulk fixes (no configuration needed)
        var bulkDefaultAction = CodeAction.Create(
            "⚡ Map all unmapped properties to default value",
            cancellationToken => BulkFixAsync(context.Document, root, invocation, semanticModel, "Default"),
            "AM011_Bulk_Default"
        );

        var bulkConstantAction = CodeAction.Create(
            "⚡ Map all unmapped properties to constant value",
            cancellationToken => BulkFixAsync(context.Document, root, invocation, semanticModel, "Constant"),
            "AM011_Bulk_Constant"
        );

        var bulkIgnoreAction = CodeAction.Create(
            "⚡ Ignore all unmapped required properties",
            cancellationToken => BulkIgnoreAsync(context.Document, root, invocation, semanticModel),
            "AM011_Bulk_Ignore"
        );

        var bulkCreateSourcePropertiesAction = CodeAction.Create(
            "⚡ Create all missing properties in source type",
            cancellationToken => BulkCreateSourcePropertiesAsync(context.Document, invocation, semanticModel),
            "AM011_Bulk_CreateSourceProperties"
        );

        // Register all bulk fixes using base class helper
        RegisterBulkFixes(context, configureAction, bulkDefaultAction, bulkConstantAction, bulkIgnoreAction,
            bulkCreateSourcePropertiesAction);
    }

    private async Task<Document> BulkFixAsync(Document document, SyntaxNode root, InvocationExpressionSyntax invocation,
        SemanticModel semanticModel, string mode)
    {
        // 1. Identify unmapped properties
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            MappingChainAnalysisHelper.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType == null || destType == null)
        {
            return document;
        }

        // This logic mimics the Analyzer logic to find *all* missing properties
        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        var destProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destType, false);

        var missingProperties = new List<IPropertySymbol>();

        foreach (var destProp in destProperties)
        {
            // Check if required
            if (!destProp.IsRequired)
            {
                continue;
            }

            // Check if mapped (name match)
            if (sourceProperties.Any(p => string.Equals(p.Name, destProp.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // Check if explicitly configured
            if (IsPropertyConfiguredWithForMember(invocation, destProp.Name, semanticModel) ||
                IsPropertyConfiguredWithForCtorParam(invocation, destProp.Name, semanticModel))
            {
                continue;
            }

            missingProperties.Add(destProp);
        }

        if (!missingProperties.Any())
        {
            return document;
        }

        // 2. Apply fixes
        InvocationExpressionSyntax currentInvocation = invocation;

        foreach (var prop in missingProperties)
        {
            string valueToMap = mode == "Default"
                ? TypeConversionHelper.GetDefaultValueForType(prop.Type.ToDisplayString())
                : TypeConversionHelper.GetSampleValueForType(prop.Type.ToDisplayString());

            currentInvocation =
                CodeFixSyntaxHelper.CreateForMemberWithMapFrom(currentInvocation, prop.Name, valueToMap);
        }

        SyntaxNode newRoot = root.ReplaceNode(invocation, currentInvocation);
        return document.WithSyntaxRoot(newRoot);
    }

    private Task<Document> BulkIgnoreAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            MappingChainAnalysisHelper.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType == null || destType == null)
        {
            return Task.FromResult(document);
        }

        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        var destProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destType, false);

        var missingProperties = destProperties
            .Where(destProp => destProp.IsRequired)
            .Where(destProp =>
                !sourceProperties.Any(sourceProp =>
                    string.Equals(sourceProp.Name, destProp.Name, StringComparison.OrdinalIgnoreCase)))
            .Where(destProp =>
                !IsPropertyConfiguredWithForMember(invocation, destProp.Name, semanticModel) &&
                !IsPropertyConfiguredWithForCtorParam(invocation, destProp.Name, semanticModel))
            .ToList();

        if (!missingProperties.Any())
        {
            return Task.FromResult(document);
        }

        InvocationExpressionSyntax currentInvocation = invocation;
        foreach (IPropertySymbol missingProperty in missingProperties)
        {
            currentInvocation = CodeFixSyntaxHelper.CreateForMemberWithIgnore(currentInvocation, missingProperty.Name);
        }

        SyntaxNode newRoot = root.ReplaceNode(invocation, currentInvocation);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private async Task<Solution> BulkCreateSourcePropertiesAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            MappingChainAnalysisHelper.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType == null || destType == null)
        {
            return document.Project.Solution;
        }

        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        var destinationProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destType, false);

        List<(string Name, string Type)> propertiesToAdd = destinationProperties
            .Where(destinationProperty => destinationProperty.IsRequired)
            .Where(destinationProperty =>
                !sourceProperties.Any(sourceProperty =>
                    string.Equals(sourceProperty.Name, destinationProperty.Name, StringComparison.OrdinalIgnoreCase)))
            .Where(destinationProperty =>
                !IsPropertyConfiguredWithForMember(invocation, destinationProperty.Name, semanticModel) &&
                !IsPropertyConfiguredWithForCtorParam(invocation, destinationProperty.Name, semanticModel))
            .Select(destinationProperty => (destinationProperty.Name, destinationProperty.Type.ToDisplayString()))
            .ToList();

        if (!propertiesToAdd.Any())
        {
            return document.Project.Solution;
        }

        return await CodeFixSyntaxHelper.AddPropertiesToTypeAsync(document, sourceType, propertiesToAdd);
    }

    private async Task<Solution> CreateSourcePropertyAsync(
        Document document,
        ITypeSymbol sourceType,
        string propertyName,
        string propertyType)
    {
        return await CodeFixSyntaxHelper.AddPropertiesToTypeAsync(document, sourceType, [(propertyName, propertyType)]);
    }

    private async Task<Document> GenerateConfigurationCommentAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        // 1. Identify unmapped properties (same logic as BulkFixAsync)
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            MappingChainAnalysisHelper.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType == null || destType == null)
        {
            return document;
        }

        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        var destProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destType, false);

        var missingProperties = new List<(IPropertySymbol Property, BulkFixAction DefaultAction, string? Parameter)>();

        foreach (var destProp in destProperties)
        {
            // Check if required
            if (!destProp.IsRequired)
            {
                continue;
            }

            // Check if mapped (name match)
            var matchedSource = sourceProperties.FirstOrDefault(p =>
                string.Equals(p.Name, destProp.Name, StringComparison.OrdinalIgnoreCase));
            if (matchedSource != null)
            {
                continue;
            }

            // Check if explicitly configured
            if (IsPropertyConfiguredWithForMember(invocation, destProp.Name, semanticModel) ||
                IsPropertyConfiguredWithForCtorParam(invocation, destProp.Name, semanticModel))
            {
                continue;
            }

            // Determine default action and parameter
            BulkFixAction defaultAction = BulkFixAction.Default;
            string? parameter = null;

            // Try fuzzy matching to find similar property
            var fuzzyMatch = FuzzyMatchHelper.FindFuzzyMatches(destProp.Name, sourceProperties, destProp.Type).FirstOrDefault();
            if (fuzzyMatch != null)
            {
                defaultAction = BulkFixAction.FuzzyMatch;
                parameter = fuzzyMatch.Name;
            }

            missingProperties.Add((destProp, defaultAction, parameter));
        }

        if (!missingProperties.Any())
        {
            return document;
        }

        // 2. Generate configuration comment
        var propertyConfigs = missingProperties.Select(p => (
            p.Property.Name,
            p.Property.Type.ToDisplayString(),
            p.DefaultAction,
            p.Parameter
        ));

        string configComment = BulkFixConfigurationParser.GenerateConfigurationComment(propertyConfigs);

        // 3. Add comment before the invocation
        var commentTrivia = SyntaxFactory.Comment(configComment);
        var newInvocation = invocation.WithLeadingTrivia(
            invocation.GetLeadingTrivia()
                .Add(commentTrivia)
                .Add(SyntaxFactory.ElasticCarriageReturnLineFeed));

        SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }

    private string? ExtractConfigurationComment(InvocationExpressionSyntax invocation)
    {
        // Look for configuration comment in leading trivia
        var leadingTrivia = invocation.GetLeadingTrivia();

        foreach (var trivia in leadingTrivia)
        {
            if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                var commentText = trivia.ToString();
                if (BulkFixConfigurationParser.IsConfigurationComment(commentText))
                {
                    return commentText;
                }
            }
        }

        return null;
    }

    private async Task<Document> ApplyConfigurationAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string configCommentText)
    {
        // 1. Parse configuration
        var config = BulkFixConfigurationParser.Parse(configCommentText);
        if (config == null)
        {
            return document; // Invalid configuration
        }

        // 2. Get type information
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            MappingChainAnalysisHelper.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType == null || destType == null)
        {
            return document;
        }

        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);

        // 3. Apply configuration (with optional chunking)
        if (config.EnableChunking)
        {
            return await ApplyConfigurationWithChunkingAsync(document, root, invocation, config, sourceProperties);
        }
        else
        {
            return ApplyConfigurationWithoutChunking(document, root, invocation, config, sourceProperties);
        }
    }

    private Document ApplyConfigurationWithoutChunking(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        BulkFixConfiguration config,
        IEnumerable<IPropertySymbol> sourceProperties)
    {
        InvocationExpressionSyntax currentInvocation = invocation;

        foreach (var propertyAction in config.PropertyActions)
        {
            currentInvocation = ApplyPropertyAction(currentInvocation, propertyAction, sourceProperties);
        }

        // Remove the configuration comment
        var cleanedInvocation = currentInvocation.WithLeadingTrivia(
            currentInvocation.GetLeadingTrivia()
                .Where(t => !t.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                            !BulkFixConfigurationParser.IsConfigurationComment(t.ToString())));

        SyntaxNode newRoot = root.ReplaceNode(invocation, cleanedInvocation);
        return document.WithSyntaxRoot(newRoot);
    }

    private async Task<Document> ApplyConfigurationWithChunkingAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        BulkFixConfiguration config,
        IEnumerable<IPropertySymbol> sourceProperties)
    {
        // 1. Split properties into chunks
        var chunks = config.PropertyActions
            .Select((action, index) => new { action, index })
            .GroupBy(x => x.index / config.ChunkSize)
            .Select(g => g.Select(x => x.action).ToList())
            .ToList();

        if (chunks.Count <= 1)
        {
            // No chunking needed, use simple approach
            return ApplyConfigurationWithoutChunking(document, root, invocation, config, sourceProperties);
        }

        // 2. Find the containing class/record
        var containingClass = invocation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()
                              ?? invocation.Ancestors().OfType<RecordDeclarationSyntax>().FirstOrDefault() as TypeDeclarationSyntax;

        if (containingClass == null)
        {
            // Can't find containing class, fall back to non-chunking
            return ApplyConfigurationWithoutChunking(document, root, invocation, config, sourceProperties);
        }

        // 3. Get type arguments for method signatures
        SemanticModel? latestSemanticModel = await document.GetSemanticModelAsync();
        if (latestSemanticModel == null)
        {
            return document;
        }

        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            MappingChainAnalysisHelper.GetCreateMapTypeArguments(invocation, latestSemanticModel);
        if (sourceType == null || destType == null)
        {
            return document;
        }

        string sourceTypeName = sourceType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        string destTypeName = destType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        // 4. Create helper methods for each chunk
        var helperMethods = new List<MethodDeclarationSyntax>();
        var methodNames = new List<string>();

        for (int i = 0; i < chunks.Count; i++)
        {
            string methodName = $"ConfigurePropertiesGroup{i + 1}";
            methodNames.Add(methodName);

            var helperMethod = CreateChunkHelperMethod(methodName, chunks[i], sourceTypeName, destTypeName, sourceProperties);
            helperMethods.Add(helperMethod);
        }

        // 5. Update CreateMap invocation to call helper methods
        InvocationExpressionSyntax updatedInvocation = invocation;
        foreach (var methodName in methodNames)
        {
            updatedInvocation = CreateHelperMethodInvocation(updatedInvocation, methodName);
        }

        // Remove configuration comment
        updatedInvocation = updatedInvocation.WithLeadingTrivia(
            updatedInvocation.GetLeadingTrivia()
                .Where(t => !t.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                            !BulkFixConfigurationParser.IsConfigurationComment(t.ToString())));

        // 6. Add helper methods to class
        var updatedClass = containingClass.AddMembers(helperMethods.ToArray());

        // 7. Replace both invocation and class in tree
        var newRoot = root.ReplaceNode(containingClass, updatedClass);
        var newInvocation = newRoot.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(i => i.ToString().Contains(invocation.ToString().Substring(0, Math.Min(50, invocation.ToString().Length))));

        if (newInvocation != null)
        {
            newRoot = newRoot.ReplaceNode(newInvocation, updatedInvocation);
        }

        return document.WithSyntaxRoot(newRoot);
    }

    private MethodDeclarationSyntax CreateChunkHelperMethod(
        string methodName,
        List<PropertyFixAction> actions,
        string sourceTypeName,
        string destTypeName,
        IEnumerable<IPropertySymbol> sourceProperties)
    {
        // Build ForMember chain for this chunk
        var statements = new List<StatementSyntax>();

        // Start with: return map
        ExpressionSyntax returnExpression = SyntaxFactory.IdentifierName("map");

        foreach (var action in actions)
        {
            var forMemberCall = CreateForMemberCallExpression(action, sourceProperties);
            returnExpression = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    returnExpression,
                    SyntaxFactory.IdentifierName("ForMember")),
                forMemberCall.ArgumentList);
        }

        var returnStatement = SyntaxFactory.ReturnStatement(returnExpression);

        var method = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName($"IMappingExpression<{sourceTypeName}, {destTypeName}>"),
                methodName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("map"))
                    .WithType(SyntaxFactory.ParseTypeName($"IMappingExpression<{sourceTypeName}, {destTypeName}>")))
            .WithBody(SyntaxFactory.Block(returnStatement))
            .WithLeadingTrivia(
                SyntaxFactory.Comment($"// Auto-generated chunk configuration ({actions.Count} properties)"),
                SyntaxFactory.ElasticCarriageReturnLineFeed);

        return method;
    }

    private InvocationExpressionSyntax CreateForMemberCallExpression(
        PropertyFixAction action,
        IEnumerable<IPropertySymbol> sourceProperties)
    {
        // Create a dummy invocation to use as the base for CodeFixSyntaxHelper methods.
        // We use IdentifierName("map") as the base expression since this will be chained
        // onto the "map" parameter in the helper method body.
        var baseInvocation = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName("map"));

        return action.Action switch
        {
            BulkFixAction.Ignore =>
                CodeFixSyntaxHelper.CreateForMemberWithIgnore(baseInvocation, action.PropertyName),
            BulkFixAction.FuzzyMatch when !string.IsNullOrEmpty(action.Parameter) =>
                CodeFixSyntaxHelper.CreateForMemberWithMapFrom(baseInvocation, action.PropertyName, $"src.{action.Parameter}"),
            _ =>
                CodeFixSyntaxHelper.CreateForMemberWithMapFrom(baseInvocation, action.PropertyName,
                    TypeConversionHelper.GetDefaultValueForType(action.PropertyType))
        };
    }

    private InvocationExpressionSyntax CreateHelperMethodInvocation(
        InvocationExpressionSyntax invocation,
        string helperMethodName)
    {
        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                invocation,
                SyntaxFactory.IdentifierName(helperMethodName)));
    }

    private InvocationExpressionSyntax ApplyPropertyAction(
        InvocationExpressionSyntax invocation,
        PropertyFixAction action,
        IEnumerable<IPropertySymbol> sourceProperties)
    {
        switch (action.Action)
        {
            case BulkFixAction.Default:
                string defaultValue = TypeConversionHelper.GetDefaultValueForType(action.PropertyType);
                return CodeFixSyntaxHelper.CreateForMemberWithMapFrom(invocation, action.PropertyName, defaultValue);

            case BulkFixAction.FuzzyMatch:
                if (!string.IsNullOrEmpty(action.Parameter))
                {
                    return CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                        invocation, action.PropertyName, $"src.{action.Parameter}");
                }
                // Fall back to default if no parameter
                goto case BulkFixAction.Default;

            case BulkFixAction.Ignore:
                return CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, action.PropertyName);

            case BulkFixAction.Todo:
            case BulkFixAction.Custom:
            case BulkFixAction.Nullable:
                // Backward compatibility for legacy configuration comments.
                goto case BulkFixAction.Default;

            default:
                return invocation;
        }
    }



    private static bool IsPropertyConfiguredWithForMember(
        InvocationExpressionSyntax createMapInvocation,
        string propertyName,
        SemanticModel semanticModel)
    {
        foreach (InvocationExpressionSyntax forMemberCall in GetScopedForMemberCalls(createMapInvocation))
        {
            if (!MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(forMemberCall, semanticModel, "ForMember"))
            {
                continue;
            }

            if (forMemberCall.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            CSharpSyntaxNode? lambdaBody = AutoMapperAnalysisHelpers.GetLambdaBody(forMemberCall.ArgumentList.Arguments[0].Expression);
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

    private static bool IsPropertyConfiguredWithForCtorParam(
        InvocationExpressionSyntax createMapInvocation,
        string propertyName,
        SemanticModel semanticModel)
    {
        SyntaxNode? currentNode = createMapInvocation.Parent;

        while (currentNode is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax invocation)
        {
            string methodName = memberAccess.Name.Identifier.ValueText;
            if (methodName == "ReverseMap")
            {
                break;
            }

            if (methodName == "ForCtorParam" &&
                MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "ForCtorParam") &&
                invocation.ArgumentList.Arguments.Count > 0)
            {
                Optional<object?> constantValue = semanticModel.GetConstantValue(invocation.ArgumentList.Arguments[0].Expression);
                if (constantValue.HasValue &&
                    constantValue.Value is string configuredParam &&
                    string.Equals(configuredParam, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            currentNode = invocation.Parent;
        }

        return false;
    }

    private static IEnumerable<InvocationExpressionSyntax> GetScopedForMemberCalls(
        InvocationExpressionSyntax createMapInvocation)
    {
        var forMemberCalls = new List<InvocationExpressionSyntax>();
        SyntaxNode? currentNode = createMapInvocation.Parent;

        while (currentNode is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax invocation)
        {
            string methodName = memberAccess.Name.Identifier.ValueText;
            if (methodName == "ReverseMap")
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
}

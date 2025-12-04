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
        (ITypeSymbol? sourceType, ITypeSymbol? destType) = AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType != null)
        {
            var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType);
            foreach (var sourceProp in sourceProperties)
            {
                // Use shared string utilities for fuzzy matching
                if (StringUtilities.AreNamesSimilar(propertyName, sourceProp.Name,
                    AutoMapperConstants.DefaultFuzzyMatchDistance,
                    AutoMapperConstants.DefaultFuzzyMatchLengthDifference))
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

        // Fix 3: Add ForMember mapping with custom logic placeholder
        var customLogicAction = CodeAction.Create(
            "Map with custom logic (requires implementation)",
            cancellationToken =>
            {
                // Use default(T) as a safe placeholder that compiles for both reference and value types
                InvocationExpressionSyntax newInvocation = CodeFixSyntaxHelper
                    .CreateForMemberWithMapFrom(invocation, propertyName, $"default({propertyType})")
                    .WithLeadingTrivia(
                        invocation.GetLeadingTrivia()
                            .Add(SyntaxFactory.Comment(
                                $"// TODO: Implement custom mapping logic for required property '{propertyName}'"))
                            .Add(SyntaxFactory.ElasticCarriageReturnLineFeed));
                return ReplaceNodeAsync(context.Document, root, invocation, newInvocation);
            },
            $"CustomLogic_{propertyName}");
        nestedActions.Add(customLogicAction);

        // Fix 4: Add comment suggesting to add property to source class
        var addPropertyAction = CodeAction.Create(
            "Add comment to suggest adding property to source class",
            cancellationToken =>
            {
                SyntaxTrivia commentTrivia = SyntaxFactory.Comment(
                    $"// TODO: Consider adding '{propertyName}' property of type '{propertyType}' to source class");
                SyntaxTrivia secondCommentTrivia =
                    SyntaxFactory.Comment("// This will ensure the required property is automatically mapped");

                InvocationExpressionSyntax newInvocation = invocation.WithLeadingTrivia(
                    invocation.GetLeadingTrivia()
                        .Add(commentTrivia)
                        .Add(SyntaxFactory.EndOfLine("\n"))
                        .Add(secondCommentTrivia)
                        .Add(SyntaxFactory.EndOfLine("\n")));

                return ReplaceNodeAsync(context.Document, root, invocation, newInvocation);
            },
            $"AddProperty_{propertyName}");
        nestedActions.Add(addPropertyAction);

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

        // Register all bulk fixes using base class helper
        RegisterBulkFixes(context, configureAction, bulkDefaultAction, bulkConstantAction);
    }

    private async Task<Document> BulkFixAsync(Document document, SyntaxNode root, InvocationExpressionSyntax invocation,
        SemanticModel semanticModel, string mode)
    {
        // 1. Identify unmapped properties
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType == null || destType == null)
        {
            return document;
        }

        // This logic mimics the Analyzer logic to find *all* missing properties
        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType);
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
            if (AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember(invocation, destProp.Name, semanticModel))
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

    private async Task<Document> GenerateConfigurationCommentAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        // 1. Identify unmapped properties (same logic as BulkFixAsync)
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType == null || destType == null)
        {
            return document;
        }

        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType);
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
            if (AutoMapperAnalysisHelpers.IsPropertyConfiguredWithForMember(invocation, destProp.Name, semanticModel))
            {
                continue;
            }

            // Determine default action and parameter
            BulkFixAction defaultAction = BulkFixAction.Default;
            string? parameter = null;

            // Try fuzzy matching to find similar property using shared utilities
            foreach (var sourceProp in sourceProperties)
            {
                if (StringUtilities.AreNamesSimilar(destProp.Name, sourceProp.Name,
                    AutoMapperConstants.DefaultFuzzyMatchDistance,
                    AutoMapperConstants.DefaultFuzzyMatchLengthDifference))
                {
                    defaultAction = BulkFixAction.FuzzyMatch;
                    parameter = sourceProp.Name;
                    break;
                }
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
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType == null || destType == null)
        {
            return document;
        }

        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType);

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
        (ITypeSymbol? sourceType, ITypeSymbol? destType) =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, await document.GetSemanticModelAsync());
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
        // This creates the ForMember call for the helper method
        string mappingExpression = action.Action switch
        {
            BulkFixAction.Default => TypeConversionHelper.GetDefaultValueForType(action.PropertyType),
            BulkFixAction.FuzzyMatch when !string.IsNullOrEmpty(action.Parameter) => $"src.{action.Parameter}",
            BulkFixAction.Ignore => "/* ignored */",
            _ => TypeConversionHelper.GetDefaultValueForType(action.PropertyType)
        };

        // For simplicity, return a placeholder - the actual implementation would create proper syntax nodes
        return SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName("placeholder"));
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
                var todoInvocation = CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, action.PropertyName);
                return todoInvocation.WithLeadingTrivia(
                    todoInvocation.GetLeadingTrivia()
                        .Add(SyntaxFactory.Comment($"// TODO: Configure mapping for '{action.PropertyName}'"))
                        .Add(SyntaxFactory.ElasticCarriageReturnLineFeed));

            case BulkFixAction.Custom:
                var customInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                    invocation, action.PropertyName, $"default({action.PropertyType})");
                return customInvocation.WithLeadingTrivia(
                    customInvocation.GetLeadingTrivia()
                        .Add(SyntaxFactory.Comment($"// TODO: Implement custom mapping for '{action.PropertyName}'"))
                        .Add(SyntaxFactory.ElasticCarriageReturnLineFeed));

            case BulkFixAction.Nullable:
                // TODO: Implement cross-file edit to make destination property nullable
                // For now, map to default with comment
                var nullableInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                    invocation, action.PropertyName, $"default({action.PropertyType})");
                return nullableInvocation.WithLeadingTrivia(
                    nullableInvocation.GetLeadingTrivia()
                        .Add(SyntaxFactory.Comment($"// TODO: Consider making destination property '{action.PropertyName}' nullable"))
                        .Add(SyntaxFactory.ElasticCarriageReturnLineFeed));

            default:
                return invocation;
        }
    }

}

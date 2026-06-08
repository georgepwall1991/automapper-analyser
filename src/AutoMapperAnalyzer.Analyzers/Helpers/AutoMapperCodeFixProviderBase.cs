using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Analyzers.Helpers;

/// <summary>
///     Base class for AutoMapper code fix providers that eliminates common boilerplate code.
///     Provides helper methods for syntax tree operations, diagnostic property extraction,
///     and common fix registration patterns.
/// </summary>
public abstract class AutoMapperCodeFixProviderBase : CodeFixProvider
{
    /// <summary>
    ///     Gets the fix all provider for batch fixes.
    ///     Default implementation uses BatchFixer.
    /// </summary>
    /// <returns>The batch fixer provider.</returns>
    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <summary>
    ///     Context information for code fix operations.
    /// </summary>
    protected class CodeFixOperationContext
    {
        /// <summary>
        ///     Gets the syntax tree root node.
        /// </summary>
        public SyntaxNode Root { get; }

        /// <summary>
        ///     Gets the semantic model for type information.
        /// </summary>
        public SemanticModel SemanticModel { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="CodeFixOperationContext"/> class.
        /// </summary>
        /// <param name="root">The syntax tree root node.</param>
        /// <param name="semanticModel">The semantic model.</param>
        public CodeFixOperationContext(SyntaxNode root, SemanticModel semanticModel)
        {
            Root = root;
            SemanticModel = semanticModel;
        }
    }

    /// <summary>
    ///     Gets the syntax root and semantic model from the code fix context.
    ///     Returns null if either is unavailable.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <returns>The operation context, or null if root or semantic model is unavailable.</returns>
    protected async Task<CodeFixOperationContext?> GetOperationContextAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return null;
        }

        SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (semanticModel == null)
        {
            return null;
        }

        return new CodeFixOperationContext(root, semanticModel);
    }

    /// <summary>
    ///     Tries to extract diagnostic properties by name.
    ///     Returns null if any property is missing or empty.
    /// </summary>
    /// <param name="diagnostic">The diagnostic to extract properties from.</param>
    /// <param name="propertyNames">The property names to extract.</param>
    /// <returns>Dictionary of property values, or null if any property is missing.</returns>
    protected Dictionary<string, string>? TryGetDiagnosticProperties(
        Diagnostic diagnostic,
        params string[] propertyNames)
    {
        var result = diagnostic.Properties
            .Where(property => !string.IsNullOrEmpty(property.Value))
            .ToDictionary(property => property.Key, property => property.Value!);

        foreach (var propertyName in propertyNames)
        {
            if (!result.ContainsKey(propertyName))
            {
                return null;
            }
        }

        return result;
    }

    /// <summary>
    ///     Gets the CreateMap invocation expression from a diagnostic location.
    ///     Returns null if the node at the diagnostic location is not an invocation.
    /// </summary>
    /// <param name="root">The syntax tree root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <param name="diagnosticProperties">Optional diagnostic properties used to locate the mapping invocation.</param>
    /// <returns>The invocation expression, or null if not found.</returns>
    protected InvocationExpressionSyntax? GetCreateMapInvocation(
        SyntaxNode root,
        Diagnostic diagnostic,
        IReadOnlyDictionary<string, string>? diagnosticProperties = null)
    {
        SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node is InvocationExpressionSyntax invocation)
        {
            return invocation;
        }

        if (diagnosticProperties == null ||
            !diagnosticProperties.TryGetValue("MappingInvocationStart", out string? startText) ||
            !diagnosticProperties.TryGetValue("MappingInvocationLength", out string? lengthText) ||
            !int.TryParse(startText, out int start) ||
            !int.TryParse(lengthText, out int length))
        {
            return null;
        }

        var mappingNode = root.FindNode(new TextSpan(start, length));
        return mappingNode as InvocationExpressionSyntax;
    }

    /// <summary>
    ///     Replaces a node in the syntax tree and returns the updated document.
    /// </summary>
    /// <param name="document">The document to update.</param>
    /// <param name="root">The syntax tree root.</param>
    /// <param name="oldNode">The node to replace.</param>
    /// <param name="newNode">The new node.</param>
    /// <returns>The updated document.</returns>
    protected Document ReplaceNode(Document document, SyntaxNode root, SyntaxNode oldNode, SyntaxNode newNode)
    {
        SyntaxNode newRoot = root.ReplaceNode(oldNode, newNode);
        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    ///     Replaces a node in the syntax tree and returns the task with updated document.
    ///     This is a convenience method for use in CodeAction.Create callbacks.
    /// </summary>
    /// <param name="document">The document to update.</param>
    /// <param name="root">The syntax tree root.</param>
    /// <param name="oldNode">The node to replace.</param>
    /// <param name="newNode">The new node.</param>
    /// <returns>Task with the updated document.</returns>
    protected Task<Document> ReplaceNodeAsync(
        Document document,
        SyntaxNode root,
        SyntaxNode oldNode,
        SyntaxNode newNode)
    {
        return Task.FromResult(ReplaceNode(document, root, oldNode, newNode));
    }

    /// <summary>
    ///     Processes diagnostics with a common pattern:
    ///     1. Extracts operation context (root + semantic model)
    ///     2. Iterates through diagnostics and extracts property info
    ///     3. Calls callback for per-property fix registration
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="propertyNames">Property names to extract from diagnostic (e.g., "PropertyName", "PropertyType").</param>
    /// <param name="registerPerPropertyFixes">Callback to register per-property fixes (called once per diagnostic).</param>
    protected async Task ProcessDiagnosticsAsync(
        CodeFixContext context,
        string[] propertyNames,
        Action<CodeFixContext, Diagnostic, InvocationExpressionSyntax, Dictionary<string, string>, SemanticModel, SyntaxNode> registerPerPropertyFixes)
    {
        var operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            var properties = TryGetDiagnosticProperties(diagnostic, propertyNames);
            if (properties == null)
            {
                continue;
            }

            var invocation = GetCreateMapInvocation(operationContext.Root, diagnostic, properties);
            if (invocation == null)
            {
                continue;
            }

            registerPerPropertyFixes(context, diagnostic, invocation, properties, operationContext.SemanticModel, operationContext.Root);
        }
    }

    /// <summary>
    ///     The kind of per-member configuration an aggregate fix appends to a CreateMap.
    /// </summary>
    protected enum AggregateFixKind
    {
        /// <summary>opt.MapFrom(src =&gt; expression) for a destination member.</summary>
        MapFrom,

        /// <summary>opt.Ignore() for a destination member.</summary>
        Ignore,

        /// <summary>opt.DoNotValidate() for a source member (ForSourceMember).</summary>
        DoNotValidate
    }

    /// <summary>
    ///     Describes a single member configuration to fold into an aggregate CreateMap fix.
    ///     Use the factory methods rather than the constructor.
    /// </summary>
    protected sealed class PropertyFixSpec
    {
        private PropertyFixSpec(AggregateFixKind kind, string memberName, string? mapFromExpression)
        {
            Kind = kind;
            MemberName = memberName;
            MapFromExpression = mapFromExpression;
        }

        /// <summary>The configuration kind.</summary>
        public AggregateFixKind Kind { get; }

        /// <summary>The destination member (MapFrom/Ignore) or source member (DoNotValidate) name.</summary>
        public string MemberName { get; }

        /// <summary>The MapFrom expression text (e.g. <c>src.Foo</c> or <c>string.Empty</c>); null otherwise.</summary>
        public string? MapFromExpression { get; }

        /// <summary>Maps <paramref name="destinationName"/> from <paramref name="mapFromExpression"/>.</summary>
        public static PropertyFixSpec MapFrom(string destinationName, string mapFromExpression) =>
            new(AggregateFixKind.MapFrom, destinationName, mapFromExpression);

        /// <summary>Ignores the destination member <paramref name="destinationName"/>.</summary>
        public static PropertyFixSpec Ignore(string destinationName) =>
            new(AggregateFixKind.Ignore, destinationName, null);

        /// <summary>Suppresses source-member validation for <paramref name="sourceName"/>.</summary>
        public static PropertyFixSpec DoNotValidate(string sourceName) =>
            new(AggregateFixKind.DoNotValidate, sourceName, null);
    }

    /// <summary>
    ///     Folds a list of member configurations into a single chained CreateMap invocation
    ///     (<c>CreateMap&lt;,&gt;().ForMember(...).ForMember(...)...</c>) so one
    ///     <see cref="ReplaceNode"/> applies every fix as a single, conflict-free edit. Specs are
    ///     expected to be pre-filtered to members the analyzer flagged (so no member is configured twice).
    /// </summary>
    /// <param name="invocation">The CreateMap invocation to chain onto.</param>
    /// <param name="specs">The member configurations to append, in the order they should appear.</param>
    /// <returns>The chained invocation with every ForMember/ForSourceMember appended.</returns>
    protected static InvocationExpressionSyntax FoldAggregateForMembers(
        InvocationExpressionSyntax invocation,
        IReadOnlyList<PropertyFixSpec> specs)
    {
        InvocationExpressionSyntax accumulated = invocation;
        foreach (PropertyFixSpec spec in specs)
        {
            accumulated = spec.Kind switch
            {
                AggregateFixKind.Ignore =>
                    CodeFixSyntaxHelper.CreateForMemberWithIgnore(accumulated, spec.MemberName),
                AggregateFixKind.MapFrom =>
                    CodeFixSyntaxHelper.CreateForMemberWithMapFrom(accumulated, spec.MemberName, spec.MapFromExpression!),
                AggregateFixKind.DoNotValidate =>
                    CodeFixSyntaxHelper.CreateForSourceMemberWithDoNotValidate(accumulated, spec.MemberName),
                _ => accumulated
            };
        }

        return accumulated;
    }

    /// <summary>
    ///     A set of diagnostics from one rule that all resolve to the same CreateMap invocation, with the
    ///     distinct flagged property names in first-seen order. Used to register a single aggregate fix per
    ///     CreateMap that addresses every one of its diagnostics.
    /// </summary>
    protected sealed class DiagnosticInvocationGroup
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="DiagnosticInvocationGroup"/> class.
        /// </summary>
        /// <param name="invocation">The shared CreateMap (or chained) invocation.</param>
        /// <param name="diagnostics">Every diagnostic resolving to <paramref name="invocation"/>.</param>
        /// <param name="propertyNames">Distinct flagged property names in first-seen order.</param>
        public DiagnosticInvocationGroup(
            InvocationExpressionSyntax invocation,
            ImmutableArray<Diagnostic> diagnostics,
            IReadOnlyList<string> propertyNames)
        {
            Invocation = invocation;
            Diagnostics = diagnostics;
            PropertyNames = propertyNames;
        }

        /// <summary>The shared CreateMap (or chained) invocation the diagnostics target.</summary>
        public InvocationExpressionSyntax Invocation { get; }

        /// <summary>Every diagnostic that resolves to <see cref="Invocation"/>.</summary>
        public ImmutableArray<Diagnostic> Diagnostics { get; }

        /// <summary>Distinct <c>PropertyName</c> values across the group, in first-seen order.</summary>
        public IReadOnlyList<string> PropertyNames { get; }
    }

    /// <summary>
    ///     Groups diagnostics that resolve to the same CreateMap invocation so an aggregate fix can be
    ///     registered once per invocation. Diagnostics missing any of <paramref name="requiredPropertyKeys"/>
    ///     or whose invocation cannot be resolved are skipped.
    /// </summary>
    protected IReadOnlyList<DiagnosticInvocationGroup> GroupDiagnosticsByInvocation(
        SyntaxNode root,
        IEnumerable<Diagnostic> diagnostics,
        params string[] requiredPropertyKeys)
    {
        var builders = new Dictionary<TextSpan, (InvocationExpressionSyntax Invocation, List<Diagnostic> Diagnostics, List<string> Names, HashSet<string> Seen)>();
        var order = new List<TextSpan>();

        foreach (Diagnostic diagnostic in diagnostics)
        {
            var properties = TryGetDiagnosticProperties(diagnostic, requiredPropertyKeys);
            if (properties == null)
            {
                continue;
            }

            InvocationExpressionSyntax? invocation = GetCreateMapInvocation(root, diagnostic, properties);
            if (invocation == null)
            {
                continue;
            }

            // Key by full span (start AND length): a forward CreateMap and its chained ReverseMap() share a
            // start offset, so keying by start alone would merge the two directions into one group and build
            // reverse fixes against the forward invocation.
            TextSpan key = invocation.Span;
            if (!builders.TryGetValue(key, out var builder))
            {
                builder = (invocation, new List<Diagnostic>(), new List<string>(), new HashSet<string>());
                builders[key] = builder;
                order.Add(key);
            }

            builder.Diagnostics.Add(diagnostic);
            if (properties.TryGetValue("PropertyName", out string? propertyName) && builder.Seen.Add(propertyName))
            {
                builder.Names.Add(propertyName);
            }
        }

        return order
            .Select(key => builders[key])
            .Select(b => new DiagnosticInvocationGroup(b.Invocation, b.Diagnostics.ToImmutableArray(), b.Names))
            .ToList();
    }

    /// <summary>
    ///     Registers per-property code fixes for each CreateMap, collapsing them into a tidy lightbulb when a
    ///     map has 2+ flagged properties: aggregate actions (from <paramref name="buildAggregateActions"/>)
    ///     surface first, then a single nested "<paramref name="nestedParentTitle"/>" submenu groups each
    ///     property's options. A single flagged property stays a flat per-property choice (no aggregate, no
    ///     nesting). Shared by AM011/AM006/AM004 so the unmapped-property family behaves consistently.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="diagnosticPropertyKeys">Diagnostic property names required to resolve each diagnostic.</param>
    /// <param name="nestedParentTitle">Title of the nested "fix individual property" parent action.</param>
    /// <param name="buildPerProperty">
    ///     Builds the submenu title and ordered actions for one diagnostic, or null to skip it.
    /// </param>
    /// <param name="buildAggregateActions">Builds the aggregate actions for a 2+ property group (may be empty).</param>
    protected async Task RegisterGroupedPerPropertyFixesAsync(
        CodeFixContext context,
        string[] diagnosticPropertyKeys,
        string nestedParentTitle,
        Func<CodeFixOperationContext, DiagnosticInvocationGroup, Diagnostic, IReadOnlyDictionary<string, string>,
            (string SubMenuTitle, ImmutableArray<CodeAction> Actions)?> buildPerProperty,
        Func<CodeFixOperationContext, DiagnosticInvocationGroup, ImmutableArray<CodeAction>> buildAggregateActions)
    {
        CodeFixOperationContext? operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        foreach (DiagnosticInvocationGroup group in GroupDiagnosticsByInvocation(
                     operationContext.Root, context.Diagnostics, diagnosticPropertyKeys))
        {
            bool isBatch = group.PropertyNames.Count >= 2;
            var perPropertySubMenus = new List<CodeAction>();

            foreach (Diagnostic diagnostic in group.Diagnostics)
            {
                var properties = TryGetDiagnosticProperties(diagnostic, diagnosticPropertyKeys);
                if (properties == null)
                {
                    continue;
                }

                (string SubMenuTitle, ImmutableArray<CodeAction> Actions)? built =
                    buildPerProperty(operationContext, group, diagnostic, properties);
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
                        context.RegisterCodeFix(action, diagnostic);
                    }
                }
            }

            if (!isBatch)
            {
                continue;
            }

            foreach (CodeAction aggregateAction in buildAggregateActions(operationContext, group))
            {
                context.RegisterCodeFix(aggregateAction, group.Diagnostics);
            }

            if (perPropertySubMenus.Count > 0)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(nestedParentTitle, perPropertySubMenus.ToImmutableArray(), isInlinable: false),
                    group.Diagnostics);
            }
        }
    }

    /// <summary>
    ///     Resolves the (source, destination) types for a CreateMap fix, handling the ReverseMap() case.
    ///     When <paramref name="invocation"/> is a <c>ReverseMap()</c> call (which has no generic type
    ///     arguments), walks up the fluent chain to the forward CreateMap and returns its arguments swapped;
    ///     ForMember/ForSourceMember fixes should still be folded onto <paramref name="invocation"/> itself.
    /// </summary>
    /// <param name="invocation">The mapping invocation the diagnostic resolves to.</param>
    /// <param name="semanticModel">The semantic model.</param>
    /// <returns>The (source, destination) types for that direction, or (null, null) if unresolved.</returns>
    protected static (ITypeSymbol? sourceType, ITypeSymbol? destinationType) ResolveCreateMapTypesWithReverse(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, semanticModel, "ReverseMap"))
        {
            InvocationExpressionSyntax? createMap = FindCreateMapInvocation(invocation, semanticModel);
            if (createMap == null)
            {
                return (null, null);
            }

            (ITypeSymbol? forwardSource, ITypeSymbol? forwardDestination) =
                MappingChainAnalysisHelper.GetCreateMapTypeArguments(createMap, semanticModel);

            // Reverse direction: source/destination are swapped.
            return (forwardDestination, forwardSource);
        }

        return MappingChainAnalysisHelper.GetCreateMapTypeArguments(invocation, semanticModel);
    }

    /// <summary>
    ///     Walks up the fluent chain from <paramref name="invocation"/> to the originating CreateMap call.
    /// </summary>
    protected static InvocationExpressionSyntax? FindCreateMapInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        InvocationExpressionSyntax? current = invocation;
        while (current != null)
        {
            if (MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(current, semanticModel, "CreateMap"))
            {
                return current;
            }

            current = (current.Expression as MemberAccessExpressionSyntax)?.Expression as InvocationExpressionSyntax;
        }

        return null;
    }
}

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        var result = new Dictionary<string, string>();

        foreach (var propertyName in propertyNames)
        {
            if (!diagnostic.Properties.TryGetValue(propertyName, out string? value) ||
                string.IsNullOrEmpty(value))
            {
                return null;
            }

            result[propertyName] = value!; // Safe: already validated above
        }

        return result;
    }

    /// <summary>
    ///     Gets the CreateMap invocation expression from a diagnostic location.
    ///     Returns null if the node at the diagnostic location is not an invocation.
    /// </summary>
    /// <param name="root">The syntax tree root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <returns>The invocation expression, or null if not found.</returns>
    protected InvocationExpressionSyntax? GetCreateMapInvocation(SyntaxNode root, Diagnostic diagnostic)
    {
        SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan);
        return node as InvocationExpressionSyntax;
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
    ///     Creates a grouped code action with nested fix options.
    ///     This creates the expandable submenu in the lightbulb UI.
    /// </summary>
    /// <param name="title">The title of the grouped action (e.g., "Fix mapping for 'PropertyName'...").</param>
    /// <param name="nestedActions">The collection of nested actions.</param>
    /// <returns>The grouped code action.</returns>
    protected CodeAction CreateGroupedAction(string title, ImmutableArray<CodeAction> nestedActions)
    {
        return CodeAction.Create(
            title,
            nestedActions,
            isInlinable: true);
    }

    /// <summary>
    ///     Creates a grouped code action with nested fix options using a builder.
    /// </summary>
    /// <param name="title">The title of the grouped action.</param>
    /// <param name="nestedActions">The builder containing nested actions.</param>
    /// <returns>The grouped code action.</returns>
    protected CodeAction CreateGroupedAction(string title, ImmutableArray<CodeAction>.Builder nestedActions)
    {
        return CreateGroupedAction(title, nestedActions.ToImmutable());
    }

    /// <summary>
    ///     Registers a grouped code action for a specific property.
    ///     This is a common pattern across multiple fixers.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="diagnostic">The diagnostic being fixed.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="nestedActions">The nested fix actions.</param>
    protected void RegisterGroupedPropertyFix(
        CodeFixContext context,
        Diagnostic diagnostic,
        string propertyName,
        ImmutableArray<CodeAction> nestedActions)
    {
        var groupAction = CreateGroupedAction($"Fix mapping for '{propertyName}'...", nestedActions);
        context.RegisterCodeFix(groupAction, diagnostic);
    }

    /// <summary>
    ///     Registers a grouped code action for a specific property using a builder.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="diagnostic">The diagnostic being fixed.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="nestedActionsBuilder">The builder containing nested actions.</param>
    protected void RegisterGroupedPropertyFix(
        CodeFixContext context,
        Diagnostic diagnostic,
        string propertyName,
        ImmutableArray<CodeAction>.Builder nestedActionsBuilder)
    {
        RegisterGroupedPropertyFix(context, diagnostic, propertyName, nestedActionsBuilder.ToImmutable());
    }

    /// <summary>
    ///     Registers bulk fix actions that apply to all diagnostics in the context.
    ///     This is a common pattern for fixers that support bulk operations.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="bulkActions">The bulk fix actions to register.</param>
    protected void RegisterBulkFixes(CodeFixContext context, params CodeAction[] bulkActions)
    {
        foreach (var action in bulkActions)
        {
            context.RegisterCodeFix(action, context.Diagnostics);
        }
    }

    /// <summary>
    ///     Information about a property that needs fixing.
    /// </summary>
    protected class PropertyFixInfo
    {
        /// <summary>
        ///     Gets the property name.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        ///     Gets the property type as a string.
        /// </summary>
        public string PropertyType { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="PropertyFixInfo"/> class.
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <param name="propertyType">The property type.</param>
        public PropertyFixInfo(string propertyName, string propertyType)
        {
            PropertyName = propertyName;
            PropertyType = propertyType;
        }
    }

    /// <summary>
    ///     Processes diagnostics with a common pattern:
    ///     1. Extracts operation context (root + semantic model)
    ///     2. Deduplicates bulk fixes per invocation
    ///     3. Iterates through diagnostics and extracts property info
    ///     4. Calls callbacks for bulk and per-property fix registration
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="propertyNames">Property names to extract from diagnostic (e.g., "PropertyName", "PropertyType").</param>
    /// <param name="registerBulkFixes">Callback to register bulk fixes (called once per invocation).</param>
    /// <param name="registerPerPropertyFixes">Callback to register per-property fixes (called once per diagnostic).</param>
    protected async Task ProcessDiagnosticsAsync(
        CodeFixContext context,
        string[] propertyNames,
        Action<CodeFixContext, InvocationExpressionSyntax, SemanticModel, SyntaxNode>? registerBulkFixes,
        Action<CodeFixContext, Diagnostic, InvocationExpressionSyntax, Dictionary<string, string>, SemanticModel, SyntaxNode>? registerPerPropertyFixes)
    {
        var operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        var handledInvocations = new HashSet<InvocationExpressionSyntax>();

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            var properties = TryGetDiagnosticProperties(diagnostic, propertyNames);
            if (properties == null)
            {
                continue;
            }

            var invocation = GetCreateMapInvocation(operationContext.Root, diagnostic);
            if (invocation == null)
            {
                continue;
            }

            // Register bulk fixes (only once per invocation)
            if (registerBulkFixes != null && handledInvocations.Add(invocation))
            {
                registerBulkFixes(context, invocation, operationContext.SemanticModel, operationContext.Root);
            }

            // Register per-property fixes
            registerPerPropertyFixes?.Invoke(context, diagnostic, invocation, properties, operationContext.SemanticModel, operationContext.Root);
        }
    }
}

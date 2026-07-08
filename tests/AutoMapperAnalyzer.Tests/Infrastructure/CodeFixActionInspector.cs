using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Tests.Infrastructure;

/// <summary>
///     Introspects the code actions a <see cref="CodeFixProvider"/> registers for a diagnostic,
///     flattening any nested (sub-menu) actions so tests can assert on the full action tree by
///     equivalence key and on the number of top-level lightbulb entries.
///     Replaces the per-file ad-hoc <c>RegisterActionsAsync</c> helpers, and adds nesting awareness
///     needed by the fixer item-picker redesign (aggregate + grouped per-property actions).
/// </summary>
internal static class CodeFixActionInspector
{
    /// <summary>
    ///     A single code action in the (possibly nested) registration tree.
    /// </summary>
    /// <param name="Title">The action's display title.</param>
    /// <param name="EquivalenceKey">The action's equivalence key (used for FixAll grouping / selection).</param>
    /// <param name="Depth">0 for a top-level lightbulb entry, 1+ for nested sub-menu entries.</param>
    /// <param name="IsNested">True when the action lives under a parent action (Depth &gt; 0).</param>
    /// <param name="HasChildren">True when the action is itself a parent with nested children.</param>
    public sealed record ActionInfo(
        string Title,
        string? EquivalenceKey,
        int Depth,
        bool IsNested,
        bool HasChildren);

    /// <summary>
    ///     Registers the provider's fixes for a single diagnostic and returns the flattened action tree.
    /// </summary>
    public static Task<IReadOnlyList<ActionInfo>> GetActionsAsync(
        Document document,
        CodeFixProvider provider,
        Diagnostic diagnostic)
    {
        return GetActionsAsync(document, provider, ImmutableArray.Create(diagnostic));
    }

    /// <summary>
    ///     Registers the provider's fixes for a set of diagnostics that share a code-fix span (as the IDE
    ///     does when several diagnostics overlap one caret) and returns the flattened action tree. Required
    ///     to exercise aggregate actions, which only register when 2+ diagnostics are present.
    /// </summary>
    public static async Task<IReadOnlyList<ActionInfo>> GetActionsAsync(
        Document document,
        CodeFixProvider provider,
        ImmutableArray<Diagnostic> diagnostics)
    {
        ImmutableArray<Diagnostic> contextDiagnostics = NormalizeForCodeFixContext(diagnostics);
        var topLevel = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            contextDiagnostics[0].Location.SourceSpan,
            contextDiagnostics,
            (action, _) => topLevel.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);

        return Flatten(topLevel);
    }

    /// <summary>
    ///     CodeFixContext requires identical SourceSpans on every diagnostic. When property-token
    ///     diagnostics differ, keep only the first (providers recompute siblings). When metadata
    ///     pile-up already shares CreateMap spans, keep the full set for aggregate registration.
    /// </summary>
    private static ImmutableArray<Diagnostic> NormalizeForCodeFixContext(ImmutableArray<Diagnostic> diagnostics)
    {
        TextSpan first = diagnostics[0].Location.SourceSpan;
        if (diagnostics.All(d => d.Location.SourceSpan == first))
        {
            return diagnostics;
        }

        return ImmutableArray.Create(diagnostics[0]);
    }

    /// <summary>
    ///     Flattens a sequence of top-level code actions (recursing into nested sub-menu actions)
    ///     into a depth-annotated list. Exposed directly so the flattening can be exercised against
    ///     synthetic nested actions without a provider/document round-trip.
    /// </summary>
    public static IReadOnlyList<ActionInfo> Flatten(IEnumerable<CodeAction> topLevelActions)
    {
        var flattened = new List<ActionInfo>();
        foreach (CodeAction action in topLevelActions)
        {
            Flatten(action, depth: 0, flattened);
        }

        return flattened;
    }

    /// <summary>
    ///     The number of top-level lightbulb entries (parents and flat leaves at depth 0).
    /// </summary>
    public static int TopLevelCount(IEnumerable<ActionInfo> actions)
    {
        return actions.Count(a => a.Depth == 0);
    }

    /// <summary>
    ///     Registers the provider's fixes for the full diagnostic set (as the IDE does when several
    ///     diagnostics overlap one caret), selects the action whose equivalence key matches
    ///     <paramref name="equivalenceKey"/>, applies it, and returns the changed document. This is the
    ///     correct way to exercise aggregate (multi-diagnostic) actions, which the standard per-diagnostic
    ///     code-fix verifier cannot register.
    /// </summary>
    public static async Task<Document> ApplyActionByKeyAsync(
        Document document,
        CodeFixProvider provider,
        ImmutableArray<Diagnostic> diagnostics,
        string equivalenceKey)
    {
        ImmutableArray<Diagnostic> contextDiagnostics = NormalizeForCodeFixContext(diagnostics);
        var topLevel = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            contextDiagnostics[0].Location.SourceSpan,
            contextDiagnostics,
            (action, _) => topLevel.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);

        CodeAction? action = FindByKey(topLevel, equivalenceKey);
        if (action == null)
        {
            string available = string.Join(", ", Flatten(topLevel).Select(a => a.EquivalenceKey ?? "(null)"));
            throw new InvalidOperationException(
                $"No code action with equivalence key '{equivalenceKey}'. Available keys: {available}");
        }

        ImmutableArray<CodeActionOperation> operations =
            await action.GetOperationsAsync(CancellationToken.None);
        foreach (CodeActionOperation operation in operations)
        {
            if (operation is ApplyChangesOperation applyChanges)
            {
                return applyChanges.ChangedSolution.GetDocument(document.Id)!;
            }
        }

        throw new InvalidOperationException($"Code action '{equivalenceKey}' produced no document change.");
    }

    private static CodeAction? FindByKey(IEnumerable<CodeAction> actions, string equivalenceKey)
    {
        foreach (CodeAction action in actions)
        {
            if (action.EquivalenceKey == equivalenceKey)
            {
                return action;
            }

            CodeAction? nestedMatch = FindByKey(action.NestedActions, equivalenceKey);
            if (nestedMatch != null)
            {
                return nestedMatch;
            }
        }

        return null;
    }

    private static void Flatten(CodeAction action, int depth, List<ActionInfo> sink)
    {
        // CodeAction.NestedActions is the public accessor for sub-menu (grouped) actions.
        ImmutableArray<CodeAction> nested = action.NestedActions;
        bool hasChildren = !nested.IsDefaultOrEmpty;

        sink.Add(new ActionInfo(
            action.Title,
            action.EquivalenceKey,
            depth,
            IsNested: depth > 0,
            HasChildren: hasChildren));

        if (!hasChildren)
        {
            return;
        }

        foreach (CodeAction child in nested)
        {
            Flatten(child, depth + 1, sink);
        }
    }
}

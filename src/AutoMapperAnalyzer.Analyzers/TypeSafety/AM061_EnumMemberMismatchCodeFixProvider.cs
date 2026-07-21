using System.Collections.Immutable;
using System.Composition;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Analyzers.TypeSafety;

/// <summary>
///     Code fix provider for AM061. Offers a name-based Enum.Parse conversion for the flagged
///     member (only when every source member name exists in the destination enum and the source
///     cannot be null, so the generated parse cannot throw), or an explicit Ignore so the data
///     loss becomes a deliberate choice. Diagnostics produced from an explicit direct MapFrom
///     rewrite that member configuration in place instead of appending a duplicate ForMember.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM061_EnumMemberMismatchCodeFixProvider))]
[Shared]
public class AM061_EnumMemberMismatchCodeFixProvider : AutoMapperCodeFixProviderBase
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM061");

    /// <summary>
    ///     Batch FixAll is disabled: multiple flagged members on one registration would append
    ///     conflicting edits at the same chain position, and solution-wide FixAll could add the
    ///     same member configuration to several profiles.
    /// </summary>
    public override FixAllProvider GetFixAllProvider() => null!;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        CodeFixOperationContext? operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            Dictionary<string, string>? properties = TryGetDiagnosticProperties(
                diagnostic,
                "PropertyName",
                "SourcePropertyName",
                "DestinationEnumName",
                "MapByNameSafe");
            if (properties == null)
            {
                continue;
            }

            string destinationProperty = properties["PropertyName"];
            string sourceProperty = properties["SourcePropertyName"];
            string destinationEnum = properties["DestinationEnumName"];
            bool mapByNameSafe = properties["MapByNameSafe"] == "true";

            // Diagnostics from an explicit direct MapFrom rewrite the owning ForMember in place;
            // appending another ForMember would be overridden by the original (last wins).
            if (TryGetExplicitForMember(operationContext, diagnostic, out InvocationExpressionSyntax? forMemberCall))
            {
                RegisterInPlaceFixes(
                    context,
                    operationContext,
                    diagnostic,
                    forMemberCall!,
                    destinationProperty,
                    sourceProperty,
                    destinationEnum,
                    mapByNameSafe);
                continue;
            }

            SyntaxNode node = operationContext.Root.FindNode(diagnostic.Location.SourceSpan);
            InvocationExpressionSyntax? registration =
                node as InvocationExpressionSyntax ?? node.FirstAncestorOrSelf<InvocationExpressionSyntax>();

            // Only extend registrations that sit in a fluent statement chain or local initializer;
            // argument-position registrations would risk changing the surrounding expression type.
            if (registration == null || !IsExtendableRegistration(registration))
            {
                continue;
            }

            if (mapByNameSafe)
            {
                string mapFromExpression = BuildNameBasedMapFromExpression(sourceProperty, destinationEnum);
                InvocationExpressionSyntax mapByNameRegistration = CodeFixSyntaxHelper
                    .CreateForMemberWithMapFrom(registration, destinationProperty, mapFromExpression)
                    .WithTriviaFrom(registration);

                context.RegisterCodeFix(
                    CodeAction.Create(
                        $"Map '{destinationProperty}' by name via Enum.Parse",
                        _ => ReplaceNodeAsync(
                            context.Document,
                            operationContext.Root,
                            registration,
                            mapByNameRegistration),
                        $"AM061_MapByName_{destinationProperty}"),
                    diagnostic);
            }

            InvocationExpressionSyntax ignoreRegistration = CodeFixSyntaxHelper
                .CreateForMemberWithIgnore(registration, destinationProperty)
                .WithTriviaFrom(registration);

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Ignore enum property '{destinationProperty}'",
                    _ => ReplaceNodeAsync(
                        context.Document,
                        operationContext.Root,
                        registration,
                        ignoreRegistration),
                    $"AM061_Ignore_{destinationProperty}"),
                diagnostic);
        }
    }

    private static bool TryGetExplicitForMember(
        CodeFixOperationContext operationContext,
        Diagnostic diagnostic,
        out InvocationExpressionSyntax? forMemberCall)
    {
        forMemberCall = null;

        if (!diagnostic.Properties.TryGetValue("MappingInvocationStart", out string? startText) ||
            !diagnostic.Properties.TryGetValue("MappingInvocationLength", out string? lengthText) ||
            !int.TryParse(startText, out int start) ||
            !int.TryParse(lengthText, out int length))
        {
            return false;
        }

        SyntaxNode node = operationContext.Root.FindNode(new TextSpan(start, length));
        forMemberCall = node as InvocationExpressionSyntax ?? node.FirstAncestorOrSelf<InvocationExpressionSyntax>();

        return forMemberCall != null &&
               forMemberCall.Expression is MemberAccessExpressionSyntax &&
               forMemberCall.ArgumentList.Arguments.Count >= 2;
    }

    private void RegisterInPlaceFixes(
        CodeFixContext context,
        CodeFixOperationContext operationContext,
        Diagnostic diagnostic,
        InvocationExpressionSyntax forMemberCall,
        string destinationProperty,
        string sourceProperty,
        string destinationEnum,
        bool mapByNameSafe)
    {
        if (mapByNameSafe)
        {
            ArgumentSyntax mapFromOptions = CreateMapFromOptionsArgument(sourceProperty, destinationEnum);
            InvocationExpressionSyntax rewritten = WithRebuiltOptions(forMemberCall, mapFromOptions);

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Map '{destinationProperty}' by name via Enum.Parse",
                    _ => ReplaceNodeAsync(context.Document, operationContext.Root, forMemberCall, rewritten),
                    $"AM061_MapByName_{destinationProperty}"),
                diagnostic);
        }

        ArgumentSyntax ignoreOptions = CreateIgnoreOptionsArgument();
        InvocationExpressionSyntax ignored = WithRebuiltOptions(forMemberCall, ignoreOptions);

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Ignore enum property '{destinationProperty}'",
                _ => ReplaceNodeAsync(context.Document, operationContext.Root, forMemberCall, ignored),
                $"AM061_Ignore_{destinationProperty}"),
            diagnostic);
    }

    private static InvocationExpressionSyntax WithRebuiltOptions(
        InvocationExpressionSyntax forMemberCall,
        ArgumentSyntax optionsArgument)
    {
        // The original destination selector (lambda, nameof, or string) is preserved verbatim;
        // only the options lambda is replaced.
        return forMemberCall.WithArgumentList(
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(new[]
                {
                    forMemberCall.ArgumentList.Arguments[0],
                    optionsArgument
                })));
    }

    private static ArgumentSyntax CreateMapFromOptionsArgument(string sourceProperty, string destinationEnum)
    {
        string mapFromExpression = BuildNameBasedMapFromExpression(sourceProperty, destinationEnum);
        return SyntaxFactory.Argument(
            SyntaxFactory.SimpleLambdaExpression(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("opt")),
                SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("opt"),
                            SyntaxFactory.IdentifierName("MapFrom")))
                    .WithArgumentList(
                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Argument(
                                    SyntaxFactory.SimpleLambdaExpression(
                                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("src")),
                                        SyntaxFactory.ParseExpression(mapFromExpression))))))));
    }

    private static ArgumentSyntax CreateIgnoreOptionsArgument()
    {
        return SyntaxFactory.Argument(
            SyntaxFactory.SimpleLambdaExpression(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("opt")),
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("opt"),
                        SyntaxFactory.IdentifierName("Ignore")))));
    }

    private static string BuildNameBasedMapFromExpression(string sourceProperty, string destinationEnum)
    {
        string escapedSourceProperty = CodeFixSyntaxHelper.EscapeIdentifier(sourceProperty);
        return $"({destinationEnum})global::System.Enum.Parse(typeof({destinationEnum}), src.{escapedSourceProperty}.ToString())";
    }

    private static bool IsExtendableRegistration(InvocationExpressionSyntax registration)
    {
        return registration.Parent is ExpressionStatementSyntax
            or MemberAccessExpressionSyntax
            or EqualsValueClauseSyntax;
    }
}

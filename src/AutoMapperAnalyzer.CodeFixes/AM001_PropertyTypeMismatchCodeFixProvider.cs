using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace AutoMapperAnalyzer.CodeFixes;

/// <summary>
/// Code fix provider for AM001 Property Type Mismatch diagnostics.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM001_PropertyTypeMismatchCodeFixProvider)), Shared]
public class AM001_PropertyTypeMismatchCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM001");

    /// <summary>
    /// Gets whether this provider can fix multiple diagnostics in a single code action.
    /// </summary>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>
    /// Registers code fixes for the diagnostics.
    /// </summary>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the CreateMap invocation that triggered the diagnostic
        var invocation = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(i => i.Span.Contains(diagnosticSpan));

        if (invocation == null) return;

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel == null) return;

        // Extract property name from diagnostic message
        var propertyName = ExtractPropertyNameFromDiagnostic(diagnostic);
        if (string.IsNullOrEmpty(propertyName)) return;

        // Register different fix strategies based on the diagnostic descriptor
        if (diagnostic.Descriptor.Id == "AM001")
        {
            // Check which specific rule was triggered
            var messageFormat = diagnostic.Descriptor.MessageFormat.ToString();
            
            if (messageFormat.Contains("incompatible types"))
            {
                // Property Type Mismatch - offer ForMember with conversion
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Add ForMember configuration for '{propertyName}' with type conversion",
                        createChangedDocument: c => AddForMemberWithConversion(context.Document, invocation, propertyName, c),
                        equivalenceKey: $"AM001_ForMember_{propertyName}"),
                    diagnostic);

                // Offer to ignore the property if types are incompatible
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Ignore property '{propertyName}'",
                        createChangedDocument: c => AddForMemberWithIgnore(context.Document, invocation, propertyName, c),
                        equivalenceKey: $"AM001_Ignore_{propertyName}"),
                    diagnostic);
            }
            else if (messageFormat.Contains("nullable compatibility"))
            {
                // Nullable Compatibility Issue - offer null handling
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Add null handling for property '{propertyName}'",
                        createChangedDocument: c => AddForMemberWithNullHandling(context.Document, invocation, propertyName, c),
                        equivalenceKey: $"AM001_NullHandling_{propertyName}"),
                    diagnostic);
            }
            else if (messageFormat.Contains("Complex type mapping"))
            {
                // Complex Type Mapping Missing - offer to create mapping
                var types = ExtractTypesFromDiagnostic(diagnostic);
                if (types.sourceType != null && types.destType != null)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: $"Add CreateMap<{types.sourceType}, {types.destType}>() configuration",
                            createChangedDocument: c => AddCreateMapForComplexTypes(context.Document, invocation, types.sourceType, types.destType, c),
                            equivalenceKey: $"AM001_CreateMap_{types.sourceType}_{types.destType}"),
                        diagnostic);
                }
            }
        }
    }

    private async Task<Document> AddForMemberWithConversion(
        Document document,
        InvocationExpressionSyntax createMapInvocation,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        var generator = editor.Generator;

        // Create ForMember call with conversion
        var forMemberCall = generator.InvocationExpression(
            generator.MemberAccessExpression(createMapInvocation, "ForMember"),
            generator.Argument(
                generator.ValueReturningLambdaExpression(
                    "dest",
                    generator.MemberAccessExpression(
                        generator.IdentifierName("dest"),
                        propertyName))),
            generator.Argument(
                generator.ValueReturningLambdaExpression(
                    "opt",
                    generator.InvocationExpression(
                        generator.MemberAccessExpression(
                            generator.IdentifierName("opt"),
                            "MapFrom"),
                        generator.ValueReturningLambdaExpression(
                            "src",
                            generator.InvocationExpression(
                                generator.MemberAccessExpression(
                                    generator.MemberAccessExpression(
                                        generator.IdentifierName("src"),
                                        propertyName),
                                    "ToString")))))));

        // Replace the original invocation with the chained call
        editor.ReplaceNode(createMapInvocation, forMemberCall);

        return editor.GetChangedDocument();
    }

    private async Task<Document> AddForMemberWithIgnore(
        Document document,
        InvocationExpressionSyntax createMapInvocation,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        var generator = editor.Generator;

        // Create ForMember call with Ignore
        var forMemberCall = generator.InvocationExpression(
            generator.MemberAccessExpression(createMapInvocation, "ForMember"),
            generator.Argument(
                generator.ValueReturningLambdaExpression(
                    "dest",
                    generator.MemberAccessExpression(
                        generator.IdentifierName("dest"),
                        propertyName))),
            generator.Argument(
                generator.ValueReturningLambdaExpression(
                    "opt",
                    generator.InvocationExpression(
                        generator.MemberAccessExpression(
                            generator.IdentifierName("opt"),
                            "Ignore")))));

        // Replace the original invocation with the chained call
        editor.ReplaceNode(createMapInvocation, forMemberCall);

        return editor.GetChangedDocument();
    }

    private async Task<Document> AddForMemberWithNullHandling(
        Document document,
        InvocationExpressionSyntax createMapInvocation,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        var generator = editor.Generator;

        // Create ForMember call with null handling
        var forMemberCall = generator.InvocationExpression(
            generator.MemberAccessExpression(createMapInvocation, "ForMember"),
            generator.Argument(
                generator.ValueReturningLambdaExpression(
                    "dest",
                    generator.MemberAccessExpression(
                        generator.IdentifierName("dest"),
                        propertyName))),
            generator.Argument(
                generator.ValueReturningLambdaExpression(
                    "opt",
                    generator.InvocationExpression(
                        generator.MemberAccessExpression(
                            generator.IdentifierName("opt"),
                            "MapFrom"),
                        generator.ValueReturningLambdaExpression(
                            "src",
                            generator.CoalesceExpression(
                                generator.MemberAccessExpression(
                                    generator.IdentifierName("src"),
                                    propertyName),
                                generator.DefaultExpression(generator.IdentifierName("var"))))))));

        // Replace the original invocation with the chained call
        editor.ReplaceNode(createMapInvocation, forMemberCall);

        return editor.GetChangedDocument();
    }

    private async Task<Document> AddCreateMapForComplexTypes(
        Document document,
        InvocationExpressionSyntax existingCreateMap,
        string sourceTypeName,
        string destTypeName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Find the containing class/method where we should add the new CreateMap
        var containingMember = existingCreateMap.Ancestors()
            .OfType<MemberDeclarationSyntax>()
            .FirstOrDefault();

        if (containingMember == null) return document;

        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        var generator = editor.Generator;

        // Create the new CreateMap statement
        var newCreateMap = generator.ExpressionStatement(
            generator.InvocationExpression(
                generator.GenericName("CreateMap",
                    generator.IdentifierName(sourceTypeName),
                    generator.IdentifierName(destTypeName))));

        // Add the new CreateMap after the existing one
        if (containingMember is MethodDeclarationSyntax method)
        {
            var existingStatement = existingCreateMap.Ancestors()
                .OfType<StatementSyntax>()
                .FirstOrDefault();

            if (existingStatement != null)
            {
                editor.InsertAfter(existingStatement, new[] { newCreateMap });
            }
        }
        else if (containingMember is ConstructorDeclarationSyntax constructor)
        {
            var existingStatement = existingCreateMap.Ancestors()
                .OfType<StatementSyntax>()
                .FirstOrDefault();

            if (existingStatement != null)
            {
                editor.InsertAfter(existingStatement, new[] { newCreateMap });
            }
        }

        return editor.GetChangedDocument();
    }

    private string ExtractPropertyNameFromDiagnostic(Diagnostic diagnostic)
    {
        // Extract property name from the diagnostic message
        // Message format: "Property 'PropertyName' has incompatible types..."
        var message = diagnostic.GetMessage();
        var startIndex = message.IndexOf('\'');
        if (startIndex == -1) return string.Empty;
        
        var endIndex = message.IndexOf('\'', startIndex + 1);
        if (endIndex == -1) return string.Empty;
        
        return message.Substring(startIndex + 1, endIndex - startIndex - 1);
    }

    private (string? sourceType, string? destType) ExtractTypesFromDiagnostic(Diagnostic diagnostic)
    {
        // Extract type names from diagnostic message for complex type mapping
        // Message format: "...Consider adding CreateMap<SourceType, DestType>()."
        var message = diagnostic.GetMessage();
        var createMapIndex = message.IndexOf("CreateMap<");
        if (createMapIndex == -1) return (null, null);

        var startIndex = createMapIndex + "CreateMap<".Length;
        var endIndex = message.IndexOf(">", startIndex);
        if (endIndex == -1) return (null, null);

        var typesPart = message.Substring(startIndex, endIndex - startIndex);
        var types = typesPart.Split(',');
        
        if (types.Length != 2) return (null, null);
        
        return (types[0].Trim(), types[1].Trim());
    }
}
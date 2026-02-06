using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Analyzers.ComplexMappings;

/// <summary>
///     Code fix provider for AM030 diagnostic - Custom Type Converter issues.
///     Provides fixes for missing ConvertUsing configurations and invalid converter implementations.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AM030_CustomTypeConverterCodeFixProvider))]
[Shared]
public class AM030_CustomTypeConverterCodeFixProvider : AutoMapperCodeFixProviderBase
{
    private const string IssueTypePropertyName = "IssueType";
    private const string MissingConvertUsingIssueType = "MissingConvertUsing";

    /// <summary>
    ///     Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("AM030");

    /// <summary>
    ///     Registers code fixes for the specified context.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var operationContext = await GetOperationContextAsync(context);
        if (operationContext == null)
        {
            return;
        }

        foreach (Diagnostic? diagnostic in context.Diagnostics)
        {
            if (diagnostic.Id != "AM030")
            {
                continue;
            }

            if (!diagnostic.Properties.TryGetValue(IssueTypePropertyName, out string? issueType) ||
                issueType != MissingConvertUsingIssueType)
            {
                continue;
            }

            if (!diagnostic.Properties.TryGetValue("PropertyName", out string? propertyName) ||
                string.IsNullOrEmpty(propertyName))
            {
                continue;
            }

            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;
            SyntaxNode node = operationContext.Root.FindNode(diagnosticSpan);

            if (node.FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation)
            {
                continue;
            }

            RegisterMissingConvertUsingFixes(context, operationContext.Root, invocation, propertyName!, diagnostic);
        }
    }

    private void RegisterMissingConvertUsingFixes(CodeFixContext context, SyntaxNode root,
        InvocationExpressionSyntax invocation, string propertyName, Diagnostic diagnostic)
    {
        // Fix 1: Add ForMember with ConvertUsing for string to primitive conversions
        if (diagnostic.Properties.TryGetValue("ConverterType", out string? converterType) &&
            converterType!.Contains("String") && IsStringToPrimitiveConversion(converterType))
        {
            var lambdaFix = CodeAction.Create(
                $"Add ConvertUsing with lambda for '{propertyName}'",
                cancellationToken =>
                {
                    string conversion = GetStringToPrimitiveConversion(converterType!);
                    InvocationExpressionSyntax newInvocation = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
                        invocation,
                        propertyName,
                        $"{conversion}(src.{propertyName})");
                    SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                $"ConvertUsingLambda_{propertyName}");

            context.RegisterCodeFix(lambdaFix, diagnostic);
        }

        // Fix 2: Generate and use custom value converter class
        var generateConverterFix = CodeAction.Create(
            $"Generate and use ValueConverter for '{propertyName}'",
            cancellationToken => GenerateValueConverterClassAsync(context.Document, invocation, propertyName, cancellationToken),
            $"GenerateValueConverter_{propertyName}");

        context.RegisterCodeFix(generateConverterFix, diagnostic);

        // Fix 3: Ignore the property if conversion is too complex
        var ignoreFix = CodeAction.Create(
            $"Ignore property '{propertyName}' (conversion too complex)",
            cancellationToken =>
            {
                InvocationExpressionSyntax newInvocation =
                    CodeFixSyntaxHelper.CreateForMemberWithIgnore(invocation, propertyName);
                SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
                return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
            },
            $"Ignore_{propertyName}");

        context.RegisterCodeFix(ignoreFix, diagnostic);
    }

    private async Task<Solution> GenerateValueConverterClassAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel == null) return document.Project.Solution;

        var (sourceType, destType) = AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
        if (sourceType == null || destType == null) return document.Project.Solution;

        var sourceProp = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType).FirstOrDefault(p => p.Name == propertyName);
        var destProp = AutoMapperAnalysisHelpers.GetMappableProperties(destType).FirstOrDefault(p => p.Name == propertyName);

        // If we can't find properties, fallback to object
        string sourcePropType = sourceProp?.Type.ToDisplayString() ?? "object";
        string destPropType = destProp?.Type.ToDisplayString() ?? "object";
        string converterName = $"{propertyName}Converter";

        // 1. Create the converter class syntax
        var converterClass = SyntaxFactory.ClassDeclaration(converterName)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
            .WithBaseList(SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName($"IValueConverter<{sourcePropType}, {destPropType}>")))))
            .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName(destPropType), "Convert")
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(new[] {
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("sourceMember")).WithType(SyntaxFactory.ParseTypeName(sourcePropType)),
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("context")).WithType(SyntaxFactory.ParseTypeName("ResolutionContext"))
                    })))
                    .WithBody(SyntaxFactory.Block(
                        SyntaxFactory.ThrowStatement(
                            SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName("NotImplementedException"))
                                .WithArgumentList(SyntaxFactory.ArgumentList()))))
            ));

        // 2. Add class to the Profile class
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var profileClass = invocation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (profileClass == null || root == null) return document.Project.Solution;

        var newProfileClass = profileClass.AddMembers(converterClass);
        var newRoot = root.ReplaceNode(profileClass, newProfileClass);

        // 3. Update the mapping to use the new converter
        // We need to update the invocation in the NEW root
        // invocation might have been replaced if we updated profileClass? No, ReplaceNode returns new root.
        // But invocation is a node in the OLD root. We need to find it in the new root.
        // Or we can do both changes at once? No, easier to chain.
        
        // Actually, updating the invocation is easier first, then adding member?
        // No, `ReplaceNode` creates a new tree.
        
        // Let's do it in one go by tracking nodes? Or just replace invocation in the newRoot.
        // Since we replaced `profileClass`, `invocation` is gone in `newRoot`.
        // We can find the equivalent node in `newRoot` via `profileClass` location?
        // Or use `TrackNodes`.
        
        root = root.TrackNodes(invocation, profileClass);
        newProfileClass = root.GetCurrentNode(profileClass)!.AddMembers(converterClass);
        newRoot = root.ReplaceNode(root.GetCurrentNode(profileClass)!, newProfileClass);
        
        var currentInvocation = newRoot.GetCurrentNode(invocation);
        
        // Create .ForMember(d => d.Prop, opt => opt.ConvertUsing(new Converter(), src => src.Prop))
        var newInvocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    currentInvocation!,
                    SyntaxFactory.IdentifierName("ForMember")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(new[]
                    {
                        // dest => dest.PropertyName
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("dest")),
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("dest"),
                                    SyntaxFactory.IdentifierName(propertyName)))),
                        // opt => opt.ConvertUsing(new Converter(), src => src.PropertyName)
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("opt")),
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName("opt"),
                                        SyntaxFactory.IdentifierName("ConvertUsing")))
                                    .WithArgumentList(
                                        SyntaxFactory.ArgumentList(
                                            SyntaxFactory.SeparatedList(new[] {
                                                SyntaxFactory.Argument(
                                                    SyntaxFactory.ObjectCreationExpression(SyntaxFactory.IdentifierName(converterName))
                                                        .WithArgumentList(SyntaxFactory.ArgumentList())),
                                                SyntaxFactory.Argument(
                                                    SyntaxFactory.SimpleLambdaExpression(
                                                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("src")),
                                                        SyntaxFactory.MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            SyntaxFactory.IdentifierName("src"),
                                                            SyntaxFactory.IdentifierName(propertyName))))
                                            })))
                            ))
                    })));

        newRoot = newRoot.ReplaceNode(currentInvocation!, newInvocation);
        
        return document.Project.Solution.WithDocumentSyntaxRoot(document.Id, newRoot);
    }

    private bool IsStringToPrimitiveConversion(string converterType)
    {
        return converterType.Contains("Int") ||
               converterType.Contains("Double") ||
               converterType.Contains("Decimal") ||
               converterType.Contains("Boolean") ||
               converterType.Contains("DateTime") ||
               converterType.Contains("Guid");
    }

    private string GetStringToPrimitiveConversion(string converterType)
    {
        if (converterType.Contains("Int"))
        {
            return "Convert.ToInt32";
        }

        if (converterType.Contains("Double"))
        {
            return "Convert.ToDouble";
        }

        if (converterType.Contains("Decimal"))
        {
            return "Convert.ToDecimal";
        }

        if (converterType.Contains("Boolean"))
        {
            return "Convert.ToBoolean";
        }

        if (converterType.Contains("DateTime"))
        {
            return "DateTime.Parse"; // Convert.ToDateTime also exists but Parse is idiomatic
        }

        if (converterType.Contains("Guid"))
        {
            return "Guid.Parse";
        }

        return "Convert.ChangeType";
    }
}

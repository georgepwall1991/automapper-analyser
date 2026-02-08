using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace AutoMapperAnalyzer.Analyzers.Helpers;

/// <summary>
///     Helper class for creating common AutoMapper code fix syntax patterns.
///     Reduces duplication across multiple CodeFix providers.
/// </summary>
public static class CodeFixSyntaxHelper
{
    /// <summary>
    ///     Creates a ForMember call with MapFrom configuration.
    ///     Example: .ForMember(dest => dest.PropertyName, opt => opt.MapFrom(src => expression))
    /// </summary>
    /// <param name="invocation">The original CreateMap invocation to chain from.</param>
    /// <param name="propertyName">The destination property name.</param>
    /// <param name="mapFromExpression">The expression to use in MapFrom (e.g., "src.SourceProperty" or "0").</param>
    /// <returns>A new InvocationExpressionSyntax with ForMember chained.</returns>
    public static InvocationExpressionSyntax CreateForMemberWithMapFrom(
        InvocationExpressionSyntax invocation,
        string propertyName,
        string mapFromExpression)
    {
        return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    invocation,
                    SyntaxFactory.IdentifierName("ForMember")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(new[]
                    {
                        // First argument: dest => dest.PropertyName
                        SyntaxFactory.Argument(
                            CreatePropertySelectorLambda("dest", propertyName)),
                        // Second argument: opt => opt.MapFrom(src => expression)
                        SyntaxFactory.Argument(
                            CreateMapFromLambda(mapFromExpression))
                    })));
    }

    /// <summary>
    ///     Creates a ForMember call with Ignore configuration.
    ///     Example: .ForMember(dest => dest.PropertyName, opt => opt.Ignore())
    /// </summary>
    /// <param name="invocation">The original CreateMap invocation to chain from.</param>
    /// <param name="propertyName">The destination property name to ignore.</param>
    /// <returns>A new InvocationExpressionSyntax with ForMember(Ignore) chained.</returns>
    public static InvocationExpressionSyntax CreateForMemberWithIgnore(
        InvocationExpressionSyntax invocation,
        string propertyName)
    {
        return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    invocation,
                    SyntaxFactory.IdentifierName("ForMember")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(new[]
                    {
                        // First argument: dest => dest.PropertyName
                        SyntaxFactory.Argument(
                            CreatePropertySelectorLambda("dest", propertyName)),
                        // Second argument: opt => opt.Ignore()
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("opt")),
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName("opt"),
                                        SyntaxFactory.IdentifierName("Ignore")))))
                    })));
    }

    /// <summary>
    ///     Creates a ForSourceMember call with DoNotValidate configuration.
    ///     Example: .ForSourceMember(src => src.PropertyName, opt => opt.DoNotValidate())
    /// </summary>
    /// <param name="invocation">The original CreateMap invocation to chain from.</param>
    /// <param name="propertyName">The source property name.</param>
    /// <returns>A new InvocationExpressionSyntax with ForSourceMember chained.</returns>
    public static InvocationExpressionSyntax CreateForSourceMemberWithDoNotValidate(
        InvocationExpressionSyntax invocation,
        string propertyName)
    {
        return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    invocation,
                    SyntaxFactory.IdentifierName("ForSourceMember")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(new[]
                    {
                        // First argument: src => src.PropertyName
                        SyntaxFactory.Argument(
                            CreatePropertySelectorLambda("src", propertyName)),
                        // Second argument: opt => opt.DoNotValidate()
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("opt")),
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName("opt"),
                                        SyntaxFactory.IdentifierName("DoNotValidate")))))
                    })));
    }

    /// <summary>
    ///     Creates a simple lambda expression for property selection.
    ///     Example: param => param.PropertyName
    /// </summary>
    /// <param name="parameterName">The lambda parameter name (e.g., "dest" or "src").</param>
    /// <param name="propertyName">The property to access.</param>
    /// <returns>A SimpleLambdaExpressionSyntax.</returns>
    private static SimpleLambdaExpressionSyntax CreatePropertySelectorLambda(
        string parameterName,
        string propertyName)
    {
        return SyntaxFactory.SimpleLambdaExpression(
            SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName)),
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(parameterName),
                SyntaxFactory.IdentifierName(propertyName)));
    }

    /// <summary>
    ///     Creates a lambda for opt.MapFrom configuration.
    ///     Example: opt => opt.MapFrom(src => expression)
    /// </summary>
    /// <param name="mapFromExpression">The expression to use in the MapFrom lambda.</param>
    /// <returns>A SimpleLambdaExpressionSyntax.</returns>
    private static SimpleLambdaExpressionSyntax CreateMapFromLambda(string mapFromExpression)
    {
        return SyntaxFactory.SimpleLambdaExpression(
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
                                    SyntaxFactory.ParseExpression(mapFromExpression)))))));
    }

    /// <summary>
    ///     Adds properties to a type declaration (class or record). Handles positional record parameters,
    ///     init accessors, and standard class properties. Shared by AM004 and AM006 code fix providers.
    /// </summary>
    /// <param name="document">The document containing the mapping invocation.</param>
    /// <param name="targetType">The type symbol to add properties to.</param>
    /// <param name="properties">The properties to add, as (Name, Type) tuples.</param>
    /// <returns>The updated solution with the new properties added.</returns>
    public static async Task<Solution> AddPropertiesToTypeAsync(
        Document document,
        ITypeSymbol targetType,
        IEnumerable<(string Name, string Type)> properties)
    {
        if (targetType.Locations.All(l => !l.IsInSource))
        {
            return document.Project.Solution;
        }

        var syntaxReference = targetType.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxReference == null)
        {
            return document.Project.Solution;
        }

        var targetSyntaxRoot = await syntaxReference.SyntaxTree.GetRootAsync();
        var targetDecl = targetSyntaxRoot.FindNode(syntaxReference.Span);

        if (targetDecl == null)
        {
            return document.Project.Solution;
        }

        var editor = new SyntaxEditor(targetSyntaxRoot, document.Project.Solution.Workspace.Services);
        var propertyList = properties.ToList();

        editor.ReplaceNode(targetDecl, (originalNode, generator) =>
        {
            if (originalNode is RecordDeclarationSyntax recordDecl && recordDecl.ParameterList != null)
            {
                var newParams = propertyList.Select(prop =>
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier(prop.Name))
                            .WithType(SyntaxFactory.ParseTypeName(prop.Type)
                                .WithTrailingTrivia(SyntaxFactory.Space)))
                    .ToArray();

                return recordDecl.WithParameterList(recordDecl.ParameterList.AddParameters(newParams));
            }

            var typeDecl = (TypeDeclarationSyntax)originalNode;
            bool useInitAccessor = typeDecl.Members.OfType<PropertyDeclarationSyntax>()
                .SelectMany(p => p.AccessorList?.Accessors ?? SyntaxFactory.List<AccessorDeclarationSyntax>())
                .Any(a => a.IsKind(SyntaxKind.InitAccessorDeclaration));

            var setterKind = useInitAccessor
                ? SyntaxKind.InitAccessorDeclaration
                : SyntaxKind.SetAccessorDeclaration;

            var newMembers = new List<MemberDeclarationSyntax>();

            foreach (var (name, type) in propertyList)
            {
                var newProperty = SyntaxFactory.PropertyDeclaration(
                        SyntaxFactory.ParseTypeName(type),
                        SyntaxFactory.Identifier(name))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(new[]
                    {
                        SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                        SyntaxFactory.AccessorDeclaration(setterKind)
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                    })));

                newMembers.Add(newProperty);
            }

            return typeDecl.AddMembers(newMembers.ToArray());
        });

        var newTargetRoot = editor.GetChangedRoot();
        var targetDocument = document.Project.Solution.GetDocument(targetSyntaxRoot.SyntaxTree);

        if (targetDocument == null)
        {
            return document.Project.Solution;
        }

        return document.Project.Solution.WithDocumentSyntaxRoot(targetDocument.Id, newTargetRoot);
    }
}

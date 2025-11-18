using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
}

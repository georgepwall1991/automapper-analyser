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
                CreateMemberName(propertyName)));
    }

    /// <summary>
    ///     Builds the simple name for a member access, escaping reserved keywords. Parsing the verbatim
    ///     form (e.g. <c>@class</c>) is used so the resulting token has the correct value text (<c>class</c>)
    ///     in addition to rendering with the leading <c>@</c>; constructing <c>IdentifierName("@class")</c>
    ///     directly would leave the <c>@</c> in the value text and produce a malformed token.
    /// </summary>
    private static SimpleNameSyntax CreateMemberName(string propertyName)
    {
        return SyntaxFacts.GetKeywordKind(propertyName) != SyntaxKind.None
            ? (SimpleNameSyntax)SyntaxFactory.ParseName("@" + propertyName)
            : SyntaxFactory.IdentifierName(propertyName);
    }

    /// <summary>
    ///     Escapes a member name with a leading <c>@</c> when it collides with a C# reserved keyword, so
    ///     it can be safely emitted into generated member-access syntax (e.g. <c>class</c> -> <c>@class</c>).
    ///     Non-keyword names are returned unchanged. Intended for names interpolated into expression strings
    ///     that are later parsed (e.g. <c>src.@class</c>); for directly-constructed name syntax use
    ///     <see cref="CreateMemberName"/>.
    /// </summary>
    /// <param name="name">The member name to escape.</param>
    /// <returns>The name, prefixed with <c>@</c> when it is a reserved keyword.</returns>
    public static string EscapeIdentifier(string name)
    {
        return SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
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

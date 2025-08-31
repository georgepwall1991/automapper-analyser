using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers;

/// <summary>
///     Analyzer for detecting missing destination properties that could lead to data loss in AutoMapper configurations
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM004_MissingDestinationPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     AM004: Missing destination property - potential data loss
    /// </summary>
    public static readonly DiagnosticDescriptor MissingDestinationPropertyRule = new(
        "AM004",
        "Source property has no corresponding destination property",
        "Source property '{0}' will not be mapped - potential data loss",
        "AutoMapper.MissingProperty",
        DiagnosticSeverity.Warning,
        true,
        "Source property exists but no corresponding destination property found, which may result in data loss during mapping.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [MissingDestinationPropertyRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCreateMapInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeCreateMapInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;

        // Check if this is a CreateMap<TSource, TDestination>() call
        if (!AutoMapperAnalysisHelpers.IsCreateMapInvocation(invocationExpr, context.SemanticModel))
        {
            return;
        }

        (ITypeSymbol? sourceType, ITypeSymbol? destinationType) typeArguments =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocationExpr, context.SemanticModel);
        if (typeArguments.sourceType == null || typeArguments.destinationType == null)
        {
            return;
        }

        // Analyze missing properties between source and destination types
        AnalyzeMissingDestinationProperties(
            context,
            invocationExpr,
            typeArguments.sourceType,
            typeArguments.destinationType
        );
    }


    private static void AnalyzeMissingDestinationProperties(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType);
        var destinationProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destinationType);

        // Check each source property to see if it has a corresponding destination property
        foreach (IPropertySymbol sourceProperty in sourceProperties)
        {
            // Check if destination has a property with the same name (case-insensitive, like AutoMapper)
            IPropertySymbol? destinationProperty = destinationProperties
                .FirstOrDefault(p => string.Equals(p.Name, sourceProperty.Name, StringComparison.OrdinalIgnoreCase));

            if (destinationProperty != null)
            {
                continue; // Property exists in destination, no data loss
            }

            // Check if this source property is handled by custom mapping configuration
            if (IsSourcePropertyHandledByCustomMapping(invocation, sourceProperty.Name))
            {
                continue; // Property is handled by custom mapping, no data loss
            }

            // Check if this source property is explicitly ignored
            if (IsSourcePropertyExplicitlyIgnored(invocation, sourceProperty.Name))
            {
                continue; // Property is explicitly ignored, no diagnostic needed
            }

            // Report diagnostic for missing destination property
            var properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("PropertyName", sourceProperty.Name);
            properties.Add("PropertyType", sourceProperty.Type.ToDisplayString());
            properties.Add("SourceTypeName", GetTypeName(sourceType));
            properties.Add("DestinationTypeName", GetTypeName(destinationType));

            var diagnostic = Diagnostic.Create(
                MissingDestinationPropertyRule,
                invocation.GetLocation(),
                properties.ToImmutable(),
                sourceProperty.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// Gets the type name from an ITypeSymbol.
    /// </summary>
    /// <param name="type">The type symbol.</param>
    /// <returns>The type name.</returns>
    private static string GetTypeName(ITypeSymbol type)
    {
        return type.Name;
    }

    private static bool IsSourcePropertyHandledByCustomMapping(InvocationExpressionSyntax createMapInvocation,
        string sourcePropertyName)
    {
        // Look for chained ForMember calls that might use this source property
        SyntaxNode? parent = createMapInvocation.Parent;

        while (parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if (memberAccess.Name.Identifier.ValueText == "ForMember")
            {
                // Check if this ForMember call references the source property
                if (ForMemberReferencesSourceProperty(chainedInvocation, sourcePropertyName))
                {
                    return true;
                }
            }

            // Move up the chain
            parent = chainedInvocation.Parent;
        }

        return false;
    }

    private static bool ForMemberReferencesSourceProperty(InvocationExpressionSyntax forMemberInvocation,
        string sourcePropertyName)
    {
        // Inspect ForMember's configuration lambda (second argument) and any nested lambdas
        var args = forMemberInvocation.ArgumentList?.Arguments;
        if (args == null || args.Value.Count < 2)
            return false;

        var configExpr = args.Value[1].Expression;

        // Search for any lambda within the configuration and verify the property access is on the lambda parameter
        foreach (var lambda in configExpr.DescendantNodesAndSelf().OfType<SimpleLambdaExpressionSyntax>())
        {
            var paramName = lambda.Parameter.Identifier.ValueText;
            if (LambdaContainsPropertyOnParameter(lambda, sourcePropertyName, paramName))
                return true;
        }

        // Fallback: MapFrom passed directly as lambda in second argument is common
        if (configExpr is SimpleLambdaExpressionSyntax directLambda)
        {
            var paramName = directLambda.Parameter.Identifier.ValueText;
            if (LambdaContainsPropertyOnParameter(directLambda, sourcePropertyName, paramName))
                return true;
        }

        return false;
    }

    private static bool LambdaContainsPropertyOnParameter(SimpleLambdaExpressionSyntax lambda, string propertyName, string paramName)
    {
        return lambda.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(ma => ma.Name.Identifier.ValueText == propertyName && IsAccessOnParameter(ma.Expression, paramName));
    }

    private static bool IsAccessOnParameter(ExpressionSyntax expr, string paramName)
    {
        // Unwrap parentheses
        while (expr is ParenthesizedExpressionSyntax p)
            expr = p.Expression;

        // Handle conditional access: src?.Prop
        if (expr is ConditionalAccessExpressionSyntax cond)
            return IsAccessOnParameter(cond.Expression, paramName);

        // Handle element access: src.Items[0].Prop
        if (expr is ElementAccessExpressionSyntax el)
            return IsAccessOnParameter(el.Expression, paramName);

        // Walk left side of member access chains: src.Foo.Bar
        if (expr is MemberAccessExpressionSyntax ma)
            return IsAccessOnParameter(ma.Expression, paramName);

        // Base case: identifier name equals the lambda parameter
        if (expr is IdentifierNameSyntax id)
            return id.Identifier.ValueText == paramName;

        return false;
    }

    private static bool IsSourcePropertyExplicitlyIgnored(InvocationExpressionSyntax createMapInvocation,
        string sourcePropertyName)
    {
        // Look for ForSourceMember calls with DoNotValidate
        SyntaxNode? parent = createMapInvocation.Parent;

        while (parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
        {
            if (memberAccess.Name.Identifier.ValueText == "ForSourceMember")
            {
                // Check if this ForSourceMember call is for the property we're analyzing
                if (IsForSourceMemberOfProperty(chainedInvocation, sourcePropertyName))
                {
                    // Check if it has DoNotValidate
                    if (HasDoNotValidateCall(chainedInvocation))
                    {
                        return true;
                    }
                }
            }

            // Move up the chain
            parent = chainedInvocation.Parent;
        }

        return false;
    }

    private static bool IsForSourceMemberOfProperty(InvocationExpressionSyntax forSourceMemberInvocation,
        string propertyName)
    {
        // Check the first argument of ForSourceMember to see if it references the property
        if (forSourceMemberInvocation.ArgumentList.Arguments.Count > 0)
        {
            ExpressionSyntax firstArg = forSourceMemberInvocation.ArgumentList.Arguments[0].Expression;
            if (firstArg is SimpleLambdaExpressionSyntax lambda)
            {
                var paramName = lambda.Parameter.Identifier.ValueText;
                return LambdaContainsPropertyOnParameter(lambda, propertyName, paramName);
            }
            // Fallback: search any nested lambdas
            foreach (var innerLambda in firstArg.DescendantNodes().OfType<SimpleLambdaExpressionSyntax>())
            {
                var paramName = innerLambda.Parameter.Identifier.ValueText;
                if (LambdaContainsPropertyOnParameter(innerLambda, propertyName, paramName))
                    return true;
            }
        }

        return false;
    }

    private static bool HasDoNotValidateCall(InvocationExpressionSyntax forSourceMemberInvocation)
    {
        // Look for DoNotValidate call in the second argument (options)
        if (forSourceMemberInvocation.ArgumentList.Arguments.Count > 1)
        {
            ExpressionSyntax secondArg = forSourceMemberInvocation.ArgumentList.Arguments[1].Expression;

            // Look for lambda expressions that contain DoNotValidate calls
            return secondArg.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation =>
                    invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name.Identifier.ValueText == "DoNotValidate");
        }

        return false;
    }
}

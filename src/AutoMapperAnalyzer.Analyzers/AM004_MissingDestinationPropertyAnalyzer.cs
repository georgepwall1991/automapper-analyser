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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [MissingDestinationPropertyRule];

    /// <inheritdoc/>
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
        // Look for lambda expressions in ForMember arguments that reference the source property
        foreach (ArgumentSyntax arg in forMemberInvocation.ArgumentList.Arguments)
        {
            if (ContainsPropertyReference(arg.Expression, sourcePropertyName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsPropertyReference(SyntaxNode node, string propertyName)
    {
        // Recursively search for property access expressions that match the property name
        return node.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
            .Any(memberAccess => memberAccess.Name.Identifier.ValueText == propertyName);
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
            return ContainsPropertyReference(firstArg, propertyName);
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

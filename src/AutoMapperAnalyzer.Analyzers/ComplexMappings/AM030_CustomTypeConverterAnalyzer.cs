using System.Collections.Concurrent;
using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.ComplexMappings;

/// <summary>
///     Analyzer for AM030: Custom Type Converter quality and usage issues in AutoMapper configurations.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM030_CustomTypeConverterAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     Diagnostic rule for invalid type converter implementations.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidConverterImplementationRule = new(
        "AM030",
        "Invalid type converter implementation",
        "Type converter '{0}' does not properly implement ITypeConverter<{1}, {2}> or has invalid Convert method signature",
        "AutoMapper.Converters",
        DiagnosticSeverity.Error,
        true,
        "Custom type converters must implement ITypeConverter<TSource, TDestination> with proper Convert method signature.");

    /// <summary>
    ///     Legacy descriptor kept for source compatibility; AM001/AM020/AM021 own missing-converter mapping diagnostics.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingConvertUsingConfigurationRule = new(
        "AM030",
        "Missing ConvertUsing configuration for type converter",
        "Property '{0}' has incompatible types but no ConvertUsing configuration. Consider using ConvertUsing<{1}> or ConvertUsing(converter => ...).",
        "AutoMapper.Converters",
        DiagnosticSeverity.Warning,
        true,
        "Type mismatches should use ConvertUsing configuration for proper conversion.");

    /// <summary>
    ///     Diagnostic rule for converter null handling issues.
    /// </summary>
    public static readonly DiagnosticDescriptor ConverterNullHandlingIssueRule = new(
        "AM030",
        "Type converter may not handle null values properly",
        "Converter for '{0}' may not handle null values. Source type '{1}' is nullable but converter doesn't check for null.",
        "AutoMapper.Converters",
        DiagnosticSeverity.Warning,
        true,
        "Type converters should handle null input values when source type is nullable.");

    /// <summary>
    ///     Diagnostic rule for unused type converters.
    /// </summary>
    public static readonly DiagnosticDescriptor UnusedTypeConverterRule = new(
        "AM030",
        "Type converter is defined but not used in mapping configuration",
        "Type converter '{0}' is defined but not used in any CreateMap configuration",
        "AutoMapper.Converters",
        DiagnosticSeverity.Info,
        true,
        "Unused type converters can be removed or should be configured in mapping.",
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            InvalidConverterImplementationRule,
            ConverterNullHandlingIssueRule,
            UnusedTypeConverterRule
        );

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var declaredConverters = new ConcurrentDictionary<string, (INamedTypeSymbol Symbol, Location Location)>(
                StringComparer.Ordinal);
            var usedConverters = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeClassDeclaration(nodeContext, declaredConverters),
                SyntaxKind.ClassDeclaration);

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeConvertUsingInvocation(nodeContext, usedConverters),
                SyntaxKind.InvocationExpression);

            compilationContext.RegisterCompilationEndAction(endContext =>
                ReportUnusedConverters(endContext, declaredConverters, usedConverters));
        });
    }

    private static void AnalyzeClassDeclaration(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<string, (INamedTypeSymbol Symbol, Location Location)> declaredConverters)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        INamedTypeSymbol? classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);

        if (classSymbol == null)
        {
            return;
        }

        INamedTypeSymbol? typeConverterInterface = GetTypeConverterInterface(classSymbol);
        if (typeConverterInterface == null)
        {
            return;
        }

        if (!classSymbol.IsAbstract)
        {
            declaredConverters.TryAdd(
                GetConverterKey(classSymbol),
                (classSymbol, classDeclaration.Identifier.GetLocation()));
        }

        AnalyzeTypeConverterImplementation(context, classDeclaration, classSymbol, typeConverterInterface);
    }

    private static void AnalyzeConvertUsingInvocation(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<string, byte> usedConverters)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        if (!MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(invocation, context.SemanticModel, "ConvertUsing"))
        {
            return;
        }

        foreach (INamedTypeSymbol converterSymbol in GetConverterSymbolsFromInvocation(invocation, context.SemanticModel))
        {
            usedConverters.TryAdd(GetConverterKey(converterSymbol), 0);
        }
    }

    private static void ReportUnusedConverters(
        CompilationAnalysisContext context,
        ConcurrentDictionary<string, (INamedTypeSymbol Symbol, Location Location)> declaredConverters,
        ConcurrentDictionary<string, byte> usedConverters)
    {
        foreach (KeyValuePair<string, (INamedTypeSymbol Symbol, Location Location)> entry in declaredConverters)
        {
            string converterKey = entry.Key;
            if (usedConverters.ContainsKey(converterKey))
            {
                continue;
            }

            (INamedTypeSymbol Symbol, Location Location) converterInfo = entry.Value;
            var diagnostic = Diagnostic.Create(
                UnusedTypeConverterRule,
                converterInfo.Location,
                converterInfo.Symbol.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetConverterSymbolsFromInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);

        foreach (IMethodSymbol methodSymbol in GetCandidateMethodSymbols(symbolInfo))
        {
            foreach (ITypeSymbol typeArgument in methodSymbol.TypeArguments)
            {
                if (typeArgument is INamedTypeSymbol namedType && ImplementsTypeConverter(namedType))
                {
                    yield return namedType;
                }
            }
        }

        foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
        {
            ITypeSymbol? argumentType = semanticModel.GetTypeInfo(argument.Expression).Type ??
                                        semanticModel.GetTypeInfo(argument.Expression).ConvertedType;

            if (argumentType is INamedTypeSymbol namedArgumentType && ImplementsTypeConverter(namedArgumentType))
            {
                yield return namedArgumentType;
            }
        }
    }

    private static IEnumerable<IMethodSymbol> GetCandidateMethodSymbols(SymbolInfo symbolInfo)
    {
        if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
        {
            yield return methodSymbol;
        }

        foreach (ISymbol candidateSymbol in symbolInfo.CandidateSymbols)
        {
            if (candidateSymbol is IMethodSymbol candidateMethod)
            {
                yield return candidateMethod;
            }
        }
    }

    private static bool ImplementsTypeConverter(INamedTypeSymbol typeSymbol)
    {
        return GetTypeConverterInterface(typeSymbol) != null;
    }

    private static string GetConverterKey(INamedTypeSymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static void AnalyzeTypeConverterImplementation(
        SyntaxNodeAnalysisContext context,
        ClassDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        INamedTypeSymbol typeConverterInterface)
    {
        // Check if Convert method is properly implemented
        IMethodSymbol? convertMethod = GetConvertMethod(classSymbol, typeConverterInterface);
        if (convertMethod == null)
        {
            var diagnostic = Diagnostic.Create(
                InvalidConverterImplementationRule,
                classDeclaration.Identifier.GetLocation(),
                classSymbol.Name,
                AutoMapperAnalysisHelpers.GetTypeName(typeConverterInterface.TypeArguments[0]),
                AutoMapperAnalysisHelpers.GetTypeName(typeConverterInterface.TypeArguments[1]));

            context.ReportDiagnostic(diagnostic);
            return;
        }

        // Check null handling
        AnalyzeConverterNullHandling(context, classDeclaration, classSymbol, typeConverterInterface, convertMethod);
    }

    private static void AnalyzeConverterNullHandling(
        SyntaxNodeAnalysisContext context,
        ClassDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        INamedTypeSymbol typeConverterInterface,
        IMethodSymbol convertMethod)
    {
        ITypeSymbol? sourceTypeParameter = typeConverterInterface.TypeArguments.FirstOrDefault();
        if (sourceTypeParameter == null || !IsNullableType(sourceTypeParameter))
        {
            return;
        }

        string sourceParameterName = convertMethod.Parameters.FirstOrDefault()?.Name ?? "source";

        MethodDeclarationSyntax? convertMethodDeclaration = classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m =>
                SymbolEqualityComparer.Default.Equals(context.SemanticModel.GetDeclaredSymbol(m), convertMethod));

        if (convertMethodDeclaration != null && !ContainsNullCheck(convertMethodDeclaration, sourceParameterName))
        {
            var diagnostic = Diagnostic.Create(
                ConverterNullHandlingIssueRule,
                convertMethodDeclaration.Identifier.GetLocation(),
                classSymbol.Name,
                sourceTypeParameter.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static INamedTypeSymbol? GetTypeConverterInterface(INamedTypeSymbol classSymbol)
    {
        return classSymbol.AllInterfaces
            .FirstOrDefault(i =>
                i.Name == "ITypeConverter" &&
                i.ContainingNamespace?.ToDisplayString() == "AutoMapper" &&
                i.TypeArguments.Length == 2);
    }

    private static IMethodSymbol? GetConvertMethod(INamedTypeSymbol classSymbol, INamedTypeSymbol typeConverterInterface)
    {
        IMethodSymbol? interfaceConvertMethod = typeConverterInterface
            .GetMembers("Convert")
            .OfType<IMethodSymbol>()
            .FirstOrDefault();

        if (interfaceConvertMethod == null)
        {
            return null;
        }

        return classSymbol.FindImplementationForInterfaceMember(interfaceConvertMethod) as IMethodSymbol;
    }

    private static bool IsNullableType(ITypeSymbol type)
    {
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }

        return type is INamedTypeSymbol namedType &&
               namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
    }

    private static bool ContainsNullCheck(MethodDeclarationSyntax method, string sourceParameterName)
    {
        IEnumerable<SyntaxNode> nodes = method.Body?.DescendantNodesAndSelf() ?? Enumerable.Empty<SyntaxNode>();
        if (method.ExpressionBody != null)
        {
            nodes = nodes.Concat(method.ExpressionBody.DescendantNodesAndSelf());
        }

        bool hasBinaryNullCheck = nodes
            .OfType<BinaryExpressionSyntax>()
            .Any(binary =>
                (binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression)) &&
                ((IsNullLiteral(binary.Left) && IsSourceReference(binary.Right, sourceParameterName)) ||
                 (IsSourceReference(binary.Left, sourceParameterName) && IsNullLiteral(binary.Right))));

        if (hasBinaryNullCheck)
        {
            return true;
        }

        bool hasPatternNullCheck = nodes
            .OfType<IsPatternExpressionSyntax>()
            .Any(pattern => IsSourceReference(pattern.Expression, sourceParameterName) && PatternMatchesNull(pattern.Pattern));

        if (hasPatternNullCheck)
        {
            return true;
        }

        bool hasStringHelperNullCheck = nodes
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.ValueText is "IsNullOrEmpty" or "IsNullOrWhiteSpace" &&
                IsStringTypeAccess(memberAccess.Expression) &&
                invocation.ArgumentList.Arguments.Any(arg => IsSourceReference(arg.Expression, sourceParameterName)));

        if (hasStringHelperNullCheck)
        {
            return true;
        }

        bool hasNullCoalescing = nodes
            .OfType<BinaryExpressionSyntax>()
            .Any(binary => binary.IsKind(SyntaxKind.CoalesceExpression) &&
                           IsSourceReference(binary.Left, sourceParameterName));

        if (hasNullCoalescing)
        {
            return true;
        }

        return nodes
            .OfType<ConditionalAccessExpressionSyntax>()
            .Any(conditional => IsSourceReference(conditional.Expression, sourceParameterName));
    }

    private static bool PatternMatchesNull(PatternSyntax pattern)
    {
        return pattern switch
        {
            ConstantPatternSyntax { Expression: var expression } => IsNullLiteral(expression),
            UnaryPatternSyntax
            {
                Pattern: ConstantPatternSyntax { Expression: var expression }
            } => IsNullLiteral(expression),
            _ => false
        };
    }

    private static bool IsNullLiteral(ExpressionSyntax expression)
    {
        return expression is LiteralExpressionSyntax literal &&
               literal.IsKind(SyntaxKind.NullLiteralExpression);
    }

    private static bool IsSourceReference(ExpressionSyntax expression, string sourceParameterName)
    {
        ExpressionSyntax unwrappedExpression = UnwrapExpression(expression);
        return unwrappedExpression is IdentifierNameSyntax identifier &&
               string.Equals(identifier.Identifier.ValueText, sourceParameterName, StringComparison.Ordinal);
    }

    private static bool IsStringTypeAccess(ExpressionSyntax expression)
    {
        return expression switch
        {
            PredefinedTypeSyntax predefinedType => predefinedType.Keyword.IsKind(SyntaxKind.StringKeyword),
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText is "String" or "string",
            _ => false
        };
    }

    private static ExpressionSyntax UnwrapExpression(ExpressionSyntax expression)
    {
        ExpressionSyntax currentExpression = expression;

        while (true)
        {
            switch (currentExpression)
            {
                case ParenthesizedExpressionSyntax parenthesizedExpression:
                    currentExpression = parenthesizedExpression.Expression;
                    continue;
                case CastExpressionSyntax castExpression:
                    currentExpression = castExpression.Expression;
                    continue;
                default:
                    return currentExpression;
            }
        }
    }
}

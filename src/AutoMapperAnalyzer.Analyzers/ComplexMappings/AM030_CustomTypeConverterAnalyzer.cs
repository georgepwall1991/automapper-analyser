using System.Collections.Concurrent;
using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.ComplexMappings;

/// <summary>
///     Analyzer for custom type converter quality and usage issues in AutoMapper configurations.
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
    ///     Legacy descriptor retained for binary compatibility. AM001, AM020, and AM021 own missing-converter
    ///     mapping diagnostics; this descriptor is never registered in <see cref="SupportedDiagnostics" /> and
    ///     never reports. Do not reuse — the drift guard
    ///     <c>RuleCatalogTests.Analyzers_ShouldRegisterEveryDeclaredDiagnosticDescriptor</c> requires every
    ///     additional orphan to be marked <see cref="ObsoleteAttribute" /> so legacy intent stays explicit.
    /// </summary>
    [Obsolete(
        "AM030 no longer owns missing ConvertUsing configuration diagnostics. AM001, AM020, and AM021 handle "
        + "property-level conversion absence. This descriptor is retained only for binary compatibility and "
        + "never appears in SupportedDiagnostics.",
        error: false)]
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
        "AM032",
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
        "AM033",
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
            var usedConverterInterfaces = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeClassDeclaration(nodeContext, declaredConverters),
                SyntaxKind.ClassDeclaration);

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeConvertUsingInvocation(nodeContext, usedConverters, usedConverterInterfaces),
                SyntaxKind.InvocationExpression);

            compilationContext.RegisterCompilationEndAction(endContext =>
                ReportUnusedConverters(endContext, declaredConverters, usedConverters, usedConverterInterfaces));
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
        ConcurrentDictionary<string, byte> usedConverters,
        ConcurrentDictionary<string, byte> usedConverterInterfaces)
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

        foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
        {
            TypeInfo argumentTypeInfo = context.SemanticModel.GetTypeInfo(argument.Expression);
            ITypeSymbol? argumentType = argumentTypeInfo.Type ?? argumentTypeInfo.ConvertedType;
            if (argumentType is INamedTypeSymbol namedArgumentType &&
                IsTypeConverterInterface(namedArgumentType) &&
                TryGetTypeConverterInterfaceKey(namedArgumentType, out string interfaceKey))
            {
                usedConverterInterfaces.TryAdd(interfaceKey, 0);
            }
        }
    }

    private static void ReportUnusedConverters(
        CompilationAnalysisContext context,
        ConcurrentDictionary<string, (INamedTypeSymbol Symbol, Location Location)> declaredConverters,
        ConcurrentDictionary<string, byte> usedConverters,
        ConcurrentDictionary<string, byte> usedConverterInterfaces)
    {
        foreach (KeyValuePair<string, (INamedTypeSymbol Symbol, Location Location)> entry in declaredConverters)
        {
            string converterKey = entry.Key;
            if (usedConverters.ContainsKey(converterKey))
            {
                continue;
            }

            (INamedTypeSymbol Symbol, Location Location) converterInfo = entry.Value;
            if (ImplementsAnyUsedConverterInterface(converterInfo.Symbol, usedConverterInterfaces))
            {
                continue;
            }

            var diagnostic = Diagnostic.Create(
                UnusedTypeConverterRule,
                converterInfo.Location,
                converterInfo.Symbol.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool ImplementsAnyUsedConverterInterface(
        INamedTypeSymbol converter,
        ConcurrentDictionary<string, byte> usedConverterInterfaces)
    {
        if (usedConverterInterfaces.IsEmpty)
        {
            return false;
        }

        foreach (INamedTypeSymbol implementedInterface in converter.AllInterfaces)
        {
            if (!IsTypeConverterInterface(implementedInterface))
            {
                continue;
            }

            if (TryGetTypeConverterInterfaceKey(implementedInterface, out string interfaceKey) &&
                usedConverterInterfaces.ContainsKey(interfaceKey))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetTypeConverterInterfaceKey(INamedTypeSymbol interfaceSymbol, out string key)
    {
        key = string.Empty;
        if (!IsTypeConverterInterface(interfaceSymbol))
        {
            return false;
        }

        ITypeSymbol source = interfaceSymbol.TypeArguments[0];
        ITypeSymbol destination = interfaceSymbol.TypeArguments[1];
        key = source.ToDisplayString() + "->" + destination.ToDisplayString();
        return true;
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
            foreach (INamedTypeSymbol converterSymbol in GetConverterSymbolsFromExpression(argument.Expression, semanticModel))
            {
                yield return converterSymbol;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetConverterSymbolsFromExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        return GetConverterSymbolsFromExpression(expression, semanticModel, new HashSet<ISymbol>(SymbolEqualityComparer.Default));
    }

    private static IEnumerable<INamedTypeSymbol> GetConverterSymbolsFromExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        HashSet<ISymbol> visitedSymbols)
    {
        expression = UnwrapConverterReferenceExpression(expression);

        if (expression is TypeOfExpressionSyntax typeOfExpression &&
            semanticModel.GetTypeInfo(typeOfExpression.Type).Type is INamedTypeSymbol typeOfArgument &&
            ImplementsTypeConverter(typeOfArgument))
        {
            yield return typeOfArgument;
            yield break;
        }

        if (expression is ObjectCreationExpressionSyntax objectCreation &&
            semanticModel.GetTypeInfo(objectCreation.Type).Type is INamedTypeSymbol objectCreationType &&
            ImplementsTypeConverter(objectCreationType))
        {
            yield return objectCreationType;
            yield break;
        }

        TypeInfo typeInfo = semanticModel.GetTypeInfo(expression);
        ITypeSymbol? argumentType = typeInfo.Type ?? typeInfo.ConvertedType;

        if (argumentType is INamedTypeSymbol namedArgumentType &&
            ImplementsTypeConverter(namedArgumentType) &&
            !IsTypeConverterInterface(namedArgumentType))
        {
            yield return namedArgumentType;
            yield break;
        }

        foreach (ExpressionSyntax initializer in GetReferencedInitializers(expression, semanticModel, visitedSymbols))
        {
            foreach (INamedTypeSymbol converterSymbol in GetConverterSymbolsFromExpression(
                         initializer,
                         semanticModel,
                         visitedSymbols))
            {
                yield return converterSymbol;
            }
        }
    }

    private static ExpressionSyntax UnwrapConverterReferenceExpression(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    continue;
                case CastExpressionSyntax cast:
                    expression = cast.Expression;
                    continue;
                default:
                    return expression;
            }
        }
    }

    private static IEnumerable<ExpressionSyntax> GetReferencedInitializers(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        HashSet<ISymbol> visitedSymbols)
    {
        if (semanticModel.GetSymbolInfo(expression).Symbol is not { } symbol ||
            !visitedSymbols.Add(symbol))
        {
            yield break;
        }

        foreach (SyntaxReference declarationReference in symbol.DeclaringSyntaxReferences)
        {
            SyntaxNode declaration = declarationReference.GetSyntax();

            if (declaration is VariableDeclaratorSyntax { Initializer.Value: { } variableInitializer })
            {
                yield return variableInitializer;
            }
            else if (declaration is PropertyDeclarationSyntax { Initializer.Value: { } propertyInitializer })
            {
                yield return propertyInitializer;
            }
            else if (declaration is PropertyDeclarationSyntax { ExpressionBody.Expression: { } propertyExpression })
            {
                yield return propertyExpression;
            }
            else if (declaration is PropertyDeclarationSyntax { AccessorList: { } accessorList })
            {
                foreach (AccessorDeclarationSyntax accessor in accessorList.Accessors)
                {
                    if (accessor.IsKind(SyntaxKind.GetAccessorDeclaration) &&
                        accessor.ExpressionBody?.Expression is { } getterExpression)
                    {
                        yield return getterExpression;
                    }
                }
            }
            else if (symbol is IMethodSymbol { Parameters.Length: 0 } &&
                     declaration is MethodDeclarationSyntax { ExpressionBody.Expression: { } methodExpression })
            {
                yield return methodExpression;
            }
            else if (symbol is IMethodSymbol { Parameters.Length: 0 } &&
                     declaration is MethodDeclarationSyntax { Body: { } methodBody } &&
                     methodBody.Statements.Count == 1 &&
                     methodBody.Statements[0] is ReturnStatementSyntax { Expression: { } returnExpression })
            {
                yield return returnExpression;
            }
            else if (symbol is IMethodSymbol { Parameters.Length: 0 } &&
                     declaration is LocalFunctionStatementSyntax { ExpressionBody.Expression: { } localFunctionExpression })
            {
                yield return localFunctionExpression;
            }
            else if (symbol is IMethodSymbol { Parameters.Length: 0 } &&
                     declaration is LocalFunctionStatementSyntax { Body: { } localFunctionBody } &&
                     localFunctionBody.Statements.Count == 1 &&
                     localFunctionBody.Statements[0] is ReturnStatementSyntax { Expression: { } localFunctionReturnExpression })
            {
                yield return localFunctionReturnExpression;
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
        return IsTypeConverterInterface(typeSymbol) || GetTypeConverterInterface(typeSymbol) != null;
    }

    private static bool IsTypeConverterInterface(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.Name == "ITypeConverter" &&
               typeSymbol.ContainingNamespace?.ToDisplayString() == "AutoMapper" &&
               typeSymbol.TypeArguments.Length == 2;
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

        ITypeSymbol? destinationTypeParameter = typeConverterInterface.TypeArguments.ElementAtOrDefault(1);
        string sourceParameterName = convertMethod.Parameters.FirstOrDefault()?.Name ?? "source";

        MethodDeclarationSyntax? convertMethodDeclaration = classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m =>
                SymbolEqualityComparer.Default.Equals(context.SemanticModel.GetDeclaredSymbol(m), convertMethod));

        if (convertMethodDeclaration != null &&
            !ContainsNullCheck(convertMethodDeclaration, sourceParameterName, destinationTypeParameter))
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

    private static bool ContainsNullCheck(
        MethodDeclarationSyntax method,
        string sourceParameterName,
        ITypeSymbol? destinationType)
    {
        // Pure pass-through of a nullable source into a nullable destination (return source / => source)
        // is intentional null-preserving conversion — do not demand an explicit null guard.
        if (destinationType != null &&
            IsNullableType(destinationType) &&
            IsNullableSourcePassThrough(method, sourceParameterName))
        {
            return true;
        }

        IEnumerable<SyntaxNode> nodes = GetConverterExecutableDescendantNodes(method);

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
            .Any(pattern =>
                IsSourceReference(pattern.Expression, sourceParameterName) &&
                PatternMatchesNull(pattern.Pattern));

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
                invocation.ArgumentList.Arguments.Any(arg =>
                    IsSourceGuardArgument(arg.Expression, method, sourceParameterName)));

        if (hasStringHelperNullCheck)
        {
            return true;
        }

        bool hasArgumentGuardNullCheck = nodes
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.ValueText is "ThrowIfNull" or "ThrowIfNullOrEmpty" or "ThrowIfNullOrWhiteSpace" &&
                IsArgumentExceptionTypeAccess(memberAccess.Expression) &&
                TryGetGuardArgument(invocation.ArgumentList, out ExpressionSyntax guardedExpression) &&
                IsSourceGuardArgument(guardedExpression, method, sourceParameterName));

        if (hasArgumentGuardNullCheck)
        {
            return true;
        }

        bool hasNullCoalescing = nodes
            .OfType<BinaryExpressionSyntax>()
            .Any(binary => binary.IsKind(SyntaxKind.CoalesceExpression) &&
                           !IsInsideControlFlowCondition(binary) &&
                           CoalesceHandlesSourceNull(binary, method, sourceParameterName));

        if (hasNullCoalescing)
        {
            return true;
        }

        bool hasOnlyConditionalAccessSourceUsage =
            ConditionalAccessOnlyUsageHandlesSourceNull(method, sourceParameterName, destinationType);
        if (hasOnlyConditionalAccessSourceUsage)
        {
            return true;
        }

        bool hasConditionalAccessGuardedFlow = nodes
            .OfType<ConditionalAccessExpressionSyntax>()
            .Any(conditional =>
                IsConditionalAccessRootedInSource(conditional, sourceParameterName) &&
                (ConditionalAccessGuardsReturningBranch(conditional, sourceParameterName) ||
                 ConditionalAccessGuardsConditionalReturn(conditional, method, sourceParameterName) ||
                 ConditionalAccessGuardsSwitchReturn(conditional, method, sourceParameterName) ||
                 ConditionalAccessGuardsSwitchStatement(conditional, sourceParameterName)));

        if (hasConditionalAccessGuardedFlow)
        {
            return true;
        }

        bool hasConditionalAccessBooleanLocalGuardedFlow =
            HasConditionalAccessBooleanLocalGuardedFlow(method, sourceParameterName);
        if (hasConditionalAccessBooleanLocalGuardedFlow)
        {
            return true;
        }

        bool hasConditionalAccessLocalGuardedFlow =
            HasConditionalAccessLocalGuardedFlow(method, sourceParameterName, destinationType);
        if (hasConditionalAccessLocalGuardedFlow)
        {
            return true;
        }

        return destinationType != null &&
               IsNullableType(destinationType) &&
               nodes
                   .OfType<ConditionalAccessExpressionSyntax>()
                   .Any(conditional =>
                       IsConditionalAccessRootedInSource(conditional, sourceParameterName) &&
                       ConditionalAccessFlowsToReturn(conditional, method, sourceParameterName));
    }

    /// <summary>
    ///     True when the converter body only returns the source parameter (block or expression-bodied).
    ///     Used to suppress AM032 for nullable→nullable pass-through converters.
    /// </summary>
    private static bool IsNullableSourcePassThrough(MethodDeclarationSyntax method, string sourceParameterName)
    {
        if (method.ExpressionBody != null)
        {
            return IsSourceReference(UnwrapParenthesized(method.ExpressionBody.Expression), sourceParameterName);
        }

        if (method.Body == null)
        {
            return false;
        }

        // Single-statement body: return source;
        if (method.Body.Statements.Count == 1 &&
            method.Body.Statements[0] is ReturnStatementSyntax { Expression: { } returnExpression })
        {
            return IsSourceReference(UnwrapParenthesized(returnExpression), sourceParameterName);
        }

        return false;
    }

    private static ExpressionSyntax UnwrapParenthesized(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
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

    private static bool IsSourceConditionalAccess(ExpressionSyntax expression, string sourceParameterName)
    {
        ExpressionSyntax unwrappedExpression = UnwrapExpression(expression);
        return unwrappedExpression is ConditionalAccessExpressionSyntax conditional &&
               IsConditionalAccessRootedInSource(conditional, sourceParameterName);
    }

    private static bool IsConditionalAccessRootedInSource(
        ConditionalAccessExpressionSyntax conditional,
        string sourceParameterName)
    {
        ExpressionSyntax receiver = UnwrapExpression(conditional.Expression);
        while (receiver is ConditionalAccessExpressionSyntax nestedConditional)
        {
            receiver = UnwrapExpression(nestedConditional.Expression);
        }

        return IsSourceReference(receiver, sourceParameterName);
    }

    private static bool IsSourceNullHandlingExpression(ExpressionSyntax expression, string sourceParameterName)
    {
        return IsSourceReference(expression, sourceParameterName) ||
               IsSourceConditionalAccess(expression, sourceParameterName);
    }

    private static bool IsSourceGuardArgument(
        ExpressionSyntax expression,
        MethodDeclarationSyntax method,
        string sourceParameterName)
    {
        return IsSourceNullHandlingExpression(expression, sourceParameterName) ||
               IsSourceConditionalAccessLocalReference(expression, method, sourceParameterName);
    }

    private static bool CoalesceHandlesSourceNull(
        BinaryExpressionSyntax coalesceExpression,
        MethodDeclarationSyntax method,
        string sourceParameterName)
    {
        if (IsSourceNullHandlingExpression(coalesceExpression.Left, sourceParameterName))
        {
            return !ExpressionContainsSourceReference(coalesceExpression.Right, sourceParameterName) &&
                   !IsExplicitNullFallback(coalesceExpression.Right);
        }

        return TryGetSourceConditionalAccessLocalName(
                   coalesceExpression.Left,
                   method,
                   sourceParameterName,
                   out string localName) &&
               !ExpressionContainsAnyIdentifierUsage(coalesceExpression.Right, sourceParameterName, localName) &&
               !IsExplicitNullFallback(coalesceExpression.Right);
    }

    private static bool IsExplicitNullFallback(ExpressionSyntax expression)
    {
        ExpressionSyntax unwrappedExpression = UnwrapExpression(expression);
        return IsNullLiteral(unwrappedExpression) ||
               unwrappedExpression.IsKind(SyntaxKind.DefaultLiteralExpression) ||
               unwrappedExpression is DefaultExpressionSyntax;
    }

    private static bool IsSourceConditionalAccessLocalReference(
        ExpressionSyntax expression,
        MethodDeclarationSyntax method,
        string sourceParameterName)
    {
        return TryGetSourceConditionalAccessLocalName(expression, method, sourceParameterName, out _);
    }

    private static bool IsInsideControlFlowCondition(SyntaxNode node)
    {
        for (SyntaxNode? current = node; current.Parent != null; current = current.Parent)
        {
            if (current.Parent is IfStatementSyntax ifStatement &&
                ifStatement.Condition == current)
            {
                return true;
            }

            if (current.Parent is ConditionalExpressionSyntax conditionalExpression &&
                conditionalExpression.Condition == current)
            {
                return true;
            }

            if (current.Parent is SwitchStatementSyntax switchStatement &&
                switchStatement.Expression == current)
            {
                return true;
            }

            if (current.Parent is SwitchExpressionSyntax switchExpression &&
                switchExpression.GoverningExpression == current)
            {
                return true;
            }

            if (current.Parent is StatementSyntax or ArrowExpressionClauseSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private static bool HasOnlyConditionalAccessSourceUsage(
        MethodDeclarationSyntax method,
        string sourceParameterName)
    {
        bool hasSourceUsage = false;

        foreach (IdentifierNameSyntax identifier in GetConverterExecutableDescendantNodes(method)
                     .OfType<IdentifierNameSyntax>())
        {
            if (!IsIdentifierUsage(identifier, sourceParameterName))
            {
                continue;
            }

            hasSourceUsage = true;
            if (!IsInsideSourceRootedConditionalAccess(identifier, sourceParameterName))
            {
                return false;
            }
        }

        return hasSourceUsage;
    }

    private static bool ConditionalAccessOnlyUsageHandlesSourceNull(
        MethodDeclarationSyntax method,
        string sourceParameterName,
        ITypeSymbol? destinationType)
    {
        if (!HasOnlyConditionalAccessSourceUsage(method, sourceParameterName))
        {
            return false;
        }

        if (SourceConditionalAccessFeedsUnsafeArgument(method, sourceParameterName, destinationType) ||
            SourceConditionalAccessLocalFeedsUnsafeArgument(method, sourceParameterName, destinationType))
        {
            return false;
        }

        return (destinationType != null &&
                IsNullableType(destinationType)) ||
               !SourceConditionalAccessPropagatesNullToReturn(method, sourceParameterName);
    }

    private static bool SourceConditionalAccessFeedsUnsafeArgument(
        MethodDeclarationSyntax method,
        string sourceParameterName,
        ITypeSymbol? destinationType)
    {
        return GetConverterExecutableDescendantNodes(method)
            .OfType<ConditionalAccessExpressionSyntax>()
            .Any(conditional =>
                IsConditionalAccessRootedInSource(conditional, sourceParameterName) &&
                ConditionalAccessFeedsUnsafeArgument(conditional, sourceParameterName, destinationType));
    }

    private static bool SourceConditionalAccessLocalFeedsUnsafeArgument(
        MethodDeclarationSyntax method,
        string sourceParameterName,
        ITypeSymbol? destinationType)
    {
        return GetConverterExecutableDescendantNodes(method)
            .OfType<ArgumentSyntax>()
            .Any(argument =>
                IsPotentialSourceConditionalAccessLocalReference(argument.Expression, method, sourceParameterName) &&
                ArgumentFeedsUnsafeNullableSource(argument, sourceParameterName, destinationType));
    }

    private static bool ConditionalAccessFeedsUnsafeArgument(
        ConditionalAccessExpressionSyntax conditional,
        string sourceParameterName,
        ITypeSymbol? destinationType)
    {
        if (!TryGetContainingArgument(conditional, out ArgumentSyntax? argument))
        {
            return false;
        }

        return ArgumentFeedsUnsafeNullableSource(argument, sourceParameterName, destinationType);
    }

    private static bool ArgumentFeedsUnsafeNullableSource(
        ArgumentSyntax argument,
        string sourceParameterName,
        ITypeSymbol? destinationType)
    {
        if (argument.Parent is not ArgumentListSyntax argumentList)
        {
            return false;
        }

        return argumentList.Parent switch
        {
            InvocationExpressionSyntax invocation =>
                InvocationRequiresNonNullArgument(invocation, argument) &&
                !NullTolerantInvocationHandlesConditionalAccess(invocation, sourceParameterName),
            BaseObjectCreationExpressionSyntax objectCreation =>
                ObjectCreationRequiresNonNullArgument(objectCreation, argument, destinationType),
            _ => false
        };
    }

    private static bool InvocationRequiresNonNullArgument(InvocationExpressionSyntax invocation, ArgumentSyntax argument)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            GetInvocationMethodName(memberAccess) is not { } methodName ||
            !IsKnownNullIntolerantParseType(memberAccess.Expression))
        {
            return false;
        }

        return methodName switch
        {
            "Parse" => IsParseValueArgument(invocation.ArgumentList, argument),
            "ParseExact" => IsParseExactValueOrFormatArgument(invocation.ArgumentList, argument),
            _ => false
        };
    }

    private static bool InvocationReturnsFalseForNullArgument(
        InvocationExpressionSyntax invocation,
        ArgumentSyntax argument)
    {
        if (argument.Parent is not ArgumentListSyntax argumentList ||
            argumentList.Parent != invocation ||
            invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            GetInvocationMethodName(memberAccess) is not { } methodName ||
            !IsKnownNullIntolerantParseType(memberAccess.Expression))
        {
            return false;
        }

        return methodName switch
        {
            "TryParse" => IsParseValueArgument(argumentList, argument),
            "TryParseExact" => IsParseExactValueOrFormatArgument(argumentList, argument),
            _ => false
        };
    }

    private static bool ObjectCreationRequiresNonNullArgument(
        BaseObjectCreationExpressionSyntax objectCreation,
        ArgumentSyntax argument,
        ITypeSymbol? destinationType)
    {
        if (!IsFirstPositionalArgument(objectCreation.ArgumentList, argument))
        {
            return false;
        }

        return objectCreation switch
        {
            ObjectCreationExpressionSyntax { Type: var type } => GetRightmostTypeName(type) == "Uri",
            ImplicitObjectCreationExpressionSyntax => IsUriType(destinationType),
            _ => false
        };
    }

    private static bool IsUriType(ITypeSymbol? type)
    {
        return type?.Name == "Uri" &&
               type.ContainingNamespace?.ToDisplayString() == "System";
    }

    private static bool IsParseValueArgument(ArgumentListSyntax argumentList, ArgumentSyntax argument)
    {
        return argument.NameColon?.Name.Identifier.ValueText switch
        {
            "s" or "input" or "value" or "uriString" => true,
            not null => false,
            _ => IsFirstPositionalArgument(argumentList, argument)
        };
    }

    private static bool IsParseExactValueOrFormatArgument(ArgumentListSyntax argumentList, ArgumentSyntax argument)
    {
        return argument.NameColon?.Name.Identifier.ValueText switch
        {
            "s" or "input" or "value" or "format" or "formats" => true,
            not null => false,
            _ => GetArgumentIndex(argumentList, argument) is 0 or 1
        };
    }

    private static bool IsFirstPositionalArgument(ArgumentListSyntax? argumentList, ArgumentSyntax argument)
    {
        return GetArgumentIndex(argumentList, argument) == 0;
    }

    private static int GetArgumentIndex(ArgumentListSyntax? argumentList, ArgumentSyntax argument)
    {
        return argumentList?.Arguments.IndexOf(argument) ?? -1;
    }

    private static bool IsKnownNullIntolerantParseType(ExpressionSyntax expression)
    {
        return GetRightmostExpressionName(expression) is
            "DateTime" or
            "DateTimeOffset" or
            "DateOnly" or
            "TimeOnly" or
            "TimeSpan" or
            "Guid" or
            "Uri" or
            "bool" or
            "Boolean" or
            "char" or
            "Char" or
            "sbyte" or
            "SByte" or
            "byte" or
            "Byte" or
            "short" or
            "Int16" or
            "ushort" or
            "UInt16" or
            "int" or
            "Int32" or
            "uint" or
            "UInt32" or
            "long" or
            "Int64" or
            "ulong" or
            "UInt64" or
            "float" or
            "Single" or
            "double" or
            "Double" or
            "decimal" or
            "Decimal";
    }

    private static string? GetRightmostExpressionName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            PredefinedTypeSyntax predefinedType => predefinedType.Keyword.ValueText,
            _ => null
        };
    }

    private static string? GetRightmostTypeName(TypeSyntax type)
    {
        return type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualifiedName => qualifiedName.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliasQualifiedName => aliasQualifiedName.Name.Identifier.ValueText,
            NullableTypeSyntax nullableType => GetRightmostTypeName(nullableType.ElementType),
            _ => null
        };
    }

    private static bool TryGetContainingArgument(
        SyntaxNode node,
        out ArgumentSyntax argument)
    {
        for (SyntaxNode? current = node.Parent; current != null; current = current.Parent)
        {
            if (current is ArgumentSyntax containingArgument)
            {
                argument = containingArgument;
                return true;
            }

            if (current is StatementSyntax or ArrowExpressionClauseSyntax or LambdaExpressionSyntax)
            {
                break;
            }
        }

        argument = null!;
        return false;
    }

    private static bool NullTolerantInvocationHandlesConditionalAccess(
        InvocationExpressionSyntax invocation,
        string sourceParameterName)
    {
        return IsTryPatternInvocation(invocation) &&
               (TryPatternConditionalExpressionHandlesNullSource(invocation, sourceParameterName) ||
                TryPatternIfStatementHandlesNullSource(invocation, sourceParameterName));
    }

    private static bool TryPatternConditionalExpressionHandlesNullSource(
        InvocationExpressionSyntax invocation,
        string sourceParameterName)
    {
        if (GetContainingConditionalExpressionCondition(invocation) is not { } conditionalExpression ||
            GetConditionValueWhenInvocationReturnsFalse(invocation, conditionalExpression.Condition) is not { } conditionValueWhenSourceIsNull)
        {
            return false;
        }

        ExpressionSyntax nullArm = conditionValueWhenSourceIsNull
            ? conditionalExpression.WhenTrue
            : conditionalExpression.WhenFalse;
        return !ExpressionContainsSourceReference(nullArm, sourceParameterName);
    }

    private static bool TryPatternIfStatementHandlesNullSource(
        InvocationExpressionSyntax invocation,
        string sourceParameterName)
    {
        if (GetContainingIfCondition(invocation) is not { } ifStatement ||
            GetConditionValueWhenInvocationReturnsFalse(invocation, ifStatement.Condition) is not { } conditionValueWhenSourceIsNull)
        {
            return false;
        }

        if (ifStatement.Else?.Statement is { } elseStatement)
        {
            StatementSyntax nullBranch = conditionValueWhenSourceIsNull
                ? ifStatement.Statement
                : elseStatement;
            return NullBranchHandlesSourceNull(ifStatement, nullBranch, sourceParameterName);
        }

        return conditionValueWhenSourceIsNull
            ? NullBranchHandlesSourceNull(ifStatement, ifStatement.Statement, sourceParameterName)
            : NextStatementExitsWithoutSource(ifStatement, sourceParameterName);
    }

    private static bool? GetConditionValueWhenInvocationReturnsFalse(
        InvocationExpressionSyntax invocation,
        ExpressionSyntax condition)
    {
        ExpressionSyntax unwrappedCondition = UnwrapExpression(condition);
        if (unwrappedCondition == invocation)
        {
            return false;
        }

        if (unwrappedCondition.IsKind(SyntaxKind.LogicalNotExpression) &&
            unwrappedCondition is PrefixUnaryExpressionSyntax logicalNotExpression)
        {
            bool? operandValue = GetConditionValueWhenInvocationReturnsFalse(invocation, logicalNotExpression.Operand);
            return operandValue.HasValue ? !operandValue.Value : null;
        }

        return null;
    }

    private static bool IsTryPatternInvocation(InvocationExpressionSyntax invocation)
    {
        return GetInvocationMethodName(invocation.Expression) is { } methodName &&
               methodName.StartsWith("Try", StringComparison.Ordinal);
    }

    private static string? GetInvocationMethodName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
            _ => null
        };
    }

    private static bool SourceConditionalAccessPropagatesNullToReturn(
        MethodDeclarationSyntax method,
        string sourceParameterName)
    {
        return GetConverterExecutableDescendantNodes(method)
            .OfType<ConditionalAccessExpressionSyntax>()
            .Any(conditional =>
                IsConditionalAccessRootedInSource(conditional, sourceParameterName) &&
                ConditionalAccessFlowsToReturn(conditional, method, sourceParameterName));
    }

    private static bool IsInsideSourceRootedConditionalAccess(
        SyntaxNode node,
        string sourceParameterName)
    {
        for (SyntaxNode? current = node.Parent; current != null; current = current.Parent)
        {
            if (current is ConditionalAccessExpressionSyntax conditional &&
                IsConditionalAccessRootedInSource(conditional, sourceParameterName))
            {
                return true;
            }

            if (current is MethodDeclarationSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryGetSourceConditionalAccessLocalName(
        ExpressionSyntax expression,
        MethodDeclarationSyntax method,
        string sourceParameterName,
        out string localName)
    {
        ExpressionSyntax unwrappedExpression = UnwrapExpression(expression);
        if (unwrappedExpression is not IdentifierNameSyntax identifier)
        {
            localName = string.Empty;
            return false;
        }

        localName = identifier.Identifier.ValueText;
        int usePosition = unwrappedExpression.SpanStart;
        string resolvedLocalName = localName;
        return GetSourceConditionalAccessLocalOriginEnds(method, sourceParameterName, resolvedLocalName, usePosition)
            .Any(originEnd =>
                !HasLocalAssignmentBetween(method, resolvedLocalName, originEnd, usePosition) &&
                !HasUnsafeSourceUsageBetween(method, sourceParameterName, originEnd, usePosition));
    }

    private static bool IsPotentialSourceConditionalAccessLocalReference(
        ExpressionSyntax expression,
        MethodDeclarationSyntax method,
        string sourceParameterName)
    {
        ExpressionSyntax unwrappedExpression = UnwrapExpression(expression);
        if (unwrappedExpression is not IdentifierNameSyntax identifier)
        {
            return false;
        }

        string localName = identifier.Identifier.ValueText;
        int usePosition = unwrappedExpression.SpanStart;
        return GetSourceConditionalAccessLocalOriginEnds(method, sourceParameterName, localName, usePosition)
            .Any(originEnd =>
                !HasUnconditionalLocalAssignmentBetween(method, localName, originEnd, usePosition) &&
                !HasUnsafeSourceUsageBetween(method, sourceParameterName, originEnd, usePosition));
    }

    private static bool HasConditionalAccessLocalGuardedFlow(
        MethodDeclarationSyntax method,
        string sourceParameterName,
        ITypeSymbol? destinationType)
    {
        IEnumerable<VariableDeclaratorSyntax> localDeclarations = GetConverterBodyVariableDeclarators(method)
            .Where(variable =>
                variable.Initializer?.Value is { } initializer &&
                IsSourceConditionalAccess(initializer, sourceParameterName));

        foreach (VariableDeclaratorSyntax localDeclaration in localDeclarations)
        {
            string localName = localDeclaration.Identifier.ValueText;
            if (LocalNullGuardHandlesSourceNull(
                    method,
                    sourceParameterName,
                    localName,
                    localDeclaration.SpanStart,
                    localDeclaration.Span.End,
                    destinationType))
            {
                return true;
            }
        }

        foreach (AssignmentExpressionSyntax assignment in GetConverterBodyDescendantNodes(method)
                     .OfType<AssignmentExpressionSyntax>())
        {
            if (!IsSourceConditionalAccess(assignment.Right, sourceParameterName) ||
                UnwrapExpression(assignment.Left) is not IdentifierNameSyntax identifier ||
                !HasLocalDeclarationInScopeAtPosition(method, identifier.Identifier.ValueText, assignment.SpanStart))
            {
                continue;
            }

            if (LocalNullGuardHandlesSourceNull(
                    method,
                    sourceParameterName,
                    identifier.Identifier.ValueText,
                    assignment.SpanStart,
                    assignment.Span.End,
                    destinationType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasConditionalAccessBooleanLocalGuardedFlow(
        MethodDeclarationSyntax method,
        string sourceParameterName)
    {
        IEnumerable<VariableDeclaratorSyntax> localDeclarations = GetConverterBodyVariableDeclarators(method)
            .Where(variable =>
                variable.Initializer?.Value is { } initializer &&
                TryGetBooleanExpressionValueWhenSourceIsNull(
                    initializer,
                    sourceParameterName,
                    out _));

        foreach (VariableDeclaratorSyntax localDeclaration in localDeclarations)
        {
            string localName = localDeclaration.Identifier.ValueText;
            if (localDeclaration.Initializer?.Value is not { } initializer ||
                !TryGetBooleanExpressionValueWhenSourceIsNull(
                    initializer,
                    sourceParameterName,
                    out bool localValueWhenSourceIsNull))
            {
                continue;
            }

            if (BooleanLocalGuardHandlesSourceNull(
                    method,
                    sourceParameterName,
                    localName,
                    localValueWhenSourceIsNull,
                    localDeclaration.SpanStart,
                    localDeclaration.Span.End))
            {
                return true;
            }
        }

        foreach (AssignmentExpressionSyntax assignment in GetConverterBodyDescendantNodes(method)
                     .OfType<AssignmentExpressionSyntax>())
        {
            if (!TryGetBooleanExpressionValueWhenSourceIsNull(
                    assignment.Right,
                    sourceParameterName,
                    out bool localValueWhenSourceIsNull) ||
                UnwrapExpression(assignment.Left) is not IdentifierNameSyntax identifier ||
                !HasLocalDeclarationInScopeAtPosition(method, identifier.Identifier.ValueText, assignment.SpanStart))
            {
                continue;
            }

            if (BooleanLocalGuardHandlesSourceNull(
                    method,
                    sourceParameterName,
                    identifier.Identifier.ValueText,
                    localValueWhenSourceIsNull,
                    assignment.SpanStart,
                    assignment.Span.End))
            {
                return true;
            }
        }

        return false;
    }

    private static bool BooleanLocalGuardHandlesSourceNull(
        MethodDeclarationSyntax method,
        string sourceParameterName,
        string localName,
        bool localValueWhenSourceIsNull,
        int guardedAssignmentStart,
        int guardedAssignmentEnd)
    {
        foreach (IfStatementSyntax ifStatement in GetConverterBodyDescendantNodes(method).OfType<IfStatementSyntax>())
        {
            if (ifStatement.SpanStart <= guardedAssignmentStart ||
                HasLocalAssignmentBetween(method, localName, guardedAssignmentEnd, ifStatement.Condition.SpanStart) ||
                HasUnsafeSourceUsageBetween(
                    method,
                    sourceParameterName,
                    guardedAssignmentEnd,
                    ifStatement.Condition.SpanStart))
            {
                continue;
            }

            if (GetConditionValueWhenBooleanLocalHasValue(
                    ifStatement.Condition,
                    localName,
                    localValueWhenSourceIsNull) is not { } conditionValueWhenSourceIsNull)
            {
                continue;
            }

            if (ifStatement.Else?.Statement is { } elseStatement)
            {
                StatementSyntax nullBranch = conditionValueWhenSourceIsNull
                    ? ifStatement.Statement
                    : elseStatement;
                if (NullBranchHandlesSourceNull(ifStatement, nullBranch, sourceParameterName))
                {
                    return true;
                }

                continue;
            }

            if (conditionValueWhenSourceIsNull)
            {
                if (NullBranchHandlesSourceNull(ifStatement, ifStatement.Statement, sourceParameterName))
                {
                    return true;
                }
            }
            else if (NextStatementExitsWithoutSource(ifStatement, sourceParameterName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetBooleanExpressionValueWhenSourceIsNull(
        ExpressionSyntax expression,
        string sourceParameterName,
        out bool value)
    {
        foreach (ConditionalAccessExpressionSyntax conditional in expression
                     .DescendantNodesAndSelf()
                     .OfType<ConditionalAccessExpressionSyntax>())
        {
            if (IsConditionalAccessRootedInSource(conditional, sourceParameterName) &&
                GetConditionValueWhenSourceIsNull(conditional, expression) is { } conditionValue)
            {
                value = conditionValue;
                return true;
            }
        }

        value = false;
        return false;
    }

    private static bool? GetConditionValueWhenBooleanLocalHasValue(
        ExpressionSyntax condition,
        string localName,
        bool localValue)
    {
        ExpressionSyntax unwrappedCondition = UnwrapExpression(condition);
        if (IsIdentifierReference(unwrappedCondition, localName))
        {
            return localValue;
        }

        if (unwrappedCondition.IsKind(SyntaxKind.LogicalNotExpression) &&
            unwrappedCondition is PrefixUnaryExpressionSyntax logicalNotExpression)
        {
            bool? operandValue = GetConditionValueWhenBooleanLocalHasValue(
                logicalNotExpression.Operand,
                localName,
                localValue);
            return operandValue.HasValue ? !operandValue.Value : null;
        }

        if (unwrappedCondition is IsPatternExpressionSyntax patternExpression &&
            IsIdentifierReference(patternExpression.Expression, localName))
        {
            return GetPatternValueWhenBooleanInput(patternExpression.Pattern, localValue);
        }

        if (unwrappedCondition is not BinaryExpressionSyntax binary)
        {
            return null;
        }

        if (binary.IsKind(SyntaxKind.LogicalAndExpression))
        {
            bool? leftValue = ExpressionContainsIdentifierUsage(binary.Left, localName)
                ? GetConditionValueWhenBooleanLocalHasValue(binary.Left, localName, localValue)
                : null;
            bool? rightValue = ExpressionContainsIdentifierUsage(binary.Right, localName)
                ? GetConditionValueWhenBooleanLocalHasValue(binary.Right, localName, localValue)
                : null;

            if (leftValue == false || rightValue == false)
            {
                return false;
            }

            return leftValue == true && rightValue == true ? true : null;
        }

        if (binary.IsKind(SyntaxKind.LogicalOrExpression))
        {
            bool? leftValue = ExpressionContainsIdentifierUsage(binary.Left, localName)
                ? GetConditionValueWhenBooleanLocalHasValue(binary.Left, localName, localValue)
                : null;
            bool? rightValue = ExpressionContainsIdentifierUsage(binary.Right, localName)
                ? GetConditionValueWhenBooleanLocalHasValue(binary.Right, localName, localValue)
                : null;

            if (leftValue == true || rightValue == true)
            {
                return true;
            }

            return leftValue == false && rightValue == false ? false : null;
        }

        ExpressionSyntax? otherOperand = GetOtherOperand(binary, localName);
        if (otherOperand == null || !TryGetBooleanLiteral(otherOperand, out bool comparedValue))
        {
            return null;
        }

        return binary.Kind() switch
        {
            SyntaxKind.EqualsExpression => localValue == comparedValue,
            SyntaxKind.NotEqualsExpression => localValue != comparedValue,
            _ => null
        };
    }

    private static bool LocalNullGuardHandlesSourceNull(
        MethodDeclarationSyntax method,
        string sourceParameterName,
        string localName,
        int guardedAssignmentStart,
        int guardedAssignmentEnd,
        ITypeSymbol? destinationType)
    {
        foreach (IfStatementSyntax ifStatement in GetConverterBodyDescendantNodes(method).OfType<IfStatementSyntax>())
        {
            if (ifStatement.SpanStart <= guardedAssignmentStart ||
                HasLocalAssignmentBetween(method, localName, guardedAssignmentEnd, ifStatement.Condition.SpanStart) ||
                HasUnsafeSourceUsageBetween(
                    method,
                    sourceParameterName,
                    guardedAssignmentEnd,
                    ifStatement.Condition.SpanStart))
            {
                continue;
            }

            if (GetConditionValueWhenLocalIsNull(ifStatement.Condition, localName) is not { } conditionValueWhenLocalIsNull)
            {
                continue;
            }

            if (ifStatement.Else?.Statement is { } elseStatement)
            {
                StatementSyntax nullBranch = conditionValueWhenLocalIsNull
                    ? ifStatement.Statement
                    : elseStatement;
                if (NullBranchHandlesLocalNull(ifStatement, nullBranch, sourceParameterName, localName) ||
                    NullableLocalFallbackHandlesLocalNull(nullBranch, destinationType, sourceParameterName, localName))
                {
                    return true;
                }

                continue;
            }

            if (conditionValueWhenLocalIsNull)
            {
                if (NullBranchHandlesLocalNull(ifStatement, ifStatement.Statement, sourceParameterName, localName))
                {
                    return true;
                }

                if (NullBranchAssignsSourceFreeLocalFallback(
                        ifStatement.Statement,
                        sourceParameterName,
                        localName) &&
                    NextStatementExitsWithoutSource(ifStatement, sourceParameterName))
                {
                    return true;
                }
            }
            else if (NextStatementExitsWithoutReferences(ifStatement, sourceParameterName, localName) ||
                     NullableNextStatementReturnsLocal(ifStatement, destinationType, sourceParameterName, localName))
            {
                return true;
            }
        }

        return LocalNullGuardHandlesConditionalReturn(
            method,
            sourceParameterName,
            localName,
            guardedAssignmentStart,
            guardedAssignmentEnd);
    }

    private static bool LocalNullGuardHandlesConditionalReturn(
        MethodDeclarationSyntax method,
        string sourceParameterName,
        string localName,
        int guardedAssignmentStart,
        int guardedAssignmentEnd)
    {
        foreach (ConditionalExpressionSyntax conditionalExpression in GetConverterBodyDescendantNodes(method)
                     .OfType<ConditionalExpressionSyntax>())
        {
            if (conditionalExpression.SpanStart <= guardedAssignmentStart ||
                HasLocalAssignmentBetween(method, localName, guardedAssignmentEnd, conditionalExpression.SpanStart) ||
                HasUnsafeSourceUsageBetween(
                    method,
                    sourceParameterName,
                    guardedAssignmentEnd,
                    conditionalExpression.SpanStart))
            {
                continue;
            }

            if (GetConditionValueWhenLocalIsNull(conditionalExpression.Condition, localName) is not { } conditionValueWhenLocalIsNull)
            {
                continue;
            }

            ExpressionSyntax nullArm = conditionValueWhenLocalIsNull
                ? conditionalExpression.WhenTrue
                : conditionalExpression.WhenFalse;
            if (!ExpressionContainsAnyIdentifierUsage(nullArm, sourceParameterName, localName) &&
                ExpressionFlowsToReturn(conditionalExpression, method))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLocalDeclarationInScopeAtPosition(
        MethodDeclarationSyntax method,
        string localName,
        int usePosition)
    {
        return GetConverterBodyVariableDeclarators(method)
            .Any(variable =>
                string.Equals(variable.Identifier.ValueText, localName, StringComparison.Ordinal) &&
                IsLocalDeclarationInScopeAtPosition(variable, usePosition));
    }

    private static IEnumerable<int> GetSourceConditionalAccessLocalOriginEnds(
        MethodDeclarationSyntax method,
        string sourceParameterName,
        string localName,
        int usePosition)
    {
        foreach (VariableDeclaratorSyntax variable in GetConverterBodyVariableDeclarators(method))
        {
            if (!IsLocalDeclarationInScopeAtPosition(variable, usePosition) ||
                !string.Equals(variable.Identifier.ValueText, localName, StringComparison.Ordinal) ||
                variable.Initializer?.Value is not { } initializer ||
                !IsSourceConditionalAccess(initializer, sourceParameterName))
            {
                continue;
            }

            yield return variable.Span.End;
        }

        foreach (AssignmentExpressionSyntax assignment in GetLocalAssignments(method, localName)
                     .Where(assignment =>
                         assignment.SpanStart < usePosition &&
                         IsSourceConditionalAccess(assignment.Right, sourceParameterName) &&
                         HasLocalDeclarationInScopeAtPosition(method, localName, assignment.SpanStart))
                     .OrderByDescending(assignment => assignment.SpanStart))
        {
            yield return assignment.Span.End;
        }
    }

    private static bool IsLocalDeclarationInScopeAtPosition(
        VariableDeclaratorSyntax variable,
        int usePosition)
    {
        if (variable.SpanStart >= usePosition)
        {
            return false;
        }

        if (variable.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>() is not { } declarationStatement ||
            declarationStatement.Parent is not BlockSyntax block)
        {
            return true;
        }

        return usePosition < block.Span.End;
    }

    private static IEnumerable<VariableDeclaratorSyntax> GetConverterBodyVariableDeclarators(
        MethodDeclarationSyntax method)
    {
        return GetConverterBodyDescendantNodes(method).OfType<VariableDeclaratorSyntax>();
    }

    private static IEnumerable<SyntaxNode> GetConverterBodyDescendantNodes(MethodDeclarationSyntax method)
    {
        if (method.Body == null)
        {
            return Enumerable.Empty<SyntaxNode>();
        }

        return method.Body
            .DescendantNodes()
            .Where(node => !IsInsideNestedFunction(node, method.Body));
    }

    private static IEnumerable<SyntaxNode> GetConverterExecutableDescendantNodes(MethodDeclarationSyntax method)
    {
        if (method.Body != null)
        {
            foreach (SyntaxNode node in method.Body
                         .DescendantNodes()
                         .Where(node => !IsInsideNestedFunction(node, method.Body)))
            {
                yield return node;
            }
        }

        if (method.ExpressionBody?.Expression is { } expression)
        {
            foreach (SyntaxNode node in expression
                         .DescendantNodesAndSelf()
                         .Where(node => !IsInsideNestedFunction(node, expression)))
            {
                yield return node;
            }
        }
    }

    private static bool HasLocalAssignmentBetween(
        MethodDeclarationSyntax method,
        string localName,
        int start,
        int end)
    {
        return GetLocalAssignments(method, localName)
            .Any(assignment => assignment.SpanStart > start && assignment.SpanStart < end);
    }

    private static bool HasUnconditionalLocalAssignmentBetween(
        MethodDeclarationSyntax method,
        string localName,
        int start,
        int end)
    {
        return GetLocalAssignments(method, localName)
            .Any(assignment =>
                assignment.SpanStart > start &&
                assignment.SpanStart < end &&
                assignment.Parent is ExpressionStatementSyntax expressionStatement &&
                expressionStatement.Parent == method.Body);
    }

    private static bool HasUnsafeLocalAssignmentAfterDeclaration(
        MethodDeclarationSyntax method,
        string localName,
        string sourceParameterName,
        VariableDeclaratorSyntax localDeclaration)
    {
        return GetLocalAssignments(method, localName)
            .Any(assignment =>
                assignment.SpanStart > localDeclaration.SpanStart &&
                ExpressionContainsSourceReference(assignment.Right, sourceParameterName));
    }

    private static bool HasUnsafeSourceUsageAfterDeclaration(
        MethodDeclarationSyntax method,
        string sourceParameterName,
        VariableDeclaratorSyntax localDeclaration)
    {
        return HasUnsafeSourceUsageBetween(method, sourceParameterName, localDeclaration.Span.End, int.MaxValue);
    }

    private static bool HasUnsafeSourceUsageBetween(
        MethodDeclarationSyntax method,
        string sourceParameterName,
        int start,
        int end)
    {
        return GetConverterBodyDescendantNodes(method)
            .OfType<IdentifierNameSyntax>()
            .Any(identifier =>
                identifier.SpanStart > start &&
                identifier.SpanStart < end &&
                IsIdentifierUsage(identifier, sourceParameterName) &&
                !IsInsideSourceRootedConditionalAccess(identifier, sourceParameterName));
    }

    private static IEnumerable<AssignmentExpressionSyntax> GetLocalAssignments(
        MethodDeclarationSyntax method,
        string localName)
    {
        return GetConverterBodyDescendantNodes(method)
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment => IsIdentifierReference(assignment.Left, localName));
    }

    private static bool ConditionalAccessGuardsReturningBranch(
        ConditionalAccessExpressionSyntax conditional,
        string sourceParameterName)
    {
        IfStatementSyntax? ifStatement = GetContainingIfCondition(conditional);
        if (ifStatement == null ||
            GetConditionValueWhenSourceIsNull(conditional, ifStatement.Condition) is not { } conditionValueWhenSourceIsNull)
        {
            return false;
        }

        if (ifStatement.Else?.Statement is { } elseStatement)
        {
            StatementSyntax nullBranch = conditionValueWhenSourceIsNull
                ? ifStatement.Statement
                : elseStatement;
            return NullBranchHandlesSourceNull(ifStatement, nullBranch, sourceParameterName);
        }

        if (ifStatement.Parent is not BlockSyntax block)
        {
            return false;
        }

        if (conditionValueWhenSourceIsNull)
        {
            return NullBranchHandlesSourceNull(ifStatement, ifStatement.Statement, sourceParameterName);
        }

        return NextStatementExitsWithoutSource(ifStatement, sourceParameterName);
    }

    private static bool NullBranchHandlesLocalNull(
        IfStatementSyntax ifStatement,
        StatementSyntax nullBranch,
        string sourceParameterName,
        string localName)
    {
        if (StatementContainsAnyIdentifierUsage(nullBranch, sourceParameterName, localName))
        {
            return false;
        }

        return ContainsTerminalExitStatement(nullBranch) ||
               NextStatementExitsWithoutReferences(ifStatement, sourceParameterName, localName);
    }

    private static bool NullBranchAssignsSourceFreeLocalFallback(
        StatementSyntax nullBranch,
        string sourceParameterName,
        string localName)
    {
        if (nullBranch is BlockSyntax block)
        {
            return block.Statements.Count == 1 &&
                   NullBranchAssignsSourceFreeLocalFallback(
                       block.Statements[0],
                       sourceParameterName,
                       localName);
        }

        return nullBranch is ExpressionStatementSyntax
        {
            Expression: AssignmentExpressionSyntax assignment
        } &&
               IsIdentifierReference(assignment.Left, localName) &&
               !ExpressionContainsAnyIdentifierUsage(assignment.Right, sourceParameterName, localName) &&
               !IsExplicitNullFallback(assignment.Right);
    }

    private static bool NullableLocalFallbackHandlesLocalNull(
        StatementSyntax nullBranch,
        ITypeSymbol? destinationType,
        string sourceParameterName,
        string localName)
    {
        return destinationType != null &&
               IsNullableType(destinationType) &&
               StatementReturnsLocalWithoutSource(nullBranch, localName, sourceParameterName);
    }

    private static bool NullableNextStatementReturnsLocal(
        IfStatementSyntax ifStatement,
        ITypeSymbol? destinationType,
        string sourceParameterName,
        string localName)
    {
        if (destinationType == null || !IsNullableType(destinationType))
        {
            return false;
        }

        if (ifStatement.Parent is not BlockSyntax block)
        {
            return false;
        }

        int statementIndex = block.Statements.IndexOf(ifStatement);
        return statementIndex >= 0 &&
               statementIndex + 1 < block.Statements.Count &&
               StatementReturnsLocalWithoutSource(
                   block.Statements[statementIndex + 1],
                   localName,
                   sourceParameterName);
    }

    private static bool StatementReturnsLocalWithoutSource(
        StatementSyntax statement,
        string localName,
        string sourceParameterName)
    {
        if (statement is BlockSyntax block)
        {
            return block.Statements.Count == 1 &&
                   StatementReturnsLocalWithoutSource(block.Statements[0], localName, sourceParameterName);
        }

        return statement is ReturnStatementSyntax { Expression: { } returnExpression } &&
               IsIdentifierReference(returnExpression, localName) &&
               !StatementContainsAnyIdentifierUsage(statement, sourceParameterName);
    }

    private static bool NullBranchHandlesSourceNull(
        IfStatementSyntax ifStatement,
        StatementSyntax nullBranch,
        string sourceParameterName)
    {
        if (StatementContainsSourceReference(nullBranch, sourceParameterName))
        {
            return false;
        }

        return ContainsTerminalExitStatement(nullBranch) ||
               NextStatementExitsWithoutSource(ifStatement, sourceParameterName);
    }

    private static bool NextStatementExitsWithoutSource(
        IfStatementSyntax ifStatement,
        string sourceParameterName)
    {
        return NextStatementExitsWithoutReferences(ifStatement, sourceParameterName);
    }

    private static bool NextStatementExitsWithoutReferences(
        IfStatementSyntax ifStatement,
        params string[] unsafeIdentifierNames)
    {
        return NextStatementExitsWithoutReferences((StatementSyntax)ifStatement, unsafeIdentifierNames);
    }

    private static bool NextStatementExitsWithoutReferences(
        StatementSyntax statement,
        params string[] unsafeIdentifierNames)
    {
        if (statement.Parent is not BlockSyntax block)
        {
            return false;
        }

        int statementIndex = block.Statements.IndexOf(statement);
        if (statementIndex < 0)
        {
            return false;
        }

        for (int i = statementIndex + 1; i < block.Statements.Count; i++)
        {
            StatementSyntax followingStatement = block.Statements[i];
            if (StatementContainsAnyIdentifierUsage(followingStatement, unsafeIdentifierNames))
            {
                return false;
            }

            if (ContainsTerminalExitStatement(followingStatement))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ConditionalAccessGuardsConditionalReturn(
        ConditionalAccessExpressionSyntax conditional,
        MethodDeclarationSyntax method,
        string sourceParameterName)
    {
        ConditionalExpressionSyntax? conditionalExpression = GetContainingConditionalExpressionCondition(conditional);
        if (conditionalExpression == null ||
            GetConditionValueWhenSourceIsNull(conditional, conditionalExpression.Condition) is not { } conditionValueWhenSourceIsNull)
        {
            return false;
        }

        ExpressionSyntax nullArm = conditionValueWhenSourceIsNull
            ? conditionalExpression.WhenTrue
            : conditionalExpression.WhenFalse;
        return !ExpressionContainsSourceReference(nullArm, sourceParameterName) &&
               ExpressionFlowsToReturn(conditionalExpression, method, sourceParameterName);
    }

    private static bool ConditionalAccessGuardsSwitchReturn(
        ConditionalAccessExpressionSyntax conditional,
        MethodDeclarationSyntax method,
        string sourceParameterName)
    {
        SwitchExpressionSyntax? switchExpression = GetContainingSwitchExpressionGoverningExpression(conditional);
        if (switchExpression == null || !ExpressionFlowsToReturn(switchExpression, method, sourceParameterName))
        {
            return false;
        }

        bool? governingValueWhenSourceIsNull =
            GetConditionValueWhenSourceIsNull(conditional, switchExpression.GoverningExpression);

        foreach (SwitchExpressionArmSyntax arm in switchExpression.Arms)
        {
            bool? patternValueWhenSourceIsNull = governingValueWhenSourceIsNull.HasValue
                ? GetPatternValueWhenBooleanInput(arm.Pattern, governingValueWhenSourceIsNull.Value)
                : GetPatternValueWhenInputIsNull(arm.Pattern);
            if (patternValueWhenSourceIsNull == false)
            {
                continue;
            }

            if (patternValueWhenSourceIsNull == true)
            {
                if (arm.WhenClause == null)
                {
                    return !ExpressionContainsSourceReference(arm.Expression, sourceParameterName);
                }

                bool? whenValueWhenSourceIsNull =
                    GetPatternWhenClauseValueWhenInputIsNull(arm.Pattern, arm.WhenClause.Condition);
                if (whenValueWhenSourceIsNull == false)
                {
                    continue;
                }

                if (ExpressionContainsSourceReference(arm.WhenClause.Condition, sourceParameterName))
                {
                    return false;
                }

                if (whenValueWhenSourceIsNull == true)
                {
                    return !ExpressionContainsSourceReference(arm.Expression, sourceParameterName);
                }

                if (ExpressionContainsSourceReference(arm.Expression, sourceParameterName))
                {
                    return false;
                }

                continue;
            }

            return false;
        }

        return false;
    }

    private static bool ConditionalAccessGuardsSwitchStatement(
        ConditionalAccessExpressionSyntax conditional,
        string sourceParameterName)
    {
        SwitchStatementSyntax? switchStatement = GetContainingSwitchStatementExpression(conditional);
        if (switchStatement == null)
        {
            return false;
        }

        bool? governingValueWhenSourceIsNull =
            GetConditionValueWhenSourceIsNull(conditional, switchStatement.Expression);
        SwitchSectionSyntax? defaultSection = null;
        foreach (SwitchSectionSyntax section in switchStatement.Sections)
        {
            foreach (SwitchLabelSyntax label in section.Labels)
            {
                if (label is DefaultSwitchLabelSyntax)
                {
                    defaultSection ??= section;
                    continue;
                }

                if (label is CasePatternSwitchLabelSyntax { WhenClause: { } whenClause } patternSwitchLabel &&
                    GetPatternValueWhenInputIsNull(patternSwitchLabel.Pattern) == true)
                {
                    bool? whenValueWhenSourceIsNull =
                        GetPatternWhenClauseValueWhenInputIsNull(patternSwitchLabel.Pattern, whenClause.Condition);
                    if (whenValueWhenSourceIsNull == false)
                    {
                        continue;
                    }

                    if (ExpressionContainsSourceReference(whenClause.Condition, sourceParameterName))
                    {
                        return false;
                    }

                    if (whenValueWhenSourceIsNull == true)
                    {
                        return SwitchSectionHandlesSourceNull(section, switchStatement, sourceParameterName);
                    }

                    if (SwitchSectionHandlesSourceNull(section, switchStatement, sourceParameterName))
                    {
                        continue;
                    }

                    return false;
                }

                if (SwitchLabelHasSafeGuardedNullHandling(label, section, switchStatement, sourceParameterName))
                {
                    continue;
                }

                bool? labelValueWhenSourceIsNull = governingValueWhenSourceIsNull.HasValue
                    ? GetSwitchLabelValueWhenBooleanInput(label, governingValueWhenSourceIsNull.Value)
                    : GetSwitchLabelValueWhenInputIsNull(label);
                if (labelValueWhenSourceIsNull == false)
                {
                    continue;
                }

                return labelValueWhenSourceIsNull == true &&
                       SwitchSectionHandlesSourceNull(section, switchStatement, sourceParameterName);
            }
        }

        if (defaultSection != null)
        {
            return SwitchSectionHandlesSourceNull(defaultSection, switchStatement, sourceParameterName);
        }

        return NextStatementExitsWithoutReferences(switchStatement, sourceParameterName);
    }

    private static bool SwitchLabelHasSafeGuardedNullHandling(
        SwitchLabelSyntax label,
        SwitchSectionSyntax section,
        SwitchStatementSyntax switchStatement,
        string sourceParameterName)
    {
        if (label is not CasePatternSwitchLabelSyntax { WhenClause: { } whenClause } patternSwitchLabel ||
            GetPatternValueWhenInputIsNull(patternSwitchLabel.Pattern) != true)
        {
            return false;
        }

        return !ExpressionContainsSourceReference(whenClause.Condition, sourceParameterName) &&
               SwitchSectionHandlesSourceNull(section, switchStatement, sourceParameterName);
    }

    private static bool SwitchSectionHandlesSourceNull(
        SwitchSectionSyntax section,
        SwitchStatementSyntax switchStatement,
        string sourceParameterName)
    {
        if (section.Statements.Any(statement => StatementContainsSourceReference(statement, sourceParameterName)))
        {
            return false;
        }

        return section.Statements.Any(ContainsTerminalExitStatement) ||
               (section.Statements.Any(statement => statement is BreakStatementSyntax) &&
                NextStatementExitsWithoutReferences(switchStatement, sourceParameterName));
    }

    private static bool? GetSwitchLabelValueWhenInputIsNull(SwitchLabelSyntax label)
    {
        return label switch
        {
            DefaultSwitchLabelSyntax => null,
            CaseSwitchLabelSyntax caseSwitchLabel => IsNullLiteral(caseSwitchLabel.Value),
            CasePatternSwitchLabelSyntax patternSwitchLabel =>
                GetPatternValueWhenInputIsNull(patternSwitchLabel.Pattern) switch
                {
                    false => false,
                    true when patternSwitchLabel.WhenClause == null => true,
                    _ => null
                },
            _ => null
        };
    }

    private static bool? GetSwitchLabelValueWhenBooleanInput(SwitchLabelSyntax label, bool value)
    {
        return label switch
        {
            DefaultSwitchLabelSyntax => null,
            CaseSwitchLabelSyntax caseSwitchLabel when TryGetBooleanLiteral(caseSwitchLabel.Value, out bool expectedValue) =>
                value == expectedValue,
            CaseSwitchLabelSyntax => false,
            CasePatternSwitchLabelSyntax patternSwitchLabel =>
                GetPatternValueWhenBooleanInput(patternSwitchLabel.Pattern, value) switch
                {
                    false => false,
                    true when patternSwitchLabel.WhenClause == null => true,
                    _ => null
                },
            _ => null
        };
    }

    private static IfStatementSyntax? GetContainingIfCondition(SyntaxNode node)
    {
        SyntaxNode current = node;
        while (current.Parent != null)
        {
            if (current.Parent is IfStatementSyntax ifStatement &&
                ifStatement.Condition == current)
            {
                return ifStatement;
            }

            if (current.Parent is StatementSyntax)
            {
                return null;
            }

            current = current.Parent;
        }

        return null;
    }

    private static ConditionalExpressionSyntax? GetContainingConditionalExpressionCondition(SyntaxNode node)
    {
        SyntaxNode current = node;
        while (current.Parent != null)
        {
            if (current.Parent is ConditionalExpressionSyntax conditionalExpression &&
                conditionalExpression.Condition == current)
            {
                return conditionalExpression;
            }

            if (current.Parent is StatementSyntax)
            {
                return null;
            }

            current = current.Parent;
        }

        return null;
    }

    private static SwitchStatementSyntax? GetContainingSwitchStatementExpression(SyntaxNode node)
    {
        SyntaxNode current = node;
        while (current.Parent != null)
        {
            if (current.Parent is SwitchStatementSyntax switchStatement &&
                ContainsNode(switchStatement.Expression, current))
            {
                return switchStatement;
            }

            if (current.Parent is StatementSyntax)
            {
                return null;
            }

            current = current.Parent;
        }

        return null;
    }

    private static SwitchExpressionSyntax? GetContainingSwitchExpressionGoverningExpression(SyntaxNode node)
    {
        SyntaxNode current = node;
        while (current.Parent != null)
        {
            if (current.Parent is SwitchExpressionSyntax switchExpression &&
                ContainsNode(switchExpression.GoverningExpression, current))
            {
                return switchExpression;
            }

            if (current.Parent is StatementSyntax)
            {
                return null;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool ContainsTerminalExitStatement(StatementSyntax statement)
    {
        return statement switch
        {
            ReturnStatementSyntax or ThrowStatementSyntax => true,
            BlockSyntax { Statements.Count: > 0 } block =>
                ContainsTerminalExitStatement(block.Statements[block.Statements.Count - 1]),
            IfStatementSyntax { Else.Statement: { } elseStatement } ifStatement =>
                ContainsTerminalExitStatement(ifStatement.Statement) &&
                ContainsTerminalExitStatement(elseStatement),
            _ => false
        };
    }

    private static bool StatementContainsSourceReference(StatementSyntax statement, string sourceParameterName)
    {
        return StatementContainsAnyIdentifierUsage(statement, sourceParameterName);
    }

    private static bool StatementContainsAnyIdentifierUsage(
        StatementSyntax statement,
        params string[] identifierNames)
    {
        return statement
            .DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => identifierNames.Any(name => IsIdentifierUsage(identifier, name)));
    }

    private static bool ExpressionContainsSourceReference(ExpressionSyntax expression, string sourceParameterName)
    {
        return ExpressionContainsAnyIdentifierUsage(expression, sourceParameterName);
    }

    private static bool ExpressionContainsIdentifierUsage(ExpressionSyntax expression, string identifierName)
    {
        return ExpressionContainsAnyIdentifierUsage(expression, identifierName);
    }

    private static bool ExpressionContainsAnyIdentifierUsage(
        ExpressionSyntax expression,
        params string[] identifierNames)
    {
        return expression
            .DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => identifierNames.Any(name => IsIdentifierUsage(identifier, name)));
    }

    private static bool IsIdentifierUsage(IdentifierNameSyntax identifier, string identifierName)
    {
        return string.Equals(identifier.Identifier.ValueText, identifierName, StringComparison.Ordinal) &&
               !IsInsideNameOfExpression(identifier) &&
               !IsNamedArgumentLabel(identifier) &&
               !IsMemberAccessName(identifier) &&
               !IsShadowedInNestedFunction(identifier, identifierName);
    }

    private static bool IsInsideNameOfExpression(SyntaxNode node)
    {
        for (SyntaxNode? current = node.Parent; current != null; current = current.Parent)
        {
            if (current is InvocationExpressionSyntax
                {
                    Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" }
                })
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMemberAccessName(IdentifierNameSyntax identifier)
    {
        return identifier.Parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Name == identifier;
    }

    private static bool IsNamedArgumentLabel(IdentifierNameSyntax identifier)
    {
        return identifier.Parent is NameColonSyntax nameColon &&
               nameColon.Name == identifier;
    }

    private static bool IsShadowedInNestedFunction(SyntaxNode node, string identifierName)
    {
        for (SyntaxNode? current = node.Parent; current != null; current = current.Parent)
        {
            switch (current)
            {
                case SimpleLambdaExpressionSyntax simpleLambda:
                    if (string.Equals(
                        simpleLambda.Parameter.Identifier.ValueText,
                        identifierName,
                        StringComparison.Ordinal))
                    {
                        return true;
                    }

                    continue;

                case ParenthesizedLambdaExpressionSyntax parenthesizedLambda:
                    if (parenthesizedLambda.ParameterList.Parameters.Any(parameter =>
                            string.Equals(parameter.Identifier.ValueText, identifierName, StringComparison.Ordinal)))
                    {
                        return true;
                    }

                    continue;

                case AnonymousMethodExpressionSyntax anonymousMethod:
                    if (anonymousMethod.ParameterList?.Parameters.Any(parameter =>
                            string.Equals(parameter.Identifier.ValueText, identifierName, StringComparison.Ordinal)) ==
                        true)
                    {
                        return true;
                    }

                    continue;

                case LocalFunctionStatementSyntax localFunction:
                    if (localFunction.ParameterList.Parameters.Any(parameter =>
                            string.Equals(parameter.Identifier.ValueText, identifierName, StringComparison.Ordinal)))
                    {
                        return true;
                    }

                    continue;

                case MethodDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }

    private static bool? GetConditionValueWhenSourceIsNull(
        ConditionalAccessExpressionSyntax conditional,
        ExpressionSyntax condition)
    {
        ExpressionSyntax unwrappedCondition = UnwrapExpression(condition);
        if (unwrappedCondition.IsKind(SyntaxKind.LogicalNotExpression) &&
            unwrappedCondition is PrefixUnaryExpressionSyntax logicalNotExpression)
        {
            bool? operandValue = GetConditionValueWhenSourceIsNull(conditional, logicalNotExpression.Operand);
            return operandValue.HasValue ? !operandValue.Value : null;
        }

        if (unwrappedCondition is IsPatternExpressionSyntax patternExpression)
        {
            bool? expressionValueWhenSourceIsNull =
                GetConditionValueWhenSourceIsNull(conditional, patternExpression.Expression);
            if (expressionValueWhenSourceIsNull.HasValue)
            {
                return GetPatternValueWhenBooleanInput(
                    patternExpression.Pattern,
                    expressionValueWhenSourceIsNull.Value);
            }

            if (ContainsNode(patternExpression.Expression, conditional))
            {
                return GetPatternValueWhenInputIsNull(patternExpression.Pattern);
            }
        }

        if (unwrappedCondition is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.ValueText == "HasValue" &&
            ContainsNode(memberAccess.Expression, conditional))
        {
            return false;
        }

        if (unwrappedCondition is InvocationExpressionSyntax invocation &&
            TryGetContainingArgument(conditional, out ArgumentSyntax invocationArgument) &&
            InvocationReturnsFalseForNullArgument(invocation, invocationArgument))
        {
            return false;
        }

        if (unwrappedCondition is not BinaryExpressionSyntax binary)
        {
            return null;
        }

        if (TryGetSourceConditionalPatternLocal(
                binary.Left,
                conditional,
                out string patternLocalName,
                out bool? leftPatternValueWhenSourceIsNull))
        {
            if (binary.IsKind(SyntaxKind.LogicalAndExpression))
            {
                if (leftPatternValueWhenSourceIsNull == false)
                {
                    return false;
                }

                bool? rightLocalValueWhenSourceIsNull =
                    GetConditionValueWhenLocalIsNull(binary.Right, patternLocalName);
                return leftPatternValueWhenSourceIsNull == true
                    ? rightLocalValueWhenSourceIsNull
                    : rightLocalValueWhenSourceIsNull == false ? false : null;
            }

            if (binary.IsKind(SyntaxKind.LogicalOrExpression))
            {
                if (leftPatternValueWhenSourceIsNull == true)
                {
                    return true;
                }

                bool? rightLocalValueWhenSourceIsNull =
                    GetConditionValueWhenLocalIsNull(binary.Right, patternLocalName);
                return leftPatternValueWhenSourceIsNull == false
                    ? rightLocalValueWhenSourceIsNull
                    : rightLocalValueWhenSourceIsNull == true ? true : null;
            }
        }

        if (binary.IsKind(SyntaxKind.LogicalAndExpression))
        {
            bool? leftValue = ContainsNode(binary.Left, conditional)
                ? GetConditionValueWhenSourceIsNull(conditional, binary.Left)
                : null;
            bool? rightValue = ContainsNode(binary.Right, conditional)
                ? GetConditionValueWhenSourceIsNull(conditional, binary.Right)
                : null;

            if (leftValue == false || rightValue == false)
            {
                return false;
            }

            return leftValue == true && rightValue == true ? true : null;
        }

        if (binary.IsKind(SyntaxKind.LogicalOrExpression))
        {
            bool? leftValue = ContainsNode(binary.Left, conditional)
                ? GetConditionValueWhenSourceIsNull(conditional, binary.Left)
                : null;
            bool? rightValue = ContainsNode(binary.Right, conditional)
                ? GetConditionValueWhenSourceIsNull(conditional, binary.Right)
                : null;

            if (leftValue == true || rightValue == true)
            {
                return true;
            }

            return leftValue == false && rightValue == false ? false : null;
        }

        if (TryGetBooleanComparisonValueWhenSourceIsNull(conditional, binary, out bool comparisonValue))
        {
            return comparisonValue;
        }

        if (TryGetBinaryValueWhenSourceIsNull(conditional, binary, out bool binaryValue))
        {
            return binaryValue;
        }

        ExpressionSyntax? otherOperand = GetOtherOperand(binary, conditional);
        if (otherOperand == null)
        {
            return null;
        }

        return binary.Kind() switch
        {
            SyntaxKind.GreaterThanExpression or
            SyntaxKind.GreaterThanOrEqualExpression or
            SyntaxKind.LessThanExpression or
            SyntaxKind.LessThanOrEqualExpression => false,
            SyntaxKind.EqualsExpression => IsNullLiteral(otherOperand),
            SyntaxKind.NotEqualsExpression => !IsNullLiteral(otherOperand),
            _ => null
        };
    }

    private static bool? GetConditionValueWhenLocalIsNull(ExpressionSyntax condition, string localName)
    {
        ExpressionSyntax unwrappedCondition = UnwrapExpression(condition);
        if (unwrappedCondition.IsKind(SyntaxKind.LogicalNotExpression) &&
            unwrappedCondition is PrefixUnaryExpressionSyntax logicalNotExpression)
        {
            bool? operandValue = GetConditionValueWhenLocalIsNull(logicalNotExpression.Operand, localName);
            return operandValue.HasValue ? !operandValue.Value : null;
        }

        if (unwrappedCondition is IsPatternExpressionSyntax patternExpression &&
            IsIdentifierReference(patternExpression.Expression, localName))
        {
            return GetPatternValueWhenInputIsNull(patternExpression.Pattern);
        }

        if (unwrappedCondition is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.ValueText == "HasValue" &&
            IsIdentifierReference(memberAccess.Expression, localName))
        {
            return false;
        }

        if (unwrappedCondition is not BinaryExpressionSyntax binary)
        {
            return null;
        }

        if (binary.IsKind(SyntaxKind.LogicalAndExpression))
        {
            bool? leftValue = ExpressionContainsIdentifierUsage(binary.Left, localName)
                ? GetConditionValueWhenLocalIsNull(binary.Left, localName)
                : null;
            bool? rightValue = ExpressionContainsIdentifierUsage(binary.Right, localName)
                ? GetConditionValueWhenLocalIsNull(binary.Right, localName)
                : null;

            if (leftValue == false || rightValue == false)
            {
                return false;
            }

            return leftValue == true && rightValue == true ? true : null;
        }

        if (binary.IsKind(SyntaxKind.LogicalOrExpression))
        {
            bool? leftValue = ExpressionContainsIdentifierUsage(binary.Left, localName)
                ? GetConditionValueWhenLocalIsNull(binary.Left, localName)
                : null;
            bool? rightValue = ExpressionContainsIdentifierUsage(binary.Right, localName)
                ? GetConditionValueWhenLocalIsNull(binary.Right, localName)
                : null;

            if (leftValue == true || rightValue == true)
            {
                return true;
            }

            return leftValue == false && rightValue == false ? false : null;
        }

        ExpressionSyntax? otherOperand = GetOtherDirectIdentifierOperand(binary, localName);
        if (otherOperand == null)
        {
            return null;
        }

        return binary.Kind() switch
        {
            SyntaxKind.GreaterThanExpression or
            SyntaxKind.GreaterThanOrEqualExpression or
            SyntaxKind.LessThanExpression or
            SyntaxKind.LessThanOrEqualExpression => false,
            SyntaxKind.EqualsExpression => IsNullLiteral(otherOperand),
            SyntaxKind.NotEqualsExpression => !IsNullLiteral(otherOperand),
            _ => null
        };
    }

    private static bool TryGetBooleanComparisonValueWhenSourceIsNull(
        ConditionalAccessExpressionSyntax conditional,
        BinaryExpressionSyntax binary,
        out bool comparisonValue)
    {
        comparisonValue = false;
        if (!binary.IsKind(SyntaxKind.EqualsExpression) &&
            !binary.IsKind(SyntaxKind.NotEqualsExpression))
        {
            return false;
        }

        ExpressionSyntax? conditionOperand = null;
        bool? comparedValue = null;
        if (ContainsNode(binary.Left, conditional) && TryGetBooleanLiteral(binary.Right, out bool rightBoolean))
        {
            conditionOperand = binary.Left;
            comparedValue = rightBoolean;
        }
        else if (ContainsNode(binary.Right, conditional) && TryGetBooleanLiteral(binary.Left, out bool leftBoolean))
        {
            conditionOperand = binary.Right;
            comparedValue = leftBoolean;
        }

        if (conditionOperand == null || comparedValue == null)
        {
            return false;
        }

        bool? conditionValue = GetConditionValueWhenSourceIsNull(conditional, conditionOperand);
        if (!conditionValue.HasValue)
        {
            return false;
        }

        comparisonValue = binary.IsKind(SyntaxKind.EqualsExpression)
            ? conditionValue.Value == comparedValue.Value
            : conditionValue.Value != comparedValue.Value;
        return true;
    }

    private static bool TryGetBinaryValueWhenSourceIsNull(
        ConditionalAccessExpressionSyntax conditional,
        BinaryExpressionSyntax binary,
        out bool value)
    {
        value = false;
        if (!TryGetExpressionValueWhenSourceIsNull(binary.Left, conditional, out object? leftValue) ||
            !TryGetExpressionValueWhenSourceIsNull(binary.Right, conditional, out object? rightValue))
        {
            return false;
        }

        return TryEvaluateBinaryValue(binary.Kind(), leftValue, rightValue, out value);
    }

    private static bool TryGetExpressionValueWhenSourceIsNull(
        ExpressionSyntax expression,
        ConditionalAccessExpressionSyntax conditional,
        out object? value)
    {
        ExpressionSyntax unwrappedExpression = UnwrapExpression(expression);
        if (unwrappedExpression == conditional)
        {
            value = null;
            return true;
        }

        if (unwrappedExpression is BinaryExpressionSyntax binary &&
            binary.IsKind(SyntaxKind.CoalesceExpression) &&
            ContainsNode(binary.Left, conditional) &&
            TryGetExpressionValueWhenSourceIsNull(binary.Left, conditional, out object? leftValue))
        {
            if (leftValue != null)
            {
                value = leftValue;
                return true;
            }

            return TryGetExpressionValueWhenSourceIsNull(binary.Right, conditional, out value);
        }

        if (unwrappedExpression is BinaryExpressionSyntax nestedBinary &&
            ContainsNode(nestedBinary, conditional) &&
            GetConditionValueWhenSourceIsNull(conditional, nestedBinary) is { } nestedValue)
        {
            value = nestedValue;
            return true;
        }

        return TryGetConstantExpressionValue(unwrappedExpression, out value);
    }

    private static bool TryGetConstantExpressionValue(ExpressionSyntax expression, out object? value)
    {
        ExpressionSyntax unwrappedExpression = UnwrapExpression(expression);
        if (IsNullLiteral(unwrappedExpression))
        {
            value = null;
            return true;
        }

        if (unwrappedExpression.IsKind(SyntaxKind.TrueLiteralExpression))
        {
            value = true;
            return true;
        }

        if (unwrappedExpression.IsKind(SyntaxKind.FalseLiteralExpression))
        {
            value = false;
            return true;
        }

        if (unwrappedExpression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.NumericLiteralExpression))
        {
            value = literal.Token.Value;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryEvaluateBinaryValue(
        SyntaxKind kind,
        object? leftValue,
        object? rightValue,
        out bool value)
    {
        value = false;
        if (kind is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
        {
            bool equal = ValuesEqual(leftValue, rightValue);
            value = kind == SyntaxKind.EqualsExpression ? equal : !equal;
            return true;
        }

        if (leftValue == null || rightValue == null)
        {
            value = false;
            return kind is SyntaxKind.GreaterThanExpression or
                SyntaxKind.GreaterThanOrEqualExpression or
                SyntaxKind.LessThanExpression or
                SyntaxKind.LessThanOrEqualExpression;
        }

        if (!TryConvertToDecimal(leftValue, out decimal leftNumber) ||
            !TryConvertToDecimal(rightValue, out decimal rightNumber))
        {
            return false;
        }

        value = kind switch
        {
            SyntaxKind.GreaterThanExpression => leftNumber > rightNumber,
            SyntaxKind.GreaterThanOrEqualExpression => leftNumber >= rightNumber,
            SyntaxKind.LessThanExpression => leftNumber < rightNumber,
            SyntaxKind.LessThanOrEqualExpression => leftNumber <= rightNumber,
            _ => false
        };

        return kind is SyntaxKind.GreaterThanExpression or
            SyntaxKind.GreaterThanOrEqualExpression or
            SyntaxKind.LessThanExpression or
            SyntaxKind.LessThanOrEqualExpression;
    }

    private static bool ValuesEqual(object? leftValue, object? rightValue)
    {
        if (TryConvertToDecimal(leftValue, out decimal leftNumber) &&
            TryConvertToDecimal(rightValue, out decimal rightNumber))
        {
            return leftNumber == rightNumber;
        }

        return Equals(leftValue, rightValue);
    }

    private static bool TryConvertToDecimal(object? value, out decimal number)
    {
        switch (value)
        {
            case byte byteValue:
                number = byteValue;
                return true;
            case sbyte sbyteValue:
                number = sbyteValue;
                return true;
            case short shortValue:
                number = shortValue;
                return true;
            case ushort ushortValue:
                number = ushortValue;
                return true;
            case int intValue:
                number = intValue;
                return true;
            case uint uintValue:
                number = uintValue;
                return true;
            case long longValue:
                number = longValue;
                return true;
            case ulong ulongValue when ulongValue <= long.MaxValue:
                number = ulongValue;
                return true;
            case float floatValue:
                number = (decimal)floatValue;
                return true;
            case double doubleValue:
                number = (decimal)doubleValue;
                return true;
            case decimal decimalValue:
                number = decimalValue;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private static bool TryGetBooleanLiteral(ExpressionSyntax expression, out bool value)
    {
        ExpressionSyntax unwrappedExpression = UnwrapExpression(expression);
        if (unwrappedExpression.IsKind(SyntaxKind.TrueLiteralExpression))
        {
            value = true;
            return true;
        }

        if (unwrappedExpression.IsKind(SyntaxKind.FalseLiteralExpression))
        {
            value = false;
            return true;
        }

        value = false;
        return false;
    }

    private static bool? GetPatternWhenClauseValueWhenInputIsNull(
        PatternSyntax pattern,
        ExpressionSyntax whenCondition)
    {
        return TryGetPatternVariableName(pattern, out string variableName)
            ? GetConditionValueWhenLocalIsNull(whenCondition, variableName)
            : null;
    }

    private static bool TryGetSourceConditionalPatternLocal(
        ExpressionSyntax expression,
        ConditionalAccessExpressionSyntax conditional,
        out string localName,
        out bool? patternValueWhenSourceIsNull)
    {
        ExpressionSyntax unwrappedExpression = UnwrapExpression(expression);
        if (unwrappedExpression is IsPatternExpressionSyntax patternExpression &&
            ContainsNode(patternExpression.Expression, conditional) &&
            TryGetPatternVariableName(patternExpression.Pattern, out localName))
        {
            patternValueWhenSourceIsNull = GetPatternValueWhenInputIsNull(patternExpression.Pattern);
            return true;
        }

        localName = string.Empty;
        patternValueWhenSourceIsNull = null;
        return false;
    }

    private static bool TryGetPatternVariableName(PatternSyntax pattern, out string variableName)
    {
        switch (pattern)
        {
            case ParenthesizedPatternSyntax parenthesizedPattern:
                return TryGetPatternVariableName(parenthesizedPattern.Pattern, out variableName);
            case VarPatternSyntax { Designation: SingleVariableDesignationSyntax designation }:
                variableName = designation.Identifier.ValueText;
                return true;
            case DeclarationPatternSyntax { Designation: SingleVariableDesignationSyntax designation }:
                variableName = designation.Identifier.ValueText;
                return true;
            case RecursivePatternSyntax { Designation: SingleVariableDesignationSyntax designation }:
                variableName = designation.Identifier.ValueText;
                return true;
            default:
                variableName = string.Empty;
                return false;
        }
    }

    private static bool? GetPatternValueWhenInputIsNull(PatternSyntax pattern)
    {
        return pattern switch
        {
            ParenthesizedPatternSyntax parenthesizedPattern =>
                GetPatternValueWhenInputIsNull(parenthesizedPattern.Pattern),
            ConstantPatternSyntax { Expression: var expression } => IsNullLiteral(expression),
            UnaryPatternSyntax { Pattern: var innerPattern } =>
                GetPatternValueWhenInputIsNull(innerPattern) is { } innerValue ? !innerValue : null,
            ListPatternSyntax => false,
            RelationalPatternSyntax => false,
            DeclarationPatternSyntax => false,
            RecursivePatternSyntax => false,
            VarPatternSyntax => true,
            DiscardPatternSyntax => true,
            BinaryPatternSyntax binaryPattern when binaryPattern.OperatorToken.IsKind(SyntaxKind.AndKeyword) =>
                CombineAndPatternValues(
                    GetPatternValueWhenInputIsNull(binaryPattern.Left),
                    GetPatternValueWhenInputIsNull(binaryPattern.Right)),
            BinaryPatternSyntax binaryPattern when binaryPattern.OperatorToken.IsKind(SyntaxKind.OrKeyword) =>
                CombineOrPatternValues(
                    GetPatternValueWhenInputIsNull(binaryPattern.Left),
                    GetPatternValueWhenInputIsNull(binaryPattern.Right)),
            _ => null
        };
    }

    private static bool? GetPatternValueWhenBooleanInput(PatternSyntax pattern, bool value)
    {
        return pattern switch
        {
            ParenthesizedPatternSyntax parenthesizedPattern =>
                GetPatternValueWhenBooleanInput(parenthesizedPattern.Pattern, value),
            ConstantPatternSyntax { Expression: var expression }
                when TryGetBooleanLiteral(expression, out bool expectedValue) => value == expectedValue,
            ConstantPatternSyntax { Expression: var expression } => IsNullLiteral(expression) ? false : null,
            UnaryPatternSyntax { Pattern: var innerPattern } =>
                GetPatternValueWhenBooleanInput(innerPattern, value) is { } innerValue ? !innerValue : null,
            VarPatternSyntax => true,
            DiscardPatternSyntax => true,
            BinaryPatternSyntax binaryPattern when binaryPattern.OperatorToken.IsKind(SyntaxKind.AndKeyword) =>
                CombineAndPatternValues(
                    GetPatternValueWhenBooleanInput(binaryPattern.Left, value),
                    GetPatternValueWhenBooleanInput(binaryPattern.Right, value)),
            BinaryPatternSyntax binaryPattern when binaryPattern.OperatorToken.IsKind(SyntaxKind.OrKeyword) =>
                CombineOrPatternValues(
                    GetPatternValueWhenBooleanInput(binaryPattern.Left, value),
                    GetPatternValueWhenBooleanInput(binaryPattern.Right, value)),
            _ => null
        };
    }

    private static bool? CombineAndPatternValues(bool? leftValue, bool? rightValue)
    {
        if (leftValue == false || rightValue == false)
        {
            return false;
        }

        return leftValue == true && rightValue == true ? true : null;
    }

    private static bool? CombineOrPatternValues(bool? leftValue, bool? rightValue)
    {
        if (leftValue == true || rightValue == true)
        {
            return true;
        }

        return leftValue == false && rightValue == false ? false : null;
    }

    private static ExpressionSyntax? GetOtherOperand(BinaryExpressionSyntax binary, SyntaxNode operand)
    {
        if (ContainsNode(binary.Left, operand))
        {
            return binary.Right;
        }

        return ContainsNode(binary.Right, operand) ? binary.Left : null;
    }

    private static ExpressionSyntax? GetOtherOperand(BinaryExpressionSyntax binary, string identifierName)
    {
        if (ExpressionContainsIdentifierUsage(binary.Left, identifierName))
        {
            return binary.Right;
        }

        return ExpressionContainsIdentifierUsage(binary.Right, identifierName) ? binary.Left : null;
    }

    private static ExpressionSyntax? GetOtherDirectIdentifierOperand(BinaryExpressionSyntax binary, string identifierName)
    {
        if (IsIdentifierReference(binary.Left, identifierName))
        {
            return binary.Right;
        }

        return IsIdentifierReference(binary.Right, identifierName) ? binary.Left : null;
    }

    private static bool ContainsNode(SyntaxNode root, SyntaxNode target)
    {
        return root == target ||
               root.DescendantNodes().Any(node => node == target);
    }

    private static bool ConditionalAccessFlowsToReturn(
        ConditionalAccessExpressionSyntax conditional,
        MethodDeclarationSyntax method,
        string sourceParameterName)
    {
        return ExpressionFlowsToReturn(conditional, method, sourceParameterName);
    }

    private static bool ExpressionFlowsToReturn(
        SyntaxNode expression,
        MethodDeclarationSyntax method,
        string? sourceParameterName = null)
    {
        SyntaxNode current = expression;
        while (true)
        {
            switch (current.Parent)
            {
                case ParenthesizedExpressionSyntax parenthesizedExpression
                    when parenthesizedExpression.Expression == current:
                    current = parenthesizedExpression;
                    continue;
                case CastExpressionSyntax castExpression
                    when castExpression.Expression == current:
                    current = castExpression;
                    continue;
                case PostfixUnaryExpressionSyntax postfixUnaryExpression
                    when postfixUnaryExpression.IsKind(SyntaxKind.SuppressNullableWarningExpression) &&
                         postfixUnaryExpression.Operand == current:
                    current = postfixUnaryExpression;
                    continue;
            }

            break;
        }

        if ((current.Parent is ReturnStatementSyntax { Expression: { } returnedExpression } &&
             returnedExpression == current) ||
            (current.Parent is ArrowExpressionClauseSyntax { Expression: { } arrowExpression } &&
             arrowExpression == current))
        {
            return true;
        }

        if (current.Parent is not EqualsValueClauseSyntax
            {
                Parent: VariableDeclaratorSyntax variableDeclarator
            })
        {
            return false;
        }

        string localName = variableDeclarator.Identifier.ValueText;
        return sourceParameterName == null
            ? method.Body?.Statements
                  .OfType<ReturnStatementSyntax>()
                  .Any(returnStatement => IsIdentifierReference(returnStatement.Expression, localName)) == true
            : LocalReturnFlowHandlesSourceNull(method, localName, sourceParameterName, variableDeclarator);
    }

    private static bool LocalReturnFlowHandlesSourceNull(
        MethodDeclarationSyntax method,
        string localName,
        string sourceParameterName,
        VariableDeclaratorSyntax localDeclaration)
    {
        if (HasUnsafeLocalAssignmentAfterDeclaration(method, localName, sourceParameterName, localDeclaration))
        {
            return false;
        }

        if (HasUnsafeSourceUsageAfterDeclaration(method, sourceParameterName, localDeclaration))
        {
            return false;
        }

        bool hasLocalReturn = false;
        foreach (ReturnStatementSyntax returnStatement in GetConverterBodyReturnStatements(method))
        {
            if (IsIdentifierReference(returnStatement.Expression, localName))
            {
                hasLocalReturn = true;
                continue;
            }

            if (returnStatement.Expression != null &&
                ExpressionContainsIdentifierUsage(returnStatement.Expression, sourceParameterName))
            {
                return false;
            }
        }

        return hasLocalReturn;
    }

    private static IEnumerable<ReturnStatementSyntax> GetConverterBodyReturnStatements(MethodDeclarationSyntax method)
    {
        if (method.Body == null)
        {
            yield break;
        }

        foreach (ReturnStatementSyntax returnStatement in method.Body.DescendantNodes().OfType<ReturnStatementSyntax>())
        {
            if (!IsInsideNestedFunction(returnStatement, method.Body))
            {
                yield return returnStatement;
            }
        }
    }

    private static bool IsInsideNestedFunction(SyntaxNode node, SyntaxNode scopeRoot)
    {
        for (SyntaxNode? current = node.Parent; current != null && current != scopeRoot; current = current.Parent)
        {
            if (current is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIdentifierReference(ExpressionSyntax? expression, string identifierName)
    {
        if (expression == null)
        {
            return false;
        }

        ExpressionSyntax unwrappedExpression = UnwrapExpression(expression);
        return unwrappedExpression is IdentifierNameSyntax identifier &&
               string.Equals(identifier.Identifier.ValueText, identifierName, StringComparison.Ordinal);
    }

    private static bool IsStringTypeAccess(ExpressionSyntax expression)
    {
        return expression switch
        {
            PredefinedTypeSyntax predefinedType => predefinedType.Keyword.IsKind(SyntaxKind.StringKeyword),
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText is "String" or "string",
            QualifiedNameSyntax qualifiedName => qualifiedName.Right.Identifier.ValueText == "String",
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText == "String",
            _ => false
        };
    }

    private static bool TryGetGuardArgument(ArgumentListSyntax argumentList, out ExpressionSyntax guardedExpression)
    {
        guardedExpression = null!;
        if (argumentList.Arguments.Count == 0)
        {
            return false;
        }

        foreach (ArgumentSyntax argument in argumentList.Arguments)
        {
            if (argument.NameColon?.Name.Identifier.ValueText == "argument")
            {
                guardedExpression = argument.Expression;
                return true;
            }
        }

        guardedExpression = argumentList.Arguments[0].Expression;
        return true;
    }

    private static bool IsArgumentExceptionTypeAccess(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifierName =>
                identifierName.Identifier.ValueText is "ArgumentNullException" or "ArgumentException",
            QualifiedNameSyntax qualifiedName =>
                qualifiedName.Right.Identifier.ValueText is "ArgumentNullException" or "ArgumentException",
            MemberAccessExpressionSyntax memberAccess =>
                memberAccess.Name.Identifier.ValueText is "ArgumentNullException" or "ArgumentException",
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
                case PostfixUnaryExpressionSyntax postfixUnaryExpression
                    when postfixUnaryExpression.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    currentExpression = postfixUnaryExpression.Operand;
                    continue;
                default:
                    return currentExpression;
            }
        }
    }
}

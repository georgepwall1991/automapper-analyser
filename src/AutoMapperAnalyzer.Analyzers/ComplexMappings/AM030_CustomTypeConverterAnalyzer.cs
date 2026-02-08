using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Analyzers.ComplexMappings;

/// <summary>
///     Analyzer for AM030: Custom Type Converter issues in AutoMapper configurations.
///     Detects invalid converter implementations, missing ConvertUsing configurations,
///     and incorrect converter method signatures.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM030_CustomTypeConverterAnalyzer : DiagnosticAnalyzer
{
    private const string IssueTypePropertyName = "IssueType";
    private const string MissingConvertUsingIssueType = "MissingConvertUsing";

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
    ///     Diagnostic rule for missing ConvertUsing configuration.
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
        "Unused type converters can be removed or should be configured in mapping.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            InvalidConverterImplementationRule,
            MissingConvertUsingConfigurationRule,
            ConverterNullHandlingIssueRule,
            UnusedTypeConverterRule
        );

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCreateMapInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeCreateMapInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Check if this is a CreateMap<TSource, TDestination>() call
        if (!AutoMapperAnalysisHelpers.IsCreateMapInvocation(invocation, context.SemanticModel))
        {
            return;
        }

        (ITypeSymbol? sourceType, ITypeSymbol? destType) createMapTypeArgs =
            AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, context.SemanticModel);
        if (createMapTypeArgs.sourceType == null || createMapTypeArgs.destType == null)
        {
            return;
        }

        AnalyzeForMissingConverterConfiguration(context, invocation, createMapTypeArgs.sourceType,
            createMapTypeArgs.destType);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        INamedTypeSymbol? classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);

        if (classSymbol == null)
        {
            return;
        }

        AnalyzeTypeConverterImplementation(context, classDeclaration, classSymbol);
    }

    private static void AnalyzeForMissingConverterConfiguration(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        IEnumerable<IPropertySymbol> sourceProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        IEnumerable<IPropertySymbol> destinationProperties =
            AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, false);

        foreach (IPropertySymbol? sourceProperty in sourceProperties)
        {
            IPropertySymbol? destinationProperty = destinationProperties
                .FirstOrDefault(p => string.Equals(p.Name, sourceProperty.Name, StringComparison.OrdinalIgnoreCase));

            if (destinationProperty == null)
            {
                continue;
            }

            if (!RequiresTypeConverter(context.Compilation, sourceProperty.Type, destinationProperty.Type))
            {
                continue;
            }

            if (IsTypeCoveredByCreateMap(context.Compilation, sourceProperty.Type, destinationProperty.Type))
            {
                continue;
            }

            if (HasConvertUsingConfiguration(invocation, sourceProperty.Name))
            {
                continue;
            }

            ImmutableDictionary<string, string?>.Builder properties =
                ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("PropertyName", sourceProperty.Name);
            properties.Add("ConverterType", GetConverterTypeName(sourceProperty.Type, destinationProperty.Type));
            properties.Add(IssueTypePropertyName, MissingConvertUsingIssueType);

            var diagnostic = Diagnostic.Create(
                MissingConvertUsingConfigurationRule,
                invocation.GetLocation(),
                properties.ToImmutable(),
                sourceProperty.Name,
                GetConverterTypeName(sourceProperty.Type, destinationProperty.Type));

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeTypeConverterImplementation(
        SyntaxNodeAnalysisContext context,
        ClassDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        INamedTypeSymbol? typeConverterInterface = GetTypeConverterInterface(classSymbol);
        if (typeConverterInterface == null)
        {
            return;
        }

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


    private static bool RequiresTypeConverter(Compilation compilation, ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        // Check if types are fundamentally incompatible and would benefit from a converter
        if (SymbolEqualityComparer.Default.Equals(sourceType, destinationType))
        {
            return false;
        }

        if (AutoMapperAnalysisHelpers.AreTypesCompatible(sourceType, destinationType))
        {
            return false;
        }

        var conversion = compilation.ClassifyCommonConversion(sourceType, destinationType);
        if (conversion.Exists && (conversion.IsIdentity || conversion.IsImplicit))
        {
            return false;
        }

        // String to primitive conversions often need converters
        if (sourceType.SpecialType == SpecialType.System_String && IsPrimitiveType(destinationType))
        {
            return true;
        }

        // Primitive to string conversions
        if (IsPrimitiveType(sourceType) && destinationType.SpecialType == SpecialType.System_String)
        {
            return true;
        }

        // Check for incompatible class types which might need a converter
        if (sourceType.TypeKind == TypeKind.Class && destinationType.TypeKind == TypeKind.Class &&
            !SymbolEqualityComparer.Default.Equals(sourceType, destinationType))
        {
            // Exclude System.Object (handled by other rules like AM001)
            if (sourceType.SpecialType == SpecialType.System_Object ||
                destinationType.SpecialType == SpecialType.System_Object)
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static bool HasConvertUsingConfiguration(InvocationExpressionSyntax createMapInvocation,
        string propertyName)
    {
        if (HasGlobalConverterConfiguration(createMapInvocation))
        {
            return true;
        }

        foreach (InvocationExpressionSyntax? forMemberCall in AutoMapperAnalysisHelpers.GetForMemberCalls(createMapInvocation))
        {
            if (!IsForMemberConfigurationForProperty(forMemberCall, propertyName))
            {
                continue;
            }

            if (HasCompatibleForMemberConfiguration(forMemberCall))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasGlobalConverterConfiguration(InvocationExpressionSyntax createMapInvocation)
    {
        SyntaxNode? current = createMapInvocation;

        while (current?.Parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Expression == current &&
               memberAccess.Parent is InvocationExpressionSyntax parentInvocation)
        {
            if (memberAccess.Name.Identifier.ValueText is "ConvertUsing" or "ConstructUsing")
            {
                return true;
            }

            current = parentInvocation;
        }

        return false;
    }

    private static bool IsTypeCoveredByCreateMap(Compilation compilation, ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        if (sourceType == null || destinationType == null)
        {
            return false;
        }

        if (sourceType is INamedTypeSymbol sourceNamed && destinationType is INamedTypeSymbol destNamed)
        {
            var registry = CreateMapRegistry.FromCompilation(compilation);
            if (registry.Contains(sourceNamed, destNamed))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsForMemberConfigurationForProperty(InvocationExpressionSyntax forMemberInvocation,
        string propertyName)
    {
        if (forMemberInvocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        ExpressionSyntax propertySelector = forMemberInvocation.ArgumentList.Arguments[0].Expression;
        return TryGetSelectedMemberName(propertySelector, out string? selectedPropertyName) &&
               string.Equals(selectedPropertyName, propertyName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCompatibleForMemberConfiguration(InvocationExpressionSyntax forMemberInvocation)
    {
        if (forMemberInvocation.ArgumentList.Arguments.Count < 2)
        {
            return false;
        }

        ExpressionSyntax configExpression = forMemberInvocation.ArgumentList.Arguments[1].Expression;
        CSharpSyntaxNode? lambdaBody = configExpression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Body,
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda => parenthesizedLambda.Body,
            _ => null
        };

        if (lambdaBody == null)
        {
            return false;
        }

        if (lambdaBody is InvocationExpressionSyntax invocation)
        {
            return InvocationContainsSupportedConfiguration(invocation);
        }

        if (lambdaBody is BlockSyntax block)
        {
            return block.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(InvocationContainsSupportedConfiguration);
        }

        return false;
    }

    private static bool InvocationContainsSupportedConfiguration(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        if (memberAccess.Name.Identifier.ValueText is "ConvertUsing" or "MapFrom" or "Ignore")
        {
            return true;
        }

        return memberAccess.Expression is InvocationExpressionSyntax innerInvocation &&
               InvocationContainsSupportedConfiguration(innerInvocation);
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

    private static bool TryGetSelectedMemberName(ExpressionSyntax selectorExpression, out string? memberName)
    {
        memberName = null;

        CSharpSyntaxNode? body = selectorExpression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Body,
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda => parenthesizedLambda.Body,
            _ => null
        };

        if (body == null)
        {
            return false;
        }

        return TryExtractMemberName(body, out memberName);
    }

    private static bool TryExtractMemberName(CSharpSyntaxNode expression, out string? memberName)
    {
        memberName = null;

        switch (expression)
        {
            case MemberAccessExpressionSyntax memberAccess:
                memberName = memberAccess.Name.Identifier.ValueText;
                return true;
            case ConditionalAccessExpressionSyntax
                {
                    WhenNotNull: MemberBindingExpressionSyntax memberBinding
                }:
                memberName = memberBinding.Name.Identifier.ValueText;
                return true;
            case CastExpressionSyntax castExpression:
                return TryExtractMemberName(castExpression.Expression, out memberName);
            case ParenthesizedExpressionSyntax parenthesizedExpression:
                return TryExtractMemberName(parenthesizedExpression.Expression, out memberName);
            default:
                return false;
        }
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

    private static bool IsPrimitiveType(ITypeSymbol type)
    {
        return type.SpecialType != SpecialType.None ||
               type.Name == "String" ||
               type.Name == "DateTime" ||
               type.Name == "DateTimeOffset" ||
               type.Name == "TimeSpan" ||
               type.Name == "Guid" ||
               type.Name == "Decimal";
    }

    private static string GetConverterTypeName(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        return $"ITypeConverter<{AutoMapperAnalysisHelpers.GetTypeName(sourceType)}, {AutoMapperAnalysisHelpers.GetTypeName(destinationType)}>";
    }
}

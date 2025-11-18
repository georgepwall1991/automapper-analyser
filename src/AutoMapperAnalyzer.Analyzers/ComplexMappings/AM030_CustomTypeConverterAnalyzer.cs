using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Analyzers.ComplexMappings;

/// <summary>
/// Analyzer for AM030: Custom Type Converter issues in AutoMapper configurations.
/// Detects invalid converter implementations, missing ConvertUsing configurations,
/// and incorrect converter method signatures.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM030_CustomTypeConverterAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Diagnostic rule for invalid type converter implementations.
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
    /// Diagnostic rule for missing ConvertUsing configuration.
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
    /// Diagnostic rule for converter null handling issues.
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
    /// Diagnostic rule for unused type converters.
    /// </summary>
    public static readonly DiagnosticDescriptor UnusedTypeConverterRule = new(
        "AM030",
        "Type converter is defined but not used in mapping configuration",
        "Type converter '{0}' is defined but not used in any CreateMap configuration",
        "AutoMapper.Converters",
        DiagnosticSeverity.Info,
        true,
        "Unused type converters can be removed or should be configured in mapping.");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            InvalidConverterImplementationRule,
            MissingConvertUsingConfigurationRule,
            ConverterNullHandlingIssueRule,
            UnusedTypeConverterRule
        );

    /// <inheritdoc/>
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
            return;

        var createMapTypeArgs = AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, context.SemanticModel);
        if (createMapTypeArgs.sourceType == null || createMapTypeArgs.destType == null)
            return;

        AnalyzeForMissingConverterConfiguration(context, invocation, createMapTypeArgs.sourceType, createMapTypeArgs.destType);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
        
        if (classSymbol == null)
            return;

        AnalyzeTypeConverterImplementation(context, classDeclaration, classSymbol);
    }

    private static void AnalyzeForMissingConverterConfiguration(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        var sourceProperties = AutoMapperAnalysisHelpers.GetMappableProperties(sourceType, requireSetter: false);
        var destinationProperties = AutoMapperAnalysisHelpers.GetMappableProperties(destinationType, requireGetter: false);

        foreach (var sourceProperty in sourceProperties)
        {
            var destinationProperty = destinationProperties
                .FirstOrDefault(p => string.Equals(p.Name, sourceProperty.Name, StringComparison.OrdinalIgnoreCase));

            if (destinationProperty == null)
                continue;

            if (!RequiresTypeConverter(sourceProperty.Type, destinationProperty.Type))
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

            var properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("PropertyName", sourceProperty.Name);
            properties.Add("ConverterType", GetConverterTypeName(sourceProperty.Type, destinationProperty.Type));

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
        if (!ImplementsTypeConverterInterface(classSymbol))
            return;

        // Check if Convert method is properly implemented
        var convertMethod = GetConvertMethod(classSymbol);
        if (convertMethod == null)
        {
            var diagnostic = Diagnostic.Create(
                InvalidConverterImplementationRule,
                classDeclaration.Identifier.GetLocation(),
                classSymbol.Name,
                "TSource",
                "TDestination");

            context.ReportDiagnostic(diagnostic);
            return;
        }

        // Check null handling
        AnalyzeConverterNullHandling(context, classDeclaration, classSymbol, convertMethod);
    }

    private static void AnalyzeConverterNullHandling(
        SyntaxNodeAnalysisContext context,
        ClassDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        IMethodSymbol convertMethod)
    {
        var sourceTypeParameter = GetSourceTypeParameter(classSymbol);
        if (sourceTypeParameter == null || !IsNullableType(sourceTypeParameter))
            return;

        // Check if converter handles null values (this is a simplified check)
        var convertMethodDeclaration = classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == "Convert");

        if (convertMethodDeclaration != null && !ContainsNullCheck(convertMethodDeclaration))
        {
            var diagnostic = Diagnostic.Create(
                ConverterNullHandlingIssueRule,
                convertMethodDeclaration.Identifier.GetLocation(),
                classSymbol.Name,
                sourceTypeParameter.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }


    private static bool RequiresTypeConverter(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        // Check if types are fundamentally incompatible and would benefit from a converter
        if (SymbolEqualityComparer.Default.Equals(sourceType, destinationType))
            return false;

        // String to primitive conversions often need converters
        if (sourceType.SpecialType == SpecialType.System_String && IsPrimitiveType(destinationType))
            return true;

        // Primitive to string conversions
        if (IsPrimitiveType(sourceType) && destinationType.SpecialType == SpecialType.System_String)
            return true;

        // Check for incompatible class types which might need a converter
        if (sourceType.TypeKind == TypeKind.Class && destinationType.TypeKind == TypeKind.Class &&
            !SymbolEqualityComparer.Default.Equals(sourceType, destinationType))
        {
            // Exclude System.Object (handled by other rules like AM001)
            if (sourceType.SpecialType == SpecialType.System_Object || destinationType.SpecialType == SpecialType.System_Object)
                return false;

            return true;
        }

        return false;
    }

    private static bool HasConvertUsingConfiguration(InvocationExpressionSyntax createMapInvocation, string propertyName)
    {
        // Look for chained method calls starting from the CreateMap invocation
        var current = createMapInvocation.Parent;

        while (current != null)
        {
            if (current is MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax chainedInvocation } memberAccess)
            {
                // Check for ForMember calls that configure this specific property
                if (memberAccess.Name.Identifier.ValueText == "ForMember")
                {
                    if (IsForMemberConfigurationForProperty(chainedInvocation, propertyName))
                    {
                        // Check if this ForMember has ConvertUsing configuration
                        if (HasConvertUsingInForMember(chainedInvocation))
                            return true;
                    }
                }
                // Check for direct ConvertUsing calls (global converters)
                else if (memberAccess.Name.Identifier.ValueText == "ConvertUsing")
                {
                    return true;
                }
                
                current = chainedInvocation.Parent;
            }
            else
            {
                current = current.Parent;
            }
        }

        return false;
    }

    private static bool IsTypeCoveredByCreateMap(Compilation compilation, ITypeSymbol sourceType, ITypeSymbol destinationType)
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

    private static bool IsForMemberConfigurationForProperty(InvocationExpressionSyntax forMemberInvocation, string propertyName)
    {
        // Check if this ForMember call is configuring the specific property
        var arguments = forMemberInvocation.ArgumentList?.Arguments;
        if (arguments?.Count > 0)
        {
            // Simple check - look for property name in the first argument
            var firstArg = arguments.Value[0];
            return firstArg.ToString().Contains(propertyName);
        }
        return false;
    }

    private static bool HasConvertUsingInForMember(InvocationExpressionSyntax forMemberInvocation)
    {
        // Check if the ForMember configuration contains ConvertUsing or MapFrom (both handle conversion)
        var arguments = forMemberInvocation.ArgumentList?.Arguments;
        if (arguments?.Count > 1)
        {
            // Look for ConvertUsing or MapFrom in the configuration lambda (second argument)
            var configArg = arguments.Value[1];
            var configText = configArg.ToString();
            return configText.Contains("ConvertUsing") || configText.Contains("MapFrom");
        }
        return false;
    }

    private static bool ImplementsTypeConverterInterface(INamedTypeSymbol classSymbol)
    {
        return classSymbol.AllInterfaces.Any(i => 
            i.Name == "ITypeConverter" && 
            i.ContainingNamespace?.Name == "AutoMapper");
    }

    private static IMethodSymbol? GetConvertMethod(INamedTypeSymbol classSymbol)
    {
        return classSymbol.GetMembers("Convert")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.Parameters.Length >= 2); // Convert method should have source and context parameters
    }

    private static ITypeSymbol? GetSourceTypeParameter(INamedTypeSymbol classSymbol)
    {
        var typeConverterInterface = classSymbol.AllInterfaces
            .FirstOrDefault(i => i.Name == "ITypeConverter" && i.TypeArguments.Length == 2);

        return typeConverterInterface?.TypeArguments[0];
    }

    private static bool IsNullableType(ITypeSymbol type)
    {
        AutoMapperAnalysisHelpers.IsNullableType(type, out _);
        return type.CanBeReferencedByName && 
               (type.NullableAnnotation == NullableAnnotation.Annotated ||
                (type is INamedTypeSymbol namedType && 
                 namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T));
    }

    private static bool ContainsNullCheck(MethodDeclarationSyntax method)
    {
        // Simplified check for null comparisons in the method body
        return method.DescendantNodes()
            .OfType<BinaryExpressionSyntax>()
            .Any(binary => binary.IsKind(SyntaxKind.EqualsExpression) || 
                          binary.IsKind(SyntaxKind.NotEqualsExpression));
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
        return $"ITypeConverter<{GetTypeName(sourceType)}, {GetTypeName(destinationType)}>";
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
}

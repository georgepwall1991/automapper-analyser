using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace AutoMapperAnalyzer.Analyzers.Configuration;

/// <summary>
///     Analyzer that detects Map/ProjectTo call sites whose source/destination pair has no
///     reachable CreateMap registration anywhere in the compilation. Such calls compile cleanly
///     and then throw <c>AutoMapperMappingException</c> ("Missing type map configuration or
///     unsupported mapping") the first time they execute — unless the pair is registered in a
///     profile that lives outside this compilation, which the rule cannot see.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AM060_UnregisteredTypeMapAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     Diagnostic descriptor for mapping calls without a reachable type map registration.
    /// </summary>
    public static readonly DiagnosticDescriptor UnregisteredTypeMapRule = new(
        "AM060",
        "Unregistered type map at mapping call site",
        "No CreateMap<{0}, {1}> registration was found in this compilation; this call throws AutoMapperMappingException at runtime unless the pair is registered in a profile outside this compilation",
        "AutoMapper.Configuration",
        DiagnosticSeverity.Warning,
        true,
        "AutoMapper resolves type maps at runtime. A Map or ProjectTo call whose source/destination pair "
        + "has no reachable CreateMap registration throws AutoMapperMappingException on first use, unless "
        + "the map is contributed by a profile assembly this compilation cannot see. Register the map in a "
        + "scanned Profile, fix swapped type arguments, or suppress AM060 for externally configured projects.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [UnregisteredTypeMapRule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            CreateMapRegistry registry = CreateMapRegistry.FromCompilation(compilationContext.Compilation);

            // Projects with no registrations at all normally consume maps configured elsewhere
            // (another assembly or composition root); absence cannot be proven there. Unresolved
            // registrations (open-generic typeof forms, generic helper methods) can cover any
            // pair, so fail closed too.
            if (registry.IsEmpty || registry.HasUnresolvedCreateMapRegistrations)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                ctx => AnalyzeInvocation(ctx, registry),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, CreateMapRegistry registry)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!TryGetReportedPair(invocation, context.SemanticModel, out ITypeSymbol? source, out ITypeSymbol? destination))
        {
            return;
        }

        if (HasReachableRegistration(registry, source, destination))
        {
            return;
        }

        string sourceName = AutoMapperAnalysisHelpers.GetTypeNameWithoutNullability(source);
        string destinationName = AutoMapperAnalysisHelpers.GetTypeNameWithoutNullability(destination);

        ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
            .Add("SourceTypeName", sourceName)
            .Add("DestinationTypeName", destinationName);

        context.ReportDiagnostic(Diagnostic.Create(
            UnregisteredTypeMapRule,
            GetNameLocation(invocation),
            properties,
            sourceName,
            destinationName));
    }

    /// <summary>
    ///     Resolves the effective (source, destination) pair for a semantic AutoMapper
    ///     <c>Map</c>/<c>ProjectTo</c> invocation and applies the conservative reporting gates.
    ///     Shared with the code fix provider so fixes target exactly the reported pair.
    /// </summary>
    internal static bool TryGetReportedPair(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out ITypeSymbol source,
        out ITypeSymbol destination)
    {
        source = null!;
        destination = null!;

        IMethodSymbol? method = ResolveMappingMethod(invocation, semanticModel);
        if (method == null ||
            !TryGetRawPair(method, invocation, semanticModel, out ITypeSymbol? rawSource, out ITypeSymbol? rawDestination))
        {
            return false;
        }

        if (rawSource == null || rawDestination == null)
        {
            return false;
        }

        rawSource = AutoMapperAnalysisHelpers.GetUnderlyingType(rawSource);
        rawDestination = AutoMapperAnalysisHelpers.GetUnderlyingType(rawDestination);

        if (rawSource.TypeKind == TypeKind.Error || rawDestination.TypeKind == TypeKind.Error)
        {
            return false;
        }

        if (ContainsTypeParameter(rawSource) || ContainsTypeParameter(rawDestination))
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(rawSource, rawDestination))
        {
            return false;
        }

        // AutoMapper's built-in mappers handle simple/enum pairs (primitive conversions, enum
        // mapping) and dictionary pairs without registration — but only when BOTH endpoints are
        // simple (or both dictionaries). One-sided pairs such as complex-to-enum still require
        // an explicit map.
        bool sourceIsSimple = IsSimpleMappingType(rawSource);
        bool destinationIsSimple = IsSimpleMappingType(rawDestination);
        if (sourceIsSimple && destinationIsSimple)
        {
            return false;
        }

        if (ImplementsDictionary(rawSource) && ImplementsDictionary(rawDestination))
        {
            return false;
        }

        // Assignable pairs (Derived -> Base, class -> implemented interface, element-wise
        // assignable collections) are served by AutoMapper's assignable mapper without a
        // registration.
        if (HasBuiltInConversion(semanticModel.Compilation, rawSource, rawDestination))
        {
            return false;
        }

        bool sourceIsCollection = AutoMapperAnalysisHelpers.IsCollectionType(rawSource);
        bool destinationIsCollection = AutoMapperAnalysisHelpers.IsCollectionType(rawDestination);
        if (sourceIsCollection != destinationIsCollection)
        {
            // Container-shape mismatches belong to AM003/AM021.
            return false;
        }

        if (sourceIsCollection)
        {
            // Collection maps succeed when the element pair is registered; report the missing
            // element map rather than the container pair, because that is the registration the
            // developer must add.
            ITypeSymbol? sourceCollectionElement = AutoMapperAnalysisHelpers.GetCollectionElementType(rawSource);
            ITypeSymbol? destinationCollectionElement =
                AutoMapperAnalysisHelpers.GetCollectionElementType(rawDestination);
            if (sourceCollectionElement == null || destinationCollectionElement == null)
            {
                return false;
            }

            ITypeSymbol sourceElement = AutoMapperAnalysisHelpers.GetUnderlyingType(sourceCollectionElement);
            ITypeSymbol destinationElement =
                AutoMapperAnalysisHelpers.GetUnderlyingType(destinationCollectionElement);

            if (sourceElement.TypeKind == TypeKind.Error || destinationElement.TypeKind == TypeKind.Error)
            {
                return false;
            }

            if (ContainsTypeParameter(sourceElement) || ContainsTypeParameter(destinationElement))
            {
                return false;
            }

            if (SymbolEqualityComparer.Default.Equals(sourceElement, destinationElement))
            {
                return false;
            }

            if (IsSimpleMappingType(sourceElement) && IsSimpleMappingType(destinationElement))
            {
                return false;
            }

            if (HasBuiltInConversion(semanticModel.Compilation, sourceElement, destinationElement))
            {
                return false;
            }

            source = sourceElement;
            destination = destinationElement;
            return true;
        }

        source = rawSource;
        destination = rawDestination;
        return true;
    }

    private static IMethodSymbol? ResolveMappingMethod(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);
        IMethodSymbol? method = symbolInfo.Symbol as IMethodSymbol
                                ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        if (method == null)
        {
            return null;
        }

        if (method.Name is not ("Map" or "ProjectTo"))
        {
            return null;
        }

        if (!IsAutoMapperType(method.ContainingType))
        {
            return null;
        }

        // Map must be an IMapper/IMapperBase instance method or a legacy Mapper static method;
        // anything else named Map inside the AutoMapper namespace (custom extensions, lookalikes)
        // stays outside the rule.
        if (method.Name == "Map" &&
            method.ContainingType?.Name is not ("IMapper" or "IMapperBase" or "Mapper"))
        {
            return null;
        }

        return method;
    }

    private static bool IsAutoMapperType(INamedTypeSymbol? type)
    {
        string? namespaceName = type?.ContainingNamespace?.ToDisplayString();
        return namespaceName == "AutoMapper" ||
               (namespaceName?.StartsWith("AutoMapper.", StringComparison.Ordinal) ?? false);
    }

    private static bool TryGetRawPair(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out ITypeSymbol? source,
        out ITypeSymbol? destination)
    {
        source = null;
        destination = null;

        // Operation-based argument binding: parameters are matched by name, so reordered named
        // arguments cannot swap the pair, and reduced extension calls expose the receiver as
        // Instance rather than as an argument.
        if (semanticModel.GetOperation(invocation) is not IInvocationOperation invocationOperation)
        {
            return false;
        }

        if (method.Name == "Map")
        {
            if (method.TypeArguments.Length == 2)
            {
                source = method.TypeArguments[0];
                destination = method.TypeArguments[1];
                return true;
            }

            if (method.TypeArguments.Length == 1)
            {
                // Map<TDestination>(object source): the static argument type is the only available
                // evidence for the source type.
                destination = method.TypeArguments[0];
                source = FindArgumentType(invocationOperation, "source");
                return source != null;
            }

            // Non-generic Map(object, Type, Type) forms: only typeof(...) operations are
            // trustworthy; any other Type-typed argument aborts the pair extraction.
            foreach (IArgumentOperation argument in invocationOperation.Arguments)
            {
                if (argument.Parameter?.Name is not ("sourceType" or "destinationType"))
                {
                    continue;
                }

                if (argument.Value is not ITypeOfOperation typeOfOperation)
                {
                    return false;
                }

                if (argument.Parameter.Name == "sourceType")
                {
                    source = typeOfOperation.TypeOperand;
                }
                else
                {
                    destination = typeOfOperation.TypeOperand;
                }
            }

            return source != null && destination != null;
        }

        // ProjectTo<TDestination>(queryable, ...): destination is the single type argument; source
        // is the queryable element type. The queryable is the "source" parameter for static and
        // instance forms, and the Instance (receiver) for reduced extension calls; a queryable-typed
        // argument scan covers parameter-name variation across AutoMapper versions.
        if (method.TypeArguments.Length != 1)
        {
            return false;
        }

        ITypeSymbol? queryableType = FindQueryableType(invocationOperation);
        if (queryableType == null)
        {
            return false;
        }

        source = AutoMapperAnalysisHelpers.GetCollectionElementType(queryableType);
        destination = method.TypeArguments[0];
        return source != null;
    }

    private static ITypeSymbol? FindArgumentType(IInvocationOperation invocationOperation, string parameterName)
    {
        foreach (IArgumentOperation argument in invocationOperation.Arguments)
        {
            if (argument.Parameter?.Name == parameterName)
            {
                // Argument values carry the implicit conversion to the parameter type
                // (Map<TDestination>(object source) would otherwise always yield "object");
                // explicit casts stay visible because they express deliberate intent.
                return UnwrapImplicitConversion(argument.Value).Type;
            }
        }

        return null;
    }

    private static IOperation UnwrapImplicitConversion(IOperation operation)
    {
        while (operation is IConversionOperation { IsImplicit: true } conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    private static ITypeSymbol? FindQueryableType(IInvocationOperation invocationOperation)
    {
        ITypeSymbol? namedSource = FindArgumentType(invocationOperation, "source");
        if (namedSource != null && AutoMapperAnalysisHelpers.GetCollectionElementType(namedSource) != null)
        {
            return namedSource;
        }

        ITypeSymbol? instanceType = invocationOperation.Instance?.Type;
        if (instanceType != null && AutoMapperAnalysisHelpers.GetCollectionElementType(instanceType) != null)
        {
            return instanceType;
        }

        foreach (IArgumentOperation argument in invocationOperation.Arguments)
        {
            ITypeSymbol? argumentType = UnwrapImplicitConversion(argument.Value).Type;
            if (argumentType != null && AutoMapperAnalysisHelpers.GetCollectionElementType(argumentType) != null)
            {
                return argumentType;
            }
        }

        return null;
    }

    private static bool HasReachableRegistration(
        CreateMapRegistry registry,
        ITypeSymbol source,
        ITypeSymbol destination)
    {
        if (registry.Contains(source, destination))
        {
            return true;
        }

        // AutoMapper resolves maps by runtime type, so a registration for any base type or
        // interface of the static source type can still serve the call.
        for (ITypeSymbol? baseType = source.BaseType;
             baseType != null && baseType.SpecialType != SpecialType.System_Object;
             baseType = baseType.BaseType)
        {
            if (registry.Contains(baseType, destination))
            {
                return true;
            }
        }

        foreach (ITypeSymbol? interfaceType in source.AllInterfaces)
        {
            if (registry.Contains(interfaceType, destination))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasBuiltInConversion(Compilation compilation, ITypeSymbol source, ITypeSymbol destination)
    {
        // AutoMapper's assignable/convert mappers serve any compiler-known implicit non-user
        // conversion (reference upcasts, boxing, primitive widening) without a registration.
        Conversion conversion = compilation.ClassifyConversion(source, destination);
        return conversion.Exists &&
               !conversion.IsUserDefined &&
               (conversion.IsIdentity || conversion.IsImplicit);
    }

    private static bool IsSimpleMappingType(ITypeSymbol type)
    {
        return AutoMapperAnalysisHelpers.IsBuiltInType(type) || type.TypeKind == TypeKind.Enum;
    }

    private static bool ContainsTypeParameter(ITypeSymbol type)
    {
        return type.TypeKind == TypeKind.TypeParameter ||
               (type is INamedTypeSymbol namedType &&
                (namedType.IsUnboundGenericType || namedType.TypeArguments.Any(ContainsTypeParameter))) ||
               (type is IArrayTypeSymbol arrayType && ContainsTypeParameter(arrayType.ElementType));
    }

    private static bool ImplementsDictionary(ITypeSymbol type)
    {
        return IsDictionaryType(type) || type.AllInterfaces.Any(IsDictionaryType);
    }

    private static bool IsDictionaryType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        INamedTypeSymbol definition = namedType.OriginalDefinition;
        string? namespaceName = definition.ContainingNamespace?.ToDisplayString();

        // IReadOnlyDictionary<,> does not implement IDictionary<,> but AutoMapper maps it natively.
        if (namespaceName == "System.Collections.Generic" &&
            definition.Name is "IDictionary" or "IReadOnlyDictionary" &&
            definition.TypeArguments.Length == 2)
        {
            return true;
        }

        return namespaceName == "System.Collections" &&
               definition.Name == "IDictionary" &&
               definition.TypeArguments.Length == 0;
    }

    private static Location GetNameLocation(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.GetLocation(),
            GenericNameSyntax genericName => genericName.GetLocation(),
            IdentifierNameSyntax identifierName => identifierName.GetLocation(),
            _ => invocation.GetLocation()
        };
    }
}

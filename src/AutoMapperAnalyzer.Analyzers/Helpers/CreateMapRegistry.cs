using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Helpers;

/// <summary>
/// Caches resolved AutoMapper CreateMap registrations for a compilation.
/// </summary>
internal sealed class CreateMapRegistry
{
    private readonly ImmutableArray<(ITypeSymbol Source, ITypeSymbol Destination)> _mappings;

    private CreateMapRegistry(ImmutableArray<(ITypeSymbol Source, ITypeSymbol Destination)> mappings)
    {
        _mappings = mappings;
    }

    public bool Contains(ITypeSymbol? source, ITypeSymbol? destination)
    {
        if (source == null || destination == null)
        {
            return false;
        }

        foreach (var mapping in _mappings)
        {
            if (SymbolEqualityComparer.Default.Equals(mapping.Source, source) &&
                SymbolEqualityComparer.Default.Equals(mapping.Destination, destination))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a CreateMap exists for the element types of collections.
    /// Unwraps collection types (IEnumerable&lt;T&gt;, List&lt;T&gt;, etc.) to check element mappings.
    /// </summary>
    public bool ContainsElementMapping(ITypeSymbol? sourceCollection, ITypeSymbol? destinationCollection)
    {
        if (sourceCollection == null || destinationCollection == null)
        {
            return false;
        }

        var sourceElementType = AutoMapperAnalysisHelpers.GetCollectionElementType(sourceCollection);
        var destElementType = AutoMapperAnalysisHelpers.GetCollectionElementType(destinationCollection);

        if (sourceElementType == null || destElementType == null)
        {
            return false;
        }

        // Check if a mapping exists for the element types
        return Contains(sourceElementType, destElementType);
    }

    public static CreateMapRegistry Build(Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<(ITypeSymbol, ITypeSymbol)>();

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var root = syntaxTree.GetRoot();
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!AutoMapperAnalysisHelpers.IsCreateMapInvocation(invocation, semanticModel))
                {
                    continue;
                }

                var (sourceType, destType) = AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
                if (sourceType != null && destType != null)
                {
                    builder.Add((sourceType, destType));
                }
            }
        }

        return new CreateMapRegistry(builder.ToImmutable());
    }

    private static readonly ConditionalWeakTable<Compilation, CreateMapRegistry> Cache = new();

    public static CreateMapRegistry FromCompilation(Compilation compilation)
    {
        return Cache.GetValue(compilation, Build);
    }
}

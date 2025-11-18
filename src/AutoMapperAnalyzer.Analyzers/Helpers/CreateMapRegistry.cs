using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Analyzers.Helpers;

/// <summary>
///     Caches resolved AutoMapper CreateMap registrations for a compilation.
/// </summary>
internal sealed class CreateMapRegistry
{
    private static readonly ConditionalWeakTable<Compilation, CreateMapRegistry> Cache = new();

    private readonly Dictionary<InvocationExpressionSyntax, (string Source, string Dest, Location Location)>
        _duplicates;

    private readonly ImmutableArray<MappingInfo> _mappings;

    private CreateMapRegistry(
        ImmutableArray<MappingInfo> mappings,
        Dictionary<InvocationExpressionSyntax, (string Source, string Dest, Location Location)> duplicates)
    {
        _mappings = mappings;
        _duplicates = duplicates;
    }

    public bool Contains(ITypeSymbol? source, ITypeSymbol? destination)
    {
        if (source == null || destination == null)
        {
            return false;
        }

        foreach (MappingInfo mapping in _mappings)
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
    ///     Checks if a CreateMap exists for the element types of collections.
    ///     Unwraps collection types (IEnumerable&lt;T&gt;, List&lt;T&gt;, etc.) to check element mappings.
    /// </summary>
    public bool ContainsElementMapping(ITypeSymbol? sourceCollection, ITypeSymbol? destinationCollection)
    {
        if (sourceCollection == null || destinationCollection == null)
        {
            return false;
        }

        ITypeSymbol? sourceElementType = AutoMapperAnalysisHelpers.GetCollectionElementType(sourceCollection);
        ITypeSymbol? destElementType = AutoMapperAnalysisHelpers.GetCollectionElementType(destinationCollection);

        if (sourceElementType == null || destElementType == null)
        {
            return false;
        }

        // Check if a mapping exists for the element types
        return Contains(sourceElementType, destElementType);
    }

    /// <summary>
    ///     Gets the duplicate mappings identified during registry build.
    /// </summary>
    public Dictionary<InvocationExpressionSyntax, (string Source, string Dest, Location Location)>
        GetDuplicateMappings()
    {
        return _duplicates;
    }

    public static CreateMapRegistry Build(Compilation compilation)
    {
        var mappings = new List<MappingInfo>();

        foreach (SyntaxTree? syntaxTree in compilation.SyntaxTrees)
        {
            // Fast path: Check if the file content contains "CreateMap" before parsing
            if (!syntaxTree.TryGetText(out SourceText? text) || !text.ToString().Contains("CreateMap"))
            {
                continue;
            }

            SyntaxNode root = syntaxTree.GetRoot();
            SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);

            foreach (InvocationExpressionSyntax? invocation in root.DescendantNodes()
                         .OfType<InvocationExpressionSyntax>())
            {
                if (!AutoMapperAnalysisHelpers.IsCreateMapInvocation(invocation, semanticModel))
                {
                    continue;
                }

                (ITypeSymbol? sourceType, ITypeSymbol? destType) =
                    AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
                if (sourceType != null && destType != null)
                {
                    mappings.Add(new MappingInfo
                    {
                        Source = sourceType,
                        Destination = destType,
                        Location = invocation.GetLocation(),
                        Node = invocation,
                        IsReverseMap = false
                    });

                    // Check for ReverseMap()
                    if (AutoMapperAnalysisHelpers.HasReverseMap(invocation))
                    {
                        InvocationExpressionSyntax? reverseMapInvocation =
                            AutoMapperAnalysisHelpers.GetReverseMapInvocation(invocation);
                        if (reverseMapInvocation != null)
                        {
                            Location loc;
                            if (reverseMapInvocation.Expression is MemberAccessExpressionSyntax ma)
                            {
                                loc = ma.Name.GetLocation();
                            }
                            else
                            {
                                loc = reverseMapInvocation.GetLocation();
                            }

                            mappings.Add(new MappingInfo
                            {
                                Source = destType,
                                Destination = sourceType,
                                Location = loc,
                                Node = reverseMapInvocation,
                                IsReverseMap = true
                            });
                        }
                    }
                }
            }
        }

        // Identify duplicates
        var duplicates = new Dictionary<InvocationExpressionSyntax, (string Source, string Dest, Location Location)>();
        IEnumerable<IGrouping<(ITypeSymbol Source, ITypeSymbol Destination), MappingInfo>> groups =
            mappings.GroupBy(m => (m.Source, m.Destination), new MappingComparer());

        foreach (IGrouping<(ITypeSymbol Source, ITypeSymbol Destination), MappingInfo>? group in groups)
        {
            if (group.Count() > 1)
            {
                // Sort by location to have deterministic reporting
                var sorted = group.OrderBy(x => x.Location.SourceTree?.FilePath)
                    .ThenBy(x => x.Location.SourceSpan.Start)
                    .ToList();

                // Report on all except the first one
                for (int i = 1; i < sorted.Count; i++)
                {
                    MappingInfo duplicate = sorted[i];
                    duplicates[duplicate.Node] = (
                        Source: duplicate.Source.Name,
                        Dest: duplicate.Destination.Name,
                        duplicate.Location
                    );
                }
            }
        }

        return new CreateMapRegistry(mappings.ToImmutableArray(), duplicates);
    }

    public static CreateMapRegistry FromCompilation(Compilation compilation)
    {
        return Cache.GetValue(compilation, Build);
    }

    internal struct MappingInfo
    {
        public ITypeSymbol Source;
        public ITypeSymbol Destination;
        public Location Location;
        public InvocationExpressionSyntax Node;
        public bool IsReverseMap;
    }

    private class MappingComparer : IEqualityComparer<(ITypeSymbol Source, ITypeSymbol Destination)>
    {
        public bool Equals((ITypeSymbol Source, ITypeSymbol Destination) x,
            (ITypeSymbol Source, ITypeSymbol Destination) y)
        {
            return SymbolEqualityComparer.Default.Equals(x.Source, y.Source) &&
                   SymbolEqualityComparer.Default.Equals(x.Destination, y.Destination);
        }

        public int GetHashCode((ITypeSymbol Source, ITypeSymbol Destination) obj)
        {
            int h1 = SymbolEqualityComparer.Default.GetHashCode(obj.Source);
            int h2 = SymbolEqualityComparer.Default.GetHashCode(obj.Destination);
            return h1 ^ h2;
        }
    }
}

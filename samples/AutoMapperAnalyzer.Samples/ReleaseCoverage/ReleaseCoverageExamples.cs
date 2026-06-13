using AutoMapper;
using System;
using System.Collections.Generic;

namespace AutoMapperAnalyzer.Samples.ReleaseCoverage;

/// <summary>
///     Minimal intentional diagnostics that keep generated sample coverage aligned with the shipped rule catalog.
/// </summary>
public sealed class ReleaseCoverageProfile : Profile
{
    public ReleaseCoverageProfile()
    {
        CreateMap<QueueContainerSource, QueueContainerDestination>();
        CreateMap<NestedOwnerSource, NestedOwnerDestination>();
        CreateMap<ItemCollectionSource, ItemCollectionDestination>();
        CreateMap<SelfReferencingSource, SelfReferencingDestination>();
        CreateMap<IndirectSourceA, IndirectDestinationA>();
        CreateMap<IndirectSourceB, IndirectDestinationB>();
        CreateMap<string?, Guid>().ConvertUsing<UnsafeStringGuidConverter>();
        CreateMap<RedundantSource, RedundantDestination>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name));
    }
}

public sealed class QueueContainerSource
{
    public Queue<string> Items { get; set; } = new();
}

public sealed class QueueContainerDestination
{
    public List<string> Items { get; set; } = new();
}

public sealed class NestedOwnerSource
{
    public NestedAddress Address { get; set; } = new();
}

public sealed class NestedOwnerDestination
{
    public NestedAddressDto Address { get; set; } = new();
}

public sealed class NestedAddress
{
    public string Street { get; set; } = string.Empty;
}

public sealed class NestedAddressDto
{
    public string Street { get; set; } = string.Empty;
}

public sealed class ItemCollectionSource
{
    public List<SourceItem> Items { get; set; } = new();
}

public sealed class ItemCollectionDestination
{
    public List<DestinationItem> Items { get; set; } = new();
}

public sealed class SourceItem
{
    public string Value { get; set; } = string.Empty;
}

public sealed class DestinationItem
{
    public string Value { get; set; } = string.Empty;
}

public sealed class SelfReferencingSource
{
    public SelfReferencingSource? Parent { get; set; }
}

public sealed class SelfReferencingDestination
{
    public SelfReferencingDestination? Parent { get; set; }
}

public sealed class IndirectSourceA
{
    public IndirectSourceB Next { get; set; } = new();
}

public sealed class IndirectSourceB
{
    public IndirectSourceA Next { get; set; } = new();
}

public sealed class IndirectDestinationA
{
    public IndirectDestinationB Next { get; set; } = new();
}

public sealed class IndirectDestinationB
{
    public IndirectDestinationA Next { get; set; } = new();
}

public sealed class UnsafeStringGuidConverter : ITypeConverter<string?, Guid>
{
    public Guid Convert(string? source, Guid destination, ResolutionContext context)
    {
        return Guid.Parse(source);
    }
}

public sealed class RedundantSource
{
    public string Name { get; set; } = string.Empty;
}

public sealed class RedundantDestination
{
    public string Name { get; set; } = string.Empty;
}

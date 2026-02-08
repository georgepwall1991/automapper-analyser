using AutoMapper;

namespace AutoMapperAnalyzer.Samples.UnmappedDestination;

/// <summary>
///     Examples of unmapped destination property issues that AutoMapper analyzer will detect (AM006)
/// </summary>
public class UnmappedDestinationExamples
{
    /// <summary>
    ///     AM006: Unmapped Destination Property
    ///     Destination has a property with no matching source property and no explicit mapping
    /// </summary>
    public void UnmappedDestinationPropertyExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ❌ AM006: Destination property 'ExtraProperty' is not mapped from source 'AM006Source'
            cfg.CreateMap<AM006Source, AM006Destination>();
        });

        var mapper = config.CreateMapper();
        var source = new AM006Source { Id = 1, Name = "Test" };
        var destination = mapper.Map<AM006Destination>(source);
        Console.WriteLine($"Mapped: Id={destination.Id}, Name={destination.Name}, Extra={destination.ExtraProperty}");
        Console.WriteLine("❌ ExtraProperty was never mapped - will be default value!");
    }
}

/// <summary>
///     Examples of CORRECT patterns for handling unmapped destination properties (for comparison)
/// </summary>
public class CorrectUnmappedDestinationExamples
{
    /// <summary>
    ///     Fix 1: Ignore the unmapped destination property
    /// </summary>
    public void IgnoreUnmappedPropertyExample()
    {
#pragma warning disable AM041
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Correct: Explicitly ignore the property
            cfg.CreateMap<AM006Source, AM006Destination>()
                .ForMember(dest => dest.ExtraProperty, opt => opt.Ignore());
        });
#pragma warning restore AM041

        var mapper = config.CreateMapper();
        var source = new AM006Source { Id = 1, Name = "Test" };
        var destination = mapper.Map<AM006Destination>(source);
        Console.WriteLine($"✅ Correctly mapped with Ignore: Id={destination.Id}, Name={destination.Name}");
    }

    /// <summary>
    ///     Fix 2: Map from a source expression
    /// </summary>
    public void MapFromExpressionExample()
    {
#pragma warning disable AM041
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Correct: Explicitly map from a source expression
            cfg.CreateMap<AM006Source, AM006Destination>()
                .ForMember(dest => dest.ExtraProperty, opt => opt.MapFrom(src => $"Extra: {src.Name}"));
        });
#pragma warning restore AM041

        var mapper = config.CreateMapper();
        var source = new AM006Source { Id = 1, Name = "Test" };
        var destination = mapper.Map<AM006Destination>(source);
        Console.WriteLine($"✅ Correctly mapped with MapFrom: Extra={destination.ExtraProperty}");
    }
}

// Supporting classes with unique names to avoid any potential conflicts

public class AM006Source
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class AM006Destination
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ExtraProperty { get; set; } = string.Empty; // Has no matching source
}

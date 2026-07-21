using AutoMapper;

namespace AutoMapperAnalyzer.Samples.Configuration;

// =============================================================================
// AM060: Unregistered Type Map at Mapping Call Site
// =============================================================================
// Map/ProjectTo calls compile cleanly even when no CreateMap registration
// exists for the source/destination pair - and then throw
// AutoMapperMappingException ("Missing type map configuration or unsupported
// mapping") the first time they run. AM060 surfaces that runtime failure at
// compile time (warning severity; escalate in closed-world solutions).

public class AM060Order
{
    public int Id { get; set; }
    public string Customer { get; set; } = string.Empty;
}

public class AM060OrderDto
{
    public int Id { get; set; }
    public string Customer { get; set; } = string.Empty;
}

public class AM060RegisteredDto
{
    public int Id { get; set; }
}

public class AM060Profile : Profile
{
    public AM060Profile()
    {
        // A registered map keeps the project's configuration non-empty...
        CreateMap<AM060Order, AM060RegisteredDto>();
    }
}

public class AM060OrderService
{
    private readonly IMapper _mapper;

    public AM060OrderService(IMapper mapper)
    {
        _mapper = mapper;
    }

    public AM060OrderDto GetDto(AM060Order order)
    {
        // ❌ AM060: No CreateMap<AM060Order, AM060OrderDto> exists anywhere in
        // this compilation, so this call throws at runtime.
        return _mapper.Map<AM060OrderDto>(order);
    }

    public AM060RegisteredDto GetRegisteredDto(AM060Order order)
    {
        // ✅ Registered above - no diagnostic.
        return _mapper.Map<AM060RegisteredDto>(order);
    }
}

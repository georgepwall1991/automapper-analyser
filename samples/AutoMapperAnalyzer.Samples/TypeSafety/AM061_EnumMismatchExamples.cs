using AutoMapper;

namespace AutoMapperAnalyzer.Samples.TypeSafety;

// =============================================================================
// AM061: Enum Member Mismatch in Mapping
// =============================================================================
// AutoMapper maps enums by numeric value by default. When the source and
// destination enums do not align by name AND value, the mapping silently
// produces wrong data - no exception, no warning, just corruption.

public enum AM061SourceStatus
{
    Active = 1,
    Inactive = 2
}

// Same names, swapped values: value-based mapping turns Active into Inactive.
public enum AM061DestinationStatus
{
    Inactive = 1,
    Active = 2
}

public class AM061Account
{
    public AM061SourceStatus Status { get; set; }
}

public class AM061AccountDto
{
    public AM061DestinationStatus Status { get; set; }
}

public class AM061EnumProfile : Profile
{
    public AM061EnumProfile()
    {
        // ❌ AM061 (twice): source member 'Active' (1) maps by value to
        // destination member 'Inactive', and 'Inactive' (2) maps to 'Active'.
        CreateMap<AM061Account, AM061AccountDto>();
    }
}

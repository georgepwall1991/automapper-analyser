using AutoMapper;

namespace AutoMapperAnalyzer.Samples;

// Test classes for AM021 - Collection Element Mismatch
public class PersonSource
{
    public List<string> PhoneNumbers { get; set; } // string elements
    public string[] Tags { get; set; }
}

public class PersonDestination
{
    public List<int> PhoneNumbers { get; set; } // int elements - should trigger AM021
    public HashSet<string> Tags { get; set; } // Different collection type but same element type
}

// Test classes for AM022 - Infinite Recursion
public class TreeNode
{
    public string Name { get; set; }
    public TreeNode Parent { get; set; } // Self-reference - should trigger AM022
    public List<TreeNode> Children { get; set; } // Collection of self - should trigger AM022
}

public class TreeNodeDto
{
    public string Name { get; set; }
    public TreeNodeDto Parent { get; set; } // Self-reference - should trigger AM022
    public List<TreeNodeDto> Children { get; set; } // Collection of self - should trigger AM022
}

// Test classes for AM020 - Nested Object Mapping (already implemented)
public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}

public class AddressDto
{
    public string Street { get; set; }
    public string City { get; set; }
    public string Country { get; set; } // Extra property
}

public class CompanySource
{
    public Address Headquarters { get; set; } // Nested object
}

public class CompanyDestination
{
    public AddressDto Headquarters { get; set; } // Different nested object type
}

// Test classes for AM030 - Custom Type Converter Issues
public class ProductSource
{
    public string Price { get; set; } = "19.99"; // String price
    public string CreatedDate { get; set; } = "2023-01-01"; // String date
    public string? Description { get; set; } // Nullable string
}

public class ProductDestination
{
    public decimal Price { get; set; } // Decimal price - needs converter
    public DateTime CreatedDate { get; set; } // DateTime - needs converter
    public string Description { get; set; } // Non-nullable string
}

// Example converter that doesn't handle nulls properly
public class UnsafeStringToDateTimeConverter : ITypeConverter<string?, DateTime>
{
    public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
    {
        // No null check - should trigger AM030 null handling warning
        return DateTime.Parse(source);
    }
}

// Example converter that handles nulls properly
public class SafeStringToDateTimeConverter : ITypeConverter<string?, DateTime>
{
    public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
    {
        if (source == null)
            return DateTime.MinValue;

        return DateTime.TryParse(source, out var result) ? result : DateTime.MinValue;
    }
}

public class TestProfile : Profile
{
    public TestProfile()
    {
        // Should trigger AM021: Collection element type mismatch (string -> int)
        CreateMap<PersonSource, PersonDestination>();

        // Should trigger AM022: Infinite recursion risk due to circular references
        CreateMap<TreeNode, TreeNodeDto>();

        // Should trigger AM020: Nested object mapping missing (Address -> AddressDto)
        CreateMap<CompanySource, CompanyDestination>();

        // Proper mapping with explicit conversion - should NOT trigger AM021
#pragma warning disable AM041
        CreateMap<PersonSource, PersonDestination>()
            .ForMember(dest => dest.PhoneNumbers,
                opt => opt.MapFrom(src => src.PhoneNumbers.Select(int.Parse).ToList()));
#pragma warning restore AM041

        // AM030 Examples: Custom Type Converter Issues

        // Should trigger AM030: Missing ConvertUsing configuration for incompatible types
        CreateMap<ProductSource, ProductDestination>();

        // Proper mapping with ConvertUsing - should NOT trigger AM030
#pragma warning disable AM041
        CreateMap<ProductSource, ProductDestination>()
            .ForMember(dest => dest.CreatedDate,
                opt => opt.MapFrom(src => new SafeStringToDateTimeConverter().Convert(src.CreatedDate, default, null!)))
            .ForMember(dest => dest.Price,
                opt => opt.MapFrom(src => ParseDecimalSafely(src.Price)))
            .ForMember(dest => dest.Description,
                opt => opt.MapFrom(src => src.Description ?? string.Empty));
#pragma warning restore AM041
    }

    private static decimal ParseDecimalSafely(string value)
    {
        return decimal.TryParse(value, out var result) ? result : 0m;
    }
}
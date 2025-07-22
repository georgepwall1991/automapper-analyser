using AutoMapper;
using System.Collections.Generic;
using System.Linq;

namespace AutoMapperAnalyzer.Samples
{
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

    public class TestProfile : Profile
    {
        public TestProfile()
        {
            // Should trigger AM021: Collection element type mismatch (string -> int)
#pragma warning disable AM001
#pragma warning disable AM002
#pragma warning disable AM003
#pragma warning disable AM021
            CreateMap<PersonSource, PersonDestination>();
#pragma warning restore AM001
#pragma warning restore AM002
#pragma warning restore AM003
#pragma warning restore AM021

            // Should trigger AM022: Infinite recursion risk due to circular references
#pragma warning disable AM001
            CreateMap<TreeNode, TreeNodeDto>();
#pragma warning restore AM001

            // Should trigger AM020: Nested object mapping missing (Address -> AddressDto)
            CreateMap<CompanySource, CompanyDestination>();

            // Proper mapping with explicit conversion - should NOT trigger AM021
#pragma warning disable AM001
#pragma warning disable AM003
            CreateMap<PersonSource, PersonDestination>()
#pragma warning restore AM003
#pragma warning restore AM001
                .ForMember(dest => dest.PhoneNumbers, opt => opt.MapFrom(src => src.PhoneNumbers.Select(int.Parse).ToList()));
        }
    }
}

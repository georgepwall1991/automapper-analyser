using AutoMapper;

namespace AutoMapperAnalyzer.Samples
{
    // Demo for AM020 Code Fix Provider
    public class Location
    {
        public string Street { get; set; }
        public string City { get; set; }
    }

    public class LocationDto
    {
        public string Street { get; set; }
        public string City { get; set; }
    }

    public class Employee
    {
        public string Name { get; set; }
        public Location WorkLocation { get; set; }
    }

    public class EmployeeDto
    {
        public string Name { get; set; }
        public LocationDto WorkLocation { get; set; }
    }

    public class CodeFixDemoProfile : Profile
    {
        public CodeFixDemoProfile()
        {
            // ❌ AM020: Missing nested object mapping - Location to LocationDto
            // The code fix should suggest adding: CreateMap<Location, LocationDto>();
#pragma warning disable AM020
            CreateMap<Employee, EmployeeDto>();
#pragma warning restore AM020
        }
    }
}

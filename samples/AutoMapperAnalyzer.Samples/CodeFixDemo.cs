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
            // This should trigger AM020 diagnostic for missing nested object mapping
            // The code fix should suggest adding: CreateMap<Location, LocationDto>();
#pragma warning disable AM020
#pragma warning disable AM001
#pragma warning disable AM030
            CreateMap<Employee, EmployeeDto>();
#pragma warning restore AM030
#pragma warning restore AM001
#pragma warning restore AM020
        }
    }
}

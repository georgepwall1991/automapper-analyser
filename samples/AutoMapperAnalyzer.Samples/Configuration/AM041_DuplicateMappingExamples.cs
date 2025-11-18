using AutoMapper;

namespace AutoMapperAnalyzer.Samples.Configuration;

public class AM041_DuplicateMappingExamples
{
    public class Source
    {
    }

    public class Destination
    {
    }

    public class MyProfile : Profile
    {
        public MyProfile()
        {
            // ❌ AM041: Mapping from 'Source' to 'Destination' is already registered
            CreateMap<Source, Destination>();
            CreateMap<Source, Destination>();
        }
    }
}
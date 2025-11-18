using AutoMapper;

namespace AutoMapperAnalyzer.Samples.Configuration;

public class AM050_RedundantMapFromExamples
{
    public class Source
    {
        public string Name { get; set; } = string.Empty;
    }

    public class Destination
    {
        public string Name { get; set; } = string.Empty;
    }

    public class MyProfile : Profile
    {
        public MyProfile()
        {
            CreateMap<Source, Destination>()
                // ℹ️ AM050: Explicit mapping for 'Name' is redundant
                .ForMember(d => d.Name, o => o.MapFrom(s => s.Name));
        }
    }
}


namespace AutoMapperAnalyzer.Samples.UnmappedDestination;

public class UnmappedDestinationExamples
{
    public class Source
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class Destination
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ExtraProperty { get; set; } // Has no matching source
    }

    public class MappingProfile : AutoMapper.Profile
    {
        public MappingProfile()
        {
            CreateMap<Source, Destination>();
            // Should trigger AM006 (Unmapped Destination Property)
            // Because Destination.ExtraProperty is not mapped and not ignored
        }
    }
}


using AutoMapper;

namespace AutoMapperAnalyzer.Samples.MissingProperties;

public class ManyMissingProps
{
    public class Source
    {
        public string Prop01 { get; set; }
        public string Prop02 { get; set; }
        public string Prop03 { get; set; }
        public string Prop04 { get; set; }
        public string Prop05 { get; set; }
        public string Prop06 { get; set; }
        public string Prop07 { get; set; }
        public string Prop08 { get; set; }
        public string Prop09 { get; set; }
        public string Prop10 { get; set; }
    }

    public class Dest
    {
        // Empty destination
    }

    public class Profile : AutoMapper.Profile
    {
        public Profile()
        {
            // This should generate 10 AM004 diagnostics
            CreateMap<Source, Dest>();
        }
    }
}


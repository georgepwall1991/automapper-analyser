using AutoMapper;

namespace AutoMapperAnalyzer.Samples.DataIntegrity;

public class ManyUnmappedRequiredProps
{
    public class Source
    {
        // Empty source
    }

    public class Dest
    {
        public required string Prop01 { get; set; }
        public required string Prop02 { get; set; }
        public required string Prop03 { get; set; }
        public required string Prop04 { get; set; }
        public required string Prop05 { get; set; }
        public required string Prop06 { get; set; }
        public required string Prop07 { get; set; }
        public required string Prop08 { get; set; }
        public required string Prop09 { get; set; }
        public required string Prop10 { get; set; }
        public required string Prop11 { get; set; }
        public required string Prop12 { get; set; }
        public required string Prop13 { get; set; }
        public required string Prop14 { get; set; }
        public required string Prop15 { get; set; }
        public required string Prop16 { get; set; }
        public required string Prop17 { get; set; }
        public required string Prop18 { get; set; }
        public required string Prop19 { get; set; }
        public required string Prop20 { get; set; }
    }

    public class Profile : AutoMapper.Profile
    {
        public Profile()
        {
            // This should generate ~20 AM011 diagnostics (one for each required property)
            CreateMap<Source, Dest>();
        }
    }
}


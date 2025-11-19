using System;
using AutoMapper;

namespace AutoMapperAnalyzer.Samples.ComplexTypes;

public class ManyNullableAndMissingProps
{
    public class Source
    {
        // Nullable properties (will trigger AM002 if mapped to non-nullable)
        public string? Name { get; set; }
        public int? Age { get; set; }
        public DateTime? BirthDate { get; set; }
        public bool? IsActive { get; set; }
        public decimal? Salary { get; set; }

        // Extra properties (will trigger AM004 if not mapped)
        public string Extra1 { get; set; }
        public string Extra2 { get; set; }
        public string Extra3 { get; set; }
        public string Extra4 { get; set; }
        public string Extra5 { get; set; }
    }

    public class Destination
    {
        // Non-nullable properties
        public string Name { get; set; }
        public int Age { get; set; }
        public DateTime BirthDate { get; set; }
        public bool IsActive { get; set; }
        public decimal Salary { get; set; }
    }

    public class Profile : AutoMapper.Profile
    {
        public Profile()
        {
            // Triggers multiple AM002 (nullable mismatch) and AM004 (unmapped source props)
            CreateMap<Source, Destination>();
        }
    }
}


using AutoMapper;

namespace AutoMapperAnalyzer.Samples.TypeSafety;

public class ManyNullableMismatches
{
    public class Source
    {
        public string? Name { get; set; }
        public int? Age { get; set; }
        public DateTime? Created { get; set; }
        public Guid? Id { get; set; }
        public bool? IsActive { get; set; }
        public string? Description { get; set; }
        public string? Title { get; set; }
        public string? Category { get; set; }
        public string? Tags { get; set; }
        public string? Notes { get; set; }
    }

    public class Dest
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public DateTime Created { get; set; }
        public Guid Id { get; set; }
        public bool IsActive { get; set; }
        public string Description { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string Tags { get; set; }
        public string Notes { get; set; }
    }

    public class NonNullableSource
    {
        public string Name { get; set; } = string.Empty;
    }

    public class NullableDest
    {
        public string? Name { get; set; }
    }

    public class Profile : AutoMapper.Profile
    {
        public Profile()
        {
            // This should generate 10 AM002 diagnostics
            CreateMap<Source, Dest>();

            // This should generate the AM002 non-nullable-to-nullable descriptor variant.
            CreateMap<NonNullableSource, NullableDest>();
        }
    }
}

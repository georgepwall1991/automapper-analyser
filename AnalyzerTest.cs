using AutoMapper;

namespace AnalyzerTest
{
    // This should trigger AM001 - Property type mismatch
    public class Source
    {
        public string Age { get; set; } // string
    }
    
    public class Destination  
    {
        public int Age { get; set; } // int - incompatible!
    }
    
    // This should trigger AM002 - Nullable compatibility
    public class SourceNullable
    {
        public string? Name { get; set; } // nullable
    }
    
    public class DestinationNonNullable
    {
        public string Name { get; set; } // non-nullable - should warn!
    }
    
    public class TestProfile : Profile
    {
        public TestProfile()
        {
            // This should trigger AM001 diagnostic
            CreateMap<Source, Destination>();
            
            // This should trigger AM002 diagnostic  
            CreateMap<SourceNullable, DestinationNonNullable>();
        }
    }
} 
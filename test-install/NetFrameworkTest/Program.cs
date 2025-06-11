using System;
using AutoMapper;

namespace NetFrameworkTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing AutoMapper Analyzer with .NET Framework 4.8");

            var config = new MapperConfiguration(cfg =>
            {
                // This should trigger AM001 - Property Type Mismatch
#pragma warning disable AM001
#pragma warning disable AM004
                cfg.CreateMap<SourceClass, DestClass>();
#pragma warning restore AM004
#pragma warning restore AM001
            });

            var mapper = config.CreateMapper();

            var source = new SourceClass { Name = "Test", Age = "25" };
            var dest = mapper.Map<DestClass>(source);

            Console.WriteLine($"Mapped: {dest.Name}, Age: {dest.Age}");
            Console.WriteLine("If you see AM001 warnings during build, the analyzer is working!");
        }
    }

    // Classes that should trigger analyzer warnings
    public class SourceClass
    {
        public string Name { get; set; } = string.Empty;
        public string Age { get; set; } = string.Empty; // This is string
        public string ExtraData { get; set; } = string.Empty; // This will trigger AM004 - missing in destination
    }

    public class DestClass
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; } // This is int - should trigger AM001
    }
}

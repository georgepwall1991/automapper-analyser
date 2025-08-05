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
                // This should trigger AM004 - Missing destination property (ExtraData)
                // Keep these enabled for CI verification
                cfg.CreateMap<SourceClass, DestClass>();

                // This should trigger AM030 - Missing ConvertUsing configuration for incompatible types
#pragma warning disable AM001, AM030
                cfg.CreateMap<CustomerSource, CustomerDest>();
#pragma warning restore AM001, AM030
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

    // AM030 test classes - Custom Type Converter Issues
    public class CustomerSource
    {
        public string JoinDate { get; set; } = "2023-01-15"; // String date
        public string CreditLimit { get; set; } = "5000.00"; // String decimal
    }

    public class CustomerDest
    {
        public DateTime JoinDate { get; set; } // DateTime - needs converter
        public decimal CreditLimit { get; set; } // Decimal - needs converter
    }

    // Example converter demonstrating AM030 scenarios
    public class StringToDateTimeConverter : ITypeConverter<string, DateTime>
    {
        public DateTime Convert(string source, DateTime destination, ResolutionContext context)
        {
            return DateTime.TryParse(source, out var result) ? result : DateTime.MinValue;
        }
    }
}

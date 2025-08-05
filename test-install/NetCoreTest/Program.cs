using System;
using AutoMapper;

namespace NetCoreTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing AutoMapper Analyzer with .NET Core 3.1");

            var config = new MapperConfiguration(cfg =>
            {
                // This should trigger AM001 - Property Type Mismatch
                // This should trigger AM004 - Missing destination property (ExtraData)
#pragma warning disable AM001, AM004
#pragma warning disable AM004
#pragma warning disable AM030
                cfg.CreateMap<SourceClass, DestClass>();
#pragma warning restore AM030
#pragma warning restore AM004
#pragma warning restore AM001, AM004

                // This should trigger AM030 - Missing ConvertUsing configuration for incompatible types
#pragma warning disable AM001, AM030
                cfg.CreateMap<OrderSource, OrderDest>();
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
    public class OrderSource
    {
        public string OrderDate { get; set; } = "2023-12-01"; // String date
        public string TotalAmount { get; set; } = "199.99"; // String decimal
    }

    public class OrderDest
    {
        public DateTime OrderDate { get; set; } // DateTime - needs converter
        public decimal TotalAmount { get; set; } // Decimal - needs converter
    }

    // Example converter with null handling issue
    public class UnsafeStringToDecimalConverter : ITypeConverter<string?, decimal>
    {
#pragma warning disable AM030
        public decimal Convert(string? source, decimal destination, ResolutionContext context)
#pragma warning restore AM030
        {
            // No null check - would trigger AM030 null handling warning
#pragma warning disable CA1305
#pragma warning disable CS8604 // Possible null reference argument.
            return decimal.Parse(source);
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CA1305
        }
    }
}

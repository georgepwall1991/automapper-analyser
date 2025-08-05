using AutoMapper;

namespace TestPackage;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Testing AutoMapper Analyzer with .NET 9.0");

        var config = new MapperConfiguration(cfg =>
        {
            // This should trigger AM001 - Property Type Mismatch
#pragma warning disable AM001, AM004
            cfg.CreateMap<SourceClass, DestClass>();
#pragma warning restore AM001, AM004

            // This should trigger AM030 - Missing ConvertUsing configuration for incompatible types
#pragma warning disable AM001, AM030
            cfg.CreateMap<EventSource, EventDest>();
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
public class EventSource
{
    public string StartTime { get; set; } = "14:30:00"; // String time
    public string Duration { get; set; } = "120"; // String minutes
}

public class EventDest
{
    public TimeSpan StartTime { get; set; } // TimeSpan - needs converter
    public int Duration { get; set; } // Int minutes - needs converter
}

// Example converter for AM030 demonstration
public class TimeStringToTimeSpanConverter : ITypeConverter<string, TimeSpan>
{
    public TimeSpan Convert(string source, TimeSpan destination, ResolutionContext context)
    {
        return TimeSpan.TryParse(source, out var result) ? result : TimeSpan.Zero;
    }
}

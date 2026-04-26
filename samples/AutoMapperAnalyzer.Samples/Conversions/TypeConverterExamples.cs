using AutoMapper;

namespace AutoMapperAnalyzer.Samples.Conversions;

/// <summary>
///     Examples of conversion-related issues that AutoMapper analyzer will detect
/// </summary>
public class TypeConverterExamples
{
    /// <summary>
    ///     AM001: Missing property-level conversion for string to DateTime
    ///     This should trigger an AM001 diagnostic.
    /// </summary>
    public void MissingTypeConverterExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ❌ AM001: Property 'BirthDate' requires explicit conversion from string to DateTime.
            cfg.CreateMap<PersonWithStringDate, PersonWithDateTime>();
        });

        var mapper = config.CreateMapper();

        var source = new PersonWithStringDate
        {
            Name = "John Doe",
            BirthDate = "1990-05-15"
        };

        try
        {
            var destination = mapper.Map<PersonWithDateTime>(source);
            Console.WriteLine($"Mapped: {destination.Name}, BirthDate: {destination.BirthDate}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Runtime error: {ex.Message}");
        }
    }

    /// <summary>
    ///     AM001: Missing property-level conversion for string to enum
    ///     This should trigger an AM001 diagnostic.
    /// </summary>
    public void MissingEnumConverterExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ❌ AM001: Property 'Status' requires explicit conversion from string to StatusEnum.
            cfg.CreateMap<OrderWithStringStatus, OrderWithEnumStatus>();
        });

        var mapper = config.CreateMapper();

        var source = new OrderWithStringStatus
        {
            Id = 1,
            Status = "Pending"
        };

        try
        {
            var destination = mapper.Map<OrderWithEnumStatus>(source);
            Console.WriteLine($"Mapped: Order {destination.Id}, Status: {destination.Status}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Runtime error: {ex.Message}");
        }
    }

    /// <summary>
    ///     AM030: Converter null-handling issue for nullable source input
    ///     This should trigger an AM030 diagnostic.
    /// </summary>
    public void NullUnsafeConverterClassExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ❌ AM030: Source type is nullable and converter implementation has no null guard.
            cfg.CreateMap<string?, Guid>().ConvertUsing<NullUnsafeStringToGuidConverter>();
        });

        var mapper = config.CreateMapper();
        string? source = null;

        try
        {
            Guid destination = mapper.Map<Guid>(source);
            Console.WriteLine($"Mapped Guid: {destination}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Runtime error: {ex.Message}");
        }
    }
}

// Supporting classes for type converter examples

public class PersonWithStringDate
{
    public string Name { get; set; } = string.Empty;
    public string BirthDate { get; set; } = string.Empty; // String date
}

public class PersonWithDateTime
{
    public string Name { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; } // DateTime - needs converter!
}

public class OrderWithStringStatus
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty; // String status
}

public enum StatusEnum
{
    Pending,
    Processing,
    Completed,
    Cancelled
}

public class OrderWithEnumStatus
{
    public int Id { get; set; }
    public StatusEnum Status { get; set; } // Enum - needs converter!
}

public class SourceWithNullableString
{
    public string Name { get; set; } = string.Empty;
    public string? GuidString { get; set; } // Nullable string
}

public class DestWithGuid
{
    public string Name { get; set; } = string.Empty;
    public Guid UniqueId { get; set; } // Guid - needs null-safe converter!
}

/// <summary>
///     Examples of CORRECT custom type converter patterns (for comparison)
/// </summary>
public class CorrectTypeConverterExamples
{
    public void CorrectStringToDateTimeConverterExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Correct: Explicit type conversion with error handling
#pragma warning disable AM041
            cfg.CreateMap<PersonWithStringDate, PersonWithDateTime>()
                .ForMember(dest => dest.BirthDate,
                    opt => opt.MapFrom(src =>
                        string.IsNullOrEmpty(src.BirthDate)
                            ? DateTime.MinValue
                            : DateTime.Parse(src.BirthDate)));
#pragma warning restore AM041
        });

        var mapper = config.CreateMapper();

        var source = new PersonWithStringDate
        {
            Name = "John Doe",
            BirthDate = "1990-05-15"
        };

        var destination = mapper.Map<PersonWithDateTime>(source);
        Console.WriteLine($"✅ Correctly mapped: {destination.Name}, BirthDate: {destination.BirthDate:yyyy-MM-dd}");
    }

    public void CorrectStringToEnumConverterExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Correct: Explicit enum conversion with fallback
#pragma warning disable AM041
            cfg.CreateMap<OrderWithStringStatus, OrderWithEnumStatus>()
                .ForMember(dest => dest.Status,
                    opt => opt.MapFrom(src =>
                        Enum.IsDefined(typeof(StatusEnum), src.Status)
                            ? (StatusEnum)Enum.Parse(typeof(StatusEnum), src.Status, true)
                            : StatusEnum.Pending));
#pragma warning restore AM041
        });

        var mapper = config.CreateMapper();

        var source = new OrderWithStringStatus
        {
            Id = 1,
            Status = "Processing"
        };

        var destination = mapper.Map<OrderWithEnumStatus>(source);
        Console.WriteLine($"✅ Correctly mapped: Order {destination.Id}, Status: {destination.Status}");
    }

    public void CorrectNullSafeConverterExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
#pragma warning disable AM041
            cfg.CreateMap<SourceWithNullableString, DestWithGuid>()
                // ✅ Correct: Null-safe conversion with default value
                .ForMember(dest => dest.UniqueId,
                    opt => opt.MapFrom(src =>
                        string.IsNullOrEmpty(src.GuidString)
                            ? Guid.Empty
                            : Guid.Parse(src.GuidString)));
#pragma warning restore AM041
        });

        var mapper = config.CreateMapper();

        var source = new SourceWithNullableString
        {
            Name = "Test",
            GuidString = null
        };

        var destination = mapper.Map<DestWithGuid>(source);
        Console.WriteLine($"✅ Correctly mapped with null handling: {destination.Name}, ID: {destination.UniqueId}");
    }

    public void CorrectCustomTypeConverterClassExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Correct: Using a dedicated ITypeConverter class
#pragma warning disable AM041
            cfg.CreateMap<string?, DateTime>()
                .ConvertUsing<SafeStringToDateTimeScalarConverter>();
#pragma warning restore AM041
        });

        var mapper = config.CreateMapper();
        DateTime destination = mapper.Map<DateTime>("1985-03-20");
        Console.WriteLine($"✅ Correctly mapped using ITypeConverter: BirthDate: {destination:yyyy-MM-dd}");
    }
}

/// <summary>
///     Example of an unsafe converter implementation (for AM030 null-handling diagnostic)
/// </summary>
public class NullUnsafeStringToGuidConverter : ITypeConverter<string?, Guid>
{
    public Guid Convert(string? source, Guid destination, ResolutionContext context)
    {
        // Intentionally unsafe to demonstrate AM030.
        return Guid.Parse(source);
    }
}

/// <summary>
///     Example of a proper scalar ITypeConverter implementation
/// </summary>
public class SafeStringToDateTimeScalarConverter : ITypeConverter<string?, DateTime>
{
    public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return DateTime.MinValue;
        }

        return DateTime.TryParse(source, out var parsed) ? parsed : DateTime.MinValue;
    }
}

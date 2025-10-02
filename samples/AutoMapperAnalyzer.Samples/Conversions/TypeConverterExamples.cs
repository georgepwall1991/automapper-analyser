using AutoMapper;

namespace AutoMapperAnalyzer.Samples.Conversions;

/// <summary>
///     Examples of custom type converter issues that AutoMapper analyzer will detect
/// </summary>
public class TypeConverterExamples
{
    /// <summary>
    ///     AM030: Missing Custom Type Converter for string to complex type
    ///     This should trigger AM030 diagnostic
    /// </summary>
    public void MissingTypeConverterExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ❌ AM001/AM030: Property 'BirthDate' requires custom converter from string to DateTime
#pragma warning disable AM001, AM030
            cfg.CreateMap<PersonWithStringDate, PersonWithDateTime>();
#pragma warning restore AM001, AM030
        });

        IMapper? mapper = config.CreateMapper();

        var source = new PersonWithStringDate
        {
            Name = "John Doe",
            BirthDate = "1990-05-15"
        };

        try
        {
            PersonWithDateTime? destination = mapper.Map<PersonWithDateTime>(source);
            Console.WriteLine($"Mapped: {destination.Name}, BirthDate: {destination.BirthDate}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Runtime error: {ex.Message}");
        }
    }

    /// <summary>
    ///     AM030: Missing ConvertUsing for enum name to value conversion
    ///     This should trigger AM030 diagnostic
    /// </summary>
    public void MissingEnumConverterExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ❌ AM001/AM030: Property 'Status' requires custom converter from string to StatusEnum
#pragma warning disable AM001, AM030
            cfg.CreateMap<OrderWithStringStatus, OrderWithEnumStatus>();
#pragma warning restore AM001, AM030
        });

        IMapper? mapper = config.CreateMapper();

        var source = new OrderWithStringStatus
        {
            Id = 1,
            Status = "Pending"
        };

        try
        {
            OrderWithEnumStatus? destination = mapper.Map<OrderWithEnumStatus>(source);
            Console.WriteLine($"Mapped: Order {destination.Id}, Status: {destination.Status}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Runtime error: {ex.Message}");
        }
    }

    /// <summary>
    ///     AM030: Null value handling in custom converter
    ///     This should trigger AM030 diagnostic for null safety
    /// </summary>
    public void NullValueInConverterExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<SourceWithNullableString, DestWithGuid>()
                // ❌ AM030: Converter doesn't handle null values properly
#pragma warning disable AM030
                .ForMember(dest => dest.UniqueId,
                    opt => opt.MapFrom(src => Guid.Parse(src.GuidString)));
#pragma warning restore AM030
        });

        IMapper? mapper = config.CreateMapper();

        var source = new SourceWithNullableString
        {
            Name = "Test",
            GuidString = null // This will cause Guid.Parse to throw!
        };

        try
        {
            DestWithGuid? destination = mapper.Map<DestWithGuid>(source);
            Console.WriteLine($"Mapped: {destination.Name}, ID: {destination.UniqueId}");
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
            cfg.CreateMap<PersonWithStringDate, PersonWithDateTime>()
                .ForMember(dest => dest.BirthDate,
                    opt => opt.MapFrom(src =>
                        string.IsNullOrEmpty(src.BirthDate)
                            ? DateTime.MinValue
                            : DateTime.Parse(src.BirthDate)));
        });

        IMapper? mapper = config.CreateMapper();

        var source = new PersonWithStringDate
        {
            Name = "John Doe",
            BirthDate = "1990-05-15"
        };

        PersonWithDateTime? destination = mapper.Map<PersonWithDateTime>(source);
        Console.WriteLine($"✅ Correctly mapped: {destination.Name}, BirthDate: {destination.BirthDate:yyyy-MM-dd}");
    }

    public void CorrectStringToEnumConverterExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Correct: Explicit enum conversion with fallback
            cfg.CreateMap<OrderWithStringStatus, OrderWithEnumStatus>()
                .ForMember(dest => dest.Status,
                    opt => opt.MapFrom(src =>
                        Enum.IsDefined(typeof(StatusEnum), src.Status)
                            ? (StatusEnum)Enum.Parse(typeof(StatusEnum), src.Status, true)
                            : StatusEnum.Pending));
        });

        IMapper? mapper = config.CreateMapper();

        var source = new OrderWithStringStatus
        {
            Id = 1,
            Status = "Processing"
        };

        OrderWithEnumStatus? destination = mapper.Map<OrderWithEnumStatus>(source);
        Console.WriteLine($"✅ Correctly mapped: Order {destination.Id}, Status: {destination.Status}");
    }

    public void CorrectNullSafeConverterExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<SourceWithNullableString, DestWithGuid>()
                // ✅ Correct: Null-safe conversion with default value
                .ForMember(dest => dest.UniqueId,
                    opt => opt.MapFrom(src =>
                        string.IsNullOrEmpty(src.GuidString)
                            ? Guid.Empty
                            : Guid.Parse(src.GuidString)));
        });

        IMapper? mapper = config.CreateMapper();

        var source = new SourceWithNullableString
        {
            Name = "Test",
            GuidString = null
        };

        DestWithGuid? destination = mapper.Map<DestWithGuid>(source);
        Console.WriteLine($"✅ Correctly mapped with null handling: {destination.Name}, ID: {destination.UniqueId}");
    }

    public void CorrectCustomTypeConverterClassExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Correct: Using a dedicated ITypeConverter class
#pragma warning disable AM001  // ConvertUsing handles the type mismatch
            cfg.CreateMap<PersonWithStringDate, PersonWithDateTime>()
                .ConvertUsing<StringToDateTimeConverter>();
#pragma warning restore AM001
        });

        IMapper? mapper = config.CreateMapper();

        var source = new PersonWithStringDate
        {
            Name = "Jane Smith",
            BirthDate = "1985-03-20"
        };

        PersonWithDateTime? destination = mapper.Map<PersonWithDateTime>(source);
        Console.WriteLine($"✅ Correctly mapped using ITypeConverter: {destination.Name}, BirthDate: {destination.BirthDate:yyyy-MM-dd}");
    }
}

/// <summary>
///     Example of a proper ITypeConverter implementation
/// </summary>
public class StringToDateTimeConverter : ITypeConverter<PersonWithStringDate, PersonWithDateTime>
{
    public PersonWithDateTime Convert(PersonWithStringDate source, PersonWithDateTime destination, ResolutionContext context)
    {
        return new PersonWithDateTime
        {
            Name = source.Name,
            BirthDate = DateTime.TryParse(source.BirthDate, out var date)
                ? date
                : DateTime.MinValue
        };
    }
}

using AutoMapper;

namespace AutoMapperAnalyzer.Samples.MissingProperties;

/// <summary>
///     Examples of missing property issues that AutoMapper analyzer will detect
/// </summary>
public class MissingPropertyExamples
{
    /// <summary>
    ///     AM010: Missing Destination Property - potential data loss
    ///     This should trigger AM010 diagnostic
    /// </summary>
    public void MissingDestinationPropertyExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ❌ AM010: Source property 'ImportantData' will not be mapped - potential data loss
            cfg.CreateMap<SourceWithExtraData, DestinationMissingData>();
        });

        IMapper? mapper = config.CreateMapper();

        var source = new SourceWithExtraData
        {
            Name = "John",
            Email = "john@example.com",
            ImportantData = "This data will be lost!" // This won't be mapped!
        };

        DestinationMissingData? destination = mapper.Map<DestinationMissingData>(source);
        Console.WriteLine($"Mapped: Name={destination.Name}, Email={destination.Email}");
        Console.WriteLine("❌ ImportantData was lost in mapping!");
    }

    /// <summary>
    ///     AM011: Unmapped Required Property - will cause runtime exception
    ///     This should trigger AM011 diagnostic
    /// </summary>
    public void UnmappedRequiredPropertyExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ❌ AM011: Required destination property 'RequiredField' is not mapped from any source property
            cfg.CreateMap<SourceWithoutRequired, DestinationWithRequired>();
        });

        IMapper? mapper = config.CreateMapper();

        var source = new SourceWithoutRequired { Name = "John", Age = 25 };

        try
        {
            DestinationWithRequired? destination = mapper.Map<DestinationWithRequired>(source);
            Console.WriteLine($"Mapped: {destination.Name}, Required: {destination.RequiredField}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Runtime error: {ex.Message}");
        }
    }

    /// <summary>
    ///     AM012: Case Sensitivity Mismatch
    ///     This should trigger AM012 diagnostic
    /// </summary>
    public void CaseSensitivityMismatchExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ❌ AM012: Properties differ only in casing: 'userName' vs 'UserName'
            cfg.CreateMap<SourceWithCamelCase, DestinationWithPascalCase>();
        });

        IMapper? mapper = config.CreateMapper();

        var source = new SourceWithCamelCase { firstName = "John", lastName = "Doe", userName = "johndoe" };

        DestinationWithPascalCase? destination = mapper.Map<DestinationWithPascalCase>(source);
        Console.WriteLine(
            $"Mapped: FirstName={destination.FirstName}, LastName={destination.LastName}, UserName={destination.UserName}");
        Console.WriteLine("❌ Case sensitivity may cause mapping issues!");
    }
}

// Supporting classes for missing property examples

public class SourceWithExtraData
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ImportantData { get; set; } = string.Empty; // This will be lost!
}

public class DestinationMissingData
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    // Missing ImportantData property - data loss!
}

public class SourceWithoutRequired
{
    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }
    // No RequiredField property
}

public class DestinationWithRequired
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public required string RequiredField { get; set; } // Required but not in source!
}

public class SourceWithCamelCase
{
    public string firstName { get; set; } = string.Empty; // camelCase
    public string lastName { get; set; } = string.Empty; // camelCase
    public string userName { get; set; } = string.Empty; // camelCase
}

public class DestinationWithPascalCase
{
    public string FirstName { get; set; } = string.Empty; // PascalCase
    public string LastName { get; set; } = string.Empty; // PascalCase
    public string UserName { get; set; } = string.Empty; // PascalCase
}

/// <summary>
///     Examples of CORRECT missing property handling (for comparison)
/// </summary>
public class CorrectMissingPropertyExamples
{
    public void CorrectDataLossHandlingExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Correct: Explicit handling of data loss
            cfg.CreateMap<SourceWithExtraData, DestinationMissingData>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src =>
                    $"{src.Name} (Data: {src.ImportantData})"));
        });

        IMapper? mapper = config.CreateMapper();
        var source = new SourceWithExtraData
        {
            Name = "John", Email = "john@example.com", ImportantData = "Important!"
        };
        DestinationMissingData? destination = mapper.Map<DestinationMissingData>(source);

        Console.WriteLine($"✅ Correctly handled: {destination.Name}");
    }

    public void CorrectRequiredFieldHandlingExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Correct: Explicit mapping for required field
            cfg.CreateMap<SourceWithoutRequired, DestinationWithRequired>()
                .ForMember(dest => dest.RequiredField, opt => opt.MapFrom(src => "Default Value"));
        });

        IMapper? mapper = config.CreateMapper();
        var source = new SourceWithoutRequired { Name = "John", Age = 25 };
        DestinationWithRequired? destination = mapper.Map<DestinationWithRequired>(source);

        Console.WriteLine($"✅ Correctly mapped: {destination.Name}, Required: {destination.RequiredField}");
    }

    public void CorrectCaseSensitivityHandlingExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Correct: Explicit case-insensitive mapping
            cfg.CreateMap<SourceWithCamelCase, DestinationWithPascalCase>()
                .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.firstName))
                .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.lastName))
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.userName));
        });

        IMapper? mapper = config.CreateMapper();
        var source = new SourceWithCamelCase { firstName = "John", lastName = "Doe", userName = "johndoe" };
        DestinationWithPascalCase? destination = mapper.Map<DestinationWithPascalCase>(source);

        Console.WriteLine(
            $"✅ Correctly mapped: {destination.FirstName} {destination.LastName}, User: {destination.UserName}");
    }
}

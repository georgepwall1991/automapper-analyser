using AutoMapper;

namespace AutoMapperAnalyzer.Samples.TypeSafety;

/// <summary>
///     Examples of type safety issues that AutoMapper analyzer will detect
/// </summary>
public class TypeSafetyExamples
{
    /// <summary>
    ///     AM001: Property Type Mismatch - string to int without converter
    ///     This should trigger AM001 diagnostic
    /// </summary>
    public void PropertyTypeMismatchExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ❌ AM001: Property 'Age' type mismatch: source is 'string' but destination is 'int'
#pragma warning disable AM001
            cfg.CreateMap<PersonWithStringAge, PersonWithIntAge>();
#pragma warning restore AM001
        });

        IMapper? mapper = config.CreateMapper();

        var source = new PersonWithStringAge { Name = "John", Age = "25" };

        try
        {
            PersonWithIntAge? destination = mapper.Map<PersonWithIntAge>(source);
            Console.WriteLine($"Mapped: {destination.Name}, Age: {destination.Age}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Runtime error: {ex.Message}");
        }
    }

    /// <summary>
    ///     AM002: Nullable to Non-Nullable Assignment without null handling
    ///     This should trigger AM002 diagnostic
    /// </summary>
    public void NullableToNonNullableExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
#pragma warning disable AM002
            cfg.CreateMap<PersonWithNullableName, PersonWithRequiredName>();
#pragma warning restore AM002
        });

        IMapper? mapper = config.CreateMapper();

        var source = new PersonWithNullableName { Id = 1, Name = null }; // Name is null!

        try
        {
            PersonWithRequiredName? destination = mapper.Map<PersonWithRequiredName>(source);
            Console.WriteLine($"Mapped: ID={destination.Id}, Name='{destination.Name}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Runtime error: {ex.Message}");
        }
    }

    /// <summary>
    ///     AM003: Collection Type Incompatibility
    ///     This should trigger AM003 diagnostic
    /// </summary>
    public void CollectionTypeIncompatibilityExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<ArticleWithStringTags, ArticleWithIntTags>();
        });

        IMapper? mapper = config.CreateMapper();

        var source = new ArticleWithStringTags
        {
            Title = "Sample Article",
            Tags = new List<string> { "tech", "programming", "csharp" },
        };

        try
        {
            ArticleWithIntTags? destination = mapper.Map<ArticleWithIntTags>(source);
            Console.WriteLine(
                $"Mapped: {destination.Title}, Tags: [{string.Join(", ", destination.Tags)}]"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Runtime error: {ex.Message}");
        }
    }
}

// Supporting classes for type safety examples

public class PersonWithStringAge
{
    public string Name { get; set; } = string.Empty;
    public string Age { get; set; } = string.Empty; // String age - problematic!
}

public class PersonWithIntAge
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; } // Int age - type mismatch!
}

public class PersonWithNullableName
{
    public int Id { get; set; }
    public string? Name { get; set; } // Nullable
}

public class PersonWithRequiredName
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // Non-nullable - potential NRE!
}

public class ArticleWithStringTags
{
    public string Title { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = []; // List of strings
}

public class ArticleWithIntTags
{
    public string Title { get; set; } = string.Empty;
    public HashSet<int> Tags { get; set; } = []; // HashSet of ints - incompatible!
}

/// <summary>
///     Examples of CORRECT type safety patterns (for comparison)
/// </summary>
public class CorrectTypeSafetyExamples
{
    public void CorrectTypeConversionExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Correct: Explicit type conversion
            cfg.CreateMap<PersonWithStringAge, PersonWithIntAge>()
                .ForMember(
                    dest => dest.Age,
                    opt =>
                        opt.MapFrom(src =>
                            string.IsNullOrEmpty(src.Age) ? 0 : Convert.ToInt32(src.Age)
                        )
                );
        });

        IMapper? mapper = config.CreateMapper();
        var source = new PersonWithStringAge { Name = "John", Age = "25" };
        PersonWithIntAge? destination = mapper.Map<PersonWithIntAge>(source);

        Console.WriteLine($"✅ Correctly mapped: {destination.Name}, Age: {destination.Age}");
    }

    public void CorrectNullHandlingExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Correct: Explicit null handling
            cfg.CreateMap<PersonWithNullableName, PersonWithRequiredName>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? "Unknown"));
        });

        IMapper? mapper = config.CreateMapper();
        var source = new PersonWithNullableName { Id = 1, Name = null };
        PersonWithRequiredName? destination = mapper.Map<PersonWithRequiredName>(source);

        Console.WriteLine($"✅ Correctly mapped: ID={destination.Id}, Name='{destination.Name}'");
    }
}

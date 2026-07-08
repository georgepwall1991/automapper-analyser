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
            cfg.CreateMap<PersonWithStringAge, PersonWithIntAge>();
        });

        var mapper = config.CreateMapper();

        var source = new PersonWithStringAge { Name = "John", Age = "25" };

        try
        {
            var destination = mapper.Map<PersonWithIntAge>(source);
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
        var config =
            new MapperConfiguration(cfg => { cfg.CreateMap<PersonWithNullableName, PersonWithRequiredName>(); });

        var mapper = config.CreateMapper();

        var source = new PersonWithNullableName { Id = 1, Name = null }; // Name is null!

        try
        {
            var destination = mapper.Map<PersonWithRequiredName>(source);
            Console.WriteLine($"Mapped: ID={destination.Id}, Name='{destination.Name}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Runtime error: {ex.Message}");
        }
    }

    /// <summary>
    ///     AM003: Collection container incompatibility (same element type, different container).
    ///     Isolates AM003 ownership — element-type mismatches are AM021, not this sample.
    /// </summary>
    public void CollectionTypeIncompatibilityExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ❌ AM003: Collection container mismatch: List<string> to HashSet<string>
            cfg.CreateMap<ArticleWithListTags, ArticleWithHashSetTags>();
        });

        var mapper = config.CreateMapper();

        var source = new ArticleWithListTags
        {
            Title = "Sample Article",
            Tags = new List<string> { "tech", "programming", "csharp" }
        };

        try
        {
            var destination = mapper.Map<ArticleWithHashSetTags>(source);
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

public class ArticleWithListTags
{
    public string Title { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = []; // List container
}

public class ArticleWithHashSetTags
{
    public string Title { get; set; } = string.Empty;
    public HashSet<string> Tags { get; set; } = []; // HashSet container — AM003 owns this, not AM021
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
#pragma warning disable AM041
            cfg.CreateMap<PersonWithStringAge, PersonWithIntAge>()
                .ForMember(
                    dest => dest.Age,
                    opt =>
                        opt.MapFrom(src =>
                            string.IsNullOrEmpty(src.Age) ? 0 : Convert.ToInt32(src.Age)
                        )
                );
#pragma warning restore AM041
        });

        var mapper = config.CreateMapper();
        var source = new PersonWithStringAge { Name = "John", Age = "25" };
        var destination = mapper.Map<PersonWithIntAge>(source);

        Console.WriteLine($"✅ Correctly mapped: {destination.Name}, Age: {destination.Age}");
    }

    public void CorrectNullHandlingExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Correct: Explicit null handling
#pragma warning disable AM041
            cfg.CreateMap<PersonWithNullableName, PersonWithRequiredName>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? "Unknown"));
#pragma warning restore AM041
        });

        var mapper = config.CreateMapper();
        var source = new PersonWithNullableName { Id = 1, Name = null };
        var destination = mapper.Map<PersonWithRequiredName>(source);

        Console.WriteLine($"✅ Correctly mapped: ID={destination.Id}, Name='{destination.Name}'");
    }
}

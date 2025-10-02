using AutoMapper;

namespace AutoMapperAnalyzer.Samples.ComplexTypes;

/// <summary>
///     Examples of complex type mapping issues that AutoMapper analyzer will detect
/// </summary>
public class ComplexTypeMappingExamples
{
    /// <summary>
    ///     AM021: Collection Element Type Mismatch
    ///     This should trigger AM021 diagnostic
    /// </summary>
    public void CollectionElementMismatchExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ❌ AM001/AM021: Collection element types are incompatible: List<SourceItem> to List<DestItem>
            // The nested object mapping is missing: SourceItem -> DestItem
            cfg.CreateMap<SourceWithItems, DestWithItems>();
        });

        IMapper? mapper = config.CreateMapper();

        var source = new SourceWithItems
        {
            Name = "Container",
            Items = [
                new SourceItem { Id = 1, Value = "Item 1" },
                new SourceItem { Id = 2, Value = "Item 2" }
            ]
        };

        try
        {
            DestWithItems? destination = mapper.Map<DestWithItems>(source);
            Console.WriteLine($"Mapped: {destination.Name}, Items: {destination.Items.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Runtime error: {ex.Message}");
        }
    }

    /// <summary>
    ///     AM022: Infinite Recursion Risk
    ///     This should trigger AM022 diagnostic
    /// </summary>
    public void InfiniteRecursionExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ❌ AM022: Circular reference detected - Parent → Child → Parent
            cfg.CreateMap<Parent, ParentDto>();
            cfg.CreateMap<Child, ChildDto>();
        });

        IMapper? mapper = config.CreateMapper();

        var parent = new Parent { Name = "Parent" };
        var child = new Child { Name = "Child", Parent = parent };
        parent.Child = child; // Circular reference!

        try
        {
            ParentDto? parentDto = mapper.Map<ParentDto>(parent);
            Console.WriteLine($"Mapped: {parentDto.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Runtime error: {ex.Message} (StackOverflowException risk!)");
        }
    }
}

// Supporting classes for collection element mismatch examples

public class SourceItem
{
    public int Id { get; set; }
    public string Value { get; set; } = string.Empty;
}

public class DestItem
{
    public int Id { get; set; }
    public string DisplayValue { get; set; } = string.Empty; // Different property name!
}

public class SourceWithItems
{
    public string Name { get; set; } = string.Empty;
    public List<SourceItem> Items { get; set; } = [];
}

public class DestWithItems
{
    public string Name { get; set; } = string.Empty;
    public List<DestItem> Items { get; set; } = []; // Element types don't match!
}

// Supporting classes for infinite recursion examples

public class Parent
{
    public string Name { get; set; } = string.Empty;
    public Child? Child { get; set; }
}

public class Child
{
    public string Name { get; set; } = string.Empty;
    public Parent? Parent { get; set; } // Circular reference!
}

public class ParentDto
{
    public string Name { get; set; } = string.Empty;
    public ChildDto? Child { get; set; }
}

public class ChildDto
{
    public string Name { get; set; } = string.Empty;
    public ParentDto? Parent { get; set; } // Circular reference in DTOs!
}

/// <summary>
///     Examples of CORRECT complex type mapping patterns (for comparison)
/// </summary>
public class CorrectComplexTypeMappingExamples
{
    public void CorrectCollectionElementMappingExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Correct: Explicit mapping for collection elements
            cfg.CreateMap<SourceWithItems, DestWithItems>();
            cfg.CreateMap<SourceItem, DestItem>()
                .ForMember(dest => dest.DisplayValue, opt => opt.MapFrom(src => src.Value));
        });

        IMapper? mapper = config.CreateMapper();

        var source = new SourceWithItems
        {
            Name = "Container",
            Items = [
                new SourceItem { Id = 1, Value = "Item 1" },
                new SourceItem { Id = 2, Value = "Item 2" }
            ]
        };

        DestWithItems? destination = mapper.Map<DestWithItems>(source);
        Console.WriteLine($"✅ Correctly mapped: {destination.Name}, Items: {destination.Items.Count}");
        Console.WriteLine($"   First item: {destination.Items[0].DisplayValue}");
    }

    public void CorrectCircularReferenceHandlingExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Correct: Use PreserveReferences to handle circular references
            cfg.CreateMap<Parent, ParentDto>().PreserveReferences();
            cfg.CreateMap<Child, ChildDto>().PreserveReferences();
        });

        IMapper? mapper = config.CreateMapper();

        var parent = new Parent { Name = "Parent" };
        var child = new Child { Name = "Child", Parent = parent };
        parent.Child = child;

        ParentDto? parentDto = mapper.Map<ParentDto>(parent);
        Console.WriteLine($"✅ Correctly mapped with circular reference handling");
        Console.WriteLine($"   Parent: {parentDto.Name}, Child: {parentDto.Child?.Name}");
    }

    public void CorrectCircularReferenceAvoidanceExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Alternative: Break the circular reference in DTOs
            cfg.CreateMap<Parent, ParentDto>()
                .ForMember(dest => dest.Child, opt => opt.MapFrom(src => src.Child));
            cfg.CreateMap<Child, ChildDto>()
                .ForMember(dest => dest.Parent, opt => opt.Ignore()); // Break the cycle
        });

        IMapper? mapper = config.CreateMapper();

        var parent = new Parent { Name = "Parent" };
        var child = new Child { Name = "Child", Parent = parent };
        parent.Child = child;

        ParentDto? parentDto = mapper.Map<ParentDto>(parent);
        Console.WriteLine($"✅ Correctly mapped by breaking circular reference");
        Console.WriteLine($"   Parent: {parentDto.Name}, Child: {parentDto.Child?.Name}");
        Console.WriteLine($"   Child's parent is ignored to prevent recursion");
    }
}

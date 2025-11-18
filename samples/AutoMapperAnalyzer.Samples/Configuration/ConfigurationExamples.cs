using AutoMapper;

namespace AutoMapperAnalyzer.Samples.Configuration;

/// <summary>
///     ⚠️ FUTURE IMPLEMENTATION - These analyzers (AM040-AM042) are NOT yet implemented.
///     These examples are included for future development reference and documentation.
///     Examples of configuration issues that AutoMapper analyzer will detect (when implemented)
/// </summary>
public class ConfigurationExamples
{
    /// <summary>
    ///     AM040: Missing Profile Registration
    ///     This should trigger AM040 diagnostic
    /// </summary>
    public void MissingProfileRegistrationExample()
    {
        // ❌ AM040: Profile 'UserMappingProfile' is defined but not registered
        var config = new MapperConfiguration(cfg =>
        {
            // Profile exists but is not registered here!
            cfg.CreateMap<User, UserDto>();
        });

        var mapper = config.CreateMapper();

        var user = new User { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com" };

        try
        {
            var userDto = mapper.Map<UserDto>(user);
            Console.WriteLine($"Mapped: {userDto.FullName}, Email: {userDto.Email}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Runtime error: {ex.Message}");
        }
    }

    /// <summary>
    ///     AM041: Conflicting Mapping Rules
    ///     This should trigger AM041 diagnostic
    /// </summary>
    public void ConflictingMappingRulesExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Product, ProductDto>()
                // ❌ AM041: Conflicting mapping rules for property 'Name'
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.ProductName))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Title)); // Conflict!
        });

        var mapper = config.CreateMapper();

        var product = new Product { Id = 1, ProductName = "Widget", Title = "Amazing Widget", Price = 19.99m };

        var productDto = mapper.Map<ProductDto>(product);
        Console.WriteLine($"Mapped: Name={productDto.Name}, Price={productDto.Price}");
        Console.WriteLine("❌ Which Name mapping was used? Behavior is undefined!");
    }

    /// <summary>
    ///     AM042: Ignore vs MapFrom Conflict (implementation for future)
    ///     This would trigger AM042 diagnostic when implemented
    /// </summary>
    public void IgnoreVsMapFromConflictExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Order, OrderDto>()
                // ❌ AM042: Property 'CustomerName' is both ignored and explicitly mapped
                .ForMember(dest => dest.CustomerName, opt => opt.Ignore())
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer.Name)); // Conflict!
        });

        var mapper = config.CreateMapper();

        var order = new Order { Id = 1, Amount = 100.00m, Customer = new Customer { Name = "John Doe" } };

        var orderDto = mapper.Map<OrderDto>(order);
        Console.WriteLine($"Mapped: Order ID={orderDto.Id}, Customer={orderDto.CustomerName}");
        Console.WriteLine("❌ Is CustomerName ignored or mapped? Behavior is undefined!");
    }
}

// Supporting classes for configuration examples

public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class UserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class Product
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // Conflicting mappings target this
    public decimal Price { get; set; }
}

public class Customer
{
    public string Name { get; set; } = string.Empty;
}

public class Order
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public Customer Customer { get; set; } = new();
}

public class OrderDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string CustomerName { get; set; } = string.Empty; // Ignore vs MapFrom conflict
}

/// <summary>
///     Profile that should be registered but isn't (for AM040 example)
/// </summary>
public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"));
    }
}

/// <summary>
///     Examples of CORRECT configuration patterns (for comparison)
/// </summary>
public class CorrectConfigurationExamples
{
    public void CorrectProfileRegistrationExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Correct: Profile is properly registered
            cfg.AddProfile<UserMappingProfile>();
        });

        var mapper = config.CreateMapper();
        var user = new User { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com" };
        var userDto = mapper.Map<UserDto>(user);

        Console.WriteLine($"✅ Correctly mapped: {userDto.FullName}, Email: {userDto.Email}");
    }

    public void CorrectSingleMappingRuleExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Product, ProductDto>()
                // ✅ Correct: Single, clear mapping rule
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src =>
                    string.IsNullOrEmpty(src.Title) ? src.ProductName : src.Title));
        });

        var mapper = config.CreateMapper();
        var product = new Product { Id = 1, ProductName = "Widget", Title = "Amazing Widget", Price = 19.99m };
        var productDto = mapper.Map<ProductDto>(product);

        Console.WriteLine($"✅ Correctly mapped: Name={productDto.Name}, Price={productDto.Price}");
    }

    public void CorrectIgnoreOrMapExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Order, OrderDto>()
                // ✅ Correct: Either ignore OR map, not both
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer.Name));
            // OR: .ForMember(dest => dest.CustomerName, opt => opt.Ignore());
        });

        var mapper = config.CreateMapper();
        var order = new Order { Id = 1, Amount = 100.00m, Customer = new Customer { Name = "John Doe" } };
        var orderDto = mapper.Map<OrderDto>(order);

        Console.WriteLine($"✅ Correctly mapped: Order ID={orderDto.Id}, Customer={orderDto.CustomerName}");
    }
}
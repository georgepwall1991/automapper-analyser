using AutoMapper;

namespace AutoMapperAnalyzer.Samples.Performance;

/// <summary>
///     Examples of performance issues that AutoMapper analyzer will detect
/// </summary>
public class PerformanceExamples
{
    /// <summary>
    ///     AM050: Static Mapper Usage
    ///     This should trigger AM050 diagnostic
    /// </summary>
    public void StaticMapperUsageExample()
    {
        // Setup a basic configuration (old way - should use DI)
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Employee, EmployeeDto>();
        });

        IMapper? mapper = config.CreateMapper();

        var employee = new Employee { Id = 1, Name = "John Doe", Department = "Engineering" };

        // ❌ AM050: Creating mapper locally instead of using injected IMapper
        EmployeeDto? employeeDto = mapper.Map<EmployeeDto>(employee);

        Console.WriteLine($"Mapped: {employeeDto.Name} in {employeeDto.Department}");
        Console.WriteLine("❌ Local mapper creation detected - prefer dependency injection!");
    }

    /// <summary>
    ///     AM052: Missing Null Propagation
    ///     This should trigger AM052 diagnostic
    /// </summary>
    public void MissingNullPropagationExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Company, CompanyDto>()
                // ❌ AM052: Missing null propagation - Address could be null
                .ForMember(dest => dest.City, opt => opt.MapFrom(src => src.Address.City))
                .ForMember(dest => dest.Country, opt => opt.MapFrom(src => src.Address.Country));
        });

        IMapper? mapper = config.CreateMapper();

        var company = new Company
        {
            Id = 1, Name = "TechCorp", Address = null // This will cause NullReferenceException!
        };

        try
        {
            CompanyDto? companyDto = mapper.Map<CompanyDto>(company);
            Console.WriteLine($"Mapped: {companyDto.Name}, {companyDto.City}, {companyDto.Country}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Runtime error: {ex.Message}");
        }
    }

    /// <summary>
    ///     AM051: Repeated Mapping Configuration (future implementation)
    ///     This would trigger AM051 diagnostic when implemented
    /// </summary>
    public void RepeatedMappingConfigurationExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ❌ AM051: Same mapping configured multiple times
            cfg.CreateMap<Customer, CustomerDto>();

            // Later in configuration...
            cfg.CreateMap<Customer, CustomerDto>(); // Duplicate!
        });

        IMapper? mapper = config.CreateMapper();

        var customer = new Customer { Id = 1, Name = "Jane Smith", Email = "jane@example.com" };
        CustomerDto? customerDto = mapper.Map<CustomerDto>(customer);

        Console.WriteLine($"Mapped: {customerDto.Name}, Email: {customerDto.Email}");
        Console.WriteLine("❌ Duplicate mapping configuration detected!");
    }
}

// Supporting classes for performance examples

public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}

public class EmployeeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}

public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class Company
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Address? Address { get; set; } // Nullable - can cause NRE in mapping
}

public class CompanyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class CustomerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

/// <summary>
///     Examples of CORRECT performance patterns (for comparison)
/// </summary>
public class CorrectPerformanceExamples
{
    private readonly IMapper _mapper;

    public CorrectPerformanceExamples(IMapper mapper)
    {
        _mapper = mapper;
    }

    public void CorrectDependencyInjectionExample()
    {
        var employee = new Employee { Id = 1, Name = "John Doe", Department = "Engineering" };

        // ✅ Correct: Using injected IMapper
        EmployeeDto? employeeDto = _mapper.Map<EmployeeDto>(employee);

        Console.WriteLine($"✅ Correctly mapped: {employeeDto.Name} in {employeeDto.Department}");
    }

    public void CorrectNullPropagationExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Company, CompanyDto>()
                // ✅ Correct: Null-safe mapping
                .ForMember(dest => dest.City,
                    opt => opt.MapFrom(src => src.Address != null ? src.Address.City : string.Empty))
                .ForMember(dest => dest.Country,
                    opt => opt.MapFrom(src => src.Address != null ? src.Address.Country : string.Empty));
        });

        IMapper? mapper = config.CreateMapper();

        var company = new Company { Id = 1, Name = "TechCorp", Address = null };

        CompanyDto? companyDto = mapper.Map<CompanyDto>(company);
        Console.WriteLine($"✅ Correctly mapped: {companyDto.Name}, {companyDto.City}, {companyDto.Country}");
    }

    public void CorrectSingleMappingConfigurationExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Correct: Single mapping configuration
            cfg.CreateMap<Customer, CustomerDto>();

            // Different mappings are fine
            cfg.CreateMap<Employee, EmployeeDto>();
        });

        IMapper? mapper = config.CreateMapper();

        var customer = new Customer { Id = 1, Name = "Jane Smith", Email = "jane@example.com" };
        CustomerDto? customerDto = mapper.Map<CustomerDto>(customer);

        Console.WriteLine($"✅ Correctly configured: {customerDto.Name}, Email: {customerDto.Email}");
    }
}

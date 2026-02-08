using AutoMapper;

namespace AutoMapperAnalyzer.Samples.Performance;

/// <summary>
///     Additional runtime/performance anti-pattern examples.
///     Note: dedicated analyzer diagnostics in this project come from AM031 and AM050.
/// </summary>
public class PerformanceExamples
{
    /// <summary>
    ///     Local mapper creation is a runtime smell, but no dedicated rule currently reports it.
    /// </summary>
    public void StaticMapperUsageExample()
    {
        // Setup a basic configuration (old way - should use DI)
        var config = new MapperConfiguration(cfg => { cfg.CreateMap<Employee, EmployeeDto>(); });

        var mapper = config.CreateMapper();

        var employee = new Employee { Id = 1, Name = "John Doe", Department = "Engineering" };

        // ❌ Runtime smell: creating mapper locally instead of using injected IMapper
        var employeeDto = mapper.Map<EmployeeDto>(employee);

        Console.WriteLine($"Mapped: {employeeDto.Name} in {employeeDto.Department}");
        Console.WriteLine("❌ Local mapper creation detected - prefer dependency injection!");
    }

    /// <summary>
    ///     Null-unsafe map expressions can fail at runtime.
    ///     This scenario is shown for education; no dedicated rule currently reports it.
    /// </summary>
    public void MissingNullPropagationExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Company, CompanyDto>()
                // ❌ Runtime risk: missing null propagation (Address can be null)
                .ForMember(dest => dest.City, opt => opt.MapFrom(src => src.Address.City))
                .ForMember(dest => dest.Country, opt => opt.MapFrom(src => src.Address.Country));
        });

        var mapper = config.CreateMapper();

        var company = new Company
        {
            Id = 1, Name = "TechCorp", Address = null // This will cause NullReferenceException!
        };

        try
        {
            var companyDto = mapper.Map<CompanyDto>(company);
            Console.WriteLine($"Mapped: {companyDto.Name}, {companyDto.City}, {companyDto.Country}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Runtime error: {ex.Message}");
        }
    }

    /// <summary>
    ///     AM041: Duplicate CreateMap registration.
    ///     This should trigger an AM041 diagnostic.
    /// </summary>
    public void RepeatedMappingConfigurationExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ❌ AM041: Same mapping configured multiple times.
            cfg.CreateMap<Customer, CustomerDto>();

            // Later in configuration...
            cfg.CreateMap<Customer, CustomerDto>(); // Duplicate!
        });

        var mapper = config.CreateMapper();

        var customer = new Customer { Id = 1, Name = "Jane Smith", Email = "jane@example.com" };
        var customerDto = mapper.Map<CustomerDto>(customer);

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
        var employeeDto = _mapper.Map<EmployeeDto>(employee);

        Console.WriteLine($"✅ Correctly mapped: {employeeDto.Name} in {employeeDto.Department}");
    }

    public void CorrectNullPropagationExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
#pragma warning disable AM041
            cfg.CreateMap<Company, CompanyDto>()
                // ✅ Correct: Null-safe mapping
                .ForMember(dest => dest.City,
                    opt => opt.MapFrom(src => src.Address != null ? src.Address.City : string.Empty))
                .ForMember(dest => dest.Country,
                    opt => opt.MapFrom(src => src.Address != null ? src.Address.Country : string.Empty));
#pragma warning restore AM041
        });

        var mapper = config.CreateMapper();

        var company = new Company { Id = 1, Name = "TechCorp", Address = null };

        var companyDto = mapper.Map<CompanyDto>(company);
        Console.WriteLine($"✅ Correctly mapped: {companyDto.Name}, {companyDto.City}, {companyDto.Country}");
    }

    public void CorrectSingleMappingConfigurationExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // ✅ Correct: Single mapping configuration
#pragma warning disable AM041
            cfg.CreateMap<Customer, CustomerDto>();

            // Different mappings are fine
            cfg.CreateMap<Employee, EmployeeDto>();
#pragma warning restore AM041
        });

        var mapper = config.CreateMapper();

        var customer = new Customer { Id = 1, Name = "Jane Smith", Email = "jane@example.com" };
        var customerDto = mapper.Map<CustomerDto>(customer);

        Console.WriteLine($"✅ Correctly configured: {customerDto.Name}, Email: {customerDto.Email}");
    }
}

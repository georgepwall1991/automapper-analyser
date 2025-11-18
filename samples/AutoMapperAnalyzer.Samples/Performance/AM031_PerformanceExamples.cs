using AutoMapper;

namespace AutoMapperAnalyzer.Samples.Performance;

/// <summary>
///     Examples of AM031 performance warnings that the analyzer will detect
/// </summary>
public class AM031_PerformanceExamples
{
    /// <summary>
    ///     AM031: Database Call in MapFrom
    ///     This should trigger AM031 diagnostic
    /// </summary>
    public void DatabaseCallInMappingExample()
    {
        var dbContext = new SampleDbContext();

        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<UserEntity, UserDto>()
                // ❌ AM031: Database query in mapping - perform before mapping
                .ForMember(dest => dest.OrderCount,
                    opt => opt.MapFrom(src => dbContext.Orders.Count(o => o.UserId == src.Id)));
        });

        var mapper = config.CreateMapper();
        var user = new UserEntity { Id = 1, Name = "John Doe", Email = "john@example.com" };

        Console.WriteLine("❌ Database call in mapping detected - perform query before mapping!");

        // ✅ CORRECT WAY:
        // var orderCount = dbContext.Orders.Count(o => o.UserId == user.Id);
        // var enrichedUser = new UserEntity { ...user, OrderCount = orderCount };
        // var dto = mapper.Map<UserDto>(enrichedUser);
    }

    /// <summary>
    ///     AM031: External Method Call in MapFrom
    ///     This should trigger AM031 diagnostic
    /// </summary>
    public void ExternalMethodCallExample()
    {
        var externalService = new ExternalService();

        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Product, ProductDto>()
                // ❌ AM031: External service call in mapping
                .ForMember(dest => dest.EnrichedData,
                    opt => opt.MapFrom(src => externalService.EnrichProductData(src.Id)));
        });

        var mapper = config.CreateMapper();
        var product = new Product { Id = 1, Name = "Widget", Price = 19.99m };

        Console.WriteLine("❌ External method call in mapping detected!");

        // ✅ CORRECT WAY:
        // var enrichedData = externalService.EnrichProductData(product.Id);
        // var enrichedProduct = new Product { ...product, EnrichedData = enrichedData };
        // var dto = mapper.Map<ProductDto>(enrichedProduct);
    }

    /// <summary>
    ///     AM031: File I/O in MapFrom
    ///     This should trigger AM031 diagnostic
    /// </summary>
    public void FileIOInMappingExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Document, DocumentDto>()
                // ❌ AM031: File I/O operation in mapping
                .ForMember(dest => dest.Content,
                    opt => opt.MapFrom(src => File.Exists(src.FilePath) ? File.ReadAllText(src.FilePath) : ""));
        });

        var mapper = config.CreateMapper();
        var document = new Document { Id = 1, Title = "Report", FilePath = "/path/to/file.txt" };

        Console.WriteLine("❌ File I/O in mapping detected - load content before mapping!");
    }

    /// <summary>
    ///     AM031: Multiple Collection Enumerations
    ///     This should trigger AM031 diagnostic
    /// </summary>
    public void MultipleEnumerationsExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<SalesReport, SalesReportDto>()
                // ❌ AM031: Numbers collection enumerated twice (Sum + Average)
                .ForMember(dest => dest.TotalWithAverage,
                    opt => opt.MapFrom(src => src.Numbers.Sum() + src.Numbers.Average()));
        });

        var mapper = config.CreateMapper();
        var report = new SalesReport { Id = 1, Numbers = new List<int> { 1, 2, 3, 4, 5 } };

        Console.WriteLine("❌ Multiple enumeration detected - cache with ToList()!");

        // ✅ CORRECT WAY:
        // In the mapping:
        // .ForMember(dest => dest.TotalWithAverage, opt => opt.MapFrom(src =>
        // {
        //     var cache = src.Numbers.ToList();
        //     return cache.Sum() + cache.Average();
        // }))
    }

    /// <summary>
    ///     AM031: Task.Result Synchronous Access
    ///     This should trigger AM031 diagnostic
    /// </summary>
    public void TaskResultExample()
    {
        var asyncService = new AsyncDataService();

        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<AM031Customer, AM031CustomerDto>()
                // ❌ AM031: Task.Result can cause deadlocks
                .ForMember(dest => dest.ExternalData,
                    opt => opt.MapFrom(src => asyncService.GetDataAsync(src.Id).Result));
        });

        var mapper = config.CreateMapper();
        var customer = new AM031Customer { Id = 1, Name = "Jane Smith" };

        Console.WriteLine("❌ Task.Result usage detected - await async operations before mapping!");

        // ✅ CORRECT WAY:
        // var externalData = await asyncService.GetDataAsync(customer.Id);
        // var enrichedCustomer = new AM031Customer { ...customer, ExternalData = externalData };
        // var dto = mapper.Map<AM031CustomerDto>(enrichedCustomer);
    }

    /// <summary>
    ///     AM031: DateTime.Now in MapFrom
    ///     This should trigger AM031 diagnostic
    /// </summary>
    public void DateTimeNowExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<BlogPost, BlogPostDto>()
                // ❌ AM031: DateTime.Now is non-deterministic
                .ForMember(dest => dest.DaysOld,
                    opt => opt.MapFrom(src => (DateTime.Now - src.CreatedDate).Days));
        });

        var mapper = config.CreateMapper();
        var post = new BlogPost { Id = 1, Title = "My Post", CreatedDate = DateTime.Now.AddDays(-7) };

        Console.WriteLine("❌ DateTime.Now usage detected - compute before mapping for testability!");

        // ✅ CORRECT WAY:
        // var daysOld = (DateTime.Now - post.CreatedDate).Days;
        // var enrichedPost = new BlogPost { ...post, DaysOld = daysOld };
        // var dto = mapper.Map<BlogPostDto>(enrichedPost);
    }

    /// <summary>
    ///     AM031: Reflection in MapFrom
    ///     This should trigger AM031 diagnostic
    /// </summary>
    public void ReflectionExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<DynamicEntity, DynamicEntityDto>()
                // ❌ AM031: Reflection is expensive
                .ForMember(dest => dest.TypeName,
                    opt => opt.MapFrom(src => src.Data != null ? src.Data.GetType().Name : "Unknown"));
        });

        var mapper = config.CreateMapper();
        var entity = new DynamicEntity { Id = 1, Data = new { Value = "test" } };

        Console.WriteLine("❌ Reflection usage detected - determine type before mapping!");
    }

    /// <summary>
    ///     AM031: HTTP Request in MapFrom
    ///     This should trigger AM031 diagnostic
    /// </summary>
    public void HttpRequestExample()
    {
        var httpClient = new HttpClient();

        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<ApiReference, ApiReferenceDto>()
                // ❌ AM031: HTTP request in mapping
                .ForMember(dest => dest.ApiResponse,
                    opt => opt.MapFrom(src => httpClient.GetStringAsync(src.ApiUrl).Result));
        });

        var mapper = config.CreateMapper();
        var apiRef = new ApiReference { Id = 1, ApiUrl = "https://api.example.com/data" };

        Console.WriteLine("❌ HTTP request in mapping detected!");
    }

    /// <summary>
    ///     AM031: Complex LINQ Operation
    ///     This should trigger AM031 diagnostic
    /// </summary>
    public void ComplexLinqExample()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<DataContainer, DataContainerDto>()
                // ❌ AM031: Complex SelectMany with nested operations
                .ForMember(dest => dest.FilteredCount,
                    opt => opt.MapFrom(src => src.NestedData
                        .SelectMany(list => list.Where(item => item.Length > 5))
                        .Count()));
        });

        var mapper = config.CreateMapper();
        var container = new DataContainer
        {
            Id = 1,
            NestedData = new List<List<string>>
            {
                new() { "short", "verylongstring", "mid" },
                new() { "anotherlongstring", "x" }
            }
        };

        Console.WriteLine("❌ Complex LINQ operation detected - simplify before mapping!");
    }
}

// Supporting classes for AM031 examples

public class SampleDbContext
{
    public List<Order> Orders { get; set; } = new();
}

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal Total { get; set; }
}

public class UserEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int OrderCount { get; set; }
}

public class ExternalService
{
    public string EnrichProductData(int productId)
    {
        return $"Enriched data for {productId}";
    }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string EnrichedData { get; set; } = string.Empty;
}

public class Document
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

public class DocumentDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class SalesReport
{
    public int Id { get; set; }
    public List<int> Numbers { get; set; } = new();
}

public class SalesReportDto
{
    public int Id { get; set; }
    public double TotalWithAverage { get; set; }
}

public class AsyncDataService
{
    public Task<string> GetDataAsync(int id)
    {
        return Task.FromResult($"Data for {id}");
    }
}

public class AM031Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class AM031CustomerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ExternalData { get; set; } = string.Empty;
}

public class BlogPost
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

public class BlogPostDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int DaysOld { get; set; }
}

public class DynamicEntity
{
    public int Id { get; set; }
    public object? Data { get; set; }
}

public class DynamicEntityDto
{
    public int Id { get; set; }
    public string TypeName { get; set; } = string.Empty;
}

public class ApiReference
{
    public int Id { get; set; }
    public string ApiUrl { get; set; } = string.Empty;
}

public class ApiReferenceDto
{
    public int Id { get; set; }
    public string ApiResponse { get; set; } = string.Empty;
}

public class DataContainer
{
    public int Id { get; set; }
    public List<List<string>> NestedData { get; set; } = new();
}

public class DataContainerDto
{
    public int Id { get; set; }
    public int FilteredCount { get; set; }
}
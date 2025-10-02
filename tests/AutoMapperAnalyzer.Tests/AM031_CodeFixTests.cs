using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests;

public class AM031_CodeFixTests
{
    [Fact]
    public async Task AM031_ShouldSuggestMovingDatabaseCallOutsideMapping()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Linq;

                                namespace TestNamespace
                                {
                                    public class DbContext
                                    {
                                        public IQueryable<Order> Orders { get; set; }
                                    }

                                    public class Order
                                    {
                                        public int Id { get; set; }
                                    }

                                    public class Source
                                    {
                                        public int UserId { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int OrderCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly DbContext _db;

                                        public TestProfile(DbContext db)
                                        {
                                            _db = db;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.OrderCount, opt => opt.MapFrom(src => _db.Orders.Count(o => o.Id == src.UserId)));
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class DbContext
                                             {
                                                 public IQueryable<Order> Orders { get; set; }
                                             }

                                             public class Order
                                             {
                                                 public int Id { get; set; }
                                             }

                                             public class Source
                                             {
                                                 public int UserId { get; set; }
                                                 public int OrderCount { get; set; } // TODO: Populate this property before mapping
                                             }

                                             public class Destination
                                             {
                                                 public int OrderCount { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 private readonly DbContext _db;

                                                 public TestProfile(DbContext db)
                                                 {
                                                     _db = db;
                                                     CreateMap<Source, Destination>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithCodeFix<AM031_PerformanceWarningCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 35, 102)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldSuggestMovingMethodCallOutsideMapping()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ExternalService
                                    {
                                        public string GetData(int id) => "data";
                                    }

                                    public class Source
                                    {
                                        public int Id { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Data { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly ExternalService _service;

                                        public TestProfile(ExternalService service)
                                        {
                                            _service = service;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Data, opt => opt.MapFrom(src => _service.GetData(src.Id)));
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System;

                                         namespace TestNamespace
                                         {
                                             public class ExternalService
                                             {
                                                 public string GetData(int id) => "data";
                                             }

                                             public class Source
                                             {
                                                 public int Id { get; set; }
                                                 public string Data { get; set; } // TODO: Populate this property before mapping
                                             }

                                             public class Destination
                                             {
                                                 public string Data { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 private readonly ExternalService _service;

                                                 public TestProfile(ExternalService service)
                                                 {
                                                     _service = service;
                                                     CreateMap<Source, Destination>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithCodeFix<AM031_PerformanceWarningCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 29, 90)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldSuggestCachingForMultipleEnumerations()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Linq;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<int> Numbers { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Total { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Total, opt => opt.MapFrom(src => src.Numbers.Sum() + src.Numbers.Average()));
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public List<int> Numbers { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public int Total { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>()
                                                         .ForMember(dest => dest.Total, opt => opt.MapFrom(src =>
                                                         {
                                                             var numbersCache = src.Numbers.ToList();
                                                             return numbersCache.Sum() + numbersCache.Average();
                                                         }));
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithCodeFix<AM031_PerformanceWarningCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 22, 95)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldSuggestInjectingTimeProvider()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public DateTime CreatedDate { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int DaysOld { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.DaysOld, opt => opt.MapFrom(src => (DateTime.Now - src.CreatedDate).Days));
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public DateTime CreatedDate { get; set; }
                                                 public int DaysOld { get; set; } // TODO: Calculate before mapping using DateTime.Now
                                             }

                                             public class Destination
                                             {
                                                 public int DaysOld { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithCodeFix<AM031_PerformanceWarningCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 21, 95)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldSuggestMovingTaskResultOutsideMapping()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Threading.Tasks;

                                namespace TestNamespace
                                {
                                    public class DataService
                                    {
                                        public Task<string> GetDataAsync(int id) => Task.FromResult("data");
                                    }

                                    public class Source
                                    {
                                        public int Id { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Data { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly DataService _service;

                                        public TestProfile(DataService service)
                                        {
                                            _service = service;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Data, opt => opt.MapFrom(src => _service.GetDataAsync(src.Id).Result));
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System.Threading.Tasks;

                                         namespace TestNamespace
                                         {
                                             public class DataService
                                             {
                                                 public Task<string> GetDataAsync(int id) => Task.FromResult("data");
                                             }

                                             public class Source
                                             {
                                                 public int Id { get; set; }
                                                 public string Data { get; set; } // TODO: Await async operation before mapping
                                             }

                                             public class Destination
                                             {
                                                 public string Data { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 private readonly DataService _service;

                                                 public TestProfile(DataService service)
                                                 {
                                                     _service = service;
                                                     CreateMap<Source, Destination>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithCodeFix<AM031_PerformanceWarningCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.TaskResultSynchronousAccessRule, 29, 90)
            .ExpectFixedCode(expectedFixedCode)
            .RunAsync();
    }
}

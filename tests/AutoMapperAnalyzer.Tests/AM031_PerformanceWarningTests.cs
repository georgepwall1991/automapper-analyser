using AutoMapperAnalyzer.Analyzers;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests;

public class AM031_PerformanceWarningTests
{
    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenDatabaseCallInMapFrom()
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 35, 102,
                "OrderCount", "database query")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenMethodCallInMapFrom()
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 29, 90,
                "Data", "method call")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenFileIOInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string FilePath { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string FileContent { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.FileContent, opt => opt.MapFrom(src => File.ReadAllText(src.FilePath)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 98,
                "FileContent", "file I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenMultipleLinqEnumerationsInMapFrom()
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 22, 95,
                "Total", "Numbers")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenReflectionInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public object Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string TypeName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.TypeName, opt => opt.MapFrom(src => src.Data.GetType().Name));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 96,
                "TypeName", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenExpensiveComputationInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Linq;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Number { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool IsPrime { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.IsPrime, opt => opt.MapFrom(src =>
                                                    src.Number > 1 && !Enumerable.Range(2, (int)Math.Sqrt(src.Number) - 1).Any(i => src.Number % i == 0)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveComputationRule, 22, 94,
                "IsPrime")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSimplePropertyMapping()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public int Age { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public int Age { get; set; }
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSimpleStringOperations()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string FirstName { get; set; }
                                        public string LastName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string FullName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSimpleArithmeticOperations()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Width { get; set; }
                                        public int Height { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Area { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Area, opt => opt.MapFrom(src => src.Width * src.Height));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenHttpClientCallInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Net.Http;
                                using System.Threading.Tasks;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Url { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Response { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly HttpClient _client;

                                        public TestProfile(HttpClient client)
                                        {
                                            _client = client;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Response, opt => opt.MapFrom(src => _client.GetStringAsync(src.Url).Result));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 25, 98,
                "Response", "HTTP request")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenTaskResultUsedInMapFrom()
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.TaskResultSynchronousAccessRule, 29, 90,
                "Data")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenToListCalledMultipleTimes()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Linq;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public IEnumerable<int> Numbers { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Count { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Count, opt => opt.MapFrom(src => src.Numbers.ToList().Count + src.Numbers.ToList().Sum()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 22, 95,
                "Count", "Numbers")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenSelectManyWithComplexLogic()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Linq;

                                namespace TestNamespace
                                {
                                    public class Item
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Source
                                    {
                                        public List<List<Item>> NestedItems { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int TotalItems { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.TotalItems, opt => opt.MapFrom(src =>
                                                    src.NestedItems.SelectMany(list => list.Where(item => item.Name.Length > 5)).Count()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ComplexLinqOperationRule, 27, 98,
                "TotalItems")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSimpleLinqExtension()
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
                                        public int Count { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Count, opt => opt.MapFrom(src => src.Numbers.Count));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenDateTimeNowInMapFrom()
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

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 21, 95,
                "DaysOld", "DateTime.Now")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenRandomInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int RandomValue { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.RandomValue, opt => opt.MapFrom(src => new Random().Next()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 21, 101,
                "RandomValue", "Random")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSingleLinqOperation()
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
                                        public int Sum { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Sum, opt => opt.MapFrom(src => src.Numbers.Sum()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForConditionalExpression()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int? Age { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string AgeCategory { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.AgeCategory, opt => opt.MapFrom(src =>
                                                    src.Age == null ? "Unknown" : src.Age < 18 ? "Minor" : "Adult"));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenDictionaryLookupInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class LookupService
                                    {
                                        public Dictionary<int, string> GetLookupData() => new Dictionary<int, string>();
                                    }

                                    public class Source
                                    {
                                        public int CategoryId { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string CategoryName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly LookupService _service;

                                        public TestProfile(LookupService service)
                                        {
                                            _service = service;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => _service.GetLookupData()[src.CategoryId]));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 29, 105,
                "CategoryName", "method call")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForNullCoalescingWithSimpleAccess()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string DisplayName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.Name ?? "Unknown"));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }
}

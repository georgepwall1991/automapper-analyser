using AutoMapperAnalyzer.Analyzers.Performance;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests.Performance;

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
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 35, 72,
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
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 29, 66,
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
                                                .ForMember(dest => dest.FileContent, opt => opt.MapFrom(src => System.IO.File.ReadAllText(src.FilePath)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 73,
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
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 22, 67,
                "Total", "Numbers")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenChainedWhereTerminalsEnumerateSameSourceCollection()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Linq;

                                namespace TestNamespace
                                {
                                    public class Item { public bool Active { get; set; } }

                                    public class Source
                                    {
                                        public List<Item> Items { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Score { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Score, opt => opt.MapFrom(src => src.Items.Where(x => x.Active).Count() + (src.Items.Where(x => !x.Active).Any() ? 1 : 0)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 24, 67,
                "Score", "Items")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_WhenLinqChainsAreRootedAtSourceMethodCalls()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Linq;

                                namespace TestNamespace
                                {
                                    public class Item { public bool Active { get; set; } }

                                    public class Source
                                    {
                                        public List<Item> GetItems() => new List<Item>();
                                    }

                                    public class Destination
                                    {
                                        public int Score { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Score, opt => opt.MapFrom(src => src.GetItems().Where(x => x.Active).Count() + (src.GetItems().Where(x => !x.Active).Any() ? 1 : 0)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_WhenChainedMethodIsAUserDefinedWhereNamesake()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Collections.Generic;
                                using System.Linq;

                                namespace TestNamespace
                                {
                                    public class Item { public bool Active { get; set; } }

                                    public static class ItemListExtensions
                                    {
                                        // User-defined namesake — eagerly materializes an independent list.
                                        public static List<Item> Where(this List<Item> items, Func<Item, bool> predicate)
                                        {
                                            return Enumerable.Where(items, predicate).ToList();
                                        }
                                    }

                                    public class Source
                                    {
                                        public List<Item> Items { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Score { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Score, opt => opt.MapFrom(src => src.Items.Where(x => x.Active).Count + (src.Items.Where(x => !x.Active).Count > 0 ? 1 : 0)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_WhenTerminalsAreCalledOnDistinctSourceMethodResults()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Linq;

                                namespace TestNamespace
                                {
                                    public class Item { public bool Active { get; set; } }

                                    public class Source
                                    {
                                        public List<Item> GetActiveItems() => new List<Item>();
                                        public List<Item> GetArchivedItems() => new List<Item>();
                                    }

                                    public class Destination
                                    {
                                        public int Score { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Score, opt => opt.MapFrom(src => src.GetActiveItems().Count() + (src.GetArchivedItems().Any() ? 1 : 0)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_WhenChainedWhereTerminalEnumeratesSameCollectionOnlyOnce()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Linq;

                                namespace TestNamespace
                                {
                                    public class Item { public bool Active { get; set; } }

                                    public class Source
                                    {
                                        public List<Item> Items { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Score { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Score, opt => opt.MapFrom(src => src.Items.Where(x => x.Active).Count()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_WhenMathMinAndMathMaxUsedInsideMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Low { get; set; }
                                        public int High { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Clamped { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Clamped, opt => opt.MapFrom(src => Math.Min(src.High, 100) + Math.Max(src.Low, 0)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenMinAndMaxEnumerateSameCollection()
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
                                        public int Range { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Range, opt => opt.MapFrom(src => src.Numbers.Max() - src.Numbers.Min()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 22, 67,
                "Range", "Numbers")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenAggregateAndCountEnumerateSameCollection()
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
                                        public double Average { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Average, opt => opt.MapFrom(src => (double)src.Numbers.Aggregate(0, (a, b) => a + b) / src.Numbers.LongCount()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 22, 69,
                "Average", "Numbers")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenSingleAndToHashSetEnumerateSameCollection()
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
                                        public int Result { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => src.Numbers.Single() + src.Numbers.ToHashSet().Count));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 22, 68,
                "Result", "Numbers")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenMultipleEnumerationUsesNonSrcParameterName()
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
                                                .ForMember(dest => dest.Total, opt => opt.MapFrom(source => source.Numbers.Sum() + source.Numbers.Average()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 22, 67,
                "Total", "Numbers")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenForPathMapFromEnumeratesCollectionMultipleTimes()
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

                                    public class StatsDto
                                    {
                                        public int Total { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public StatsDto Stats { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForPath(dest => dest.Stats.Total, opt => opt.MapFrom(src => src.Numbers.Sum() + src.Numbers.Average()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 27, 71,
                "Stats.Total", "Numbers")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSimpleForPathMapFrom()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Total { get; set; }
                                    }

                                    public class StatsDto
                                    {
                                        public int Total { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public StatsDto Stats { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForPath(dest => dest.Stats.Total, opt => opt.MapFrom(src => src.Total));
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
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 70,
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
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveComputationRule, 22, 69,
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
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 25, 70,
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
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.TaskResultSynchronousAccessRule, 29, 66,
                "Data")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenTaskResultUsedOnTaskPropertyInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Threading.Tasks;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Task<string> DataTask { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Data { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Data, opt => opt.MapFrom(src => src.DataTask.Result));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.TaskResultSynchronousAccessRule, 21, 66,
                "Data")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenValueTaskResultUsedOnPropertyInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Threading.Tasks;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public ValueTask<string> DataTask { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Data { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Data, opt => opt.MapFrom(src => src.DataTask.Result));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.TaskResultSynchronousAccessRule, 21, 66,
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
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 22, 67,
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
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ComplexLinqOperationRule, 27, 72,
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
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 21, 69,
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
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 21, 73,
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
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 29, 74,
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

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForCreateMapLikeApiOutsideAutoMapper()
    {
        const string testCode = """
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

                                    public class FakeMapOptions<TSource, TDestMember>
                                    {
                                        public void MapFrom(Func<TSource, TDestMember> resolver)
                                        {
                                        }
                                    }

                                    public class FakeMapExpression<TSource, TDestination>
                                    {
                                        public FakeMapExpression<TSource, TDestination> ForMember<TDestMember>(
                                            Func<TDestination, TDestMember> destinationMember,
                                            Action<FakeMapOptions<TSource, TDestMember>> optionsAction)
                                        {
                                            return this;
                                        }
                                    }

                                    public class Profile
                                    {
                                        public FakeMapExpression<TSource, TDestination> CreateMap<TSource, TDestination>() => new();
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly ExternalService _service = new();

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Data, opt => opt.MapFrom(src => _service.GetData(src.Id)));
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
    public async Task AM031_ShouldReportDiagnostic_ForParenthesizedLambdasInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class DataService
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
                                        private readonly DataService _service;

                                        public TestProfile(DataService service)
                                        {
                                            _service = service;
                                            CreateMap<Source, Destination>()
                                                .ForMember((dest) => dest.Data, (opt) => opt.MapFrom((src) => _service.GetData(src.Id)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 28, 70,
                "Data", "method call")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_ForTaskResultOnStaticAsyncMethod()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Threading.Tasks;

                                namespace TestNamespace
                                {
                                    public static class DataService
                                    {
                                        public static Task<string> GetDataAsync(int id) => Task.FromResult("data");
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
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Data, opt => opt.MapFrom(src => DataService.GetDataAsync(src.Id).Result));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.TaskResultSynchronousAccessRule, 26, 66,
                "Data")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenTaskWaitUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Threading.Tasks;

                                namespace TestNamespace
                                {
                                    public static class DataService
                                    {
                                        public static Task<string> GetDataAsync(int id) => Task.FromResult("data");
                                    }

                                    public class Source
                                    {
                                        public int Id { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool Completed { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Completed, opt => opt.MapFrom(src => DataService.GetDataAsync(src.Id).Wait(1000)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.TaskResultSynchronousAccessRule, 26, 71,
                "Completed")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForDeterministicMethodWithRandomInName()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public static class ValueTransformations
                                    {
                                        public static int RandomizeSeed(int value) => value + 1;
                                    }

                                    public class Source
                                    {
                                        public int Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Result { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => ValueTransformations.RandomizeSeed(src.Value)));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForDelegateInvokeOnSourceProperty()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Id { get; set; }
                                        public Func<int, string> Formatter { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Data { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Data, opt => opt.MapFrom(src => src.Formatter.Invoke(src.Id)));
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
    public async Task AM031_ShouldReportDiagnostic_WhenDateTimeUtcNowInMapFrom()
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
                                                .ForMember(dest => dest.DaysOld, opt => opt.MapFrom(src => (DateTime.UtcNow - src.CreatedDate).Days));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 21, 69,
                "DaysOld", "DateTime.UtcNow")
            .RunAsync();
    }
}

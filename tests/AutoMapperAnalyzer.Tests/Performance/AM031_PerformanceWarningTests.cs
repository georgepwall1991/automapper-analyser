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
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeWhoseNameContainsDbContext()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class DbContextCache
                                    {
                                        public int GetCachedCount(int userId) => userId;
                                    }

                                    public class Source
                                    {
                                        public int UserId { get; set; }
                                        public DbContextCache Cache { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int OrderCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.OrderCount, opt => opt.MapFrom(source => source.Cache.GetCachedCount(source.UserId)));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeWhoseNameEndsWithDbContext()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class ReportingDbContext
                                    {
                                        public int GetCachedCount(int userId) => userId;
                                    }

                                    public class Source
                                    {
                                        public int UserId { get; set; }
                                        public ReportingDbContext Context { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int OrderCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.OrderCount, opt => opt.MapFrom(source => source.Context.GetCachedCount(source.UserId)));
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
    public async Task AM031_ShouldReportDiagnostic_WhenInjectedDbContextNamedTypeMethodCalledInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class ReportingDbContext
                                    {
                                        public int GetOrderCount(int userId) => userId;
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
                                        private readonly ReportingDbContext _db;

                                        public TestProfile(ReportingDbContext db)
                                        {
                                            _db = db;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.OrderCount, opt => opt.MapFrom(src => _db.GetOrderCount(src.UserId)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 28, 72,
                "OrderCount", "database query")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserDefinedDbSetNamesake()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class DbSet<T>
                                    {
                                        public int CachedCount() => 0;
                                    }

                                    public class Source
                                    {
                                        public DbSet<int> Numbers { get; set; }
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
                                                .ForMember(dest => dest.Count, opt => opt.MapFrom(source => source.Numbers.CachedCount()));
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
    public async Task AM031_ShouldReportDiagnostic_WhenEfCoreDbSetMethodCalledInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using Microsoft.EntityFrameworkCore;

                                namespace Microsoft.EntityFrameworkCore
                                {
                                    public class DbSet<T>
                                    {
                                        public T Find(int id) => default!;
                                    }
                                }

                                namespace TestNamespace
                                {
                                    using Microsoft.EntityFrameworkCore;

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
                                        public int OrderId { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly DbSet<Order> _orders;

                                        public TestProfile(DbSet<Order> orders)
                                        {
                                            _orders = orders;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => _orders.Find(src.UserId).Id));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 39, 69,
                "OrderId", "database query")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedSqlConnection()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SqlConnection
                                    {
                                        public string GetCachedName(int userId) => userId.ToString();
                                    }

                                    public class Source
                                    {
                                        public int UserId { get; set; }
                                        public SqlConnection Connection { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string ConnectionName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.ConnectionName, opt => opt.MapFrom(source => source.Connection.GetCachedName(source.UserId)));
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
    public async Task AM031_ShouldReportDiagnostic_WhenMicrosoftSqlConnectionCallInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace Microsoft.Data.SqlClient
                                {
                                    public class SqlConnection
                                    {
                                        public int ExecuteScalar() => 0;
                                    }
                                }

                                namespace TestNamespace
                                {
                                    using Microsoft.Data.SqlClient;

                                    public class Source
                                    {
                                        public int UserId { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Count { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly SqlConnection _connection;

                                        public TestProfile(SqlConnection connection)
                                        {
                                            _connection = connection;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Count, opt => opt.MapFrom(src => _connection.ExecuteScalar()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 33, 67,
                "Count", "database query")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForUserDefinedEntityFrameworkQueryableExtensionsNamesake()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Linq;

                                namespace TestNamespace
                                {
                                    public static class EntityFrameworkQueryableExtensions
                                    {
                                        public static int CachedCount<T>(this IEnumerable<T> source)
                                        {
                                            return source.Count();
                                        }
                                    }

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
                                                .ForMember(dest => dest.Count, opt => opt.MapFrom(source => source.Numbers.CachedCount()));
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
    public async Task AM031_ShouldReportDiagnostic_WhenEfCoreQueryableExtensionCalledInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Linq;
                                using Microsoft.EntityFrameworkCore;

                                namespace Microsoft.EntityFrameworkCore
                                {
                                    public static class EntityFrameworkQueryableExtensions
                                    {
                                        public static int CountAsync<T>(this IQueryable<T> source)
                                        {
                                            return source.Count();
                                        }
                                    }
                                }

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
                                                .ForMember(dest => dest.OrderCount, opt => opt.MapFrom(src => _db.Orders.CountAsync()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 46, 72,
                "OrderCount", "database query")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserDefinedDapperNamespaceType()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace Dapper
                                {
                                    public class Cache
                                    {
                                        public int GetCachedCount(int userId) => userId;
                                    }
                                }

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int UserId { get; set; }
                                        public Dapper.Cache Cache { get; set; }
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
                                                .ForMember(dest => dest.Count, opt => opt.MapFrom(source => source.Cache.GetCachedCount(source.UserId)));
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
    public async Task AM031_ShouldReportDiagnostic_WhenDapperSqlMapperCalledInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using Dapper;

                                namespace Dapper
                                {
                                    public static class SqlMapper
                                    {
                                        public static int QueryCount(this object connection, int userId) => userId;
                                    }
                                }

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int UserId { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Count { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly object _connection;

                                        public TestProfile(object connection)
                                        {
                                            _connection = connection;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Count, opt => opt.MapFrom(src => _connection.QueryCount(src.UserId)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 32, 67,
                "Count", "database query")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserDefinedNHibernateNamespaceType()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace NHibernate
                                {
                                    public class Cache
                                    {
                                        public int GetCachedCount(int userId) => userId;
                                    }
                                }

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int UserId { get; set; }
                                        public NHibernate.Cache Cache { get; set; }
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
                                                .ForMember(dest => dest.Count, opt => opt.MapFrom(source => source.Cache.GetCachedCount(source.UserId)));
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
    public async Task AM031_ShouldReportDiagnostic_WhenNHibernateSessionCalledInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace NHibernate
                                {
                                    public interface ISession
                                    {
                                        int CreateQuery(int userId);
                                    }
                                }

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int UserId { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Count { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly NHibernate.ISession _session;

                                        public TestProfile(NHibernate.ISession session)
                                        {
                                            _session = session;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Count, opt => opt.MapFrom(src => _session.CreateQuery(src.UserId)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 31, 67,
                "Count", "database query")
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
    public async Task AM031_ShouldReportDiagnostic_WhenStreamReaderReadInMapFrom()
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
                                                .ForMember(dest => dest.FileContent, opt => opt.MapFrom(src => new StreamReader(src.FilePath).ReadToEnd()));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenStringReaderReadInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Text { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string FirstLine { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.FirstLine, opt => opt.MapFrom(src => new StringReader(src.Text).ReadLine()));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenTextReaderLocalWrapsStringReaderInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Text { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Text { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Text, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using TextReader reader = new StringReader(src.Text);
                                                    return reader.ReadToEnd();
                                                }));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenMemoryStreamReadInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Bytes { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int FirstByte { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.FirstByte, opt => opt.MapFrom(src => new MemoryStream(src.Bytes).ReadByte()));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenStreamTypedLocalMemoryStreamReadsInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Bytes { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int FirstByte { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.FirstByte, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using Stream stream = new MemoryStream(src.Bytes);
                                                    return stream.ReadByte();
                                                }));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenStreamReaderReadsMemoryStreamInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Bytes { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Text { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Text, opt => opt.MapFrom(src => new StreamReader(new MemoryStream(src.Bytes)).ReadToEnd()));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenLocalStreamReaderReadsMemoryStreamInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Bytes { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Text { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Text, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var stream = new MemoryStream(src.Bytes);
                                                    using var reader = new StreamReader(stream);
                                                    return reader.ReadToEnd();
                                                }));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenStreamTypedLocalMemoryStreamFeedsStreamReaderInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Bytes { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Text { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Text, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using Stream stream = new MemoryStream(src.Bytes);
                                                    using var reader = new StreamReader(stream);
                                                    return reader.ReadToEnd();
                                                }));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenBinaryReaderReadsMemoryStreamInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Bytes { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Value { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Value, opt => opt.MapFrom(src => new BinaryReader(new MemoryStream(src.Bytes)).ReadInt32()));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenStringWriterWriteInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Text { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Normalized { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Normalized, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var writer = new StringWriter();
                                                    writer.Write(src.Text);
                                                    return writer.ToString();
                                                }));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenTextWriterLocalWrapsStringWriterInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Text { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Normalized { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Normalized, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using TextWriter writer = new StringWriter();
                                                    writer.Write(src.Text);
                                                    return writer.ToString();
                                                }));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenStreamTypedLocalMemoryStreamWritesInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public long Length { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Length, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using Stream stream = new MemoryStream();
                                                    stream.WriteByte((byte)src.Value);
                                                    return stream.Length;
                                                }));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenStreamTypedLocalMemoryStreamCopiesInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Bytes { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Length { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Length, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using Stream stream = new MemoryStream(src.Bytes);
                                                    using var output = new MemoryStream();
                                                    stream.CopyTo(output);
                                                    return (int)output.Length;
                                                }));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenBinaryWriterWritesMemoryStreamInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Length { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Length, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var stream = new MemoryStream();
                                                    using var writer = new BinaryWriter(stream);
                                                    writer.Write(src.Value);
                                                    return stream.ToArray().Length;
                                                }));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenStreamWriterWritesMemoryStreamInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Text { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Length { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Length, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var stream = new MemoryStream();
                                                    using var writer = new StreamWriter(stream);
                                                    writer.Write(src.Text);
                                                    writer.Flush();
                                                    return (int)stream.Length;
                                                }));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenMemoryStreamWriteInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Bytes { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Length { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Length, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var stream = new MemoryStream();
                                                    stream.Write(src.Bytes, 0, src.Bytes.Length);
                                                    return (int)stream.Length;
                                                }));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenPathCombineInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Folder { get; set; }
                                        public string FileName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string FullPath { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.FullPath, opt => opt.MapFrom(src => Path.Combine(src.Folder, src.FileName)));
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
    public async Task AM031_ShouldReportDiagnostic_WhenMemoryMappedFileViewAccessorUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO.MemoryMappedFiles;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public MemoryMappedFile MapFile { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public long Capacity { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Capacity, opt => opt.MapFrom(src => src.MapFile.CreateViewAccessor().Capacity));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 70,
                "Capacity", "file I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenMemoryMappedViewAccessorFlushUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO.MemoryMappedFiles;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public MemoryMappedViewAccessor Accessor { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool Flushed { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Flushed, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    src.Accessor.Flush();
                                                    return true;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 69,
                "Flushed", "file I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenFileStreamFlushUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public FileStream Stream { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool Flushed { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Flushed, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    src.Stream.Flush();
                                                    return true;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 69,
                "Flushed", "file I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenFileStreamSetLengthUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public FileStream Stream { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool Resized { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Resized, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    src.Stream.SetLength(0);
                                                    return true;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 69,
                "Resized", "file I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenFileStreamLockUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public FileStream Stream { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool Locked { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Locked, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    src.Stream.Lock(0, 1);
                                                    return true;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 68,
                "Locked", "file I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenFileStreamUnlockUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public FileStream Stream { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool Unlocked { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Unlocked, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    src.Stream.Unlock(0, 1);
                                                    return true;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 70,
                "Unlocked", "file I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenFileStreamCopyToUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public FileStream Stream { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Length { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Length, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var output = new MemoryStream();
                                                    src.Stream.CopyTo(output);
                                                    return (int)output.Length;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 68,
                "Length", "file I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenDirectoryInfoGetFilesUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Folder { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int FileCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.FileCount, opt => opt.MapFrom(src => new DirectoryInfo(src.Folder).GetFiles().Length));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 71,
                "FileCount", "file I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenFileInfoLengthUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Path { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public long FileSize { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.FileSize, opt => opt.MapFrom(src => new FileInfo(src.Path).Length));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 70,
                "FileSize", "file I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenDirectoryInfoExistsUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Folder { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool FolderExists { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.FolderExists, opt => opt.MapFrom(src => new DirectoryInfo(src.Folder).Exists));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 74,
                "FolderExists", "file I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenFileInfoTimestampUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Path { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public System.DateTime Timestamp { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Timestamp, opt => opt.MapFrom(src => new FileInfo(src.Path).LastWriteTimeUtc));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 71,
                "Timestamp", "file I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenDirectoryInfoTimestampUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Folder { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public System.DateTime Timestamp { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Timestamp, opt => opt.MapFrom(src => new DirectoryInfo(src.Folder).CreationTimeUtc));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 71,
                "Timestamp", "file I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenFileSystemInfoRefreshUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Path { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Path { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Path, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    var info = new FileInfo(src.Path);
                                                    info.Refresh();
                                                    return src.Path;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 66,
                "Path", "file I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenFileSystemInfoDeleteUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Path { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Path { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Path, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    FileSystemInfo info = new FileInfo(src.Path);
                                                    info.Delete();
                                                    return src.Path;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 66,
                "Path", "file I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenZipFileOpenReadUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO.Compression;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Path { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int EntryCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.EntryCount, opt => opt.MapFrom(src => ZipFile.OpenRead(src.Path).Entries.Count));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 72,
                "EntryCount", "file I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenZipFileExtractToDirectoryUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO.Compression;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Path { get; set; }
                                        public string TargetFolder { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Path { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Path, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    ZipFile.ExtractToDirectory(src.Path, src.TargetFolder);
                                                    return src.Path;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 22, 66,
                "Path", "file I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenZipFileCreateFromDirectoryUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO.Compression;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Folder { get; set; }
                                        public string Path { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Path { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Path, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    ZipFile.CreateFromDirectory(src.Folder, src.Path);
                                                    return src.Path;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 22, 66,
                "Path", "file I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenGZipStreamWriteUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;
                                using System.IO.Compression;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Compressed { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Compressed, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var output = new MemoryStream();
                                                    using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
                                                    {
                                                        gzip.Write(src.Data, 0, src.Data.Length);
                                                    }

                                                    return output.ToArray();
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 22, 72,
                "Compressed", "compression operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenBrotliStreamCopyToUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;
                                using System.IO.Compression;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Decompressed { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Decompressed, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var input = new MemoryStream(src.Data);
                                                    using var brotli = new BrotliStream(input, CompressionMode.Decompress);
                                                    using var output = new MemoryStream();
                                                    brotli.CopyTo(output);
                                                    return output.ToArray();
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 22, 74,
                "Decompressed", "compression operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserCompressionStreamNamesakes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Sink
                                    {
                                    }

                                    public class GZipStream
                                    {
                                        public void Write(byte[] buffer, int offset, int count)
                                        {
                                        }
                                    }

                                    public class BrotliStream
                                    {
                                        public void CopyTo(Sink destination)
                                        {
                                        }
                                    }

                                    public class Source
                                    {
                                        public byte[] Data { get; set; }
                                        public Sink Output { get; set; }
                                        public GZipStream GZip { get; set; }
                                        public BrotliStream Brotli { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Length { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Length, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    src.GZip.Write(src.Data, 0, src.Data.Length);
                                                    src.Brotli.CopyTo(src.Output);
                                                    return src.Data.Length;
                                                }));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedDirectoryInfo()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class DirectoryInfo
                                    {
                                        public string[] GetFiles() => new string[0];
                                    }

                                    public class Source
                                    {
                                        public DirectoryInfo Directory { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int FileCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.FileCount, opt => opt.MapFrom(src => src.Directory.GetFiles().Length));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedMemoryMappedFile()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class MemoryMappedFile
                                    {
                                        public Accessor CreateViewAccessor() => new Accessor();
                                    }

                                    public class Accessor
                                    {
                                        public long Capacity { get; set; }
                                    }

                                    public class Source
                                    {
                                        public MemoryMappedFile MapFile { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public long Capacity { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Capacity, opt => opt.MapFrom(src => src.MapFile.CreateViewAccessor().Capacity));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedMemoryMappedViewAccessor()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class MemoryMappedViewAccessor
                                    {
                                        public void Flush()
                                        {
                                        }
                                    }

                                    public class Source
                                    {
                                        public MemoryMappedViewAccessor Accessor { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool Flushed { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Flushed, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    src.Accessor.Flush();
                                                    return true;
                                                }));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedFileStream()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class FileStream
                                    {
                                        public void Flush()
                                        {
                                        }

                                        public void SetLength(long value)
                                        {
                                        }

                                        public void Lock(long position, long length)
                                        {
                                        }

                                        public void Unlock(long position, long length)
                                        {
                                        }
                                    }

                                    public class Source
                                    {
                                        public FileStream Stream { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool Flushed { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Flushed, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    src.Stream.Flush();
                                                    src.Stream.SetLength(0);
                                                    src.Stream.Lock(0, 1);
                                                    src.Stream.Unlock(0, 1);
                                                    return true;
                                                }));
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
    public async Task AM031_ShouldReportDiagnostic_WhenTwoCollectionsAreEachMultiplyEnumerated()
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
                                        public List<int> Scores { get; set; }
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
                                                .ForMember(dest => dest.Total, opt => opt.MapFrom(src => src.Numbers.Sum() + src.Numbers.Average() + src.Scores.Sum() + src.Scores.Average()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 23, 67,
                "Total", "Numbers")
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 23, 67,
                "Total", "Scores")
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
    public async Task AM031_ShouldReportDiagnostic_WhenContainsAndElementAtEnumerateSameCollection()
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
                                        public bool Result { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => src.Numbers.Contains(src.Numbers.ElementAt(0)) || src.Numbers.ElementAtOrDefault(1) == 0));
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
    public async Task AM031_ShouldReportDiagnostic_WhenListContainsAndSumEnumerateSameCollection()
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
                                        public bool Result { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => src.Numbers.Contains(5) && src.Numbers.Sum() > 0));
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
    public async Task AM031_ShouldReportDiagnostic_WhenSequenceEqualAndAnyEnumerateSameCollection()
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
                                        public List<int> OtherNumbers { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool Result { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => src.Numbers.SequenceEqual(src.OtherNumbers) && src.Numbers.Any()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 23, 68,
                "Result", "Numbers")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenSequenceEqualArgumentAndAnyEnumerateSameCollection()
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
                                        public List<int> OtherNumbers { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool Result { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => src.OtherNumbers.SequenceEqual(src.Numbers) && src.Numbers.Any()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 23, 68,
                "Result", "Numbers")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenSequenceEqualArgumentEnumeratesCapturedCollection()
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
                                        public bool Result { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            var numbers = new List<int> { 1, 2, 3 };

                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => src.Numbers.SequenceEqual(numbers) && numbers.Any()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 24, 68,
                "Result", "numbers")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenStaticSequenceEqualArgumentAndAnyEnumerateSameCollection()
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
                                        public List<int> OtherNumbers { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool Result { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => Enumerable.SequenceEqual(src.OtherNumbers, src.Numbers) && src.Numbers.Any()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 23, 68,
                "Result", "Numbers")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_WhenStaticLinqTerminalsEnumerateDifferentCollections()
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
                                        public List<int> OtherNumbers { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public double Result { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => Enumerable.Sum(src.Numbers) + Enumerable.Average(src.OtherNumbers)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .RunWithNoDiagnosticsAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenStaticLinqTerminalsEnumerateCapturedCollection()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Linq;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Id { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public double Result { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            var numbers = new List<int> { 1, 2, 3 };

                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => Enumerable.Sum(numbers) + Enumerable.Average(numbers)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 24, 68,
                "Result", "numbers")
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
    public async Task AM031_ShouldReportDiagnostic_WhenTypedForPathMapFromEnumeratesCollectionMultipleTimes()
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
                                                .ForPath((Destination dest) => dest.Stats.Total,
                                                    opt => opt.MapFrom((Source src) => src.Numbers.Sum() + src.Numbers.Average()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 28, 40,
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
    public async Task AM031_ShouldReportDiagnostic_WhenTypeGetPropertiesUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Type TargetType { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int PropertyCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.PropertyCount, opt => opt.MapFrom(src => src.TargetType.GetProperties().Length));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 75,
                "PropertyCount", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenAssemblyGetTypesUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int TypeCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.TypeCount, opt => opt.MapFrom(src => typeof(Source).Assembly.GetTypes().Length));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 71,
                "TypeCount", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenAssemblyLoadUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string AssemblyName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Assembly LoadedAssembly { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.LoadedAssembly, opt => opt.MapFrom(src => Assembly.Load(src.AssemblyName)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 76,
                "LoadedAssembly", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenAssemblyLoadContextLoadFromAssemblyPathUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Reflection;
                                using System.Runtime.Loader;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string AssemblyPath { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Assembly LoadedAssembly { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.LoadedAssembly, opt => opt.MapFrom(src => AssemblyLoadContext.Default.LoadFromAssemblyPath(src.AssemblyPath)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 22, 76,
                "LoadedAssembly", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenAssemblyLoadContextLoadFromStreamUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;
                                using System.Reflection;
                                using System.Runtime.Loader;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] AssemblyBytes { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Assembly LoadedAssembly { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.LoadedAssembly, opt => opt.MapFrom(src => AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(src.AssemblyBytes))));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 76,
                "LoadedAssembly", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenAssemblyNameGetAssemblyNameUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string AssemblyPath { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string AssemblyName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.AssemblyName, opt => opt.MapFrom(src => AssemblyName.GetAssemblyName(src.AssemblyPath).FullName));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 74,
                "AssemblyName", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenAssemblyCreateInstanceUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Assembly CurrentAssembly { get; set; }
                                        public string TypeName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public object Instance { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Instance, opt => opt.MapFrom(src => src.CurrentAssembly.CreateInstance(src.TypeName)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 22, 70,
                "Instance", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenAssemblyGetModulesUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Assembly CurrentAssembly { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int ModuleCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.ModuleCount, opt => opt.MapFrom(src => src.CurrentAssembly.GetModules().Length));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 73,
                "ModuleCount", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenAssemblyGetSatelliteAssemblyUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Globalization;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Assembly CurrentAssembly { get; set; }
                                        public string CultureName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Assembly SatelliteAssembly { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.SatelliteAssembly, opt => opt.MapFrom(src => src.CurrentAssembly.GetSatelliteAssembly(CultureInfo.GetCultureInfo(src.CultureName))));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 79,
                "SatelliteAssembly", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenAssemblyGetExecutingAssemblyUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Assembly CurrentAssembly { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.CurrentAssembly, opt => opt.MapFrom(src => Assembly.GetExecutingAssembly()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 77,
                "CurrentAssembly", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenTypeInvokeMemberUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Type TargetType { get; set; }
                                        public object Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public object Result { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => src.TargetType.InvokeMember("ToString", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod, null, src.Value, new object[0])));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 68,
                "Result", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenMemberInfoGetCustomAttributesDataUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public MemberInfo SourceMember { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int AttributeCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.AttributeCount, opt => opt.MapFrom(src => src.SourceMember.GetCustomAttributesData().Count));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 76,
                "AttributeCount", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenAttributeGetCustomAttributesUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public MemberInfo SourceMember { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int AttributeCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.AttributeCount, opt => opt.MapFrom(src => System.Attribute.GetCustomAttributes(src.SourceMember).Length));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 76,
                "AttributeCount", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenMemberInfoIsDefinedUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public MemberInfo SourceMember { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool HasAttribute { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.HasAttribute, opt => opt.MapFrom(src => src.SourceMember.IsDefined(typeof(System.ObsoleteAttribute), inherit: false)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 74,
                "HasAttribute", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenModuleResolveTypeUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Module SourceModule { get; set; }
                                        public int MetadataToken { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string ResolvedTypeName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.ResolvedTypeName, opt => opt.MapFrom(src => src.SourceModule.ResolveType(src.MetadataToken).Name));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 22, 78,
                "ResolvedTypeName", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenTypeGetTypeFromHandleUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public RuntimeTypeHandle TypeHandle { get; set; }
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
                                                .ForMember(dest => dest.TypeName, opt => opt.MapFrom(src => Type.GetTypeFromHandle(src.TypeHandle).Name));
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
    public async Task AM031_ShouldReportDiagnostic_WhenMethodBaseGetMethodFromHandleUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public RuntimeMethodHandle MethodHandle { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string MethodName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.MethodName, opt => opt.MapFrom(src => MethodBase.GetMethodFromHandle(src.MethodHandle).Name));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 22, 72,
                "MethodName", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenMethodBaseGetCurrentMethodUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string MethodName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.MethodName, opt => opt.MapFrom(src => MethodBase.GetCurrentMethod().Name));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 72,
                "MethodName", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenFieldInfoGetFieldFromHandleUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public RuntimeFieldHandle FieldHandle { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string FieldName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.FieldName, opt => opt.MapFrom(src => FieldInfo.GetFieldFromHandle(src.FieldHandle).Name));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 22, 71,
                "FieldName", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenReflectionEmitDynamicTypeUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Reflection;
                                using System.Reflection.Emit;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string TypeName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Type GeneratedType { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.GeneratedType, opt => opt.MapFrom(src =>
                                                    AssemblyBuilder
                                                        .DefineDynamicAssembly(new AssemblyName("DynamicMappings"), AssemblyBuilderAccess.Run)
                                                        .DefineDynamicModule("MainModule")
                                                        .DefineType(src.TypeName)
                                                        .CreateType()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 75,
                "GeneratedType", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenReflectionEmitIlGeneratorUsedInFuncMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Reflection.Emit;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int InstructionCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.InstructionCount, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    var method = new DynamicMethod("Map", typeof(int), Type.EmptyTypes);
                                                    var il = method.GetILGenerator();
                                                    il.Emit(OpCodes.Ldc_I4_1);
                                                    il.Emit(OpCodes.Ret);
                                                    return src.Value;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 22, 78,
                "InstructionCount", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenTypeGetTypeInfoUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Linq;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Type TargetType { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int DeclaredPropertyCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.DeclaredPropertyCount, opt => opt.MapFrom(src => src.TargetType.GetTypeInfo().DeclaredProperties.Count()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 83,
                "DeclaredPropertyCount", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenTypeGetRuntimeMethodsUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Linq;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Type TargetType { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int RuntimeMethodCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.RuntimeMethodCount, opt => opt.MapFrom(src => src.TargetType.GetRuntimeMethods().Count()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 80,
                "RuntimeMethodCount", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenTypeAssemblyPropertyUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Type TargetType { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string AssemblyName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.AssemblyName, opt => opt.MapFrom(src => src.TargetType.Assembly.FullName));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 74,
                "AssemblyName", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenTypeInfoDeclaredMethodsPropertyUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Linq;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Type TargetType { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int DeclaredMethodCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.DeclaredMethodCount, opt => opt.MapFrom(src => src.TargetType.GetTypeInfo().DeclaredMethods.Count()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 81,
                "DeclaredMethodCount", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenPropertyInfoPropertyTypeUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public PropertyInfo SourceProperty { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string SourcePropertyType { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.SourcePropertyType, opt => opt.MapFrom(src => src.SourceProperty.PropertyType.Name));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 80,
                "SourcePropertyType", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenMethodInfoReturnTypeUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public MethodInfo SourceMethod { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string SourceReturnType { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.SourceReturnType, opt => opt.MapFrom(src => src.SourceMethod.ReturnType.Name));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 78,
                "SourceReturnType", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenMethodInfoGetParametersUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public MethodInfo SourceMethod { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int ParameterCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.ParameterCount, opt => opt.MapFrom(src => src.SourceMethod.GetParameters().Length));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 76,
                "ParameterCount", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenTypeGetGenericArgumentsUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Type TargetType { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int GenericArgumentCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.GenericArgumentCount, opt => opt.MapFrom(src => src.TargetType.GetGenericArguments().Length));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 82,
                "GenericArgumentCount", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenTypeMakeGenericTypeUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Type TargetType { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string ConstructedTypeName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.ConstructedTypeName, opt => opt.MapFrom(src => typeof(List<>).MakeGenericType(src.TargetType).Name));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 22, 81,
                "ConstructedTypeName", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenMethodInfoMakeGenericMethodUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public MethodInfo SourceMethod { get; set; }
                                        public Type TargetType { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string ConstructedMethodName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.ConstructedMethodName, opt => opt.MapFrom(src => src.SourceMethod.MakeGenericMethod(src.TargetType).Name));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 83,
                "ConstructedMethodName", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenMethodInfoCreateDelegateUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Reflection;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public MethodInfo SourceMethod { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Delegate SourceDelegate { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.SourceDelegate, opt => opt.MapFrom(src => src.SourceMethod.CreateDelegate(typeof(Func<int>))));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 22, 76,
                "SourceDelegate", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenActivatorCreateInstanceUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Type TargetType { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public object Instance { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Instance, opt => opt.MapFrom(src => Activator.CreateInstance(src.TargetType)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 70,
                "Instance", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenExpressionCompileUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Linq.Expressions;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Value { get; set; }
                                        public Expression<Func<int, int>> Transform { get; set; }
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
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => src.Transform.Compile()(src.Value)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 68,
                "Result", "reflection operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForUserReflectionMetadataMethodNamesakes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class MethodInfo
                                    {
                                        public object[] GetParameters() => new object[0];
                                        public MethodInfo MakeGenericMethod(Type targetType) => this;
                                        public object CreateDelegate(Type targetType) => targetType;
                                    }

                                    public class Type
                                    {
                                        public object[] GetGenericArguments() => new object[0];
                                        public Type MakeGenericType(Type targetType) => this;
                                    }

                                    public class Source
                                    {
                                        public MethodInfo SourceMethod { get; set; }
                                        public Type TargetType { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int ParameterCount { get; set; }
                                        public int GenericArgumentCount { get; set; }
                                        public string ConstructedMethodName { get; set; }
                                        public object SourceDelegate { get; set; }
                                        public Type ConstructedType { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.ParameterCount, opt => opt.MapFrom(src => src.SourceMethod.GetParameters().Length))
                                                .ForMember(dest => dest.GenericArgumentCount, opt => opt.MapFrom(src => src.TargetType.GetGenericArguments().Length))
                                                .ForMember(dest => dest.ConstructedMethodName, opt => opt.MapFrom(src => src.SourceMethod.MakeGenericMethod(src.TargetType).ToString()))
                                                .ForMember(dest => dest.SourceDelegate, opt => opt.MapFrom(src => src.SourceMethod.CreateDelegate(src.TargetType)))
                                                .ForMember(dest => dest.ConstructedType, opt => opt.MapFrom(src => src.TargetType.MakeGenericType(src.TargetType)));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForUserReflectionTypePropertyNamesakes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class PropertyInfo
                                    {
                                        public TypeInfo PropertyType { get; set; }
                                    }

                                    public class MethodInfo
                                    {
                                        public TypeInfo ReturnType { get; set; }
                                    }

                                    public class TypeInfo
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Source
                                    {
                                        public PropertyInfo SourceProperty { get; set; }
                                        public MethodInfo SourceMethod { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string SourcePropertyType { get; set; }
                                        public string SourceReturnType { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.SourcePropertyType, opt => opt.MapFrom(src => src.SourceProperty.PropertyType.Name))
                                                .ForMember(dest => dest.SourceReturnType, opt => opt.MapFrom(src => src.SourceMethod.ReturnType.Name));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForUserReflectionMetadataPropertyNamesakes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Assembly
                                    {
                                        public string FullName { get; set; }
                                    }

                                    public class Type
                                    {
                                        public Assembly Assembly { get; set; }
                                    }

                                    public class Source
                                    {
                                        public Type TargetType { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string AssemblyName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.AssemblyName, opt => opt.MapFrom(src => src.TargetType.Assembly.FullName));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForUserReflectionExtensionNamesakes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public static class ReflectionExtensions
                                    {
                                        public static TypeInfo GetTypeInfo(this Type type) => new();
                                        public static object[] GetRuntimeMethods(this Type type) => new object[0];
                                    }

                                    public class Type
                                    {
                                    }

                                    public class TypeInfo
                                    {
                                        public object[] DeclaredProperties { get; } = new object[0];
                                    }

                                    public class Source
                                    {
                                        public Type TargetType { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int DeclaredPropertyCount { get; set; }
                                        public int RuntimeMethodCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.DeclaredPropertyCount, opt => opt.MapFrom(src => src.TargetType.GetTypeInfo().DeclaredProperties.Length))
                                                .ForMember(dest => dest.RuntimeMethodCount, opt => opt.MapFrom(src => src.TargetType.GetRuntimeMethods().Length));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForUserReflectionLookupNamesakes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Type
                                    {
                                        public object[] GetProperties() => new object[0];
                                    }

                                    public class Assembly
                                    {
                                        public object[] GetTypes() => new object[0];
                                    }

                                    public class Source
                                    {
                                        public Type TargetType { get; set; }
                                        public Assembly SourceAssembly { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int PropertyCount { get; set; }
                                        public int TypeCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.PropertyCount, opt => opt.MapFrom(src => src.TargetType.GetProperties().Length))
                                                .ForMember(dest => dest.TypeCount, opt => opt.MapFrom(src => src.SourceAssembly.GetTypes().Length));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForUserAssemblyLoadNamesakes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Assembly
                                    {
                                        public string FullName { get; set; }

                                        public static Assembly Load(string name) => new Assembly { FullName = name };
                                        public static Assembly GetExecutingAssembly() => new Assembly { FullName = "Current" };
                                    }

                                    public class Source
                                    {
                                        public string AssemblyName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Assembly LoadedAssembly { get; set; }
                                        public string CurrentAssemblyName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.LoadedAssembly, opt => opt.MapFrom(src => Assembly.Load(src.AssemblyName)))
                                                .ForMember(dest => dest.CurrentAssemblyName, opt => opt.MapFrom(src => Assembly.GetExecutingAssembly().FullName));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForUserAssemblyCreateInstanceNamesake()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Assembly
                                    {
                                        public object CreateInstance(string typeName) => typeName;
                                    }

                                    public class Source
                                    {
                                        public Assembly CurrentAssembly { get; set; }
                                        public string TypeName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public object Instance { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Instance, opt => opt.MapFrom(src => src.CurrentAssembly.CreateInstance(src.TypeName)));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForUserAssemblyGetModulesNamesake()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Assembly
                                    {
                                        public string[] GetModules() => new[] { "Core" };
                                    }

                                    public class Source
                                    {
                                        public Assembly CurrentAssembly { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int ModuleCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.ModuleCount, opt => opt.MapFrom(src => src.CurrentAssembly.GetModules().Length));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForUserAssemblyGetSatelliteAssemblyNamesake()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Assembly
                                    {
                                        public string FullName { get; set; }

                                        public Assembly GetSatelliteAssembly(string cultureName) => new Assembly { FullName = cultureName };
                                    }

                                    public class Source
                                    {
                                        public Assembly CurrentAssembly { get; set; }
                                        public string CultureName { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string SatelliteAssemblyName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.SatelliteAssemblyName, opt => opt.MapFrom(src => src.CurrentAssembly.GetSatelliteAssembly(src.CultureName).FullName));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForUserAssemblyNameGetAssemblyNameNamesake()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class AssemblyName
                                    {
                                        public string FullName { get; set; }

                                        public AssemblyName GetAssemblyName(string path) => new AssemblyName { FullName = path };
                                    }

                                    public class Source
                                    {
                                        public AssemblyName NameReader { get; set; }
                                        public string AssemblyPath { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string AssemblyName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.AssemblyName, opt => opt.MapFrom(src => src.NameReader.GetAssemblyName(src.AssemblyPath).FullName));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForUserAssemblyLoadContextNamesakes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Assembly
                                    {
                                        public string FullName { get; set; }
                                    }

                                    public class Stream
                                    {
                                    }

                                    public class MemoryStream : Stream
                                    {
                                        public MemoryStream(byte[] buffer) { }
                                    }

                                    public class AssemblyLoadContext
                                    {
                                        public static AssemblyLoadContext Default { get; } = new AssemblyLoadContext();

                                        public Assembly LoadFromAssemblyPath(string path) => new Assembly { FullName = path };
                                        public Assembly LoadFromStream(Stream stream) => new Assembly { FullName = "Stream" };
                                    }

                                    public class Source
                                    {
                                        public AssemblyLoadContext Context { get; set; }
                                        public string AssemblyPath { get; set; }
                                        public byte[] AssemblyBytes { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Assembly LoadedAssembly { get; set; }
                                        public Assembly StreamAssembly { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.LoadedAssembly, opt => opt.MapFrom(src => src.Context.LoadFromAssemblyPath(src.AssemblyPath)))
                                                .ForMember(dest => dest.StreamAssembly, opt => opt.MapFrom(src => src.Context.LoadFromStream(new MemoryStream(src.AssemblyBytes))));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForUserReflectionInvocationAndResolutionNamesakes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Type
                                    {
                                        public object InvokeMember(string name, int flags, object binder, object target, object[] args) => target;
                                    }

                                    public class MemberInfo
                                    {
                                        public object[] GetCustomAttributesData() => new object[0];
                                    }

                                    public class Module
                                    {
                                        public Type ResolveType(int metadataToken) => new Type();
                                    }

                                    public class Source
                                    {
                                        public Type TargetType { get; set; }
                                        public MemberInfo SourceMember { get; set; }
                                        public Module SourceModule { get; set; }
                                        public int MetadataToken { get; set; }
                                        public object Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public object Result { get; set; }
                                        public int AttributeCount { get; set; }
                                        public string ResolvedTypeName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => src.TargetType.InvokeMember("ToString", 0, null, src.Value, new object[0])))
                                                .ForMember(dest => dest.AttributeCount, opt => opt.MapFrom(src => src.SourceMember.GetCustomAttributesData().Length))
                                                .ForMember(dest => dest.ResolvedTypeName, opt => opt.MapFrom(src => src.SourceModule.ResolveType(src.MetadataToken).ToString()));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForUserReflectionHandleLookupNamesakes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public readonly struct RuntimeTypeHandle { }
                                    public readonly struct RuntimeMethodHandle { }
                                    public readonly struct RuntimeFieldHandle { }

                                    public class Type
                                    {
                                        public string Name { get; set; }

                                        public static Type GetTypeFromHandle(RuntimeTypeHandle handle) => new Type { Name = "Source" };
                                    }

                                    public class MethodBase
                                    {
                                        public string Name { get; set; }

                                        public static MethodBase GetMethodFromHandle(RuntimeMethodHandle handle) => new MethodBase { Name = "Map" };
                                        public MethodBase GetCurrentMethod() => this;
                                    }

                                    public class FieldInfo
                                    {
                                        public string Name { get; set; }

                                        public static FieldInfo GetFieldFromHandle(RuntimeFieldHandle handle) => new FieldInfo { Name = "Value" };
                                    }

                                    public class Source
                                    {
                                        public RuntimeTypeHandle TypeHandle { get; set; }
                                        public RuntimeMethodHandle MethodHandle { get; set; }
                                        public RuntimeFieldHandle FieldHandle { get; set; }
                                        public MethodBase SourceMethod { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string TypeName { get; set; }
                                        public string MethodName { get; set; }
                                        public string CurrentMethodName { get; set; }
                                        public string FieldName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.TypeName, opt => opt.MapFrom(src => Type.GetTypeFromHandle(src.TypeHandle).Name))
                                                .ForMember(dest => dest.MethodName, opt => opt.MapFrom(src => MethodBase.GetMethodFromHandle(src.MethodHandle).Name))
                                                .ForMember(dest => dest.CurrentMethodName, opt => opt.MapFrom(src => src.SourceMethod.GetCurrentMethod().Name))
                                                .ForMember(dest => dest.FieldName, opt => opt.MapFrom(src => FieldInfo.GetFieldFromHandle(src.FieldHandle).Name));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForUserReflectionEmitNamesakes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class AssemblyBuilder
                                    {
                                        public static AssemblyBuilder DefineDynamicAssembly(string name, int access) => new AssemblyBuilder();
                                        public ModuleBuilder DefineDynamicModule(string name) => new ModuleBuilder();
                                    }

                                    public class ModuleBuilder
                                    {
                                        public TypeBuilder DefineType(string name) => new TypeBuilder { Name = name };
                                    }

                                    public class TypeBuilder
                                    {
                                        public string Name { get; set; }

                                        public TypeBuilder CreateType() => this;
                                    }

                                    public class DynamicMethod
                                    {
                                        public ILGenerator GetILGenerator() => new ILGenerator();
                                    }

                                    public class ILGenerator
                                    {
                                        public void Emit(int opcode) { }
                                    }

                                    public class Source
                                    {
                                        public string TypeName { get; set; }
                                        public int Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public TypeBuilder GeneratedType { get; set; }
                                        public int InstructionCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.GeneratedType, opt => opt.MapFrom(src => AssemblyBuilder.DefineDynamicAssembly("DynamicMappings", 1).DefineDynamicModule("MainModule").DefineType(src.TypeName).CreateType()))
                                                .ForMember(dest => dest.InstructionCount, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    var method = new DynamicMethod();
                                                    var il = method.GetILGenerator();
                                                    il.Emit(src.Value);
                                                    return src.Value;
                                                }));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForUserAttributeLookupNamesakes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Type
                                    {
                                    }

                                    public class MemberInfo
                                    {
                                        public bool IsDefined(Type attributeType, bool inherit) => inherit || attributeType is not null;
                                    }

                                    public static class Attribute
                                    {
                                        public static object[] GetCustomAttributes(MemberInfo member) => new object[0];
                                        public static bool IsDefined(MemberInfo member, Type attributeType) => member.IsDefined(attributeType, inherit: false);
                                    }

                                    public class Source
                                    {
                                        public MemberInfo SourceMember { get; set; }
                                        public Type AttributeType { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int AttributeCount { get; set; }
                                        public bool HasAttribute { get; set; }
                                        public bool StaticHasAttribute { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.AttributeCount, opt => opt.MapFrom(src => Attribute.GetCustomAttributes(src.SourceMember).Length))
                                                .ForMember(dest => dest.HasAttribute, opt => opt.MapFrom(src => src.SourceMember.IsDefined(src.AttributeType, false)))
                                                .ForMember(dest => dest.StaticHasAttribute, opt => opt.MapFrom(src => Attribute.IsDefined(src.SourceMember, src.AttributeType)));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForUserActivatorAndExpressionNamesakes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public static class Activator
                                    {
                                        public static object CreateInstance(string name) => name;
                                    }

                                    public class Expression<TDelegate>
                                    {
                                        public TDelegate Compile() => default;
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        public Expression<System.Func<int, int>> Transform { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public object Instance { get; set; }
                                        public System.Func<int, int> Transform { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Instance, opt => opt.MapFrom(src => Activator.CreateInstance(src.Name)))
                                                .ForMember(dest => dest.Transform, opt => opt.MapFrom(src => src.Transform.Compile()));
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
    public async Task AM031_ShouldReportDiagnostic_WhenProcessStartUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Diagnostics;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Command { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Command { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Command, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    Process.Start(src.Command);
                                                    return src.Command;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 69,
                "Command", "process launch")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenProcessKillUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Diagnostics;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Code { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Code { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Code, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    var process = Process.GetCurrentProcess();
                                                    process.Kill();
                                                    return src.Code;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 66,
                "Code", "process control operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenProcessWaitForExitUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Diagnostics;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Code { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Code { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Code, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    var process = Process.GetCurrentProcess();
                                                    process.WaitForExit(1);
                                                    return src.Code;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 66,
                "Code", "blocking process operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenEnvironmentExitUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Code { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Code { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Code, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    Environment.Exit(src.Code);
                                                    return src.Code;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 66,
                "Code", "process termination")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenEnvironmentFailFastUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Message { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Message { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Message, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    Environment.FailFast(src.Message);
                                                    return src.Message;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 69,
                "Message", "process termination")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenGcCollectUsedInMapFrom()
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
                                        public int Value { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Value, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    GC.Collect();
                                                    return src.Value;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 67,
                "Value", "GC operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenGcWaitForPendingFinalizersUsedInMapFrom()
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
                                        public int Value { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Value, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    GC.WaitForPendingFinalizers();
                                                    return src.Value;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 67,
                "Value", "GC operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedProcess()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Process
                                    {
                                        public string Start(string command) => command;
                                    }

                                    public class Source
                                    {
                                        public string Command { get; set; }
                                        public Process Launcher { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Command { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Command, opt => opt.MapFrom(src => src.Launcher.Start(src.Command)));
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
    public async Task AM031_ShouldReportDiagnostic_WhenConsoleWriteLineUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Value { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Value, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    Console.WriteLine(src.Value);
                                                    return src.Value;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 67,
                "Value", "console I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedConsole()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Console
                                    {
                                        public string WriteLine(string value) => value;
                                    }

                                    public class Source
                                    {
                                        public string Value { get; set; }
                                        public Console Logger { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Value { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.Logger.WriteLine(src.Value)));
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
    public async Task AM031_ShouldReportDiagnostic_WhenThreadSleepUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Threading;

                                namespace TestNamespace
                                {
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
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    Thread.Sleep(10);
                                                    return src.Value;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 68,
                "Result", "blocking thread operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenSpinWaitSpinUntilUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Threading;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public bool Ready { get; set; }
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
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    SpinWait.SpinUntil(() => src.Ready, 1);
                                                    return src.Value;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 22, 68,
                "Result", "blocking thread operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedSpinWait()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SpinWait
                                    {
                                        public bool SpinUntil(System.Func<bool> condition) => condition();
                                    }

                                    public class Source
                                    {
                                        public bool Ready { get; set; }
                                        public int Value { get; set; }
                                        public SpinWait Waiter { get; set; }
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
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => src.Waiter.SpinUntil(() => src.Ready) ? src.Value : 0));
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
    public async Task AM031_ShouldReportDiagnostic_WhenThreadJoinUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Threading;

                                namespace TestNamespace
                                {
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
                                        private readonly Thread _worker = new Thread(() => { });

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    _worker.Join(10);
                                                    return src.Value;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 68,
                "Result", "blocking thread operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenWaitHandleWaitOneUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Threading;

                                namespace TestNamespace
                                {
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
                                        private readonly WaitHandle _ready = new ManualResetEvent(false);

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    _ready.WaitOne(10);
                                                    return src.Value;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 68,
                "Result", "blocking thread operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenMonitorWaitUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Threading;

                                namespace TestNamespace
                                {
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
                                        private readonly object _gate = new object();

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    Monitor.Wait(_gate, 10);
                                                    return src.Value;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 68,
                "Result", "blocking thread operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenSemaphoreSlimWaitUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Threading;

                                namespace TestNamespace
                                {
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
                                        private readonly SemaphoreSlim _gate = new SemaphoreSlim(0);

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    _gate.Wait(10);
                                                    return src.Value;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 68,
                "Result", "blocking thread operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenManualResetEventSlimWaitUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Threading;

                                namespace TestNamespace
                                {
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
                                        private readonly ManualResetEventSlim _ready = new ManualResetEventSlim(false);

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    _ready.Wait(10);
                                                    return src.Value;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 68,
                "Result", "blocking thread operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenReaderWriterLockSlimEnterWriteLockUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Threading;

                                namespace TestNamespace
                                {
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
                                        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    _lock.EnterWriteLock();
                                                    return src.Value;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 68,
                "Result", "blocking thread operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedThread()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Thread
                                    {
                                        public int Sleep(int value) => value;
                                    }

                                    public class Source
                                    {
                                        public Thread Worker { get; set; }
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
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => src.Worker.Sleep(10)));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedSemaphoreSlim()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SemaphoreSlim
                                    {
                                        public int Wait(int value) => value;
                                    }

                                    public class Source
                                    {
                                        public SemaphoreSlim Gate { get; set; }
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
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => src.Gate.Wait(10)));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedReaderWriterLockSlim()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class ReaderWriterLockSlim
                                    {
                                        public int EnterWriteLock(int value) => value;
                                    }

                                    public class Source
                                    {
                                        public ReaderWriterLockSlim Lock { get; set; }
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
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => src.Lock.EnterWriteLock(10)));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedWaitHandle()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class WaitHandle
                                    {
                                        public int WaitOne(int value) => value;
                                    }

                                    public class Source
                                    {
                                        public WaitHandle Signal { get; set; }
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
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => src.Signal.WaitOne(10)));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForUserDefinedEnumerableRangeNamesake()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public static class Enumerable
                                    {
                                        public static int Range(int start, int count) => start + count;
                                    }

                                    public class Source
                                    {
                                        public int Number { get; set; }
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
                                                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => Enumerable.Range(src.Number, 1)));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeWhoseNameContainsHttpClient()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class HttpClientCache
                                    {
                                        public string GetCachedResponse(string key) => key;
                                    }

                                    public class Source
                                    {
                                        public string Key { get; set; }
                                        public HttpClientCache Cache { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Response { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Response, opt => opt.MapFrom(source => source.Cache.GetCachedResponse(source.Key)));
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
    public async Task AM031_ShouldReportDiagnostic_WhenHttpMessageInvokerSendAsyncUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Net.Http;
                                using System.Threading;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Url { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string StatusCode { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly HttpMessageInvoker _invoker;

                                        public TestProfile(HttpMessageInvoker invoker)
                                        {
                                            _invoker = invoker;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.StatusCode, opt => opt.MapFrom(src =>
                                                    _invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, src.Url), CancellationToken.None).Result.StatusCode.ToString()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 25, 72,
                "StatusCode", "HTTP request")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_WhenHttpClientCancelPendingRequestsUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Net.Http;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Url { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Url { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly HttpClient _client;

                                        public TestProfile(HttpClient client)
                                        {
                                            _client = client;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Url, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    _client.CancelPendingRequests();
                                                    return src.Url;
                                                }));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenHttpMessageInvokerDisposeUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Net.Http;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Url { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Url { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly HttpMessageInvoker _invoker;

                                        public TestProfile(HttpMessageInvoker invoker)
                                        {
                                            _invoker = invoker;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Url, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    _invoker.Dispose();
                                                    return src.Url;
                                                }));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenHttpClientHeaderConfigurationUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Net.Http;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Url { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Url { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly HttpClient _client;

                                        public TestProfile(HttpClient client)
                                        {
                                            _client = client;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Url, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    _client.DefaultRequestHeaders.Clear();
                                                    return src.Url;
                                                }));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenHttpClientHeaderValueCollectionConfigurationUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Net.Http;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Url { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Url { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly HttpClient _client;

                                        public TestProfile(HttpClient client)
                                        {
                                            _client = client;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Url, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    _client.DefaultRequestHeaders.UserAgent.ParseAdd("AutoMapperAnalyzer/1.0");
                                                    return src.Url;
                                                }));
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
    public async Task AM031_ShouldReportDiagnostic_WhenWebClientDownloadStringUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Net;

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
                                        private readonly WebClient _client;

                                        public TestProfile(WebClient client)
                                        {
                                            _client = client;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Response, opt => opt.MapFrom(src => _client.DownloadString(src.Url)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 24, 70,
                "Response", "HTTP request")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenHttpContentReadUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Net.Http;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Id { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Body { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly HttpContent _content;

                                        public TestProfile(HttpContent content)
                                        {
                                            _content = content;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Body, opt => opt.MapFrom(src => _content.ReadAsStringAsync().Result));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 24, 66,
                "Body", "HTTP request")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportOnlyTaskResult_ForSourceRootedUserTypeNamedHttpContent()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Threading.Tasks;

                                namespace TestNamespace
                                {
                                    public class HttpContent
                                    {
                                        public Task<string> ReadAsStringAsync() => Task.FromResult("cached");
                                    }

                                    public class Source
                                    {
                                        public HttpContent Content { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Body { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Body, opt => opt.MapFrom(source => source.Content.ReadAsStringAsync().Result));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.TaskResultSynchronousAccessRule, 26, 66,
                "Body")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenHttpClientJsonExtensionUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Net.Http;
                                using System.Net.Http.Json;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Url { get; set; }
                                    }

                                    public class Payload
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly HttpClient _client;

                                        public TestProfile(HttpClient client)
                                        {
                                            _client = client;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => _client.GetFromJsonAsync<Payload>(src.Url).Result.Name));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 30, 66,
                "Name", "HTTP request")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportHttpDiagnostic_ForLocalHttpClientJsonExtensionsNamesake()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Payload
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class JsonClient
                                    {
                                    }

                                    public static class HttpClientJsonExtensions
                                    {
                                        public static Payload GetFromJsonAsync(this JsonClient client, string path) => new Payload { Name = path };
                                    }

                                    public class Source
                                    {
                                        public string Path { get; set; }
                                        public JsonClient Client { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom(source => source.Client.GetFromJsonAsync(source.Path).Name));
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
    public async Task AM031_ShouldReportDiagnostic_WhenWebRequestGetResponseUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Net;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Url { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string ResponseUri { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly WebRequest _request;

                                        public TestProfile(WebRequest request)
                                        {
                                            _request = request;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.ResponseUri, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var response = _request.GetResponse();
                                                    return response.ResponseUri.ToString();
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 24, 73,
                "ResponseUri", "HTTP request")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedWebRequest()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class WebResponse
                                    {
                                        public string ResponseUri { get; set; }
                                    }

                                    public class WebRequest
                                    {
                                        public WebResponse GetResponse() => new WebResponse { ResponseUri = "cached" };
                                    }

                                    public class Source
                                    {
                                        public WebRequest Request { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string ResponseUri { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.ResponseUri, opt => opt.MapFrom(source => source.Request.GetResponse().ResponseUri));
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
    public async Task AM031_ShouldReportDiagnostic_WhenDnsLookupUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Net;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Host { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int AddressCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.AddressCount, opt => opt.MapFrom(src => Dns.GetHostAddresses(src.Host).Length));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 74,
                "AddressCount", "network lookup")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedDns()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Dns
                                    {
                                        public int GetHostAddresses(string host) => host.Length;
                                    }

                                    public class Source
                                    {
                                        public string Host { get; set; }
                                        public Dns Resolver { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int AddressCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.AddressCount, opt => opt.MapFrom(source => source.Resolver.GetHostAddresses(source.Host)));
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
    public async Task AM031_ShouldReportDiagnostic_WhenTcpClientConnectUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Net.Sockets;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Host { get; set; }
                                        public int Port { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool Connected { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly TcpClient _client;

                                        public TestProfile(TcpClient client)
                                        {
                                            _client = client;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Connected, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    _client.Connect(src.Host, src.Port);
                                                    return _client.Connected;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 25, 71,
                "Connected", "network I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedTcpClient()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class TcpClient
                                    {
                                        public bool Connect(string host, int port) => true;
                                    }

                                    public class Source
                                    {
                                        public string Host { get; set; }
                                        public int Port { get; set; }
                                        public TcpClient Client { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool Connected { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Connected, opt => opt.MapFrom(source => source.Client.Connect(source.Host, source.Port)));
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
    public async Task AM031_ShouldReportDiagnostic_WhenPingSendUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Net.NetworkInformation;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Host { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Status { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly Ping _ping;

                                        public TestProfile(Ping ping)
                                        {
                                            _ping = ping;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Status, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    PingReply reply = _ping.Send(src.Host);
                                                    return reply.Status.ToString();
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 24, 68,
                "Status", "network I/O operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedPing()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class PingReply
                                    {
                                        public string Status { get; set; }
                                    }

                                    public class Ping
                                    {
                                        public PingReply Send(string host) => new PingReply { Status = host };
                                    }

                                    public class Source
                                    {
                                        public string Host { get; set; }
                                        public Ping Probe { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Status { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Status, opt => opt.MapFrom(source => source.Probe.Send(source.Host).Status));
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
    public async Task AM031_ShouldNotReportDiagnostic_WhenUserDefinedSelectManyNamesakeHasNestedInvocation()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Item
                                    {
                                        public string Name { get; set; }
                                    }

                                    public static class ItemExtensions
                                    {
                                        public static List<Item> SelectMany(
                                            this List<List<Item>> groups,
                                            Func<List<Item>, List<Item>> selector)
                                        {
                                            return selector(groups[0]);
                                        }

                                        public static List<Item> ActiveOnly(this List<Item> items) => items;
                                    }

                                    public class Source
                                    {
                                        public List<List<Item>> Groups { get; set; }
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
                                                    src.Groups.SelectMany(group => group.ActiveOnly()).Count));
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
    public async Task AM031_ShouldReportDiagnostic_WhenTypedLambdaMapFromUsesDateTimeNow()
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
                                                .ForMember((Destination dest) => dest.DaysOld,
                                                    opt => opt.MapFrom((Source src) => (DateTime.Now - src.CreatedDate).Days));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 22, 40,
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
    public async Task AM031_ShouldReportDiagnostic_WhenRandomNumberGeneratorUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Security.Cryptography;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Token { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Token, opt => opt.MapFrom(src => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 22, 67,
                "Token", "RandomNumberGenerator")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenDateTimeOffsetUtcNowInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Id { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public DateTimeOffset ObservedAt { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.ObservedAt, opt => opt.MapFrom(src => DateTimeOffset.UtcNow));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 21, 72,
                "ObservedAt", "DateTimeOffset.UtcNow")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenStopwatchGetTimestampUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Diagnostics;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Id { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public long Timestamp { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Timestamp, opt => opt.MapFrom(src => Stopwatch.GetTimestamp()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 21, 71,
                "Timestamp", "Stopwatch")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedStopwatch()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Stopwatch
                                    {
                                        public long GetTimestamp() => 42;
                                    }

                                    public class Source
                                    {
                                        public Stopwatch Clock { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public long Timestamp { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Timestamp, opt => opt.MapFrom(src => src.Clock.GetTimestamp()));
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
    public async Task AM031_ShouldReportDiagnostic_WhenEnvironmentVariableUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Id { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Region { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Region, opt => opt.MapFrom(src => Environment.GetEnvironmentVariable("REGION")));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 21, 68,
                "Region", "Environment")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenEnvironmentTickCountUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Id { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public long TickCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.TickCount, opt => opt.MapFrom(src => Environment.TickCount64));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 21, 71,
                "TickCount", "Environment.TickCount64")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenEnvironmentMachineNameUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Id { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Machine { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Machine, opt => opt.MapFrom(src => Environment.MachineName));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 21, 69,
                "Machine", "Environment.MachineName")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenEnvironmentCurrentDirectoryUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Id { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Directory { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Directory, opt => opt.MapFrom(src => Environment.CurrentDirectory));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 21, 71,
                "Directory", "Environment.CurrentDirectory")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenEnvironmentCommandLineArgsUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Id { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int ArgumentCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.ArgumentCount, opt => opt.MapFrom(src => Environment.GetCommandLineArgs().Length));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 21, 75,
                "ArgumentCount", "Environment")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenEnvironmentLogicalDrivesUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Id { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int DriveCount { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.DriveCount, opt => opt.MapFrom(src => Environment.GetLogicalDrives().Length));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 21, 72,
                "DriveCount", "Environment")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenEnvironmentVariableSetInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Region { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Region { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Region, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    Environment.SetEnvironmentVariable("REGION", src.Region);
                                                    return src.Region;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 21, 68,
                "Region", "Environment")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenEnvironmentExitCodeSetInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Code { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Code { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Code, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    Environment.ExitCode = src.Code;
                                                    return src.Code;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 21, 66,
                "Code", "Environment.ExitCode")
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
    public async Task AM031_ShouldReportDiagnostic_WhenGetAwaiterGetResultUsedInMapFrom()
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
                                                .ForMember(dest => dest.Data, opt => opt.MapFrom(src => DataService.GetDataAsync(src.Id).GetAwaiter().GetResult()));
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
    public async Task AM031_ShouldReportDiagnostic_WhenConfiguredTaskGetResultUsedInMapFrom()
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
                                                .ForMember(dest => dest.Data, opt => opt.MapFrom(src => DataService.GetDataAsync(src.Id).ConfigureAwait(false).GetAwaiter().GetResult()));
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
    public async Task AM031_ShouldReportDiagnostic_WhenValueTaskGetAwaiterGetResultUsedInMapFrom()
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
                                                .ForMember(dest => dest.Data, opt => opt.MapFrom(src => src.DataTask.GetAwaiter().GetResult()));
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
    public async Task AM031_ShouldReportDiagnostic_WhenConfiguredValueTaskGetResultUsedInMapFrom()
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
                                                .ForMember(dest => dest.Data, opt => opt.MapFrom(src => src.DataTask.ConfigureAwait(false).GetAwaiter().GetResult()));
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
    public async Task AM031_ShouldReportDiagnostic_WhenTaskRunUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Threading.Tasks;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Value { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Value, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    Task.Run(() => src.Value);
                                                    return src.Value;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 67,
                "Value", "background work scheduling")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenTaskFactoryStartNewUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Threading.Tasks;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Value { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Value, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    Task.Factory.StartNew(() => src.Value);
                                                    return src.Value;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 67,
                "Value", "background work scheduling")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenThreadPoolQueueUserWorkItemUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Threading;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Value { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Value { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Value, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    ThreadPool.QueueUserWorkItem(_ =>
                                                    {
                                                        var value = src.Value;
                                                    });
                                                    return src.Value;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 67,
                "Value", "background work scheduling")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenJsonSerializeUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Text.Json;

                                namespace TestNamespace
                                {
                                    public class Payload
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Source
                                    {
                                        public Payload Payload { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Json { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Json, opt => opt.MapFrom(src => JsonSerializer.Serialize(src.Payload)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 26, 66,
                "Json", "serialization operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenJsonDeserializeUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Text.Json;

                                namespace TestNamespace
                                {
                                    public class Payload
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Json { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => JsonSerializer.Deserialize<Payload>(src.Json)!.Name));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 26, 66,
                "Name", "serialization operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenJsonSerializeToNodeUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Text.Json;

                                namespace TestNamespace
                                {
                                    public class Payload
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Source
                                    {
                                        public Payload Payload { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Json { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Json, opt => opt.MapFrom(src => JsonSerializer.SerializeToNode(src.Payload)!.ToJsonString()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 26, 66,
                "Json", "serialization operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenJsonDeserializeAsyncEnumerableUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;
                                using System.Text.Json;

                                namespace TestNamespace
                                {
                                    public class Payload
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Source
                                    {
                                        public Stream Json { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public object Values { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Values, opt => opt.MapFrom(src => JsonSerializer.DeserializeAsyncEnumerable<Payload>(src.Json)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 27, 68,
                "Values", "serialization operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenXmlDeserializeUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;
                                using System.Xml.Serialization;

                                namespace TestNamespace
                                {
                                    public class Payload
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Xml { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => ((Payload)new XmlSerializer(typeof(Payload)).Deserialize(new StringReader(src.Xml))!).Name));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 27, 66,
                "Name", "serialization operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenXmlSerializeUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;
                                using System.Xml.Serialization;

                                namespace TestNamespace
                                {
                                    public class Payload
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Source
                                    {
                                        public Payload Payload { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Xml { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Xml, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    var serializer = new XmlSerializer(typeof(Payload));
                                                    using var writer = new StringWriter();
                                                    serializer.Serialize(writer, src.Payload);
                                                    return writer.ToString();
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 27, 65,
                "Xml", "serialization operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenDataContractReadObjectUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;
                                using System.Runtime.Serialization;

                                namespace TestNamespace
                                {
                                    [DataContract]
                                    public class Payload
                                    {
                                        [DataMember]
                                        public string Name { get; set; }
                                    }

                                    public class Source
                                    {
                                        public byte[] Xml { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => ((Payload)new DataContractSerializer(typeof(Payload)).ReadObject(new MemoryStream(src.Xml))!).Name));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 29, 66,
                "Name", "serialization operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenDataContractWriteObjectUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;
                                using System.Runtime.Serialization;

                                namespace TestNamespace
                                {
                                    [DataContract]
                                    public class Payload
                                    {
                                        [DataMember]
                                        public string Name { get; set; }
                                    }

                                    public class Source
                                    {
                                        public Payload Payload { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Xml { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Xml, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    var serializer = new DataContractSerializer(typeof(Payload));
                                                    using var stream = new MemoryStream();
                                                    serializer.WriteObject(stream, src.Payload);
                                                    return stream.ToArray();
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 29, 65,
                "Xml", "serialization operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenDataContractJsonReadObjectUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;
                                using System.Runtime.Serialization;
                                using System.Runtime.Serialization.Json;

                                namespace TestNamespace
                                {
                                    [DataContract]
                                    public class Payload
                                    {
                                        [DataMember]
                                        public string Name { get; set; }
                                    }

                                    public class Source
                                    {
                                        public byte[] Json { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => ((Payload)new DataContractJsonSerializer(typeof(Payload)).ReadObject(new MemoryStream(src.Json))!).Name));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 30, 66,
                "Name", "serialization operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenDataContractJsonWriteObjectUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;
                                using System.Runtime.Serialization;
                                using System.Runtime.Serialization.Json;

                                namespace TestNamespace
                                {
                                    [DataContract]
                                    public class Payload
                                    {
                                        [DataMember]
                                        public string Name { get; set; }
                                    }

                                    public class Source
                                    {
                                        public Payload Payload { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Json { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Json, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    var serializer = new DataContractJsonSerializer(typeof(Payload));
                                                    using var stream = new MemoryStream();
                                                    serializer.WriteObject(stream, src.Payload);
                                                    return stream.ToArray();
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 30, 66,
                "Json", "serialization operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenXmlObjectSerializerBaseReadObjectUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.IO;
                                using System.Runtime.Serialization;

                                namespace TestNamespace
                                {
                                    [DataContract]
                                    public class Payload
                                    {
                                        [DataMember]
                                        public string Name { get; set; }
                                    }

                                    public class Source
                                    {
                                        public byte[] Xml { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly XmlObjectSerializer _serializer = new DataContractSerializer(typeof(Payload));

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => ((Payload)_serializer.ReadObject(new MemoryStream(src.Xml))!).Name));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 31, 66,
                "Name", "serialization operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForUserRuntimeSerializerNamesakes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class DataContractSerializer
                                    {
                                        public Payload ReadObject(string value) => new Payload { Name = value };
                                        public string WriteObject(Payload value) => value.Name;
                                    }

                                    public class DataContractJsonSerializer
                                    {
                                        public Payload ReadObject(string value) => new Payload { Name = value };
                                        public string WriteObject(Payload value) => value.Name;
                                    }

                                    public class Payload
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Source
                                    {
                                        public string Json { get; set; }
                                        public Payload Payload { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string JsonName { get; set; }
                                        public string WrittenName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.JsonName, opt => opt.MapFrom(src => new DataContractJsonSerializer().ReadObject(src.Json).Name))
                                                .ForMember(dest => dest.WrittenName, opt => opt.MapFrom(src => new DataContractSerializer().WriteObject(src.Payload)));
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
    public async Task AM031_ShouldReportDiagnostic_WhenJsonDocumentParseUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Text.Json;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Json { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => JsonDocument.Parse(src.Json).RootElement.GetProperty("name").GetString()!));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 66,
                "Name", "parsing operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenJsonNodeParseUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Text.Json.Nodes;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Json { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => JsonNode.Parse(src.Json)!["name"]!.ToString()));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 66,
                "Name", "parsing operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenXDocumentParseUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Xml.Linq;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Xml { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => XDocument.Parse(src.Xml).Root!.Element("name")!.Value));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 66,
                "Name", "parsing operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenXElementParseUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Xml.Linq;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Xml { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => XElement.Parse(src.Xml).Element("name")!.Value));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 66,
                "Name", "parsing operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenXDocumentLoadUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Xml.Linq;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Path { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => XDocument.Load(src.Path).Root!.Element("name")!.Value));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 66,
                "Name", "parsing operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenXElementLoadUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Xml.Linq;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Path { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => XElement.Load(src.Path).Element("name")!.Value));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 66,
                "Name", "parsing operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenXmlDocumentLoadXmlUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Xml;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Xml { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    var document = new XmlDocument();
                                                    document.LoadXml(src.Xml);
                                                    return document.DocumentElement!.InnerText;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 66,
                "Name", "parsing operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenXmlDocumentLoadUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Xml;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Path { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Name, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    var document = new XmlDocument();
                                                    document.Load(src.Path);
                                                    return document.DocumentElement!.InnerText;
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 66,
                "Name", "parsing operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForUserJsonAndXmlParseNamesakes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class JsonDocument
                                    {
                                        public static ParsedValue Parse(string value) => new ParsedValue(value);
                                    }

                                    public class XDocument
                                    {
                                        public static ParsedValue Parse(string value) => new ParsedValue(value);
                                    }

                                    public class ParsedValue
                                    {
                                        private readonly string _value;

                                        public ParsedValue(string value)
                                        {
                                            _value = value;
                                        }

                                        public string Read() => _value;
                                    }

                                    public class Source
                                    {
                                        public string Json { get; set; }
                                        public string Xml { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string JsonName { get; set; }
                                        public string XmlName { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.JsonName, opt => opt.MapFrom(src => JsonDocument.Parse(src.Json).Read()))
                                                .ForMember(dest => dest.XmlName, opt => opt.MapFrom(src => XDocument.Parse(src.Xml).Read()));
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
    public async Task AM031_ShouldReportDiagnostic_WhenResourceManagerGetStringUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Resources;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string ResourceKey { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Label { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly ResourceManager _resources = new ResourceManager("TestNamespace.Resources", typeof(TestProfile).Assembly);

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Label, opt => opt.MapFrom(src => _resources.GetString(src.ResourceKey)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 67,
                "Label", "resource lookup")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenResourceManagerGetResourceSetUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Globalization;
                                using System.Resources;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string ResourceKey { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Label { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly ResourceManager _resources = new ResourceManager("TestNamespace.Resources", typeof(TestProfile).Assembly);

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Label, opt => opt.MapFrom(src => _resources.GetResourceSet(CultureInfo.InvariantCulture, true, true)!.GetString(src.ResourceKey)!));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 24, 67,
                "Label", "resource lookup")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserResourceManagerNamesake()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class ResourceManager
                                    {
                                        public string GetString(string key) => key;
                                    }

                                    public class Source
                                    {
                                        public ResourceManager Resources { get; set; }
                                        public string ResourceKey { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Label { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Label, opt => opt.MapFrom(src => src.Resources.GetString(src.ResourceKey)));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserResourceManagerGetResourceSetNamesake()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class ResourceManager
                                    {
                                        public string GetResourceSet(string key) => key;
                                    }

                                    public class Source
                                    {
                                        public ResourceManager Resources { get; set; }
                                        public string ResourceKey { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Label { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Label, opt => opt.MapFrom(src => src.Resources.GetResourceSet(src.ResourceKey)));
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
    public async Task AM031_ShouldReportDiagnostic_WhenRegexIsMatchUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Text.RegularExpressions;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Code { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool IsValid { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.IsValid, opt => opt.MapFrom(src => Regex.IsMatch(src.Code, "^[A-Z]{3}$")));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 69,
                "IsValid", "regex operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenRegexInstanceReplaceUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Text.RegularExpressions;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Slug { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly Regex _spaces = new Regex("\\s+");

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Slug, opt => opt.MapFrom(src => _spaces.Replace(src.Name, "-")));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 66,
                "Slug", "regex operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedRegex()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Regex
                                    {
                                        public bool IsMatch(string value) => value.Length > 0;
                                    }

                                    public class Source
                                    {
                                        public string Code { get; set; }
                                        public Regex Validator { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool IsValid { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.IsValid, opt => opt.MapFrom(src => src.Validator.IsMatch(src.Code)));
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
    public async Task AM031_ShouldReportDiagnostic_WhenSha256HashDataUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Security.Cryptography;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Hash { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Hash, opt => opt.MapFrom(src => SHA256.HashData(src.Data)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 66,
                "Hash", "cryptographic operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenHashAlgorithmComputeHashUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Security.Cryptography;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Hash { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly SHA256 _sha = SHA256.Create();

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Hash, opt => opt.MapFrom(src => _sha.ComputeHash(src.Data)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 66,
                "Hash", "cryptographic operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenIncrementalHashUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Security.Cryptography;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Hash { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Hash, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                                                    hash.AppendData(src.Data);
                                                    return hash.GetHashAndReset();
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 66,
                "Hash", "cryptographic operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenIncrementalHmacUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Security.Cryptography;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Data { get; set; }
                                        public byte[] Key { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Hash { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Hash, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var hash = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, src.Key);
                                                    hash.AppendData(src.Data);
                                                    return hash.GetCurrentHash();
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 22, 66,
                "Hash", "cryptographic operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForUserTypeNamedSha256()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public static class SHA256
                                    {
                                        public static byte[] HashData(byte[] data) => data;
                                    }

                                    public class Source
                                    {
                                        public byte[] Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Hash { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Hash, opt => opt.MapFrom(src => SHA256.HashData(src.Data)));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForUserIncrementalHashNamesake()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class IncrementalHash
                                    {
                                        public static IncrementalHash CreateHash(string algorithmName) => new IncrementalHash();
                                        public static IncrementalHash CreateHMAC(string algorithmName, byte[] key) => new IncrementalHash();
                                        public void AppendData(byte[] data)
                                        {
                                        }

                                        public byte[] GetHashAndReset() => new byte[] { 1 };
                                        public byte[] GetCurrentHash() => new byte[] { 2 };
                                    }

                                    public class Source
                                    {
                                        public byte[] Data { get; set; }
                                        public byte[] Key { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Hash { get; set; }
                                        public byte[] Hmac { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Hash, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    var hash = IncrementalHash.CreateHash("SHA256");
                                                    hash.AppendData(src.Data);
                                                    return hash.GetHashAndReset();
                                                }))
                                                .ForMember(dest => dest.Hmac, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    var hmac = IncrementalHash.CreateHMAC("SHA256", src.Key);
                                                    hmac.AppendData(src.Data);
                                                    return hmac.GetCurrentHash();
                                                }));
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
    public async Task AM031_ShouldReportDiagnostic_WhenRfc2898DeriveBytesGetBytesUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Security.Cryptography;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Password { get; set; }
                                        public byte[] Salt { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Key { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Key, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var deriveBytes = new Rfc2898DeriveBytes(src.Password, src.Salt, 100000, HashAlgorithmName.SHA256);
                                                    return deriveBytes.GetBytes(32);
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 22, 65,
                "Key", "cryptographic operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenRfc2898Pbkdf2UsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Security.Cryptography;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Password { get; set; }
                                        public byte[] Salt { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Key { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Key, opt => opt.MapFrom(src => Rfc2898DeriveBytes.Pbkdf2(src.Password, src.Salt, 100000, HashAlgorithmName.SHA256, 32)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 22, 65,
                "Key", "cryptographic operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForUserRfc2898DeriveBytesNamesake()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Rfc2898DeriveBytes
                                    {
                                        public Rfc2898DeriveBytes(string password, byte[] salt, int iterations)
                                        {
                                        }

                                        public byte[] GetBytes(int count) => new byte[count];

                                        public static byte[] Pbkdf2(string password, byte[] salt, int iterations, int outputLength) => new byte[outputLength];
                                    }

                                    public class Source
                                    {
                                        public string Password { get; set; }
                                        public byte[] Salt { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] InstanceKey { get; set; }
                                        public byte[] StaticKey { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.InstanceKey, opt => opt.MapFrom(src => new Rfc2898DeriveBytes(src.Password, src.Salt, 100000).GetBytes(32)))
                                                .ForMember(dest => dest.StaticKey, opt => opt.MapFrom(src => Rfc2898DeriveBytes.Pbkdf2(src.Password, src.Salt, 100000, 32)));
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
    public async Task AM031_ShouldReportDiagnostic_WhenPasswordDeriveBytesGetBytesUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Security.Cryptography;
                                using System.Text;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Password { get; set; }
                                        public byte[] Salt { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Key { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Key, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var deriveBytes = new PasswordDeriveBytes(src.Password, src.Salt);
                                                    return deriveBytes.GetBytes(32);
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 65,
                "Key", "cryptographic operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenPasswordDeriveBytesCryptDeriveKeyUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Security.Cryptography;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Password { get; set; }
                                        public byte[] Salt { get; set; }
                                        public byte[] Iv { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Key { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Key, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var deriveBytes = new PasswordDeriveBytes(src.Password, src.Salt);
                                                    return deriveBytes.CryptDeriveKey("AES", "SHA1", 256, src.Iv);
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 65,
                "Key", "cryptographic operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForUserPasswordDeriveBytesNamesake()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class PasswordDeriveBytes
                                    {
                                        public PasswordDeriveBytes(string password, byte[] salt)
                                        {
                                        }

                                        public byte[] GetBytes(int count) => new byte[count];
                                        public byte[] CryptDeriveKey(string algname, string alghashname, int keySize, byte[] rgbIV) => rgbIV;
                                    }

                                    public class Source
                                    {
                                        public string Password { get; set; }
                                        public byte[] Salt { get; set; }
                                        public byte[] Iv { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Key { get; set; }
                                        public byte[] LegacyKey { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Key, opt => opt.MapFrom(src => new PasswordDeriveBytes(src.Password, src.Salt).GetBytes(32)))
                                                .ForMember(dest => dest.LegacyKey, opt => opt.MapFrom(src => new PasswordDeriveBytes(src.Password, src.Salt).CryptDeriveKey("AES", "SHA1", 256, src.Iv)));
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
    public async Task AM031_ShouldReportDiagnostic_WhenRsaEncryptUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Security.Cryptography;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Key { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Key, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var rsa = RSA.Create();
                                                    return rsa.Encrypt(src.Data, RSAEncryptionPadding.OaepSHA256);
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 65,
                "Key", "cryptographic operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenEcdsaSignDataUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Security.Cryptography;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Key { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Key, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var ecdsa = ECDsa.Create();
                                                    return ecdsa.SignData(src.Data, HashAlgorithmName.SHA256);
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 65,
                "Key", "cryptographic operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenDsaSignDataUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Security.Cryptography;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Key { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Key, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var dsa = DSA.Create();
                                                    return dsa.SignData(src.Data, HashAlgorithmName.SHA256);
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 65,
                "Key", "cryptographic operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenEcdiffieHellmanDeriveKeyMaterialUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Security.Cryptography;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Key { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Key, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var local = ECDiffieHellman.Create();
                                                    using var remote = ECDiffieHellman.Create();
                                                    return local.DeriveKeyMaterial(remote.PublicKey);
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 21, 65,
                "Key", "cryptographic operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenAesCreateEncryptorUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Security.Cryptography;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Data { get; set; }
                                        public byte[] Key { get; set; }
                                        public byte[] Iv { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Key { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Key, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    using var aes = Aes.Create();
                                                    aes.Key = src.Key;
                                                    aes.IV = src.Iv;
                                                    using ICryptoTransform encryptor = aes.CreateEncryptor();
                                                    return encryptor.TransformFinalBlock(src.Data, 0, src.Data.Length);
                                                }));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 65,
                "Key", "cryptographic operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenCryptoTransformUsedInMapFrom()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Security.Cryptography;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public byte[] Data { get; set; }
                                        public ICryptoTransform Transform { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Key { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Key, opt => opt.MapFrom(src => src.Transform.TransformFinalBlock(src.Data, 0, src.Data.Length)));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 22, 65,
                "Key", "cryptographic operation")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForUserSymmetricCryptoNamesakes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Aes
                                    {
                                        public ICryptoTransform CreateEncryptor() => new ICryptoTransform();
                                    }

                                    public class ICryptoTransform
                                    {
                                        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount) => inputBuffer;
                                    }

                                    public class Source
                                    {
                                        public byte[] Data { get; set; }
                                        public Aes Aes { get; set; }
                                        public ICryptoTransform Transform { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Encrypted { get; set; }
                                        public byte[] Transformed { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Encrypted, opt => opt.MapFrom(src => src.Aes.CreateEncryptor().TransformFinalBlock(src.Data, 0, src.Data.Length)))
                                                .ForMember(dest => dest.Transformed, opt => opt.MapFrom(src => src.Transform.TransformFinalBlock(src.Data, 0, src.Data.Length)));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForUserPublicKeyCryptoNamesakes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class RSA
                                    {
                                        public byte[] Encrypt(byte[] data, string padding) => data;
                                    }

                                    public class ECDsa
                                    {
                                        public byte[] SignData(byte[] data, string hashAlgorithm) => data;
                                    }

                                    public class DSA
                                    {
                                        public bool VerifyData(byte[] data, byte[] signature, string hashAlgorithm) => signature.Length > 0;
                                    }

                                    public class ECDiffieHellman
                                    {
                                        public byte[] DeriveKeyMaterial(object publicKey) => new byte[] { 1 };
                                    }

                                    public class Source
                                    {
                                        public byte[] Data { get; set; }
                                        public byte[] Signature { get; set; }
                                        public object PublicKey { get; set; }
                                        public RSA Rsa { get; set; }
                                        public ECDsa Ecdsa { get; set; }
                                        public DSA Dsa { get; set; }
                                        public ECDiffieHellman Agreement { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public byte[] Encrypted { get; set; }
                                        public byte[] Signature { get; set; }
                                        public byte[] SharedKey { get; set; }
                                        public bool IsValid { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Encrypted, opt => opt.MapFrom(src => src.Rsa.Encrypt(src.Data, "OAEP")))
                                                .ForMember(dest => dest.Signature, opt => opt.MapFrom(src => src.Ecdsa.SignData(src.Data, "SHA256")))
                                                .ForMember(dest => dest.SharedKey, opt => opt.MapFrom(src => src.Agreement.DeriveKeyMaterial(src.PublicKey)))
                                                .ForMember(dest => dest.IsValid, opt => opt.MapFrom(src => src.Dsa.VerifyData(src.Data, src.Signature, "SHA256")));
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
    public async Task AM031_ShouldReportDiagnostic_WhenTaskWaitAnyUsedInMapFrom()
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
                                        public int CompletedIndex { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.CompletedIndex, opt => opt.MapFrom(src => Task.WaitAny(DataService.GetDataAsync(src.Id))));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.TaskResultSynchronousAccessRule, 26, 76,
                "CompletedIndex")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_WhenTaskWaitAllUsedInFuncMapFrom()
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
                                                .ForMember(dest => dest.Completed, opt => opt.MapFrom((src, dest) =>
                                                {
                                                    Task.WaitAll(DataService.GetDataAsync(src.Id));
                                                    return true;
                                                }));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForReadonlyStringComparerHelperCall()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Role { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool IsAdmin { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly StringComparer _comparer = StringComparer.OrdinalIgnoreCase;

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.IsAdmin, opt => opt.MapFrom(src => _comparer.Equals(src.Role, "admin")));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForDirectFrameworkComparerSingletonCalls()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Role { get; set; }
                                        public int Rank { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool IsPrivileged { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.IsPrivileged, opt => opt.MapFrom(src =>
                                                    StringComparer.OrdinalIgnoreCase.Equals(src.Role, "admin") ||
                                                    EqualityComparer<string>.Default.Equals(src.Role, "root") ||
                                                    Comparer<int>.Default.Compare(src.Rank, 1) == 0));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForReferenceEqualityComparerSingletonCalls()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public object Primary { get; set; }
                                        public object Secondary { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool SameReference { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly ReferenceEqualityComparer _fieldComparer = ReferenceEqualityComparer.Instance;
                                        private ReferenceEqualityComparer PropertyComparer => ReferenceEqualityComparer.Instance;

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.SameReference, opt => opt.MapFrom(src =>
                                                    ReferenceEqualityComparer.Instance.Equals(src.Primary, src.Secondary) ||
                                                    _fieldComparer.Equals(src.Primary, src.Secondary) ||
                                                    PropertyComparer.GetHashCode(src.Primary) == PropertyComparer.GetHashCode(src.Secondary)));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForReadonlyEqualityComparerHelperCall()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Role { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool IsAdmin { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly EqualityComparer<string> _comparer = EqualityComparer<string>.Default;

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.IsAdmin, opt => opt.MapFrom(src => _comparer.Equals(src.Role, "admin")));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForGetOnlyEqualityComparerPropertyHelperCall()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Role { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool IsAdmin { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private EqualityComparer<string> Comparer { get; } = EqualityComparer<string>.Default;

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.IsAdmin, opt => opt.MapFrom(src => Comparer.Equals(src.Role, "admin")));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForExpressionBodiedEqualityComparerPropertyHelperCall()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Role { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool IsAdmin { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private EqualityComparer<string> Comparer => EqualityComparer<string>.Default;
                                        private EqualityComparer<string> AccessorComparer { get => EqualityComparer<string>.Default; }

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.IsAdmin, opt => opt.MapFrom(src =>
                                                    Comparer.Equals(src.Role, "admin") ||
                                                    AccessorComparer.Equals(src.Role, "root")));
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
    public async Task AM031_ShouldReportDiagnostic_ForInjectedEqualityComparerTypedServiceCall()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Role { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool IsAdmin { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly EqualityComparer<string> _comparer;

                                        public TestProfile(EqualityComparer<string> comparer)
                                        {
                                            _comparer = comparer;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.IsAdmin, opt => opt.MapFrom(src => _comparer.Equals(src.Role, "admin")));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 24, 69,
                "IsAdmin", "method call")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_ForMutableEqualityComparerFieldCall()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Role { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool IsAdmin { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private EqualityComparer<string> _comparer = EqualityComparer<string>.Default;

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.IsAdmin, opt => opt.MapFrom(src => _comparer.Equals(src.Role, "admin")));
                                        }

                                        public void UseComparer(EqualityComparer<string> comparer)
                                        {
                                            _comparer = comparer;
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 23, 69,
                "IsAdmin", "method call")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldReportDiagnostic_ForInjectedEqualityComparerPropertyCall()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Role { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool IsAdmin { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private EqualityComparer<string> Comparer { get; }

                                        public TestProfile(EqualityComparer<string> comparer)
                                        {
                                            Comparer = comparer;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.IsAdmin, opt => opt.MapFrom(src => Comparer.Equals(src.Role, "admin")));
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 24, 69,
                "IsAdmin", "method call")
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldNotReportDiagnostic_ForReadonlyComparerHelperCall()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Rank { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public bool IsTopRank { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly Comparer<int> _comparer = Comparer<int>.Default;

                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.IsTopRank, opt => opt.MapFrom(src => _comparer.Compare(src.Rank, 1) == 0));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForSourceRootedUserTypeNamedRandomNumberGenerator()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class RandomNumberGenerator
                                    {
                                        public int GetBytes(int length) => length;
                                    }

                                    public class Source
                                    {
                                        public RandomNumberGenerator Generator { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Length { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Length, opt => opt.MapFrom(src => src.Generator.GetBytes(16)));
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
    public async Task AM031_ShouldNotReportDiagnostic_ForReadOnlyDelegateInvokeOnSourcePropertyWithCustomParameterName()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public int Id { get; set; }
                                        public Func<int, string> Formatter { get; } = id => id.ToString();
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
                                                .ForMember(dest => dest.Data, opt => opt.MapFrom(source => source.Formatter.Invoke(source.Id)));
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

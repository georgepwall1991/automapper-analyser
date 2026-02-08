using AutoMapperAnalyzer.Analyzers.Performance;
using AutoMapperAnalyzer.Tests.Framework;

namespace AutoMapperAnalyzer.Tests.Performance;

public class AM031_CodeFixTests
{
    [Fact]
    public async Task AM031_ShouldRegisterCodeFixes_ForMultipleEnumerationDiagnostics()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Linq;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<int> Numbers { get; set; } = new();
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

        await CodeFixTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithCodeFix<AM031_PerformanceWarningCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.MultipleEnumerationRule, 22, 67,
                "Total", "Numbers")
            .ExpectFixedCode(testCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldRegisterCodeFixes_ForExpensiveOperationDiagnostics()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class ScoreService
                                    {
                                        public int Calculate(int id) => id * 2;
                                    }

                                    public class Source
                                    {
                                        public int Id { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public int Score { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly ScoreService _service;

                                        public TestProfile(ScoreService service)
                                        {
                                            _service = service;
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Score, opt => opt.MapFrom(src => _service.Calculate(src.Id)));
                                        }
                                    }
                                }
                                """;

        await CodeFixTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithCodeFix<AM031_PerformanceWarningCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.ExpensiveOperationInMapFromRule, 28, 67,
                "Score", "method call")
            .ExpectFixedCode(testCode)
            .RunAsync();
    }

    [Fact]
    public async Task AM031_ShouldRegisterCodeFixes_ForNonDeterministicDiagnostics()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public DateTime Timestamp { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public DateTime Timestamp { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>()
                                                .ForMember(dest => dest.Timestamp, opt => opt.MapFrom(src => DateTime.Now));
                                        }
                                    }
                                }
                                """;

        await CodeFixTestFramework
            .ForAnalyzer<AM031_PerformanceWarningAnalyzer>()
            .WithCodeFix<AM031_PerformanceWarningCodeFixProvider>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM031_PerformanceWarningAnalyzer.NonDeterministicOperationRule, 21, 71,
                "Timestamp", "DateTime.Now")
            .ExpectFixedCode(testCode)
            .RunAsync();
    }
}

using AutoMapperAnalyzer.Analyzers.ComplexMappings;
using AutoMapperAnalyzer.Tests.Framework;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.ComplexMappings;

public class AM030_CustomTypeConverterTests
{
    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenConverterDoesNotHandleNullValues()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class NullUnsafeConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<NullUnsafeConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "NullUnsafeConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenConverterHandlesNullsProperly()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class NullSafeConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if (string.IsNullOrWhiteSpace(source))
                                            {
                                                return DateTime.MinValue;
                                            }

                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<NullSafeConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenInvalidTypeConverterImplementation()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class InvalidConverter : ITypeConverter<string, DateTime>
                                    {
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM030_CustomTypeConverterAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            new DiagnosticResult(AM030_CustomTypeConverterAnalyzer.InvalidConverterImplementationRule)
                .WithLocation(6, 18)
                .WithArguments("InvalidConverter", "String", "DateTime"),
            new DiagnosticResult(AM030_CustomTypeConverterAnalyzer.UnusedTypeConverterRule)
                .WithLocation(6, 18)
                .WithArguments("InvalidConverter"),
            DiagnosticResult.CompilerError("CS0535")
                .WithLocation(6, 37));
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenTypeConverterIsUnused()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class UnusedConverter : ITypeConverter<string, DateTime>
                                    {
                                        public DateTime Convert(string source, DateTime destination, ResolutionContext context)
                                        {
                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class Source
                                    {
                                        public string Name { get; set; } = string.Empty;
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; } = string.Empty;
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
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.UnusedTypeConverterRule, 6, 18,
                "UnusedConverter")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenTypeConverterIsUsedInConvertUsing()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string CreatedDate { get; set; } = "2024-01-01";
                                    }

                                    public class Destination
                                    {
                                        public DateTime CreatedDate { get; set; }
                                    }

                                    public class UsedConverter : ITypeConverter<Source, Destination>
                                    {
                                        public Destination Convert(Source source, Destination destination, ResolutionContext context)
                                        {
                                            return new Destination
                                            {
                                                CreatedDate = DateTime.Parse(source.CreatedDate)
                                            };
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>().ConvertUsing<UsedConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectNoDiagnostics()
            .RunAsync();
    }
}

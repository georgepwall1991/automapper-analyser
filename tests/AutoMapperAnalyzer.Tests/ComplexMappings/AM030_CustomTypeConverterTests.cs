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
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessDoesNotGuardLaterUnsafeUse()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessOnlyConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            _ = source?.Length;
                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessOnlyConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessOnlyConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessFeedsUnsafeInvocationArgument()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessInvocationArgumentConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            return DateTime.Parse(source?.Trim());
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessInvocationArgumentConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessInvocationArgumentConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessFeedsPrimitiveParseArgument()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessPrimitiveParseConverter : ITypeConverter<string?, int>
                                    {
                                        public int Convert(string? source, int destination, ResolutionContext context)
                                        {
                                            return int.Parse(source?.Trim());
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, int>().ConvertUsing<ConditionalAccessPrimitiveParseConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 7, 20,
                "ConditionalAccessPrimitiveParseConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessFeedsTryParseFallback()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessTryParseFallbackConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            return DateTime.TryParse(source?.Trim(), out var parsed)
                                                ? parsed
                                                : DateTime.MinValue;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessTryParseFallbackConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessFeedsTryParseIfFallback()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessTryParseIfFallbackConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if (DateTime.TryParse(source?.Trim(), out var parsed))
                                            {
                                                return parsed;
                                            }

                                            return DateTime.MinValue;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessTryParseIfFallbackConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenTryParseSuccessBranchUsesSourceAfterConditionalAccess()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class TryParseSuccessSourceConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if (DateTime.TryParse(source?.Trim(), out _))
                                            {
                                                return DateTime.Parse(source);
                                            }

                                            return DateTime.MinValue;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<TryParseSuccessSourceConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessFeedsNullTolerantInvocation()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessStringConcatConverter : ITypeConverter<string?, string>
                                    {
                                        public string Convert(string? source, string destination, ResolutionContext context)
                                        {
                                            return string.Concat(source?.Trim());
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, string>().ConvertUsing<ConditionalAccessStringConcatConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessPatternGuardsSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessPatternGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if (source?.Length is null)
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
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessPatternGuardConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessPositivePatternGuardsSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessPositivePatternGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if (source?.Length is > 0)
                                            {
                                                return DateTime.Parse(source);
                                            }

                                            return DateTime.MinValue;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessPositivePatternGuardConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessListPatternGuardsSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class ListPatternSource
                                    {
                                        public List<int>? Items { get; set; }
                                    }

                                    public class ConditionalAccessListPatternGuardConverter : ITypeConverter<ListPatternSource?, DateTime>
                                    {
                                        public DateTime Convert(ListPatternSource? source, DateTime destination, ResolutionContext context)
                                        {
                                            if (source?.Items is [_, ..])
                                            {
                                                return DateTime.Parse(source.ToString());
                                            }

                                            return DateTime.MinValue;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<ListPatternSource?, DateTime>().ConvertUsing<ConditionalAccessListPatternGuardConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessComparisonGuardsSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessComparisonGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if (source?.Trim() == null)
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
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessComparisonGuardConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenStringHelperGuardsConditionalAccess()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class StringHelperConditionalAccessGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if (string.IsNullOrWhiteSpace(source?.Trim()))
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
                                            CreateMap<string?, DateTime>().ConvertUsing<StringHelperConditionalAccessGuardConverter>();
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
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessNullComparisonBranchUsesSourceForNull()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessUnsafeNullComparisonConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if (source?.Trim() == null)
                                            {
                                                return DateTime.Parse(source);
                                            }

                                            return DateTime.MinValue;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessUnsafeNullComparisonConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessUnsafeNullComparisonConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessBranchGuardsSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessBranchGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if (source?.Length > 0)
                                            {
                                                return DateTime.Parse(source);
                                            }

                                            return DateTime.MinValue;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessBranchGuardConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessBooleanLocalGuardsSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessBooleanLocalGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            var hasText = source?.Length > 0;
                                            if (hasText)
                                            {
                                                return DateTime.Parse(source);
                                            }

                                            return DateTime.MinValue;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessBooleanLocalGuardConverter>();
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
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessCoalescedGuardCanEnterUnsafeBranchForNull()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessCoalescedUnsafeGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if ((source?.Length ?? 1) > 0)
                                            {
                                                return DateTime.Parse(source);
                                            }

                                            return DateTime.MinValue;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessCoalescedUnsafeGuardConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessCoalescedUnsafeGuardConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenNullBranchUsesShadowedSourceLambdaParameter()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Linq;

                                namespace TestNamespace
                                {
                                    public class Item
                                    {
                                        public string Name { get; set; } = string.Empty;
                                    }

                                    public class ShadowedSourceLambdaConverter : ITypeConverter<string?, string>
                                    {
                                        public string Convert(string? source, string destination, ResolutionContext context)
                                        {
                                            if (source?.Length is null)
                                            {
                                                var values = new[] { new Item { Name = "fallback" } };
                                                return values.Select(source => source.Name).First();
                                            }

                                            return source;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, string>().ConvertUsing<ShadowedSourceLambdaConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenFallbackUsesNamedArgumentCalledSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class NamedArgumentFallbackConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if (source?.Length > 0)
                                            {
                                                return DateTime.Parse(source);
                                            }

                                            return Parse(source: "2000-01-01");
                                        }

                                        private static DateTime Parse(string source) => DateTime.Parse(source);
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<NamedArgumentFallbackConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenNestedLambdaCapturesShadowedSourceParameter()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Linq;

                                namespace TestNamespace
                                {
                                    public class Item
                                    {
                                        public string Name { get; set; } = string.Empty;
                                        public List<Item> Children { get; set; } = new();
                                    }

                                    public class NestedShadowedSourceLambdaConverter : ITypeConverter<string?, string>
                                    {
                                        public string Convert(string? source, string destination, ResolutionContext context)
                                        {
                                            if (source?.Length is null)
                                            {
                                                var values = new[]
                                                {
                                                    new Item
                                                    {
                                                        Name = "fallback",
                                                        Children = new() { new Item { Name = "child" } }
                                                    }
                                                };

                                                return values
                                                    .Select(source => source.Children.Select(child => source.Name).First())
                                                    .First();
                                            }

                                            return source;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, string>().ConvertUsing<NestedShadowedSourceLambdaConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenNegatedConditionalAccessBranchGuardsSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class NegatedConditionalAccessBranchGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if (!(source?.Length > 0))
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
                                            CreateMap<string?, DateTime>().ConvertUsing<NegatedConditionalAccessBranchGuardConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessComparisonToFalseGuardsSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessEqualsFalseConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if ((source?.Length > 0) == false)
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
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessEqualsFalseConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessBooleanPatternGuardsSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessBooleanPatternConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if ((source?.Length > 0) is false)
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
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessBooleanPatternConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessHasValueGuardsSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessHasValueConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if ((source?.Length).HasValue)
                                            {
                                                return DateTime.Parse(source);
                                            }

                                            return DateTime.MinValue;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessHasValueConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessNullBranchThrows()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessThrowingNullBranchConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if (source?.Trim() == null)
                                            {
                                                throw new ArgumentNullException(nameof(source));
                                            }

                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessThrowingNullBranchConverter>();
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
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessBranchFallsThroughToUnsafeUseForNull()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessUnsafeFallthroughConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if (source?.Length > 0)
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
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessUnsafeFallthroughConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessUnsafeFallthroughConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessFallbackConditionallyFallsThroughToUnsafeUse()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessConditionalFallbackConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if (source?.Length > 0)
                                            {
                                                return DateTime.Parse(source);
                                            }

                                            if (destination == DateTime.MinValue)
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
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessConditionalFallbackConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessConditionalFallbackConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessBranchGuardsAssignment()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessBranchAssignmentConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            var value = DateTime.MinValue;
                                            if (source?.Length > 0)
                                            {
                                                value = DateTime.Parse(source);
                                            }

                                            return value;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessBranchAssignmentConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessBranchGuardsElseAssignment()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessBranchElseAssignmentConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            var value = DateTime.MinValue;
                                            if (source?.Length > 0)
                                            {
                                                value = DateTime.Parse(source);
                                            }
                                            else
                                            {
                                                value = DateTime.MinValue;
                                            }

                                            return value;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessBranchElseAssignmentConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessBranchFallbackReturnIsSeparatedByHarmlessStatement()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessSeparatedFallbackConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            var value = DateTime.MinValue;
                                            if (source?.Length > 0)
                                            {
                                                value = DateTime.Parse(source);
                                            }

                                            var fallback = value;
                                            return fallback;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessSeparatedFallbackConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessTernaryGuardsSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessTernaryGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context) =>
                                            source?.Length > 0 ? DateTime.Parse(source) : DateTime.MinValue;
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessTernaryGuardConverter>();
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
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessTernaryFalseArmUsesSourceForNull()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessUnsafeTernaryConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context) =>
                                            source?.Length > 0 ? DateTime.MinValue : DateTime.Parse(source);
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessUnsafeTernaryConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessUnsafeTernaryConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessInequalityCanEnterUnsafeBranchForNull()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessUnsafeInequalityConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context) =>
                                            source?.Length != 0 ? DateTime.Parse(source) : DateTime.MinValue;
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessUnsafeInequalityConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessUnsafeInequalityConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessSwitchGuardsSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessSwitchGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context) =>
                                            source?.Length switch
                                            {
                                                > 0 => DateTime.Parse(source),
                                                _ => DateTime.MinValue
                                            };
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessSwitchGuardConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessBooleanSwitchUsesFalseFallback()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessBooleanSwitchConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context) =>
                                            (source?.Length > 0) switch
                                            {
                                                true => DateTime.Parse(source),
                                                false => DateTime.MinValue
                                            };
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessBooleanSwitchConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessSwitchGuardedNullArmFallsThroughToFallback()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessGuardedNullSwitchConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context) =>
                                            source?.Length switch
                                            {
                                                null when destination == DateTime.MaxValue => DateTime.MinValue,
                                                _ => DateTime.MinValue
                                            };
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessGuardedNullSwitchConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessSwitchWhenGuardExcludesNullSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessSwitchPatternVariableGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context) =>
                                            source?.Length switch
                                            {
                                                var length when length > 0 => DateTime.Parse(source),
                                                _ => DateTime.MinValue
                                            };
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessSwitchPatternVariableGuardConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessSwitchWhenGuardHandlesNullSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessSwitchPatternVariableNullConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context) =>
                                            source?.Length switch
                                            {
                                                var length when length is null => DateTime.MinValue,
                                                _ => DateTime.Parse(source)
                                            };
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessSwitchPatternVariableNullConverter>();
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
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessSwitchNullArmUsesSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessUnsafeSwitchConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context) =>
                                            source?.Length switch
                                            {
                                                null => DateTime.Parse(source),
                                                _ => DateTime.MinValue
                                            };
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessUnsafeSwitchConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessUnsafeSwitchConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessSwitchStatementGuardsSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessSwitchStatementGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            switch (source?.Length)
                                            {
                                                case > 0:
                                                    return DateTime.Parse(source);
                                                default:
                                                    return DateTime.MinValue;
                                            }
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessSwitchStatementGuardConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessSwitchStatementWhenClauseKeepsNullExcludingPattern()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessSwitchStatementWhenConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            switch (source?.Length)
                                            {
                                                case > 0 when destination != DateTime.MaxValue:
                                                    return DateTime.Parse(source);
                                                default:
                                                    return DateTime.MinValue;
                                            }
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessSwitchStatementWhenConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessSwitchStatementPatternVariableWhenGuardExcludesNull()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessSwitchStatementPatternVariableWhenConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            switch (source?.Length)
                                            {
                                                case var length when length > 0:
                                                    return DateTime.Parse(source);
                                                default:
                                                    return DateTime.MinValue;
                                            }
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessSwitchStatementPatternVariableWhenConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessSwitchStatementGuardedNullCaseFallsThroughToDefault()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessGuardedNullSwitchStatementConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            switch (source?.Length)
                                            {
                                                case null when destination == DateTime.MaxValue:
                                                    return DateTime.MinValue;
                                                default:
                                                    return DateTime.MinValue;
                                            }
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessGuardedNullSwitchStatementConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessSwitchStatementBreaksToFallbackReturn()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessSwitchStatementBreakFallbackConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            var value = DateTime.MinValue;
                                            switch (source?.Length)
                                            {
                                                case > 0:
                                                    value = DateTime.Parse(source);
                                                    break;
                                                default:
                                                    value = DateTime.MinValue;
                                                    break;
                                            }

                                            return value;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessSwitchStatementBreakFallbackConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessSwitchStatementFallsThroughToFallbackReturn()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessSwitchStatementFallthroughFallbackConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            switch (source?.Length)
                                            {
                                                case > 0:
                                                    return DateTime.Parse(source);
                                            }

                                            return DateTime.MinValue;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessSwitchStatementFallthroughFallbackConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessSwitchStatementDefaultPrecedesNullCase()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessSwitchStatementDefaultBeforeNullConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            switch (source?.Length)
                                            {
                                                default:
                                                    return DateTime.Parse(source);
                                                case null:
                                                    return DateTime.MinValue;
                                            }
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessSwitchStatementDefaultBeforeNullConverter>();
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
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessSwitchStatementNullCaseUsesSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessUnsafeSwitchStatementConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            switch (source?.Length)
                                            {
                                                case null:
                                                    return DateTime.Parse(source);
                                                default:
                                                    return DateTime.MinValue;
                                            }
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessUnsafeSwitchStatementConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessUnsafeSwitchStatementConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessBooleanSwitchStatementUsesFalseFallback()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessBooleanSwitchStatementConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            switch (source?.Length > 0)
                                            {
                                                case true:
                                                    return DateTime.Parse(source);
                                                case false:
                                                    return DateTime.MinValue;
                                            }
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessBooleanSwitchStatementConverter>();
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
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessBooleanSwitchStatementFalseCaseUsesSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessUnsafeBooleanSwitchStatementConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            switch (source?.Length > 0)
                                            {
                                                case true:
                                                    return DateTime.MinValue;
                                                case false:
                                                    return DateTime.Parse(source);
                                            }
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessUnsafeBooleanSwitchStatementConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessUnsafeBooleanSwitchStatementConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessLocalMemberDereferenceGuardsSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessLocalMemberDereferenceConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            var trimmed = source?.Trim();
                                            if (trimmed.Length > 0)
                                            {
                                                return DateTime.Parse(trimmed);
                                            }

                                            return DateTime.MinValue;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessLocalMemberDereferenceConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessLocalMemberDereferenceConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessLocalUsesFallback()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessLocalFallbackConverter : ITypeConverter<string?, string>
                                    {
                                        public string Convert(string? source, string destination, ResolutionContext context)
                                        {
                                            var trimmed = source?.Trim();
                                            return trimmed ?? string.Empty;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, string>().ConvertUsing<ConditionalAccessLocalFallbackConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessLocalTernaryUsesFallback()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessLocalTernaryFallbackConverter : ITypeConverter<string?, string>
                                    {
                                        public string Convert(string? source, string destination, ResolutionContext context)
                                        {
                                            var trimmed = source?.Trim();
                                            return trimmed is null ? string.Empty : trimmed;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, string>().ConvertUsing<ConditionalAccessLocalTernaryFallbackConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessGuardClauseThrowsBeforeSourceUse()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessGuardClauseConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            ArgumentNullException.ThrowIfNull(source?.Trim());
                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessGuardClauseConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessPatternVariableGuardExcludesNullSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessPatternVariableGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            if (source?.Length is var length && length > 0)
                                            {
                                                return DateTime.Parse(source);
                                            }

                                            return DateTime.MinValue;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessPatternVariableGuardConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessLocalIsNullCheckedBeforeUse()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessLocalNullCheckedConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            var trimmed = source?.Trim();
                                            if (trimmed is null)
                                            {
                                                return DateTime.MinValue;
                                            }

                                            return DateTime.Parse(trimmed);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessLocalNullCheckedConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessLocalNullBranchAssignsSourceFreeFallback()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessLocalFallbackAssignmentConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            var trimmed = source?.Trim();
                                            if (trimmed is null)
                                            {
                                                trimmed = "2000-01-01";
                                            }

                                            return DateTime.Parse(trimmed);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessLocalFallbackAssignmentConverter>();
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
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessLocalFeedsUnsafeInvocationArgument()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessLocalUnsafeInvocationConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            var trimmed = source?.Trim();
                                            return DateTime.Parse(trimmed);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessLocalUnsafeInvocationConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessLocalUnsafeInvocationConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessLocalGuardFollowsUnsafeSourceUse()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessLocalLateGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            var trimmed = source?.Trim();
                                            _ = DateTime.Parse(source);
                                            if (trimmed is null)
                                            {
                                                return DateTime.MinValue;
                                            }

                                            return DateTime.Parse(trimmed);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessLocalLateGuardConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessLocalLateGuardConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessLocalMayRemainNullAfterConditionalReassignment()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessLocalConditionalReassignmentConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            var trimmed = source?.Trim();
                                            if (destination.Ticks > 0)
                                            {
                                                trimmed = "2000-01-01";
                                            }

                                            return DateTime.Parse(trimmed);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessLocalConditionalReassignmentConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessLocalConditionalReassignmentConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenSplitAssignedConditionalAccessLocalIsNullCheckedBeforeUse()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class SplitAssignedConditionalAccessLocalConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            string? trimmed;
                                            trimmed = source?.Trim();
                                            if (trimmed is null)
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
                                            CreateMap<string?, DateTime>().ConvertUsing<SplitAssignedConditionalAccessLocalConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenSplitAssignedConditionalAccessLocalStringHelperGuardsSource()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class SplitAssignedConditionalAccessLocalStringHelperConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            string? trimmed;
                                            trimmed = source?.Trim();
                                            if (string.IsNullOrWhiteSpace(trimmed))
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
                                            CreateMap<string?, DateTime>().ConvertUsing<SplitAssignedConditionalAccessLocalStringHelperConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessLocalRelationalGuardExcludesNull()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessLocalRelationalGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            var length = source?.Length;
                                            if (length > 0)
                                            {
                                                return DateTime.Parse(source);
                                            }

                                            return DateTime.MinValue;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessLocalRelationalGuardConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessLocalHasValueGuardExcludesNull()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessLocalHasValueGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            var length = source?.Length;
                                            if (length.HasValue)
                                            {
                                                return DateTime.Parse(source);
                                            }

                                            return DateTime.MinValue;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessLocalHasValueGuardConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessLocalGuardClauseThrowsBeforeUse()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessLocalGuardClauseConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            var trimmed = source?.Trim();
                                            ArgumentNullException.ThrowIfNull(trimmed);
                                            return DateTime.Parse(trimmed);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessLocalGuardClauseConverter>();
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
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessLocalIsReassignedBeforeGuardClause()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessLocalReassignedBeforeGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            string? trimmed = source?.Trim();
                                            trimmed = destination.ToString();
                                            ArgumentNullException.ThrowIfNull(trimmed);
                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessLocalReassignedBeforeGuardConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessLocalReassignedBeforeGuardConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessLocalGuardIsInsideNestedHelper()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessLocalNestedHelperGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            void Helper()
                                            {
                                                var trimmed = source?.Trim();
                                                if (trimmed is null)
                                                {
                                                    return;
                                                }
                                            }

                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessLocalNestedHelperGuardConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessLocalNestedHelperGuardConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenNullCheckIsOnlyInsideNestedHelper()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class NestedHelperNullCheckConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            void Helper()
                                            {
                                                if (source == null)
                                                {
                                                    return;
                                                }
                                            }

                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<NestedHelperNullCheckConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "NestedHelperNullCheckConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessUsesFallback()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessFallbackConverter : ITypeConverter<string?, string>
                                    {
                                        public string Convert(string? source, string destination, ResolutionContext context)
                                        {
                                            return source?.Trim() ?? string.Empty;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, string>().ConvertUsing<ConditionalAccessFallbackConverter>();
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
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessFeedsUnsafeConstructorArgument()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessConstructorArgumentConverter : ITypeConverter<string?, Uri>
                                    {
                                        public Uri Convert(string? source, Uri destination, ResolutionContext context)
                                        {
                                            return new Uri(source?.Trim());
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, Uri>().ConvertUsing<ConditionalAccessConstructorArgumentConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 20,
                "ConditionalAccessConstructorArgumentConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessFeedsUnsafeTargetTypedConstructorArgument()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessTargetTypedConstructorArgumentConverter : ITypeConverter<string?, Uri>
                                    {
                                        public Uri Convert(string? source, Uri destination, ResolutionContext context)
                                        {
                                            return new(source?.Trim());
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, Uri>().ConvertUsing<ConditionalAccessTargetTypedConstructorArgumentConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 20,
                "ConditionalAccessTargetTypedConstructorArgumentConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessFeedsNullableParseProviderArgument()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;
                                using System.Globalization;

                                namespace TestNamespace
                                {
                                    public class FormatSource
                                    {
                                        public CultureInfo? Culture { get; set; }
                                    }

                                    public class ConditionalAccessParseProviderConverter : ITypeConverter<FormatSource?, DateTime>
                                    {
                                        public DateTime Convert(FormatSource? source, DateTime destination, ResolutionContext context)
                                        {
                                            return DateTime.Parse("2024-01-01", source?.Culture);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<FormatSource?, DateTime>().ConvertUsing<ConditionalAccessParseProviderConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessFeedsUriKindFallbackArgument()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class UriOptions
                                    {
                                        public UriKind Kind { get; set; } = UriKind.Relative;
                                    }

                                    public class ConditionalAccessUriKindConverter : ITypeConverter<UriOptions?, Uri>
                                    {
                                        public Uri Convert(UriOptions? source, Uri destination, ResolutionContext context)
                                        {
                                            return new Uri("relative/path", source?.Kind ?? UriKind.Relative);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<UriOptions?, Uri>().ConvertUsing<ConditionalAccessUriKindConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessFeedsNullableConstructorArgument()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Wrapper
                                    {
                                        public Wrapper(string? value)
                                        {
                                            Value = value;
                                        }

                                        public string? Value { get; }
                                    }

                                    public class ConditionalAccessNullableConstructorConverter : ITypeConverter<string?, Wrapper>
                                    {
                                        public Wrapper Convert(string? source, Wrapper destination, ResolutionContext context)
                                        {
                                            return new Wrapper(source?.Trim());
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, Wrapper>().ConvertUsing<ConditionalAccessNullableConstructorConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenObjectInitializerOnlyUsesConditionalAccessSource()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class InitializerSource
                                    {
                                        public string? Name { get; set; }
                                    }

                                    public class InitializerDestination
                                    {
                                        public string? Name { get; set; }
                                    }

                                    public class ConditionalAccessObjectInitializerConverter : ITypeConverter<InitializerSource?, InitializerDestination>
                                    {
                                        public InitializerDestination Convert(InitializerSource? source, InitializerDestination destination, ResolutionContext context)
                                        {
                                            return new InitializerDestination
                                            {
                                                Name = source?.Name
                                            };
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<InitializerSource?, InitializerDestination>().ConvertUsing<ConditionalAccessObjectInitializerConverter>();
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
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessCoalesceFallbackUsesSourceForNull()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessUnsafeCoalesceFallbackConverter : ITypeConverter<string?, string>
                                    {
                                        public string Convert(string? source, string destination, ResolutionContext context)
                                        {
                                            return source?.Trim() ?? source.Trim();
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, string>().ConvertUsing<ConditionalAccessUnsafeCoalesceFallbackConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 7, 23,
                "ConditionalAccessUnsafeCoalesceFallbackConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessCoalesceFallbackPassesNullToUnsafeInvocation()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessNullCoalesceParseConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            return DateTime.Parse(source?.Trim() ?? null);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessNullCoalesceParseConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessNullCoalesceParseConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessCoalesceNullForgivingFallbackFeedsUnsafeInvocation()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class ConditionalAccessNullForgivingCoalesceParseConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            return DateTime.Parse(source?.Trim() ?? null!);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<ConditionalAccessNullForgivingCoalesceParseConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "ConditionalAccessNullForgivingCoalesceParseConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenCoalesceUsesShadowedUnsafeLocal()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class ShadowedCoalesceLocalConverter : ITypeConverter<string?, string>
                                    {
                                        public string Convert(string? source, string destination, ResolutionContext context)
                                        {
                                            if (destination.Length == 0)
                                            {
                                                var trimmed = source?.Trim();
                                            }

                                            if (destination.Length > 0)
                                            {
                                                var trimmed = source.Trim();
                                                return trimmed ?? string.Empty;
                                            }

                                            return string.Empty;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, string>().ConvertUsing<ShadowedCoalesceLocalConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 7, 23,
                "ShadowedCoalesceLocalConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessPropagatesNullToNonNullableDestination()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class NonNullableDestinationConverter : ITypeConverter<string?, string>
                                    {
                                        public string Convert(string? source, string destination, ResolutionContext context)
                                        {
                                            return source?.Trim();
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, string>().ConvertUsing<NonNullableDestinationConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 7, 23,
                "NonNullableDestinationConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessPropagatesNullToNullableDestination()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class NullableDestinationConverter : ITypeConverter<string?, string?>
                                    {
                                        public string? Convert(string? source, string? destination, ResolutionContext context)
                                        {
                                            return source?.Trim();
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, string?>().ConvertUsing<NullableDestinationConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenCastConditionalAccessPropagatesNullToNullableDestination()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class NullableDestinationCastConverter : ITypeConverter<string?, object?>
                                    {
                                        public object? Convert(string? source, object? destination, ResolutionContext context)
                                        {
                                            return (object?)source?.Trim();
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, object?>().ConvertUsing<NullableDestinationCastConverter>();
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
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessLocalIsOverwrittenBeforeNullableReturn()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class NullableDestinationOverwrittenLocalConverter : ITypeConverter<string?, string?>
                                    {
                                        public string? Convert(string? source, string? destination, ResolutionContext context)
                                        {
                                            string? trimmed = source?.Trim();
                                            trimmed = source.Trim();
                                            return trimmed;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, string?>().ConvertUsing<NullableDestinationOverwrittenLocalConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 7, 24,
                "NullableDestinationOverwrittenLocalConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessLocalPropagatesNullToNullableDestination()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class NullableDestinationLocalConverter : ITypeConverter<string?, string?>
                                    {
                                        public string? Convert(string? source, string? destination, ResolutionContext context)
                                        {
                                            var trimmed = source?.Trim();
                                            return trimmed;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, string?>().ConvertUsing<NullableDestinationLocalConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessLocalPropagatesNullThroughBranchReturns()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class NullableDestinationBranchReturnConverter : ITypeConverter<string?, string?>
                                    {
                                        public string? Convert(string? source, string? destination, ResolutionContext context)
                                        {
                                            var trimmed = source?.Trim();
                                            if (destination is null)
                                            {
                                                return trimmed;
                                            }
                                            else
                                            {
                                                return (trimmed);
                                            }
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, string?>().ConvertUsing<NullableDestinationBranchReturnConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenPositiveLocalGuardReturnsNullableFallback()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class PositiveLocalGuardNullableFallbackConverter : ITypeConverter<string?, string?>
                                    {
                                        public string? Convert(string? source, string? destination, ResolutionContext context)
                                        {
                                            var trimmed = source?.Trim();
                                            if (trimmed is not null)
                                            {
                                                return source.Trim();
                                            }

                                            return trimmed;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, string?>().ConvertUsing<PositiveLocalGuardNullableFallbackConverter>();
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
    public async Task AM030_ShouldReportDiagnostic_WhenGuardedLocalReturnFollowsUnsafeSourceUse()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class UnsafeGuardedLocalReturnConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            var value = source?.Length > 0 ? DateTime.Parse(source) : DateTime.MinValue;
                                            _ = DateTime.Parse(source);
                                            return value;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<UnsafeGuardedLocalReturnConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "UnsafeGuardedLocalReturnConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldReportDiagnostic_WhenConditionalAccessLocalOnlyFlowsToNestedReturn()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class NullableDestinationNestedReturnConverter : ITypeConverter<string?, string?>
                                    {
                                        public string? Convert(string? source, string? destination, ResolutionContext context)
                                        {
                                            var trimmed = source?.Trim();
                                            string? GetFallback()
                                            {
                                                return trimmed;
                                            }

                                            return source.Trim();
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, string?>().ConvertUsing<NullableDestinationNestedReturnConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 7, 24,
                "NullableDestinationNestedReturnConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenNestedConditionalAccessPropagatesNullToNullableDestination()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class SourceModel
                                    {
                                        public AddressModel? Address { get; set; }
                                    }

                                    public class AddressModel
                                    {
                                        public string? City { get; set; }
                                    }

                                    public class NestedNullableDestinationConverter : ITypeConverter<SourceModel?, string?>
                                    {
                                        public string? Convert(SourceModel? source, string? destination, ResolutionContext context)
                                        {
                                            return source?.Address?.City;
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<SourceModel?, string?>().ConvertUsing<NestedNullableDestinationConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenChainedConditionalAccessPropagatesNullToNullableDestination()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class ChainedNullableDestinationConverter : ITypeConverter<string?, string?>
                                    {
                                        public string? Convert(string? source, string? destination, ResolutionContext context)
                                        {
                                            return source?.Trim()?.ToUpperInvariant();
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, string?>().ConvertUsing<ChainedNullableDestinationConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConditionalAccessLocalParenthesizedReturnPropagatesNullToNullableDestination()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class NullableDestinationParenthesizedLocalConverter : ITypeConverter<string?, string?>
                                    {
                                        public string? Convert(string? source, string? destination, ResolutionContext context)
                                        {
                                            var trimmed = source?.Trim();
                                            return (trimmed);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, string?>().ConvertUsing<NullableDestinationParenthesizedLocalConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConverterUsesArgumentNullExceptionThrowIfNull()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class GuardedConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            ArgumentNullException.ThrowIfNull(source);
                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<GuardedConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenConverterUsesArgumentExceptionThrowIfNullOrEmpty()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class GuardedConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            ArgumentException.ThrowIfNullOrEmpty(source);
                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<GuardedConverter>();
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenThrowIfNullUsesNamedArgumentParameterOrder()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class NamedGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            ArgumentNullException.ThrowIfNull(paramName: nameof(source), argument: source);
                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<NamedGuardConverter>();
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
    public async Task AM030_ShouldReportDiagnostic_WhenThrowIfNullArgumentIsUnrelatedToSourceParameter()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class UnrelatedGuardConverter : ITypeConverter<string?, DateTime>
                                    {
                                        public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
                                        {
                                            ArgumentNullException.ThrowIfNull(context);
                                            return DateTime.Parse(source!);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, DateTime>().ConvertUsing<UnrelatedGuardConverter>();
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule, 8, 25,
                "UnrelatedGuardConverter", "String")
            .RunAsync();
    }

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_ForPropertyTypeMismatchWithoutConvertUsing()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string CreatedDate { get; set; } = string.Empty;
                                    }

                                    public class Destination
                                    {
                                        public System.DateTime CreatedDate { get; set; }
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

    [Fact]
    public async Task AM030_ShouldNotReportDiagnostic_WhenTypeConverterInstanceIsPassedToConvertUsing()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class InstanceConverter : ITypeConverter<string, DateTime>
                                    {
                                        public DateTime Convert(string source, DateTime destination, ResolutionContext context)
                                        {
                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string, DateTime>().ConvertUsing(new InstanceConverter());
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenTypeConverterTypeIsPassedToConvertUsing()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class TypeBasedConverter : ITypeConverter<string, DateTime>
                                    {
                                        public DateTime Convert(string source, DateTime destination, ResolutionContext context)
                                        {
                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string, DateTime>().ConvertUsing(typeof(TypeBasedConverter));
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenInterfaceTypedLocalConverterIsPassedToConvertUsing()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class InterfaceTypedLocalConverter : ITypeConverter<string, DateTime>
                                    {
                                        public DateTime Convert(string source, DateTime destination, ResolutionContext context)
                                        {
                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            ITypeConverter<string, DateTime> converter = new InterfaceTypedLocalConverter();
                                            CreateMap<string, DateTime>().ConvertUsing(converter);
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenInterfaceTypedFieldConverterIsPassedToConvertUsing()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class InterfaceTypedFieldConverter : ITypeConverter<string, DateTime>
                                    {
                                        public DateTime Convert(string source, DateTime destination, ResolutionContext context)
                                        {
                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private readonly ITypeConverter<string, DateTime> converter =
                                            new InterfaceTypedFieldConverter();

                                        public TestProfile()
                                        {
                                            CreateMap<string, DateTime>().ConvertUsing(converter);
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenInterfaceTypedPropertyConverterIsPassedToConvertUsing()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class InterfaceTypedPropertyConverter : ITypeConverter<string, DateTime>
                                    {
                                        public DateTime Convert(string source, DateTime destination, ResolutionContext context)
                                        {
                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        private ITypeConverter<string, DateTime> Converter { get; } =
                                            new InterfaceTypedPropertyConverter();

                                        public TestProfile()
                                        {
                                            CreateMap<string, DateTime>().ConvertUsing(Converter);
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenInterfaceTypedConstructorInjectedConverterIsPassedToConvertUsing()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class InjectedConverter : ITypeConverter<string, DateTime>
                                    {
                                        public DateTime Convert(string source, DateTime destination, ResolutionContext context)
                                        {
                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(ITypeConverter<string, DateTime> converter)
                                        {
                                            CreateMap<string, DateTime>().ConvertUsing(converter);
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
    public async Task AM030_ShouldNotReportDiagnostic_WhenInterfaceTypedServiceLocatorConverterIsPassedToConvertUsing()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public interface IServiceProviderLike
                                    {
                                        T Resolve<T>();
                                    }

                                    public class ResolvedConverter : ITypeConverter<string, DateTime>
                                    {
                                        public DateTime Convert(string source, DateTime destination, ResolutionContext context)
                                        {
                                            return DateTime.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(IServiceProviderLike services)
                                        {
                                            ITypeConverter<string, DateTime> converter = services.Resolve<ITypeConverter<string, DateTime>>();
                                            CreateMap<string, DateTime>().ConvertUsing(converter);
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
    public async Task AM030_ShouldReportDiagnostic_WhenDeclaredConverterIsNotMatchedByAnyConvertUsingInterfaceShape()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class UnusedNoInterfaceUsageConverter : ITypeConverter<int, string>
                                    {
                                        public string Convert(int source, string destination, ResolutionContext context)
                                        {
                                            return source.ToString();
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile(ITypeConverter<string, DateTime> converter)
                                        {
                                            CreateMap<string, DateTime>().ConvertUsing(converter);
                                        }
                                    }
                                }
                                """;

        await DiagnosticTestFramework
            .ForAnalyzer<AM030_CustomTypeConverterAnalyzer>()
            .WithSource(testCode)
            .ExpectDiagnostic(AM030_CustomTypeConverterAnalyzer.UnusedTypeConverterRule, 6, 18,
                "UnusedNoInterfaceUsageConverter")
            .RunAsync();
    }
}

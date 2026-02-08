using AutoMapperAnalyzer.Analyzers.ComplexMappings;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.ComplexMappings;

public class AM030_CodeFixTests
{
    [Fact]
    public async Task AM030_ShouldAddNullGuard_ForBlockBodiedConvertMethod()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class NullUnsafeConverter : ITypeConverter<string?, int>
                                    {
                                        public int Convert(string? source, int destination, ResolutionContext context)
                                        {
                                            return int.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, int>().ConvertUsing<NullUnsafeConverter>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System;

                                         namespace TestNamespace
                                         {
                                             public class NullUnsafeConverter : ITypeConverter<string?, int>
                                             {
                                                 public int Convert(string? source, int destination, ResolutionContext context)
                                                 {
                                                     if (source == null) throw new ArgumentNullException(nameof(source));
                                                     return int.Parse(source);
                                                 }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<string?, int>().ConvertUsing<NullUnsafeConverter>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM030_CustomTypeConverterAnalyzer, AM030_CustomTypeConverterCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule)
                    .WithLocation(7, 20)
                    .WithArguments("NullUnsafeConverter", "String"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM030_ShouldAddNullGuard_ForExpressionBodiedConvertMethod()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class NullUnsafeConverter : ITypeConverter<string?, int>
                                    {
                                        public int Convert(string? source, int destination, ResolutionContext context) => int.Parse(source);
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, int>().ConvertUsing<NullUnsafeConverter>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System;

                                         namespace TestNamespace
                                         {
                                             public class NullUnsafeConverter : ITypeConverter<string?, int>
                                             {
                                                 public int Convert(string? source, int destination, ResolutionContext context)
                                                 {
                                                     if (source == null) throw new ArgumentNullException(nameof(source));
                                                     return int.Parse(source);
                                                 }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<string?, int>().ConvertUsing<NullUnsafeConverter>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM030_CustomTypeConverterAnalyzer, AM030_CustomTypeConverterCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule)
                    .WithLocation(7, 20)
                    .WithArguments("NullUnsafeConverter", "String"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM030_ShouldNotDuplicateSystemUsing_WhenAddingNullGuard()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class NullUnsafeConverter : ITypeConverter<string?, int>
                                    {
                                        public int Convert(string? source, int destination, ResolutionContext context)
                                        {
                                            return int.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, int>().ConvertUsing<NullUnsafeConverter>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System;

                                         namespace TestNamespace
                                         {
                                             public class NullUnsafeConverter : ITypeConverter<string?, int>
                                             {
                                                 public int Convert(string? source, int destination, ResolutionContext context)
                                                 {
                                                     if (source == null) throw new ArgumentNullException(nameof(source));
                                                     return int.Parse(source);
                                                 }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<string?, int>().ConvertUsing<NullUnsafeConverter>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM030_CustomTypeConverterAnalyzer, AM030_CustomTypeConverterCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule)
                    .WithLocation(8, 20)
                    .WithArguments("NullUnsafeConverter", "String"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM030_ShouldHandleMultipleConverters_InSameFile()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class StringToIntConverter : ITypeConverter<string?, int>
                                    {
                                        public int Convert(string? source, int destination, ResolutionContext context)
                                        {
                                            return int.Parse(source);
                                        }
                                    }

                                    public class StringToDecimalConverter : ITypeConverter<string?, decimal>
                                    {
                                        public decimal Convert(string? source, decimal destination, ResolutionContext context)
                                        {
                                            return decimal.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, int>().ConvertUsing<StringToIntConverter>();
                                            CreateMap<string?, decimal>().ConvertUsing<StringToDecimalConverter>();
                                        }
                                    }
                                }
                                """;

        DiagnosticResult diag1 = new DiagnosticResult(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule)
            .WithLocation(7, 20)
            .WithArguments("StringToIntConverter", "String");

        DiagnosticResult diag2 = new DiagnosticResult(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule)
            .WithLocation(15, 24)
            .WithArguments("StringToDecimalConverter", "String");

        await AnalyzerVerifier<AM030_CustomTypeConverterAnalyzer>.VerifyAnalyzerAsync(testCode, diag1, diag2);
    }

    [Fact]
    public async Task AM030_ShouldNotAddNullCheck_WhenAlreadyExists()
    {
        const string testCode = """
                                using AutoMapper;
                                using System;

                                namespace TestNamespace
                                {
                                    public class SafeConverter : ITypeConverter<string?, int>
                                    {
                                        public int Convert(string? source, int destination, ResolutionContext context)
                                        {
                                            if (source == null) throw new ArgumentNullException(nameof(source));
                                            return int.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, int>().ConvertUsing<SafeConverter>();
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM030_CustomTypeConverterAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM030_ShouldAddNullGuard_ForNullableValueTypes()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class NullableIntConverter : ITypeConverter<int?, string>
                                    {
                                        public string Convert(int? source, string destination, ResolutionContext context)
                                        {
                                            return source.ToString();
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<int?, string>().ConvertUsing<NullableIntConverter>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System;

                                         namespace TestNamespace
                                         {
                                             public class NullableIntConverter : ITypeConverter<int?, string>
                                             {
                                                 public string Convert(int? source, string destination, ResolutionContext context)
                                                 {
                                                     if (source == null) throw new ArgumentNullException(nameof(source));
                                                     return source.ToString();
                                                 }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<int?, string>().ConvertUsing<NullableIntConverter>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM030_CustomTypeConverterAnalyzer, AM030_CustomTypeConverterCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule)
                    .WithLocation(7, 23)
                    .WithArguments("NullableIntConverter", "Nullable"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM030_ShouldAddNullGuard_ForMultipleNullableParameters()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class MultiNullableConverter : ITypeConverter<string?, int>
                                    {
                                        public int Convert(string? source, int destination, ResolutionContext context)
                                        {
                                            return int.Parse(source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, int>().ConvertUsing<MultiNullableConverter>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System;

                                         namespace TestNamespace
                                         {
                                             public class MultiNullableConverter : ITypeConverter<string?, int>
                                             {
                                                 public int Convert(string? source, int destination, ResolutionContext context)
                                                 {
                                                     if (source == null) throw new ArgumentNullException(nameof(source));
                                                     return int.Parse(source);
                                                 }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<string?, int>().ConvertUsing<MultiNullableConverter>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM030_CustomTypeConverterAnalyzer, AM030_CustomTypeConverterCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule)
                    .WithLocation(7, 20)
                    .WithArguments("MultiNullableConverter", "String"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM030_ShouldAddNullGuard_InNestedNamespace()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace Company.Project.Converters
                                {
                                    public class NestedConverter : ITypeConverter<string?, int>
                                    {
                                        public int Convert(string? source, int destination, ResolutionContext context)
                                        {
                                            return int.Parse(source);
                                        }
                                    }
                                }

                                namespace Company.Project.Profiles
                                {
                                    using AutoMapper;
                                    using Company.Project.Converters;

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, int>().ConvertUsing<NestedConverter>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System;

                                         namespace Company.Project.Converters
                                         {
                                             public class NestedConverter : ITypeConverter<string?, int>
                                             {
                                                 public int Convert(string? source, int destination, ResolutionContext context)
                                                 {
                                                     if (source == null) throw new ArgumentNullException(nameof(source));
                                                     return int.Parse(source);
                                                 }
                                             }
                                         }

                                         namespace Company.Project.Profiles
                                         {
                                             using AutoMapper;
                                             using Company.Project.Converters;

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<string?, int>().ConvertUsing<NestedConverter>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM030_CustomTypeConverterAnalyzer, AM030_CustomTypeConverterCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule)
                    .WithLocation(7, 20)
                    .WithArguments("NestedConverter", "String"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM030_ShouldConvertExpressionBodied_WithComplexExpression()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class ComplexConverter : ITypeConverter<string?, int>
                                    {
                                        public int Convert(string? source, int destination, ResolutionContext context) =>
                                            source.Contains("test") ? int.Parse(source.Split('-')[0]) : 0;
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<string?, int>().ConvertUsing<ComplexConverter>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System;

                                         namespace TestNamespace
                                         {
                                             public class ComplexConverter : ITypeConverter<string?, int>
                                             {
                                                 public int Convert(string? source, int destination, ResolutionContext context)
                                                 {
                                                     if (source == null) throw new ArgumentNullException(nameof(source));
                                                     return source.Contains("test") ? int.Parse(source.Split('-')[0]) : 0;
                                                 }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<string?, int>().ConvertUsing<ComplexConverter>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM030_CustomTypeConverterAnalyzer, AM030_CustomTypeConverterCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule)
                    .WithLocation(7, 20)
                    .WithArguments("ComplexConverter", "String"),
                expectedFixedCode);
    }

    [Fact]
    public async Task AM030_ShouldAddNullGuard_ForGenericTypeConverter()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class ListConverter : ITypeConverter<List<string>?, string>
                                    {
                                        public string Convert(List<string>? source, string destination, ResolutionContext context)
                                        {
                                            return string.Join(",", source);
                                        }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<List<string>?, string>().ConvertUsing<ListConverter>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System.Collections.Generic;
                                         using System;

                                         namespace TestNamespace
                                         {
                                             public class ListConverter : ITypeConverter<List<string>?, string>
                                             {
                                                 public string Convert(List<string>? source, string destination, ResolutionContext context)
                                                 {
                                                     if (source == null) throw new ArgumentNullException(nameof(source));
                                                     return string.Join(",", source);
                                                 }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<List<string>?, string>().ConvertUsing<ListConverter>();
                                                 }
                                             }
                                         }
                                         """;

        await CodeFixVerifier<AM030_CustomTypeConverterAnalyzer, AM030_CustomTypeConverterCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM030_CustomTypeConverterAnalyzer.ConverterNullHandlingIssueRule)
                    .WithLocation(8, 23)
                    .WithArguments("ListConverter", "List"),
                expectedFixedCode);
    }
}

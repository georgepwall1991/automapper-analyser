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
}

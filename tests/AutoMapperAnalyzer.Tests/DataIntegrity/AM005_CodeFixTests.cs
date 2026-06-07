using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

/// <summary>
///     End-to-end code-fix coverage for AM005 (case-sensitivity mismatch). Previously these tests ran on
///     a no-op stub framework that never applied the fix, so their expected output (a newline-indented
///     ForMember) was never validated and was in fact wrong — the real fixer appends a single-line
///     <c>.ForMember(...)</c>. Migrated onto the real <see cref="CodeFixVerifier{TAnalyzer,TCodeFix}"/>.
/// </summary>
public class AM005_CodeFixTests
{
    [Theory]
    [InlineData("firstName", "FirstName")]
    [InlineData("userName", "UserName")]
    [InlineData("emailAddress", "EmailAddress")]
    [InlineData("Url", "URL")]
    [InlineData("address1", "Address1")]
    public async Task AM005_AddsExplicitMapping_ForCaseOnlyMismatch(string sourceName, string destName)
    {
        string testCode = BuildSource(sourceName, destName, fixApplied: false);
        string fixedCode = BuildSource(sourceName, destName, fixApplied: true);

        await CodeFixVerifier<AM005_CaseSensitivityMismatchAnalyzer, AM005_CaseSensitivityMismatchCodeFixProvider>
            .VerifyFixAsync(
                testCode,
                new DiagnosticResult(AM005_CaseSensitivityMismatchAnalyzer.CaseSensitivityMismatchRule)
                    .WithLocation(19, 13)
                    .WithArguments(sourceName, destName),
                fixedCode,
                0);
    }

    [Fact]
    public async Task AM005_DoesNotFlag_WhenNamesDifferByMoreThanCase()
    {
        // 'user_name' vs 'UserName' are not case-insensitively equal (the underscore differs), so AM005
        // (case-ONLY mismatch) must stay silent — this is AM004/convention territory, not a case fix.
        string source = BuildSource("user_name", "UserName", fixApplied: false);

        await AnalyzerVerifier<AM005_CaseSensitivityMismatchAnalyzer>.VerifyAnalyzerAsync(source);
    }

    private static string BuildSource(string sourceName, string destName, bool fixApplied)
    {
        string createMap = fixApplied
            ? $"CreateMap<Source, Destination>().ForMember(dest => dest.{destName}, opt => opt.MapFrom(src => src.{sourceName}));"
            : "CreateMap<Source, Destination>();";

        return $$"""
                 using AutoMapper;

                 namespace TestNamespace
                 {
                     public class Source
                     {
                         public string {{sourceName}} { get; set; }
                     }

                     public class Destination
                     {
                         public string {{destName}} { get; set; }
                     }

                     public class TestProfile : Profile
                     {
                         public TestProfile()
                         {
                             {{createMap}}
                         }
                     }
                 }
                 """;
    }
}

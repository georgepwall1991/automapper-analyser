using System.Reflection;
using AutoMapperAnalyzer.Analyzers.ComplexMappings;
using Microsoft.CodeAnalysis.CSharp;

namespace AutoMapperAnalyzer.Tests.Helpers;

public class AM020MappingConfigurationHelpersTests
{
    private static readonly Type HelperType = typeof(AM020_NestedObjectMappingAnalyzer).Assembly
        .GetType("AutoMapperAnalyzer.Analyzers.Helpers.AM020MappingConfigurationHelpers")!;

    private static readonly MethodInfo GetSelectedTopLevelMemberNameMethod = HelperType.GetMethod(
        "GetSelectedTopLevelMemberName",
        BindingFlags.Public | BindingFlags.Static)!;

    [Fact]
    public void GetSelectedTopLevelMemberName_ShouldReturnFirstSegment_ForStringLiteralPath()
    {
        var expression = SyntaxFactory.ParseExpression("\"Address.Street\"");

        string? selectedMember =
            (string?)GetSelectedTopLevelMemberNameMethod.Invoke(null, [expression]);

        Assert.Equal("Address", selectedMember);
    }

    [Fact]
    public void GetSelectedTopLevelMemberName_ShouldReturnLiteralValue_ForTopLevelStringMember()
    {
        var expression = SyntaxFactory.ParseExpression("\"Numbers\"");

        string? selectedMember =
            (string?)GetSelectedTopLevelMemberNameMethod.Invoke(null, [expression]);

        Assert.Equal("Numbers", selectedMember);
    }
}

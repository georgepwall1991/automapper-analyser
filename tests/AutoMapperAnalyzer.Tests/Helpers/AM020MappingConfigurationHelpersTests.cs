using System.Reflection;
using AutoMapperAnalyzer.Analyzers.ComplexMappings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Tests.Helpers;

public class AM020MappingConfigurationHelpersTests
{
    private static readonly Type HelperType = typeof(AM020_NestedObjectMappingAnalyzer).Assembly
        .GetType("AutoMapperAnalyzer.Analyzers.Helpers.AM020MappingConfigurationHelpers")!;

    private static readonly MethodInfo GetSelectedTopLevelMemberNameMethod = HelperType.GetMethod(
        "GetSelectedTopLevelMemberName",
        BindingFlags.Public | BindingFlags.Static)!;

    private static readonly MethodInfo GetSelectedTopLevelMemberNameWithSemanticModelMethod = HelperType.GetMethod(
        "GetSelectedTopLevelMemberNameWithSemanticModel",
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

    [Fact]
    public void GetSelectedTopLevelMemberNameWithSemanticModel_ShouldReturnNameofValue()
    {
        (ExpressionSyntax expression, SemanticModel semanticModel) =
            GetExpressionWithSemanticModel("nameof(Destination.Address)");

        string? selectedMember =
            (string?)GetSelectedTopLevelMemberNameWithSemanticModelMethod.Invoke(null, [expression, semanticModel]);

        Assert.Equal("Address", selectedMember);
    }

    [Fact]
    public void GetSelectedTopLevelMemberNameWithSemanticModel_ShouldReturnFirstSegment_ForConstStringPath()
    {
        (ExpressionSyntax expression, SemanticModel semanticModel) =
            GetExpressionWithSemanticModel("AddressPath");

        string? selectedMember =
            (string?)GetSelectedTopLevelMemberNameWithSemanticModelMethod.Invoke(null, [expression, semanticModel]);

        Assert.Equal("Address", selectedMember);
    }

    private static (ExpressionSyntax Expression, SemanticModel SemanticModel) GetExpressionWithSemanticModel(
        string expressionText)
    {
        string source = $$"""
                          namespace TestNamespace
                          {
                              public class Address
                              {
                                  public string Street { get; set; }
                              }

                              public class Destination
                              {
                                  public Address Address { get; set; }
                              }

                              public class Test
                              {
                                  private const string AddressPath = "Address.Street";

                                  public string GetMemberName() => {{expressionText}};
                              }
                          }
                          """;

        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            "AM020MappingConfigurationHelpersTests",
            [syntaxTree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
        ExpressionSyntax expression = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<ArrowExpressionClauseSyntax>()
            .Single()
            .Expression;

        return (expression, semanticModel);
    }
}

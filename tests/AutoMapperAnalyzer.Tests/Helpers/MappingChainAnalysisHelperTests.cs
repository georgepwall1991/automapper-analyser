using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Tests.Helpers;

public class MappingChainAnalysisHelperTests
{
    [Fact]
    public void GetSelectedMemberName_ShouldReturnMemberName_ForLambdaSelector()
    {
        var expression = (ExpressionSyntax)SyntaxFactory.ParseExpression("src => src.TempData");

        string? selectedMember = MappingChainAnalysisHelper.GetSelectedMemberName(expression);

        Assert.Equal("TempData", selectedMember);
    }

    [Fact]
    public void GetSelectedMemberName_ShouldReturnFirstSegment_ForStringLiteralPath()
    {
        var expression = (ExpressionSyntax)SyntaxFactory.ParseExpression("\"TempData.Value\"");

        string? selectedMember = MappingChainAnalysisHelper.GetSelectedMemberName(expression);

        Assert.Equal("TempData", selectedMember);
    }

    [Fact]
    public void GetSelectedMemberName_ShouldReturnNameofValue_WithSemanticModel()
    {
        (ExpressionSyntax expression, SemanticModel semanticModel) =
            GetExpressionWithSemanticModel("nameof(Source.TempData)");

        string? selectedMember = MappingChainAnalysisHelper.GetSelectedMemberName(expression, semanticModel);

        Assert.Equal("TempData", selectedMember);
    }

    [Fact]
    public void GetSelectedMemberName_ShouldReturnFirstSegment_ForConstStringPath()
    {
        (ExpressionSyntax expression, SemanticModel semanticModel) =
            GetExpressionWithSemanticModel("TempDataPath");

        string? selectedMember = MappingChainAnalysisHelper.GetSelectedMemberName(expression, semanticModel);

        Assert.Equal("TempData", selectedMember);
    }

    private static (ExpressionSyntax Expression, SemanticModel SemanticModel) GetExpressionWithSemanticModel(
        string expressionText)
    {
        string source = $$"""
                          namespace TestNamespace
                          {
                              public class Source
                              {
                                  public string TempData { get; set; }
                              }

                              public class Test
                              {
                                  private const string TempDataPath = "TempData.Value";

                                  public string GetMemberName() => {{expressionText}};
                              }
                          }
                          """;

        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            "MappingChainAnalysisHelperTests",
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

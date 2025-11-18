using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Tests.Helpers;

/// <summary>
///     Tests for CodeFixSyntaxHelper static helper methods that generate AutoMapper syntax patterns.
/// </summary>
public class CodeFixSyntaxHelperTests
{
    #region Helper Methods

    /// <summary>
    ///     Creates a sample CreateMap&lt;Source, Destination&gt;() invocation for testing.
    /// </summary>
    private static InvocationExpressionSyntax CreateSampleCreateMapInvocation()
    {
        // Parse a simple CreateMap<Source, Destination>() invocation
        ExpressionSyntax expression = SyntaxFactory.ParseExpression("CreateMap<Source, Destination>()");
        return (InvocationExpressionSyntax)expression;
    }

    #endregion

    #region CreateForMemberWithMapFrom Tests

    [Fact]
    public void CreateForMemberWithMapFrom_ShouldGenerateCorrectSyntax_ForSimpleProperty()
    {
        // Arrange
        InvocationExpressionSyntax createMapInvocation = CreateSampleCreateMapInvocation();

        // Act
        InvocationExpressionSyntax result = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
            createMapInvocation,
            "Name",
            "src.SourceName");

        // Assert
        string resultText = result.ToFullString();
        Assert.Contains("ForMember", resultText);
        Assert.Contains("dest.Name", resultText);
        Assert.Contains("MapFrom", resultText);
        Assert.Contains("src.SourceName", resultText);
    }

    [Fact]
    public void CreateForMemberWithMapFrom_ShouldGenerateCorrectSyntax_ForConstantValue()
    {
        // Arrange
        InvocationExpressionSyntax createMapInvocation = CreateSampleCreateMapInvocation();

        // Act
        InvocationExpressionSyntax result = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
            createMapInvocation,
            "Age",
            "0");

        // Assert
        string resultText = result.ToFullString();
        Assert.Contains("ForMember", resultText);
        Assert.Contains("dest.Age", resultText);
        Assert.Contains("MapFrom", resultText);
        // Verify constant 0 is in the expression
        Assert.Matches(@"MapFrom.*0", resultText);
    }

    [Fact]
    public void CreateForMemberWithMapFrom_ShouldGenerateCorrectSyntax_ForComplexExpression()
    {
        // Arrange
        InvocationExpressionSyntax createMapInvocation = CreateSampleCreateMapInvocation();

        // Act
        InvocationExpressionSyntax result = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
            createMapInvocation,
            "FullName",
            "src.FirstName + \" \" + src.LastName");

        // Assert
        string resultText = result.ToFullString();
        Assert.Contains("ForMember", resultText);
        Assert.Contains("dest.FullName", resultText);
        Assert.Contains("MapFrom", resultText);
        Assert.Contains("src.FirstName", resultText);
        Assert.Contains("src.LastName", resultText);
    }

    [Fact]
    public void CreateForMemberWithMapFrom_ShouldGenerateCorrectSyntax_ForLinqExpression()
    {
        // Arrange
        InvocationExpressionSyntax createMapInvocation = CreateSampleCreateMapInvocation();

        // Act
        InvocationExpressionSyntax result = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
            createMapInvocation,
            "Items",
            "src.Items.Select(x => x.ToString())");

        // Assert
        string resultText = result.ToFullString();
        Assert.Contains("ForMember", resultText);
        Assert.Contains("dest.Items", resultText);
        Assert.Contains("MapFrom", resultText);
        Assert.Contains("src.Items.Select", resultText);
        Assert.Contains("x.ToString()", resultText);
    }

    [Fact]
    public void CreateForMemberWithMapFrom_ShouldChainToOriginalInvocation()
    {
        // Arrange
        InvocationExpressionSyntax createMapInvocation = CreateSampleCreateMapInvocation();
        string originalText = createMapInvocation.ToFullString();

        // Act
        InvocationExpressionSyntax result = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
            createMapInvocation,
            "Property",
            "src.Value");

        // Assert
        // The result should start with the original invocation
        string resultText = result.ToFullString();
        Assert.StartsWith(originalText.Trim(), resultText.Trim());
        Assert.Contains(".ForMember", resultText);
    }

    #endregion

    #region CreateForMemberWithIgnore Tests

    [Fact]
    public void CreateForMemberWithIgnore_ShouldGenerateCorrectSyntax()
    {
        // Arrange
        InvocationExpressionSyntax createMapInvocation = CreateSampleCreateMapInvocation();

        // Act
        InvocationExpressionSyntax result = CodeFixSyntaxHelper.CreateForMemberWithIgnore(
            createMapInvocation,
            "UnmappedProperty");

        // Assert
        string resultText = result.ToFullString();
        Assert.Contains("ForMember", resultText);
        Assert.Contains("dest.UnmappedProperty", resultText);
        Assert.Contains("Ignore()", resultText);
    }

    [Fact]
    public void CreateForMemberWithIgnore_ShouldChainToOriginalInvocation()
    {
        // Arrange
        InvocationExpressionSyntax createMapInvocation = CreateSampleCreateMapInvocation();
        string originalText = createMapInvocation.ToFullString();

        // Act
        InvocationExpressionSyntax result = CodeFixSyntaxHelper.CreateForMemberWithIgnore(
            createMapInvocation,
            "IgnoredField");

        // Assert
        string resultText = result.ToFullString();
        Assert.StartsWith(originalText.Trim(), resultText.Trim());
        Assert.Contains(".ForMember", resultText);
    }

    [Fact]
    public void CreateForMemberWithIgnore_ShouldHandleMultipleProperties()
    {
        // Arrange
        InvocationExpressionSyntax createMapInvocation = CreateSampleCreateMapInvocation();

        // Act
        InvocationExpressionSyntax result1 =
            CodeFixSyntaxHelper.CreateForMemberWithIgnore(createMapInvocation, "Property1");
        InvocationExpressionSyntax result2 = CodeFixSyntaxHelper.CreateForMemberWithIgnore(result1, "Property2");

        // Assert
        string resultText = result2.ToFullString();
        Assert.Contains("dest.Property1", resultText);
        Assert.Contains("dest.Property2", resultText);
        int ignoreCount = resultText.Split(new[] { "Ignore()" }, StringSplitOptions.None).Length - 1;
        Assert.Equal(2, ignoreCount);
    }

    #endregion

    #region CreateForSourceMemberWithDoNotValidate Tests

    [Fact]
    public void CreateForSourceMemberWithDoNotValidate_ShouldGenerateCorrectSyntax()
    {
        // Arrange
        InvocationExpressionSyntax createMapInvocation = CreateSampleCreateMapInvocation();

        // Act
        InvocationExpressionSyntax result = CodeFixSyntaxHelper.CreateForSourceMemberWithDoNotValidate(
            createMapInvocation,
            "UnusedSourceProperty");

        // Assert
        string resultText = result.ToFullString();
        Assert.Contains("ForSourceMember", resultText);
        Assert.Contains("src.UnusedSourceProperty", resultText);
        Assert.Contains("DoNotValidate()", resultText);
    }

    [Fact]
    public void CreateForSourceMemberWithDoNotValidate_ShouldChainToOriginalInvocation()
    {
        // Arrange
        InvocationExpressionSyntax createMapInvocation = CreateSampleCreateMapInvocation();
        string originalText = createMapInvocation.ToFullString();

        // Act
        InvocationExpressionSyntax result = CodeFixSyntaxHelper.CreateForSourceMemberWithDoNotValidate(
            createMapInvocation,
            "TempData");

        // Assert
        string resultText = result.ToFullString();
        Assert.StartsWith(originalText.Trim(), resultText.Trim());
        Assert.Contains(".ForSourceMember", resultText);
    }

    [Fact]
    public void CreateForSourceMemberWithDoNotValidate_ShouldHandleMultipleProperties()
    {
        // Arrange
        InvocationExpressionSyntax createMapInvocation = CreateSampleCreateMapInvocation();

        // Act
        InvocationExpressionSyntax result1 =
            CodeFixSyntaxHelper.CreateForSourceMemberWithDoNotValidate(createMapInvocation, "Temp1");
        InvocationExpressionSyntax result2 =
            CodeFixSyntaxHelper.CreateForSourceMemberWithDoNotValidate(result1, "Temp2");

        // Assert
        string resultText = result2.ToFullString();
        Assert.Contains("src.Temp1", resultText);
        Assert.Contains("src.Temp2", resultText);
        int doNotValidateCount = resultText.Split(new[] { "DoNotValidate()" }, StringSplitOptions.None).Length - 1;
        Assert.Equal(2, doNotValidateCount);
    }

    #endregion

    #region Syntax Validation Tests

    [Fact]
    public void CreateForMemberWithMapFrom_ShouldProduceValidSyntaxTree()
    {
        // Arrange
        InvocationExpressionSyntax createMapInvocation = CreateSampleCreateMapInvocation();

        // Act
        InvocationExpressionSyntax result = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
            createMapInvocation,
            "Name",
            "src.SourceName");

        // Assert
        // Verify the syntax tree has no errors
        SyntaxTree syntaxTree = SyntaxFactory.SyntaxTree(result);
        IEnumerable<Diagnostic> diagnostics = syntaxTree.GetDiagnostics();
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void CreateForMemberWithIgnore_ShouldProduceValidSyntaxTree()
    {
        // Arrange
        InvocationExpressionSyntax createMapInvocation = CreateSampleCreateMapInvocation();

        // Act
        InvocationExpressionSyntax result = CodeFixSyntaxHelper.CreateForMemberWithIgnore(
            createMapInvocation,
            "UnmappedProperty");

        // Assert
        SyntaxTree syntaxTree = SyntaxFactory.SyntaxTree(result);
        IEnumerable<Diagnostic> diagnostics = syntaxTree.GetDiagnostics();
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void CreateForSourceMemberWithDoNotValidate_ShouldProduceValidSyntaxTree()
    {
        // Arrange
        InvocationExpressionSyntax createMapInvocation = CreateSampleCreateMapInvocation();

        // Act
        InvocationExpressionSyntax result = CodeFixSyntaxHelper.CreateForSourceMemberWithDoNotValidate(
            createMapInvocation,
            "UnusedProperty");

        // Assert
        SyntaxTree syntaxTree = SyntaxFactory.SyntaxTree(result);
        IEnumerable<Diagnostic> diagnostics = syntaxTree.GetDiagnostics();
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void MixedForMemberAndForSourceMember_ShouldProduceValidSyntaxTree()
    {
        // Arrange
        InvocationExpressionSyntax createMapInvocation = CreateSampleCreateMapInvocation();

        // Act - Chain multiple operations
        InvocationExpressionSyntax result = CodeFixSyntaxHelper.CreateForMemberWithMapFrom(
            createMapInvocation,
            "Name",
            "src.SourceName");
        result = CodeFixSyntaxHelper.CreateForMemberWithIgnore(result, "IgnoredProp");
        result = CodeFixSyntaxHelper.CreateForSourceMemberWithDoNotValidate(result, "TempData");

        // Assert
        string resultText = result.ToFullString();
        Assert.Contains("ForMember", resultText);
        Assert.Contains("ForSourceMember", resultText);
        Assert.Contains("MapFrom", resultText);
        Assert.Contains("Ignore()", resultText);
        Assert.Contains("DoNotValidate()", resultText);

        SyntaxTree syntaxTree = SyntaxFactory.SyntaxTree(result);
        IEnumerable<Diagnostic> diagnostics = syntaxTree.GetDiagnostics();
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    #endregion
}

using AutoMapper;
using AutoMapperAnalyzer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Tests.Helpers;

/// <summary>
///     Tests for FuzzyMatchHelper static methods
/// </summary>
public class FuzzyMatchHelperTests
{
    #region ComputeLevenshteinDistance Tests

    [Fact]
    public void ComputeLevenshteinDistance_ShouldReturnZero_WhenStringsAreIdentical()
    {
        int result = FuzzyMatchHelper.ComputeLevenshteinDistance("Email", "Email");

        Assert.Equal(0, result);
    }

    [Fact]
    public void ComputeLevenshteinDistance_ShouldReturnOne_ForSingleCharacterDeletion()
    {
        int result = FuzzyMatchHelper.ComputeLevenshteinDistance("Names", "Name");

        Assert.Equal(1, result);
    }

    [Fact]
    public void ComputeLevenshteinDistance_ShouldReturnOne_ForSingleCharacterInsertion()
    {
        int result = FuzzyMatchHelper.ComputeLevenshteinDistance("Name", "Names");

        Assert.Equal(1, result);
    }

    [Fact]
    public void ComputeLevenshteinDistance_ShouldReturnOne_ForSingleCharacterSubstitution()
    {
        int result = FuzzyMatchHelper.ComputeLevenshteinDistance("cat", "car");

        Assert.Equal(1, result);
    }

    [Fact]
    public void ComputeLevenshteinDistance_ShouldReturnTwo_ForCharacterSwap()
    {
        // Swapping two characters requires 2 edits in Levenshtein distance
        int result = FuzzyMatchHelper.ComputeLevenshteinDistance("Email", "Emial");

        Assert.Equal(2, result);
    }

    [Fact]
    public void ComputeLevenshteinDistance_ShouldReturnThree_ForCompletelyDifferentStrings()
    {
        int result = FuzzyMatchHelper.ComputeLevenshteinDistance("abc", "xyz");

        Assert.Equal(3, result);
    }

    [Fact]
    public void ComputeLevenshteinDistance_ShouldReturnStringLength_WhenOneStringIsEmpty()
    {
        int result = FuzzyMatchHelper.ComputeLevenshteinDistance("", "hello");

        Assert.Equal(5, result);
    }

    [Fact]
    public void ComputeLevenshteinDistance_ShouldReturnStringLength_WhenOtherStringIsEmpty()
    {
        int result = FuzzyMatchHelper.ComputeLevenshteinDistance("hello", "");

        Assert.Equal(5, result);
    }

    [Fact]
    public void ComputeLevenshteinDistance_ShouldReturnZero_WhenBothStringsAreEmpty()
    {
        int result = FuzzyMatchHelper.ComputeLevenshteinDistance("", "");

        Assert.Equal(0, result);
    }

    #endregion

    #region IsFuzzyMatchCandidate Tests

    [Fact]
    public void IsFuzzyMatchCandidate_ShouldReturnTrue_WhenDistance1AndSameType()
    {
        const string code = @"
            public class TestClass
            {
                public string Emal { get; set; }
            }";

        CSharpCompilation compilation = CreateCompilation(code);
        SyntaxTree tree = compilation.SyntaxTrees.First();
        SemanticModel semanticModel = compilation.GetSemanticModel(tree);
        IPropertySymbol propertySymbol = GetPropertySymbol(tree, semanticModel, "Emal");
        ITypeSymbol typeSymbol = propertySymbol.Type;

        bool result = FuzzyMatchHelper.IsFuzzyMatchCandidate("Email", propertySymbol, typeSymbol);

        Assert.True(result);
    }

    [Fact]
    public void IsFuzzyMatchCandidate_ShouldReturnTrue_WhenDistance2AndSameType()
    {
        const string code = @"
            public class TestClass
            {
                public string Usr { get; set; }
            }";

        CSharpCompilation compilation = CreateCompilation(code);
        SyntaxTree tree = compilation.SyntaxTrees.First();
        SemanticModel semanticModel = compilation.GetSemanticModel(tree);
        IPropertySymbol propertySymbol = GetPropertySymbol(tree, semanticModel, "Usr");
        ITypeSymbol typeSymbol = propertySymbol.Type;

        bool result = FuzzyMatchHelper.IsFuzzyMatchCandidate("User", propertySymbol, typeSymbol);

        Assert.True(result);
    }

    [Fact]
    public void IsFuzzyMatchCandidate_ShouldReturnFalse_WhenDistanceGreaterThan2()
    {
        const string code = @"
            public class TestClass
            {
                public string Xyz { get; set; }
            }";

        CSharpCompilation compilation = CreateCompilation(code);
        SyntaxTree tree = compilation.SyntaxTrees.First();
        SemanticModel semanticModel = compilation.GetSemanticModel(tree);
        IPropertySymbol propertySymbol = GetPropertySymbol(tree, semanticModel, "Xyz");
        ITypeSymbol typeSymbol = propertySymbol.Type;

        bool result = FuzzyMatchHelper.IsFuzzyMatchCandidate("Email", propertySymbol, typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void IsFuzzyMatchCandidate_ShouldReturnFalse_WhenExactMatch()
    {
        const string code = @"
            public class TestClass
            {
                public string Name { get; set; }
            }";

        CSharpCompilation compilation = CreateCompilation(code);
        SyntaxTree tree = compilation.SyntaxTrees.First();
        SemanticModel semanticModel = compilation.GetSemanticModel(tree);
        IPropertySymbol propertySymbol = GetPropertySymbol(tree, semanticModel, "Name");
        ITypeSymbol typeSymbol = propertySymbol.Type;

        bool result = FuzzyMatchHelper.IsFuzzyMatchCandidate("Name", propertySymbol, typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void IsFuzzyMatchCandidate_ShouldReturnFalse_WhenLengthDifferenceTooLarge()
    {
        const string code = @"
            public class TestClass
            {
                public string N { get; set; }
            }";

        CSharpCompilation compilation = CreateCompilation(code);
        SyntaxTree tree = compilation.SyntaxTrees.First();
        SemanticModel semanticModel = compilation.GetSemanticModel(tree);
        IPropertySymbol propertySymbol = GetPropertySymbol(tree, semanticModel, "N");
        ITypeSymbol typeSymbol = propertySymbol.Type;

        // "N" (length 1) vs "Name" (length 4) has length difference of 3, which exceeds the threshold of 2
        bool result = FuzzyMatchHelper.IsFuzzyMatchCandidate("Name", propertySymbol, typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void IsFuzzyMatchCandidate_ShouldReturnFalse_WhenIncompatibleTypes()
    {
        const string code = @"
            public class TestClass
            {
                public int Emal { get; set; }
            }";

        CSharpCompilation compilation = CreateCompilation(code);
        SyntaxTree tree = compilation.SyntaxTrees.First();
        SemanticModel semanticModel = compilation.GetSemanticModel(tree);
        IPropertySymbol propertySymbol = GetPropertySymbol(tree, semanticModel, "Emal");
        var stringType = compilation.GetSpecialType(SpecialType.System_String);

        bool result = FuzzyMatchHelper.IsFuzzyMatchCandidate("Email", propertySymbol, stringType);

        Assert.False(result);
    }

    [Fact]
    public void IsFuzzyMatchCandidate_ShouldReturnTrue_WhenNullableTypesCompatible()
    {
        const string code = @"
            public class TestClass
            {
                public int? Id { get; set; }
            }";

        CSharpCompilation compilation = CreateCompilation(code);
        SyntaxTree tree = compilation.SyntaxTrees.First();
        SemanticModel semanticModel = compilation.GetSemanticModel(tree);
        IPropertySymbol propertySymbol = GetPropertySymbol(tree, semanticModel, "Id");
        var intType = compilation.GetSpecialType(SpecialType.System_Int32);

        // Distance 1: "Id" vs "Id", but we're passing "Idx" which has distance 1
        bool result = FuzzyMatchHelper.IsFuzzyMatchCandidate("Idx", propertySymbol, intType);

        Assert.True(result);
    }

    [Fact]
    public void IsFuzzyMatchCandidate_ShouldReturnFalse_WhenDistanceExactly3()
    {
        const string code = @"
            public class TestClass
            {
                public string Abc { get; set; }
            }";

        CSharpCompilation compilation = CreateCompilation(code);
        SyntaxTree tree = compilation.SyntaxTrees.First();
        SemanticModel semanticModel = compilation.GetSemanticModel(tree);
        IPropertySymbol propertySymbol = GetPropertySymbol(tree, semanticModel, "Abc");
        ITypeSymbol typeSymbol = propertySymbol.Type;

        bool result = FuzzyMatchHelper.IsFuzzyMatchCandidate("Xyz", propertySymbol, typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void IsFuzzyMatchCandidate_ShouldReturnTrue_WhenDistance2WithLengthDifference0()
    {
        const string code = @"
            public class TestClass
            {
                public string Aa { get; set; }
            }";

        CSharpCompilation compilation = CreateCompilation(code);
        SyntaxTree tree = compilation.SyntaxTrees.First();
        SemanticModel semanticModel = compilation.GetSemanticModel(tree);
        IPropertySymbol propertySymbol = GetPropertySymbol(tree, semanticModel, "Aa");
        ITypeSymbol typeSymbol = propertySymbol.Type;

        // "Aa" vs "Bb" has distance 2 and same length (no length difference)
        bool result = FuzzyMatchHelper.IsFuzzyMatchCandidate("Bb", propertySymbol, typeSymbol);

        Assert.True(result);
    }

    [Fact]
    public void IsFuzzyMatchCandidate_ShouldReturnTrue_WhenLengthDifferenceExactly2()
    {
        const string code = @"
            public class TestClass
            {
                public string Name { get; set; }
            }";

        CSharpCompilation compilation = CreateCompilation(code);
        SyntaxTree tree = compilation.SyntaxTrees.First();
        SemanticModel semanticModel = compilation.GetSemanticModel(tree);
        IPropertySymbol propertySymbol = GetPropertySymbol(tree, semanticModel, "Name");
        ITypeSymbol typeSymbol = propertySymbol.Type;

        // "Na" (length 2) vs "Name" (length 4) has length difference 2 and distance 2
        bool result = FuzzyMatchHelper.IsFuzzyMatchCandidate("Na", propertySymbol, typeSymbol);

        Assert.True(result);
    }

    #endregion

    #region Helper Methods

    private static CSharpCompilation CreateCompilation(string code)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);
        PortableExecutableReference[] references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Profile).Assembly.Location)
        };

        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static IPropertySymbol GetPropertySymbol(SyntaxTree tree, SemanticModel semanticModel, string propertyName)
    {
        PropertyDeclarationSyntax propertyDeclaration = tree.GetRoot()
            .DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .First(p => p.Identifier.Text == propertyName);

        return semanticModel.GetDeclaredSymbol(propertyDeclaration)!;
    }

    #endregion
}

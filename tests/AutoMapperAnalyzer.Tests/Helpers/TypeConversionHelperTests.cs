using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Tests.Helpers;

public class TypeConversionHelperTests
{
    [Theory]
    [InlineData("string", "string.Empty")]
    [InlineData("String", "string.Empty")]
    [InlineData("STRING", "string.Empty")]
    public void GetDefaultValueForType_ShouldReturnStringEmpty_ForStringTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetDefaultValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("int", "0")]
    [InlineData("INT", "0")]
    public void GetDefaultValueForType_ShouldReturnZero_ForIntTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetDefaultValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("long", "0L")]
    [InlineData("Long", "0L")]
    [InlineData("LONG", "0L")]
    public void GetDefaultValueForType_ShouldReturnZeroL_ForLongTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetDefaultValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("double", "0.0")]
    [InlineData("Double", "0.0")]
    [InlineData("DOUBLE", "0.0")]
    public void GetDefaultValueForType_ShouldReturnZeroPointZero_ForDoubleTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetDefaultValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("float", "0.0f")]
    [InlineData("Float", "0.0f")]
    [InlineData("FLOAT", "0.0f")]
    public void GetDefaultValueForType_ShouldReturnZeroPointZeroF_ForFloatTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetDefaultValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("decimal", "0m")]
    [InlineData("Decimal", "0m")]
    [InlineData("DECIMAL", "0m")]
    public void GetDefaultValueForType_ShouldReturnZeroM_ForDecimalTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetDefaultValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("bool", "false")]
    [InlineData("Bool", "false")]
    [InlineData("BOOL", "false")]
    public void GetDefaultValueForType_ShouldReturnFalse_ForBoolTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetDefaultValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("datetime", "DateTime.MinValue")]
    [InlineData("DateTime", "DateTime.MinValue")]
    [InlineData("DATETIME", "DateTime.MinValue")]
    public void GetDefaultValueForType_ShouldReturnDateTimeMinValue_ForDateTimeTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetDefaultValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("guid", "Guid.Empty")]
    [InlineData("Guid", "Guid.Empty")]
    [InlineData("GUID", "Guid.Empty")]
    public void GetDefaultValueForType_ShouldReturnGuidEmpty_ForGuidTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetDefaultValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("MyCustomType", "default")]
    [InlineData("UnknownType", "default")]
    [InlineData("", "default")]
    public void GetDefaultValueForType_ShouldReturnDefault_ForUnknownTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetDefaultValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("string", "\"DefaultValue\"")]
    [InlineData("String", "\"DefaultValue\"")]
    [InlineData("STRING", "\"DefaultValue\"")]
    public void GetSampleValueForType_ShouldReturnDefaultValue_ForStringTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetSampleValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("int", "1")]
    [InlineData("INT", "1")]
    public void GetSampleValueForType_ShouldReturnOne_ForIntTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetSampleValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("long", "1L")]
    [InlineData("Long", "1L")]
    [InlineData("LONG", "1L")]
    public void GetSampleValueForType_ShouldReturnOneL_ForLongTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetSampleValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("double", "1.0")]
    [InlineData("Double", "1.0")]
    [InlineData("DOUBLE", "1.0")]
    public void GetSampleValueForType_ShouldReturnOnePointZero_ForDoubleTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetSampleValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("float", "1.0f")]
    [InlineData("Float", "1.0f")]
    [InlineData("FLOAT", "1.0f")]
    public void GetSampleValueForType_ShouldReturnOnePointZeroF_ForFloatTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetSampleValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("decimal", "1.0m")]
    [InlineData("Decimal", "1.0m")]
    [InlineData("DECIMAL", "1.0m")]
    public void GetSampleValueForType_ShouldReturnOnePointZeroM_ForDecimalTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetSampleValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("bool", "true")]
    [InlineData("Bool", "true")]
    [InlineData("BOOL", "true")]
    public void GetSampleValueForType_ShouldReturnTrue_ForBoolTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetSampleValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("datetime", "DateTime.Now")]
    [InlineData("DateTime", "DateTime.Now")]
    [InlineData("DATETIME", "DateTime.Now")]
    public void GetSampleValueForType_ShouldReturnDateTimeNow_ForDateTimeTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetSampleValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("guid", "Guid.NewGuid()")]
    [InlineData("Guid", "Guid.NewGuid()")]
    [InlineData("GUID", "Guid.NewGuid()")]
    public void GetSampleValueForType_ShouldReturnGuidNewGuid_ForGuidTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetSampleValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("MyCustomType", "new MyCustomType()")]
    [InlineData("Person", "new Person()")]
    [InlineData("Order", "new Order()")]
    public void GetSampleValueForType_ShouldReturnNewInstance_ForCustomTypes(string type, string expected)
    {
        var result = TypeConversionHelper.GetSampleValueForType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("string", true)]
    [InlineData("String", true)]
    [InlineData("STRING", true)]
    [InlineData("StRiNg", true)]
    public void IsStringType_ShouldReturnTrue_ForStringTypes(string type, bool expected)
    {
        var result = TypeConversionHelper.IsStringType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("System.String", true)]
    [InlineData("system.string", true)]
    [InlineData("SYSTEM.STRING", true)]
    public void IsStringType_ShouldReturnTrue_ForFullyQualifiedStringTypes(string type, bool expected)
    {
        var result = TypeConversionHelper.IsStringType(type);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("int", false)]
    [InlineData("bool", false)]
    [InlineData("double", false)]
    [InlineData("MyCustomType", false)]
    [InlineData("", false)]
    public void IsStringType_ShouldReturnFalse_ForNonStringTypes(string type, bool expected)
    {
        var result = TypeConversionHelper.IsStringType(type);
        Assert.Equal(expected, result);
    }
}

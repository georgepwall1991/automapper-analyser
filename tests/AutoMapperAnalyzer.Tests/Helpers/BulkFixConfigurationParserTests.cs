using AutoMapperAnalyzer.Analyzers.Helpers;

namespace AutoMapperAnalyzer.Tests.Helpers;

public class BulkFixConfigurationParserTests
{
    [Fact]
    public void GenerateConfigurationComment_ShouldNotContainLegacyPlaceholderActions()
    {
        string comment = BulkFixConfigurationParser.GenerateConfigurationComment(new[]
        {
            ("RequiredName", "string", BulkFixAction.Default, (string?)null)
        });

        Assert.DoesNotContain("TODO", comment, StringComparison.Ordinal);
        Assert.DoesNotContain("CUSTOM", comment, StringComparison.Ordinal);
        Assert.DoesNotContain("NULLABLE", comment, StringComparison.Ordinal);
        Assert.Contains("DEFAULT", comment, StringComparison.Ordinal);
        Assert.Contains("FUZZY", comment, StringComparison.Ordinal);
        Assert.Contains("IGNORE", comment, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ShouldMapLegacyActionsToDefault_ForBackwardCompatibility()
    {
        const string comment = """
                               /* BULK-FIX-CONFIG:
                                * Property Name           | Type              | Action        | Parameter
                                * -------------------------------------------------------------------------------
                                * RequiredA               | string            | TODO          |
                                * RequiredB               | int               | CUSTOM        |
                                * RequiredC               | string            | NULLABLE      |
                                */
                               """;

        BulkFixConfiguration? config = BulkFixConfigurationParser.Parse(comment);

        Assert.NotNull(config);
        Assert.Equal(3, config!.PropertyActions.Count);
        Assert.All(config.PropertyActions, action => Assert.Equal(BulkFixAction.Default, action.Action));
    }

    [Fact]
    public void GenerateConfigurationComment_ShouldRenderLegacyEnumValuesAsDefault()
    {
        string comment = BulkFixConfigurationParser.GenerateConfigurationComment(new[]
        {
            ("LegacyTodo", "string", BulkFixAction.Todo, (string?)null),
            ("LegacyCustom", "int", BulkFixAction.Custom, (string?)null),
            ("LegacyNullable", "bool", BulkFixAction.Nullable, (string?)null)
        });

        Assert.Contains("LegacyTodo", comment, StringComparison.Ordinal);
        Assert.Contains("LegacyCustom", comment, StringComparison.Ordinal);
        Assert.Contains("LegacyNullable", comment, StringComparison.Ordinal);
        Assert.DoesNotContain("| TODO", comment, StringComparison.Ordinal);
        Assert.DoesNotContain("| CUSTOM", comment, StringComparison.Ordinal);
        Assert.DoesNotContain("| NULLABLE", comment, StringComparison.Ordinal);
        Assert.Contains("| DEFAULT", comment, StringComparison.Ordinal);
    }
}

using SDV.DependenciesAnalyzer.Helpers;

namespace SDV.DependenciesAnalyzer.Tests.Helpers;

public class FilterHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildFilter_BlankPattern_ReturnsNull(string? pattern)
    {
        var filter = FilterHelper.BuildFilter(pattern!);

        Assert.Null(filter);
    }

    [Theory]
    // exact
    [InlineData("Newtonsoft.Json", "Newtonsoft.Json", true)]
    [InlineData("Newtonsoft.Json", "Newtonsoft.Json.Bson", false)]
    // prefix
    [InlineData("Microsoft.*", "Microsoft.Extensions.Logging", true)]
    [InlineData("Microsoft.*", "Microsoft.", true)]
    [InlineData("Microsoft.*", "System.Microsoft.Text", false)]
    // suffix
    [InlineData("*.Abstractions", "Microsoft.Extensions.Logging.Abstractions", true)]
    [InlineData("*.Abstractions", "Abstractions.Core", false)]
    // split
    [InlineData("Serilog*Console", "Serilog.Sinks.Console", true)]
    [InlineData("Serilog*Console", "Serilog.Sinks.File", false)]
    [InlineData("Serilog*Console", "Logging.Console", false)]
    // contains
    [InlineData("*Logging*", "Microsoft.Extensions.Logging.Abstractions", true)]
    [InlineData("*Logging*", "Logging", true)]
    [InlineData("*Logging*", "Serilog", false)]
    public void BuildFilter_ValidPattern_MatchesExpectedNames(string pattern, string name, bool expected)
    {
        var filter = FilterHelper.BuildFilter(pattern);

        Assert.NotNull(filter);
        Assert.Equal(expected, filter(name));
    }

    [Fact]
    public void BuildFilter_PatternWithSurroundingWhitespace_IsTrimmed()
    {
        var filter = FilterHelper.BuildFilter("  System.*  ");

        Assert.NotNull(filter);
        Assert.True(filter("System.Text.Json"));
    }

    [Theory]
    [InlineData("Microsoft.*", "microsoft.extensions")]
    [InlineData("Newtonsoft.Json", "newtonsoft.json")]
    public void BuildFilter_DifferentCase_DoesNotMatch(string pattern, string name)
    {
        var filter = FilterHelper.BuildFilter(pattern);

        Assert.NotNull(filter);
        Assert.False(filter(name));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("a*b*c")]
    [InlineData("*a*b")]
    [InlineData("a*b*")]
    [InlineData("*a*b*")]
    public void BuildFilter_UnsupportedWildcardCombination_Throws(string pattern)
    {
        var ex = Assert.Throws<ArgumentException>(() => FilterHelper.BuildFilter(pattern));

        Assert.Contains(pattern, ex.Message);
    }
}

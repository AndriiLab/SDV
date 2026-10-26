using SDV.DependenciesAnalyzer.Interfaces;

namespace SDV.DependenciesAnalyzer.Tests.Interfaces;

public class TreeGeneratorConfigurationTests
{
    [Fact]
    public void Constructor_Defaults_DependentProjectsDisabled()
    {
        var configuration = new TreeGeneratorConfiguration("a.sln", [], []);

        Assert.Equal("a.sln", configuration.SlnFilePath);
        Assert.False(configuration.IncludeDependentProjects);
    }

    [Theory]
    [InlineData("Anything")]
    [InlineData("Microsoft.Extensions.Logging")]
    public void IsPackageEnabled_NoFilters_EnablesEverything(string name)
    {
        var configuration = new TreeGeneratorConfiguration(string.Empty, [], []);

        Assert.True(configuration.IsPackageEnabled(name));
    }

    [Theory]
    [InlineData("Serilog", true)]
    [InlineData("Serilog.Sinks.File", true)]
    [InlineData("Newtonsoft.Json", true)]
    [InlineData("Microsoft.Extensions.Logging", false)]
    public void IsPackageEnabled_IncludeFilters_EnablesOnlyMatchingAny(string name, bool expected)
    {
        var configuration = new TreeGeneratorConfiguration(string.Empty, ["Serilog*", "Newtonsoft.Json"], []);

        Assert.Equal(expected, configuration.IsPackageEnabled(name));
    }

    [Theory]
    [InlineData("Microsoft.Extensions.Logging", false)]
    [InlineData("System.Text.Json", false)]
    [InlineData("Serilog", true)]
    public void IsPackageEnabled_ExcludeFilters_DisablesMatching(string name, bool expected)
    {
        var configuration = new TreeGeneratorConfiguration(string.Empty, [], ["Microsoft.*", "System.*"]);

        Assert.Equal(expected, configuration.IsPackageEnabled(name));
    }

    [Fact]
    public void IsPackageEnabled_IncludedAndExcluded_ExcludeWins()
    {
        var configuration = new TreeGeneratorConfiguration(string.Empty, ["Serilog*"], ["*.Console"]);

        Assert.True(configuration.IsPackageEnabled("Serilog.Sinks.File"));
        Assert.False(configuration.IsPackageEnabled("Serilog.Sinks.Console"));
    }

    [Fact]
    public void IsPackageEnabled_BlankFilters_AreIgnored()
    {
        var configuration = new TreeGeneratorConfiguration(string.Empty, ["", "  "], [" "]);

        Assert.True(configuration.IsPackageEnabled("Anything"));
    }

    [Fact]
    public void Constructor_InvalidFilter_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TreeGeneratorConfiguration(string.Empty, [], ["*"]));
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using SDV.DependenciesAnalyzer.AssetsJson;
using SDV.DependenciesAnalyzer.Exceptions;
using SDV.DependenciesAnalyzer.PackagesConfig;
using SDV.DependenciesAnalyzer.Tests.TestUtils;

namespace SDV.DependenciesAnalyzer.Tests.Structure;

public sealed class ExtractorBuilderTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly FakeLogger _logger = new();

    public void Dispose() => _dir.Dispose();

    [Theory]
    [InlineData(@"C:\src\App\obj\project.assets.json", true)]
    [InlineData(@"C:\src\App\packages.config", false)]
    [InlineData("", false)]
    public void AssetsExtractorBuilder_IsCompatible_OnlyForAssetsFile(string source, bool expected)
    {
        Assert.Equal(expected, new AssetsExtractorBuilder(_logger).IsCompatible("App", source));
    }

    [Theory]
    [InlineData(@"C:\src\App\packages.config", true)]
    [InlineData(@"C:\src\App\obj\project.assets.json", false)]
    [InlineData("", false)]
    public void PackagesExtractorBuilder_IsCompatible_OnlyForPackagesConfig(string source, bool expected)
    {
        Assert.Equal(expected, new PackagesExtractorBuilder(_logger).IsCompatible("App", source));
    }

    [Fact]
    public void AssetsExtractor_ValidAssetsFile_ExposesDependencies()
    {
        var csproj = _dir.WriteFile(Path.Combine("App", "App.csproj"), TestFiles.SdkCsproj());
        var assetsPath = new AssetsJsonBuilder(csproj)
            .WithDirectDependency("Serilog.Sinks.File", "5.0.0")
            .WithPackage("Serilog.Sinks.File", "5.0.0", "Serilog")
            .WithPackage("Serilog", "2.10.0")
            .WithProjectReference("Core")
            .WriteTo();

        var extractor = new AssetsExtractorBuilder(_logger).GetExtractor(assetsPath, csproj, _dir.Combine("App.sln"));

        Assert.Equal(["Serilog.Sinks.File"], extractor.DirectDependencies());
        Assert.Equal(["Serilog", "Serilog.Sinks.File"], extractor.AllDependencies().Keys.Order());
        Assert.Equal(["Serilog"], extractor.ChildrenMap()["Serilog.Sinks.File"]);
        Assert.Equal(["Core"], extractor.DependentProjects().Select(p => p.Id));
    }

    [Fact]
    public void AssetsExtractor_MissingFile_Throws()
    {
        var builder = new AssetsExtractorBuilder(_logger);

        Assert.Throws<NugetDepsTreeException>(() =>
            builder.GetExtractor(_dir.Combine("missing.json"), _dir.Combine("App.csproj"), _dir.Combine("App.sln")));
    }

    [Fact]
    public void PackagesExtractor_PackagesInLocalFolder_DerivesRootsFromChildren()
    {
        var (packagesConfig, csproj, sln) = WriteLegacyProject(("A", "1.0.0"), ("B", "1.0.0"), ("C", "1.0.0"));
        WriteNupkg("A", "1.0.0", "B");
        WriteNupkg("B", "1.0.0");
        WriteNupkg("C", "1.0.0");

        var extractor = new PackagesExtractorBuilder(_logger).GetExtractor(packagesConfig, csproj, sln);

        Assert.Equal(["A", "B", "C"], extractor.AllDependencies().Keys.Order());
        Assert.Equal(["A", "C"], extractor.DirectDependencies().Order());
        Assert.Equal(["B"], extractor.ChildrenMap()["A"]);
    }

    [Fact]
    public void PackagesExtractor_CircularPackages_AreBothRoots()
    {
        var (packagesConfig, csproj, sln) = WriteLegacyProject(("A", "1.0.0"), ("B", "1.0.0"));
        WriteNupkg("A", "1.0.0", "B");
        WriteNupkg("B", "1.0.0", "A");

        var extractor = new PackagesExtractorBuilder(_logger).GetExtractor(packagesConfig, csproj, sln);

        Assert.Equal(["A", "B"], extractor.DirectDependencies().Order());
    }

    [Fact]
    public void PackagesExtractor_ThreePartVersion_ResolvesNupkgWithFourPartVersion()
    {
        var (packagesConfig, csproj, sln) = WriteLegacyProject(("A", "2.0.0"));
        WriteNupkg("A", "2.0.0.0");

        var extractor = new PackagesExtractorBuilder(_logger).GetExtractor(packagesConfig, csproj, sln);

        var dependency = Assert.Single(extractor.AllDependencies().Values);
        Assert.Equal(("A", "2.0.0"), (dependency.Id, dependency.Version));
    }

    [Theory]
    [InlineData("2", "2.0.0")]
    [InlineData("2.0", "2.0.0")]
    [InlineData("2.0", "2.0.0.0")]
    public void PackagesExtractor_ShortVersion_ResolvesNupkgWithPaddedVersion(string version, string nupkgVersion)
    {
        var (packagesConfig, csproj, sln) = WriteLegacyProject(("A", version));
        WriteNupkg("A", nupkgVersion);

        var extractor = new PackagesExtractorBuilder(_logger).GetExtractor(packagesConfig, csproj, sln);

        var dependency = Assert.Single(extractor.AllDependencies().Values);
        Assert.Equal(("A", version), (dependency.Id, dependency.Version));
    }

    [Fact]
    public void PackagesExtractor_TrailingZeroVersion_ResolvesNupkgWithTrimmedVersion()
    {
        var (packagesConfig, csproj, sln) = WriteLegacyProject(("A", "3.1.0.0"));
        WriteNupkg("A", "3.1");

        var extractor = new PackagesExtractorBuilder(_logger).GetExtractor(packagesConfig, csproj, sln);

        Assert.Equal(["A"], extractor.AllDependencies().Keys);
    }

    [Fact]
    public void PackagesExtractor_PackageNotInCache_IsSkippedWithWarning()
    {
        var missing = $"Missing{Guid.NewGuid():N}";
        var (packagesConfig, csproj, sln) = WriteLegacyProject(("A", "1.0.0"), (missing, "1.0.0"));
        WriteNupkg("A", "1.0.0");

        var extractor = new PackagesExtractorBuilder(_logger).GetExtractor(packagesConfig, csproj, sln);

        Assert.Equal(["A"], extractor.AllDependencies().Keys);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning && r.Message.Contains(missing));
    }

    [Fact]
    public void PackagesExtractor_DependentProjects_ReadFromCsproj()
    {
        var (packagesConfig, csproj, sln) = WriteLegacyProject(["Core", "Data"]);

        var extractor = new PackagesExtractorBuilder(_logger).GetExtractor(packagesConfig, csproj, sln);

        Assert.Equal([("Core", ""), ("Data", "")], extractor.DependentProjects().Select(p => (p.Id, p.Version)));
    }

    [Fact]
    public void PackagesExtractor_DependentProjectsWithMissingCsproj_Throws()
    {
        var (packagesConfig, _, sln) = WriteLegacyProject();

        var extractor = new PackagesExtractorBuilder(_logger)
            .GetExtractor(packagesConfig, _dir.Combine("Other", "Other.csproj"), sln);

        Assert.Throws<NugetDepsTreeException>(() => extractor.DependentProjects());
    }

    [Fact]
    public void PackagesExtractor_MissingPackagesConfig_Throws()
    {
        var builder = new PackagesExtractorBuilder(_logger);

        Assert.Throws<NugetDepsTreeException>(() =>
            builder.GetExtractor(_dir.Combine("packages.config"), _dir.Combine("App.csproj"), _dir.Combine("App.sln")));
    }

    private (string PackagesConfig, string Csproj, string Sln) WriteLegacyProject(params (string Id, string Version)[] packages) =>
        WriteLegacyProject([], packages);

    private (string PackagesConfig, string Csproj, string Sln) WriteLegacyProject(
        string[] projectReferences, params (string Id, string Version)[] packages)
    {
        var sln = _dir.WriteFile("Legacy.sln", TestFiles.Solution("Legacy"));
        var csproj = _dir.WriteFile(Path.Combine("Legacy", "Legacy.csproj"), TestFiles.LegacyCsproj(projectReferences));
        var packagesConfig = _dir.WriteFile(Path.Combine("Legacy", "packages.config"), TestFiles.PackagesConfig(packages));
        return (packagesConfig, csproj, sln);
    }

    private void WriteNupkg(string id, string version, params string[] dependencies) =>
        TestFiles.WriteNupkg(TestFiles.PackagesConfigNupkgPath(_dir.Path, id, version), id, version, dependencies);
}

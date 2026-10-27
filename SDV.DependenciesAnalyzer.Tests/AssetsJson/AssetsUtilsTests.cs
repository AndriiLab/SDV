using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using SDV.DependenciesAnalyzer.AssetsJson;
using SDV.DependenciesAnalyzer.Exceptions;
using SDV.DependenciesAnalyzer.PackagesConfig;
using SDV.DependenciesAnalyzer.Tests.TestUtils;

namespace SDV.DependenciesAnalyzer.Tests.AssetsJson;

public sealed class AssetsUtilsTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly FakeLogger _logger = new();
    private readonly AssetsUtils _sut;
    private readonly string _csprojPath;

    public AssetsUtilsTests()
    {
        _sut = new AssetsUtils(_logger);
        _csprojPath = _dir.Combine("App", "App.csproj");
    }

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void GetDirectDependencies_MultipleFrameworks_ReturnsDependenciesOfAll()
    {
        var assets = new AssetsJsonBuilder(_csprojPath)
            .WithDirectDependency("Serilog", "1.0.0", "net8.0")
            .WithDirectDependency("Newtonsoft.Json", "13.0.1", "net10.0")
            .Build();

        var result = _sut.GetDirectDependencies(assets);

        Assert.Equal(["Serilog", "Newtonsoft.Json"], result);
    }

    [Fact]
    public void GetDirectDependencies_NoProjectSection_ReturnsEmpty()
    {
        Assert.Empty(_sut.GetDirectDependencies(new ProjectAssetsJsonModel()));
    }

    [Fact]
    public void GetDirectDependencies_FrameworkWithoutDependencies_ReturnsEmpty()
    {
        var assets = new ProjectAssetsJsonModel
        {
            Project = new AssetsProject { Frameworks = new() { ["net10.0"] = new ProjectFramework() } }
        };

        Assert.Empty(_sut.GetDirectDependencies(assets));
    }

    [Fact]
    public void GetChildrenMap_Targets_MapsPackageIdToDependencyIds()
    {
        var assets = new AssetsJsonBuilder(_csprojPath)
            .WithPackage("Serilog.Sinks.File", "5.0.0", "Serilog")
            .WithPackage("Serilog", "2.10.0")
            .Build();

        var result = _sut.GetChildrenMap(assets);

        Assert.Equal(["Serilog"], result["Serilog.Sinks.File"]);
        Assert.Empty(result["Serilog"]);
    }

    [Fact]
    public void GetChildrenMap_IsCaseInsensitive()
    {
        var assets = new AssetsJsonBuilder(_csprojPath).WithPackage("Serilog", "2.10.0").Build();

        var result = _sut.GetChildrenMap(assets);

        Assert.True(result.ContainsKey("SERILOG"));
    }

    [Fact]
    public void GetChildrenMap_NoTargets_ReturnsEmpty()
    {
        Assert.Empty(_sut.GetChildrenMap(new ProjectAssetsJsonModel()));
    }

    [Fact]
    public void GetChildrenMap_KeyWithoutVersion_Throws()
    {
        var assets = new ProjectAssetsJsonModel
        {
            Targets = new() { ["net10.0"] = new() { ["Serilog"] = new ProjectAssetsTarget() } }
        };

        Assert.Throws<NugetDepsTreeException>(() => _sut.GetChildrenMap(assets));
    }

    [Fact]
    public void GetAllDependencies_PackagesInCache_ReturnsThemWithVersions()
    {
        var builder = new AssetsJsonBuilder(_csprojPath)
            .WithPackage("Serilog", "2.10.0")
            .WithPackage("Newtonsoft.Json", "13.0.1");
        builder.WriteNupkgs();

        var result = _sut.GetAllDependencies(builder.Build());

        Assert.Equal(2, result.Count);
        Assert.Equal("2.10.0", result["serilog"].Version);
        Assert.Equal("Newtonsoft.Json", result["Newtonsoft.Json"].Id);
    }

    [Fact]
    public void GetAllDependencies_ProjectLibraries_AreSkipped()
    {
        var builder = new AssetsJsonBuilder(_csprojPath)
            .WithPackage("Serilog", "2.10.0")
            .WithProjectReference("Core");
        builder.WriteNupkgs();

        var result = _sut.GetAllDependencies(builder.Build());

        Assert.Equal(["Serilog"], result.Keys);
    }

    [Fact]
    public void GetAllDependencies_ProjectPathMissing_Throws()
    {
        var assets = new AssetsJsonBuilder(_csprojPath).Build();
        assets.Project!.Restore!.ProjectPath = null;

        Assert.Throws<NugetDepsTreeException>(() => _sut.GetAllDependencies(assets));
    }

    [Fact]
    public void GetAllDependencies_NoLibraries_ReturnsEmpty()
    {
        var assets = new AssetsJsonBuilder(_csprojPath).Build();
        assets.Libraries = null;

        Assert.Empty(_sut.GetAllDependencies(assets));
    }

    [Fact]
    public void GetAllDependencies_LibraryPathMissing_Throws()
    {
        var assets = new AssetsJsonBuilder(_csprojPath).WithPackage("Serilog", "2.10.0").Build();
        assets.Libraries!["Serilog/2.10.0"].Path = null;

        var ex = Assert.Throws<NugetDepsTreeException>(() => _sut.GetAllDependencies(assets));
        Assert.Contains("Serilog/2.10.0", ex.Message);
    }

    [Fact]
    public void GetAllDependencies_NoSha512File_Throws()
    {
        var assets = new AssetsJsonBuilder(_csprojPath).WithPackage("Serilog", "2.10.0").Build();
        assets.Libraries!["Serilog/2.10.0"].Files = ["lib/net8.0/Serilog.dll"];

        Assert.Throws<NugetDepsTreeException>(() => _sut.GetAllDependencies(assets));
    }

    [Fact]
    public void GetAllDependencies_NupkgMissingButListedInTargets_IsSkippedWithWarning()
    {
        // library path "serilog/2.10.0" matches target key "Serilog/2.10.0" case-insensitively
        var assets = new AssetsJsonBuilder(_csprojPath).WithPackage($"Serilog{Guid.NewGuid():N}", "2.10.0").Build();

        var result = _sut.GetAllDependencies(assets);

        Assert.Empty(result);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public void GetAllDependencies_NupkgMissingAndNotInTargets_Throws()
    {
        var assets = new AssetsJsonBuilder(_csprojPath).WithPackage($"Serilog{Guid.NewGuid():N}", "2.10.0").Build();
        assets.Targets = null;

        Assert.Throws<NugetDepsTreeException>(() => _sut.GetAllDependencies(assets));
    }

    [Fact]
    public void GetDependentProjects_MixedLibraries_ReturnsOnlyProjects()
    {
        var assets = new AssetsJsonBuilder(_csprojPath)
            .WithPackage("Serilog", "2.10.0")
            .WithProjectReference("Core")
            .WithProjectReference("Data")
            .Build();

        var result = _sut.GetDependentProjects(assets);

        Assert.Equal([("Core", "1.0.0"), ("Data", "1.0.0")], result.Select(d => (d.Id, d.Version)));
    }

    [Fact]
    public void GetDependentProjects_NoLibraries_ReturnsEmpty()
    {
        Assert.Empty(_sut.GetDependentProjects(new ProjectAssetsJsonModel()));
    }
}

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using SDV.DependenciesAnalyzer.Exceptions;
using SDV.DependenciesAnalyzer.Interfaces;
using SDV.DependenciesAnalyzer.Models;
using SDV.DependenciesAnalyzer.Tests.TestUtils;

namespace SDV.DependenciesAnalyzer.Tests;

public sealed class NugetDependenciesGeneratorTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly FakeLogger<NugetDependenciesGenerator> _logger = new();
    private readonly NugetDependenciesGenerator _sut;

    public NugetDependenciesGeneratorTests()
    {
        _sut = new NugetDependenciesGenerator(_logger);
    }

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Generate_MissingFile_Throws()
    {
        var ex = Assert.Throws<NugetDepsTreeException>(() => _sut.Generate(_dir.Combine("missing.sln")));

        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public void Generate_NotASolutionFile_Throws()
    {
        var path = _dir.WriteFile("App.csproj", TestFiles.SdkCsproj());

        var ex = Assert.Throws<NugetDepsTreeException>(() => _sut.Generate(path));

        Assert.Contains("not a solution file", ex.Message);
    }

    [Fact]
    public void Generate_PackageReferenceSolution_BuildsTreePerProject()
    {
        var sln = _dir.WriteFile("Shop.sln", TestFiles.Solution("Web", "Core"));
        WriteAssetsProject("Web", b => b
            .WithDirectDependency("Serilog.Sinks.File", "5.0.0")
            .WithPackage("Serilog.Sinks.File", "5.0.0", "Serilog")
            .WithPackage("Serilog", "2.10.0")
            .WithProjectReference("Core"));
        WriteAssetsProject("Core", b => b
            .WithDirectDependency("Newtonsoft.Json", "13.0.1")
            .WithPackage("Newtonsoft.Json", "13.0.1"));

        var tree = _sut.Generate(sln);

        Assert.Equal("Shop", tree.SolutionName);
        Assert.Collection(tree.Projects,
            web =>
            {
                Assert.Equal("Web", web.Name);
                var sink = Assert.Single(web.Dependencies);
                Assert.Equal(("Serilog.Sinks.File", "5.0.0", DependencyType.Package), (sink.Id, sink.Version, sink.Type));
                var serilog = Assert.Single(sink.Dependencies);
                Assert.Equal(("Serilog", "2.10.0"), (serilog.Id, serilog.Version));
            },
            core =>
            {
                Assert.Equal("Core", core.Name);
                Assert.Equal(["Newtonsoft.Json"], core.Dependencies.Select(d => d.Id));
            });
    }

    [Fact]
    public void Generate_IncludeDependentProjects_AddsProjectReferences()
    {
        var sln = _dir.WriteFile("Shop.sln", TestFiles.Solution("Web"));
        WriteAssetsProject("Web", b => b.WithProjectReference("Core"));

        var tree = _sut.Generate(Configuration(sln, includeDependentProjects: true));

        var dependency = Assert.Single(Assert.Single(tree.Projects).Dependencies);
        Assert.Equal(("Core", DependencyType.Project), (dependency.Id, dependency.Type));
    }

    [Fact]
    public void Generate_PackagesConfigSolution_BuildsTree()
    {
        var sln = _dir.WriteFile("Legacy.sln", TestFiles.Solution("Legacy"));
        _dir.WriteFile(Path.Combine("Legacy", "Legacy.csproj"), TestFiles.LegacyCsproj("Core"));
        _dir.WriteFile(Path.Combine("Legacy", "packages.config"),
            TestFiles.PackagesConfig(("EntityFramework", "6.4.4"), ("Antlr", "3.5.0.2")));
        TestFiles.WriteNupkg(TestFiles.PackagesConfigNupkgPath(_dir.Path, "EntityFramework", "6.4.4"),
            "EntityFramework", "6.4.4", "Antlr");
        TestFiles.WriteNupkg(TestFiles.PackagesConfigNupkgPath(_dir.Path, "Antlr", "3.5.0.2"), "Antlr", "3.5.0.2");

        var tree = _sut.Generate(Configuration(sln, includeDependentProjects: true));

        var project = Assert.Single(tree.Projects);
        Assert.Collection(project.Dependencies,
            core => Assert.Equal(("Core", DependencyType.Project), (core.Id, core.Type)),
            ef =>
            {
                Assert.Equal(("EntityFramework", "6.4.4"), (ef.Id, ef.Version));
                Assert.Equal(["Antlr"], ef.Dependencies.Select(d => d.Id));
            });
    }

    [Fact]
    public void Generate_ProjectWithoutDependencySource_IsSkipped()
    {
        var sln = _dir.WriteFile("Shop.sln", TestFiles.Solution("Web", "Unrestored"));
        WriteAssetsProject("Web", b => b);
        _dir.WriteFile(Path.Combine("Unrestored", "Unrestored.csproj"), TestFiles.SdkCsproj());

        var tree = _sut.Generate(sln);

        Assert.Equal(["Web"], tree.Projects.Select(p => p.Name));
        Assert.Contains(_logger.Collector.GetSnapshot(),
            r => r.Level == LogLevel.Warning && r.Message.Contains("Unrestored"));
    }

    [Fact]
    public void Generate_ProjectExcludedByFilter_IsSkipped()
    {
        var sln = _dir.WriteFile("Shop.sln", TestFiles.Solution("Web", "Web.Tests"));
        WriteAssetsProject("Web", b => b);
        WriteAssetsProject("Web.Tests", b => b);

        var tree = _sut.Generate(new TreeGeneratorConfiguration(sln, [], ["*.Tests"]));

        Assert.Equal(["Web"], tree.Projects.Select(p => p.Name));
    }

    [Fact]
    public void Generate_SolutionFoldersAndMalformedLines_AreSkipped()
    {
        var sln = new StringBuilder()
            .AppendLine(TestFiles.SolutionFolderLine("src")).AppendLine("EndProject")
            .AppendLine("Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Broken\"").AppendLine("EndProject")
            .AppendLine(TestFiles.ProjectLine("Web", @"Web\Web.csproj")).AppendLine("EndProject")
            .ToString();
        var slnPath = _dir.WriteFile("Shop.sln", sln);
        WriteAssetsProject("Web", b => b);

        var tree = _sut.Generate(slnPath);

        Assert.Equal(["Web"], tree.Projects.Select(p => p.Name));
        var log = _logger.Collector.GetSnapshot();
        Assert.Contains(log, r => r.Level == LogLevel.Warning && r.Message.Contains("'src'"));
        Assert.Contains(log, r => r.Level == LogLevel.Error && r.Message.Contains("Broken"));
    }

    [Fact]
    public void Generate_ProjectInSubfolder_ResolvesRelativePath()
    {
        var sln = _dir.WriteFile("Shop.sln",
            TestFiles.ProjectLine("Web", @"src\Web\Web.csproj") + Environment.NewLine + "EndProject");
        var csproj = _dir.WriteFile(Path.Combine("src", "Web", "Web.csproj"), TestFiles.SdkCsproj());
        new AssetsJsonBuilder(csproj)
            .WithDirectDependency("Serilog", "2.10.0")
            .WithPackage("Serilog", "2.10.0")
            .WriteTo();

        var tree = _sut.Generate(sln);

        Assert.Equal(["Serilog"], Assert.Single(tree.Projects).Dependencies.Select(d => d.Id));
    }

    [Fact]
    public void Generate_SolutionWithoutProjects_FallsBackToSingleCsprojInDirectory()
    {
        var sln = _dir.WriteFile("Solo.sln", "Microsoft Visual Studio Solution File, Format Version 12.00");
        var csproj = _dir.WriteFile("Solo.csproj", TestFiles.SdkCsproj());
        new AssetsJsonBuilder(csproj)
            .WithDirectDependency("Serilog", "2.10.0")
            .WithPackage("Serilog", "2.10.0")
            .WriteTo();

        var tree = _sut.Generate(sln);

        var project = Assert.Single(tree.Projects);
        Assert.Equal("Solo", project.Name);
        Assert.Equal(["Serilog"], project.Dependencies.Select(d => d.Id));
    }

    [Fact]
    public void Generate_SolutionWithoutProjectsAndSeveralCsprojs_ReturnsNoProjects()
    {
        var sln = _dir.WriteFile("Solo.sln", "Microsoft Visual Studio Solution File, Format Version 12.00");
        _dir.WriteFile("One.csproj", TestFiles.SdkCsproj());
        _dir.WriteFile("Two.csproj", TestFiles.SdkCsproj());

        var tree = _sut.Generate(sln);

        Assert.Empty(tree.Projects);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning && r.Message.Contains("2"));
    }

    private void WriteAssetsProject(string name, Func<AssetsJsonBuilder, AssetsJsonBuilder> configure)
    {
        var csproj = _dir.WriteFile(Path.Combine(name, $"{name}.csproj"), TestFiles.SdkCsproj());
        configure(new AssetsJsonBuilder(csproj)).WriteTo();
    }

    private static TreeGeneratorConfiguration Configuration(string sln, bool includeDependentProjects) =>
        new(sln, [], []) { IncludeDependentProjects = includeDependentProjects };
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using SDV.DependenciesAnalyzer.Common;
using SDV.DependenciesAnalyzer.Dependencies;
using SDV.DependenciesAnalyzer.Interfaces;
using SDV.DependenciesAnalyzer.Models;
using SDV.DependenciesAnalyzer.Structure;

namespace SDV.DependenciesAnalyzer.Tests.Dependencies;

public class DependenciesUtilsTests
{
    private readonly FakeLogger _logger = new();
    private readonly DependenciesUtils _sut;

    public DependenciesUtilsTests()
    {
        _sut = new DependenciesUtils(_logger);
    }

    [Fact]
    public void CreateDependencyTree_NestedPackages_BuildsRecursiveTree()
    {
        var extractor = CreateExtractor(
            direct: ["A"],
            packages: [("A", "1.0.0", ["B"]), ("B", "2.0.0", ["C"]), ("C", "3.0.0", [])]);

        var tree = _sut.CreateDependencyTree(extractor, Configuration());

        var a = Assert.Single(tree);
        AssertPackage(a, "A", "1.0.0");
        var b = Assert.Single(a.Dependencies);
        AssertPackage(b, "B", "2.0.0");
        var c = Assert.Single(b.Dependencies);
        AssertPackage(c, "C", "3.0.0");
        Assert.Empty(c.Dependencies);
    }

    [Fact]
    public void CreateDependencyTree_PackageWithoutChildrenEntry_HasNoDependencies()
    {
        var extractor = CreateExtractor(direct: ["A"], packages: [("A", "1.0.0", null)]);

        var tree = _sut.CreateDependencyTree(extractor, Configuration());

        Assert.Empty(Assert.Single(tree).Dependencies);
    }

    [Fact]
    public void CreateDependencyTree_ExcludedRoot_IsSkipped()
    {
        var extractor = CreateExtractor(
            direct: ["Microsoft.Extensions.Logging", "Serilog"],
            packages: [("Microsoft.Extensions.Logging", "1.0.0", []), ("Serilog", "1.0.0", [])]);

        var tree = _sut.CreateDependencyTree(extractor, Configuration(exclude: ["Microsoft.*"]));

        Assert.Equal(["Serilog"], tree.Select(t => t.Id));
    }

    [Fact]
    public void CreateDependencyTree_ExcludedChild_IsSkippedWithItsSubtree()
    {
        var extractor = CreateExtractor(
            direct: ["Serilog"],
            packages:
            [
                ("Serilog", "1.0.0", ["System.Memory", "Serilog.Core"]),
                ("System.Memory", "1.0.0", ["Serilog.Hidden"]),
                ("Serilog.Core", "1.0.0", []),
                ("Serilog.Hidden", "1.0.0", [])
            ]);

        var tree = _sut.CreateDependencyTree(extractor, Configuration(exclude: ["System.*"]));

        var serilog = Assert.Single(tree);
        Assert.Equal(["Serilog.Core"], serilog.Dependencies.Select(d => d.Id));
    }

    [Fact]
    public void CreateDependencyTree_IncludeFilter_AppliesToChildrenToo()
    {
        var extractor = CreateExtractor(
            direct: ["Serilog", "Newtonsoft.Json"],
            packages:
            [
                ("Serilog", "1.0.0", ["Serilog.Core", "System.Memory"]),
                ("Serilog.Core", "1.0.0", []),
                ("System.Memory", "1.0.0", []),
                ("Newtonsoft.Json", "1.0.0", [])
            ]);

        var tree = _sut.CreateDependencyTree(extractor, Configuration(include: ["Serilog*"]));

        var serilog = Assert.Single(tree);
        Assert.Equal(["Serilog.Core"], serilog.Dependencies.Select(d => d.Id));
    }

    [Fact]
    public void CreateDependencyTree_CircularDependency_IsCutAndWarned()
    {
        var extractor = CreateExtractor(
            direct: ["A"],
            packages: [("A", "1.0.0", ["B"]), ("B", "1.0.0", ["A"])]);

        var tree = _sut.CreateDependencyTree(extractor, Configuration());

        var a = Assert.Single(tree);
        var b = Assert.Single(a.Dependencies);
        Assert.Equal("B", b.Id);
        Assert.Empty(b.Dependencies);
        AssertWarningLogged("Circular dependency detected", "A");
    }

    [Fact]
    public void CreateDependencyTree_CircularDependencyWithDifferentCase_IsCut()
    {
        var extractor = CreateExtractor(
            direct: ["A"],
            packages: [("A", "1.0.0", ["B"]), ("B", "1.0.0", ["a"])]);

        var tree = _sut.CreateDependencyTree(extractor, Configuration());

        Assert.Empty(Assert.Single(Assert.Single(tree).Dependencies).Dependencies);
    }

    [Fact]
    public void CreateDependencyTree_DiamondDependency_SharedChildAppearsUnderEachParent()
    {
        var extractor = CreateExtractor(
            direct: ["A"],
            packages: [("A", "1.0.0", ["B", "C"]), ("B", "1.0.0", ["C"]), ("C", "1.0.0", [])]);

        var tree = _sut.CreateDependencyTree(extractor, Configuration());

        var a = Assert.Single(tree);
        Assert.Equal(["B", "C"], a.Dependencies.Select(d => d.Id));
        Assert.Equal(["C"], a.Dependencies[0].Dependencies.Select(d => d.Id));
        Assert.DoesNotContain(_logger.Collector.GetSnapshot(), r => r.Message.Contains("Circular"));
    }

    [Fact]
    public void CreateDependencyTree_UnknownRoot_IsSkippedAndWarned()
    {
        var extractor = CreateExtractor(direct: ["Ghost", "A"], packages: [("A", "1.0.0", [])]);

        var tree = _sut.CreateDependencyTree(extractor, Configuration());

        Assert.Equal(["A"], tree.Select(t => t.Id));
        AssertWarningLogged("root dependencies", "Ghost");
    }

    [Fact]
    public void CreateDependencyTree_UnknownChild_IsSkippedAndWarned()
    {
        var extractor = CreateExtractor(direct: ["A"], packages: [("A", "1.0.0", ["Ghost"])]);

        var tree = _sut.CreateDependencyTree(extractor, Configuration());

        Assert.Empty(Assert.Single(tree).Dependencies);
        AssertWarningLogged("children array", "Ghost");
    }

    [Fact]
    public void CreateDependencyTree_IncludeDependentProjects_PrependsProjectNodes()
    {
        var extractor = CreateExtractor(direct: ["A"], packages: [("A", "1.0.0", [])],
            dependentProjects: [new Dependency("Core", "1.0.0")]);

        var tree = _sut.CreateDependencyTree(extractor, Configuration(includeDependentProjects: true));

        Assert.Collection(tree,
            p =>
            {
                Assert.Equal(("Core", "1.0.0", DependencyType.Project), (p.Id, p.Version, p.Type));
                Assert.Empty(p.Dependencies);
            },
            p => AssertPackage(p, "A", "1.0.0"));
    }

    [Fact]
    public void CreateDependencyTree_DependentProjectsDisabled_DoesNotQueryProjects()
    {
        var extractor = CreateExtractor(direct: ["A"], packages: [("A", "1.0.0", [])],
            dependentProjects: [new Dependency("Core", "1.0.0")]);

        var tree = _sut.CreateDependencyTree(extractor, Configuration(includeDependentProjects: false));

        Assert.All(tree, t => Assert.Equal(DependencyType.Package, t.Type));
        extractor.DidNotReceive().DependentProjects();
    }

    [Fact]
    public void CreateDependencyTree_NoDirectDependencies_ReturnsEmpty()
    {
        var extractor = CreateExtractor(direct: [], packages: [("A", "1.0.0", [])]);

        Assert.Empty(_sut.CreateDependencyTree(extractor, Configuration()));
    }

    private static TreeGeneratorConfiguration Configuration(
        string[]? include = null, string[]? exclude = null, bool includeDependentProjects = false) =>
        new(string.Empty, include ?? [], exclude ?? []) { IncludeDependentProjects = includeDependentProjects };

    private static IExtractor CreateExtractor(
        string[] direct,
        (string Id, string Version, string[]? Children)[] packages,
        Dependency[]? dependentProjects = null)
    {
        var all = new CaseInsensitiveMap<IDependencyDetails>();
        var children = new CaseInsensitiveMap<List<string>>();
        foreach (var (id, version, packageChildren) in packages)
        {
            all[id] = new Dependency(id, version);
            if (packageChildren is not null)
                children[id] = [..packageChildren];
        }

        var extractor = Substitute.For<IExtractor>();
        extractor.DirectDependencies().Returns([..direct]);
        extractor.AllDependencies().Returns(all);
        extractor.ChildrenMap().Returns(children);
        extractor.DependentProjects().Returns([..dependentProjects ?? []]);
        return extractor;
    }

    private static void AssertPackage(DependencyTree tree, string id, string version)
    {
        Assert.Equal((id, version, DependencyType.Package), (tree.Id, tree.Version, tree.Type));
    }

    private void AssertWarningLogged(string messagePart, string id)
    {
        Assert.Contains(_logger.Collector.GetSnapshot(), r =>
            r.Level == LogLevel.Warning && r.Message.Contains(messagePart) && r.Message.Contains(id));
    }
}

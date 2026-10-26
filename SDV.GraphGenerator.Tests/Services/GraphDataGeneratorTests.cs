using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using SDV.DependenciesAnalyzer.Models;
using SDV.GraphGenerator.Services;
using SDV.GraphGenerator.Services.Models;

namespace SDV.GraphGenerator.Tests.Services;

public class GraphDataGeneratorTests
{
    private readonly FakeLogger<GraphDataGenerator> _logger = new();
    private readonly GraphDataGenerator _sut;
    private readonly Dictionary<string, GraphProject> _graph = new();

    public GraphDataGeneratorTests()
    {
        _sut = new GraphDataGenerator(_logger);
    }

    [Fact]
    public void GenerateGraphDataFromTree_ProjectWithPackage_CreatesNodesAndEdge()
    {
        var tree = Tree("Shop", Project("Web", Package("Serilog", "2.10.0")));

        _sut.GenerateGraphDataFromTree(tree, _graph, singleSolutionMode: false);

        Assert.Equal(["Web", "Serilog"], _graph.Keys);
        AssertNode("Web", "Project");
        AssertNode("Serilog", "Package");
        Assert.Empty(_graph["Web"].Edges);
        var edge = Assert.Single(_graph["Serilog"].Edges.Values);
        Assert.Equal(("Web", "Serilog", "2.10.0", false), (edge.From, edge.To, edge.Label, edge.HasMultipleVersions));
    }

    [Fact]
    public void GenerateGraphDataFromTree_TransitivePackages_CreatesEdgeFromParentPackage()
    {
        var tree = Tree("Shop", Project("Web", Package("Serilog.Sinks.File", "5.0.0", Package("Serilog", "2.10.0"))));

        _sut.GenerateGraphDataFromTree(tree, _graph, singleSolutionMode: false);

        var edge = Assert.Single(_graph["Serilog"].Edges.Values);
        Assert.Equal(("Serilog.Sinks.File", "Serilog"), (edge.From, edge.To));
    }

    [Fact]
    public void GenerateGraphDataFromTree_PackageUsedByTwoProjects_CreatesEdgePerParent()
    {
        var tree = Tree("Shop",
            Project("Web", Package("Serilog", "2.10.0")),
            Project("Core", Package("Serilog", "3.0.0")));

        _sut.GenerateGraphDataFromTree(tree, _graph, singleSolutionMode: false);

        var serilog = _graph["Serilog"];
        Assert.Equal(["Web", "Core"], serilog.Edges.Keys);
        Assert.Equal("2.10.0", serilog.Edges["Web"].Label);
        Assert.Equal("3.0.0", serilog.Edges["Core"].Label);
        Assert.False(serilog.Package.HasMultipleVersions);
    }

    [Fact]
    public void GenerateGraphDataFromTree_SameEdgeWithDifferentVersions_FlagsConflict()
    {
        var tree = Tree("Shop",
            Project("Web", Package("Serilog.Sinks.File", "5.0.0", Package("Serilog", "2.10.0"))),
            Project("Core", Package("Serilog.Sinks.File", "5.0.0", Package("Serilog", "3.0.0"))));

        _sut.GenerateGraphDataFromTree(tree, _graph, singleSolutionMode: false);

        var serilog = _graph["Serilog"];
        var edge = Assert.Single(serilog.Edges.Values);
        Assert.Equal("2.10.0 | 3.0.0", edge.Label);
        Assert.True(edge.HasMultipleVersions);
        Assert.True(serilog.Package.HasMultipleVersions);
        Assert.False(_graph["Serilog.Sinks.File"].Package.HasMultipleVersions);
        Assert.Contains(_logger.Collector.GetSnapshot(), r =>
            r.Level == LogLevel.Warning && r.Message.Contains("2.10.0 | 3.0.0"));
    }

    [Fact]
    public void GenerateGraphDataFromTree_ThreeDistinctVersions_AppendsEach()
    {
        var tree = Tree("Shop",
            Project("A", Package("Lib", "1.0.0", Package("Dep", "1.0.0"))),
            Project("B", Package("Lib", "1.0.0", Package("Dep", "2.0.0"))),
            Project("C", Package("Lib", "1.0.0", Package("Dep", "3.0.0"))));

        _sut.GenerateGraphDataFromTree(tree, _graph, singleSolutionMode: false);

        Assert.Equal("1.0.0 | 2.0.0 | 3.0.0", _graph["Dep"].Edges["Lib"].Label);
    }

    [Fact]
    public void GenerateGraphDataFromTree_SameEdgeWithSameVersion_DoesNotFlagConflict()
    {
        var tree = Tree("Shop",
            Project("Web", Package("Serilog.Sinks.File", "5.0.0", Package("Serilog", "2.10.0"))),
            Project("Core", Package("Serilog.Sinks.File", "5.0.0", Package("Serilog", "2.10.0"))));

        _sut.GenerateGraphDataFromTree(tree, _graph, singleSolutionMode: false);

        var edge = Assert.Single(_graph["Serilog"].Edges.Values);
        Assert.Equal("2.10.0", edge.Label);
        Assert.False(edge.HasMultipleVersions);
        Assert.False(_graph["Serilog"].Package.HasMultipleVersions);
    }

    [Fact(Skip = "Known bug: GraphDataGenerator checks edge.Label.Contains(version), so '1.0' is treated as already present in '11.0'")]
    public void GenerateGraphDataFromTree_VersionIsSubstringOfExistingVersion_FlagsConflict()
    {
        var tree = Tree("Shop",
            Project("Web", Package("Lib", "1.0.0", Package("Dep", "11.0"))),
            Project("Core", Package("Lib", "1.0.0", Package("Dep", "1.0"))));

        _sut.GenerateGraphDataFromTree(tree, _graph, singleSolutionMode: false);

        Assert.Equal("11.0 | 1.0", _graph["Dep"].Edges["Lib"].Label);
        Assert.True(_graph["Dep"].Package.HasMultipleVersions);
    }

    [Fact]
    public void GenerateGraphDataFromTree_ProjectDependency_CreatesProjectNodeAndEdge()
    {
        var tree = Tree("Shop",
            Project("Web", DependencyTree.AsProject("Core", "1.0.0")),
            Project("Core"));

        _sut.GenerateGraphDataFromTree(tree, _graph, singleSolutionMode: false);

        AssertNode("Core", "Project");
        Assert.Equal(["Web"], _graph["Core"].Edges.Keys);
    }

    [Fact]
    public void GenerateGraphDataFromTree_SharedGraphAcrossSolutions_MergesNodes()
    {
        _sut.GenerateGraphDataFromTree(Tree("Library", Project("Lib", Package("Serilog", "2.10.0"))), _graph, false);
        _sut.GenerateGraphDataFromTree(Tree("Shop", Project("Web", Package("Lib", "1.0.0"), Package("Serilog", "3.0.0"))), _graph, false);

        Assert.Equal(["Lib", "Serilog", "Web"], _graph.Keys);
        var lib = _graph["Lib"].Package;
        Assert.Equal(DependencyType.Project | DependencyType.Package, lib.TypeInternal);
        Assert.Equal("Project", lib.Type);
        Assert.Equal(["Lib", "Web"], _graph["Serilog"].Edges.Keys);
    }

    [Fact]
    public void GenerateGraphDataFromTree_SingleSolutionMode_CollapsesProjectsIntoSolutionNode()
    {
        var tree = Tree("Shop",
            Project("Web", DependencyTree.AsProject("Core", "1.0.0"), Package("Serilog", "2.10.0")),
            Project("Core", Package("Newtonsoft.Json", "13.0.1")));

        _sut.GenerateGraphDataFromTree(tree, _graph, singleSolutionMode: true);

        Assert.Equal(["Shop", "Serilog", "Newtonsoft.Json"], _graph.Keys);
        AssertNode("Shop", "Project");
        Assert.Equal(["Shop"], _graph["Serilog"].Edges.Keys);
        Assert.Equal(["Shop"], _graph["Newtonsoft.Json"].Edges.Keys);
    }

    [Fact]
    public void GenerateGraphDataFromTree_SingleSolutionModeWithConflictingProjects_FlagsConflictOnSolutionEdge()
    {
        var tree = Tree("Shop",
            Project("Web", Package("Serilog", "2.10.0")),
            Project("Core", Package("Serilog", "3.0.0")));

        _sut.GenerateGraphDataFromTree(tree, _graph, singleSolutionMode: true);

        var edge = Assert.Single(_graph["Serilog"].Edges.Values);
        Assert.Equal(("Shop", "2.10.0 | 3.0.0", true), (edge.From, edge.Label, edge.HasMultipleVersions));
    }

    [Theory]
    [InlineData("IsNuget")]
    [InlineData("isnuget")]
    [InlineData(" ISNUGET ")]
    public void SetLabels_IsNugetKey_LabelsPackageNodesOnly(string key)
    {
        _sut.SetLabels(new Dictionary<string, string[]> { [key] = ["📦"] });

        _sut.GenerateGraphDataFromTree(Tree("Shop", Project("Web", Package("Serilog", "2.10.0"))), _graph, false);

        Assert.Equal("Serilog📦", _graph["Serilog"].Package.Label);
        Assert.Equal("Web", _graph["Web"].Package.Label);
    }

    [Fact]
    public void SetLabels_IsProjectKey_LabelsProjectNodesOnly()
    {
        _sut.SetLabels(new Dictionary<string, string[]> { ["IsProject"] = ["🏠"] });

        _sut.GenerateGraphDataFromTree(Tree("Shop", Project("Web", Package("Serilog", "2.10.0"))), _graph, false);

        Assert.Equal("Web🏠", _graph["Web"].Package.Label);
        Assert.Equal("Serilog", _graph["Serilog"].Package.Label);
    }

    [Fact]
    public void SetLabels_NamePattern_LabelsMatchingNodes()
    {
        _sut.SetLabels(new Dictionary<string, string[]> { ["Serilog*"] = ["🔥"] });

        _sut.GenerateGraphDataFromTree(
            Tree("Shop", Project("Web", Package("Serilog.Sinks.File", "5.0.0"), Package("Newtonsoft.Json", "13.0.1"))),
            _graph, false);

        Assert.Equal("Serilog.Sinks.File🔥", _graph["Serilog.Sinks.File"].Package.Label);
        Assert.Equal("Newtonsoft.Json", _graph["Newtonsoft.Json"].Package.Label);
    }

    [Fact]
    public void SetLabels_SeveralMatchingRules_ConcatenatesLabelsAndSkipsBlankOnes()
    {
        _sut.SetLabels(new Dictionary<string, string[]>
        {
            ["IsNuget"] = ["📦", " "],
            ["Serilog"] = ["🔥", "⭐"],
            ["Newtonsoft.Json"] = ["", "  "],
            [""] = ["❌"]
        });

        _sut.GenerateGraphDataFromTree(
            Tree("Shop", Project("Web", Package("Serilog", "2.10.0"), Package("Newtonsoft.Json", "13.0.1"))),
            _graph, false);

        Assert.Equal("Serilog📦🔥⭐", _graph["Serilog"].Package.Label);
        Assert.Equal("Newtonsoft.Json📦", _graph["Newtonsoft.Json"].Package.Label);
        Assert.Equal("Web", _graph["Web"].Package.Label);
    }

    [Fact]
    public void SetLabels_LabelsDoNotChangeNodeIds()
    {
        _sut.SetLabels(new Dictionary<string, string[]> { ["IsNuget"] = ["📦"] });

        _sut.GenerateGraphDataFromTree(Tree("Shop", Project("Web", Package("Serilog", "2.10.0"))), _graph, false);

        Assert.Equal("Serilog", _graph["Serilog"].Package.Id);
        Assert.Equal("Serilog", _graph["Serilog"].Edges["Web"].To);
    }

    [Fact]
    public void SetLabels_InvalidPattern_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _sut.SetLabels(new Dictionary<string, string[]> { ["*"] = ["📦"] }));
    }

    [Fact(Skip = "Known bug: labels are re-applied to every node of the shared graph on each solution, duplicating them in multi-solution mode")]
    public void SetLabels_MultipleSolutions_AppliesLabelOncePerNode()
    {
        _sut.SetLabels(new Dictionary<string, string[]> { ["IsNuget"] = ["📦"] });

        _sut.GenerateGraphDataFromTree(Tree("Library", Project("Lib", Package("Serilog", "2.10.0"))), _graph, false);
        _sut.GenerateGraphDataFromTree(Tree("Shop", Project("Web", Package("Newtonsoft.Json", "13.0.1"))), _graph, false);

        Assert.Equal("Serilog📦", _graph["Serilog"].Package.Label);
        Assert.Equal("Newtonsoft.Json📦", _graph["Newtonsoft.Json"].Package.Label);
    }

    private void AssertNode(string id, string expectedType)
    {
        var package = _graph[id].Package;
        Assert.Equal((id, id, expectedType), (package.Id, package.Label, package.Type));
    }

    private static Tree Tree(string solutionName, params Project[] projects)
    {
        var tree = new Tree(solutionName);
        tree.Projects.AddRange(projects);
        return tree;
    }

    private static Project Project(string name, params DependencyTree[] dependencies) => new(name, dependencies);

    private static DependencyTree Package(string id, string version, params DependencyTree[] dependencies) =>
        DependencyTree.AsPackage(id, version, [..dependencies]);
}

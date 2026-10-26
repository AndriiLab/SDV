using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SDV.DependenciesAnalyzer.Interfaces;
using SDV.DependenciesAnalyzer.Models;
using SDV.GraphGenerator.Interfaces;
using SDV.GraphGenerator.Services;
using SDV.GraphGenerator.Services.Models;

namespace SDV.GraphGenerator.Tests.Services;

public sealed class GraphBuilderTests : IDisposable
{
    private readonly INugetDependenciesGenerator _dependenciesGenerator = Substitute.For<INugetDependenciesGenerator>();
    private readonly List<string> _generatedFiles = [];

    // unique per test so parallel tests never write the same output file
    private readonly string _slnA = $"SlnA{Guid.NewGuid():N}";
    private readonly string _slnB = $"SlnB{Guid.NewGuid():N}";

    public GraphBuilderTests()
    {
        _dependenciesGenerator.Generate(Arg.Any<TreeGeneratorConfiguration>())
            .Returns(ci => TreeFor(ci.Arg<TreeGeneratorConfiguration>().SlnFilePath));
    }

    public void Dispose()
    {
        foreach (var file in _generatedFiles)
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void BuildGraphAndGetFilePath_MultipleSolutions_AnalysesEachAndNamesFileAfterAll()
    {
        var analysed = new List<string>();
        _dependenciesGenerator.When(g => g.Generate(Arg.Any<TreeGeneratorConfiguration>()))
            .Do(ci => analysed.Add(ci.Arg<TreeGeneratorConfiguration>().SlnFilePath));

        var html = Build(new GraphBuilderRequest([SlnPath(_slnA), SlnPath(_slnB)]), out var resultPath);

        Assert.Equal([SlnPath(_slnA), SlnPath(_slnB)], analysed);
        Assert.Equal($"{_slnA}_{_slnB}.html", Path.GetFileName(resultPath));
        Assert.Equal($"{_slnA}, {_slnB}", LineAfter(html, "<title>"));
    }

    [Fact]
    public void BuildGraphAndGetFilePath_Template_SubstitutesNodesAndEdgesAsJson()
    {
        var lines = Build(new GraphBuilderRequest([SlnPath(_slnA)]), out _);

        var nodes = JsonSerializer.Deserialize<JsonElement[]>(LineAfter(lines, "var nodes ="))!;
        var edges = JsonSerializer.Deserialize<JsonElement[]>(LineAfter(lines, "var edges ="))!;

        Assert.Equal([$"{_slnA}.Web", "Serilog"], nodes.Select(n => n.GetProperty("id").GetString()));
        Assert.Equal("Project", nodes[0].GetProperty("type").GetString());
        Assert.Equal("Package", nodes[1].GetProperty("type").GetString());
        Assert.False(nodes[1].GetProperty("multipleVersions").GetBoolean());
        Assert.False(nodes[1].TryGetProperty("TypeInternal", out _));

        var edge = Assert.Single(edges);
        Assert.Equal($"{_slnA}.Web", edge.GetProperty("from").GetString());
        Assert.Equal("Serilog", edge.GetProperty("to").GetString());
        Assert.Equal("2.10.0", edge.GetProperty("label").GetString());
    }

    [Fact]
    public void BuildGraphAndGetFilePath_Template_InlinesVisNetworkScriptAndLeavesNoPlaceholders()
    {
        var lines = Build(new GraphBuilderRequest([SlnPath(_slnA)]), out _);
        var html = string.Join('\n', lines);

        var visNetwork = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "template", "vis-network.js"));
        Assert.Contains(visNetwork.Trim().Split('\n')[0], html);
        Assert.DoesNotContain("{{Title}}", html);
        Assert.DoesNotContain("{{VisNetworkScript}}", html);
        Assert.DoesNotContain("{{Nodes}}", html);
        Assert.DoesNotContain("{{Edges}}", html);
        Assert.StartsWith("<!DOCTYPE html>", html);
    }

    [Fact]
    public void BuildGraphAndGetFilePath_MergeProjects_DisablesDependentProjects()
    {
        var request = new GraphBuilderRequest([SlnPath(_slnA)]) { MergeProjects = true, IncludeDependentProjects = true };

        Build(request, out _);

        _dependenciesGenerator.Received(1).Generate(Arg.Is<TreeGeneratorConfiguration>(c => !c.IncludeDependentProjects));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    public void BuildGraphAndGetFilePath_IncludeDependentProjects_PassedThroughWhenNotMerging(
        bool merge, bool includeDependent, bool expected)
    {
        var request = new GraphBuilderRequest([SlnPath(_slnA)]) { MergeProjects = merge, IncludeDependentProjects = includeDependent };

        Build(request, out _);

        _dependenciesGenerator.Received(1)
            .Generate(Arg.Is<TreeGeneratorConfiguration>(c => c.IncludeDependentProjects == expected));
    }

    [Fact]
    public void BuildGraphAndGetFilePath_Filters_AppliedToConfiguration()
    {
        var request = new GraphBuilderRequest([SlnPath(_slnA)])
        {
            FiltersInclude = ["Serilog*"],
            FiltersExclude = ["*.Console"]
        };

        Build(request, out _);

        _dependenciesGenerator.Received(1).Generate(Arg.Is<TreeGeneratorConfiguration>(c =>
            c.IsPackageEnabled("Serilog.Sinks.File")
            && !c.IsPackageEnabled("Serilog.Sinks.Console")
            && !c.IsPackageEnabled("Newtonsoft.Json")));
    }

    [Fact]
    public void BuildGraphAndGetFilePath_Request_ForwardsLabelsAndMergeFlagToDataGenerator()
    {
        var dataGenerator = Substitute.For<IGraphDataGenerator>();
        var labels = new Dictionary<string, string[]> { ["IsNuget"] = ["📦"] };
        var request = new GraphBuilderRequest([SlnPath(_slnA)]) { Labels = labels, MergeProjects = true };

        Build(request, out _, dataGenerator);

        dataGenerator.Received(1).SetLabels(labels);
        dataGenerator.Received(1).GenerateGraphDataFromTree(
            Arg.Is<Tree>(t => t.SolutionName == _slnA),
            Arg.Any<IDictionary<string, GraphProject>>(),
            true);
    }

    [Fact]
    public void BuildGraphAndGetFilePath_MultipleSolutions_SharesGraphDictionary()
    {
        var dataGenerator = Substitute.For<IGraphDataGenerator>();
        var dictionaries = new List<IDictionary<string, GraphProject>>();
        dataGenerator.When(g => g.GenerateGraphDataFromTree(Arg.Any<Tree>(), Arg.Any<IDictionary<string, GraphProject>>(), Arg.Any<bool>()))
            .Do(ci => dictionaries.Add(ci.ArgAt<IDictionary<string, GraphProject>>(1)));

        Build(new GraphBuilderRequest([SlnPath(_slnA), SlnPath(_slnB)]), out _, dataGenerator);

        Assert.Equal(2, dictionaries.Count);
        Assert.Same(dictionaries[0], dictionaries[1]);
    }

    private string[] Build(GraphBuilderRequest request, out string resultPath, IGraphDataGenerator? dataGenerator = null)
    {
        var sut = new GraphBuilder(
            _dependenciesGenerator,
            dataGenerator ?? new GraphDataGenerator(NullLogger<GraphDataGenerator>.Instance),
            NullLogger<GraphBuilder>.Instance);

        resultPath = sut.BuildGraphAndGetFilePath(request);

        // GraphBuilder writes to the current directory but reports a path under the base directory
        var writtenPath = Path.GetFullPath(Path.GetFileName(resultPath));
        _generatedFiles.Add(writtenPath);
        return File.ReadAllLines(writtenPath);
    }

    private static string LineAfter(string[] lines, string marker)
    {
        var index = Array.FindIndex(lines, l => l.Trim() == marker);
        Assert.True(index >= 0, $"Marker '{marker}' not found in generated html");
        return lines[index + 1];
    }

    private static string SlnPath(string name) => Path.Combine("C:", "src", $"{name}.sln");

    private static Tree TreeFor(string slnPath)
    {
        var name = Path.GetFileNameWithoutExtension(slnPath);
        var tree = new Tree(name);
        tree.Projects.Add(new Project($"{name}.Web", [DependencyTree.AsPackage("Serilog", "2.10.0", [])]));
        return tree;
    }
}

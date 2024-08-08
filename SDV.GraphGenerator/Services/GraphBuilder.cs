using System.Text.Json;
using Microsoft.Extensions.Logging;
using SDV.DependenciesAnalyzer.Interfaces;
using SDV.GraphGenerator.Interfaces;
using SDV.GraphGenerator.Services.Models;

namespace SDV.GraphGenerator.Services;

public class GraphBuilder : IGraphBuilder
{
    private readonly INugetDependenciesGenerator _nugetDependenciesGenerator;
    private readonly IGraphDataGenerator _graphDataGenerator;
    private readonly ILogger<GraphBuilder> _logger;

    public GraphBuilder(INugetDependenciesGenerator nugetDependenciesGenerator,
        IGraphDataGenerator graphDataGenerator,
        ILogger<GraphBuilder> logger)
    {
        _nugetDependenciesGenerator = nugetDependenciesGenerator;
        _graphDataGenerator = graphDataGenerator;
        _logger = logger;
    }

    public string BuildGraphAndGetFilePath(GraphBuilderRequest request)
    {
        var packages = new Dictionary<string, GraphProject>();
        var names = new List<string>();
        
        var config = new TreeGeneratorConfiguration(string.Empty, request.FiltersInclude, request.FiltersExclude)
        {
            IncludeDependentProjects = request is { MergeProjects: false, IncludeDependentProjects: true },
        };

        _graphDataGenerator.SetLabels(request.Labels);
        
        foreach (var slnFilePath in request.SlnFilePaths)
        {
            config.SlnFilePath = slnFilePath;

            _logger.LogInformation("Processing solution {SlnPath}", slnFilePath);

            var tree = _nugetDependenciesGenerator.Generate(config);
            names.Add(tree.SolutionName);

            _logger.LogInformation("Solution analyzed {SlnName}", tree.SolutionName);


            _logger.LogInformation("Generating graph data {SlnName}", tree.SolutionName);

            _graphDataGenerator.GenerateGraphDataFromTree(tree, packages, request.MergeProjects);

            _logger.LogInformation("Graph data generation completed for {SlnName}", tree.SolutionName);
        }

        if (request.SlnFilePaths.Count() > 1)
            _logger.LogInformation("Graph data generation completed for all solutions");

        var nodes = packages.Values.Select(v => v.Package).ToList();
        var edges = packages.Values.SelectMany(v => v.Edges.Values).ToList();

        return WriteHtmlFile(names, nodes, edges);
    }

    private string WriteHtmlFile(IReadOnlyCollection<string> names, List<GraphPackage> nodes,
        List<GraphEdge> edges)
    {
        const string titlePlaceholder = "Title";
        const string nodesPlaceholder = "Nodes";
        const string edgesPlaceholder = "Edges";
        const string visNetworkScriptPlaceholder = "VisNetworkScript";
        var fileName = $"{string.Join('_', names)}.html";
        using var rs = new FileStream("template\\output.html", FileMode.Open);
        using var ws = new FileStream(fileName, FileMode.Create);
        using var reader = new StreamReader(rs);
        using var writer = new StreamWriter(ws);

        while (reader.ReadLine() is { } line)
        {
            var detectedPlaceholder = DetectPlaceholder(line.AsSpan());
            if (detectedPlaceholder is not null)
            {
                switch (detectedPlaceholder)
                {
                    case titlePlaceholder:
                        writer.WriteLine(string.Join(", ", names));
                        continue;
                    case visNetworkScriptPlaceholder:
                        WriteVisNetworkScript(writer);
                        continue;
                    case nodesPlaceholder:
                        writer.WriteLine(JsonSerializer.Serialize(nodes));
                        continue;
                    case edgesPlaceholder:
                        writer.WriteLine(JsonSerializer.Serialize(edges));
                        continue;
                    default:
                        _logger.LogWarning("Unknown placeholder detected in template html file: {Placeholder}",
                            detectedPlaceholder);
                        break;
                }
            }

            writer.WriteLine(line);
        }

        _logger.LogInformation("Result written to file {FileName}", fileName);

        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
    }

    private static void WriteVisNetworkScript(TextWriter writer)
    {
        using var rs = new FileStream(  "template\\vis-network.js", FileMode.Open);
        using var reader = new StreamReader(rs);
        while (reader.ReadLine() is { } line)
        {
            writer.WriteLine(line);
        }
    }

    private static string? DetectPlaceholder(ReadOnlySpan<char> line)
    {
        const char startPlaceholder = '{';
        const char endPlaceholder = '}';

        var startPosition = -1;
        var cursor = -1;
        var consecutiveStartPlaceholders = 0;
        var consecutiveEndPlaceholders = 0;
        foreach (var chr in line)
        {
            cursor++;

            if ((char.IsAsciiDigit(chr) || char.IsAsciiLetter(chr)) && consecutiveStartPlaceholders < 1)
            {
                return null;
            }

            switch (chr)
            {
                case startPlaceholder:
                    consecutiveStartPlaceholders++;
                    continue;
                case endPlaceholder:
                    consecutiveEndPlaceholders++;
                    if (consecutiveEndPlaceholders == 2)
                    {
                        return line.Slice(startPosition, cursor - startPosition - 1).ToString();
                    }

                    continue;
            }

            if (consecutiveStartPlaceholders == 2 && startPosition < 0)
            {
                startPosition = cursor;
            }
        }

        return null;
    }
}
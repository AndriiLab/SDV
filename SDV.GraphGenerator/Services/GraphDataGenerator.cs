using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SDV.DependenciesAnalyzer.Models;
using SDV.GraphGenerator.Utils;

namespace SDV.GraphGenerator.Services;

public interface IGraphDataGenerator
{
    void GenerateGraphDataFromTree(
        Tree tree,
        IDictionary<string, GraphDataGenerator.GraphProject> packages,
        bool singleSolutionMode);
}

public class GraphDataGenerator : IGraphDataGenerator
{
    private const string FontDark = "#ddd";
    private const string ColorDark = "#009911";
    private const string FontWhite = "#ddd";
    private const string ColorWhite = "#00cc22";
    private const string ProjectType = "project";
    private const string PackageType = "package";
    private const string ArrowTo = "to";
    private const string SmoothCubicBezier = "cubicBezier";

    private readonly ILogger<GraphDataGenerator> _log;

    public GraphDataGenerator(ILogger<GraphDataGenerator> log)
    {
        _log = log;
    }

    public void GenerateGraphDataFromTree(
        Tree tree,
        IDictionary<string, GraphProject> packages,
        bool singleSolutionMode)
    {
        var isDarkMode = SystemHelper.IsDarkModeEnabled();
        foreach (var project in tree.Projects)
        {
            var projectName = singleSolutionMode ? tree.SolutionName : project.Name;
            if (!packages.TryGetValue(projectName, out var graphProject))
            {
                graphProject = new GraphProject
                {
                    Package = new GraphPackage
                    {
                        Id = projectName,
                        Label = projectName,
                        Font = new GraphFont
                        {
                            Color = isDarkMode ? FontDark : FontWhite
                        },
                        Type = ProjectType,
                        Color = GetItemColorForType(ProjectType)
                    }
                };
                packages[projectName] = graphProject;
            }

            GenerateDependencies(projectName, project.Dependencies, packages, singleSolutionMode);
        }
    }

    private void GenerateDependencies(string parentName,
        IEnumerable<DependencyTree> projectDependencies,
        IDictionary<string, GraphProject> packages,
        bool singleSolutionMode)
    {
        var isDarkMode = SystemHelper.IsDarkModeEnabled();

        foreach (var dependency in projectDependencies)
        {
            if (singleSolutionMode && dependency.Type == ProjectType)
            {
                continue;
            }

            if (!packages.TryGetValue(dependency.Id, out var graphProject))
            {
                graphProject = new GraphProject
                {
                    Package = new GraphPackage
                    {
                        Id = dependency.Id,
                        Label = dependency.Id,
                        Font = new GraphFont
                        {
                            Color = isDarkMode ? FontDark : FontWhite
                        },
                        Type = dependency.Type,
                        Color = GetItemColorForType(dependency.Type)
                    }
                };
                packages[dependency.Id] = graphProject;
            }

            if (!graphProject.Edges.TryGetValue(parentName, out var edge))
            {
                edge = new GraphEdge
                {
                    From = parentName,
                    To = dependency.Id,
                    Arrows = ArrowTo,
                    Label = dependency.Version,
                    Smooth = new Smooth { Type = SmoothCubicBezier }
                };
                graphProject.Edges[parentName] = edge;
            }
            else if (!edge.Label.Contains(dependency.Version))
            {
                edge.Label += $" | {dependency.Version}";
                _log.LogWarning("Multiple versions detected for {From} -> {To}: {Versions}",
                    parentName, dependency.Id, edge.Label);
            }

            GenerateDependencies(dependency.Id, dependency.Dependencies, packages, singleSolutionMode);
        }
    }

    private static string GetItemColorForType(string projectType)
    {
        return projectType switch
        {
            ProjectType => SystemHelper.IsDarkModeEnabled() ? ColorDark : ColorWhite,
            PackageType => "#22aaee",
            _ => "#22aaee"
        };
    }

    public class GraphProject
    {
        public GraphPackage Package { get; init; }
        public Dictionary<string, GraphEdge> Edges { get; set; } = new();
    }

    public class GraphPackage
    {
        [JsonPropertyName("id")] public string Id { get; set; }

        [JsonPropertyName("label")] public string Label { get; set; }

        [JsonPropertyName("font")] public GraphFont Font { get; set; }

        [JsonPropertyName("type")] public string Type { get; set; }

        [JsonPropertyName("color")] public string Color { get; set; }
    }

    public class GraphEdge
    {
        [JsonPropertyName("from")] public string From { get; set; }

        [JsonPropertyName("to")] public string To { get; set; }

        [JsonPropertyName("arrows")] public string Arrows { get; set; }

        [JsonPropertyName("smooth")] public Smooth Smooth { get; set; }

        [JsonPropertyName("label")] public string Label { get; set; }
    }

    public class Smooth
    {
        [JsonPropertyName("type")] public string Type { get; set; }
    }

    public class GraphFont
    {
        [JsonPropertyName("color")] public string Color { get; set; }

    }
}


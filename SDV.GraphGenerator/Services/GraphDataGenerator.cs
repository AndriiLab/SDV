using Microsoft.Extensions.Logging;
using SDV.DependenciesAnalyzer.Models;
using SDV.GraphGenerator.Interfaces;
using SDV.GraphGenerator.Services.Models;

namespace SDV.GraphGenerator.Services;

public class GraphDataGenerator : IGraphDataGenerator
{
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
                        Type = DependencyType.Project.ToString()
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
        foreach (var dependency in projectDependencies)
        {
            if (singleSolutionMode && dependency.Type == DependencyType.Project)
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
                        Type = dependency.Type.ToString(),
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
                    Label = dependency.Version,
                };
                graphProject.Edges[parentName] = edge;
            }
            else if (!edge.Label.Contains(dependency.Version))
            {
                edge.Label += $" | {dependency.Version}";
                edge.HasMultipleVersions = true;
                graphProject.Package.HasMultipleVersions = true;
                _log.LogWarning("Multiple versions detected for {From} -> {To}: {Versions}",
                    parentName, dependency.Id, edge.Label);
            }

            GenerateDependencies(dependency.Id, dependency.Dependencies, packages, singleSolutionMode);
        }
    }
}


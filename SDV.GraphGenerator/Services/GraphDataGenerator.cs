using Microsoft.Extensions.Logging;
using SDV.DependenciesAnalyzer.Helpers;
using SDV.DependenciesAnalyzer.Models;
using SDV.GraphGenerator.Interfaces;
using SDV.GraphGenerator.Services.Models;

namespace SDV.GraphGenerator.Services;

public class GraphDataGenerator(ILogger<GraphDataGenerator> log) : IGraphDataGenerator
{
    private (Func<GraphProject, bool> Function, string[] Labels)[] _labels = [];

    public void GenerateGraphDataFromTree(Tree tree,
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
                        TypeInternal = DependencyType.Project
                    }
                };
                packages[projectName] = graphProject;
            }

            graphProject.Package.TypeInternal &= DependencyType.Project;

            GenerateDependencies(projectName, project.Dependencies, packages, singleSolutionMode);
        }
        
        foreach (var node in packages.Values)
        {
            var labelsToApply = _labels.Where(l => l.Function(node)).SelectMany(l => l.Labels).ToArray();
            if (labelsToApply.Length != 0)
                node.Package.Label += string.Join("", labelsToApply);
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
                        TypeInternal = dependency.Type
                    }
                };
                packages[dependency.Id] = graphProject;
            }
            
            graphProject.Package.TypeInternal &= dependency.Type;


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
                log.LogWarning("Multiple versions detected for {From} -> {To}: {Versions}",
                    parentName, dependency.Id, edge.Label);
            }

            GenerateDependencies(dependency.Id, dependency.Dependencies, packages, singleSolutionMode);
        }
    }

    public void SetLabels(IReadOnlyDictionary<string, string[]> labels)
    {
        _labels = labels.Select(kv => (BuildFilter(kv.Key), kv.Value))
            .Where(f => f.Item1 != null && f.Item2.Any(s => !string.IsNullOrWhiteSpace(s)))
            .Select(f => (f.Item1!, f.Item2.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray()))
            .ToArray();
    }

    private const string IsProjectKey = "IsProject";
    private const string IsNugetKey = "IsNuget";
    private static Func<GraphProject, bool>? BuildFilter(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return null;

        filter = filter.Trim();

        if (string.Equals(filter, IsProjectKey, StringComparison.InvariantCultureIgnoreCase))
            return x => x.Package.TypeInternal.HasFlag(DependencyType.Project);
        
        if (string.Equals(filter, IsNugetKey, StringComparison.InvariantCultureIgnoreCase))
            return x => x.Package.TypeInternal.HasFlag(DependencyType.Package);

        var func = FilterHelper.BuildFilter(filter);
        return func != null 
            ? new Func<GraphProject, bool>(x => func(x.Package.Label))
            : null;
    }
}


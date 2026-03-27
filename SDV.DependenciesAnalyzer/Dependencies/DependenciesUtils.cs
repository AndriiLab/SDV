using Microsoft.Extensions.Logging;
using SDV.DependenciesAnalyzer.Interfaces;
using SDV.DependenciesAnalyzer.Models;

namespace SDV.DependenciesAnalyzer.Dependencies;

public class DependenciesUtils(ILogger log)
{
    public DependencyTree[] CreateDependencyTree(IExtractor extractor, TreeGeneratorConfiguration configuration)
    {
        var rootTree = configuration.IncludeDependentProjects
            ? extractor.DependentProjects().Select(p => DependencyTree.AsProject(p.Id, p.Version)).ToList()
            : [];
        var rootDependencies = extractor.DirectDependencies();
        var allDependencies = extractor.AllDependencies();
        var childrenMap = extractor.ChildrenMap();

        foreach (var rootId in rootDependencies.Where(configuration.IsPackageEnabled))
        {
            if (allDependencies.TryGetValue(rootId, out var dependency))
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootId };
                var childrenDependencies = FindChildrenDependencies(rootId, allDependencies, childrenMap, configuration, visited).ToList();
                var dependencyTree = DependencyTree.AsPackage(dependency.Id, dependency.Version, childrenDependencies);
                rootTree.Add(dependencyTree);
            }
            else
            {
                log.LogWarning("Unexpected dependency found in root dependencies array: {RootId}", rootId);
            }
        }

        return rootTree.ToArray();
    }

    private IEnumerable<DependencyTree> FindChildrenDependencies(
        string id,
        IReadOnlyDictionary<string, IDependencyDetails> allDependencies,
        IReadOnlyDictionary<string, List<string>> childrenMap,
        TreeGeneratorConfiguration configuration,
        HashSet<string> visited)
    {
        if (!childrenMap.TryGetValue(id, out var childArray))
        {
            yield break;
        }

        foreach (var childId in childArray.Where(configuration.IsPackageEnabled))
        {
            if (visited.Contains(childId))
            {
                log.LogWarning("Circular dependency detected, skipping: {ChildId}", childId);
                continue;
            }

            if (allDependencies.TryGetValue(childId, out var dependency))
            {
                visited.Add(childId);
                var childrenDependencies = FindChildrenDependencies(childId, allDependencies, childrenMap, configuration, visited).ToList();
                visited.Remove(childId);
                yield return DependencyTree.AsPackage(dependency.Id, dependency.Version, childrenDependencies);
            }
            else
            {
                log.LogWarning("Unexpected dependency found in children array: {ChildId}", childId);
            }
        }
    }
}

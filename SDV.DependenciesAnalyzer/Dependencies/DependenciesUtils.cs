using Microsoft.Extensions.Logging;
using SDV.DependenciesAnalyzer.Interfaces;
using SDV.DependenciesAnalyzer.Models;

namespace SDV.DependenciesAnalyzer.Dependencies;

public class DependenciesUtils
{
    private readonly ILogger _log;
    private TreeGeneratorConfiguration _configuration;

    public DependenciesUtils(ILogger log)
    {
        _log = log;
        _configuration = new TreeGeneratorConfiguration(string.Empty, [], []);
    }
    
    public DependencyTree[] CreateDependencyTree(IExtractor extractor, TreeGeneratorConfiguration configuration)
    {
        _configuration = configuration;
        var rootTree = _configuration.IncludeDependentProjects
            ? extractor.DependentProjects().Select(p => DependencyTree.AsProject(p.Id, p.Version)).ToList()
            : [];
        var rootDependencies = extractor.DirectDependencies();
        var allDependencies = extractor.AllDependencies();
        var childrenMap = extractor.ChildrenMap();

        foreach (var rootId in rootDependencies.Where(_configuration.IsPackageEnabled))
        {
            if (allDependencies.TryGetValue(rootId, out var dependency))
            {
                var childrenDependencies = FindChildrenDependencies(rootId, allDependencies, childrenMap).ToList();
                var dependencyTree = DependencyTree.AsPackage(dependency.Id, dependency.Version, childrenDependencies);
                rootTree.Add(dependencyTree);
            }
            else
            {
                _log.LogWarning("Unexpected dependency found in root dependencies array: {RootId}", rootId);
            }
        }

        return rootTree.ToArray();
    }
    
    private IEnumerable<DependencyTree> FindChildrenDependencies(string id,
        IReadOnlyDictionary<string, IDependencyDetails> allDependencies,
        IReadOnlyDictionary<string, List<string>> childrenMap)
    {
        if (!childrenMap.TryGetValue(id, out var childArray))
        {
            yield break;
        }

        foreach (var childId in childArray.Where(_configuration.IsPackageEnabled))
        {
            if (allDependencies.TryGetValue(childId, out var dependency))
            {
                var childrenDependencies = FindChildrenDependencies(childId, allDependencies, childrenMap).ToList();
                yield return DependencyTree.AsPackage(dependency.Id, dependency.Version, childrenDependencies);
            }
            else
            {
                _log.LogWarning("Unexpected dependency found in children array: {ChildId}", childId);
            }
        }
    }
}

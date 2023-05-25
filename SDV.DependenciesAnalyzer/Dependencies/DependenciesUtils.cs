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
        _configuration = new TreeGeneratorConfiguration(string.Empty);
    }
    
    public DependencyTree[] CreateDependencyTree(IExtractor extractor, TreeGeneratorConfiguration configuration)
    {
        _configuration = configuration;
        var rootTree = _configuration.IncludeDependentProjects
            ? extractor.DependentProjects().Select(p => DependencyTree.AsProject(p.Id, p.Version)).ToList()
            : new List<DependencyTree>();
        var rootDependencies = extractor.DirectDependencies();
        var allDependencies = extractor.AllDependencies();
        var childrenMap = extractor.ChildrenMap();

        foreach (var rootId in rootDependencies)
        {
            if (!CanBeProcessed(rootId))
            {
                continue;
            }
            
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
        
        foreach (var childId in childArray)
        {
            if (!CanBeProcessed(childId))
            {
                continue;
            }
            
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
    
        
    private bool CanBeProcessed(string name)
    {
        if ( _configuration.Mode == PackageFilterMode.None || _configuration.PackagePrefixes.Length == 0)
        {
            return true;
        }

        var isMatched = _configuration.PackagePrefixes.Any(name.StartsWith);
        var shouldBeProcessed = _configuration.Mode == PackageFilterMode.Include ? isMatched : !isMatched;
        if (!shouldBeProcessed)
        {
            _log.LogDebug("Processing of package {Name} and its dependencies is skipped due to configuration",  name);
        }

        return shouldBeProcessed;
    }
}

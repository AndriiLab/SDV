using SDV.DependenciesAnalyzer.Interfaces;

namespace SDV.DependenciesAnalyzer.Models;

public class DependencyTree : IDependencyDetails
{
    public string Id { get; }
    public string Type { get; }
    public string Version { get; }
    public List<DependencyTree> Dependencies { get; }

    private DependencyTree(string id, string version, List<DependencyTree> dependencies, string type)
    {
        Id = id;
        Version = version;
        Dependencies = dependencies;
        Type = type;
    }
    
    public static DependencyTree AsProject(string id, string version)
    {
        return new DependencyTree(id, version, new List<DependencyTree>(0), "project");
    }
    
    public static DependencyTree AsPackage(string id, string version, List<DependencyTree> dependencies)
    {
        return new DependencyTree(id, version, dependencies, "package");
    }
}
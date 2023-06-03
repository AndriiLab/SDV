using SDV.DependenciesAnalyzer.Interfaces;

namespace SDV.DependenciesAnalyzer.Models;

public class DependencyTree : IDependencyDetails
{
    public string Id { get; }
    public DependencyType Type { get; }
    public string Version { get; }
    public List<DependencyTree> Dependencies { get; }

    private DependencyTree(string id, string version, List<DependencyTree> dependencies, DependencyType type)
    {
        Id = id;
        Version = version;
        Dependencies = dependencies;
        Type = type;
    }

    public static DependencyTree AsProject(string id, string version)
    {
        return new DependencyTree(id, version, new List<DependencyTree>(0), DependencyType.Project);
    }

    public static DependencyTree AsPackage(string id, string version, List<DependencyTree> dependencies)
    {
        return new DependencyTree(id, version, dependencies, DependencyType.Package);
    }
}

public enum DependencyType
{
    Project = 1,
    Package = 2,
}
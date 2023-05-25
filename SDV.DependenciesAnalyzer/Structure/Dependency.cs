using SDV.DependenciesAnalyzer.Interfaces;

namespace SDV.DependenciesAnalyzer.Structure;

public class Dependency : IDependencyDetails
{
    public Dependency(string id, string version)
    {
        Id = id;
        Version = version;
    }
    
    public string Id { get; }
    public string Version { get; }
}

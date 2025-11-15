using SDV.DependenciesAnalyzer.Interfaces;

namespace SDV.DependenciesAnalyzer.Structure;

public class Dependency(string id, string version) : IDependencyDetails
{
    public string Id { get; } = id;
    public string Version { get; } = version;
}

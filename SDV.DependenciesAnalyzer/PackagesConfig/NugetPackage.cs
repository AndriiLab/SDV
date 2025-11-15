using SDV.DependenciesAnalyzer.Common;

namespace SDV.DependenciesAnalyzer.PackagesConfig;

public class NugetPackage(string id, string version)
{
    public string Id { get; } = id;
    public string Version { get; } = version;
    public CaseInsensitiveMap<bool> Dependencies { get; } = new();
}

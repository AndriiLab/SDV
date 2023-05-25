using SDV.DependenciesAnalyzer.Common;

namespace SDV.DependenciesAnalyzer.PackagesConfig;

public class NugetPackage
{
    public string Id { get; }
    public string Version { get; }
    public CaseInsensitiveMap<bool> Dependencies { get; }

    public NugetPackage(string id, string version)
    {
        Id = id;
        Version = version;
        Dependencies = new CaseInsensitiveMap<bool>();
    }
}

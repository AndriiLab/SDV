namespace SDV.DependenciesAnalyzer.Models;

public class Project(string name, DependencyTree[] dependencies)
{
    public string Name { get; } = name;

    public DependencyTree[] Dependencies { get; } = dependencies;
}

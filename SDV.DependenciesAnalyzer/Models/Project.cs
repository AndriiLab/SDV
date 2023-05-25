namespace SDV.DependenciesAnalyzer.Models;

public class Project
{
    public Project(string name, DependencyTree[] dependencies)
    {
        Name = name;
        Dependencies = dependencies;
    }

    public string Name { get; }

    public DependencyTree[] Dependencies { get; }
}

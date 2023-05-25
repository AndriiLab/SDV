namespace SDV.DependenciesAnalyzer.PackagesConfig;

public class Dfs
{
    public Dfs(bool visited, bool notRoot, bool circular)
    {
        Visited = visited;
        NotRoot = notRoot;
        Circular = circular;
    }

    public bool Circular { get; set; }

    public bool NotRoot { get; set; }

    public bool Visited { get; set; }
}
namespace SDV.DependenciesAnalyzer.PackagesConfig;

public class Dfs(bool visited, bool notRoot, bool circular)
{
    public bool Circular { get; set; } = circular;
    public bool NotRoot { get; set; } = notRoot;
    public bool Visited { get; set; } = visited;
}
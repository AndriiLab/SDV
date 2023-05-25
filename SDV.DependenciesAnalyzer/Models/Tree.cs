namespace SDV.DependenciesAnalyzer.Models;

public class Tree
{
    public Tree(string solutionName)
    {
        SolutionName = solutionName;
        Projects = new List<Project>();
    }

    public string SolutionName { get; }
    public List<Project> Projects { get; }
}